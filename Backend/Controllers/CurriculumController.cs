using System.Security.Claims;
using EduMy.Backend.Data;
using EduMy.Backend.DTOs;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers;

[ApiController]
[Route("api")]
public class CurriculumController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly EduMy.Backend.Services.ICourseProgressService _progress;
    private readonly EduMy.Backend.Services.ILessonResourceStorage _storage;
    public CurriculumController(ApplicationDbContext context, EduMy.Backend.Services.ICourseProgressService progress, EduMy.Backend.Services.ILessonResourceStorage storage)
    {
        _context = context;
        _progress = progress;
        _storage = storage;
    }

    [HttpGet("courses/{courseId:int}/curriculum")]
    public async Task<IActionResult> GetCurriculum(int courseId)
    {
        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == courseId);
        if (course == null) return NotFound();
        var userId = CurrentUserId();
        var canManage = User.IsInRole("Admin") || userId == course.InstructorId;
        var enrolled = userId > 0 && await _context.Enrollments.AnyAsync(e => e.CourseId == courseId && e.UserId == userId);
        if (course.Status != "Published" && !canManage) return NotFound();
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Student") && !enrolled) return Forbid();

        var sections = await _context.CourseSections.AsNoTracking()
            .Where(s => s.CourseId == courseId)
            .Include(s => s.Lessons)
            .Include(s => s.Quizzes)
            .OrderBy(s => s.OrderIndex)
            .ToListAsync();

        return Ok(sections.Select(s => new
        {
            s.SectionId, s.CourseId, s.Title, s.OrderIndex,
            lessons = s.Lessons.OrderBy(l => l.OrderIndex).Where(l => canManage || !l.IsDraft).Select(l => new
            {
                l.LessonId, l.SectionId, l.Title, l.Duration, l.OrderIndex, l.IsPreview,
                l.ResourceType, l.OriginalFileName, l.ContentType, l.FileSizeBytes, l.UploadedAt,
                fileUrl = canManage || enrolled || l.IsPreview ? (l.FileUrl ?? l.VideoUrl) : null,
                videoUrl = canManage || enrolled || l.IsPreview ? (l.VideoUrl ?? l.FileUrl) : null,
                hasResource = l.ResourceType != "None" && l.ResourceType != "Reading" && (!string.IsNullOrWhiteSpace(l.FileUrl) || !string.IsNullOrWhiteSpace(l.VideoUrl) || !string.IsNullOrWhiteSpace(l.OriginalFileName)),
                resourceExists = (canManage || enrolled || l.IsPreview) && _storage.ResourceExists(l.FileUrl ?? l.VideoUrl),
                resourceEndpoint = $"/api/learning/lessons/{l.LessonId}/resource",
                isDraft = canManage && l.IsDraft
            }),
            quizzes = s.Quizzes.Select(q => new { q.QuizId, q.Title })
        }));
    }

    [Authorize(Roles = "Instructor")]
    [HttpPost("courses/{courseId:int}/sections")]
    public async Task<IActionResult> CreateSection(int courseId, SectionUpsertDto dto)
    {
        var course = await OwnedCourse(courseId);
        if (course == null) return NotFound();
        var canManage = CanManage(course);
        var title = dto.Title.Trim();
        if (title.Length == 0) return BadRequest(new { message = "Section title is required." });
        if (!canManage)
            return StatusCode(403, new { code = "FORBIDDEN", message = "Bạn không có quyền quản lý khóa học này." });
        var existingMax = await _context.CourseSections.Where(s => s.CourseId == courseId).MaxAsync(s => (int?)s.OrderIndex) ?? 0;
        var order = dto.OrderIndex > 0 ? dto.OrderIndex : existingMax + 1;
        var section = new CourseSection { CourseId = courseId, Title = title, OrderIndex = order };
        _context.CourseSections.Add(section);
        await _context.SaveChangesAsync();
        await NormalizeSectionOrdersAsync(courseId);
        return Ok(new { section.SectionId, section.CourseId, section.Title, section.OrderIndex, lessons = Array.Empty<object>() });
    }

    [Authorize(Roles = "Instructor")]
    [HttpPut("sections/{sectionId:int}")]
    public async Task<IActionResult> UpdateSection(int sectionId, SectionUpsertDto dto)
    {
        var section = await _context.CourseSections.Include(s => s.Course).FirstOrDefaultAsync(s => s.SectionId == sectionId);
        if (section == null) return NotFound();
        if (!CanManage(section.Course)) return Forbid();
        section.Title = dto.Title.Trim();
        section.OrderIndex = dto.OrderIndex > 0 ? dto.OrderIndex : section.OrderIndex;
        await _context.SaveChangesAsync();
        await NormalizeSectionOrdersAsync(section.CourseId);
        return Ok(new { section.SectionId, section.Title, section.OrderIndex });
    }

    [Authorize(Roles = "Instructor")]
    [HttpDelete("sections/{sectionId:int}")]
    public async Task<IActionResult> DeleteSection(int sectionId)
    {
        var section = await _context.CourseSections.Include(s => s.Course).Include(s => s.Lessons).FirstOrDefaultAsync(s => s.SectionId == sectionId);
        if (section == null) return NotFound();
        if (!CanManage(section.Course)) return Forbid();
        var courseId = section.CourseId;
        _context.CourseSections.Remove(section);
        await _context.SaveChangesAsync();
        await NormalizeSectionOrdersAsync(courseId);
        await _progress.RecalculateCourseEnrollmentsAsync(courseId);
        return NoContent();
    }

    [Authorize(Roles = "Instructor")]
    [HttpPost("sections/{sectionId:int}/lessons")]
    public async Task<IActionResult> CreateLesson(int sectionId, LessonUpsertDto dto)
    {
        var section = await _context.CourseSections.Include(s => s.Course).FirstOrDefaultAsync(s => s.SectionId == sectionId);
        if (section == null) return NotFound();
        if (!CanManage(section.Course)) return Forbid();
        dto.IsPreview = false;
        var existingMax = await _context.Lessons.Where(l => l.SectionId == sectionId).MaxAsync(l => (int?)l.OrderIndex) ?? 0;
        dto.OrderIndex = dto.OrderIndex > 0 ? dto.OrderIndex : existingMax + 1;
        var lesson = MapLesson(new Lesson { SectionId = sectionId }, dto);
        lesson.IsPreview = false;
        _context.Lessons.Add(lesson);
        await _context.SaveChangesAsync();
        await NormalizeLessonOrdersAsync(sectionId);
        await _progress.RecalculateCourseEnrollmentsAsync(section.CourseId);
        return Ok(ToLesson(lesson));
    }

    [Authorize(Roles = "Instructor")]
    [HttpPut("lessons/{lessonId:int}")]
    public async Task<IActionResult> UpdateLesson(int lessonId, LessonUpsertDto dto)
    {
        var lesson = await _context.Lessons.Include(l => l.Section)!.ThenInclude(s => s.Course).FirstOrDefaultAsync(l => l.LessonId == lessonId);
        if (lesson?.Section == null) return NotFound();
        if (!CanManage(lesson.Section.Course)) return Forbid();
        dto.IsPreview = false;
        dto.OrderIndex = dto.OrderIndex > 0 ? dto.OrderIndex : lesson.OrderIndex;
        MapLesson(lesson, dto);
        lesson.IsPreview = false;
        await _context.SaveChangesAsync();
        await NormalizeLessonOrdersAsync(lesson.SectionId);
        await _progress.RecalculateCourseEnrollmentsAsync(lesson.Section.CourseId);
        return Ok(ToLesson(lesson));
    }

    [Authorize(Roles = "Instructor")]
    [HttpDelete("lessons/{lessonId:int}")]
    public async Task<IActionResult> DeleteLesson(int lessonId)
    {
        var lesson = await _context.Lessons.Include(l => l.Section)!.ThenInclude(s => s.Course).FirstOrDefaultAsync(l => l.LessonId == lessonId);
        if (lesson?.Section == null) return NotFound();
        if (!CanManage(lesson.Section.Course)) return Forbid();
        var courseId = lesson.Section.CourseId;
        var sectionId = lesson.SectionId;
        _context.Lessons.Remove(lesson);
        await _context.SaveChangesAsync();
        await NormalizeLessonOrdersAsync(sectionId);
        await _progress.RecalculateCourseEnrollmentsAsync(courseId);
        return NoContent();
    }

    [Authorize(Roles = "Instructor")]
    [HttpPost("courses/{courseId:int}/curriculum/sample")]
    public async Task<IActionResult> CreateSample(int courseId)
    {
        var course = await OwnedCourse(courseId);
        if (course == null) return NotFound();
        if (!CanManage(course)) return Forbid();
        if (await _context.CourseSections.AnyAsync(s => s.CourseId == courseId))
            return Conflict(new { message = "Khóa học đã có curriculum; dữ liệu mẫu không được tạo trùng." });

        var sectionTitles = new[] { "Giới thiệu", "Nội dung cốt lõi", "Thực hành" };
        for (var i = 0; i < sectionTitles.Length; i++)
        {
            var section = new CourseSection { CourseId = courseId, Title = sectionTitles[i], OrderIndex = i + 1 };
            section.Lessons.Add(new Lesson
            {
                Title = $"Bài học mẫu {i + 1}", OrderIndex = 1, ResourceType = "Document", IsDraft = true, IsPreview = false
            });
            _context.CourseSections.Add(section);
        }
        await _context.SaveChangesAsync();
        await _progress.RecalculateCourseEnrollmentsAsync(courseId);
        return Ok(new { message = "Đã tạo 3 chương mẫu ở trạng thái bản nháp." });
    }

    private int CurrentUserId() => int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
    private bool CanManage(Course course) => User.IsInRole("Instructor") && course.InstructorId == CurrentUserId();
    private Task<Course?> OwnedCourse(int id) => _context.Courses.FirstOrDefaultAsync(c => c.CourseId == id);

    private async Task NormalizeSectionOrdersAsync(int courseId)
    {
        var sections = await _context.CourseSections
            .Where(s => s.CourseId == courseId)
            .OrderBy(s => s.OrderIndex)
            .ThenBy(s => s.SectionId)
            .ToListAsync();

        for (int i = 0; i < sections.Count; i++)
        {
            sections[i].OrderIndex = i + 1;
        }
        await _context.SaveChangesAsync();
    }

    private async Task NormalizeLessonOrdersAsync(int sectionId)
    {
        var lessons = await _context.Lessons
            .Where(l => l.SectionId == sectionId)
            .OrderBy(l => l.OrderIndex)
            .ThenBy(l => l.LessonId)
            .ToListAsync();

        for (int i = 0; i < lessons.Count; i++)
        {
            lessons[i].OrderIndex = i + 1;
        }
        await _context.SaveChangesAsync();
    }

    private static Lesson MapLesson(Lesson lesson, LessonUpsertDto dto)
    {
        lesson.Title = dto.Title.Trim();
        lesson.Duration = dto.Duration;
        lesson.OrderIndex = dto.OrderIndex;
        lesson.IsPreview = false;

        if (!string.IsNullOrWhiteSpace(dto.FileUrl))
        {
            lesson.FileUrl = dto.FileUrl.Trim();
            lesson.OriginalFileName = dto.OriginalFileName?.Trim();
            lesson.ContentType = dto.ContentType?.Trim();
            lesson.ResourceType = NormalizeResourceType(dto.ResourceType, lesson.FileUrl, lesson.ContentType);
            lesson.VideoUrl = lesson.ResourceType.Equals("Video", StringComparison.OrdinalIgnoreCase) ? lesson.FileUrl : null;
            lesson.FileSizeBytes = dto.FileSizeBytes;
            lesson.UploadedAt = DateTime.UtcNow;
        }
        else if (dto.ResourceType != null && (dto.ResourceType.Equals("None", StringComparison.OrdinalIgnoreCase) || dto.ResourceType.Equals("Reading", StringComparison.OrdinalIgnoreCase)))
        {
            lesson.FileUrl = null;
            lesson.VideoUrl = null;
            lesson.OriginalFileName = null;
            lesson.ContentType = null;
            lesson.FileSizeBytes = null;
            lesson.UploadedAt = null;
            lesson.ResourceType = dto.ResourceType;
        }

        lesson.IsDraft = dto.IsDraft;
        return lesson;
    }

    private static string NormalizeResourceType(string? dtoResourceType, string? fileUrl, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(fileUrl)) return "None";
        var ext = System.IO.Path.GetExtension(fileUrl).ToLowerInvariant();
        var mime = (contentType ?? "").ToLowerInvariant();

        if (mime.StartsWith("video/") || new[] { ".mp4", ".webm", ".ogg", ".mov", ".m4v" }.Contains(ext))
            return "Video";
        if (mime.StartsWith("image/") || new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp" }.Contains(ext))
            return "Image";
        if (mime.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) || ext == ".pdf")
            return "Pdf";
        if (new[] { ".ppt", ".pptx" }.Contains(ext) ||
            mime.Contains("presentation") || mime.Contains("powerpoint") ||
            mime.Equals("application/vnd.ms-powerpoint", StringComparison.OrdinalIgnoreCase) ||
            mime.Equals("application/vnd.openxmlformats-officedocument.presentationml.presentation", StringComparison.OrdinalIgnoreCase))
            return "PowerPoint";
        if (new[] { ".doc", ".docx", ".xls", ".xlsx", ".txt", ".csv", ".odt", ".ods", ".odp" }.Contains(ext) ||
            mime.Contains("officedocument") || mime.Contains("word") || mime.Contains("excel") || mime.StartsWith("text/"))
            return "Document";

        var rawType = dtoResourceType?.Trim();
        if (!string.IsNullOrWhiteSpace(rawType) && !rawType.Equals("File", StringComparison.OrdinalIgnoreCase))
        {
            return rawType;
        }

        return "File";
    }

    private object ToLesson(Lesson l) => new
    {
        l.LessonId, l.SectionId, l.Title, l.Duration, l.OrderIndex, IsPreview = false, l.ResourceType,
        fileUrl = l.FileUrl ?? l.VideoUrl,
        videoUrl = l.VideoUrl ?? l.FileUrl,
        l.OriginalFileName, l.ContentType, l.FileSizeBytes, l.UploadedAt, l.IsDraft,
        hasResource = l.ResourceType != "None" && l.ResourceType != "Reading" && (!string.IsNullOrWhiteSpace(l.FileUrl) || !string.IsNullOrWhiteSpace(l.VideoUrl)),
        resourceExists = _storage.ResourceExists(l.FileUrl ?? l.VideoUrl),
        resourceEndpoint = $"/api/learning/lessons/{l.LessonId}/resource"
    };
}

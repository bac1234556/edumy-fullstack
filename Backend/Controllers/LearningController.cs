using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using EduMy.Backend.Data;
using EduMy.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers;

[ApiController]
[Route("api/my-courses")]
[Authorize]
public sealed class LearningController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ICourseProgressService _progress;
    private readonly ILessonResourceStorage _storage;
    public LearningController(ApplicationDbContext db, ICourseProgressService progress, ILessonResourceStorage storage)
    {
        _db = db;
        _progress = progress;
        _storage = storage;
    }

    [HttpGet("{courseId:int}/learn")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Learn(int courseId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var progress = await _progress.GetCourseProgressAsync(userId, courseId);
        if (progress == null) return Forbid();
        var course = await _db.Courses.AsNoTracking().Include(c => c.Instructor)
            .FirstOrDefaultAsync(c => c.CourseId == courseId);
        if (course == null) return NotFound();
        var completedIds = progress.CompletedLessonIds.ToHashSet();
        var sections = await _db.CourseSections.AsNoTracking().Where(s => s.CourseId == courseId)
            .Include(s => s.Lessons).Include(s => s.Quizzes).OrderBy(s => s.OrderIndex).ToListAsync();
        return Ok(new
        {
            course = new { course.CourseId, course.Title, course.ThumbnailUrl, instructorName = course.Instructor!.FullName },
            sections = sections.Select(s => new
            {
                s.SectionId, s.Title, s.OrderIndex,
                lessons = s.Lessons.Where(l => !l.IsDraft).OrderBy(l => l.OrderIndex).Select(l => new
                {
                    l.LessonId, l.SectionId, l.Title, l.Duration, l.OrderIndex, l.IsPreview, l.ResourceType,
                    fileUrl = l.FileUrl ?? l.VideoUrl,
                    videoUrl = l.VideoUrl ?? l.FileUrl,
                    l.OriginalFileName, l.ContentType, l.FileSizeBytes,
                    hasResource = l.ResourceType != "None" && l.ResourceType != "Reading" && (!string.IsNullOrWhiteSpace(l.FileUrl) || !string.IsNullOrWhiteSpace(l.VideoUrl) || !string.IsNullOrWhiteSpace(l.OriginalFileName)),
                    resourceExists = _storage.ResourceExists(l.FileUrl ?? l.VideoUrl),
                    resourceEndpoint = $"/api/learning/lessons/{l.LessonId}/resource",
                    isCompleted = completedIds.Contains(l.LessonId)
                }),
                quizzes = s.Quizzes.Select(q => new { q.QuizId, q.Title })
            }),
            progress.TotalLessons,
            progress.CompletedLessons,
            progress.ProgressPercentage,
            progress.CompletedAt,
            progress.CertificateUrl
        });
    }

    [HttpGet("{courseId:int}/continue-lesson")]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> GetContinueLesson(int courseId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();

        var lessons = await _db.Lessons
            .AsNoTracking()
            .Include(l => l.Section)
            .Where(l => l.Section!.CourseId == courseId && !l.IsDraft)
            .OrderBy(l => l.Section!.OrderIndex)
            .ThenBy(l => l.OrderIndex)
            .ToListAsync();

        if (lessons.Count == 0) return NotFound(new { message = "Khóa học chưa có bài giảng." });

        var progress = await _progress.GetCourseProgressAsync(userId, courseId);
        if (progress == null) return Forbid();

        var completedIds = progress.CompletedLessonIds.ToHashSet();
        var nextLesson = lessons.FirstOrDefault(l => !completedIds.Contains(l.LessonId));

        if (nextLesson == null)
        {
            return Ok(new { completedAll = true, lessonId = lessons.Last().LessonId });
        }

        return Ok(new { completedAll = false, lessonId = nextLesson.LessonId });
    }

    [HttpGet("/api/learning/lessons/{lessonId:int}/resource")]
    [HttpGet("/api/my-courses/lessons/{lessonId:int}/resource")]
    [HttpGet("lessons/{lessonId:int}/resource")]
    public async Task<IActionResult> GetLessonResource(int lessonId)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized(new { code = "UNAUTHORIZED", message = "Chưa đăng nhập." });

        var lesson = await _db.Lessons.AsNoTracking().FirstOrDefaultAsync(l => l.LessonId == lessonId);
        if (lesson == null)
            return NotFound(new { code = "LESSON_NOT_FOUND", message = "Bài học không tồn tại." });

        var section = await _db.CourseSections.AsNoTracking().FirstOrDefaultAsync(s => s.SectionId == lesson.SectionId);
        if (section == null)
            return NotFound(new { code = "SECTION_NOT_FOUND", message = "Chương học không tồn tại." });

        var course = await _db.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == section.CourseId);
        if (course == null)
            return NotFound(new { code = "COURSE_NOT_FOUND", message = "Khóa học không tồn tại." });

        var canManage = User.IsInRole("Admin") || userId == course.InstructorId;
        var enrolled = await _db.Enrollments.AnyAsync(e => e.CourseId == course.CourseId && e.UserId == userId);

        if (!canManage && !enrolled && !lesson.IsPreview)
        {
            return StatusCode(403, new { code = "FORBIDDEN", message = "Bạn chưa đăng ký khóa học này." });
        }

        if (lesson.IsDraft && !canManage)
        {
            return NotFound(new { code = "LESSON_DRAFT", message = "Bài học chưa được xuất bản." });
        }

        var relativeUrl = lesson.FileUrl ?? lesson.VideoUrl;
        if (string.IsNullOrWhiteSpace(relativeUrl))
        {
            return NotFound(new { code = "LESSON_RESOURCE_NOT_ATTACHED", message = "Bài học chưa có tài nguyên đính kèm." });
        }

        var physicalPath = _storage.GetPhysicalPath(relativeUrl);
        if (string.IsNullOrWhiteSpace(physicalPath) || !System.IO.File.Exists(physicalPath))
        {
            return StatusCode(410, new { code = "LESSON_RESOURCE_MISSING", message = "Tài nguyên của bài học không còn tồn tại." });
        }

        var contentType = _storage.GetContentType(physicalPath, lesson.ContentType);
        var ext = System.IO.Path.GetExtension(physicalPath).ToLowerInvariant();

        var isVideo = contentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase) || new[] { ".mp4", ".webm", ".ogg", ".mov" }.Contains(ext);
        var isImage = contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) || new[] { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".svg" }.Contains(ext);
        var isPdf = contentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase) || ext == ".pdf";

        if (isImage)
        {
            return PhysicalFile(physicalPath, contentType, enableRangeProcessing: false);
        }
        if (isVideo || isPdf)
        {
            return PhysicalFile(physicalPath, contentType, enableRangeProcessing: true);
        }

        var downloadName = lesson.OriginalFileName ?? System.IO.Path.GetFileName(physicalPath);
        return PhysicalFile(physicalPath, contentType, downloadName, enableRangeProcessing: true);
    }

    [HttpPost("{courseId:int}/lessons/{lessonId:int}/complete")]
    [Authorize(Roles = "Student")]
    public Task<IActionResult> Complete(int courseId, int lessonId) => Set(courseId, lessonId, true);

    [HttpDelete("{courseId:int}/lessons/{lessonId:int}/complete")]
    [Authorize(Roles = "Student")]
    public Task<IActionResult> Uncomplete(int courseId, int lessonId) => Set(courseId, lessonId, false);

    private async Task<IActionResult> Set(int courseId, int lessonId, bool isCompleted)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        try
        {
            var progress = await _progress.SetLessonCompletionAsync(userId, courseId, lessonId, isCompleted);
            if (progress == null) return Forbid();
            return Ok(new { lessonId, isCompleted, progress.TotalLessons, progress.CompletedLessons, progress.ProgressPercentage, progress.CompletedAt });
        }
        catch (InvalidOperationException ex) when (ex.Message == "LESSON_NOT_LEARNABLE")
        {
            return BadRequest(new { code = ex.Message, message = "Lesson does not belong to this course or is still a draft." });
        }
    }
}

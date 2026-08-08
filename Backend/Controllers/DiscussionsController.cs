using System.Security.Claims;
using EduMy.Backend.Data;
using EduMy.Backend.DTOs;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers;

[ApiController]
[Authorize(Roles = "Student,Instructor,Admin")]
[Route("api")]
public class DiscussionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly EduMy.Backend.Services.INotificationService _notificationService;
    public DiscussionsController(ApplicationDbContext context, EduMy.Backend.Services.INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    [HttpGet("courses/{courseId:int}/discussions")]
    public async Task<IActionResult> List(int courseId, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (!await CanAccess(courseId)) return Forbid();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 50);
        var query = _context.CourseDiscussionThreads.AsNoTracking().Where(t => t.CourseId == courseId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 100) return BadRequest(new { message = "Search is too long." });
            query = query.Where(t => t.Title.Contains(term));
        }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(t => t.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new
            {
                t.Id, t.CourseId, t.Title, t.IsClosed, t.CreatedAt, t.UpdatedAt,
                createdBy = new { t.CreatedByUser.UserId, t.CreatedByUser.FullName, t.CreatedByUser.AvatarUrl, t.CreatedByUser.Role },
                excerpt = t.Messages.OrderBy(m => m.CreatedAt).Select(m => m.Content).FirstOrDefault(),
                messageCount = t.Messages.Count,
                answerCount = t.Messages.Count > 0 ? t.Messages.Count - 1 : 0
            }).ToListAsync();
        return Ok(new PagedResponseDto<object>(items.Cast<object>().ToList(), page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
    }

    [HttpGet("discussions/{threadId:int}")]
    public async Task<IActionResult> Detail(int threadId)
    {
        var thread = await _context.CourseDiscussionThreads.AsNoTracking()
            .Where(t => t.Id == threadId)
            .Select(t => new
            {
                t.Id, t.CourseId, t.Title, t.IsClosed, t.CreatedAt, t.UpdatedAt,
                createdBy = new { t.CreatedByUser.UserId, t.CreatedByUser.FullName, t.CreatedByUser.AvatarUrl, t.CreatedByUser.Role },
                messages = t.Messages.OrderBy(m => m.CreatedAt).Select(m => new
                {
                    m.Id, m.UserId, m.Content, m.CreatedAt, m.UpdatedAt, m.IsInstructorMessage,
                    user = new { m.User.UserId, m.User.FullName, m.User.AvatarUrl, m.User.Role }
                })
            }).FirstOrDefaultAsync();
        if (thread == null) return NotFound();
        if (!await CanAccess(thread.CourseId)) return Forbid();
        return Ok(thread);
    }

    [HttpPost("courses/{courseId:int}/discussions")]
    public async Task<IActionResult> Create(int courseId, DiscussionThreadCreateDto dto)
    {
        var userId = UserId();
        if (!await _context.Courses.AnyAsync(c => c.CourseId == courseId))
            return NotFound(new { code = "COURSE_NOT_FOUND", message = "Không tìm thấy khóa học." });
        if (!await CanAccess(courseId))
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "NOT_ENROLLED", message = "Bạn cần đăng ký khóa học để sử dụng chức năng này." });
        var title = dto.Title.Trim(); var content = dto.Content.Trim();
        var errors = new List<string>();
        if (title.Length is < 5 or > 200) errors.Add("Tiêu đề câu hỏi phải có từ 5 đến 200 ký tự.");
        if (content.Length is < 10 or > 4000) errors.Add("Nội dung câu hỏi phải có từ 10 đến 4000 ký tự.");
        if (errors.Count > 0) return BadRequest(new { code = "VALIDATION_ERROR", message = "Vui lòng kiểm tra nội dung câu hỏi.", errors });
        var thread = new CourseDiscussionThread { CourseId = courseId, CreatedByUserId = userId, Title = title };
        var isInstructor = User.IsInRole("Admin") || await _context.Courses.AnyAsync(c => c.CourseId == courseId && c.InstructorId == userId);
        thread.Messages.Add(new CourseDiscussionMessage { UserId = userId, Content = content, IsInstructorMessage = isInstructor });
        _context.CourseDiscussionThreads.Add(thread);
        await _context.SaveChangesAsync();

        var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == courseId);
        if (course != null && course.InstructorId != userId)
        {
            var instructorUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == course.InstructorId);
            var targetUrl = _notificationService.BuildNotificationTargetUrl(
                recipientRole: instructorUser?.Role ?? "Instructor",
                courseId: courseId,
                discussionThreadId: thread.Id
            );

            await _notificationService.CreateNotificationAsync(
                recipientUserId: course.InstructorId,
                actorUserId: userId,
                type: "NewDiscussionQuestion",
                title: "Câu hỏi mới trong khóa học",
                message: $"Học viên vừa tạo câu hỏi mới '{title}' trong khóa học {course.Title}.",
                targetUrl: targetUrl,
                courseId: courseId,
                discussionThreadId: thread.Id
            );
        }

        return CreatedAtAction(nameof(Detail), new { threadId = thread.Id }, new { thread.Id });
    }

    [HttpPost("discussions/{threadId:int}/messages")]
    public async Task<IActionResult> AddMessage(int threadId, DiscussionMessageCreateDto dto)
    {
        var thread = await _context.CourseDiscussionThreads.Include(t => t.Course).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return NotFound();
        if (!await CanAccess(thread.CourseId))
            return StatusCode(StatusCodes.Status403Forbidden, new { code = "NOT_ENROLLED", message = "Bạn cần đăng ký khóa học để sử dụng chức năng này." });
        if (thread.IsClosed)
            return Conflict(new { code = "THREAD_CLOSED", message = "Thảo luận đã đóng và không nhận thêm phản hồi." });
        var content = dto.Content.Trim();
        if (content.Length is < 2 or > 4000) return BadRequest(new { code = "VALIDATION_ERROR", message = "Phản hồi phải có từ 2 đến 4000 ký tự." });
        var actorUserId = UserId();
        var isInstructor = User.IsInRole("Admin") || actorUserId == thread.Course.InstructorId;
        var message = new CourseDiscussionMessage { ThreadId = threadId, UserId = actorUserId, Content = content, IsInstructorMessage = isInstructor };
        _context.CourseDiscussionMessages.Add(message);
        thread.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        var recipients = new HashSet<int>();
        if (thread.Course != null && thread.Course.InstructorId != actorUserId)
        {
            recipients.Add(thread.Course.InstructorId);
        }
        if (thread.CreatedByUserId != actorUserId)
        {
            recipients.Add(thread.CreatedByUserId);
        }

        var otherParticipants = await _context.CourseDiscussionMessages
            .Where(m => m.ThreadId == threadId && m.UserId != actorUserId)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync();
        foreach (var participantId in otherParticipants)
        {
            recipients.Add(participantId);
        }

        foreach (var recipientId in recipients)
        {
            var recipientUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == recipientId);
            var targetUrl = _notificationService.BuildNotificationTargetUrl(
                recipientRole: recipientUser?.Role ?? "Student",
                courseId: thread.CourseId,
                discussionThreadId: thread.Id,
                discussionMessageId: message.Id
            );

            await _notificationService.CreateNotificationAsync(
                recipientUserId: recipientId,
                actorUserId: actorUserId,
                type: "NewDiscussionReply",
                title: isInstructor ? "Giảng viên đã trả lời câu hỏi" : "Có phản hồi mới trong thảo luận",
                message: $"Có phản hồi mới trong câu hỏi '{thread.Title}'.",
                targetUrl: targetUrl,
                courseId: thread.CourseId,
                discussionThreadId: thread.Id,
                discussionMessageId: message.Id
            );
        }

        return Ok(new { message.Id, message.UserId, message.Content, message.CreatedAt, message.IsInstructorMessage });
    }

    [Authorize(Roles = "Instructor,Admin")]
    [HttpPut("discussions/{threadId:int}/status")]
    public async Task<IActionResult> Status(int threadId, DiscussionStatusDto dto)
    {
        var thread = await _context.CourseDiscussionThreads.Include(t => t.Course).FirstOrDefaultAsync(t => t.Id == threadId);
        if (thread == null) return NotFound();
        if (!User.IsInRole("Admin") && UserId() != thread.Course.InstructorId) return Forbid();
        thread.IsClosed = dto.IsClosed; thread.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { thread.Id, thread.IsClosed });
    }

    private int UserId() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private async Task<bool> CanAccess(int courseId)
    {
        if (User.IsInRole("Admin")) return true;
        var userId = UserId();
        if (User.IsInRole("Instructor")) return await _context.Courses.AnyAsync(c => c.CourseId == courseId && c.InstructorId == userId);
        return await _context.Enrollments.AnyAsync(e => e.CourseId == courseId && e.UserId == userId);
    }
}

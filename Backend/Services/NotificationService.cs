using EduMy.Backend.Data;
using EduMy.Backend.DTOs;
using EduMy.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Notification?> CreateNotificationAsync(
        int recipientUserId,
        int? actorUserId,
        string type,
        string title,
        string message,
        string targetUrl,
        int? courseId = null,
        int? reviewId = null,
        int? reviewReplyId = null,
        int? discussionThreadId = null,
        int? discussionMessageId = null)
    {
        // Rule: Never create self-notifications
        if (actorUserId.HasValue && actorUserId.Value == recipientUserId)
        {
            return null;
        }

        // Avoid duplicate active notifications for identical entity actions within a short time window (1 minute)
        var exists = await _context.Notifications.AnyAsync(n =>
            n.RecipientUserId == recipientUserId &&
            n.ActorUserId == actorUserId &&
            n.Type == type &&
            n.CourseId == courseId &&
            n.ReviewId == reviewId &&
            n.ReviewReplyId == reviewReplyId &&
            n.DiscussionThreadId == discussionThreadId &&
            n.DiscussionMessageId == discussionMessageId &&
            n.CreatedAt >= DateTime.UtcNow.AddMinutes(-1));

        if (exists)
        {
            return null;
        }

        var notification = new Notification
        {
            RecipientUserId = recipientUserId,
            ActorUserId = actorUserId,
            Type = type,
            Title = title,
            Message = message,
            TargetUrl = targetUrl,
            CourseId = courseId,
            ReviewId = reviewId,
            ReviewReplyId = reviewReplyId,
            DiscussionThreadId = discussionThreadId,
            DiscussionMessageId = discussionMessageId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();
        return notification;
    }

    public async Task<PagedResponseDto<NotificationResponseDto>> GetUserNotificationsAsync(int userId, int page = 1, int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _context.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId);

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationResponseDto
            {
                NotificationId = n.NotificationId,
                Type = n.Type,
                Title = n.Title,
                Message = n.Message,
                TargetUrl = n.TargetUrl,
                CourseId = n.CourseId,
                ReviewId = n.ReviewId,
                ReviewReplyId = n.ReviewReplyId,
                DiscussionThreadId = n.DiscussionThreadId,
                DiscussionMessageId = n.DiscussionMessageId,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt,
                Actor = n.ActorUser != null ? new NotificationActorDto
                {
                    UserId = n.ActorUser.UserId,
                    FullName = n.ActorUser.FullName,
                    AvatarUrl = n.ActorUser.AvatarUrl,
                    Role = n.ActorUser.Role
                } : null
            })
            .ToListAsync();

        return new PagedResponseDto<NotificationResponseDto>(items, page, pageSize, totalItems, totalPages);
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.RecipientUserId == userId);

        if (notification == null) return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(int userId)
    {
        var unreadList = await _context.Notifications
            .Where(n => n.RecipientUserId == userId && !n.IsRead)
            .ToListAsync();

        if (unreadList.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var item in unreadList)
        {
            item.IsRead = true;
            item.ReadAt = now;
        }

        await _context.SaveChangesAsync();
        return unreadList.Count;
    }

    public string BuildNotificationTargetUrl(
        string recipientRole,
        int courseId,
        int? reviewId = null,
        int? reviewReplyId = null,
        int? discussionThreadId = null,
        int? discussionMessageId = null)
    {
        var role = (recipientRole ?? "").Trim();

        if (discussionThreadId.HasValue)
        {
            if (role.Equals("Instructor", StringComparison.OrdinalIgnoreCase))
            {
                var url = $"/instructor/courses/{courseId}/discussions?thread={discussionThreadId.Value}";
                if (discussionMessageId.HasValue) url += $"&message={discussionMessageId.Value}";
                return url;
            }
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            {
                var url = $"/admin/courses/{courseId}/preview?discussion={discussionThreadId.Value}";
                if (discussionMessageId.HasValue) url += $"&message={discussionMessageId.Value}";
                return url;
            }
            {
                var url = $"/my-courses/{courseId}/learn?discussion={discussionThreadId.Value}";
                if (discussionMessageId.HasValue) url += $"&message={discussionMessageId.Value}";
                return url;
            }
        }

        if (reviewReplyId.HasValue)
        {
            return $"/courses/{courseId}#reply-{reviewReplyId.Value}";
        }

        if (reviewId.HasValue)
        {
            return $"/courses/{courseId}#review-{reviewId.Value}";
        }

        if (role.Equals("Instructor", StringComparison.OrdinalIgnoreCase))
        {
            return $"/instructor/courses/{courseId}/preview";
        }
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            return $"/admin/courses/{courseId}/preview";
        }

        return $"/courses/{courseId}";
    }
}

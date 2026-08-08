using EduMy.Backend.DTOs;
using EduMy.Backend.Models;

namespace EduMy.Backend.Services;

public interface INotificationService
{
    Task<Notification?> CreateNotificationAsync(
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
        int? discussionMessageId = null);

    Task<PagedResponseDto<NotificationResponseDto>> GetUserNotificationsAsync(int userId, int page = 1, int pageSize = 20);
    Task<int> GetUnreadCountAsync(int userId);
    Task<bool> MarkAsReadAsync(int notificationId, int userId);
    Task<int> MarkAllAsReadAsync(int userId);
    string BuildNotificationTargetUrl(string recipientRole, int courseId, int? reviewId = null, int? reviewReplyId = null, int? discussionThreadId = null, int? discussionMessageId = null);
}

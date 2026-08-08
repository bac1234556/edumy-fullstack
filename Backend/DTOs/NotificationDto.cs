namespace EduMy.Backend.DTOs;

public class NotificationActorDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class NotificationResponseDto
{
    public int NotificationId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string TargetUrl { get; set; } = string.Empty;
    public int? CourseId { get; set; }
    public int? ReviewId { get; set; }
    public int? ReviewReplyId { get; set; }
    public int? DiscussionThreadId { get; set; }
    public int? DiscussionMessageId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public NotificationActorDto? Actor { get; set; }
}

public class UnreadCountDto
{
    public int UnreadCount { get; set; }
}

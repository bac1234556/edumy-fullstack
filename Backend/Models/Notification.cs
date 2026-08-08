using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.Models;

public class Notification
{
    public int NotificationId { get; set; }
    public int RecipientUserId { get; set; }
    public int? ActorUserId { get; set; }
    
    [Required, MaxLength(50)]
    public string Type { get; set; } = string.Empty;
    
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
    
    [Required, MaxLength(500)]
    public string TargetUrl { get; set; } = string.Empty;
    
    public int? CourseId { get; set; }
    public int? ReviewId { get; set; }
    public int? ReviewReplyId { get; set; }
    public int? DiscussionThreadId { get; set; }
    public int? DiscussionMessageId { get; set; }
    
    public bool IsRead { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    public User RecipientUser { get; set; } = null!;
    public User? ActorUser { get; set; }
}

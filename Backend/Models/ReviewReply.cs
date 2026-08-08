using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.Models;

public class ReviewReply
{
    public int ReviewReplyId { get; set; }
    public int ReviewId { get; set; }
    public int UserId { get; set; }
    [MaxLength(2000)] public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Review Review { get; set; } = null!;
    public User User { get; set; } = null!;
}

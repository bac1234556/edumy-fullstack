using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.Models;

public class CourseComment
{
    public int CourseCommentId { get; set; }
    public int CourseId { get; set; }
    public int UserId { get; set; }
    [MaxLength(2000)] public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Course Course { get; set; } = null!;
    public User User { get; set; } = null!;
}

using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.Models;

public class CourseDiscussionMessage
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int UserId { get; set; }
    [MaxLength(4000)] public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsInstructorMessage { get; set; }

    public CourseDiscussionThread Thread { get; set; } = null!;
    public User User { get; set; } = null!;
}

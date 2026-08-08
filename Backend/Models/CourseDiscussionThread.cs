using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.Models;

public class CourseDiscussionThread
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int CreatedByUserId { get; set; }
    [MaxLength(200)] public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsClosed { get; set; }

    public Course Course { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<CourseDiscussionMessage> Messages { get; set; } = new List<CourseDiscussionMessage>();
}

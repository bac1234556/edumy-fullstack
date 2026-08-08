using System;

namespace EduMy.Backend.Models
{
    public class UserActivity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string ActivityType { get; set; } = string.Empty;
        public string? ResourceId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

using System;

namespace EduMy.Backend.Models
{
    public class Wishlist
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;
        
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}

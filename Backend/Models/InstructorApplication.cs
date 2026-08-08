using System;

namespace EduMy.Backend.Models
{
    public class InstructorApplication
    {
        public int InstructorApplicationId { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string Introduction { get; set; } = string.Empty;
        public string Expertise { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
        public int? ReviewedByAdminId { get; set; }
        public User? ReviewedByAdmin { get; set; }
        public string? AdminNote { get; set; }
    }
}

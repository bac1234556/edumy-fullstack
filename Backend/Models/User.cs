using System;
using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public string Role { get; set; } = "Student";
        public string? Provider { get; set; }
        public string? ProviderUserId { get; set; }
        public string? AvatarUrl { get; set; }
        public string? Headline { get; set; }
        public string? Bio { get; set; }
        public bool IsActive { get; set; } = true;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? DeletedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        public ICollection<Course> Courses { get; set; } = new List<Course>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        
        public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        public ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
        public ICollection<ReviewReply> ReviewReplies { get; set; } = new List<ReviewReply>();
        public ICollection<CourseComment> CourseComments { get; set; } = new List<CourseComment>();
        public ICollection<CourseDiscussionThread> DiscussionThreads { get; set; } = new List<CourseDiscussionThread>();
        public ICollection<CourseDiscussionMessage> DiscussionMessages { get; set; } = new List<CourseDiscussionMessage>();
        public Cart? Cart { get; set; }
        public User? DeletedByUser { get; set; }
    }
}

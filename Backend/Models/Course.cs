using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.Models
{
    public class Course
    {
        public int CourseId { get; set; }
        public int InstructorId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        [MaxLength(500)]
        public string? ThumbnailUrl { get; set; }
        public string Level { get; set; } = "Beginner"; // Beginner, Intermediate, Advanced
        public string Status { get; set; } = "Draft"; // Draft, Published
        public double AverageRating { get; set; } = 0;
        public int ReviewCount { get; set; } = 0;
        public int StudentCount { get; set; } = 0;
        public bool NeedsReanalysis { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; }
        public DateTime? DeletedAt { get; set; }
        public int? DeletedByUserId { get; set; }

        public int? PrimaryCategoryId { get; set; }
        public Category? PrimaryCategory { get; set; }

        public User? Instructor { get; set; }
        public User? DeletedByUser { get; set; }
        public ICollection<CourseCategory> CourseCategories { get; set; } = new List<CourseCategory>();
        public ICollection<CourseTopic> CourseTopics { get; set; } = new List<CourseTopic>();
        public ICollection<CourseSection> Sections { get; set; } = new List<CourseSection>();
        public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        
        public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
        public ICollection<CourseCoupon> CourseCoupons { get; set; } = new List<CourseCoupon>();
        public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
        public ICollection<CourseMlAnalysis> MlAnalyses { get; set; } = new List<CourseMlAnalysis>();
        
        public ICollection<CourseTag> CourseTags { get; set; } = new List<CourseTag>();
        public ICollection<CourseDiscussionThread> DiscussionThreads { get; set; } = new List<CourseDiscussionThread>();
        public ICollection<CourseComment> Comments { get; set; } = new List<CourseComment>();
    }
}

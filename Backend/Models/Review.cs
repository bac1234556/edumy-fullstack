using System;

namespace EduMy.Backend.Models
{
    public class Review
    {
        public int ReviewId { get; set; }
        public int UserId { get; set; }
        public int CourseId { get; set; }
        public int Rating { get; set; } // 1-5
        public string Comment { get; set; } = string.Empty;
        public string? SentimentLabel { get; set; }
        public double? SentimentScore { get; set; }
        public double? SentimentConfidence { get; set; }
        public string? SentimentSource { get; set; }
        public string? SentimentModelVersion { get; set; }
        public DateTime? SentimentUpdatedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public User? User { get; set; }
        public Course? Course { get; set; }
        public ICollection<ReviewReply> Replies { get; set; } = new List<ReviewReply>();
    }
}

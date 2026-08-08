using System;
using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class CourseMlAnalysis
    {
        public int Id { get; set; }
        
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;
        
        public string? PrimaryCategory { get; set; }
        public string? SubCategory { get; set; }
        public string? SuggestedLevel { get; set; }
        public double Confidence { get; set; }
        public int QualityScore { get; set; }
        public string? RiskLevel { get; set; }
        public string? RawResponseJson { get; set; }
        public string? ModelVersion { get; set; }
        public string Status { get; set; } = "Pending";
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedByUserId { get; set; }
        public User? ApprovedByUser { get; set; }
        
        public ICollection<CourseMlAnalysisTag> Tags { get; set; } = new List<CourseMlAnalysisTag>();
    }
}

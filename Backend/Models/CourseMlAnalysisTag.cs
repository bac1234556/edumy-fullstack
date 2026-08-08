using System;

namespace EduMy.Backend.Models
{
    public class CourseMlAnalysisTag
    {
        public int Id { get; set; }
        
        public int CourseMlAnalysisId { get; set; }
        public CourseMlAnalysis CourseMlAnalysis { get; set; } = null!;
        
        public string TagName { get; set; } = string.Empty;
        public double Confidence { get; set; }
    }
}

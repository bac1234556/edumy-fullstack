using System;
using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class Quiz
    {
        public int QuizId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PassingScore { get; set; } = 80;
        public int? TimeLimitMinutes { get; set; }
        
        public int CourseSectionId { get; set; }
        public CourseSection CourseSection { get; set; } = null!;
        
        public ICollection<Question> Questions { get; set; } = new List<Question>();
        public ICollection<QuizAttempt> QuizAttempts { get; set; } = new List<QuizAttempt>();
    }
}

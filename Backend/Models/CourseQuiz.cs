using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class CourseQuiz
    {
        public int CourseQuizId { get; set; }
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public int PassingScore { get; set; } = 80;
        public bool IsActive { get; set; } = true;

        public ICollection<CourseQuizQuestion> Questions { get; set; } = new List<CourseQuizQuestion>();
        public ICollection<CourseQuizAttempt> Attempts { get; set; } = new List<CourseQuizAttempt>();
    }
}

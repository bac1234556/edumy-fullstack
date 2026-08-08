using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class CourseQuizQuestion
    {
        public int CourseQuizQuestionId { get; set; }
        public int CourseQuizId { get; set; }
        public CourseQuiz CourseQuiz { get; set; } = null!;
        public string QuestionText { get; set; } = string.Empty;
        public int OrderIndex { get; set; }

        public ICollection<CourseQuizOption> Options { get; set; } = new List<CourseQuizOption>();
    }
}

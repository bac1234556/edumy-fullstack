namespace EduMy.Backend.Models
{
    public class CourseQuizOption
    {
        public int CourseQuizOptionId { get; set; }
        public int CourseQuizQuestionId { get; set; }
        public CourseQuizQuestion Question { get; set; } = null!;
        public string OptionText { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
    }
}

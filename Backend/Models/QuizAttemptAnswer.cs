namespace EduMy.Backend.Models
{
    public class QuizAttemptAnswer
    {
        public int QuizAttemptAnswerId { get; set; }
        
        public int QuizAttemptId { get; set; }
        public QuizAttempt QuizAttempt { get; set; } = null!;
        
        public int QuestionId { get; set; }
        public Question Question { get; set; } = null!;
        
        public int AnswerId { get; set; }
        public Answer Answer { get; set; } = null!;
    }
}

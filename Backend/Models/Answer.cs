namespace EduMy.Backend.Models
{
    public class Answer
    {
        public int AnswerId { get; set; }
        public string Content { get; set; } = string.Empty;
        public bool IsCorrect { get; set; }
        
        public int QuestionId { get; set; }
        public Question Question { get; set; } = null!;
    }
}

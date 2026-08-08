using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class Question
    {
        public int QuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Points { get; set; } = 1;
        public string? Explanation { get; set; }
        
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; } = null!;
        
        public ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}

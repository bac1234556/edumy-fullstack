using System;
using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class QuizAttempt
    {
        public int QuizAttemptId { get; set; }
        public int Score { get; set; }
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        
        public int QuizId { get; set; }
        public Quiz Quiz { get; set; } = null!;
        
        public ICollection<QuizAttemptAnswer> SelectedAnswers { get; set; } = new List<QuizAttemptAnswer>();
    }
}

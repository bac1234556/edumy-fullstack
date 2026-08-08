using System;

namespace EduMy.Backend.Models
{
    public class CourseQuizAttempt
    {
        public int CourseQuizAttemptId { get; set; }
        public int CourseQuizId { get; set; }
        public CourseQuiz CourseQuiz { get; set; } = null!;
        public int StudentId { get; set; }
        public User Student { get; set; } = null!;
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public bool IsPassed { get; set; }
        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    }
}

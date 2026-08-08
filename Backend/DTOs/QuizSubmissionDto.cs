using System.Collections.Generic;

namespace EduMy.Backend.DTOs
{
    public class QuizSubmissionDto
    {
        public int QuizId { get; set; }
        // Key: QuestionId, Value: Selected AnswerId
        public Dictionary<int, int> SelectedAnswers { get; set; } = new();
    }

    public class QuizResultDto
    {
        public int QuizAttemptId { get; set; }
        public int Score { get; set; }
        public int TotalPoints { get; set; }
        public bool Passed { get; set; }
        
        public List<QuestionResultDto> Results { get; set; } = new();
    }

    public class QuestionResultDto
    {
        public int QuestionId { get; set; }
        public int SelectedAnswerId { get; set; }
        public int CorrectAnswerId { get; set; }
        public bool IsCorrect { get; set; }
        public string? Explanation { get; set; }
    }
}

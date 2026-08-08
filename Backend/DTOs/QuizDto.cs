using System.Collections.Generic;

namespace EduMy.Backend.DTOs
{
    public class QuizDto
    {
        public int QuizId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PassingScore { get; set; }
        public int? TimeLimitMinutes { get; set; }
        public int CourseSectionId { get; set; }
        public List<QuestionDto> Questions { get; set; } = new();
    }

    public class QuestionDto
    {
        public int QuestionId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int Points { get; set; }
        public string? Explanation { get; set; }
        public List<AnswerDto> Answers { get; set; } = new();
    }

    public class AnswerDto
    {
        public int AnswerId { get; set; }
        public string Content { get; set; } = string.Empty;
        // Instructor can see IsCorrect, student taking quiz won't see this field if we map it null or exclude it, but for simplicity we keep it and student API might just return another DTO or we clear it.
        // Let's make a StudentQuizDto if needed, or just set IsCorrect to false when sending to student.
        public bool? IsCorrect { get; set; }
    }
}

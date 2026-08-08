using System;
using System.Collections.Generic;

namespace EduMy.Backend.DTOs
{
    public class CourseQuizDto
    {
        public int CourseQuizId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int PassingScore { get; set; }
        public bool IsActive { get; set; }
        public List<CourseQuizQuestionDto> Questions { get; set; } = new();
    }

    public class CourseQuizQuestionDto
    {
        public int CourseQuizQuestionId { get; set; }
        public string QuestionText { get; set; } = string.Empty;
        public int OrderIndex { get; set; }
        public List<CourseQuizOptionDto> Options { get; set; } = new();
    }

    public class CourseQuizOptionDto
    {
        public int CourseQuizOptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public bool? IsCorrect { get; set; }
    }

    public class CourseQuizSubmissionDto
    {
        public Dictionary<int, int> Answers { get; set; } = new();
    }

    public class CourseQuizAttemptDto
    {
        public int CourseQuizAttemptId { get; set; }
        public int Score { get; set; }
        public int TotalQuestions { get; set; }
        public int CorrectAnswers { get; set; }
        public bool IsPassed { get; set; }
        public DateTime SubmittedAt { get; set; }
    }
}

using EduMy.Backend.Data;
using EduMy.Backend.DTOs;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EduMy.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class QuizAttemptsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QuizAttemptsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitQuiz([FromBody] QuizSubmissionDto submission)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(q => q.QuizId == submission.QuizId);

            if (quiz == null) return NotFound("Quiz not found.");

            // Create a new Attempt
            var attempt = new QuizAttempt
            {
                UserId = userId,
                QuizId = quiz.QuizId,
                StartedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };

            int score = 0;
            int totalPoints = quiz.Questions.Sum(q => q.Points);
            var resultDto = new QuizResultDto
            {
                TotalPoints = totalPoints
            };

            foreach (var question in quiz.Questions)
            {
                submission.SelectedAnswers.TryGetValue(question.QuestionId, out int selectedAnswerId);
                
                var correctAnswer = question.Answers.FirstOrDefault(a => a.IsCorrect);
                bool isCorrect = correctAnswer != null && correctAnswer.AnswerId == selectedAnswerId;
                
                if (isCorrect) score += question.Points;

                // Record answer
                if (selectedAnswerId > 0)
                {
                    attempt.SelectedAnswers.Add(new QuizAttemptAnswer
                    {
                        QuestionId = question.QuestionId,
                        AnswerId = selectedAnswerId
                    });
                }

                resultDto.Results.Add(new QuestionResultDto
                {
                    QuestionId = question.QuestionId,
                    SelectedAnswerId = selectedAnswerId,
                    CorrectAnswerId = correctAnswer?.AnswerId ?? 0,
                    IsCorrect = isCorrect,
                    Explanation = question.Explanation
                });
            }

            attempt.Score = score;
            _context.QuizAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            // Calculate percentage and check if passed
            double percentage = totalPoints > 0 ? (double)score / totalPoints * 100 : 0;
            resultDto.Score = score;
            resultDto.Passed = percentage >= quiz.PassingScore;
            resultDto.QuizAttemptId = attempt.QuizAttemptId;

            return Ok(resultDto);
        }
    }
}

using EduMy.Backend.Data;
using EduMy.Backend.Models;
using EduMy.Backend.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/courses/{courseId:int}/final-quiz")]
    [Authorize]
    public class CourseQuizzesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CourseQuizzesController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetQuiz(int courseId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var course = await _context.Courses
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CourseId == courseId && !c.IsDeleted);

            if (course == null) return NotFound("Course not found.");

            var isInstructor = course.InstructorId == userId;
            var isAdmin = User.IsInRole("Admin");

            // For student, check enrollment
            if (!isInstructor && !isAdmin)
            {
                var enrolled = await _context.Enrollments
                    .AnyAsync(e => e.CourseId == courseId && e.UserId == userId);
                if (!enrolled) return Forbid("Bạn chưa đăng ký khóa học này.");
            }

            var quiz = await _context.CourseQuizzes
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(q => q.CourseId == courseId && q.IsActive);

            if (quiz == null) return NotFound("Khóa học chưa có Final Quiz hoặc quiz chưa được kích hoạt.");

            var result = new CourseQuizDto
            {
                CourseQuizId = quiz.CourseQuizId,
                CourseId = quiz.CourseId,
                Title = quiz.Title,
                PassingScore = quiz.PassingScore,
                IsActive = quiz.IsActive,
                Questions = quiz.Questions.OrderBy(q => q.OrderIndex).Select(q => new CourseQuizQuestionDto
                {
                    CourseQuizQuestionId = q.CourseQuizQuestionId,
                    QuestionText = q.QuestionText,
                    OrderIndex = q.OrderIndex,
                    Options = q.Options.Select(o => new CourseQuizOptionDto
                    {
                        CourseQuizOptionId = o.CourseQuizOptionId,
                        OptionText = o.OptionText,
                        IsCorrect = (isInstructor || isAdmin) ? o.IsCorrect : (bool?)null // Hide correctness from students
                    }).ToList()
                }).ToList()
            };

            return Ok(result);
        }

        [HttpPost]
        public async Task<IActionResult> SaveQuiz(int courseId, [FromBody] CourseQuizDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var course = await _context.Courses
                .FirstOrDefaultAsync(c => c.CourseId == courseId && !c.IsDeleted);

            if (course == null) return NotFound("Course not found.");

            if (course.InstructorId != userId && !User.IsInRole("Admin"))
            {
                return Forbid("Bạn không có quyền quản lý quiz của khóa học này.");
            }

            // Validate questions and options
            if (dto.Questions == null || !dto.Questions.Any())
            {
                return BadRequest("Quiz phải có ít nhất 1 câu hỏi.");
            }

            foreach (var q in dto.Questions)
            {
                if (string.IsNullOrWhiteSpace(q.QuestionText))
                {
                    return BadRequest("Nội dung câu hỏi không được để trống.");
                }
                if (q.Options == null || q.Options.Count < 2)
                {
                    return BadRequest($"Câu hỏi '{q.QuestionText}' phải có ít nhất 2 đáp án lựa chọn.");
                }
                if (!q.Options.Any(o => o.IsCorrect == true))
                {
                    return BadRequest($"Câu hỏi '{q.QuestionText}' phải có ít nhất 1 đáp án đúng.");
                }
            }

            var existingQuiz = await _context.CourseQuizzes
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(q => q.CourseId == courseId);

            if (existingQuiz == null)
            {
                existingQuiz = new CourseQuiz
                {
                    CourseId = courseId,
                    Title = dto.Title?.Trim() ?? "Final Quiz",
                    PassingScore = dto.PassingScore > 0 ? dto.PassingScore : 80,
                    IsActive = dto.IsActive
                };
                _context.CourseQuizzes.Add(existingQuiz);
            }
            else
            {
                existingQuiz.Title = dto.Title?.Trim() ?? "Final Quiz";
                existingQuiz.PassingScore = dto.PassingScore > 0 ? dto.PassingScore : 80;
                existingQuiz.IsActive = dto.IsActive;

                // Remove existing questions
                _context.CourseQuizQuestions.RemoveRange(existingQuiz.Questions);
                existingQuiz.Questions.Clear();
            }

            int index = 1;
            foreach (var qDto in dto.Questions)
            {
                var question = new CourseQuizQuestion
                {
                    QuestionText = qDto.QuestionText.Trim(),
                    OrderIndex = qDto.OrderIndex > 0 ? qDto.OrderIndex : index++
                };

                foreach (var oDto in qDto.Options)
                {
                    question.Options.Add(new CourseQuizOption
                    {
                        OptionText = oDto.OptionText.Trim(),
                        IsCorrect = oDto.IsCorrect ?? false
                    });
                }

                existingQuiz.Questions.Add(question);
            }

            await _context.SaveChangesAsync();
            return Ok(new { success = true, existingQuiz.CourseQuizId });
        }

        [HttpPost("submit")]
        public async Task<IActionResult> SubmitQuizAttempt(int courseId, [FromBody] CourseQuizSubmissionDto submission)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            // Check enrollment
            var enrolled = await _context.Enrollments
                .AnyAsync(e => e.CourseId == courseId && e.UserId == userId);
            if (!enrolled) return Forbid("Bạn chưa đăng ký khóa học này.");

            var quiz = await _context.CourseQuizzes
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Options)
                .FirstOrDefaultAsync(q => q.CourseId == courseId && q.IsActive);

            if (quiz == null) return NotFound("Final Quiz not found or inactive.");

            int totalQuestions = quiz.Questions.Count;
            int correctAnswers = 0;

            foreach (var question in quiz.Questions)
            {
                submission.Answers.TryGetValue(question.CourseQuizQuestionId, out int selectedOptionId);
                var correctOption = question.Options.FirstOrDefault(o => o.IsCorrect);
                if (correctOption != null && correctOption.CourseQuizOptionId == selectedOptionId)
                {
                    correctAnswers++;
                }
            }

            double percentage = totalQuestions > 0 ? (double)correctAnswers / totalQuestions * 100 : 0;
            bool isPassed = percentage >= quiz.PassingScore;

            var attempt = new CourseQuizAttempt
            {
                CourseQuizId = quiz.CourseQuizId,
                StudentId = userId,
                Score = (int)Math.Round(percentage),
                TotalQuestions = totalQuestions,
                CorrectAnswers = correctAnswers,
                IsPassed = isPassed,
                SubmittedAt = DateTime.UtcNow
            };

            _context.CourseQuizAttempts.Add(attempt);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                attempt.CourseQuizAttemptId,
                attempt.Score,
                attempt.TotalQuestions,
                attempt.CorrectAnswers,
                attempt.IsPassed,
                attempt.SubmittedAt,
                correctAnswersMap = quiz.Questions.ToDictionary(
                    q => q.CourseQuizQuestionId,
                    q => q.Options.FirstOrDefault(o => o.IsCorrect)?.CourseQuizOptionId ?? 0
                )
            });
        }

        [HttpGet("attempts")]
        public async Task<IActionResult> GetMyAttempts(int courseId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var attempts = await _context.CourseQuizAttempts
                .Where(a => a.CourseQuiz.CourseId == courseId && a.StudentId == userId)
                .OrderByDescending(a => a.SubmittedAt)
                .Select(a => new CourseQuizAttemptDto
                {
                    CourseQuizAttemptId = a.CourseQuizAttemptId,
                    Score = a.Score,
                    TotalQuestions = a.TotalQuestions,
                    CorrectAnswers = a.CorrectAnswers,
                    IsPassed = a.IsPassed,
                    SubmittedAt = a.SubmittedAt
                })
                .ToListAsync();

            return Ok(attempts);
        }
    }
}

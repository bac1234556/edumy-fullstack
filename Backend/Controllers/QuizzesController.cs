using EduMy.Backend.Data;
using EduMy.Backend.DTOs;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace EduMy.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class QuizzesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public QuizzesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/quizzes/section/{sectionId}
        [HttpGet("section/{sectionId}")]
        public async Task<IActionResult> GetQuizzesBySection(int sectionId)
        {
            var quizzes = await _context.Quizzes
                .Where(q => q.CourseSectionId == sectionId)
                .Select(q => new QuizDto
                {
                    QuizId = q.QuizId,
                    Title = q.Title,
                    Description = q.Description,
                    PassingScore = q.PassingScore,
                    TimeLimitMinutes = q.TimeLimitMinutes,
                    CourseSectionId = q.CourseSectionId
                })
                .ToListAsync();

            return Ok(quizzes);
        }

        // GET: api/quizzes/{id}
        // Allows student to fetch questions (without IsCorrect flag)
        [HttpGet("{id}")]
        [Authorize]
        public async Task<IActionResult> GetQuizForStudent(int id)
        {
            var quiz = await _context.Quizzes
                .Include(q => q.Questions)
                    .ThenInclude(q => q.Answers)
                .FirstOrDefaultAsync(q => q.QuizId == id);

            if (quiz == null) return NotFound("Quiz not found");

            var dto = new QuizDto
            {
                QuizId = quiz.QuizId,
                Title = quiz.Title,
                Description = quiz.Description,
                PassingScore = quiz.PassingScore,
                TimeLimitMinutes = quiz.TimeLimitMinutes,
                CourseSectionId = quiz.CourseSectionId,
                Questions = quiz.Questions.Select(q => new QuestionDto
                {
                    QuestionId = q.QuestionId,
                    Content = q.Content,
                    Points = q.Points,
                    Answers = q.Answers.Select(a => new AnswerDto
                    {
                        AnswerId = a.AnswerId,
                        Content = a.Content
                    }).ToList()
                }).ToList()
            };

            return Ok(dto);
        }
    }
}

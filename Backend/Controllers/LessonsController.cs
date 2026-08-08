using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LessonsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LessonsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("section/{sectionId}")]
        public async Task<IActionResult> GetSectionLessons(int sectionId)
        {
            var lessons = await _context.Lessons
                .Where(l => l.SectionId == sectionId)
                .OrderBy(l => l.OrderIndex)
                .ToListAsync();

            return Ok(lessons);
        }

    }
}

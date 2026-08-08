using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TagsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public TagsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetTags()
        {
            var tags = await _context.Tags.ToListAsync();
            return Ok(tags);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchTags([FromQuery] string query)
        {
            if (string.IsNullOrEmpty(query)) return Ok(new List<Tag>());
            
            var tags = await _context.Tags
                .Where(t => t.Name.Contains(query))
                .Take(20)
                .ToListAsync();
                
            return Ok(tags);
        }

        [HttpPost]
        [Authorize(Roles = "Instructor,Admin")]
        public async Task<IActionResult> CreateTag([FromBody] Tag tag)
        {
            // Check if exists
            var existing = await _context.Tags.FirstOrDefaultAsync(t => t.Name.ToLower() == tag.Name.ToLower());
            if (existing != null) return Ok(existing);
            
            _context.Tags.Add(tag);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetTags), new { id = tag.Id }, tag);
        }
    }
}

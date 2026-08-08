using System.Security.Claims;
using EduMy.Backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [AllowAnonymous]
        [HttpGet("{id:int}/public-profile")]
        public async Task<IActionResult> GetPublicProfile(int id)
        {
            var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null) return NotFound();

            object roleData;
            if (user.Role == "Instructor")
            {
                var courses = await _context.Courses.AsNoTracking()
                    .Where(c => c.InstructorId == id && !c.IsDeleted && c.Status == "Published")
                    .OrderByDescending(c => c.CreatedAt)
                    .Select(c => new { c.CourseId, c.Title, c.Slug, c.ThumbnailUrl, c.AverageRating, c.StudentCount })
                    .ToListAsync();
                roleData = new
                {
                    courses,
                    averageRating = courses.Count == 0 ? 0 : Math.Round(courses.Average(c => c.AverageRating), 1),
                    totalStudents = courses.Sum(c => c.StudentCount),
                    courseCount = courses.Count
                };
            }
            else
            {
                var enrolledCourses = await _context.Enrollments.AsNoTracking().Where(e => e.UserId == id)
                    .OrderByDescending(e => e.EnrolledAt)
                    .Select(e => new { e.CourseId, e.Course!.Title, e.Course.ThumbnailUrl, e.ProgressPercentage })
                    .ToListAsync();
                var wishlist = await _context.Wishlists.AsNoTracking().Where(w => w.UserId == id)
                    .OrderByDescending(w => w.AddedAt)
                    .Select(w => new { w.CourseId, w.Course.Title, w.Course.ThumbnailUrl })
                    .ToListAsync();
                roleData = new { enrolledCourses, wishlist };
            }

            return Ok(new
            {
                userId = user.UserId, user.FullName, user.AvatarUrl, user.Headline, user.Bio,
                user.Role, user.CreatedAt,
                isActive = User.IsInRole("Admin") ? user.IsActive : (bool?)null,
                roleData
            });
        }

        [HttpGet("profile")]
        [HttpGet("/api/account/me")]
        [HttpGet("/api/users/me")]
        public async Task<IActionResult> GetProfile()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var user = await _context.Users
                .Where(u => u.UserId == userId)
                .Select(u => new
                {
                    u.UserId,
                    id = u.UserId,
                    u.Email,
                    u.FullName,
                    u.Headline,
                    u.Bio,
                    u.AvatarUrl,
                    u.Role,
                    u.IsActive,
                    u.CreatedAt
                })
                .FirstOrDefaultAsync();

            if (user == null) return NotFound("User not found");

            return Ok(user);
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound("User not found");

            if (!string.IsNullOrWhiteSpace(request.FullName))
            {
                user.FullName = request.FullName;
            }
            
            if (request.Headline != null)
            {
                user.Headline = request.Headline;
            }

            if (request.Bio != null)
            {
                user.Bio = request.Bio;
            }

            if (request.AvatarUrl != null)
            {
                user.AvatarUrl = request.AvatarUrl;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                user.UserId,
                user.Email,
                user.FullName,
                user.Headline,
                user.Bio,
                user.AvatarUrl,
                user.Role,
                user.CreatedAt
            });
        }
    }

    public class UpdateProfileRequest
    {
        public string? FullName { get; set; }
        public string? Headline { get; set; }
        public string? Bio { get; set; }
        public string? AvatarUrl { get; set; }
    }
}

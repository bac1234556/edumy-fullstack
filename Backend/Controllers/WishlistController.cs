using System.Security.Claims;
using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Student")]
    public class WishlistController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public WishlistController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyWishlist()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var wishlist = await _context.Wishlists
                .Include(w => w.Course)
                .ThenInclude(c => c.Instructor)
                .Where(w => w.UserId == userId && !w.Course.IsDeleted)
                .OrderByDescending(w => w.AddedAt)
                .Select(w => new
                {
                    w.Id,
                    w.CourseId,
                    w.AddedAt,
                    Course = new
                    {
                        w.Course.Title,
                        w.Course.ThumbnailUrl,
                        w.Course.Price,
                        CategoryName = w.Course.CourseCategories.Select(cc => cc.Category.Name).FirstOrDefault(),
                        Instructor = new { w.Course.Instructor.FullName }
                    }
                })
                .ToListAsync();

            return Ok(wishlist);
        }

        [HttpPost("add/{courseId}")]
        public async Task<IActionResult> AddToWishlist(int courseId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            // Check if already in wishlist
            var existing = await _context.Wishlists
                .FirstOrDefaultAsync(w => w.UserId == userId && w.CourseId == courseId);

            if (existing != null)
                return BadRequest(new { message = "Course is already in your wishlist" });

            // Check if course exists
            var courseExists = await _context.Courses.AnyAsync(c => c.CourseId == courseId && !c.IsDeleted && c.Status == "Published");
            if (!courseExists)
                return NotFound(new { message = "Course not found" });

            var wishlistItem = new Wishlist
            {
                UserId = userId,
                CourseId = courseId
            };

            _context.Wishlists.Add(wishlistItem);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Added to wishlist successfully" });
        }

        [HttpDelete("{courseId}")]
        [HttpDelete("remove/{courseId}")]
        public async Task<IActionResult> RemoveFromWishlist(int courseId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var item = await _context.Wishlists
                .FirstOrDefaultAsync(w => w.UserId == userId && w.CourseId == courseId);

            if (item == null)
                return NotFound(new { message = "Course is not in your wishlist" });

            _context.Wishlists.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Removed from wishlist" });
        }
        
        [HttpGet("check/{courseId}")]
        public async Task<IActionResult> CheckWishlistStatus(int courseId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Ok(new { inWishlist = false });

            var existing = await _context.Wishlists
                .AnyAsync(w => w.UserId == userId && w.CourseId == courseId && !w.Course.IsDeleted);

            return Ok(new { inWishlist = existing });
        }
    }
}

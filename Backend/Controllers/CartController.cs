using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Student")]
    public class CartController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        private async Task<Cart> GetOrCreateCartAsync(int userId)
        {
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Course)
                        .ThenInclude(course => course.CourseCategories)
                            .ThenInclude(cc => cc.Category)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Course)
                        .ThenInclude(course => course.Instructor)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }

        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var cart = await GetOrCreateCartAsync(userId);
            
            // Format response to avoid circular references
            var response = new
            {
                cart.Id,
                cart.UserId,
                TotalItems = cart.CartItems.Count(ci => !ci.Course.IsDeleted),
                TotalPrice = cart.CartItems.Where(ci => !ci.Course.IsDeleted).Sum(ci => ci.Course.Price),
                Items = cart.CartItems.Where(ci => !ci.Course.IsDeleted).Select(ci => new
                {
                    ci.Id,
                    ci.CourseId,
                    ci.AddedAt,
                    Course = new {
                        ci.Course.Title,
                        ci.Course.Price,
                        ci.Course.ThumbnailUrl,
                        CategoryName = ci.Course.CourseCategories.FirstOrDefault()?.Category?.Name,
                        ci.Course.Instructor?.FullName
                    }
                })
            };

            return Ok(response);
        }

        [HttpPost("add/{courseId}")]
        public async Task<IActionResult> AddToCart(int courseId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            // Check if course exists
            var course = await _context.Courses.FirstOrDefaultAsync(c => c.CourseId == courseId && !c.IsDeleted && c.Status == "Published");
            if (course == null) return NotFound("Course not found.");

            // Check if user already enrolled
            var alreadyEnrolled = await _context.Enrollments.AnyAsync(e => e.UserId == userId && e.CourseId == courseId);
            if (alreadyEnrolled) return BadRequest("You are already enrolled in this course.");

            var cart = await GetOrCreateCartAsync(userId);

            // Check if already in cart
            if (cart.CartItems.Any(ci => ci.CourseId == courseId))
                return Ok(new { message = "Course is already in your cart." });

            var cartItem = new CartItem
            {
                CartId = cart.Id,
                CourseId = courseId
            };

            _context.CartItems.Add(cartItem);
            cart.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Course added to cart successfully." });
        }

        [HttpDelete("remove/{courseId}")]
        public async Task<IActionResult> RemoveFromCart(int courseId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var cart = await GetOrCreateCartAsync(userId);
            var item = cart.CartItems.FirstOrDefault(ci => ci.CourseId == courseId);

            if (item != null)
            {
                _context.CartItems.Remove(item);
                cart.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return Ok(new { message = "Course removed from cart." });
        }
    }
}

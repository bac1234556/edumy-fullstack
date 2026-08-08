using EduMy.Backend.Data;
using EduMy.Backend.Models;
using EduMy.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InstructorApplicationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly INotificationService _notificationService;

        public InstructorApplicationsController(ApplicationDbContext context, INotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }

        [HttpGet("my-status")]
        public async Task<IActionResult> GetMyApplicationStatus()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var application = await _context.InstructorApplications
                .Where(a => a.UserId == userId)
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync();

            if (application == null)
            {
                return Ok(new { hasApplication = false });
            }

            return Ok(new
            {
                hasApplication = true,
                application.InstructorApplicationId,
                application.Introduction,
                application.Expertise,
                application.Reason,
                application.Status,
                application.CreatedAt,
                application.ReviewedAt,
                application.AdminNote
            });
        }

        [HttpPost]
        public async Task<IActionResult> SubmitApplication([FromBody] SubmitApplicationDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) return NotFound("User not found.");

            // Check if already instructor or admin
            if (user.Role == "Instructor" || user.Role == "Admin")
            {
                return BadRequest("You are already an Instructor or Admin.");
            }

            // Check if there is already a Pending application
            var hasPending = await _context.InstructorApplications
                .AnyAsync(a => a.UserId == userId && a.Status == "Pending");

            if (hasPending)
            {
                return BadRequest("You already have a pending application.");
            }

            var application = new InstructorApplication
            {
                UserId = userId,
                Introduction = dto.Introduction?.Trim() ?? string.Empty,
                Expertise = dto.Expertise?.Trim() ?? string.Empty,
                Reason = dto.Reason?.Trim() ?? string.Empty,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.InstructorApplications.Add(application);
            await _context.SaveChangesAsync();

            // Create notification for all Admin users
            var admins = await _context.Users.Where(u => u.Role == "Admin").ToListAsync();
            foreach (var admin in admins)
            {
                await _notificationService.CreateNotificationAsync(
                    recipientUserId: admin.UserId,
                    actorUserId: userId,
                    type: "InstructorApplication",
                    title: "Đăng ký giảng viên mới",
                    message: $"{user.FullName} đã gửi đăng ký trở thành Giảng viên.",
                    targetUrl: "/admin/instructor-applications"
                );
            }

            return Ok(new { success = true, application.InstructorApplicationId, application.Status });
        }
    }

    public class SubmitApplicationDto
    {
        public string Introduction { get; set; } = string.Empty;
        public string Expertise { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
    }
}

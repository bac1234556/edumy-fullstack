using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduMy.Backend.DTOs;
using EduMy.Backend.Services;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IAccountDeletionService _accountDeletion;
        private readonly INotificationService _notificationService;

        public AdminController(ApplicationDbContext context, IAccountDeletionService accountDeletion, INotificationService notificationService)
        {
            _context = context;
            _accountDeletion = accountDeletion;
            _notificationService = notificationService;
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var newUsersThisMonth = await _context.Users
                .Where(u => !u.IsDeleted && u.CreatedAt >= startOfMonth)
                .CountAsync();

            var totalUsers = await _context.Users.CountAsync(u => !u.IsDeleted);
            var totalCourses = await _context.Courses.CountAsync(c => !c.IsDeleted);
            
            var totalStudents = await _context.Enrollments
                .Select(e => e.UserId)
                .Distinct()
                .CountAsync();

            var recentUsers = await _context.Users
                .Where(u => !u.IsDeleted)
                .OrderByDescending(u => u.CreatedAt)
                .Take(5)
                .Select(u => new {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.Role,
                    u.CreatedAt
                })
                .ToListAsync();

            var recentReviews = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.User)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .Select(r => new {
                    r.ReviewId,
                    r.Rating,
                    r.Comment,
                    r.SentimentLabel,
                    CourseTitle = r.Course.Title,
                    StudentName = r.User.FullName,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                NewUsersThisMonth = newUsersThisMonth,
                TotalUsers = totalUsers,
                TotalCourses = totalCourses,
                TotalStudents = totalStudents,
                RecentUsers = recentUsers,
                RecentReviews = recentReviews,
                RecentSales = await RecentSalesQuery(10).ToListAsync()
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> GetAllUsers([FromQuery] string? search, [FromQuery] bool includeDeleted = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.Users.AsNoTracking().AsQueryable();
            if (!includeDeleted) query = query.Where(u => !u.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                if (value.Length > 100) return BadRequest(new { message = "Search is too long." });
                query = query.Where(u => u.FullName.Contains(value) || u.Email.Contains(value) || u.Role.Contains(value));
            }
            var total = await query.CountAsync();
            var users = await query.OrderByDescending(u => u.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new { u.UserId, u.FullName, u.Email, u.Role, u.IsActive, u.IsDeleted, u.DeletedAt, u.CreatedAt }).ToListAsync();
            return Ok(new PagedResponseDto<object>(users.Cast<object>().ToList(), page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
        }

        [HttpGet("courses")]
        public async Task<IActionResult> GetAllCourses([FromQuery] string? search, [FromQuery] bool includeDeleted = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.Courses.AsNoTracking().AsQueryable();
            if (!includeDeleted) query = query.Where(c => !c.IsDeleted);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var value = search.Trim();
                if (value.Length > 100) return BadRequest(new { message = "Search is too long." });
                query = query.Where(c => c.Title.Contains(value) || (c.Instructor != null && c.Instructor.FullName.Contains(value)));
            }
            var total = await query.CountAsync();
            var courses = await query.OrderByDescending(c => c.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(c => new
                {
                    c.CourseId, c.Title, c.Status, c.Price, c.ThumbnailUrl, c.IsDeleted, c.DeletedAt, c.CreatedAt,
                    instructor = c.Instructor == null ? null : new { c.Instructor.UserId, c.Instructor.FullName },
                    category = c.CourseCategories.Select(cc => new { cc.Category.CategoryId, cc.Category.Name }).FirstOrDefault()
                }).ToListAsync();
            return Ok(new PagedResponseDto<object>(courses.Cast<object>().ToList(), page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
        }

        [HttpPut("courses/{id}/status")]
        public async Task<IActionResult> UpdateCourseStatus(int id, [FromBody] string status)
        {
            if (status is not ("Draft" or "Published" or "PendingApproval" or "NeedsReview")) return BadRequest(new { message = "Invalid status." });
            var course = await _context.Courses.FindAsync(id);
            if (course == null || course.IsDeleted) return NotFound();

            course.Status = status;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Status updated" });
        }

        [HttpPut("users/{id}/toggle-status")]
        public async Task<IActionResult> ToggleUserStatus(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || user.IsDeleted) return NotFound();
            var currentAdminId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : 0;
            if (id == currentAdminId && user.IsActive)
                return BadRequest(new { message = "Bạn không thể tự khóa tài khoản admin đang sử dụng." });

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User status updated to {(user.IsActive ? "Active" : "Blocked")}", isActive = user.IsActive });
        }

        [HttpDelete("users/{id:int}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var adminId)) return Unauthorized();
            var result = await _accountDeletion.DeleteAsync(id, adminId, false);
            return StatusCode(result.StatusCode, result);
        }

        [HttpGet("recent-sales")]
        public async Task<IActionResult> GetRecentSales([FromQuery] int limit = 20) =>
            Ok(await RecentSalesQuery(Math.Clamp(limit, 1, 100)).ToListAsync());

        private IQueryable<AdminRecentSaleDto> RecentSalesQuery(int limit) => _context.OrderItems.AsNoTracking()
            .Where(i => i.Order != null && (i.Order.Status == "Completed" || i.Order.Status == "Paid"))
            .OrderByDescending(i => i.Order!.CreatedAt)
            .Take(limit)
            .Select(i => new AdminRecentSaleDto
            {
                OrderId = i.OrderId, OrderItemId = i.OrderItemId, CourseId = i.CourseId,
                CourseTitle = i.Course!.Title,
                InstructorName = i.Course.Instructor!.FullName,
                BuyerName = i.Order!.User!.FullName,
                SoldPrice = i.Price,
                SoldAt = i.Order.CreatedAt
            });

        [HttpPut("users/{id}/role")]
        public async Task<IActionResult> UpdateUserRole(int id, [FromBody] string newRole)
        {
            var validRoles = new[] { "Admin", "Instructor", "Student" };
            if (!validRoles.Contains(newRole)) return BadRequest("Invalid role.");

            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            user.Role = newRole;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"User role updated to {newRole}" });
        }

        [HttpGet("ml-monitoring")]
        public async Task<IActionResult> GetMlMonitoring()
        {
            var totalAnalyses = await _context.CourseMlAnalyses.CountAsync();
            var highRiskCount = await _context.CourseMlAnalyses.CountAsync(a => a.RiskLevel == "High");
            var pendingReviews = await _context.CourseMlAnalyses.CountAsync(a => a.Status == "NeedsManualReview" || a.Status == "InstructorConfirmationRequired");
            
            var totalReviews = await _context.Reviews.CountAsync();
            var sentimentStats = await _context.Reviews
                .GroupBy(r => r.SentimentLabel)
                .Select(g => new { Label = g.Key, Count = g.Count() })
                .ToListAsync();

            var analysesHistory = await _context.CourseMlAnalyses
                .Include(a => a.Course)
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .Select(a => new {
                    a.Id,
                    a.CourseId,
                    CourseTitle = a.Course.Title,
                    a.PrimaryCategory,
                    a.Confidence,
                    a.QualityScore,
                    a.RiskLevel,
                    a.Status,
                    a.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                TotalAnalyses = totalAnalyses,
                HighRiskCount = highRiskCount,
                PendingReviews = pendingReviews,
                TotalReviews = totalReviews,
                SentimentStats = sentimentStats,
                AnalysesHistory = analysesHistory
            });
        }

        [HttpPost("ml-analyses/{id}/approve")]
        public async Task<IActionResult> ApproveMlAnalysis(int id)
        {
            var analysis = await _context.CourseMlAnalyses
                .Include(a => a.Course)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (analysis == null) return NotFound("Analysis record not found.");

            analysis.Status = "Approved";
            analysis.ApprovedAt = DateTime.UtcNow;

            var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int adminId))
            {
                analysis.ApprovedByUserId = adminId;
            }

            analysis.Course.Status = "PendingApproval";
            await _context.SaveChangesAsync();

            return Ok(new { message = "ML classification approved." });
        }

        [HttpPost("ml-analyses/{id}/override")]
        public async Task<IActionResult> OverrideMlAnalysis(int id, [FromBody] OverrideMlDto dto)
        {
            var analysis = await _context.CourseMlAnalyses
                .Include(a => a.Course)
                    .ThenInclude(c => c.CourseCategories)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (analysis == null) return NotFound("Analysis record not found.");

            analysis.Status = "Overridden";
            analysis.ApprovedAt = DateTime.UtcNow;

            var userIdStr = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int adminId))
            {
                analysis.ApprovedByUserId = adminId;
            }

            var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == dto.CategoryName);
            if (cat != null)
            {
                analysis.Course.CourseCategories.Clear();
                analysis.Course.CourseCategories.Add(new CourseCategory { CourseId = analysis.Course.CourseId, CategoryId = cat.CategoryId });
            }

            analysis.Course.Status = "PendingApproval";
            await _context.SaveChangesAsync();

            return Ok(new { message = "ML classification overridden and course updated." });
        }

        [HttpGet("courses/{courseId:int}/preview")]
        public async Task<IActionResult> GetAdminCoursePreview(int courseId)
        {
            var course = await _context.Courses
                .Include(c => c.CourseCategories)
                    .ThenInclude(cc => cc.Category)
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.CourseId == courseId && !c.IsDeleted);

            if (course == null) return NotFound(new { message = "Khóa học không tồn tại." });

            var sections = await _context.CourseSections
                .Where(s => s.CourseId == courseId)
                .OrderBy(s => s.OrderIndex)
                .Select(s => new
                {
                    s.SectionId,
                    s.CourseId,
                    s.Title,
                    s.OrderIndex,
                    lessons = _context.Lessons
                        .Where(l => l.SectionId == s.SectionId)
                        .OrderBy(l => l.OrderIndex)
                        .Select(l => new
                        {
                            l.LessonId,
                            l.SectionId,
                            l.Title,
                            l.Duration,
                            l.OrderIndex,
                            l.IsPreview,
                            l.ResourceType,
                            l.FileUrl,
                            l.VideoUrl,
                            l.OriginalFileName,
                            l.ContentType,
                            l.FileSizeBytes,
                            l.UploadedAt,
                            l.IsDraft,
                            isCompleted = false
                        }).ToList()
                }).ToListAsync();

            return Ok(new
            {
                course = new
                {
                    course.CourseId,
                    course.InstructorId,
                    instructorName = course.Instructor?.FullName ?? "Giảng viên",
                    course.Title,
                    course.Description,
                    course.Price,
                    course.ThumbnailUrl,
                    course.Level,
                    course.Status,
                    categoryName = course.CourseCategories.FirstOrDefault()?.Category?.Name
                },
                sections
            });
        }

        [HttpGet("instructor-applications")]
        public async Task<IActionResult> GetInstructorApplications([FromQuery] string? status)
        {
            var query = _context.InstructorApplications
                .Include(a => a.User)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.Status == status);
            }

            var list = await query
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new
                {
                    a.InstructorApplicationId,
                    a.UserId,
                    ApplicantName = a.User.FullName,
                    ApplicantEmail = a.User.Email,
                    a.Introduction,
                    a.Expertise,
                    a.Reason,
                    a.Status,
                    a.CreatedAt,
                    a.ReviewedAt,
                    a.AdminNote
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("instructor-applications/{id}/approve")]
        public async Task<IActionResult> ApproveInstructorApplication(int id, [FromBody] ReviewApplicationDto dto)
        {
            var application = await _context.InstructorApplications
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.InstructorApplicationId == id);

            if (application == null) return NotFound("Application not found.");
            if (application.Status != "Pending") return BadRequest("Application is already processed.");

            var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(adminIdStr, out int adminId)) return Unauthorized();

            application.Status = "Approved";
            application.ReviewedAt = DateTime.UtcNow;
            application.ReviewedByAdminId = adminId;
            application.AdminNote = dto.AdminNote?.Trim();

            // Promote user to Instructor role
            application.User.Role = "Instructor";

            await _context.SaveChangesAsync();

            // Create notification for student
            await _notificationService.CreateNotificationAsync(
                recipientUserId: application.UserId,
                actorUserId: adminId,
                type: "InstructorApplicationApproved",
                title: "Đơn đăng ký được duyệt",
                message: "Chúc mừng! Đăng ký trở thành Giảng viên của bạn đã được duyệt.",
                targetUrl: "/instructor"
            );

            return Ok(new { success = true, application.Status });
        }

        [HttpPost("instructor-applications/{id}/reject")]
        public async Task<IActionResult> RejectInstructorApplication(int id, [FromBody] ReviewApplicationDto dto)
        {
            var application = await _context.InstructorApplications
                .FirstOrDefaultAsync(a => a.InstructorApplicationId == id);

            if (application == null) return NotFound("Application not found.");
            if (application.Status != "Pending") return BadRequest("Application is already processed.");

            var adminIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(adminIdStr, out int adminId)) return Unauthorized();

            application.Status = "Rejected";
            application.ReviewedAt = DateTime.UtcNow;
            application.ReviewedByAdminId = adminId;
            application.AdminNote = dto.AdminNote?.Trim();

            await _context.SaveChangesAsync();

            // Create notification for student
            await _notificationService.CreateNotificationAsync(
                recipientUserId: application.UserId,
                actorUserId: adminId,
                type: "InstructorApplicationRejected",
                title: "Đơn đăng ký bị từ chối",
                message: $"Đăng ký trở thành Giảng viên của bạn đã bị từ chối. Lý do: {dto.AdminNote}",
                targetUrl: "/teach-on-edumy"
            );

            return Ok(new { success = true, application.Status });
        }
    }

    public class ReviewApplicationDto
    {
        public string? AdminNote { get; set; }
    }

    public class OverrideMlDto
    {
        public string CategoryName { get; set; } = string.Empty;
    }

    public class AdminRecentSaleDto
    {
        public int OrderId { get; set; }
        public int OrderItemId { get; set; }
        public int CourseId { get; set; }
        public string CourseTitle { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string BuyerName { get; set; } = string.Empty;
        public decimal SoldPrice { get; set; }
        public DateTime SoldAt { get; set; }
    }
}

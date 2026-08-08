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
    [Authorize(Roles = "Instructor")]
    public class InstructorController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly EduMy.Backend.Services.ILessonResourceStorage _storage;

        public InstructorController(ApplicationDbContext context, EduMy.Backend.Services.ILessonResourceStorage storage)
        {
            _context = context;
            _storage = storage;
        }

        [HttpGet("dashboard-stats")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetDashboardStatsCustom()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"Current User ID: {userIdStr}");

            int instructorId = 0;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out instructorId))
            {
                var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
                if (!string.IsNullOrEmpty(email))
                {
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                    if (user != null)
                    {
                        instructorId = user.UserId;
                    }
                }
            }

            if (instructorId == 0) return Unauthorized();

            var totalCourses = await _context.Courses
                .Where(c => c.InstructorId == instructorId && !c.IsDeleted)
                .CountAsync();

            var activeStudents = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.Course.InstructorId == instructorId && !e.Course.IsDeleted)
                .Select(e => e.UserId)
                .Distinct()
                .CountAsync();

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var monthlyRevenueDecimal = await _context.OrderItems
                .Where(i => i.Course!.InstructorId == instructorId && i.Order!.CreatedAt >= startOfMonth &&
                            (i.Order.Status == "Completed" || i.Order.Status == "Paid"))
                .SumAsync(i => (decimal?)i.Price) ?? 0m;

            var allReviews = await _context.Reviews
                .Include(r => r.Course)
                .Where(r => r.Course.InstructorId == instructorId)
                .ToListAsync();

            double? aiQualityRating = null;
            if (allReviews.Any())
            {
                var positiveReviews = allReviews.Count(r => r.SentimentLabel == "Positive");
                aiQualityRating = (double)positiveReviews / allReviews.Count * 100;
            }

            return Ok(new
            {
                totalCourses = totalCourses,
                activeStudents = activeStudents,
                monthlyRevenue = (double)monthlyRevenueDecimal,
                aiQualityRating = aiQualityRating.HasValue ? (double?)Math.Round(aiQualityRating.Value, 1) : null
            });
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            Console.WriteLine($"Current User ID: {userIdStr}");

            int instructorId = 0;
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out instructorId))
            {
                var email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email);
                if (!string.IsNullOrEmpty(email))
                {
                    var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
                    if (user != null)
                    {
                        instructorId = user.UserId;
                    }
                }
            }

            if (instructorId == 0) return Unauthorized();

            var totalCourses = await _context.Courses
                .Where(c => c.InstructorId == instructorId && !c.IsDeleted)
                .CountAsync();

            var totalStudents = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.Course.InstructorId == instructorId && !e.Course.IsDeleted)
                .Select(e => e.UserId)
                .Distinct()
                .CountAsync();

            var totalRevenue = await _context.OrderItems
                .Where(i => i.Course!.InstructorId == instructorId && i.Order != null &&
                            (i.Order.Status == "Completed" || i.Order.Status == "Paid"))
                .SumAsync(i => (decimal?)i.Price) ?? 0m;

            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var monthlyRevenue = await _context.OrderItems
                .Where(i => i.Course!.InstructorId == instructorId && i.Order!.CreatedAt >= startOfMonth &&
                            (i.Order.Status == "Completed" || i.Order.Status == "Paid"))
                .SumAsync(i => (decimal?)i.Price) ?? 0m;

            var averageRating = await _context.Courses
                .Where(c => c.InstructorId == instructorId && !c.IsDeleted && c.AverageRating > 0)
                .AverageAsync(c => (double?)c.AverageRating) ?? 0.0;

            var recentReviews = await _context.Reviews
                .Include(r => r.Course)
                .Include(r => r.User)
                .Where(r => r.Course.InstructorId == instructorId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .Select(r => new {
                    r.ReviewId,
                    r.Rating,
                    r.Comment,
                    r.SentimentLabel,
                    r.UserId,
                    r.CourseId,
                    CourseTitle = r.Course.Title,
                    StudentName = r.User.FullName,
                    r.CreatedAt
                })
                .ToListAsync();

            // Calculate AI Quality Rating (Positive reviews percentage)
            var allReviews = await _context.Reviews
                .Include(r => r.Course)
                .Where(r => r.Course.InstructorId == instructorId)
                .ToListAsync();

            double? aiQualityRating = null;
            if (allReviews.Any())
            {
                var positiveReviews = allReviews.Count(r => r.SentimentLabel == "Positive");
                aiQualityRating = (double)positiveReviews / allReviews.Count * 100;
            }

            // 1. Revenue by Date (last 6 months)
            var revenueGroups = await _context.OrderItems
                .Include(oi => oi.Course)
                .Include(oi => oi.Order)
                .Where(oi => oi.Course.InstructorId == instructorId && (oi.Order.Status == "Completed" || oi.Order.Status == "Paid") && oi.Order.CreatedAt >= DateTime.UtcNow.AddMonths(-6))
                .GroupBy(oi => new { Year = oi.Order.CreatedAt.Year, Month = oi.Order.CreatedAt.Month })
                .Select(g => new {
                    g.Key.Year,
                    g.Key.Month,
                    Revenue = g.Sum(oi => oi.Price)
                })
                .ToListAsync();
            var revenueByDate = revenueGroups.Select(g => new { Date = $"{g.Year}-{g.Month:D2}", g.Revenue }).OrderBy(x => x.Date).ToList();

            // 2. Enrollments by Date (last 6 months)
            var enrollmentGroups = await _context.Enrollments
                .Include(e => e.Course)
                .Where(e => e.Course.InstructorId == instructorId && e.EnrolledAt >= DateTime.UtcNow.AddMonths(-6))
                .GroupBy(e => new { Year = e.EnrolledAt.Year, Month = e.EnrolledAt.Month })
                .Select(g => new {
                    g.Key.Year,
                    g.Key.Month,
                    Enrollments = g.Count()
                })
                .ToListAsync();
            var enrollmentByDate = enrollmentGroups.Select(g => new { Date = $"{g.Year}-{g.Month:D2}", g.Enrollments }).OrderBy(x => x.Date).ToList();

            // 3. Sentiment breakdown
            var sentimentStats = await _context.Reviews
                .Include(r => r.Course)
                .Where(r => r.Course.InstructorId == instructorId)
                .GroupBy(r => r.SentimentLabel)
                .Select(g => new {
                    Label = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            // 4. ML average quality score
            var mlScores = await _context.CourseMlAnalyses
                .Include(ma => ma.Course)
                .Where(ma => ma.Course.InstructorId == instructorId)
                .Select(ma => ma.QualityScore)
                .ToListAsync();
            var averageQualityScore = mlScores.Any() ? mlScores.Average() : 0.0;

            // 5. Actionable improvement recommendations
            var lowQualityCourses = await _context.Courses
                .Include(c => c.MlAnalyses)
                .Where(c => c.InstructorId == instructorId && !c.IsDeleted)
                .Select(c => new {
                    c.CourseId,
                    c.Title,
                    QualityScore = c.MlAnalyses.OrderByDescending(ma => ma.CreatedAt).Select(ma => (int?)ma.QualityScore).FirstOrDefault() ?? 100,
                    NeedsReanalysis = c.NeedsReanalysis
                })
                .Where(c => c.QualityScore < 70 || c.NeedsReanalysis)
                .ToListAsync();

            var recommendations = new List<string>();
            foreach (var lq in lowQualityCourses)
            {
                if (lq.NeedsReanalysis)
                {
                    recommendations.Add($"Course '{lq.Title}' has modified content. Trigger 'Analyze content' to update parameters.");
                }
                else if (lq.QualityScore < 70)
                {
                    recommendations.Add($"Course '{lq.Title}' has a low quality score ({lq.QualityScore}%). Try expanding the course description or lessons outcomes.");
                }
            }

            // Aggregated metrics
            var coursesData = await _context.Courses
                .AsNoTracking()
                .Where(c => c.InstructorId == instructorId && !c.IsDeleted)
                .Select(c => new
                {
                    c.CourseId,
                    c.Title,
                    c.ThumbnailUrl,
                    c.AverageRating,
                    Enrollments = _context.Enrollments.Where(e => e.CourseId == c.CourseId).ToList(),
                    Revenue = _context.OrderItems
                        .Where(oi => oi.CourseId == c.CourseId && oi.Order != null && (oi.Order.Status == "Completed" || oi.Order.Status == "Paid"))
                        .Sum(oi => (decimal?)oi.Price) ?? 0m,
                    QuizAttempts = _context.CourseQuizAttempts.Where(a => a.CourseQuiz.CourseId == c.CourseId).ToList()
                })
                .ToListAsync();

            var unansweredQasCount = await _context.Set<CourseDiscussionThread>()
                .Where(t => t.Course.InstructorId == instructorId && !t.IsClosed)
                .Where(t => !t.Messages.Any(m => m.IsInstructorMessage))
                .CountAsync();

            var perCourseList = coursesData.Select(c => {
                double avgComp = c.Enrollments.Any() ? c.Enrollments.Average(e => e.ProgressPercentage) : 0.0;
                double avgQuiz = c.QuizAttempts.Any() ? c.QuizAttempts.Average(a => a.Score) : 0.0;
                return new {
                    c.CourseId,
                    c.Title,
                    c.ThumbnailUrl,
                    enrollmentsCount = c.Enrollments.Count,
                    revenue = (double)c.Revenue,
                    averageRating = Math.Round((double)c.AverageRating, 1),
                    completionRate = Math.Round(avgComp, 1),
                    finalQuizAverageScore = Math.Round(avgQuiz, 1)
                };
            }).ToList();

            double overallComp = perCourseList.Any() ? perCourseList.Average(c => c.completionRate) : 0.0;
            double overallQuiz = perCourseList.Any(c => c.finalQuizAverageScore > 0) ? perCourseList.Where(c => c.finalQuizAverageScore > 0).Average(c => c.finalQuizAverageScore) : 0.0;

            return Ok(new
            {
                totalCourses,
                totalStudents,
                activeStudents = totalStudents,
                totalRevenue = (double)totalRevenue,
                monthlyRevenue = (double)monthlyRevenue,
                averageRating = Math.Round(averageRating, 1),
                aiQualityRating = aiQualityRating.HasValue ? (double?)Math.Round(aiQualityRating.Value, 1) : null,
                recentReviews,
                revenueByDate,
                enrollmentByDate,
                sentimentStats,
                averageQualityScore = Math.Round(averageQualityScore, 1),
                recommendations,
                unansweredQasCount,
                averageCompletionRate = Math.Round(overallComp, 1),
                finalQuizAverageScore = Math.Round(overallQuiz, 1),
                perCourseAnalytics = perCourseList
            });
        }

        [HttpGet("recent-sales")]
        public async Task<IActionResult> GetRecentSales([FromQuery] int limit = 20)
        {
            var instructorId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;
            if (instructorId == 0) return Unauthorized();
            limit = Math.Clamp(limit, 1, 100);
            var sales = await _context.OrderItems.AsNoTracking()
                .Where(i => i.Course!.InstructorId == instructorId && i.Order != null &&
                            (i.Order.Status == "Completed" || i.Order.Status == "Paid"))
                .OrderByDescending(i => i.Order!.CreatedAt).Take(limit)
                .Select(i => new
                {
                    i.OrderId, i.OrderItemId, i.CourseId,
                    courseTitle = i.Course!.Title, i.Course.ThumbnailUrl,
                    buyerName = i.Order!.User!.FullName,
                    soldPrice = i.Price, soldAt = i.Order.CreatedAt
                }).ToListAsync();
            return Ok(sales);
        }

        [HttpGet("courses/{courseId:int}/preview")]
        public async Task<IActionResult> GetInstructorCoursePreview(int courseId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int instructorId)) return Unauthorized();

            var course = await _context.Courses
                .Include(c => c.CourseCategories)
                    .ThenInclude(cc => cc.Category)
                .Include(c => c.Instructor)
                .FirstOrDefaultAsync(c => c.CourseId == courseId && c.InstructorId == instructorId && !c.IsDeleted);

            if (course == null) return NotFound(new { message = "Khóa học không tồn tại hoặc bạn không có quyền truy cập." });

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
                            fileUrl = l.FileUrl ?? l.VideoUrl,
                            videoUrl = l.VideoUrl ?? l.FileUrl,
                            l.OriginalFileName,
                            l.ContentType,
                            l.FileSizeBytes,
                            l.UploadedAt,
                            l.IsDraft,
                            hasResource = l.ResourceType != "None" && l.ResourceType != "Reading" && (!string.IsNullOrWhiteSpace(l.FileUrl) || !string.IsNullOrWhiteSpace(l.VideoUrl) || !string.IsNullOrWhiteSpace(l.OriginalFileName)),
                            resourceExists = _storage.ResourceExists(l.FileUrl ?? l.VideoUrl),
                            resourceEndpoint = $"/api/learning/lessons/{l.LessonId}/resource",
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
    }
}

using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduMy.Backend.DTOs;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReviewsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly EduMy.Backend.Services.IMachineLearningService _mlService;
        private readonly EduMy.Backend.Services.ICourseRatingService _ratingService;
        private readonly EduMy.Backend.Services.INotificationService _notificationService;

        public ReviewsController(ApplicationDbContext context, EduMy.Backend.Services.IMachineLearningService mlService,
            EduMy.Backend.Services.ICourseRatingService ratingService, EduMy.Backend.Services.INotificationService notificationService)
        {
            _context = context;
            _mlService = mlService;
            _ratingService = ratingService;
            _notificationService = notificationService;
        }

        [HttpGet("course/{courseId}")]
        public async Task<IActionResult> GetCourseReviews(int courseId)
        {
            var reviews = await _context.Reviews
                .Where(r => r.CourseId == courseId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    r.ReviewId, r.UserId, r.CourseId, r.Rating, r.Comment, r.SentimentLabel,
                    r.SentimentScore, r.SentimentConfidence, r.SentimentSource, r.SentimentModelVersion, r.SentimentUpdatedAt, r.CreatedAt,
                    user = new { r.User!.UserId, r.User.FullName, r.User.AvatarUrl, r.User.Role },
                    replies = r.Replies.OrderBy(x => x.CreatedAt).Select(x => new
                    {
                        x.ReviewReplyId, x.UserId, x.Content, x.CreatedAt,
                        user = new { x.User.UserId, x.User.FullName, x.User.AvatarUrl, x.User.Role }
                    })
                })
                .ToListAsync();

            return Ok(reviews);
        }

        [Authorize(Roles = "Student")]
        [HttpPost("course/{courseId}")]
        public async Task<IActionResult> CreateReview(int courseId, [FromBody] Review review)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            review.Comment = (review.Comment ?? string.Empty).Trim();
            if (review.Rating is < 1 or > 5 || review.Comment.Length is < 1 or > 2000)
                return BadRequest(new { message = "Rating must be 1-5 and comment must be 1-2000 characters." });

            // Check if already enrolled
            var isEnrolled = await _context.Enrollments.AnyAsync(e => e.CourseId == courseId && e.UserId == userId);
            if (!isEnrolled) return BadRequest(new { message = "Bạn cần mua khóa học này trước khi để lại bình luận và đánh giá!" });
            if (await _context.Reviews.AnyAsync(r => r.CourseId == courseId && r.UserId == userId))
                return Conflict(new { message = "Bạn đã đánh giá khóa học này." });

            // ML Integration: Analyze Sentiment
            var sentimentResult = await _mlService.AnalyzeSentimentAsync(review.Comment, review.Rating);
            ApplySentiment(review, sentimentResult);

            review.CourseId = courseId;
            review.UserId = userId;
            review.CreatedAt = DateTime.UtcNow;

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            await _ratingService.RecalculateCourseRatingAsync(courseId);

            var course = await _context.Courses.AsNoTracking().FirstOrDefaultAsync(c => c.CourseId == courseId);
            if (course != null && course.InstructorId != userId)
            {
                await _notificationService.CreateNotificationAsync(
                    recipientUserId: course.InstructorId,
                    actorUserId: userId,
                    type: "NewCourseReview",
                    title: "Đánh giá mới cho khóa học",
                    message: $"Học viên đã để lại đánh giá {review.Rating} sao trong khóa học {course.Title}.",
                    targetUrl: $"/courses/{courseId}#review-{review.ReviewId}",
                    courseId: courseId,
                    reviewId: review.ReviewId
                );
            }

            return CreatedAtAction(nameof(GetCourseReviews), new { courseId = review.CourseId }, review);
        }

        [Authorize(Roles = "Student,Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateReview(int id, [FromBody] Review review)
        {
            if (id != review.ReviewId) return BadRequest();
            review.Comment = (review.Comment ?? string.Empty).Trim();
            if (review.Rating is < 1 or > 5 || review.Comment.Length is < 1 or > 2000)
                return BadRequest(new { message = "Rating must be 1-5 and comment must be 1-2000 characters." });

            var existing = await _context.Reviews.FirstOrDefaultAsync(r => r.ReviewId == id);
            if (existing == null) return NotFound();

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId) && existing.UserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            existing.Comment = review.Comment;
            existing.Rating = review.Rating;
            existing.UpdatedAt = DateTime.UtcNow;
            ApplySentiment(existing, await _mlService.AnalyzeSentimentAsync(existing.Comment, existing.Rating));
            
            try
            {
                await _context.SaveChangesAsync();
                
                await _ratingService.RecalculateCourseRatingAsync(existing.CourseId);
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ReviewExists(id)) return NotFound();
                else throw;
            }

            return NoContent();
        }

        [Authorize(Roles = "Student,Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReview(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review == null) return NotFound();

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId) && review.UserId != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            int courseId = review.CourseId;

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            await _ratingService.RecalculateCourseRatingAsync(courseId);

            return NoContent();
        }

        private bool ReviewExists(int id)
        {
            return _context.Reviews.Any(e => e.ReviewId == id);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("recalculate-sentiment")]
        public async Task<IActionResult> RecalculateSentiment()
        {
            const string pipelineVersion = "sentiment-hybrid-v2";
            var reviews = await _context.Reviews
                .Where(r => r.SentimentModelVersion != pipelineVersion || r.SentimentLabel == null)
                .OrderBy(r => r.ReviewId)
                .ToListAsync();
            foreach (var item in reviews)
                ApplySentiment(item, await _mlService.AnalyzeSentimentAsync(item.Comment, item.Rating));
            await _context.SaveChangesAsync();
            return Ok(new { updated = reviews.Count, pipelineVersion });
        }

        private static void ApplySentiment(Review review, EduMy.Backend.Services.SentimentResult? result)
        {
            review.SentimentLabel = result?.Label?.Trim().ToLowerInvariant() switch
            {
                "positive" => "Positive", "negative" => "Negative", "neutral" => "Neutral", _ => "Unknown"
            };
            review.SentimentScore = result?.Score ?? 0.5;
            review.SentimentConfidence = result?.Confidence ?? 0;
            review.SentimentSource = result?.Source ?? "unavailable";
            review.SentimentModelVersion = "sentiment-hybrid-v2";
            review.SentimentUpdatedAt = DateTime.UtcNow;
        }

        [HttpGet("{reviewId:int}/replies")]
        public async Task<IActionResult> GetReplies(int reviewId)
        {
            var exists = await _context.Reviews.AnyAsync(r => r.ReviewId == reviewId);
            if (!exists) return NotFound();
            var replies = await _context.ReviewReplies.AsNoTracking().Where(r => r.ReviewId == reviewId)
                .OrderBy(r => r.CreatedAt)
                .Select(r => new
                {
                    r.ReviewReplyId, r.UserId, r.Content, r.CreatedAt, r.UpdatedAt,
                    user = new { r.User.UserId, r.User.FullName, r.User.AvatarUrl, r.User.Role }
                }).ToListAsync();
            return Ok(replies);
        }

        [Authorize(Roles = "Instructor,Admin")]
        [HttpPost("{reviewId:int}/replies")]
        public async Task<IActionResult> Reply(int reviewId, ReviewReplyCreateDto dto)
        {
            var review = await _context.Reviews.Include(r => r.Course).FirstOrDefaultAsync(r => r.ReviewId == reviewId);
            if (review?.Course == null) return NotFound();
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!User.IsInRole("Admin") && review.Course.InstructorId != userId) return Forbid();
            var content = dto.Content.Trim();
            if (content.Length == 0) return BadRequest(new { message = "Content is required." });
            var reply = new ReviewReply { ReviewId = reviewId, UserId = userId, Content = content };
            _context.ReviewReplies.Add(reply);
            await _context.SaveChangesAsync();

            if (review.UserId != userId)
            {
                await _notificationService.CreateNotificationAsync(
                    recipientUserId: review.UserId,
                    actorUserId: userId,
                    type: "NewReviewReply",
                    title: "Giảng viên đã phản hồi đánh giá của bạn",
                    message: $"Giảng viên đã phản hồi đánh giá của bạn trong khóa học {review.Course.Title}.",
                    targetUrl: $"/courses/{review.CourseId}#reply-{reply.ReviewReplyId}",
                    courseId: review.CourseId,
                    reviewId: review.ReviewId,
                    reviewReplyId: reply.ReviewReplyId
                );
            }

            return Ok(new { reply.ReviewReplyId, reply.UserId, reply.Content, reply.CreatedAt });
        }

        [Authorize(Roles = "Instructor,Admin")]
        [HttpPost("/api/courses/{courseId:int}/comments")]
        public async Task<IActionResult> CreateComment(int courseId, CourseCommentCreateDto dto)
        {
            var course = await _context.Courses.FindAsync(courseId);
            if (course == null) return NotFound();
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!User.IsInRole("Admin") && course.InstructorId != userId) return Forbid();
            var content = dto.Content.Trim();
            if (content.Length == 0) return BadRequest(new { message = "Content is required." });
            var comment = new CourseComment { CourseId = courseId, UserId = userId, Content = content };
            _context.CourseComments.Add(comment);
            await _context.SaveChangesAsync();
            return Ok(new { comment.CourseCommentId, comment.UserId, comment.Content, comment.CreatedAt });
        }
    }
}

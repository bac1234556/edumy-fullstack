using EduMy.Backend.Data;
using EduMy.Backend.DTOs;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using EduMy.Backend.Services;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CoursesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly EduMy.Backend.Services.IMachineLearningService _mlService;
        private readonly ICourseProgressService _progressService;
        private readonly ICourseWorkflowService _workflowService;

        public CoursesController(ApplicationDbContext context, EduMy.Backend.Services.IMachineLearningService mlService,
            ICourseProgressService progressService, ICourseWorkflowService workflowService)
        {
            _context = context;
            _mlService = mlService;
            _progressService = progressService;
            _workflowService = workflowService;
        }

        [HttpGet("recommend")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetRecommendations()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var recs = await _mlService.RecommendCoursesAsync(userId);
            if (recs != null && recs.RecommendedCourseIds.Any())
            {
                var recommendedCourses = await _context.Courses
                    .Where(c => !c.IsDeleted && c.Status == "Published" && recs.RecommendedCourseIds.Contains(c.CourseId))
                    .Include(c => c.Instructor)
                    .ToListAsync();
                
                // Sort to match recommendation order
                var orderedCourses = recs.RecommendedCourseIds
                    .Select(id => recommendedCourses.FirstOrDefault(c => c.CourseId == id))
                    .Where(c => c != null)
                    .ToList();

                return Ok(orderedCourses);
            }

            return Ok(new List<Course>());
        }

        [HttpGet]
        public async Task<IActionResult> GetCourses([FromQuery] CourseQueryDto queryDto)
        {
            queryDto.PageNumber = Math.Max(1, queryDto.PageNumber);
            queryDto.PageSize = Math.Clamp(queryDto.PageSize, 1, 100);
            var query = _context.Courses.Where(c => !c.IsDeleted && c.Status == "Published").AsQueryable();

            if (!string.IsNullOrEmpty(queryDto.Search))
            {
                query = query.Where(c => c.Title.Contains(queryDto.Search));
            }

            if (queryDto.CategoryId.HasValue)
            {
                query = query.Where(c => c.CourseCategories.Any(cc => cc.CategoryId == queryDto.CategoryId.Value));
            }

            if (queryDto.MinPrice.HasValue)
            {
                query = query.Where(c => c.Price >= queryDto.MinPrice.Value);
            }

            if (queryDto.MaxPrice.HasValue)
            {
                query = query.Where(c => c.Price <= queryDto.MaxPrice.Value);
            }

            query = queryDto.SortBy?.ToLower() switch
            {
                "price" => queryDto.SortOrder?.ToLower() == "asc" ? query.OrderBy(c => c.Price) : query.OrderByDescending(c => c.Price),
                "rating" => queryDto.SortOrder?.ToLower() == "asc" ? query.OrderBy(c => c.AverageRating) : query.OrderByDescending(c => c.AverageRating),
                "newest" => queryDto.SortOrder?.ToLower() == "asc" ? query.OrderBy(c => c.CreatedAt) : query.OrderByDescending(c => c.CreatedAt),
                _ => query.OrderByDescending(c => c.CreatedAt) // Default sorting
            };

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)queryDto.PageSize);

            var items = await query
                .Skip((queryDto.PageNumber - 1) * queryDto.PageSize)
                .Take(queryDto.PageSize)
                .Include(c => c.CourseCategories)
                    .ThenInclude(cc => cc.Category)
                .Include(c => c.Instructor)
                .ToListAsync();

            var result = new PagedResultDto<Course>
            {
                Items = items,
                PageNumber = queryDto.PageNumber,
                PageSize = queryDto.PageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            };

            return Ok(result);
        }

        [HttpGet("suggestions")]
        public async Task<IActionResult> GetSuggestions([FromQuery] string keyword, [FromQuery] int limit = 8)
        {
            keyword = (keyword ?? string.Empty).Trim();
            if (keyword.Length < 2) return Ok(Array.Empty<object>());
            if (keyword.Length > 100) return BadRequest(new { message = "Keyword is too long." });
            limit = Math.Clamp(limit, 1, 20);

            var escaped = keyword.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]");
            var normalized = keyword.ToLower();
            var pattern = escaped + "%";

            var items = await _context.Courses.AsNoTracking()
                .Where(c => !c.IsDeleted && c.Status == "Published" && EF.Functions.Like(c.Title, pattern))
                .OrderByDescending(c => c.Title.ToLower() == normalized)
                .ThenByDescending(c => c.StudentCount)
                .ThenByDescending(c => c.CreatedAt)
                .Take(limit)
                .Select(c => new
                {
                    courseId = c.CourseId,
                    c.Title,
                    c.Slug,
                    c.ThumbnailUrl,
                    instructorName = c.Instructor != null ? c.Instructor.FullName : string.Empty
                })
                .ToListAsync();
            return Ok(items);
        }

        [HttpGet("my-courses")]
        [Authorize(Roles = "Instructor")]
        public async Task<IActionResult> GetMyCourses()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var courses = await _context.Courses
                .Where(c => c.InstructorId == userId && !c.IsDeleted)
                .Include(c => c.CourseCategories)
                    .ThenInclude(cc => cc.Category)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return Ok(courses);
        }

        [HttpGet("enrolled")]
        [Authorize(Roles = "Student,Instructor,Admin")]
        public async Task<IActionResult> GetEnrolledCourses()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var enrollments = await _context.Enrollments
                .Where(e => e.UserId == userId)
                .Include(e => e.Course)
                    .ThenInclude(c => c.Instructor)
                .OrderByDescending(e => e.EnrolledAt)
                .ToListAsync();

            foreach (var enrollment in enrollments)
                await _progressService.RecalculateEnrollmentProgressAsync(enrollment.EnrollmentId);

            var courses = enrollments.Select(e => new {
                e.CourseId,
                e.Course?.Title,
                e.Course?.ThumbnailUrl,
                InstructorName = e.Course?.Instructor?.FullName,
                e.EnrolledAt,
                e.TotalLessons,
                e.CompletedLessons,
                e.ProgressPercentage,
                e.LastAccessedAt,
                e.CompletedAt
            });

            return Ok(courses);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCourse(int id)
        {
            var course = await _context.Courses.AsNoTracking()
                .Include(c => c.CourseCategories)
                    .ThenInclude(cc => cc.Category)
                .Include(c => c.Instructor)
                .Include(c => c.Sections)
                    .ThenInclude(s => s.Lessons)
                .Include(c => c.Reviews)
                    .ThenInclude(r => r.User)
                .Include(c => c.Reviews)
                    .ThenInclude(r => r.Replies)
                        .ThenInclude(r => r.User)
                .Include(c => c.Comments)
                    .ThenInclude(c => c.User)
                .Include(c => c.CourseTags)
                    .ThenInclude(ct => ct.Tag)
                .Include(c => c.Enrollments)
                .FirstOrDefaultAsync(c => c.CourseId == id && !c.IsDeleted);

            if (course == null) return NotFound();
            var currentUserId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId) ? parsedId : 0;
            var canManage = User.IsInRole("Admin") || currentUserId == course.InstructorId;
            if (course.Status != "Published" && !canManage) return NotFound();

            var sections = course.Sections.OrderBy(s => s.OrderIndex).Select(s => new
            {
                s.SectionId,
                s.Title,
                s.OrderIndex,
                lessons = s.Lessons.OrderBy(l => l.OrderIndex)
                    .Where(l => canManage || !l.IsDraft)
                    .Select(l => new
                    {
                        l.LessonId, l.Title, l.Duration, l.OrderIndex, l.IsPreview,
                        l.ResourceType,
                        FileUrl = canManage ? l.FileUrl : null,
                        VideoUrl = canManage ? l.VideoUrl : null,
                        l.OriginalFileName,
                        l.ContentType, l.FileSizeBytes, l.UploadedAt,
                        isDraft = canManage && l.IsDraft
                    })
            });

            return Ok(new
            {
                course.CourseId, course.InstructorId,
                categoryId = course.CourseCategories.FirstOrDefault()?.CategoryId ?? 0,
                categoryIds = course.CourseCategories.Select(cc => cc.CategoryId).ToList(),
                categories = course.CourseCategories.Select(cc => new { cc.Category.CategoryId, cc.Category.Name }).ToList(),
                course.Title, course.Slug,
                course.Description, course.Price, course.ThumbnailUrl, course.Level, course.Status,
                course.AverageRating, course.ReviewCount, course.StudentCount, course.CreatedAt, course.UpdatedAt,
                category = course.CourseCategories.FirstOrDefault() == null ? null : new { course.CourseCategories.First().Category.CategoryId, course.CourseCategories.First().Category.Name },
                instructor = course.Instructor == null ? null : new
                {
                    course.Instructor.UserId, course.Instructor.FullName, course.Instructor.AvatarUrl,
                    course.Instructor.Headline, course.Instructor.Bio
                },
                sections,
                reviews = course.Reviews.OrderByDescending(r => r.CreatedAt).Select(r => new
                {
                    r.ReviewId, r.UserId, r.Rating, r.Comment, r.SentimentLabel, r.CreatedAt,
                    user = r.User == null ? null : new { r.User.UserId, r.User.FullName, r.User.AvatarUrl, r.User.Role },
                    replies = r.Replies.OrderBy(x => x.CreatedAt).Select(x => new
                    {
                        x.ReviewReplyId, x.UserId, x.Content, x.CreatedAt, x.UpdatedAt,
                        user = new { x.User.UserId, x.User.FullName, x.User.AvatarUrl, x.User.Role }
                    })
                }),
                comments = course.Comments.OrderByDescending(c => c.CreatedAt).Select(c => new
                {
                    c.CourseCommentId, c.UserId, c.Content, c.CreatedAt,
                    user = new { c.User.UserId, c.User.FullName, c.User.AvatarUrl, c.User.Role }
                }),
                courseTags = course.CourseTags.Select(ct => new { ct.TagId, tag = new { ct.Tag.Id, ct.Tag.Name } }),
                enrollmentCount = course.Enrollments.Count
            });
        }

        [Authorize(Roles = "Instructor")]
        [HttpPost("ai-suggest")]
        public async Task<IActionResult> GetAiSuggest([FromBody] AiSuggestRequestDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest("Title is required.");
            }

            var classification = await _mlService.ClassifyCourseAsync(dto.Title, dto.Description ?? "");
            if (classification == null)
                return Ok(new { recommendedCategory = (object?)null, alternatives = Array.Empty<object>(), source = "unavailable" });

            var categories = await _context.Categories.AsNoTracking().Where(c => c.IsActive && c.Name != "Uncategorized").ToListAsync();
            var matched = categories.FirstOrDefault(c => c.Name.Equals(classification.Category, StringComparison.OrdinalIgnoreCase));
            var recommendation = matched != null && classification.Confidence >= 0.65
                ? new { matched.CategoryId, matched.Name, confidence = classification.Confidence }
                : null;
            var alternatives = classification.Alternatives
                .Select(candidate => new
                {
                    category = categories.FirstOrDefault(c => c.Name.Equals(candidate.Category, StringComparison.OrdinalIgnoreCase)),
                    candidate.Confidence
                })
                .Where(item => item.category != null)
                .Take(3)
                .Select(item => new { item.category!.CategoryId, item.category.Name, confidence = item.Confidence })
                .ToList();

            return Ok(new { recommendedCategory = recommendation, alternatives, source = classification.Source });
        }

        [Authorize(Roles = "Instructor")]
        [HttpPost]
        public async Task<IActionResult> CreateCourse([FromBody] CreateCourseDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var title = dto.Title.Trim();
            var description = dto.Description?.Trim();
            var catIds = dto.CategoryIds ?? new List<int>();
            if (!catIds.Any() && dto.CategoryId > 0)
            {
                catIds.Add(dto.CategoryId);
            }
            if (!catIds.Any())
                return BadRequest(new { code = "INVALID_CATEGORY", message = "Phải chọn ít nhất 1 Category." });

            var existingCats = await _context.Categories
                .Where(c => catIds.Contains(c.CategoryId) && c.IsActive && c.Name != "Uncategorized")
                .Select(c => c.CategoryId)
                .ToListAsync();

            if (existingCats.Count != catIds.Count)
                return BadRequest(new { code = "INVALID_CATEGORY", message = "Một hoặc nhiều CategoryId không tồn tại." });

            var course = new Course
            {
                InstructorId = userId,
                Title = title,
                Description = description,
                Price = dto.Price,
                Level = "Beginner", // Kept internally for legacy non-null database compatibility.
                ThumbnailUrl = string.IsNullOrWhiteSpace(dto.ThumbnailUrl) ? null : dto.ThumbnailUrl.Trim(),
                Slug = GenerateSlug(title),
                Status = "Draft",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            foreach (var catId in catIds)
            {
                course.CourseCategories.Add(new CourseCategory { CategoryId = catId });
            }

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCourse), new { id = course.CourseId }, ToCourseEditDto(course));
        }

        [Authorize(Roles = "Instructor")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCourse(int id, [FromBody] UpdateCourseDto dto)
        {
            var existing = await _context.Courses
                .Include(c => c.CourseCategories)
                .FirstOrDefaultAsync(c => c.CourseId == id && !c.IsDeleted);
            if (existing == null) return NotFound();
            
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId) || existing.InstructorId != userId)
            {
                return Forbid();
            }

            var catIds = dto.CategoryIds ?? new List<int>();
            if (!catIds.Any() && dto.CategoryId > 0)
            {
                catIds.Add(dto.CategoryId);
            }
            if (!catIds.Any())
                return BadRequest(new { code = "INVALID_CATEGORY", message = "Phải chọn ít nhất 1 Category." });

            var existingCats = await _context.Categories
                .Where(c => catIds.Contains(c.CategoryId) && c.IsActive && c.Name != "Uncategorized")
                .Select(c => c.CategoryId)
                .ToListAsync();

            if (existingCats.Count != catIds.Count)
                return BadRequest(new { code = "INVALID_CATEGORY", message = "Một hoặc nhiều CategoryId không tồn tại." });

            var title = dto.Title.Trim();
            var description = dto.Description?.Trim();
            if (existing.Title != title || existing.Description != description)
            {
                existing.NeedsReanalysis = true;
                existing.Slug = GenerateSlug(title);
            }

            existing.Title = title;
            existing.Description = description;
            existing.Price = dto.Price;
            existing.ThumbnailUrl = string.IsNullOrWhiteSpace(dto.ThumbnailUrl) ? existing.ThumbnailUrl : dto.ThumbnailUrl.Trim();

            // Load and update CourseCategories
            var toRemove = existing.CourseCategories.Where(cc => !catIds.Contains(cc.CategoryId)).ToList();
            _context.CourseCategories.RemoveRange(toRemove);
            foreach (var cc in toRemove)
            {
                existing.CourseCategories.Remove(cc);
            }

            var currentCatIds = existing.CourseCategories.Select(cc => cc.CategoryId).ToHashSet();
            foreach (var catId in catIds)
            {
                if (!currentCatIds.Contains(catId))
                {
                    existing.CourseCategories.Add(new CourseCategory { CourseId = id, CategoryId = catId });
                }
            }

            var statusResult = await _workflowService.ApplyStatusAsync(existing, dto.Status);
            if (!statusResult.Success)
                return BadRequest(new
                {
                    code = dto.Status.Equals("Published", StringComparison.OrdinalIgnoreCase) ? "COURSE_NOT_READY_TO_PUBLISH" : "INVALID_COURSE_STATUS",
                    message = dto.Status.Equals("Published", StringComparison.OrdinalIgnoreCase)
                        ? "Khóa học chưa đủ điều kiện để xuất bản." : "Trạng thái khóa học không hợp lệ.",
                    errors = statusResult.Errors
                });

            existing.UpdatedAt = DateTime.UtcNow;
            
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CourseExists(id)) return NotFound();
                else throw;
            }

            return Ok(ToCourseEditDto(existing));
        }

        [Authorize(Roles = "Instructor,Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.Courses.FindAsync(id);
            if (course == null) return NotFound();
            if (course.IsDeleted) return Ok(new { code = "COURSE_ALREADY_DELETED", message = "Khóa học đã được xóa trước đó." });

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId) || (!User.IsInRole("Admin") && course.InstructorId != userId))
            {
                return Forbid();
            }

            course.IsDeleted = true;
            course.DeletedAt = DateTime.UtcNow;
            course.DeletedByUserId = userId;
            course.Status = "Archived";
            course.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { code = "COURSE_DELETED", message = "Khóa học đã được gỡ khỏi danh sách công khai; dữ liệu lịch sử được giữ lại." });
        }

        [Authorize(Roles = "Instructor")]
        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateStatus(int id, [FromBody] string newStatus)
        {
            newStatus = newStatus?.Trim() ?? string.Empty;
            if (!_workflowService.ValidStatuses.Contains(newStatus, StringComparer.OrdinalIgnoreCase))
                return BadRequest(new { code = "INVALID_COURSE_STATUS", message = "Invalid status." });

            var course = await _context.Courses
                .Include(c => c.CourseTags)
                .Include(c => c.MlAnalyses)
                .Include(c => c.CourseCategories)
                .FirstOrDefaultAsync(c => c.CourseId == id && !c.IsDeleted);

            if (course == null) return NotFound();

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId) || course.InstructorId != userId)
            {
                return Forbid();
            }
            
            if (newStatus == "Analyzing")
            {
                course.Status = "Analyzing";
                await _context.SaveChangesAsync();

                var classification = await _mlService.ClassifyCourseAsync(course.Title, course.Description ?? "");
                var contentAnalysis = await _mlService.AnalyzeContentAsync(course.Title, course.Description ?? "");

                if (classification != null && contentAnalysis != null)
                {
                    var cat = await _context.Categories.FirstOrDefaultAsync(c => c.Name == classification.Category);
                    if (cat != null && classification.Confidence >= 0.65) 
                    {
                        var toRemoveMl = course.CourseCategories.ToList();
                        _context.CourseCategories.RemoveRange(toRemoveMl);
                        course.CourseCategories.Clear();
                        course.CourseCategories.Add(new CourseCategory { CourseId = id, CategoryId = cat.CategoryId });
                    }

                    var oldTags = await _context.Set<CourseTag>().Where(ct => ct.CourseId == id).ToListAsync();
                    if (oldTags.Any()) _context.Set<CourseTag>().RemoveRange(oldTags);
                    
                    foreach (var tagName in contentAnalysis.Tags)
                    {
                        var tag = await _context.Tags.FirstOrDefaultAsync(t => t.Name == tagName);
                        if (tag == null)
                        {
                            tag = new Tag { Name = tagName };
                            _context.Tags.Add(tag);
                            await _context.SaveChangesAsync();
                        }
                        course.CourseTags.Add(new CourseTag { CourseId = id, TagId = tag.Id });
                    }

                    var isToxic = contentAnalysis.IsToxic || contentAnalysis.ToxicityScore > 0.5;
                    var confidence = classification.Confidence;

                    string analysisStatus = "AutoApproved";
                    string finalCourseStatus = "PendingApproval";

                    if (isToxic)
                    {
                        analysisStatus = "NeedsManualReview";
                        finalCourseStatus = "NeedsReview";
                    }
                    else if (confidence < 0.65)
                    {
                        analysisStatus = "NeedsManualReview";
                        finalCourseStatus = "NeedsReview";
                    }
                    else if (confidence < 0.85)
                    {
                        analysisStatus = "InstructorConfirmationRequired";
                        finalCourseStatus = "NeedsReview";
                    }

                    var mlAnalysis = new CourseMlAnalysis
                    {
                        CourseId = id,
                        PrimaryCategory = classification.Category,
                        SubCategory = classification.Category + " Sub",
                        SuggestedLevel = "Intermediate",
                        Confidence = confidence,
                        QualityScore = (int)(contentAnalysis.QualityScore * 100),
                        RiskLevel = isToxic ? "High" : "Low",
                        RawResponseJson = System.Text.Json.JsonSerializer.Serialize(new {
                            classification,
                            contentAnalysis
                        }),
                        Status = analysisStatus,
                        CreatedAt = DateTime.UtcNow
                    };
                    course.MlAnalyses.Add(mlAnalysis);

                    course.Status = finalCourseStatus;
                    course.NeedsReanalysis = false;
                    course.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return Ok(new { 
                        message = $"Analysis completed. Course set to {finalCourseStatus}.", 
                        categoryId = course.CourseCategories.FirstOrDefault()?.CategoryId ?? 0,
                        analysis = new {
                            primaryCategory = mlAnalysis.PrimaryCategory,
                            confidence = mlAnalysis.Confidence,
                            qualityScore = mlAnalysis.QualityScore,
                            riskLevel = mlAnalysis.RiskLevel,
                            status = mlAnalysis.Status
                        }
                    });
                }
                else
                {
                    course.Status = "NeedsReview";
                    course.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync();
                    return Ok(new { message = "ML Service unavailable. Set status to NeedsReview for manual moderation." });
                }
            }

            var statusResult = await _workflowService.ApplyStatusAsync(course, newStatus);
            if (!statusResult.Success)
                return BadRequest(new
                {
                    code = newStatus.Equals("Published", StringComparison.OrdinalIgnoreCase) ? "COURSE_NOT_READY_TO_PUBLISH" : "INVALID_COURSE_STATUS",
                    message = "Khóa học chưa đủ điều kiện để xuất bản.",
                    errors = statusResult.Errors
                });
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Status updated to {course.Status}", course.Status });
        }

        private static string GenerateSlug(string title)
        {
            if (string.IsNullOrEmpty(title)) return "";
            
            string[] vietnameseSigns = new string[]
            {
                "aAeEoOuUiIdDyY",
                "áàạảãâấầậẩẫăắằặẳẵ",
                "ÁÀẠẢÃÂẤẦẬẨẪĂẮẰẶẲẴ",
                "éèẹẻẽêếềệểễ",
                "ÉÈẸẺẼÊẾỀỆỂỄ",
                "óòọỏõôốồộổỗơớờợởỡ",
                "ÓÒỌỎÕÔỐỒỘỔỖƠỚỜỢỞỠ",
                "úùụủũưứừựửữ",
                "ÚÙỤỦŨƯỨỪỰỬỮ",
                "íìịỉĩ",
                "ÍÌỊỈĨ",
                "đ",
                "Đ",
                "ýỳỵỷỹ",
                "ÝỲỴỶỸ"
            };

            for (int i = 1; i < vietnameseSigns.Length; i++)
            {
                for (int j = 0; j < vietnameseSigns[i].Length; j++)
                {
                    title = title.Replace(vietnameseSigns[i][j], vietnameseSigns[0][i - 1]);
                }
            }

            var slug = title.ToLower().Trim()
                .Replace(" ", "-")
                .Replace("--", "-");
            slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[^a-z0-9\-]", "");
            slug += "-" + Guid.NewGuid().ToString("N")[..6];
            return slug;
        }

        private bool CourseExists(int id)
        {
            return _context.Courses.Any(e => e.CourseId == id);
        }

        private static object ToCourseEditDto(Course course) => new
        {
            course.CourseId,
            course.Title,
            course.Description,
            course.Price,
            course.Level,
            categoryId = course.CourseCategories.FirstOrDefault()?.CategoryId ?? 0,
            categoryIds = course.CourseCategories.Select(cc => cc.CategoryId).ToList(),
            course.ThumbnailUrl,
            course.Status,
            course.Slug,
            course.UpdatedAt
        };
        [HttpPost("{courseId}/lessons/{lessonId}/complete")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> CompleteLesson(int courseId, int lessonId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();
            try
            {
                var progress = await _progressService.SetLessonCompletionAsync(userId, courseId, lessonId, true);
                if (progress == null) return Forbid("You must be enrolled to complete lessons.");
                return Ok(new { lessonId, isCompleted = true, progress.TotalLessons, progress.CompletedLessons, progress.ProgressPercentage });
            }
            catch (InvalidOperationException ex) when (ex.Message == "LESSON_NOT_LEARNABLE")
            {
                return BadRequest(new { code = ex.Message, message = "Lesson does not belong to this course or is still a draft." });
            }
        }

        [HttpDelete("{courseId}/lessons/{lessonId}/complete")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> UncompleteLesson(int courseId, int lessonId)
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
            try
            {
                var progress = await _progressService.SetLessonCompletionAsync(userId, courseId, lessonId, false);
                if (progress == null) return Forbid();
                return Ok(new { lessonId, isCompleted = false, progress.TotalLessons, progress.CompletedLessons, progress.ProgressPercentage });
            }
            catch (InvalidOperationException ex) when (ex.Message == "LESSON_NOT_LEARNABLE")
            {
                return BadRequest(new { code = ex.Message, message = "Lesson does not belong to this course or is still a draft." });
            }
        }

        [HttpGet("{courseId}/progress")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> GetCourseProgress(int courseId)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();
            var progress = await _progressService.GetCourseProgressAsync(userId, courseId);
            if (progress == null) return Forbid();
            return Ok(progress);
        }

        [HttpPost("{id}/reviews")]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> AddReview(int id, [FromBody] CreateReviewDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            if (dto.Rating is < 1 or > 5) return BadRequest(new { message = "Rating must be between 1 and 5." });
            dto.Comment = (dto.Comment ?? string.Empty).Trim();
            if (dto.Comment.Length is < 1 or > 2000) return BadRequest(new { message = "Comment must be between 1 and 2000 characters." });

            // Verify course exists
            var course = await _context.Courses.Include(c => c.Reviews).FirstOrDefaultAsync(c => c.CourseId == id);
            if (course == null) return NotFound("Course not found");

            // Verify enrollment
            var isEnrolled = await _context.Enrollments.AnyAsync(e => e.CourseId == id && e.UserId == userId);
            if (!isEnrolled) return BadRequest(new { message = "Bạn cần mua khóa học này trước khi để lại bình luận và đánh giá!" });

            // Verify not already reviewed
            if (course.Reviews.Any(r => r.UserId == userId))
            {
                return BadRequest("You have already reviewed this course.");
            }

            // ML Integration: Analyze Sentiment
            string sentimentLabel = "Unknown";
            double sentimentScore = 0.5;

            var sentimentResult = await _mlService.AnalyzeSentimentAsync(dto.Comment, dto.Rating);
            if (sentimentResult != null)
            {
                sentimentLabel = NormalizeSentimentLabel(sentimentResult.Label);
                sentimentScore = sentimentResult.Score;
            }

            var review = new Review
            {
                UserId = userId,
                CourseId = id,
                Rating = dto.Rating,
                Comment = dto.Comment,
                SentimentLabel = sentimentLabel,
                SentimentScore = sentimentScore,
                SentimentConfidence = sentimentResult?.Confidence ?? 0,
                SentimentSource = sentimentResult?.Source ?? "unavailable",
                SentimentModelVersion = "sentiment-hybrid-v2",
                SentimentUpdatedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reviews.Add(review);
            
            // Update Course Average Rating
            course.Reviews.Add(review);
            course.AverageRating = Math.Round(course.Reviews.Average(r => r.Rating), 1, MidpointRounding.AwayFromZero);
            course.ReviewCount = course.Reviews.Count;
            _context.Entry(course).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok(review);
        }

        private static string NormalizeSentimentLabel(string? label) => label?.Trim().ToLowerInvariant() switch
        {
            "positive" => "Positive",
            "negative" => "Negative",
            "neutral" => "Neutral",
            _ => "Unknown"
        };
    }

    public class CreateReviewDto
    {
        public int Rating { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    public class AiSuggestRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}

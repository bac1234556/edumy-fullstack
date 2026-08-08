using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Services;

public sealed record CourseStatusResult(bool Success, IReadOnlyList<string> Errors);

public interface ICourseWorkflowService
{
    IReadOnlyCollection<string> ValidStatuses { get; }
    Task<CourseStatusResult> ApplyStatusAsync(Course course, string status);
}

public sealed class CourseWorkflowService : ICourseWorkflowService
{
    private readonly ApplicationDbContext _db;
    private static readonly string[] Statuses = ["Draft", "Published", "Unpublished", "PendingApproval", "Analyzing", "NeedsReview"];
    public IReadOnlyCollection<string> ValidStatuses => Statuses;
    public CourseWorkflowService(ApplicationDbContext db) => _db = db;

    public async Task<CourseStatusResult> ApplyStatusAsync(Course course, string status)
    {
        status = status.Trim();
        if (!Statuses.Contains(status, StringComparer.OrdinalIgnoreCase))
            return new(false, ["Trạng thái khóa học không hợp lệ."]);
        status = Statuses.Single(s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
        if (status == "Published")
        {
            var errors = await ValidateForPublishing(course);
            if (errors.Count > 0) return new(false, errors);
        }
        course.Status = status;
        course.UpdatedAt = DateTime.UtcNow;
        return new(true, Array.Empty<string>());
    }

    private async Task<List<string>> ValidateForPublishing(Course course)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(course.Title)) errors.Add("Chưa nhập tiêu đề khóa học.");
        if (string.IsNullOrWhiteSpace(course.Description)) errors.Add("Chưa nhập mô tả khóa học.");
        if (course.Price < 0) errors.Add("Giá khóa học không hợp lệ.");
        if (string.IsNullOrWhiteSpace(course.ThumbnailUrl)) errors.Add("Chưa có ảnh đại diện khóa học.");
        var hasValidCategory = await _db.CourseCategories.AnyAsync(cc => cc.CourseId == course.CourseId && cc.Category.IsActive && cc.Category.Name != "Uncategorized");
        if (!hasValidCategory) errors.Add("Chưa chọn danh mục hợp lệ.");
        if (!await _db.Users.AnyAsync(u => u.UserId == course.InstructorId && (u.Role == "Instructor" || u.Role == "Admin"))) errors.Add("Giảng viên không hợp lệ.");
        if (!await _db.CourseSections.AnyAsync(s => s.CourseId == course.CourseId)) errors.Add("Khóa học chưa có chương.");
        if (!await _db.Lessons.AnyAsync(l => l.Section!.CourseId == course.CourseId && !l.IsDraft)) errors.Add("Khóa học chưa có bài giảng có thể học.");
        return errors;
    }
}

using System.Data;
using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Services;

public sealed record OrderCompletionResult(bool Success, bool AlreadyCompleted, string Status, string? Error = null);

public interface IOrderCompletionService
{
    Task<OrderCompletionResult> CompletePaidOrderAsync(int orderId, int? expectedUserId = null);
}

public sealed class OrderCompletionService : IOrderCompletionService
{
    private readonly ApplicationDbContext _db;
    public OrderCompletionService(ApplicationDbContext db) => _db = db;

    public async Task<OrderCompletionResult> CompletePaidOrderAsync(int orderId, int? expectedUserId = null)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await _db.Orders.Include(o => o.OrderItems).ThenInclude(i => i.Course)
            .FirstOrDefaultAsync(o => o.OrderId == orderId);
        if (order == null || expectedUserId.HasValue && order.UserId != expectedUserId)
            return new(false, false, "NotFound", "Order not found.");
        if (order.Status is "Cancelled" or "Failed")
            return new(false, false, order.Status, "A failed or cancelled order cannot be completed.");

        var alreadyCompleted = order.Status is "Paid" or "Completed";
        if (!alreadyCompleted && order.Status != "Pending")
            return new(false, false, order.Status, "Order status is not eligible for completion.");
        if (!alreadyCompleted && order.OrderItems.Any(item => item.Course == null || item.Course.IsDeleted || item.Course.Status != "Published"))
            return new(false, false, "CourseUnavailable", "One or more courses are no longer available for purchase.");

        var courseIds = order.OrderItems.Select(i => i.CourseId).Distinct().ToArray();
        var existingCourseIds = await _db.Enrollments
            .Where(e => e.UserId == order.UserId && courseIds.Contains(e.CourseId))
            .Select(e => e.CourseId).ToListAsync();
        var createdCourseIds = new HashSet<int>();
        foreach (var courseId in courseIds.Except(existingCourseIds))
        {
            var totalLessons = await _db.Lessons.CountAsync(l => l.Section!.CourseId == courseId && !l.IsDraft);
            _db.Enrollments.Add(new Enrollment
            {
                UserId = order.UserId,
                CourseId = courseId,
                EnrolledAt = DateTime.UtcNow,
                TotalLessons = totalLessons
            });
            createdCourseIds.Add(courseId);
        }

        var wishes = await _db.Wishlists.Where(w => w.UserId == order.UserId && courseIds.Contains(w.CourseId)).ToListAsync();
        if (wishes.Count > 0) _db.Wishlists.RemoveRange(wishes);

        var cartItems = await _db.CartItems.Where(i => i.Cart!.UserId == order.UserId && courseIds.Contains(i.CourseId)).ToListAsync();
        if (cartItems.Count > 0) _db.CartItems.RemoveRange(cartItems);

        foreach (var item in order.OrderItems.Where(i => createdCourseIds.Contains(i.CourseId)))
        {
            if (item.Course != null) item.Course.StudentCount += 1;
        }
        order.Status = "Completed";
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return new(true, alreadyCompleted, order.Status);
    }
}

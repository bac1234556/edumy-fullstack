using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Services;

public sealed record AccountDeletionResult(bool Success, int StatusCode, string Code, string Message,
    int PublishedCourses = 0, int Enrollments = 0, int Orders = 0, bool AlreadyDeleted = false);

public interface IAccountDeletionService
{
    Task<AccountDeletionResult> DeleteAsync(int targetUserId, int actorUserId, bool selfDelete);
}

public sealed class AccountDeletionService : IAccountDeletionService
{
    private readonly ApplicationDbContext _db;
    public AccountDeletionService(ApplicationDbContext db) => _db = db;

    public async Task<AccountDeletionResult> DeleteAsync(int targetUserId, int actorUserId, bool selfDelete)
    {
        var user = await _db.Users.Include(u => u.RefreshTokens).FirstOrDefaultAsync(u => u.UserId == targetUserId);
        if (user == null) return new(false, 404, "USER_NOT_FOUND", "Không tìm thấy tài khoản.");
        if (user.IsDeleted) return new(true, 200, "ACCOUNT_ALREADY_DELETED", "Tài khoản đã được xóa trước đó.", AlreadyDeleted: true);
        if (!selfDelete && targetUserId == actorUserId)
            return new(false, 400, "ADMIN_SELF_DELETE_NOT_ALLOWED", "Admin không thể xóa tài khoản đang sử dụng.");

        var published = await _db.Courses.CountAsync(c => c.InstructorId == targetUserId && !c.IsDeleted && c.Status == "Published");
        var enrollments = await _db.Enrollments.CountAsync(e => e.UserId == targetUserId);
        var orders = await _db.Orders.CountAsync(o => o.UserId == targetUserId);
        if (selfDelete && user.Role == "Instructor" && published > 0)
            return new(false, 409, "ACCOUNT_HAS_ACTIVE_COURSES", "Bạn đang có khóa học hoạt động. Vui lòng ngừng xuất bản hoặc liên hệ quản trị viên trước khi xóa tài khoản.", published, enrollments, orders);
        if (user.Role == "Admin")
        {
            var activeAdmins = await _db.Users.CountAsync(u => u.Role == "Admin" && u.IsActive && !u.IsDeleted);
            if (activeAdmins <= 1)
                return new(false, 409, "LAST_ACTIVE_ADMIN", "Không thể xóa Admin hoạt động cuối cùng.");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        if (!selfDelete && user.Role == "Instructor")
        {
            var activeCourses = await _db.Courses.Where(c => c.InstructorId == targetUserId && !c.IsDeleted && c.Status == "Published").ToListAsync();
            foreach (var course in activeCourses) { course.Status = "Archived"; course.UpdatedAt = DateTime.UtcNow; }
        }
        var wishes = await _db.Wishlists.Where(w => w.UserId == targetUserId).ToListAsync();
        if (wishes.Count > 0) _db.Wishlists.RemoveRange(wishes);
        var cartItems = await _db.CartItems.Where(item => item.Cart != null && item.Cart.UserId == targetUserId).ToListAsync();
        if (cartItems.Count > 0) _db.CartItems.RemoveRange(cartItems);
        foreach (var token in user.RefreshTokens.Where(token => token.IsActive)) token.Revoked = DateTime.UtcNow;

        user.IsDeleted = true; user.IsActive = false; user.DeletedAt = DateTime.UtcNow; user.DeletedByUserId = actorUserId;
        user.FullName = "Người dùng đã xóa"; user.Email = $"deleted-{user.UserId}-{Guid.NewGuid():N}@deleted.edumy";
        user.PasswordHash = null; user.AvatarUrl = null; user.Headline = null; user.Bio = null;
        user.Provider = null; user.ProviderUserId = null; user.ResetToken = null; user.ResetTokenExpiry = null;
        await _db.SaveChangesAsync(); await transaction.CommitAsync();
        return new(true, 200, "ACCOUNT_DELETED", "Tài khoản đã được xóa.", published, enrollments, orders);
    }
}

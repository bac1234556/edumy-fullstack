using System.Security.Claims;
using EduMy.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Middlewares;

public sealed class ActiveUserMiddleware
{
    private readonly RequestDelegate _next;

    public ActiveUserMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, IConfiguration configuration)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            int.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            var state = await db.Users.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.IsActive, u.IsDeleted })
                .SingleOrDefaultAsync();

            if (state?.IsDeleted == true)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { code = "ACCOUNT_DELETED", message = "Tài khoản đã được xóa." });
                return;
            }
            if (state?.IsActive == false)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    code = "ACCOUNT_INACTIVE",
                    message = "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.",
                    adminEmail = configuration["Support:AdminEmail"]
                });
                return;
            }
        }

        await _next(context);
    }
}

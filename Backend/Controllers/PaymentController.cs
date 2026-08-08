using System.Security.Claims;
using EduMy.Backend.Data;
using EduMy.Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IOrderCompletionService _completion;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public PaymentController(ApplicationDbContext db, IOrderCompletionService completion,
        IWebHostEnvironment environment, IConfiguration configuration)
    {
        _db = db;
        _completion = completion;
        _environment = environment;
        _configuration = configuration;
    }

    [HttpPost("simulate-success")]
    public async Task<IActionResult> SimulateSuccess(PaymentResultDto result)
    {
        if (!_environment.IsDevelopment()) return NotFound();
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        return ToActionResult(await _completion.CompletePaidOrderAsync(result.OrderId, userId));
    }

    [HttpPost("callback")]
    public async Task<IActionResult> PaymentCallback(PaymentResultDto result, [FromHeader(Name = "X-Payment-Signature")] string? signature)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var configuredSecret = _configuration["Payment:CallbackSecret"];
        if (string.IsNullOrWhiteSpace(configuredSecret) || !string.Equals(signature, configuredSecret, StringComparison.Ordinal))
            return Unauthorized(new { code = "INVALID_PAYMENT_SIGNATURE", message = "Payment callback signature is invalid." });

        if (!result.Success)
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == result.OrderId && o.UserId == userId);
            if (order == null) return NotFound();
            if (order.Status == "Pending")
            {
                order.Status = "Cancelled";
                await _db.SaveChangesAsync();
            }
            return Ok(new { message = "Payment cancelled", status = order.Status });
        }
        return ToActionResult(await _completion.CompletePaidOrderAsync(result.OrderId, userId));
    }

    private IActionResult ToActionResult(OrderCompletionResult result)
    {
        if (result.Status == "NotFound") return NotFound(new { message = result.Error });
        if (!result.Success) return Conflict(new { message = result.Error, status = result.Status });
        return Ok(new
        {
            message = result.AlreadyCompleted ? "Order was already completed; provisioning is unchanged." : "Payment successful",
            status = result.Status,
            idempotent = result.AlreadyCompleted
        });
    }
}

public sealed class PaymentResultDto
{
    public int OrderId { get; set; }
    public bool Success { get; set; }
}

using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EduMy.Backend.DTOs;
using EduMy.Backend.Services;
using System.Security.Cryptography;

namespace EduMy.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CouponsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICouponService _couponService;

        public CouponsController(ApplicationDbContext context, ICouponService couponService)
        {
            _context = context;
            _couponService = couponService;
        }

        [HttpPost("validate")]
        public async Task<IActionResult> ValidateCoupon([FromBody] ValidateCouponRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                return BadRequest("Coupon code is required");

            var coupon = await _context.Coupons
                .FirstOrDefaultAsync(c => c.Code.ToUpper() == request.Code.ToUpper());

            if (coupon == null)
                return NotFound(new { message = "Invalid coupon code" });

            if (!coupon.IsActive)
                return BadRequest(new { message = "This coupon is no longer active" });

            if (coupon.ExpiryDate < DateTime.UtcNow)
                return BadRequest(new { message = "This coupon has expired" });

            var normalized = _couponService.Normalize(coupon);
            return Ok(new
            {
                id = coupon.Id,
                code = coupon.Code,
                discountType = normalized.Type,
                discountValue = normalized.Value,
                discountPercentage = normalized.Type == "Percentage" ? normalized.Value : 0,
                message = "Coupon applied successfully!"
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet("/api/admin/coupons")]
        public async Task<IActionResult> AdminList([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
            var query = _context.Coupons.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(search)) query = query.Where(c => c.Code.Contains(search.Trim()));
            var total = await query.CountAsync();
            var coupons = await query.OrderByDescending(c => c.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
            var items = coupons.Select(c =>
            {
                var n = _couponService.Normalize(c);
                return (object)new { c.Id, c.Code, discountType = n.Type, discountValue = n.Value, c.ExpiryDate, c.IsActive };
            }).ToList();
            return Ok(new PagedResponseDto<object>(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize)));
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("/api/admin/coupons")]
        public async Task<IActionResult> AdminCreate(CouponUpsertDto dto)
        {
            var error = Validate(dto); if (error != null) return BadRequest(new { message = error });
            var code = dto.Code.Trim().ToUpperInvariant();
            if (await _context.Coupons.AnyAsync(c => c.Code.ToUpper() == code)) return Conflict(new { message = "Coupon code already exists." });
            var coupon = new Coupon
            {
                Code = code, DiscountType = dto.DiscountType, DiscountValue = dto.DiscountValue,
                DiscountPercentage = dto.DiscountType == "Percentage" ? dto.DiscountValue : 0,
                ExpiryDate = dto.ExpiryDate.ToUniversalTime(), IsActive = dto.IsActive
            };
            _context.Coupons.Add(coupon); await _context.SaveChangesAsync();
            return Ok(new { coupon.Id });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("/api/admin/coupons/{id:int}")]
        public async Task<IActionResult> AdminUpdate(int id, CouponUpsertDto dto)
        {
            var error = Validate(dto); if (error != null) return BadRequest(new { message = error });
            var coupon = await _context.Coupons.FindAsync(id); if (coupon == null) return NotFound();
            var code = dto.Code.Trim().ToUpperInvariant();
            if (await _context.Coupons.AnyAsync(c => c.Id != id && c.Code.ToUpper() == code)) return Conflict(new { message = "Coupon code already exists." });
            coupon.Code = code; coupon.DiscountType = dto.DiscountType; coupon.DiscountValue = dto.DiscountValue;
            coupon.DiscountPercentage = dto.DiscountType == "Percentage" ? dto.DiscountValue : 0;
            coupon.ExpiryDate = dto.ExpiryDate.ToUniversalTime(); coupon.IsActive = dto.IsActive;
            await _context.SaveChangesAsync(); return Ok(new { message = "Coupon updated." });
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("/api/admin/coupons/{id:int}/status")]
        public async Task<IActionResult> AdminStatus(int id)
        {
            var coupon = await _context.Coupons.FindAsync(id); if (coupon == null) return NotFound();
            coupon.IsActive = !coupon.IsActive; await _context.SaveChangesAsync();
            return Ok(new { coupon.IsActive });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("/api/admin/coupons/generate-code")]
        public async Task<IActionResult> GenerateCode()
        {
            for (var i = 0; i < 10; i++)
            {
                var bytes = RandomNumberGenerator.GetBytes(6);
                var raw = Convert.ToHexString(bytes);
                var code = $"EDUMY-{raw[..4]}-{raw[4..8]}";
                if (!await _context.Coupons.AnyAsync(c => c.Code == code)) return Ok(new { code });
            }
            return StatusCode(503, new { message = "Could not generate a unique code." });
        }

        private static string? Validate(CouponUpsertDto dto)
        {
            dto.Code = (dto.Code ?? string.Empty).Trim();
            dto.DiscountType = (dto.DiscountType ?? string.Empty).Trim();
            if (dto.Code.Length < 3) return "Code is required.";
            if (dto.DiscountType is not ("Percentage" or "FixedAmount")) return "Invalid discount type.";
            if (dto.DiscountValue <= 0 || (dto.DiscountType == "Percentage" && dto.DiscountValue > 100)) return "Invalid discount value.";
            if (dto.ExpiryDate == default || dto.ExpiryDate.ToUniversalTime() <= DateTime.UtcNow) return "Expiry date must be in the future.";
            return null;
        }
    }

    public class ValidateCouponRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}

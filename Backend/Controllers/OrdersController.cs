using EduMy.Backend.Data;
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
    [Authorize(Roles = "Student")]
    public class OrdersController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ICouponService _couponService;

        public OrdersController(ApplicationDbContext context, ICouponService couponService)
        {
            _context = context;
            _couponService = couponService;
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            // 1. Get user's cart
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Course)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null || !cart.CartItems.Any())
            {
                return BadRequest("Your cart is empty.");
            }
            if (cart.CartItems.Any(item => item.Course.IsDeleted || item.Course.Status != "Published"))
                return Conflict(new { code = "COURSE_NOT_AVAILABLE", message = "Giỏ hàng có khóa học không còn được bán. Vui lòng tải lại giỏ hàng." });

            // 2. Calculate total amount
            decimal originalAmount = cart.CartItems.Sum(ci => ci.Course.Price);
            decimal totalAmount = originalAmount;
            decimal discountAmount = 0;
            int? couponId = null;

            // 3. Apply Coupon if any
            if (!string.IsNullOrWhiteSpace(request?.CouponCode))
            {
                var coupon = await _context.Coupons
                    .FirstOrDefaultAsync(c => c.Code.ToUpper() == request.CouponCode.ToUpper() && c.IsActive && c.ExpiryDate > DateTime.UtcNow);
                
                if (coupon != null)
                {
                    var normalized = _couponService.Normalize(coupon);
                    discountAmount = _couponService.CalculateDiscount(originalAmount, normalized.Type, normalized.Value);
                    totalAmount = _couponService.CalculateFinalPrice(originalAmount, normalized.Type, normalized.Value);
                    couponId = coupon.Id;
                }
                else return BadRequest(new { message = "Coupon is invalid, inactive or expired." });
            }

            // 4. Create Order in Pending state
            var order = new Order
            {
                UserId = userId,
                OriginalAmount = originalAmount,
                DiscountAmount = discountAmount,
                CouponId = couponId,
                TotalAmount = totalAmount,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            foreach (var item in cart.CartItems)
            {
                var soldPrice = originalAmount > 0
                    ? decimal.Round(item.Course.Price * totalAmount / originalAmount, 2)
                    : 0;
                order.OrderItems.Add(new OrderItem
                {
                    CourseId = item.CourseId,
                    Price = soldPrice
                });
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // 5. Return payment redirect URL (Mock)
            var paymentUrl = $"/payment?orderId={order.OrderId}&amount={totalAmount}";

            return Ok(new { 
                message = "Order created. Redirecting to payment gateway...", 
                orderId = order.OrderId,
                paymentUrl = paymentUrl
            });
        }

        [HttpGet("my-orders")]
        public async Task<IActionResult> GetMyOrders()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdStr, out int userId)) return Unauthorized();

            var orders = await _context.Orders
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Course)
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return Ok(orders);
        }
    }

    public class CheckoutRequest
    {
        public string? CouponCode { get; set; }
    }
}

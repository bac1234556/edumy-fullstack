using System;
using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class Order
    {
        public int OrderId { get; set; }
        public int UserId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal OriginalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public int? CouponId { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Completed, Failed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public Coupon? Coupon { get; set; }
    }
}

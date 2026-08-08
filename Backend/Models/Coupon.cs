using System;
using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class Coupon
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal DiscountPercentage { get; set; }
        public string DiscountType { get; set; } = "Percentage";
        public decimal DiscountValue { get; set; }
        public DateTime ExpiryDate { get; set; }
        public bool IsActive { get; set; } = true;
        
        public ICollection<CourseCoupon> CourseCoupons { get; set; } = new List<CourseCoupon>();
    }
}

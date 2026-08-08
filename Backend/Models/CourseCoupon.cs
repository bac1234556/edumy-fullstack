using System;

namespace EduMy.Backend.Models
{
    public class CourseCoupon
    {
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;
        
        public int CouponId { get; set; }
        public Coupon Coupon { get; set; } = null!;
    }
}

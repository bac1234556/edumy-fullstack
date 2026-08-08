using EduMy.Backend.Models;

namespace EduMy.Backend.Services;

public interface ICouponService
{
    decimal CalculateDiscount(decimal originalPrice, string discountType, decimal discountValue);
    decimal CalculateFinalPrice(decimal originalPrice, string discountType, decimal discountValue);
    (string Type, decimal Value) Normalize(Coupon coupon);
}

public sealed class CouponService : ICouponService
{
    public decimal CalculateDiscount(decimal originalPrice, string discountType, decimal discountValue)
    {
        if (originalPrice <= 0 || discountValue <= 0) return 0;
        var discount = discountType.Equals("FixedAmount", StringComparison.OrdinalIgnoreCase)
            ? discountValue
            : originalPrice * Math.Min(discountValue, 100m) / 100m;
        return Math.Min(originalPrice, Math.Max(0, decimal.Round(discount, 2)));
    }

    public decimal CalculateFinalPrice(decimal originalPrice, string discountType, decimal discountValue) =>
        Math.Max(0, originalPrice - CalculateDiscount(originalPrice, discountType, discountValue));

    public (string Type, decimal Value) Normalize(Coupon coupon)
    {
        var type = string.IsNullOrWhiteSpace(coupon.DiscountType) ? "Percentage" : coupon.DiscountType;
        var value = coupon.DiscountValue > 0 ? coupon.DiscountValue : coupon.DiscountPercentage;
        return (type, value);
    }
}

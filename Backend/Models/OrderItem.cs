namespace EduMy.Backend.Models
{
    public class OrderItem
    {
        public int OrderItemId { get; set; }
        public int OrderId { get; set; }
        public int CourseId { get; set; }
        public decimal Price { get; set; }

        public Order? Order { get; set; }
        public Course? Course { get; set; }
    }
}

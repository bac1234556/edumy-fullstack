namespace EduMy.Backend.Models
{
    public class CourseCategory
    {
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        public int CategoryId { get; set; }
        public Category Category { get; set; } = null!;
    }
}

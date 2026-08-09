namespace EduMy.Backend.Models
{
    public class CourseTopic
    {
        public int CourseId { get; set; }
        public Course Course { get; set; } = null!;

        public int TopicId { get; set; }
        public Topic Topic { get; set; } = null!;
    }
}

using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        
        public ICollection<CourseTag> CourseTags { get; set; } = new List<CourseTag>();
    }
}

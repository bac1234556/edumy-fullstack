using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.Models
{
    public class Topic
    {
        public int TopicId { get; set; }

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        public ICollection<CourseTopic> CourseTopics { get; set; } = new List<CourseTopic>();
    }
}

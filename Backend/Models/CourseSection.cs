using System.Collections.Generic;

namespace EduMy.Backend.Models
{
    public class CourseSection
    {
        public int SectionId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int OrderIndex { get; set; }

        public Course Course { get; set; } = null!;
        
        public ICollection<Lesson> Lessons { get; set; } = new List<Lesson>();
        public ICollection<Quiz> Quizzes { get; set; } = new List<Quiz>();
    }
}

namespace EduMy.Backend.Models
{
    public class Lesson
    {
        public int LessonId { get; set; }
        public int SectionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? VideoUrl { get; set; }
        public int Duration { get; set; } // in seconds
        public int OrderIndex { get; set; }
        public bool IsPreview { get; set; } = false;
        public string? FileUrl { get; set; }
        public string? OriginalFileName { get; set; }
        public string? ContentType { get; set; }
        public string ResourceType { get; set; } = "Video";
        public long? FileSizeBytes { get; set; }
        public DateTime? UploadedAt { get; set; }
        public bool IsDraft { get; set; }

        public CourseSection? Section { get; set; }
    }
}

using System;

namespace EduMy.Backend.Models
{
    public class SearchHistory
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }
        public string SearchQuery { get; set; } = string.Empty;
        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    }
}

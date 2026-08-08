using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.DTOs
{
    public class PagedResultDto<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => PageNumber < TotalPages;
    }

    public class CourseQueryDto
    {
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public string? SortBy { get; set; } // price, rating, newest
        public string? SortOrder { get; set; } // asc, desc
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 12;
    }

    public class CourseUpsertDto
    {
        [Required, StringLength(200, MinimumLength = 3)]
        public string Title { get; set; } = string.Empty;
        [Required, StringLength(5000, MinimumLength = 10)]
        public string? Description { get; set; }
        [Range(typeof(decimal), "0", "999999999999")]
        public decimal Price { get; set; }
        
        public int CategoryId { get; set; }
        
        public List<int> CategoryIds { get; set; } = new List<int>();
        
        [StringLength(500)]
        public string? ThumbnailUrl { get; set; }
    }

    public class CreateCourseDto : CourseUpsertDto { }

    public class UpdateCourseDto : CourseUpsertDto
    {
        [Required]
        public string Status { get; set; } = "Draft";
    }
}

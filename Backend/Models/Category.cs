using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.Models
{
    public class Category
    {
        public int CategoryId { get; set; }
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(160)]
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        
        public int? ParentCategoryId { get; set; }
        public Category? ParentCategory { get; set; }
        public ICollection<Category> SubCategories { get; set; } = new List<Category>();
        
        public ICollection<CourseCategory> CourseCategories { get; set; } = new List<CourseCategory>();
    }
}

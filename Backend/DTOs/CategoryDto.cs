using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.DTOs;

public sealed record CategoryDto(
    int CategoryId,
    string Name,
    string Slug,
    string? Description,
    int PublishedCourseCount,
    IReadOnlyList<CategoryDto> SubCategories);

public sealed class CategoryUpsertDto
{
    [Required, StringLength(120, MinimumLength = 2)] public string Name { get; set; } = string.Empty;
    [StringLength(160)] public string? Slug { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    public int? ParentCategoryId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class AccountDeleteDto
{
    [Required] public string Confirmation { get; set; } = string.Empty;
}

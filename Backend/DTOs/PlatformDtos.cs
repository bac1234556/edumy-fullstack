using System.ComponentModel.DataAnnotations;

namespace EduMy.Backend.DTOs;

public record PagedResponseDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public sealed class SectionUpsertDto
{
    [Required, StringLength(200, MinimumLength = 1)] public string Title { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int OrderIndex { get; set; } = 1;
}

public sealed class LessonUpsertDto
{
    [Required, StringLength(250, MinimumLength = 1)] public string Title { get; set; } = string.Empty;
    [Range(0, int.MaxValue)] public int Duration { get; set; }
    [Range(1, int.MaxValue)] public int OrderIndex { get; set; } = 1;
    public bool IsPreview { get; set; }
    [StringLength(40)] public string ResourceType { get; set; } = "Video";
    [StringLength(1000)] public string? FileUrl { get; set; }
    [StringLength(255)] public string? OriginalFileName { get; set; }
    [StringLength(150)] public string? ContentType { get; set; }
    [Range(0, long.MaxValue)] public long? FileSizeBytes { get; set; }
    public bool IsDraft { get; set; }
}

public sealed class DiscussionThreadCreateDto
{
    [Required, StringLength(200, MinimumLength = 5)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(4000, MinimumLength = 10)] public string Content { get; set; } = string.Empty;
}

public sealed class DiscussionMessageCreateDto
{
    [Required, StringLength(4000, MinimumLength = 2)] public string Content { get; set; } = string.Empty;
}

public sealed class DiscussionStatusDto { public bool IsClosed { get; set; } }

public sealed class ReviewReplyCreateDto
{
    [Required, StringLength(2000, MinimumLength = 1)] public string Content { get; set; } = string.Empty;
}

public sealed class CourseCommentCreateDto
{
    [Required, StringLength(2000, MinimumLength = 1)] public string Content { get; set; } = string.Empty;
}

public sealed class CouponUpsertDto
{
    [Required, StringLength(50, MinimumLength = 3)] public string Code { get; set; } = string.Empty;
    [Required] public string DiscountType { get; set; } = "Percentage";
    [Range(typeof(decimal), "0.01", "999999999999")] public decimal DiscountValue { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using EduMy.Backend.Data;
using EduMy.Backend.DTOs;
using EduMy.Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public CategoriesController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetCategories()
    {
        var categories = await _context.Categories.AsNoTracking()
            .Where(c => c.IsActive && c.Name != "Uncategorized")
            .OrderBy(c => c.Name).ToListAsync();
        var counts = await _context.CourseCategories.AsNoTracking()
            .Where(cc => !cc.Course.IsDeleted && cc.Course.Status == "Published")
            .GroupBy(cc => cc.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count);
        CategoryDto Map(Category category) => new(category.CategoryId, category.Name, category.Slug,
            category.Description, counts.GetValueOrDefault(category.CategoryId),
            categories.Where(child => child.ParentCategoryId == category.CategoryId).Select(Map).ToList());
        return Ok(categories.Where(c => c.ParentCategoryId == null).Select(Map).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CategoryDto>> GetCategory(int id)
    {
        var all = await GetCategories();
        if (all.Result is not OkObjectResult ok || ok.Value is not IReadOnlyList<CategoryDto> values) return NotFound();
        var found = values.SelectMany(Flatten).FirstOrDefault(c => c.CategoryId == id);
        return found == null ? NotFound() : Ok(found);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> CreateCategory(CategoryUpsertDto dto)
    {
        var name = dto.Name.Trim();
        var slug = Slug(dto.Slug ?? name);
        if (await _context.Categories.AnyAsync(c => c.Name == name || c.Slug == slug))
            return Conflict(new { code = "DUPLICATE_CATEGORY", message = "Tên hoặc slug danh mục đã tồn tại." });
        var category = new Category { Name = name, Slug = slug, Description = dto.Description?.Trim(), ParentCategoryId = dto.ParentCategoryId, IsActive = dto.IsActive };
        _context.Categories.Add(category); await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCategory), new { id = category.CategoryId }, new { category.CategoryId });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCategory(int id, CategoryUpsertDto dto)
    {
        var category = await _context.Categories.FindAsync(id); if (category == null) return NotFound();
        var name = dto.Name.Trim(); var slug = Slug(dto.Slug ?? name);
        if (await _context.Categories.AnyAsync(c => c.CategoryId != id && (c.Name == name || c.Slug == slug)))
            return Conflict(new { code = "DUPLICATE_CATEGORY", message = "Tên hoặc slug danh mục đã tồn tại." });
        category.Name = name; category.Slug = slug; category.Description = dto.Description?.Trim();
        category.ParentCategoryId = dto.ParentCategoryId; category.IsActive = dto.IsActive;
        await _context.SaveChangesAsync(); return NoContent();
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories.FindAsync(id); if (category == null) return NotFound();
        if (await _context.CourseCategories.AnyAsync(cc => cc.CategoryId == id))
            return Conflict(new { code = "CATEGORY_IN_USE", message = "Danh mục đang được khóa học sử dụng và không thể xóa." });
        _context.Categories.Remove(category); await _context.SaveChangesAsync(); return NoContent();
    }

    private static IEnumerable<CategoryDto> Flatten(CategoryDto item) => new[] { item }.Concat(item.SubCategories.SelectMany(Flatten));
    private static string Slug(string value)
    {
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var chars = normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray();
        return Regex.Replace(new string(chars).Replace('đ', 'd'), @"[^a-z0-9]+", "-").Trim('-');
    }
}

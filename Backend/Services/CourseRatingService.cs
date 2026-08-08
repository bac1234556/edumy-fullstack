using EduMy.Backend.Data;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Services;

public interface ICourseRatingService { Task RecalculateCourseRatingAsync(int courseId); }

public sealed class CourseRatingService : ICourseRatingService
{
    private readonly ApplicationDbContext _db;
    public CourseRatingService(ApplicationDbContext db) => _db = db;
    public async Task RecalculateCourseRatingAsync(int courseId)
    {
        var course = await _db.Courses.FindAsync(courseId);
        if (course == null) return;
        var ratings = _db.Reviews.Where(r => r.CourseId == courseId);
        course.ReviewCount = await ratings.CountAsync();
        course.AverageRating = Math.Round(await ratings.Select(r => (double?)r.Rating).AverageAsync() ?? 0, 1, MidpointRounding.AwayFromZero);
        await _db.SaveChangesAsync();
    }
}

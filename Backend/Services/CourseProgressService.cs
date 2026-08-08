using System.Data;
using EduMy.Backend.Data;
using EduMy.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Services;

public sealed record CourseProgressResult(
    int CourseId,
    int EnrollmentId,
    IReadOnlyList<int> CompletedLessonIds,
    int TotalLessons,
    int CompletedLessons,
    int ProgressPercentage,
    DateTime? LastAccessedAt,
    DateTime? CompletedAt,
    string? CertificateUrl);

public interface ICourseProgressService
{
    Task<CourseProgressResult?> GetCourseProgressAsync(int userId, int courseId, bool touch = true);
    Task<CourseProgressResult?> SetLessonCompletionAsync(int userId, int courseId, int lessonId, bool isCompleted, int lastPositionSeconds = 0);
    Task RecalculateEnrollmentProgressAsync(int enrollmentId);
    Task RecalculateCourseEnrollmentsAsync(int courseId);
}

public sealed class CourseProgressService : ICourseProgressService
{
    private readonly ApplicationDbContext _db;
    public CourseProgressService(ApplicationDbContext db) => _db = db;

    public async Task<CourseProgressResult?> GetCourseProgressAsync(int userId, int courseId, bool touch = true)
    {
        var enrollment = await _db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);
        if (enrollment == null) return null;
        await Recalculate(enrollment);
        if (touch) enrollment.LastAccessedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return await BuildResult(enrollment);
    }

    public async Task<CourseProgressResult?> SetLessonCompletionAsync(int userId, int courseId, int lessonId, bool isCompleted, int lastPositionSeconds = 0)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var enrollment = await _db.Enrollments.FirstOrDefaultAsync(e => e.UserId == userId && e.CourseId == courseId);
        if (enrollment == null) return null;

        var validLesson = await _db.Lessons.AnyAsync(l => l.LessonId == lessonId && l.Section!.CourseId == courseId && !l.IsDraft);
        if (!validLesson) throw new InvalidOperationException("LESSON_NOT_LEARNABLE");

        var item = await _db.LessonProgresses
            .FirstOrDefaultAsync(p => p.EnrollmentId == enrollment.EnrollmentId && p.LessonId == lessonId);
        if (item == null)
        {
            item = new LessonProgress
            {
                EnrollmentId = enrollment.EnrollmentId,
                UserId = userId,
                CourseId = courseId,
                LessonId = lessonId
            };
            _db.LessonProgresses.Add(item);
        }
        item.IsCompleted = isCompleted;
        item.CompletedAt = isCompleted ? item.CompletedAt ?? DateTime.UtcNow : null;
        item.LastPositionSeconds = Math.Max(0, lastPositionSeconds);
        item.UpdatedAt = DateTime.UtcNow;
        enrollment.LastAccessedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await Recalculate(enrollment);
        await EnsureCertificate(enrollment);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();
        return await BuildResult(enrollment);
    }

    public async Task RecalculateEnrollmentProgressAsync(int enrollmentId)
    {
        var enrollment = await _db.Enrollments.FirstOrDefaultAsync(e => e.EnrollmentId == enrollmentId);
        if (enrollment == null) return;
        await Recalculate(enrollment);
        await _db.SaveChangesAsync();
    }

    public async Task RecalculateCourseEnrollmentsAsync(int courseId)
    {
        var enrollments = await _db.Enrollments.Where(e => e.CourseId == courseId).ToListAsync();
        foreach (var enrollment in enrollments) await Recalculate(enrollment);
        await _db.SaveChangesAsync();
    }

    private async Task Recalculate(Enrollment enrollment)
    {
        var total = await _db.Lessons.CountAsync(l => l.Section!.CourseId == enrollment.CourseId && !l.IsDraft);
        var completed = await _db.LessonProgresses.CountAsync(p =>
            p.EnrollmentId == enrollment.EnrollmentId && p.IsCompleted && !p.Lesson.IsDraft && p.Lesson.Section!.CourseId == enrollment.CourseId);
        completed = Math.Min(completed, total);
        var percentage = total == 0 ? 0 : (int)Math.Round(completed * 100.0 / total, MidpointRounding.AwayFromZero);
        enrollment.TotalLessons = total;
        enrollment.CompletedLessons = completed;
        enrollment.ProgressPercentage = percentage;
        enrollment.IsCompleted = total > 0 && completed == total;
        if (enrollment.IsCompleted) enrollment.CompletedAt ??= DateTime.UtcNow;
        else enrollment.CompletedAt = null;
    }

    private async Task EnsureCertificate(Enrollment enrollment)
    {
        if (!enrollment.IsCompleted) return;
        if (await _db.Certificates.AnyAsync(c => c.UserId == enrollment.UserId && c.CourseId == enrollment.CourseId)) return;
        _db.Certificates.Add(new Certificate
        {
            UserId = enrollment.UserId,
            CourseId = enrollment.CourseId,
            IssuedAt = DateTime.UtcNow,
            CertificateUrl = Guid.NewGuid().ToString("N")
        });
    }

    private async Task<CourseProgressResult> BuildResult(Enrollment enrollment)
    {
        var ids = await _db.LessonProgresses
            .Where(p => p.EnrollmentId == enrollment.EnrollmentId && p.IsCompleted && !p.Lesson.IsDraft)
            .Select(p => p.LessonId).OrderBy(id => id).ToListAsync();
        var certificate = await _db.Certificates.Where(c => c.UserId == enrollment.UserId && c.CourseId == enrollment.CourseId)
            .Select(c => c.CertificateUrl).FirstOrDefaultAsync();
        return new CourseProgressResult(enrollment.CourseId, enrollment.EnrollmentId, ids,
            enrollment.TotalLessons, enrollment.CompletedLessons, enrollment.ProgressPercentage,
            enrollment.LastAccessedAt, enrollment.CompletedAt, certificate);
    }
}

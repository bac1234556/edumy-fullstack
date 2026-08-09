using EduMy.Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace EduMy.Backend.Data;

public static class DataSeeder
{
    private static readonly string[] CategoryNames =
    [
        "Arts & Humanities",
        "Business & Management",
        "Computer Science & Development",
        "Data Science & AI",
        "Information Technology",
        "Health & Wellness",
        "Math & Logic",
        "Personal Development",
        "Engineering",
        "Social Sciences",
        "Language Learning"
    ];

    private static readonly string[] ActiveTopics =
    [
        "API Development", "AWS", "Big Data", "Business Analysis", "C/C++", "Cloud Computing",
        "Communication", "Computer Vision", "Cybersecurity", "Data Analysis", "Data Engineering",
        "Data Visualization", "Deep Learning", "DevOps", "Digital Marketing", "Docker", "Excel",
        "Finance & Accounting", "Frontend Development", "Generative AI & LLM", "Git & GitHub",
        "Google Cloud", "Java", "JavaScript & TypeScript", "Kubernetes", "Leadership", "Linux",
        "Machine Learning", "Marketing", "Microsoft Azure", "Mobile Development", "Natural Language Processing",
        "Networking", "Product Management", "Project Management", "Python", "SQL & Databases",
        "Software Testing", "Statistics", "TensorFlow & Keras", "UI/UX Design", "Web Development"
    ];

    public static void Initialize(IServiceProvider services)
    {
        using var db = new ApplicationDbContext(services.GetRequiredService<DbContextOptions<ApplicationDbContext>>());
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DataSeeder");
        db.Database.Migrate();
        EnsureRoles(db);
        var users = EnsureUsers(db);
        var categories = EnsureCategories(db);
        var topics = EnsureTopics(db);
        var courses = EnsureCourses(db, users.Instructors, categories);
        
        // Ensure PrimaryCategoryId is populated for all courses
        foreach (var c in db.Courses.Where(c => c.PrimaryCategoryId == null).ToList())
        {
            var cc = db.CourseCategories.FirstOrDefault(x => x.CourseId == c.CourseId);
            if (cc != null)
            {
                c.PrimaryCategoryId = cc.CategoryId;
            }
        }
        db.SaveChanges();

        EnsureCourseTopics(db, courses, topics);

        BackfillCourseThumbnails(db);
        EnsureCourseSectionsAndLessons(db, courses);
        EnsureEnrollments(db, users.Students, courses);
        EnsureWishlists(db, users.Students, courses);
        EnsureOrders(db, users.Students);
        EnsureReviews(db, users.Admin, users.Students);
        EnsureReviewReplies(db, users.Admin);
        EnsureCourseComments(db, users.Admin);
        EnsureLessonProgress(db);
        EnsureDiscussions(db, users.Students);
        EnsureCoupons(db);
        RecalculateAggregates(db);
        DemoLessonImageSeeder.EnsureDemoImagesAndBackfill(services);
        ValidateAndReport(db, logger);
    }

    private static void EnsureRoles(ApplicationDbContext db)
    {
        foreach (var name in new[] { "Admin", "Instructor", "Student" })
            if (!db.Roles.Any(r => r.Name == name)) db.Roles.Add(new Role { Name = name });
        db.SaveChanges();
    }

    private sealed record SeedUsers(User Admin, List<User> Instructors, List<User> Students);

    private static SeedUsers EnsureUsers(ApplicationDbContext db)
    {
        var admin = EnsureUser(db, "admin@edumy.com", "System Admin", "Admin", "123123", "Quản trị nền tảng Edumy");
        var instructors = new List<User>();
        var instructorNames = new[] { "John Doe", "Jane Miller", "Bob Smith", "An Nguyen", "Mai Tran", "David Chen", "Sarah Kim", "Alex Morgan", "Linh Pham", "Omar Hassan", "Sofia Rossi", "Minh Le" };
        for (var i = 0; i < 12; i++)
        {
            var email = i switch { 0 => "instructor@edumy.com", 1 => "instructor2@edumy.com", 2 => "instructor3@edumy.com", _ => $"instructor{i + 1:00}@edumy.com" };
            instructors.Add(EnsureUser(db, email, instructorNames[i], "Instructor", "123123", $"Edumy instructor specializing in {CategoryNames[i % CategoryNames.Length]}"));
        }

        var students = new List<User>();
        for (var i = 0; i < 40; i++)
        {
            var email = i switch
            {
                0 => "student@edumy.com",
                1 => "student2@edumy.com",
                2 => "hung@h.com",
                _ => $"seedstudent{i + 1:00}@edumy.com"
            };
            students.Add(EnsureUser(db, email, $"Edumy Student {i + 1:00}", "Student", "123123", "Learning with Edumy"));
        }

        foreach (var user in new[] { admin }.Concat(instructors).Concat(students))
        {
            var role = db.Roles.Single(r => r.Name == user.Role);
            if (!db.UserRoles.Any(x => x.UserId == user.UserId && x.RoleId == role.Id))
                db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.Id });
        }
        db.SaveChanges();

        ResetAllExistingUserPasswords(db);

        return new(admin, instructors, students);
    }

    private static List<Category> EnsureCategories(ApplicationDbContext db)
    {
        foreach (var name in CategoryNames.Append("Uncategorized"))
        {
            if (!db.Categories.Any(c => c.Name == name))
                db.Categories.Add(new Category { Name = name, Slug = Slug(name), IsActive = name != "Uncategorized", Description = $"Khóa học thuộc danh mục {name}." });
        }
        db.SaveChanges();
        foreach (var category in db.Categories.Where(c => c.Slug == "")) category.Slug = Slug(category.Name);
        var uncategorized = db.Categories.FirstOrDefault(c => c.Name == "Uncategorized");
        if (uncategorized != null) uncategorized.IsActive = false;
        db.SaveChanges();
        return db.Categories.OrderBy(c => c.CategoryId).ToList();
    }

    private static List<Course> EnsureCourses(ApplicationDbContext db, IReadOnlyList<User> instructors, IReadOnlyList<Category> categories)
    {
        var topicNames = new[] { "Foundations", "Applied Workshop", "Professional Toolkit", "Real-world Projects", "Advanced Practice" };
        var result = new List<Course>();
        for (var categoryIndex = 0; categoryIndex < CategoryNames.Length; categoryIndex++)
        {
            var category = categories.Single(c => c.Name == CategoryNames[categoryIndex]);
            for (var topicIndex = 0; topicIndex < topicNames.Length; topicIndex++)
            {
                var sequence = categoryIndex * topicNames.Length + topicIndex + 1;
                var title = $"{category.Name} {topicNames[topicIndex]}";
                var slug = $"seed-{Slug(category.Name)}-{topicIndex + 1}";
                var course = db.Courses.FirstOrDefault(c => c.Slug == slug || c.Title == title);
                if (course == null)
                {
                    course = new Course
                    {
                        InstructorId = instructors[categoryIndex % instructors.Count].UserId,
                        Title = title,
                        Slug = slug,
                        Description = $"Khóa học thực hành {title}, gồm ví dụ, bài tập và dự án có thể áp dụng ngay.",
                        Price = 199000m + sequence * 10000m,
                        ThumbnailUrl = ThumbnailFor(category.Name),
                        Level = topicIndex < 2 ? "Beginner" : topicIndex < 4 ? "Intermediate" : "Advanced",
                        Status = sequence % 7 == 0 ? "Draft" : "Published",
                        CreatedAt = DateTime.UtcNow.AddDays(-sequence * 2),
                        UpdatedAt = DateTime.UtcNow.AddDays(-sequence)
                    };
                    course.CourseCategories.Add(new CourseCategory { CategoryId = category.CategoryId });
                    db.Courses.Add(course);
                    db.SaveChanges();
                }
                else if (!db.CourseCategories.Any(cc => cc.CourseId == course.CourseId && cc.CategoryId == category.CategoryId))
                {
                    db.CourseCategories.Add(new CourseCategory { CourseId = course.CourseId, CategoryId = category.CategoryId });
                    db.SaveChanges();
                }
                result.Add(course);
            }
        }

        // Also cover categories created outside this seeder. The category id and
        // slot make these identities stable across restarts.
        foreach (var category in categories)
        {
            var publishedCount = db.CourseCategories.Count(cc => cc.CategoryId == category.CategoryId && cc.Course.Status == "Published");
            for (var slot = 1; publishedCount < 2; slot++)
            {
                var slug = $"seed-category-{category.CategoryId}-published-{slot}";
                var title = $"{category.Name} Essential Practice {slot}";
                var course = db.Courses.FirstOrDefault(c => c.Slug == slug || c.Title == title);
                if (course != null)
                {
                    if (!result.Any(c => c.CourseId == course.CourseId)) result.Add(course);
                    if (course.Status == "Published") publishedCount++;
                    continue;
                }
                if (course == null)
                {
                    course = new Course
                    {
                        InstructorId = instructors[(category.CategoryId + slot) % instructors.Count].UserId,
                        Title = title,
                        Slug = slug,
                        Description = $"Khóa học thực hành trọng tâm về {category.Name}, có ví dụ và bài tập theo tình huống thực tế.",
                        Price = 249000m + slot * 20000m,
                        ThumbnailUrl = ThumbnailFor(category.Name),
                        Level = slot == 1 ? "Beginner" : "Intermediate",
                        Status = "Published",
                        CreatedAt = DateTime.UtcNow.AddDays(-(category.CategoryId * 3 + slot)),
                        UpdatedAt = DateTime.UtcNow.AddDays(-slot)
                    };
                    course.CourseCategories.Add(new CourseCategory { CategoryId = category.CategoryId });
                    db.Courses.Add(course);
                    db.SaveChanges();
                }
                if (!result.Any(c => c.CourseId == course.CourseId)) result.Add(course);
                publishedCount++;
            }
        }

        const string emptyTitle = "Edumy Empty Curriculum State";
        var empty = db.Courses.FirstOrDefault(c => c.Title == emptyTitle);
        if (empty == null)
        {
            empty = new Course
            {
                InstructorId = instructors[0].UserId, Title = emptyTitle,
                Slug = "seed-empty-curriculum", Description = "Khóa học bản nháp dùng để kiểm thử trạng thái chưa có curriculum.",
                Price = 199000, ThumbnailUrl = ThumbnailFor(categories[0].Name), Status = "Draft", Level = "Beginner"
            };
            empty.CourseCategories.Add(new CourseCategory { CategoryId = categories[0].CategoryId });
            db.Courses.Add(empty);
            db.SaveChanges();
        }
        db.SaveChanges();
        return db.Courses.Where(c => c.Slug.StartsWith("seed-"))
            .OrderBy(c => c.CourseId).ToList();
    }

    private static void EnsureCourseSectionsAndLessons(ApplicationDbContext db, IReadOnlyList<Course> courses)
    {
        if (db.CourseSections.Any()) return;
        for (var courseIndex = 0; courseIndex < courses.Count; courseIndex++)
        {
            var course = courses[courseIndex];
            if (course.Slug == "seed-empty-curriculum") continue;
            var sectionCount = 2 + courseIndex % 5;
            for (var s = 1; s <= sectionCount; s++)
            {
                var title = $"Chương {s}: {SectionTitle(s)}";
                var section = db.CourseSections.FirstOrDefault(x => x.CourseId == course.CourseId && x.OrderIndex == s);
                if (section == null)
                {
                    section = new CourseSection { CourseId = course.CourseId, Title = title, OrderIndex = s };
                    db.CourseSections.Add(section);
                    db.SaveChanges();
                }
                var lessonCount = 2 + (courseIndex + s) % 7;
                for (var l = 1; l <= lessonCount; l++)
                {
                    if (db.Lessons.Any(x => x.SectionId == section.SectionId && x.OrderIndex == l)) continue;
                    var isDraft = courseIndex % 9 == 0 && s == sectionCount && l == lessonCount;
                    db.Lessons.Add(new Lesson
                    {
                        SectionId = section.SectionId,
                        Title = $"Bài {s}.{l}: {LessonTitle(s, l, course.Title)}",
                        Duration = 420 + (l * 90), OrderIndex = l, IsPreview = s == 1 && l == 1,
                        ResourceType = isDraft ? "Video" : "Reading", FileUrl = null,
                        VideoUrl = null, OriginalFileName = null,
                        ContentType = null, FileSizeBytes = null,
                        UploadedAt = isDraft ? null : DateTime.UtcNow.AddDays(-courseIndex), IsDraft = isDraft
                    });
                }
                db.SaveChanges();
            }

            // Previous seed versions pointed to a physical sample that was not
            // shipped. Convert those placeholders to safe reading lessons.
            var placeholders = db.Lessons.Where(l => l.Section!.CourseId == course.CourseId &&
                (l.FileUrl == "/uploads/sample.mp4" || l.VideoUrl == "/uploads/sample.mp4")).ToList();
            foreach (var lesson in placeholders)
            {
                lesson.FileUrl = null;
                lesson.VideoUrl = null;
                lesson.OriginalFileName = null;
                lesson.ContentType = null;
                lesson.FileSizeBytes = null;
                lesson.ResourceType = lesson.IsDraft ? "Video" : "Reading";
            }
            db.SaveChanges();
        }
    }

    private static void EnsureEnrollments(ApplicationDbContext db, IReadOnlyList<User> students, IReadOnlyList<Course> courses)
    {
        if (db.Enrollments.Any()) return;
        var published = courses.Where(c => c.Status == "Published" && c.Slug != "seed-empty-curriculum").ToList();
        foreach (var course in published)
        {
            foreach (var student in students.Take(3))
            {
                if (!db.Enrollments.Any(e => e.UserId == student.UserId && e.CourseId == course.CourseId))
                    db.Enrollments.Add(new Enrollment
                    {
                        UserId = student.UserId,
                        CourseId = course.CourseId,
                        EnrolledAt = DateTime.UtcNow.AddDays(-(course.CourseId % 40 + student.UserId % 7 + 1))
                    });
            }
        }
        db.SaveChanges();
        for (var studentIndex = 0; studentIndex < students.Count; studentIndex++)
        {
            for (var offset = 0; offset < 6; offset++)
            {
                var course = published[(studentIndex * 3 + offset * 7) % published.Count];
                if (!db.Enrollments.Any(e => e.UserId == students[studentIndex].UserId && e.CourseId == course.CourseId))
                    db.Enrollments.Add(new Enrollment { UserId = students[studentIndex].UserId, CourseId = course.CourseId, EnrolledAt = DateTime.UtcNow.AddDays(-(studentIndex + offset + 2)) });
            }
        }
        db.SaveChanges();
    }

    private static void EnsureWishlists(ApplicationDbContext db, IReadOnlyList<User> students, IReadOnlyList<Course> courses)
    {
        var published = courses.Where(c => c.Status == "Published").ToList();
        foreach (var student in students)
        {
            var excluded = db.Enrollments.Where(e => e.UserId == student.UserId).Select(e => e.CourseId).ToHashSet();
            foreach (var course in published.Where(c => !excluded.Contains(c.CourseId)).Take(3))
                if (!db.Wishlists.Any(w => w.UserId == student.UserId && w.CourseId == course.CourseId))
                    db.Wishlists.Add(new Wishlist { UserId = student.UserId, CourseId = course.CourseId, AddedAt = DateTime.UtcNow.AddDays(-student.UserId % 20) });
        }
        db.SaveChanges();
    }

    private static void EnsureOrders(ApplicationDbContext db, IReadOnlyList<User> students)
    {
        if (db.Orders.Any()) return;
        foreach (var student in students)
        {
            var enrolledIds = db.Enrollments.Where(e => e.UserId == student.UserId).Select(e => e.CourseId).ToList();
            var purchasedIds = db.OrderItems.Where(i => i.Order!.UserId == student.UserId && (i.Order.Status == "Completed" || i.Order.Status == "Paid")).Select(i => i.CourseId).ToHashSet();
            foreach (var chunk in enrolledIds.Where(id => !purchasedIds.Contains(id)).Chunk(2))
            {
                var prices = db.Courses.Where(c => chunk.Contains(c.CourseId)).ToDictionary(c => c.CourseId, c => c.Price);
                var order = new Order
                {
                    UserId = student.UserId, OriginalAmount = prices.Values.Sum(), TotalAmount = prices.Values.Sum(),
                    Status = "Completed", CreatedAt = DateTime.UtcNow.AddDays(-(student.UserId % 30 + chunk[0] % 12))
                };
                foreach (var id in chunk) order.OrderItems.Add(new OrderItem { CourseId = id, Price = prices[id] });
                db.Orders.Add(order);
            }
        }
        db.SaveChanges();
    }

    private static void EnsureReviews(ApplicationDbContext db, User admin, IReadOnlyList<User> students)
    {
        if (db.Reviews.Any()) return;
        var comments = new[]
        {
            "Nội dung rõ ràng và các ví dụ rất sát với công việc thực tế.",
            "Bài tập có độ khó hợp lý, phần giải thích giúp tôi hiểu sâu hơn.",
            "Khóa học có cấu trúc tốt nhưng một vài bài có thể đi nhanh hơn.",
            "Tài liệu hữu ích, tôi đã áp dụng được ngay vào dự án cá nhân.",
            "Giảng viên phản hồi chi tiết và curriculum được sắp xếp dễ theo dõi.",
            "Phần nền tảng ổn, tôi mong có thêm một dự án nâng cao ở cuối khóa.",
            "Video súc tích, chất lượng nội dung tốt và không có phần thừa.",
            "Một số khái niệm khá khó nhưng ví dụ minh họa đã giúp ích nhiều."
        };
        foreach (var student in students)
        {
            var enrollments = db.Enrollments.Where(e => e.UserId == student.UserId).OrderBy(e => e.CourseId).ToList();
            foreach (var enrollment in enrollments)
            {
                if (db.Reviews.Any(r => r.UserId == student.UserId && r.CourseId == enrollment.CourseId)) continue;
                var rating = 1 + (student.UserId + enrollment.CourseId) % 5;
                db.Reviews.Add(new Review
                {
                    UserId = student.UserId, CourseId = enrollment.CourseId, Rating = rating,
                    Comment = comments[(student.UserId + enrollment.CourseId) % comments.Length],
                    SentimentLabel = rating >= 4 ? "Positive" : rating <= 2 ? "Negative" : "Neutral",
                    SentimentScore = rating / 5.0, CreatedAt = DateTime.UtcNow.AddDays(-((student.UserId * 3 + enrollment.CourseId) % 120))
                });
            }
        }
        db.SaveChanges();
    }

    private static void EnsureReviewReplies(ApplicationDbContext db, User admin)
    {
        var reviews = db.Reviews.Include(r => r.Course).OrderBy(r => r.ReviewId).ToList();
        foreach (var review in reviews)
        {
            if (db.ReviewReplies.Any(r => r.ReviewId == review.ReviewId)) continue;
            var authorId = review.ReviewId % 5 == 0 ? admin.UserId : review.Course!.InstructorId;
            db.ReviewReplies.Add(new ReviewReply
            {
                ReviewId = review.ReviewId, UserId = authorId,
                Content = review.Rating < 3 ? "Cảm ơn góp ý của bạn. Chúng tôi sẽ cập nhật bài học này." : "Cảm ơn bạn đã chia sẻ trải nghiệm học tập!",
                CreatedAt = review.CreatedAt.AddHours(12)
            });
        }
        db.SaveChanges();
    }

    private static void EnsureCourseComments(ApplicationDbContext db, User admin)
    {
        var comments = new[]
        {
            "Hãy hoàn thành bài giới thiệu trước khi bắt đầu phần thực hành.",
            "Bạn có thể đặt câu hỏi trong khu vực Hỏi đáp nếu gặp vướng mắc.",
            "Tài liệu bổ sung và checklist đã được cập nhật theo curriculum.",
            "Hãy chia sẻ kết quả bài tập để giảng viên và các học viên cùng góp ý."
        };
        var courses = db.Courses.Where(c => c.Status == "Published").OrderBy(c => c.CourseId).ToList();
        foreach (var course in courses)
        {
            var instructorContent = comments[course.CourseId % comments.Length];
            if (!db.CourseComments.Any(c => c.CourseId == course.CourseId && c.UserId == course.InstructorId && c.Content == instructorContent))
                db.CourseComments.Add(new CourseComment
                {
                    CourseId = course.CourseId,
                    UserId = course.InstructorId,
                    Content = instructorContent,
                    CreatedAt = DateTime.UtcNow.AddDays(-(course.CourseId % 25 + 1))
                });

            var adminContent = $"Nội dung khóa học “{course.Title}” đã được kiểm tra cho dữ liệu mẫu.";
            if (!db.CourseComments.Any(c => c.CourseId == course.CourseId && c.UserId == admin.UserId && c.Content == adminContent))
                db.CourseComments.Add(new CourseComment
                {
                    CourseId = course.CourseId,
                    UserId = admin.UserId,
                    Content = adminContent,
                    CreatedAt = DateTime.UtcNow.AddHours(-(course.CourseId % 72 + 2))
                });
        }
        db.SaveChanges();
    }

    private static void EnsureLessonProgress(ApplicationDbContext db)
    {
        if (db.LessonProgresses.Any()) return;
        var seededEnrollments = db.Enrollments.Where(e => e.User!.Email.StartsWith("seedstudent"))
            .OrderBy(e => e.EnrollmentId).ToList();
        foreach (var enrollment in seededEnrollments)
        {
            var lessons = db.Lessons.Where(l => l.Section!.CourseId == enrollment.CourseId && !l.IsDraft).OrderBy(l => l.Section!.OrderIndex).ThenBy(l => l.OrderIndex).ToList();
            var completionCount = lessons.Count == 0 ? 0 : enrollment.EnrollmentId % (lessons.Count + 1);
            foreach (var lesson in lessons.Take(completionCount))
            {
                if (db.LessonProgresses.Any(p => p.EnrollmentId == enrollment.EnrollmentId && p.LessonId == lesson.LessonId)) continue;
                db.LessonProgresses.Add(new LessonProgress
                {
                    EnrollmentId = enrollment.EnrollmentId, UserId = enrollment.UserId, CourseId = enrollment.CourseId,
                    LessonId = lesson.LessonId, IsCompleted = true, CompletedAt = DateTime.UtcNow.AddDays(-(lesson.LessonId % 20)), UpdatedAt = DateTime.UtcNow
                });
            }
        }
        db.SaveChanges();
    }

    private static void EnsureDiscussions(ApplicationDbContext db, IReadOnlyList<User> students)
    {
        if (db.CourseDiscussionThreads.Any()) return;
        var courses = db.Courses.Where(c => c.Status == "Published").OrderBy(c => c.CourseId).ToList();
        foreach (var course in courses)
        {
            var enrolledIds = db.Enrollments.Where(e => e.CourseId == course.CourseId).Select(e => e.UserId).ToHashSet();
            var participants = students.Where(s => enrolledIds.Contains(s.UserId)).Take(3).ToList();
            if (participants.Count == 0) continue;

            const string studentTitle = "Cần chuẩn bị gì trước khi bắt đầu?";
            if (!db.CourseDiscussionThreads.Any(t => t.CourseId == course.CourseId && t.Title == studentTitle))
            {
                var thread = new CourseDiscussionThread
                {
                    CourseId = course.CourseId,
                    CreatedByUserId = participants[0].UserId,
                    Title = studentTitle,
                    CreatedAt = DateTime.UtcNow.AddDays(-(course.CourseId % 30 + 4)),
                    UpdatedAt = DateTime.UtcNow.AddDays(-(course.CourseId % 7 + 1))
                };
                thread.Messages.Add(new CourseDiscussionMessage { UserId = participants[0].UserId, Content = $"Em nên chuẩn bị công cụ và kiến thức nào trước khi học {course.Title}?" });
                thread.Messages.Add(new CourseDiscussionMessage { UserId = course.InstructorId, Content = "Bạn hãy xem phần yêu cầu, hoàn thành bài giới thiệu và chuẩn bị môi trường theo hướng dẫn nhé.", IsInstructorMessage = true });
                if (participants.Count > 1)
                    thread.Messages.Add(new CourseDiscussionMessage { UserId = participants[1].UserId, Content = "Mình đã làm theo checklist ở bài đầu và có thể bắt đầu thuận lợi." });
                db.CourseDiscussionThreads.Add(thread);
            }

            const string instructorTitle = "Thảo luận bài tập thực hành cuối chương";
            if (!db.CourseDiscussionThreads.Any(t => t.CourseId == course.CourseId && t.Title == instructorTitle))
            {
                var thread = new CourseDiscussionThread
                {
                    CourseId = course.CourseId,
                    CreatedByUserId = course.InstructorId,
                    Title = instructorTitle,
                    IsClosed = course.CourseId % 4 == 0,
                    CreatedAt = DateTime.UtcNow.AddDays(-(course.CourseId % 20 + 2)),
                    UpdatedAt = DateTime.UtcNow.AddHours(-(course.CourseId % 48 + 1))
                };
                thread.Messages.Add(new CourseDiscussionMessage { UserId = course.InstructorId, Content = "Hãy chia sẻ cách bạn giải bài tập và phần nào còn chưa rõ.", IsInstructorMessage = true });
                if (course.CourseId % 3 != 0)
                    thread.Messages.Add(new CourseDiscussionMessage { UserId = participants[0].UserId, Content = "Em đã hoàn thành phần chính nhưng muốn hỏi thêm về cách tối ưu kết quả." });
                db.CourseDiscussionThreads.Add(thread);
            }
            db.SaveChanges();
        }
    }

    private static void EnsureCoupons(ApplicationDbContext db)
    {
        EnsureCoupon(db, "SUMMER20", "Percentage", 20, 90);
        EnsureCoupon(db, "EDUMY-START-2026", "FixedAmount", 100000, 180);
        db.SaveChanges();
    }

    private static void RecalculateAggregates(ApplicationDbContext db)
    {
        if (db.Enrollments.Any(e => e.TotalLessons > 0)) return; // Skip if already calculated
        foreach (var enrollment in db.Enrollments.ToList())
        {
            var total = db.Lessons.Count(l => l.Section!.CourseId == enrollment.CourseId && !l.IsDraft);
            var completed = db.LessonProgresses.Count(p => p.EnrollmentId == enrollment.EnrollmentId && p.IsCompleted && !p.Lesson.IsDraft);
            enrollment.TotalLessons = total;
            enrollment.CompletedLessons = Math.Min(completed, total);
            enrollment.ProgressPercentage = total == 0 ? 0 : (int)Math.Round(enrollment.CompletedLessons * 100.0 / total, MidpointRounding.AwayFromZero);
            enrollment.IsCompleted = total > 0 && enrollment.CompletedLessons == total;
            enrollment.CompletedAt = enrollment.IsCompleted ? enrollment.CompletedAt ?? DateTime.UtcNow : null;
        }
        foreach (var course in db.Courses.Include(c => c.Reviews).ToList())
        {
            course.AverageRating = course.Reviews.Count == 0 ? 0 : Math.Round(course.Reviews.Average(r => r.Rating), 1, MidpointRounding.AwayFromZero);
            course.ReviewCount = course.Reviews.Count;
            course.StudentCount = db.Enrollments.Count(e => e.CourseId == course.CourseId);
        }
        db.SaveChanges();
    }

    private static User EnsureUser(ApplicationDbContext db, string email, string name, string role, string password, string headline)
    {
        var user = db.Users.FirstOrDefault(u => u.Email == email);
        if (user != null) return user;
        user = new User { Email = email, FullName = name, Role = role, Headline = headline, IsActive = true, PasswordHash = BCrypt.Net.BCrypt.HashPassword(password) };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    private static void ResetAllExistingUserPasswords(ApplicationDbContext db)
    {
        var users = db.Users.ToList();
        var targetHash = BCrypt.Net.BCrypt.HashPassword("123123");
        var updatedCount = 0;
        foreach (var u in users)
        {
            var isMatch = !string.IsNullOrEmpty(u.PasswordHash) && Verify123123(u.PasswordHash);
            if (!isMatch)
            {
                u.PasswordHash = targetHash;
                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            db.SaveChanges();
            Console.WriteLine($"[PasswordReset] Successfully reset password to 123123 for {updatedCount} user account(s).");
        }
    }

    private static bool Verify123123(string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify("123123", hash);
        }
        catch
        {
            return false;
        }
    }

    private static void EnsureCoupon(ApplicationDbContext db, string code, string type, decimal value, int days)
    {
        if (!db.Coupons.Any(c => c.Code == code))
            db.Coupons.Add(new Coupon { Code = code, DiscountType = type, DiscountValue = value, DiscountPercentage = type == "Percentage" ? value : 0, ExpiryDate = DateTime.UtcNow.AddDays(days), IsActive = true });
    }

    private static string SectionTitle(int index) => index switch { 1 => "Khởi động", 2 => "Kiến thức cốt lõi", 3 => "Thực hành", 4 => "Dự án", 5 => "Tối ưu", _ => "Tổng kết" };
    private static string LessonTitle(int section, int lesson, string courseTitle) => (section, lesson) switch
    {
        (1, 1) => $"Giới thiệu và mục tiêu của {courseTitle}",
        (1, 2) => "Thiết lập môi trường và công cụ",
        (2, 1) => "Các khái niệm nền tảng",
        (2, 2) => "Phân tích ví dụ từng bước",
        (3, 1) => "Bài tập thực hành có hướng dẫn",
        (3, 2) => "Kiểm tra và cải thiện kết quả",
        _ => $"Chuyên đề {section}.{lesson} và tình huống thực tế"
    };

    private static void ValidateAndReport(ApplicationDbContext db, ILogger logger)
    {
        var distribution = db.Categories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new
            {
                c.Name,
                Published = c.CourseCategories.Count(cc => cc.Course.Status == "Published"),
                Draft = c.CourseCategories.Count(cc => cc.Course.Status == "Draft"),
                Total = c.CourseCategories.Count
            }).ToList();

        foreach (var item in distribution)
            logger.LogInformation("Seed category {Category}: Published={Published}, Draft={Draft}, Total={Total}", item.Name, item.Published, item.Draft, item.Total);

        var invalidCategories = distribution.Where(x => x.Published < 2).Select(x => x.Name).ToList();
        var invalidCourseCount = db.Courses.Count(c => !db.CourseCategories.Any(cc => cc.CourseId == c.CourseId));
        if (invalidCategories.Count > 0 || invalidCourseCount > 0)
            throw new InvalidOperationException($"Seed validation failed. Categories below two published courses: {string.Join(", ", invalidCategories)}; invalid course categories: {invalidCourseCount}.");

        logger.LogInformation(
            "Seed totals: Categories={Categories}, Courses={Courses}, Published={Published}, Draft={Draft}, Sections={Sections}, Lessons={Lessons}, Students={Students}, Instructors={Instructors}, Reviews={Reviews}, Ratings={Ratings}, CourseComments={Comments}, DiscussionThreads={Threads}, DiscussionMessages={Messages}",
            db.Categories.Count(), db.Courses.Count(), db.Courses.Count(c => c.Status == "Published"), db.Courses.Count(c => c.Status == "Draft"),
            db.CourseSections.Count(), db.Lessons.Count(), db.Users.Count(u => u.Role == "Student"), db.Users.Count(u => u.Role == "Instructor"),
            db.Reviews.Count(), db.Reviews.Count(r => r.Rating >= 1 && r.Rating <= 5), db.CourseComments.Count(), db.CourseDiscussionThreads.Count(), db.CourseDiscussionMessages.Count());
    }

    private static void BackfillCourseThumbnails(ApplicationDbContext db)
    {
        var courses = db.Courses.Include(c => c.CourseCategories).ThenInclude(cc => cc.Category)
            .Where(c => c.ThumbnailUrl == null || c.ThumbnailUrl == "" || c.ThumbnailUrl.Contains("picsum.photos") || c.ThumbnailUrl.Contains("via.placeholder.com"))
            .ToList();
        foreach (var course in courses)
        {
            var categoryName = course.CourseCategories.FirstOrDefault()?.Category?.Name;
            course.ThumbnailUrl = ThumbnailFor(categoryName);
        }
        if (courses.Count > 0)
        {
            db.SaveChanges();
            Console.WriteLine($"[ThumbnailBackfill] Updated {courses.Count} course thumbnail(s): {string.Join(",", courses.Select(c => c.CourseId))}");
        }
    }

    private static string ThumbnailFor(string? categoryName)
    {
        var slug = Slug(categoryName ?? "default");
        var asset = slug switch
        {
            "photography" => "photography",
            "business" => "business",
            "design" => "design",
            "marketing" => "marketing",
            "office-productivity" => "office",
            "machine-learning" or "data-science" => "data-science",
            "cloud-computing" => "cloud",
            "cyber-security" => "security",
            "it-software" => "it-software",
            "personal-development" => "personal-development",
            "development" or "web-development" or "mobile-development" => "development",
            _ => "default"
        };
        return $"/images/course-placeholders/{asset}.svg";
    }

    private static string Slug(string value) => System.Text.RegularExpressions.Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

    private static List<Topic> EnsureTopics(ApplicationDbContext db)
    {
        var list = new List<Topic>();
        foreach (var name in ActiveTopics)
        {
            var topic = db.Topics.FirstOrDefault(t => t.Name == name);
            if (topic == null)
            {
                topic = new Topic { Name = name };
                db.Topics.Add(topic);
            }
            list.Add(topic);
        }
        db.SaveChanges();
        return db.Topics.OrderBy(t => t.TopicId).ToList();
    }

    private static void EnsureCourseTopics(ApplicationDbContext db, List<Course> courses, List<Topic> topics)
    {
        foreach (var course in courses)
        {
            if (db.CourseTopics.Any(ct => ct.CourseId == course.CourseId)) continue;

            var titleLower = course.Title.ToLower();
            var matchedTopics = new List<Topic>();
            
            foreach (var topic in topics)
            {
                var topicNameLower = topic.Name.ToLower();
                if (topicNameLower.Contains(" & "))
                {
                    var parts = topicNameLower.Split(new[] { " & " }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Any(p => titleLower.Contains(p))) matchedTopics.Add(topic);
                }
                else if (topicNameLower.Contains("/"))
                {
                    var parts = topicNameLower.Split('/');
                    if (parts.Any(p => titleLower.Contains(p))) matchedTopics.Add(topic);
                }
                else
                {
                    if (titleLower.Contains(topicNameLower)) matchedTopics.Add(topic);
                }
            }

            // Fallback: assign 1-2 random topics if none matched by keyword
            if (matchedTopics.Count == 0)
            {
                var random = new Random(course.CourseId);
                var t1 = topics[random.Next(topics.Count)];
                var t2 = topics[random.Next(topics.Count)];
                matchedTopics.Add(t1);
                if (t1.TopicId != t2.TopicId) matchedTopics.Add(t2);
            }

            foreach (var topic in matchedTopics.Take(3))
            {
                db.CourseTopics.Add(new CourseTopic { CourseId = course.CourseId, TopicId = topic.TopicId });
            }
        }
        db.SaveChanges();
    }
}

using Microsoft.EntityFrameworkCore;
using EduMy.Backend.Models;

namespace EduMy.Backend.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Role> Roles { get; set; } = null!;
        public DbSet<UserRole> UserRoles { get; set; } = null!;
        public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Course> Courses { get; set; } = null!;
         public DbSet<CourseSection> CourseSections { get; set; } = null!;
        public DbSet<Lesson> Lessons { get; set; } = null!;
        public DbSet<CourseCategory> CourseCategories { get; set; } = null!;
        public DbSet<Enrollment> Enrollments { get; set; } = null!;
        public DbSet<Review> Reviews { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<OrderItem> OrderItems { get; set; } = null!;
        
        public DbSet<Cart> Carts { get; set; } = null!;
        public DbSet<CartItem> CartItems { get; set; } = null!;
        public DbSet<Wishlist> Wishlists { get; set; } = null!;
        public DbSet<Coupon> Coupons { get; set; } = null!;
        public DbSet<CourseCoupon> CourseCoupons { get; set; } = null!;
        public DbSet<Certificate> Certificates { get; set; } = null!;
        public DbSet<CourseMlAnalysis> CourseMlAnalyses { get; set; } = null!;
        public DbSet<CourseMlAnalysisTag> CourseMlAnalysisTags { get; set; } = null!;
        
        public DbSet<Tag> Tags { get; set; } = null!;
        public DbSet<CourseTag> CourseTags { get; set; } = null!;
        public DbSet<LessonProgress> LessonProgresses { get; set; } = null!;
        public DbSet<SearchHistory> SearchHistories { get; set; } = null!;
        public DbSet<UserActivity> UserActivities { get; set; } = null!;
        public DbSet<CourseDiscussionThread> CourseDiscussionThreads { get; set; } = null!;
        public DbSet<CourseDiscussionMessage> CourseDiscussionMessages { get; set; } = null!;
        public DbSet<ReviewReply> ReviewReplies { get; set; } = null!;
        public DbSet<CourseComment> CourseComments { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        
        // Phase 12: Quizzes
        public DbSet<Quiz> Quizzes { get; set; } = null!;
        public DbSet<Question> Questions { get; set; } = null!;
        public DbSet<Answer> Answers { get; set; } = null!;
        public DbSet<QuizAttempt> QuizAttempts { get; set; } = null!;
        public DbSet<QuizAttemptAnswer> QuizAttemptAnswers { get; set; } = null!;
        
        public DbSet<InstructorApplication> InstructorApplications { get; set; } = null!;
        public DbSet<CourseQuiz> CourseQuizzes { get; set; } = null!;
        public DbSet<CourseQuizQuestion> CourseQuizQuestions { get; set; } = null!;
        public DbSet<CourseQuizOption> CourseQuizOptions { get; set; } = null!;
        public DbSet<CourseQuizAttempt> CourseQuizAttempts { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<CourseSection>()
                .HasKey(cs => cs.SectionId);

            // Configure Users
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();
            modelBuilder.Entity<User>().HasIndex(u => u.IsDeleted);
            modelBuilder.Entity<User>().HasOne(u => u.DeletedByUser).WithMany()
                .HasForeignKey(u => u.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);

            // Configure UserRoles
            modelBuilder.Entity<UserRole>()
                .HasKey(ur => new { ur.UserId, ur.RoleId });
                
            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.User)
                .WithMany(u => u.UserRoles)
                .HasForeignKey(ur => ur.UserId);
                
            modelBuilder.Entity<UserRole>()
                .HasOne(ur => ur.Role)
                .WithMany(r => r.UserRoles)
                .HasForeignKey(ur => ur.RoleId);

            // Configure RefreshTokens
            modelBuilder.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Course relationships
            modelBuilder.Entity<Course>()
                .HasOne(c => c.Instructor)
                .WithMany(u => u.Courses)
                .HasForeignKey(c => c.InstructorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CourseCategory>()
                .HasKey(cc => new { cc.CourseId, cc.CategoryId });

            modelBuilder.Entity<CourseCategory>()
                .HasOne(cc => cc.Course)
                .WithMany(c => c.CourseCategories)
                .HasForeignKey(cc => cc.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseCategory>()
                .HasOne(cc => cc.Category)
                .WithMany(cat => cat.CourseCategories)
                .HasForeignKey(cc => cc.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Course>().HasIndex(c => c.IsDeleted);
            modelBuilder.Entity<Course>().HasOne(c => c.DeletedByUser).WithMany()
                .HasForeignKey(c => c.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Category>().HasIndex(c => c.Name).IsUnique();
            modelBuilder.Entity<Category>().HasIndex(c => c.Slug).IsUnique();

            // Configure Enrollment
            modelBuilder.Entity<Enrollment>()
                .HasIndex(e => new { e.UserId, e.CourseId })
                .IsUnique();
            
            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.User)
                .WithMany(u => u.Enrollments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Enrollment>()
                .HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Review
            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Course)
                .WithMany(c => c.Reviews)
                .HasForeignKey(r => r.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ReviewReply>()
                .HasOne(r => r.Review)
                .WithMany(r => r.Replies)
                .HasForeignKey(r => r.ReviewId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<ReviewReply>()
                .HasOne(r => r.User)
                .WithMany(u => u.ReviewReplies)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<ReviewReply>().HasIndex(r => r.ReviewId);

            modelBuilder.Entity<CourseComment>()
                .HasOne(c => c.Course)
                .WithMany(c => c.Comments)
                .HasForeignKey(c => c.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CourseComment>()
                .HasOne(c => c.User)
                .WithMany(u => u.CourseComments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Order and OrderItem
            modelBuilder.Entity<Order>()
                .HasOne(o => o.User)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<OrderItem>()
                .HasOne(oi => oi.Course)
                .WithMany(c => c.OrderItems)
                .HasForeignKey(oi => oi.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Order>()
                .HasOne(o => o.Coupon)
                .WithMany()
                .HasForeignKey(o => o.CouponId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // Configure Cart and CartItem
            modelBuilder.Entity<Cart>()
                .HasOne(c => c.User)
                .WithOne(u => u.Cart)
                .HasForeignKey<Cart>(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Cart)
                .WithMany(c => c.CartItems)
                .HasForeignKey(ci => ci.CartId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<CartItem>()
                .HasOne(ci => ci.Course)
                .WithMany()
                .HasForeignKey(ci => ci.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Configure Wishlist
            modelBuilder.Entity<Wishlist>()
                .HasOne(w => w.User)
                .WithMany(u => u.Wishlists)
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Wishlist>()
                .HasOne(w => w.Course)
                .WithMany(c => c.Wishlists)
                .HasForeignKey(w => w.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Wishlist>()
                .HasIndex(w => new { w.UserId, w.CourseId })
                .IsUnique();
                
            // Configure CourseCoupon
            modelBuilder.Entity<CourseCoupon>()
                .HasKey(cc => new { cc.CourseId, cc.CouponId });
                
            modelBuilder.Entity<CourseCoupon>()
                .HasOne(cc => cc.Course)
                .WithMany(c => c.CourseCoupons)
                .HasForeignKey(cc => cc.CourseId);
                
            modelBuilder.Entity<CourseCoupon>()
                .HasOne(cc => cc.Coupon)
                .WithMany(c => c.CourseCoupons)
                .HasForeignKey(cc => cc.CouponId);
                
            // Configure Certificate
            modelBuilder.Entity<Certificate>()
                .HasOne(cert => cert.User)
                .WithMany(u => u.Certificates)
                .HasForeignKey(cert => cert.UserId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<Certificate>()
                .HasOne(cert => cert.Course)
                .WithMany(c => c.Certificates)
                .HasForeignKey(cert => cert.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Configure CourseMlAnalysis and Tags
            modelBuilder.Entity<CourseMlAnalysis>()
                .HasOne(ma => ma.Course)
                .WithMany(c => c.MlAnalyses)
                .HasForeignKey(ma => ma.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<CourseMlAnalysis>()
                .HasOne(ma => ma.ApprovedByUser)
                .WithMany()
                .HasForeignKey(ma => ma.ApprovedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
                
            modelBuilder.Entity<CourseMlAnalysisTag>()
                .HasOne(mat => mat.CourseMlAnalysis)
                .WithMany(ma => ma.Tags)
                .HasForeignKey(mat => mat.CourseMlAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Configure Category self-referencing
            modelBuilder.Entity<Category>()
                .HasOne(c => c.ParentCategory)
                .WithMany(c => c.SubCategories)
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
                
            // Configure CourseTag
            modelBuilder.Entity<CourseTag>()
                .HasKey(ct => new { ct.CourseId, ct.TagId });
                
            modelBuilder.Entity<CourseTag>()
                .HasOne(ct => ct.Course)
                .WithMany(c => c.CourseTags)
                .HasForeignKey(ct => ct.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
                
            modelBuilder.Entity<CourseTag>()
                .HasOne(ct => ct.Tag)
                .WithMany(t => t.CourseTags)
                .HasForeignKey(ct => ct.TagId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<LessonProgress>()
                .HasIndex(lp => new { lp.EnrollmentId, lp.LessonId })
                .IsUnique();
            modelBuilder.Entity<LessonProgress>()
                .HasIndex(lp => new { lp.UserId, lp.LessonId })
                .IsUnique();

            modelBuilder.Entity<LessonProgress>()
                .HasOne(lp => lp.Enrollment)
                .WithMany(e => e.LessonProgresses)
                .HasForeignKey(lp => lp.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LessonProgress>()
                .HasOne(lp => lp.User)
                .WithMany()
                .HasForeignKey(lp => lp.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LessonProgress>()
                .HasOne(lp => lp.Course)
                .WithMany()
                .HasForeignKey(lp => lp.CourseId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<LessonProgress>()
                .HasOne(lp => lp.Lesson)
                .WithMany()
                .HasForeignKey(lp => lp.LessonId)
                .OnDelete(DeleteBehavior.Cascade);
                
            // Configure Quizzes (Phase 12)
            modelBuilder.Entity<Quiz>()
                .HasOne(q => q.CourseSection)
                .WithMany(cs => cs.Quizzes)
                .HasForeignKey(q => q.CourseSectionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Question>()
                .HasOne(q => q.Quiz)
                .WithMany(qz => qz.Questions)
                .HasForeignKey(q => q.QuizId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Answer>()
                .HasOne(a => a.Question)
                .WithMany(q => q.Answers)
                .HasForeignKey(a => a.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizAttempt>()
                .HasOne(qa => qa.User)
                .WithMany(u => u.QuizAttempts)
                .HasForeignKey(qa => qa.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizAttempt>()
                .HasOne(qa => qa.Quiz)
                .WithMany(qz => qz.QuizAttempts)
                .HasForeignKey(qa => qa.QuizId)
                .OnDelete(DeleteBehavior.Cascade); // Cascade is fine here or restrict if we want to keep attempts

            modelBuilder.Entity<QuizAttemptAnswer>()
                .HasOne(qaa => qaa.QuizAttempt)
                .WithMany(qa => qa.SelectedAnswers)
                .HasForeignKey(qaa => qaa.QuizAttemptId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<QuizAttemptAnswer>()
                .HasOne(qaa => qaa.Question)
                .WithMany()
                .HasForeignKey(qaa => qaa.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<QuizAttemptAnswer>()
                .HasOne(qaa => qaa.Answer)
                .WithMany()
                .HasForeignKey(qaa => qaa.AnswerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure SearchHistory
            modelBuilder.Entity<SearchHistory>()
                .HasOne(sh => sh.User)
                .WithMany()
                .HasForeignKey(sh => sh.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // Configure UserActivity
            modelBuilder.Entity<UserActivity>()
                .HasOne(ua => ua.User)
                .WithMany()
                .HasForeignKey(ua => ua.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseDiscussionThread>()
                .HasOne(t => t.Course)
                .WithMany(c => c.DiscussionThreads)
                .HasForeignKey(t => t.CourseId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CourseDiscussionThread>()
                .HasOne(t => t.CreatedByUser)
                .WithMany(u => u.DiscussionThreads)
                .HasForeignKey(t => t.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<CourseDiscussionThread>().HasIndex(t => t.CourseId);

            modelBuilder.Entity<CourseDiscussionMessage>()
                .HasOne(m => m.Thread)
                .WithMany(t => t.Messages)
                .HasForeignKey(m => m.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CourseDiscussionMessage>()
                .HasOne(m => m.User)
                .WithMany(u => u.DiscussionMessages)
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure Notifications
            modelBuilder.Entity<Notification>()
                .HasKey(n => n.NotificationId);
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.RecipientUser)
                .WithMany()
                .HasForeignKey(n => n.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.ActorUser)
                .WithMany()
                .HasForeignKey(n => n.ActorUserId)
                .OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.RecipientUserId, n.IsRead });
            modelBuilder.Entity<Notification>()
                .HasIndex(n => new { n.RecipientUserId, n.CreatedAt });

            // InstructorApplication configuration
            modelBuilder.Entity<InstructorApplication>()
                .HasKey(ia => ia.InstructorApplicationId);
            modelBuilder.Entity<InstructorApplication>()
                .HasOne(ia => ia.User)
                .WithMany()
                .HasForeignKey(ia => ia.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<InstructorApplication>()
                .HasOne(ia => ia.ReviewedByAdmin)
                .WithMany()
                .HasForeignKey(ia => ia.ReviewedByAdminId)
                .OnDelete(DeleteBehavior.Restrict);

            // CourseQuiz configuration
            modelBuilder.Entity<CourseQuiz>()
                .HasKey(q => q.CourseQuizId);
            modelBuilder.Entity<CourseQuiz>()
                .HasOne(q => q.Course)
                .WithMany()
                .HasForeignKey(q => q.CourseId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseQuizQuestion>()
                .HasKey(q => q.CourseQuizQuestionId);
            modelBuilder.Entity<CourseQuizQuestion>()
                .HasOne(q => q.CourseQuiz)
                .WithMany(qz => qz.Questions)
                .HasForeignKey(q => q.CourseQuizId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseQuizOption>()
                .HasKey(o => o.CourseQuizOptionId);
            modelBuilder.Entity<CourseQuizOption>()
                .HasOne(o => o.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(o => o.CourseQuizQuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CourseQuizAttempt>()
                .HasKey(a => a.CourseQuizAttemptId);
            modelBuilder.Entity<CourseQuizAttempt>()
                .HasOne(a => a.CourseQuiz)
                .WithMany(q => q.Attempts)
                .HasForeignKey(a => a.CourseQuizId)
                .OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<CourseQuizAttempt>()
                .HasOne(a => a.Student)
                .WithMany()
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}

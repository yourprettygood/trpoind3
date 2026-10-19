using EduCabinet.Web.Models.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EduCabinet.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<StudentGroup> StudentGroups => Set<StudentGroup>();
    public DbSet<GroupMembership> GroupMemberships => Set<GroupMembership>();
    public DbSet<Discipline> Disciplines => Set<Discipline>();
    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Lecture> Lectures => Set<Lecture>();
    public DbSet<ControlQuestion> ControlQuestions => Set<ControlQuestion>();
    public DbSet<StudentAnswer> StudentAnswers => Set<StudentAnswer>();
    public DbSet<Test> Tests => Set<Test>();
    public DbSet<TestQuestion> TestQuestions => Set<TestQuestion>();
    public DbSet<AnswerOption> AnswerOptions => Set<AnswerOption>();
    public DbSet<TestAttempt> TestAttempts => Set<TestAttempt>();
    public DbSet<StudentTestAnswer> StudentTestAnswers => Set<StudentTestAnswer>();
    public DbSet<LectureProgress> LectureProgresses => Set<LectureProgress>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Group Memberships
        builder.Entity<GroupMembership>()
            .HasOne(m => m.Student)
            .WithMany(u => u.GroupMemberships)
            .HasForeignKey(m => m.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<GroupMembership>()
            .HasOne(m => m.Group)
            .WithMany(g => g.Memberships)
            .HasForeignKey(m => m.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // Disciplines
        builder.Entity<Discipline>()
            .HasOne(d => d.Teacher)
            .WithMany(t => t.AuthoredDisciplines)
            .HasForeignKey(d => d.TeacherId)
            .OnDelete(DeleteBehavior.Restrict);

        // Topics
        builder.Entity<Topic>()
            .HasOne(t => t.Discipline)
            .WithMany(d => d.Topics)
            .HasForeignKey(t => t.DisciplineId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lectures
        builder.Entity<Lecture>()
            .HasOne(l => l.Topic)
            .WithMany(t => t.Lectures)
            .HasForeignKey(l => l.TopicId)
            .OnDelete(DeleteBehavior.Cascade);

        // Control Questions
        builder.Entity<ControlQuestion>()
            .HasOne(q => q.Lecture)
            .WithMany(l => l.ControlQuestions)
            .HasForeignKey(q => q.LectureId)
            .OnDelete(DeleteBehavior.Cascade);

        // Student Answers
        builder.Entity<StudentAnswer>()
            .HasOne(a => a.Question)
            .WithMany(q => q.StudentAnswers)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentAnswer>()
            .HasOne(a => a.Student)
            .WithMany(u => u.StudentAnswers)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentAnswer>()
            .HasOne(a => a.GradedBy)
            .WithMany()
            .HasForeignKey(a => a.GradedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Tests
        builder.Entity<Test>()
            .HasOne(t => t.Lecture)
            .WithMany(l => l.Tests)
            .HasForeignKey(t => t.LectureId)
            .OnDelete(DeleteBehavior.Cascade);

        // Test Questions
        builder.Entity<TestQuestion>()
            .HasOne(q => q.Test)
            .WithMany(t => t.Questions)
            .HasForeignKey(q => q.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Answer Options
        builder.Entity<AnswerOption>()
            .HasOne(o => o.Question)
            .WithMany(q => q.Options)
            .HasForeignKey(o => o.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Test Attempts
        builder.Entity<TestAttempt>()
            .HasOne(a => a.Test)
            .WithMany(t => t.Attempts)
            .HasForeignKey(a => a.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TestAttempt>()
            .HasOne(a => a.Student)
            .WithMany(u => u.TestAttempts)
            .HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Student Test Answers
        builder.Entity<StudentTestAnswer>()
            .HasOne(sta => sta.Attempt)
            .WithMany(a => a.StudentAnswers)
            .HasForeignKey(sta => sta.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<StudentTestAnswer>()
            .HasOne(sta => sta.Question)
            .WithMany()
            .HasForeignKey(sta => sta.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lecture Progress
        builder.Entity<LectureProgress>()
            .HasOne(p => p.Student)
            .WithMany(u => u.ProgressRecords)
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LectureProgress>()
            .HasOne(p => p.Lecture)
            .WithMany(l => l.ProgressRecords)
            .HasForeignKey(p => p.LectureId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<LectureProgress>()
            .HasOne(p => p.UnlockedByTeacher)
            .WithMany()
            .HasForeignKey(p => p.UnlockedByTeacherId)
            .OnDelete(DeleteBehavior.SetNull);

        // Ensure unique progress per student + lecture
        builder.Entity<LectureProgress>()
            .HasIndex(p => new { p.StudentId, p.LectureId })
            .IsUnique();
    }
}

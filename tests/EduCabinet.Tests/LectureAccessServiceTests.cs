using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using EduCabinet.Web.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduCabinet.Tests;

public class LectureAccessServiceTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CanAccessLecture_AllLecturesAccessibleForStudy()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new LectureAccessService(context);

        var disc = new Discipline { Id = 1, Title = "Курс 1", TeacherId = "teacher1" };
        var topic = new Topic { Id = 1, DisciplineId = 1, Title = "Тема 1", OrderIndex = 1 };
        var lecture1 = new Lecture { Id = 1, TopicId = 1, Title = "Лекция 1", OrderIndex = 1 };
        var lecture2 = new Lecture { Id = 2, TopicId = 1, Title = "Лекция 2", OrderIndex = 2 };

        context.Disciplines.Add(disc);
        context.Topics.Add(topic);
        context.Lectures.AddRange(lecture1, lecture2);
        await context.SaveChangesAsync();

        // Act
        var result1 = await service.CanAccessLectureAsync("student1", 1);
        var result2 = await service.CanAccessLectureAsync("student1", 2);

        // Assert: Both lectures are open for student
        result1.IsAllowed.Should().BeTrue();
        result2.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task DeadlineExpired_AutomaticallyAssignsGrade2()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new LectureAccessService(context);

        var disc = new Discipline { Id = 1, Title = "Курс 1", TeacherId = "teacher1" };
        var topic = new Topic { Id = 1, DisciplineId = 1, Title = "Тема 1", OrderIndex = 1 };
        var lecture = new Lecture
        {
            Id = 1,
            TopicId = 1,
            Title = "Лекция 1",
            Deadline = DateTime.UtcNow.AddDays(-2) // Deadline was 2 days ago!
        };

        context.Disciplines.Add(disc);
        context.Topics.Add(topic);
        context.Lectures.Add(lecture);
        await context.SaveChangesAsync();

        // Act: Student accesses lecture or background check executes
        await service.CheckAndApplyDeadlinesAsync("student1", 1);

        // Assert: Student gets automatic grade 2 for missing deadline
        var progress = await context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == "student1" && p.LectureId == 1);

        progress.Should().NotBeNull();
        progress!.IsOverdue.Should().BeTrue();
        progress.FinalGrade.Should().Be(2);
        progress.TeacherGradeComment.Should().Contain("Просрочено");
    }

    [Fact]
    public async Task TeacherCanOverrideGrade_ToAnyValueWithAuditLog()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new LectureAccessService(context);

        var disc = new Discipline { Id = 1, Title = "Курс 1", TeacherId = "teacher1" };
        var topic = new Topic { Id = 1, DisciplineId = 1, Title = "Тема 1", OrderIndex = 1 };
        var lecture = new Lecture { Id = 1, TopicId = 1, Title = "Лекция 1", Deadline = DateTime.UtcNow.AddDays(-3) };

        context.Disciplines.Add(disc);
        context.Topics.Add(topic);
        context.Lectures.Add(lecture);

        // Initially student has grade 2 because of deadline
        context.LectureProgresses.Add(new LectureProgress
        {
            StudentId = "student1",
            LectureId = 1,
            FinalGrade = 2,
            IsOverdue = true
        });
        await context.SaveChangesAsync();

        // Act: Teacher manually changes grade to 5 (Отлично) with comment
        await service.SetTeacherGradeAsync("student1", 1, 5, "Студент пересдал на консультации на отлично", "teacher1");

        // Assert
        var progress = await context.LectureProgresses
            .FirstOrDefaultAsync(p => p.StudentId == "student1" && p.LectureId == 1);

        progress.Should().NotBeNull();
        progress!.FinalGrade.Should().Be(5);
        progress.TeacherGradeComment.Should().Be("Студент пересдал на консультации на отлично");
        progress.IsCompleted.Should().BeTrue();

        // AuditLog record created
        var log = await context.AuditLogs.FirstOrDefaultAsync(l => l.Action == "TeacherGradeChanged");
        log.Should().NotBeNull();
        log!.Details.Should().Contain("с [2] на [5]");
    }
}

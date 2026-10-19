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
    public async Task CanAccessLecture_FirstLectureInDiscipline_AlwaysAllowed()
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
        var result = await service.CanAccessLectureAsync("student1", 1);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessLecture_SecondLecture_DeniedIfPreviousTheoryNotRead()
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
        var result = await service.CanAccessLectureAsync("student1", 2);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("теоретический материал предшествующей лекции");
    }

    [Fact]
    public async Task CanAccessLecture_SecondLecture_DeniedIfTestScoreBelowPassingThreshold()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new LectureAccessService(context);

        var disc = new Discipline { Id = 1, Title = "Курс 1", TeacherId = "teacher1" };
        var topic = new Topic { Id = 1, DisciplineId = 1, Title = "Тема 1", OrderIndex = 1 };
        var lecture1 = new Lecture { Id = 1, TopicId = 1, Title = "Лекция 1", OrderIndex = 1, RequiresTestToUnlockNext = true, PassingScorePercent = 70 };
        var lecture2 = new Lecture { Id = 2, TopicId = 1, Title = "Лекция 2", OrderIndex = 2 };

        var test = new Test { Id = 1, LectureId = 1, Title = "Тест 1", PassingScorePercent = 70 };

        context.Disciplines.Add(disc);
        context.Topics.Add(topic);
        context.Lectures.AddRange(lecture1, lecture2);
        context.Tests.Add(test);

        // Student read theory
        context.LectureProgresses.Add(new LectureProgress
        {
            StudentId = "student1",
            LectureId = 1,
            IsTheoryRead = true
        });

        // Student took test but got 50% (< 70%)
        context.TestAttempts.Add(new TestAttempt
        {
            StudentId = "student1",
            TestId = 1,
            ScorePercent = 50,
            IsPassed = false,
            Status = AttemptStatus.Completed
        });

        await context.SaveChangesAsync();

        // Act
        var result = await service.CanAccessLectureAsync("student1", 2);

        // Assert
        result.IsAllowed.Should().BeFalse();
        result.Reason.Should().Contain("не менее 70%");
    }

    [Fact]
    public async Task CanAccessLecture_SecondLecture_AllowedWhenTestPassedSuccessfully()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new LectureAccessService(context);

        var disc = new Discipline { Id = 1, Title = "Курс 1", TeacherId = "teacher1" };
        var topic = new Topic { Id = 1, DisciplineId = 1, Title = "Тема 1", OrderIndex = 1 };
        var lecture1 = new Lecture { Id = 1, TopicId = 1, Title = "Лекция 1", OrderIndex = 1, RequiresTestToUnlockNext = true, PassingScorePercent = 70 };
        var lecture2 = new Lecture { Id = 2, TopicId = 1, Title = "Лекция 2", OrderIndex = 2 };

        var test = new Test { Id = 1, LectureId = 1, Title = "Тест 1", PassingScorePercent = 70 };

        context.Disciplines.Add(disc);
        context.Topics.Add(topic);
        context.Lectures.AddRange(lecture1, lecture2);
        context.Tests.Add(test);

        // Student read theory
        context.LectureProgresses.Add(new LectureProgress
        {
            StudentId = "student1",
            LectureId = 1,
            IsTheoryRead = true
        });

        // Student passed test with 85%
        context.TestAttempts.Add(new TestAttempt
        {
            StudentId = "student1",
            TestId = 1,
            ScorePercent = 85,
            IsPassed = true,
            Status = AttemptStatus.Completed
        });

        await context.SaveChangesAsync();

        // Act
        var result = await service.CanAccessLectureAsync("student1", 2);

        // Assert
        result.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public async Task CanAccessLecture_SecondLecture_AllowedWhenTeacherUnlocksManually()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new LectureAccessService(context);

        var disc = new Discipline { Id = 1, Title = "Курс 1", TeacherId = "teacher1" };
        var topic = new Topic { Id = 1, DisciplineId = 1, Title = "Тема 1", OrderIndex = 1 };
        var lecture1 = new Lecture { Id = 1, TopicId = 1, Title = "Лекция 1", OrderIndex = 1, RequiresTestToUnlockNext = true, PassingScorePercent = 70 };
        var lecture2 = new Lecture { Id = 2, TopicId = 1, Title = "Лекция 2", OrderIndex = 2 };

        context.Disciplines.Add(disc);
        context.Topics.Add(topic);
        context.Lectures.AddRange(lecture1, lecture2);
        await context.SaveChangesAsync();

        // Act - Teacher manually unlocks lecture 2
        var unlockSuccess = await service.UnlockLectureManuallyAsync("student1", 2, "teacher1", "Индивидуальный график");
        var accessResult = await service.CanAccessLectureAsync("student1", 2);

        // Assert
        unlockSuccess.Should().BeTrue();
        accessResult.IsAllowed.Should().BeTrue();

        // Check AuditLog was created
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ManualLectureUnlock");
        audit.Should().NotBeNull();
        audit!.Details.Should().Contain("Индивидуальный график");
    }

    [Fact]
    public async Task MultiUserIsolation_StudentAProgressDoesNotUnlockStudentB()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var service = new LectureAccessService(context);

        var disc = new Discipline { Id = 1, Title = "Курс 1", TeacherId = "teacher1" };
        var topic = new Topic { Id = 1, DisciplineId = 1, Title = "Тема 1", OrderIndex = 1 };
        var lecture1 = new Lecture { Id = 1, TopicId = 1, Title = "Лекция 1", OrderIndex = 1, RequiresTestToUnlockNext = true, PassingScorePercent = 70 };
        var lecture2 = new Lecture { Id = 2, TopicId = 1, Title = "Лекция 2", OrderIndex = 2 };

        var test = new Test { Id = 1, LectureId = 1, Title = "Тест 1", PassingScorePercent = 70 };

        context.Disciplines.Add(disc);
        context.Topics.Add(topic);
        context.Lectures.AddRange(lecture1, lecture2);
        context.Tests.Add(test);

        // Student A passed everything
        context.LectureProgresses.Add(new LectureProgress { StudentId = "studentA", LectureId = 1, IsTheoryRead = true });
        context.TestAttempts.Add(new TestAttempt { StudentId = "studentA", TestId = 1, ScorePercent = 100, IsPassed = true, Status = AttemptStatus.Completed });

        await context.SaveChangesAsync();

        // Act
        var accessA = await service.CanAccessLectureAsync("studentA", 2);
        var accessB = await service.CanAccessLectureAsync("studentB", 2);

        // Assert: Student A has access, Student B does not!
        accessA.IsAllowed.Should().BeTrue();
        accessB.IsAllowed.Should().BeFalse();
    }
}

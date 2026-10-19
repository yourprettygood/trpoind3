using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using EduCabinet.Web.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduCabinet.Tests;

public class AssessmentAndDataIsolationTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GeminiRule1_StaffGrading_NeverOverwritesStudentAnswer()
    {
        // Arrange (GEMINI.md Rule 1 check)
        using var context = CreateInMemoryDbContext();

        var q = new ControlQuestion
        {
            Id = 1,
            LectureId = 1,
            QuestionText = "Поясните разницу между ручным и автоматизированным тестированием",
            QuestionType = QuestionType.DetailedAnswer,
            MaxScore = 10
        };
        context.ControlQuestions.Add(q);

        var originalStudentText = "Ручное тестирование выполняет человек, а автоматизированное - скрипты.";
        var submissionTime = DateTime.UtcNow.AddHours(-2);

        var answer = new StudentAnswer
        {
            QuestionId = 1,
            StudentId = "student_1",
            StudentAnswerText = originalStudentText,
            SubmittedAt = submissionTime,
            IsGraded = false
        };
        context.StudentAnswers.Add(answer);
        await context.SaveChangesAsync();

        // Act - Teacher grades the student's submission
        var toGrade = await context.StudentAnswers.FindAsync(answer.Id);
        toGrade.Should().NotBeNull();

        toGrade!.IsGraded = true;
        toGrade.TeacherScore = 9;
        toGrade.TeacherFeedback = "Отличный ответ, раскрыта суть понятия.";
        toGrade.GradedById = "teacher_1";
        toGrade.GradedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        // Assert
        var reloaded = await context.StudentAnswers.FindAsync(answer.Id);
        reloaded.Should().NotBeNull();

        // Student's data is 100% preserved and untouched
        reloaded!.StudentAnswerText.Should().Be(originalStudentText);
        reloaded.SubmittedAt.Should().Be(submissionTime);

        // Teacher's resolution is cleanly saved in separate fields
        reloaded.IsGraded.Should().BeTrue();
        reloaded.TeacherScore.Should().Be(9);
        reloaded.TeacherFeedback.Should().Be("Отличный ответ, раскрыта суть понятия.");
    }

    [Fact]
    public async Task TestService_CalculatesScoreAndPassingCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var accessMock = new LectureAccessService(context);
        var testService = new TestService(context, accessMock);

        var lecture = new Lecture { Id = 1, Title = "Лекция", TopicId = 1 };
        context.Lectures.Add(lecture);

        var test = new Test
        {
            Id = 1,
            LectureId = 1,
            Title = "Тест по качеству ПО",
            PassingScorePercent = 70,
            MaxAttempts = 3
        };
        context.Tests.Add(test);

        var q1 = new TestQuestion { Id = 1, TestId = 1, QuestionText = "Вопрос 1", QuestionType = QuestionType.SingleChoice, Points = 1 };
        var q2 = new TestQuestion { Id = 2, TestId = 1, QuestionText = "Вопрос 2", QuestionType = QuestionType.SingleChoice, Points = 1 };
        context.TestQuestions.AddRange(q1, q2);

        var o1 = new AnswerOption { Id = 1, QuestionId = 1, OptionText = "Верный", IsCorrect = true };
        var o2 = new AnswerOption { Id = 2, QuestionId = 1, OptionText = "Неверный", IsCorrect = false };
        var o3 = new AnswerOption { Id = 3, QuestionId = 2, OptionText = "Верный", IsCorrect = true };
        var o4 = new AnswerOption { Id = 4, QuestionId = 2, OptionText = "Неверный", IsCorrect = false };
        context.AnswerOptions.AddRange(o1, o2, o3, o4);
        await context.SaveChangesAsync();

        // Act 1: Student starts attempt
        var (attempt, startErr) = await testService.StartAttemptAsync("student1", 1);
        startErr.Should().BeNull();
        attempt.Should().NotBeNull();

        // Act 2: Submit with 1 correct and 1 wrong answer (1/2 = 50%)
        var submission = new SubmitAttemptModel
        {
            AttemptId = attempt!.Id,
            Answers = new Dictionary<int, string>
            {
                { 1, "1" }, // Correct
                { 2, "4" }  // Wrong
            }
        };

        var (completedAttempt, submitErr) = await testService.SubmitAttemptAsync("student1", submission);

        // Assert
        submitErr.Should().BeNull();
        completedAttempt.Should().NotBeNull();
        completedAttempt!.TotalPoints.Should().Be(1);
        completedAttempt.MaxPoints.Should().Be(2);
        completedAttempt.ScorePercent.Should().Be(50);
        completedAttempt.IsPassed.Should().BeFalse(); // 50% < 70%
    }

    [Fact]
    public async Task TestService_BlocksAfterExceedingMaxAttempts()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var accessMock = new LectureAccessService(context);
        var testService = new TestService(context, accessMock);

        var lecture = new Lecture { Id = 1, Title = "Лекция", TopicId = 1 };
        context.Lectures.Add(lecture);

        var test = new Test
        {
            Id = 1,
            LectureId = 1,
            Title = "Тест",
            PassingScorePercent = 70,
            MaxAttempts = 2
        };
        context.Tests.Add(test);

        // Simulate 2 completed attempts
        context.TestAttempts.Add(new TestAttempt { StudentId = "student1", TestId = 1, Status = AttemptStatus.Completed, AttemptNumber = 1 });
        context.TestAttempts.Add(new TestAttempt { StudentId = "student1", TestId = 1, Status = AttemptStatus.Completed, AttemptNumber = 2 });
        await context.SaveChangesAsync();

        // Act - Try 3rd attempt
        var (attempt, error) = await testService.StartAttemptAsync("student1", 1);

        // Assert
        attempt.Should().BeNull();
        error.Should().Contain("исчерпали максимальное количество попыток (2)");
    }
}

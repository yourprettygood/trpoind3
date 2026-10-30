using System.Security.Claims;
using EduCabinet.Web.Controllers;
using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using EduCabinet.Web.Services;
using EduCabinet.Web.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduCabinet.Tests;

public class TeacherTestManagementTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static void SetupTeacherContext(Controller controller, string teacherId = "teacher_1")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, teacherId),
            new(ClaimTypes.Name, "teacher@cabinet.local"),
            new(ClaimTypes.Role, "Teacher")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = claimsPrincipal };
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = tempData;
    }

    [Fact]
    public async Task Teacher_CreateTestQuestion_SuccessfullyAddsQuestionAndOptions()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userManagerMock = CreateUserManagerMock();
        var envMock = new Mock<IWebHostEnvironment>();
        var accessService = new LectureAccessService(context);

        var discipline = new Discipline { Id = 1, Title = "Архитектура ПО", TeacherId = "teacher_1" };
        var topic = new Topic { Id = 1, DisciplineId = 1, Title = "Введение" };
        var lecture = new Lecture { Id = 10, TopicId = 1, Title = "Тестирование и развертывание", Topic = topic };
        context.Disciplines.Add(discipline);
        context.Topics.Add(topic);
        context.Lectures.Add(lecture);
        await context.SaveChangesAsync();

        var controller = new TeacherController(context, userManagerMock.Object, accessService, envMock.Object);
        SetupTeacherContext(controller);

        var model = new CreateTestQuestionViewModel
        {
            LectureId = 10,
            QuestionText = "Какой протокол используется для шифрования веб-трафика?",
            QuestionType = QuestionType.SingleChoice,
            Points = 2,
            OrderIndex = 1,
            Options = new List<AnswerOptionItemViewModel>
            {
                new() { OptionText = "HTTPS", IsCorrect = true, OrderIndex = 1 },
                new() { OptionText = "HTTP", IsCorrect = false, OrderIndex = 2 },
                new() { OptionText = "FTP", IsCorrect = false, OrderIndex = 3 }
            }
        };

        // Act
        var result = await controller.CreateTestQuestion(model);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Questions");

        // Verify Test was created/ensured
        var test = await context.Tests.Include(t => t.Questions).ThenInclude(q => q.Options).FirstOrDefaultAsync(t => t.LectureId == 10);
        test.Should().NotBeNull();
        test!.Questions.Should().HaveCount(1);

        var savedQuestion = test.Questions.First();
        savedQuestion.QuestionText.Should().Be("Какой протокол используется для шифрования веб-трафика?");
        savedQuestion.Points.Should().Be(2);
        savedQuestion.Options.Should().HaveCount(3);
        savedQuestion.Options.Should().Contain(o => o.OptionText == "HTTPS" && o.IsCorrect);
        savedQuestion.Options.Should().Contain(o => o.OptionText == "HTTP" && !o.IsCorrect);

        // Verify audit log
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "TestQuestionCreated");
        audit.Should().NotBeNull();
        audit!.Details.Should().Contain("Какой протокол используется для шифрования");
    }

    [Fact]
    public async Task Teacher_CreateTestQuestion_FailsWhenFewerThanTwoOptions()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userManagerMock = CreateUserManagerMock();
        var envMock = new Mock<IWebHostEnvironment>();
        var accessService = new LectureAccessService(context);

        var discipline = new Discipline { Id = 1, Title = "Курс", TeacherId = "teacher_1" };
        var topic = new Topic { Id = 1, DisciplineId = 1, Title = "Тема" };
        var lecture = new Lecture { Id = 5, TopicId = 1, Title = "Лекция", Topic = topic };
        context.Disciplines.Add(discipline);
        context.Topics.Add(topic);
        context.Lectures.Add(lecture);
        await context.SaveChangesAsync();

        var controller = new TeacherController(context, userManagerMock.Object, accessService, envMock.Object);
        SetupTeacherContext(controller);

        var model = new CreateTestQuestionViewModel
        {
            LectureId = 5,
            QuestionText = "Вопрос с одним вариантом",
            QuestionType = QuestionType.SingleChoice,
            Options = new List<AnswerOptionItemViewModel>
            {
                new() { OptionText = "Единственный вариант", IsCorrect = true, OrderIndex = 1 }
            }
        };

        // Act
        var result = await controller.CreateTestQuestion(model);

        // Assert
        result.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState[string.Empty]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("минимум 2 варианта"));
    }

    [Fact]
    public async Task Teacher_DeleteTestQuestion_CascadesAndRemovesOptions()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userManagerMock = CreateUserManagerMock();
        var envMock = new Mock<IWebHostEnvironment>();
        var accessService = new LectureAccessService(context);

        var test = new Test { Id = 1, LectureId = 1, Title = "Тест" };
        var q = new TestQuestion { Id = 100, TestId = 1, QuestionText = "Вопрос для удаления" };
        var opt1 = new AnswerOption { Id = 201, QuestionId = 100, OptionText = "Опция 1", IsCorrect = true };
        var opt2 = new AnswerOption { Id = 202, QuestionId = 100, OptionText = "Опция 2", IsCorrect = false };

        context.Tests.Add(test);
        context.TestQuestions.Add(q);
        context.AnswerOptions.AddRange(opt1, opt2);
        await context.SaveChangesAsync();

        var controller = new TeacherController(context, userManagerMock.Object, accessService, envMock.Object);
        SetupTeacherContext(controller);

        // Act
        var result = await controller.DeleteTestQuestion(100, 1);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Questions");

        var deletedQuestion = await context.TestQuestions.FindAsync(100);
        deletedQuestion.Should().BeNull();

        var remainingOptions = await context.AnswerOptions.Where(o => o.QuestionId == 100).ToListAsync();
        remainingOptions.Should().BeEmpty();
    }

    [Fact]
    public async Task Teacher_UpdateTestSettings_UpdatesThresholdAndAttempts()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var userManagerMock = CreateUserManagerMock();
        var envMock = new Mock<IWebHostEnvironment>();
        var accessService = new LectureAccessService(context);

        var lecture = new Lecture { Id = 2, Title = "Лекция 2", PassingScorePercent = 60 };
        var test = new Test { Id = 10, LectureId = 2, Title = "Старое название", PassingScorePercent = 60, MaxAttempts = 2 };
        context.Lectures.Add(lecture);
        context.Tests.Add(test);
        await context.SaveChangesAsync();

        var controller = new TeacherController(context, userManagerMock.Object, accessService, envMock.Object);
        SetupTeacherContext(controller);

        var model = new UpdateTestSettingsViewModel
        {
            TestId = 10,
            LectureId = 2,
            Title = "Итоговый тест (Обновлено)",
            PassingScorePercent = 85,
            MaxAttempts = 5,
            TimeLimitMinutes = 20
        };

        // Act
        var result = await controller.UpdateTestSettings(model);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Questions");

        var updated = await context.Tests.FindAsync(10);
        updated!.Title.Should().Be("Итоговый тест (Обновлено)");
        updated.PassingScorePercent.Should().Be(85);
        updated.MaxAttempts.Should().Be(5);
        updated.TimeLimitMinutes.Should().Be(20);

        var updatedLec = await context.Lectures.FindAsync(2);
        updatedLec!.PassingScorePercent.Should().Be(85);
    }
}

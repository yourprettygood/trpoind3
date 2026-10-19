using System.Security.Claims;
using EduCabinet.Web.Controllers;
using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using EduCabinet.Web.ViewModels;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduCabinet.Tests;

public class AdminUserManagementTests
{
    private ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static (Mock<UserManager<ApplicationUser>> userManagerMock, Mock<RoleManager<IdentityRole>> roleManagerMock) CreateIdentityMocks()
    {
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        var userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStoreMock = new Mock<IRoleStore<IdentityRole>>();
        var roleManagerMock = new Mock<RoleManager<IdentityRole>>(
            roleStoreMock.Object, null!, null!, null!, null!);

        return (userManagerMock, roleManagerMock);
    }

    private static void SetupAdminUserContext(Controller controller)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "admin_guid_1"),
            new(ClaimTypes.Name, "admin@cabinet.local"),
            new(ClaimTypes.Role, "Administrator")
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = claimsPrincipal };
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = tempData;
    }

    [Fact]
    public async Task Admin_CreateUser_SuccessfullyRegistersUserAndGroup()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (userManagerMock, roleManagerMock) = CreateIdentityMocks();

        var group = new StudentGroup { Id = 10, Name = "ИС-31", CourseNumber = 3 };
        context.StudentGroups.Add(group);
        await context.SaveChangesAsync();

        userManagerMock.Setup(m => m.FindByEmailAsync("newstudent@cabinet.local"))
            .ReturnsAsync((ApplicationUser?)null);

        userManagerMock.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), "Student123!"))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) => u.Id = "student_new_1");

        roleManagerMock.Setup(m => m.RoleExistsAsync("Student"))
            .ReturnsAsync(true);

        userManagerMock.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Student"))
            .ReturnsAsync(IdentityResult.Success);

        userManagerMock.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns("admin_guid_1");

        var controller = new AdminController(context, userManagerMock.Object, roleManagerMock.Object);
        SetupAdminUserContext(controller);

        var model = new CreateUserViewModel
        {
            FullName = "Сидоров Петр Алексеевич",
            Email = "newstudent@cabinet.local",
            Password = "Student123!",
            ConfirmPassword = "Student123!",
            Role = "Student",
            GroupId = 10,
            PhoneNumber = "+79991234567"
        };

        // Act
        var result = await controller.CreateUser(model);

        // Assert
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Users");

        // Verify group membership was created
        var membership = await context.GroupMemberships.FirstOrDefaultAsync(m => m.StudentId == "student_new_1");
        membership.Should().NotBeNull();
        membership!.GroupId.Should().Be(10);

        // Verify audit log entry
        var audit = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "UserCreated");
        audit.Should().NotBeNull();
        audit!.Details.Should().Contain("newstudent@cabinet.local");
        audit.Details.Should().Contain("Сидоров Петр Алексеевич");
    }

    [Fact]
    public async Task Admin_CreateUser_RejectsDuplicateEmail()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (userManagerMock, roleManagerMock) = CreateIdentityMocks();

        userManagerMock.Setup(m => m.FindByEmailAsync("existing@cabinet.local"))
            .ReturnsAsync(new ApplicationUser { Email = "existing@cabinet.local", UserName = "existing@cabinet.local" });

        var controller = new AdminController(context, userManagerMock.Object, roleManagerMock.Object);
        SetupAdminUserContext(controller);

        var model = new CreateUserViewModel
        {
            FullName = "Иванов Иван",
            Email = "existing@cabinet.local",
            Password = "Pass1234!",
            ConfirmPassword = "Pass1234!",
            Role = "Teacher"
        };

        // Act
        var result = await controller.CreateUser(model);

        // Assert
        result.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState["Email"]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("уже зарегистрирован"));
    }

    [Fact]
    public async Task Admin_CreateUser_RejectsInvalidRole()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (userManagerMock, roleManagerMock) = CreateIdentityMocks();

        userManagerMock.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var controller = new AdminController(context, userManagerMock.Object, roleManagerMock.Object);
        SetupAdminUserContext(controller);

        var model = new CreateUserViewModel
        {
            FullName = "Хакер Хакер",
            Email = "hacker@evil.local",
            Password = "Pass1234!",
            ConfirmPassword = "Pass1234!",
            Role = "SuperGodRole"
        };

        // Act
        var result = await controller.CreateUser(model);

        // Assert
        result.Should().BeOfType<ViewResult>();
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState["Role"]!.Errors.Should().Contain(e => e.ErrorMessage.Contains("недопустимая роль"));
    }

    [Fact]
    public async Task Admin_DeleteUser_PreventsDeletingSelf()
    {
        // Arrange
        using var context = CreateInMemoryDbContext();
        var (userManagerMock, roleManagerMock) = CreateIdentityMocks();

        userManagerMock.Setup(m => m.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns("admin_guid_1");

        var controller = new AdminController(context, userManagerMock.Object, roleManagerMock.Object);
        SetupAdminUserContext(controller);

        // Act - Trying to delete self
        var result = await controller.DeleteUser("admin_guid_1");

        // Assert
        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Users");
        controller.TempData["ErrorMessage"].Should().Be("Нельзя удалить свою собственную учётную запись администратора.");
    }
}

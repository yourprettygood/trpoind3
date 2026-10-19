using System.ComponentModel.DataAnnotations;
using EduCabinet.Web.Models.Entities;

namespace EduCabinet.Web.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int StudentsCount { get; set; }
    public int TeachersCount { get; set; }
    public int DisciplinesCount { get; set; }
    public int TotalAttemptsCount { get; set; }
    public List<AuditLog> RecentAuditLogs { get; set; } = new();
}

public class UserListItemViewModel
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? GroupName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EditUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = "Student";
    public int? GroupId { get; set; }
}

public class CreateUserViewModel
{
    [Required(ErrorMessage = "Введите ФИО пользователя")]
    [Display(Name = "ФИО пользователя")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "ФИО должно содержать от 3 до 150 символов")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Введите адрес электронной почты")]
    [EmailAddress(ErrorMessage = "Укажите корректный адрес электронной почты")]
    [Display(Name = "Email (Логин для входа)")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Задайте пароль учетной записи")]
    [StringLength(100, MinimumLength = 4, ErrorMessage = "Пароль должен содержать не менее {2} символов")]
    [DataType(DataType.Password)]
    [Display(Name = "Пароль")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Повтор пароля")]
    [Compare("Password", ErrorMessage = "Введенные пароли не совпадают")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Выберите роль в системе")]
    [Display(Name = "Роль пользователя (RBAC)")]
    public string Role { get; set; } = "Student";

    [Display(Name = "Учебная группа (для студентов)")]
    public int? GroupId { get; set; }

    [Phone(ErrorMessage = "Укажите корректный номер телефона")]
    [Display(Name = "Контактный телефон")]
    public string? PhoneNumber { get; set; }
}


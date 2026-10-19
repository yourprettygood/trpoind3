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

using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using EduCabinet.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCabinet.Web.Controllers;

[Authorize(Roles = "Administrator")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _context.Users.ToListAsync();
        var disciplinesCount = await _context.Disciplines.CountAsync();
        var attemptsCount = await _context.TestAttempts.CountAsync();
        var auditLogs = await _context.AuditLogs
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalUsers = users.Count,
            StudentsCount = users.Count(u => u.UserRole == "Student"),
            TeachersCount = users.Count(u => u.UserRole == "Teacher"),
            DisciplinesCount = disciplinesCount,
            TotalAttemptsCount = attemptsCount,
            RecentAuditLogs = auditLogs
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Users()
    {
        var users = await _context.Users
            .Include(u => u.GroupMemberships)
                .ThenInclude(m => m.Group)
            .OrderBy(u => u.UserRole)
            .ThenBy(u => u.FullName)
            .ToListAsync();

        var list = users.Select(u => new UserListItemViewModel
        {
            Id = u.Id,
            UserName = u.UserName ?? string.Empty,
            Email = u.Email ?? string.Empty,
            FullName = u.FullName,
            Role = u.UserRole,
            GroupName = u.GroupMemberships.FirstOrDefault()?.Group.Name,
            CreatedAt = u.CreatedAt
        }).ToList();

        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> EditUser(string id)
    {
        var user = await _context.Users
            .Include(u => u.GroupMemberships)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null) return NotFound();

        ViewBag.Groups = await _context.StudentGroups.OrderBy(g => g.Name).ToListAsync();

        var model = new EditUserViewModel
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Role = user.UserRole,
            GroupId = user.GroupMemberships.FirstOrDefault()?.GroupId
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUser(EditUserViewModel model)
    {
        var user = await _context.Users
            .Include(u => u.GroupMemberships)
            .FirstOrDefaultAsync(u => u.Id == model.Id);

        if (user == null) return NotFound();

        var oldRole = user.UserRole;
        user.FullName = model.FullName;
        user.UserRole = model.Role;

        // Update Identity Roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, model.Role);

        // Update Group
        var currentMembership = user.GroupMemberships.FirstOrDefault();
        if (model.GroupId.HasValue)
        {
            if (currentMembership != null)
            {
                currentMembership.GroupId = model.GroupId.Value;
            }
            else
            {
                _context.GroupMemberships.Add(new GroupMembership
                {
                    StudentId = user.Id,
                    GroupId = model.GroupId.Value
                });
            }
        }
        else if (currentMembership != null)
        {
            _context.GroupMemberships.Remove(currentMembership);
        }

        // Audit log
        _context.AuditLogs.Add(new AuditLog
        {
            Action = "UserUpdated",
            EntityName = "User",
            EntityId = user.Id,
            UserId = _userManager.GetUserId(User),
            UserName = User.Identity?.Name ?? "Admin",
            Details = $"Обновлены данные пользователя {user.Email}. Роль изменена с {oldRole} на {model.Role}.",
            Timestamp = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Данные пользователя успешно обновлены!";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> Groups()
    {
        var groups = await _context.StudentGroups
            .Include(g => g.Memberships)
                .ThenInclude(m => m.Student)
            .OrderBy(g => g.Name)
            .ToListAsync();

        return View(groups);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateGroup(StudentGroup model)
    {
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            TempData["ErrorMessage"] = "Название группы не может быть пустым.";
            return RedirectToAction(nameof(Groups));
        }

        _context.StudentGroups.Add(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Учебная группа успешно добавлена!";
        return RedirectToAction(nameof(Groups));
    }

    [HttpGet]
    public async Task<IActionResult> AuditLogs()
    {
        var logs = await _context.AuditLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(100)
            .ToListAsync();

        return View(logs);
    }
}

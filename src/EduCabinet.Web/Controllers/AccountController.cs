using EduCabinet.Web.Data;
using EduCabinet.Web.Models.Entities;
using EduCabinet.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EduCabinet.Web.Controllers;

public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Пользователь с таким email не найден.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (result.Succeeded)
        {
            return RedirectToLocal(returnUrl, user);
        }

        ModelState.AddModelError(string.Empty, "Неверный пароль.");
        return View(model);
    }

    /// <summary>
    /// Демонстрационный быстрый вход в 1 клик (согласно GEMINI.md Rule 4)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> QuickLogin(string role)
    {
        string email = role.ToLower() switch
        {
            "admin" or "administrator" => "admin@cabinet.local",
            "teacher" => "teacher@cabinet.local",
            "student2" => "student2@cabinet.local",
            _ => "student@cabinet.local"
        };

        var user = await _userManager.FindByEmailAsync(email);
        if (user != null)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToLocal(null, user);
        }

        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> Register()
    {
        ViewBag.Groups = await _context.StudentGroups.OrderBy(g => g.Name).ToListAsync();
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Groups = await _context.StudentGroups.OrderBy(g => g.Name).ToListAsync();
            return View(model);
        }

        var existing = await _userManager.FindByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError("Email", "Пользователь с таким email уже зарегистрирован.");
            ViewBag.Groups = await _context.StudentGroups.OrderBy(g => g.Name).ToListAsync();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            UserRole = "Student",
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Student");

            if (model.GroupId.HasValue)
            {
                _context.GroupMemberships.Add(new GroupMembership
                {
                    StudentId = user.Id,
                    GroupId = model.GroupId.Value
                });
                await _context.SaveChangesAsync();
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Student");
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        ViewBag.Groups = await _context.StudentGroups.OrderBy(g => g.Name).ToListAsync();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl, ApplicationUser user)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        if (user.UserRole == "Administrator")
            return RedirectToAction("Index", "Admin");
        if (user.UserRole == "Teacher")
            return RedirectToAction("Index", "Teacher");

        return RedirectToAction("Index", "Student");
    }
}

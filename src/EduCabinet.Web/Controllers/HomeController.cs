using EduCabinet.Web.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EduCabinet.Web.Controllers;

public class HomeController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public HomeController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                if (user.UserRole == "Administrator")
                    return RedirectToAction("Index", "Admin");
                if (user.UserRole == "Teacher")
                    return RedirectToAction("Index", "Teacher");
                return RedirectToAction("Index", "Student");
            }
        }

        return RedirectToAction("Login", "Account");
    }

    public IActionResult Privacy()
    {
        return View();
    }
}

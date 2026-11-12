using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ShopManager.Web.Models;
using ShopManager.Web.ViewModels;

namespace ShopManager.Web.Controllers;

[Authorize(Roles = Roles.Manager)]
public class StaffController(UserManager<ApplicationUser> users) : Controller
{
    public async Task<IActionResult> Index() => View(await users.GetUsersInRoleAsync(Roles.Staff));
    [HttpGet] public IActionResult Create() => View(new StaffForm());
    [HttpPost]
    public async Task<IActionResult> Create(StaffForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var user = new ApplicationUser { UserName = form.Email.Trim(), Email = form.Email.Trim(), FullName = form.FullName.Trim(), EmailConfirmed = true };
        var result = await users.CreateAsync(user, form.Password);
        if (result.Succeeded)
        {
            result = await users.AddToRoleAsync(user, Roles.Staff);
            if (result.Succeeded) { TempData["Success"] = "Đã tạo tài khoản nhân viên."; return RedirectToAction(nameof(Index)); }
            await users.DeleteAsync(user);
        }
        foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        return View(form);
    }
    [HttpPost]
    public async Task<IActionResult> SetActive(string id, bool active)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null || !await users.IsInRoleAsync(user, Roles.Staff) || await users.IsInRoleAsync(user, Roles.Manager)) return NotFound();
        user.IsActive = active;
        var result = await users.UpdateAsync(user);
        if (result.Succeeded)
        {
            await users.UpdateSecurityStampAsync(user);
            TempData["Success"] = active ? "Đã mở tài khoản." : "Đã khóa tài khoản nhân viên.";
        }
        else TempData["Error"] = "Tài khoản vừa thay đổi, hãy thử lại.";
        return RedirectToAction(nameof(Index));
    }
}

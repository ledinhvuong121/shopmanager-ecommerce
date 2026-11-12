using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.ViewModels;

namespace ShopManager.Web.Controllers;

[Authorize]
public class AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles, AppDbContext db) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        if (User.Identity?.IsAuthenticated == true) return Landing();
        return View(new LoginForm { ReturnUrl = returnUrl });
    }
    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var identifier = form.Identifier.Trim();
        var user = await users.FindByNameAsync(identifier) ?? await users.FindByEmailAsync(identifier);
        if (user is { IsActive: true })
        {
            var result = await signIn.PasswordSignInAsync(user, form.Password, false, true);
            if (result.Succeeded)
                return Url.IsLocalUrl(form.ReturnUrl) ? LocalRedirect(form.ReturnUrl!)
                    : await users.IsInRoleAsync(user, Roles.Customer) ? RedirectToAction("Create", "Orders") : RedirectToAction("Index", "Home");
        }
        ModelState.AddModelError("", "Thông tin đăng nhập không đúng, tài khoản bị khóa hoặc tạm khóa sau nhiều lần nhập sai.");
        return View(form);
    }
    private IActionResult Landing() => User.IsInRole(Roles.Customer)
        ? RedirectToAction("Create", "Orders") : RedirectToAction("Index", "Home");

    [AllowAnonymous, HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? Landing() : View(new CustomerRegistrationForm());

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Register(CustomerRegistrationForm form)
    {
        if (User.Identity?.IsAuthenticated == true) return Landing();
        if (!ModelState.IsValid) return View(form);

        await using var transaction = await db.Database.BeginTransactionAsync();
        try
        {
            var result = await roles.RoleExistsAsync(Roles.Customer)
                ? IdentityResult.Success : await roles.CreateAsync(new IdentityRole(Roles.Customer));
            var user = new ApplicationUser
            {
                UserName = form.Email.Trim(), Email = form.Email.Trim(),
                FullName = form.FullName.Trim(), PhoneNumber = form.Phone.Trim()
            };
            if (result.Succeeded) result = await users.CreateAsync(user, form.Password);
            if (result.Succeeded) result = await users.AddToRoleAsync(user, Roles.Customer);
            if (result.Succeeded)
            {
                await transaction.CommitAsync();
                TempData["Success"] = "Đã tạo tài khoản. Bạn có thể đăng nhập để mua hàng.";
                return RedirectToAction(nameof(Login));
            }
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("", "Chưa thể tạo tài khoản. Hãy kiểm tra email và thử lại.");
        }
        return View(form);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
    [HttpGet] public IActionResult ChangePassword() => View(new PasswordForm());
    [HttpPost]
    public async Task<IActionResult> ChangePassword(PasswordForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var user = (await users.GetUserAsync(User))!;
        var result = await users.ChangePasswordAsync(user, form.CurrentPassword, form.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError("", error.Description);
            return View(form);
        }
        await signIn.RefreshSignInAsync(user);
        TempData["Success"] = "Đã đổi mật khẩu.";
        return Landing();
    }
    public IActionResult Denied() { Response.StatusCode = 403; return View(); }
}

using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopManager.Web.Models;
using ShopManager.Web.Services;

namespace ShopManager.Web.Controllers;

[Authorize(Roles = Roles.BuyersAndEmployees)]
public class HomeController(ReportService reports) : Controller
{
    [AllowAnonymous]
    public async Task<IActionResult> Index(DateTime? from, DateTime? to)
    {
        if (!User.IsInRole(Roles.Manager)) return RedirectToAction("Create", "Orders");
        var today = VietnamTime.Today;
        var start = (from ?? today.AddDays(-6)).Date;
        var end = (to ?? today).Date;
        if (start < new DateTime(2000, 1, 1) || end > today || end < start || (end - start).TotalDays > 365)
        {
            TempData["Error"] = "Chọn khoảng ngày hợp lệ, tối đa 366 ngày và không sau hôm nay.";
            return RedirectToAction(nameof(Index));
        }
        return View(await reports.GetAsync(start, end));
    }
    [AllowAnonymous, IgnoreAntiforgeryToken]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

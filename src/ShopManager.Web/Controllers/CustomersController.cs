using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.ViewModels;

namespace ShopManager.Web.Controllers;

[Authorize(Roles = Roles.Employees)]
public class CustomersController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = db.Customers.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Phone.Contains(search));
        return View(await Paging.CreateAsync(query.OrderBy(x => x.Name), page, search));
    }
    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (!id.HasValue) return View(new CustomerForm());
        var c = await db.Customers.FindAsync(id);
        return c is null ? NotFound() : View(new CustomerForm { Id = c.Id, Name = c.Name, Phone = c.Phone, Address = c.Address });
    }
    [HttpPost]
    public async Task<IActionResult> Edit(CustomerForm form)
    {
        if (!ModelState.IsValid) return View(form);
        var c = form.Id == 0 ? new Customer() : await db.Customers.FindAsync(form.Id);
        if (c is null) return NotFound();
        if (form.Id == 0) db.Customers.Add(c);
        c.Name = form.Name.Trim(); c.Phone = form.Phone.Trim(); c.Address = form.Address?.Trim();
        await db.SaveChangesAsync();
        TempData["Success"] = "Đã lưu khách hàng.";
        return RedirectToAction(nameof(Index));
    }
}

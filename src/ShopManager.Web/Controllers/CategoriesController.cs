using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.ViewModels;

namespace ShopManager.Web.Controllers;

[Authorize(Roles = Roles.Manager)]
public class CategoriesController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index() => View(await db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync());
    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (!id.HasValue) return View(new CategoryForm());
        var category = await db.Categories.FindAsync(id);
        return category is null ? NotFound() : View(new CategoryForm { Id = category.Id, Name = category.Name });
    }
    [HttpPost]
    public async Task<IActionResult> Edit(CategoryForm form)
    {
        if (!ModelState.IsValid) return View(form);
        if (await db.Categories.AnyAsync(x => x.Name == form.Name.Trim() && x.Id != form.Id)) ModelState.AddModelError(nameof(form.Name), "Danh mục đã tồn tại.");
        if (!ModelState.IsValid) return View(form);
        var category = form.Id == 0 ? new Category() : await db.Categories.FindAsync(form.Id);
        if (category is null) return NotFound();
        if (form.Id == 0) db.Categories.Add(category);
        category.Name = form.Name.Trim();
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { ModelState.AddModelError("", "Tên danh mục đã được sử dụng."); return View(form); }
        TempData["Success"] = "Đã lưu danh mục.";
        return RedirectToAction(nameof(Index));
    }
}

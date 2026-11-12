using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.ViewModels;

namespace ShopManager.Web.Controllers;

[Authorize(Roles = Roles.Employees)]
public class ProductsController(AppDbContext db) : Controller
{
    public async Task<IActionResult> Index(string? search, int page = 1)
    {
        var query = db.Products.AsNoTracking().Include(x => x.Category).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.Name.Contains(search) || x.Sku.Contains(search));
        return View(await Paging.CreateAsync(query.OrderBy(x => x.Name), page, search));
    }
    [Authorize(Roles = Roles.Manager), HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        await Categories();
        if (!id.HasValue) return View(new ProductForm());
        var p = await db.Products.FindAsync(id.Value);
        if (p is null) return NotFound();
        return View(new ProductForm { Id = p.Id, Sku = p.Sku, Name = p.Name, CategoryId = p.CategoryId, Price = p.Price, LowStockThreshold = p.LowStockThreshold, IsActive = p.IsActive, RowVersion = Convert.ToBase64String(p.RowVersion) });
    }
    [Authorize(Roles = Roles.Manager), HttpPost]
    public async Task<IActionResult> Edit(ProductForm form)
    {
        await Categories();
        if (form.Price != decimal.Truncate(form.Price)) ModelState.AddModelError(nameof(form.Price), "Giá phải là số nguyên đồng.");
        if (!await db.Categories.AnyAsync(x => x.Id == form.CategoryId)) ModelState.AddModelError(nameof(form.CategoryId), "Chọn danh mục hợp lệ.");
        form.Sku = (form.Sku ?? "").Trim().ToUpperInvariant();
        if (await db.Products.AnyAsync(x => x.Sku == form.Sku && x.Id != form.Id)) ModelState.AddModelError(nameof(form.Sku), "Mã sản phẩm đã tồn tại.");
        if (!ModelState.IsValid) return View(form);
        var p = form.Id == 0 ? new Product() : await db.Products.FindAsync(form.Id);
        if (p is null) return NotFound();
        if (form.Id == 0) db.Products.Add(p);
        else
        {
            try { db.Entry(p).Property(x => x.RowVersion).OriginalValue = Convert.FromBase64String(form.RowVersion ?? ""); }
            catch (FormatException) { ModelState.AddModelError("", "Phiên bản dữ liệu không hợp lệ. Hãy tải lại trang."); return View(form); }
        }
        p.Sku = form.Sku; p.Name = form.Name.Trim(); p.CategoryId = form.CategoryId;
        p.Price = form.Price; p.LowStockThreshold = form.LowStockThreshold; p.IsActive = form.IsActive;
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "Sản phẩm đã được cập nhật hoặc phát sinh giao dịch. Hãy tải lại trang trước khi lưu."); return View(form); }
        catch (DbUpdateException) { ModelState.AddModelError("", "Không lưu được sản phẩm; mã sản phẩm có thể vừa được sử dụng."); return View(form); }
        TempData["Success"] = "Đã lưu sản phẩm.";
        return RedirectToAction(nameof(Index));
    }
    private async Task Categories() => ViewBag.Categories = new SelectList(await db.Categories.OrderBy(x => x.Name).ToListAsync(), "Id", "Name");
}

public static class Paging
{
    public static async Task<PagedList<T>> CreateAsync<T>(IQueryable<T> query, int page, string? search = null)
    {
        var count = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(count / 12d));
        page = Math.Clamp(page, 1, pages);
        return new PagedList<T> { Items = await query.Skip((page - 1) * 12).Take(12).ToListAsync(), Page = page, TotalPages = pages, TotalCount = count, Search = search };
    }
}

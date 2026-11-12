using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.Services;
using ShopManager.Web.ViewModels;

namespace ShopManager.Web.Controllers;

[Authorize(Roles = Roles.Manager)]
public class InventoryController(AppDbContext db, OrderService orders) : Controller
{
    public async Task<IActionResult> Index(int page = 1) => View(await Paging.CreateAsync(db.StockMovements.AsNoTracking().Include(x => x.Product).Include(x => x.CreatedBy).OrderByDescending(x => x.Id), page));
    [HttpGet]
    public async Task<IActionResult> Receive(int? productId)
    {
        await Products();
        return View(new ReceiptForm { ProductId = productId ?? 0 });
    }
    [HttpPost]
    public async Task<IActionResult> Receive(ReceiptForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                await orders.ReceiveAsync(form, User.FindFirstValue(ClaimTypes.NameIdentifier)!);
                TempData["Success"] = "Đã ghi nhận nhập kho.";
                return RedirectToAction(nameof(Index));
            }
            catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "Dữ liệu vừa thay đổi hoặc yêu cầu đã được xử lý. Kiểm tra sổ kho trước khi thử lại."); }
        }
        await Products();
        return View(form);
    }
    private async Task Products() => ViewBag.Products = new SelectList(await db.Products.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, Name = x.Sku + " · " + x.Name }).ToListAsync(), "Id", "Name");
}

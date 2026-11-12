using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.Services;
using ShopManager.Web.ViewModels;

namespace ShopManager.Web.Controllers;

[Authorize(Roles = Roles.BuyersAndEmployees)]
public class OrdersController(AppDbContext db, OrderService service, UserManager<ApplicationUser> users, MomoSandbox momo) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    public async Task<IActionResult> Index(string? search, OrderStatus? status, int page = 1)
    {
        var query = db.Orders.AsNoTracking().Include(x => x.CreatedBy).AsQueryable();
        if (!User.IsInRole(Roles.Manager)) query = query.Where(x => x.CreatedById == UserId);
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var numeric = search.Trim().ToUpperInvariant().Replace("HD", "");
            int.TryParse(numeric, out var id);
            query = query.Where(x => x.Id == id || x.CustomerName.Contains(search));
        }
        ViewBag.Status = status;
        return View(await Paging.CreateAsync(query.OrderByDescending(x => x.CreatedAt), page, search));
    }
    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Create(bool cartOnly = false, int? buyNow = null)
    {
        if (buyNow.HasValue)
        {
            if (User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(Create), new { buyNow }) });
            if (!User.IsInRole(Roles.Customer)) return Forbid();
        }
        ViewBag.CartOnly = (cartOnly || buyNow.HasValue) && !User.IsInRole(Roles.Employees);
        ViewBag.BuyNow = buyNow;
        await Catalog();
        var form = new CheckoutForm();
        if (buyNow.HasValue)
        {
            var product = ((List<Product>)ViewBag.Products).SingleOrDefault(x => x.Id == buyNow);
            if (product is null) return NotFound();
            form.Lines.Add(new CartLine { ProductId = product.Id, Quantity = 1, ExpectedPrice = product.Price });
        }
        return View(form);
    }
    [HttpPost]
    public async Task<IActionResult> Create(CheckoutForm form, bool cartOnly = false, int? buyNow = null)
    {
        ViewBag.CartOnly = (cartOnly || buyNow.HasValue) && User.IsInRole(Roles.Customer);
        ViewBag.BuyNow = buyNow;
        if (buyNow.HasValue && (!User.IsInRole(Roles.Customer)
            || form.Lines.Count != 1 || form.Lines[0].ProductId != buyNow))
            return BadRequest();
        if (ModelState.IsValid)
        {
            try
            {
                ApplicationUser? onlineCustomer = null;
                if (User.IsInRole(Roles.Customer))
                {
                    onlineCustomer = await users.GetUserAsync(User);
                    if (onlineCustomer is null) return Challenge();
                    form.CustomerId = null;
                    form.CashReceived = 0;
                    if (form.PayWithMomo && !momo.Enabled)
                        throw new BusinessException("MoMo sandbox chưa được cấu hình.");
                }
                var id = await service.CheckoutAsync(form, UserId, onlineCustomer);
                if (onlineCustomer is not null && !buyNow.HasValue)
                {
                    TempData["CompletedCart"] = form.RequestId.ToString();
                    TempData["PurchasedProducts"] = string.Join(",", form.Lines.Select(x => x.ProductId));
                }
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BusinessException ex) { ModelState.AddModelError("", ex.Message); }
            catch (DbUpdateConcurrencyException) { ModelState.AddModelError("", "Tồn kho vừa thay đổi. Đơn chưa được tạo, hãy tải lại và kiểm tra hàng."); }
            catch (DbUpdateException) { ModelState.AddModelError("", "Yêu cầu có thể đã được xử lý. Kiểm tra danh sách đơn trước khi tạo lại."); }
        }
        await Catalog();
        return View(form);
    }
    public async Task<IActionResult> Details(int id)
    {
        var order = await db.Orders.AsNoTracking().Include(x => x.Items).Include(x => x.CreatedBy).SingleOrDefaultAsync(x => x.Id == id);
        if (order is null) return NotFound();
        if (!User.IsInRole(Roles.Manager) && order.CreatedById != UserId) return Forbid();
        if (User.IsInRole(Roles.Customer) && order.Status == OrderStatus.Completed)
        {
            var ids = order.Items.Select(x => x.ProductId).ToList();
            ViewBag.ReviewedProducts = (await db.ProductReviews.Where(x => x.UserId == UserId && ids.Contains(x.ProductId))
                .Select(x => x.ProductId).ToListAsync()).ToHashSet();
        }
        return View(order);
    }
    [HttpPost, Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Review(int id, int productId, int rating, string? comment)
    {
        var canReview = await db.OrderItems.AnyAsync(x => x.SaleOrderId == id && x.ProductId == productId
            && x.SaleOrder.CreatedById == UserId && x.SaleOrder.Status == OrderStatus.Completed);
        if (!canReview) return Forbid();

        if (!ModelState.IsValid || rating is < 1 or > 5 || comment?.Length > 500)
            TempData["Error"] = "Chọn từ 1 đến 5 sao và nhập tối đa 500 ký tự.";
        else
        {
            db.ProductReviews.Add(new ProductReview
            {
                UserId = UserId, ProductId = productId, Rating = rating, Comment = comment?.Trim()
            });
            try
            {
                await db.SaveChangesAsync();
                TempData["Success"] = "Đã gửi đánh giá.";
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Không lưu được đánh giá. Mỗi tài khoản chỉ đánh giá một lần cho mỗi sản phẩm.";
            }
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [AllowAnonymous, HttpGet]
    public async Task<IActionResult> Reviews(int id, int page = 1)
    {
        var product = await db.Products.FindAsync(id);
        if (product is null) return NotFound();
        ViewBag.ProductName = product.Name;
        return View(await Paging.CreateAsync(db.ProductReviews.AsNoTracking().Where(x => x.ProductId == id)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.UserId), page));
    }

    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        try { await service.CancelAsync(id, reason, UserId); TempData["Success"] = "Đã hủy đơn và hoàn lại tồn kho. Hãy hoàn tiền mặt cho khách nếu cần."; }
        catch (BusinessException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateException) { TempData["Error"] = "Đơn hoặc tồn kho vừa thay đổi. Kiểm tra trạng thái trước khi thử lại."; }
        return RedirectToAction(nameof(Details), new { id });
    }
    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> StartDelivery(int id)
    {
        try
        {
            await service.StartDeliveryAsync(id);
            TempData["Success"] = "Đơn đã chuyển sang đang giao.";
        }
        catch (BusinessException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Trạng thái đơn vừa thay đổi. Hãy kiểm tra lại."; }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> CompleteDelivery(int id)
    {
        try
        {
            await service.CompleteDeliveryAsync(id);
            TempData["Success"] = "Đã xác nhận giao hàng thành công.";
        }
        catch (BusinessException ex) { TempData["Error"] = ex.Message; }
        catch (DbUpdateConcurrencyException) { TempData["Error"] = "Trạng thái đơn vừa thay đổi. Hãy kiểm tra lại."; }
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task Catalog()
    {
        ViewBag.MomoEnabled = momo.Enabled;
        ViewBag.Products = await db.Products.AsNoTracking().Include(x => x.Category).Where(x => x.IsActive && x.Stock > 0).OrderBy(x => x.Name).ToListAsync();
        if (!User.IsInRole(Roles.Employees))
        {
            ViewBag.UnitsSold = await db.OrderItems.Where(x => x.SaleOrder.Status == OrderStatus.Completed)
                .GroupBy(x => x.ProductId).ToDictionaryAsync(g => g.Key, g => g.Sum(x => (long)x.Quantity));
            ViewBag.Ratings = await db.ProductReviews.GroupBy(x => x.ProductId)
                .ToDictionaryAsync(g => g.Key, g => new ProductRating(g.Count(), g.Average(x => (double)x.Rating)));
            return;
        }
        ViewBag.Customers = new SelectList(await db.Customers.AsNoTracking().OrderBy(x => x.Name).Select(x => new { x.Id, Name = x.Name + " · " + x.Phone }).ToListAsync(), "Id", "Name");
    }
}

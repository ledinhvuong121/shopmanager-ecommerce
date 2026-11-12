using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.ViewModels;

namespace ShopManager.Web.Services;

public static class VietnamTime
{
    public static DateTime Today => DateTime.UtcNow.AddHours(7).Date;
    public static DateTime ToUtc(DateTime localDate) => DateTime.SpecifyKind(localDate.Date.AddHours(-7), DateTimeKind.Utc);
    public static string Format(DateTime utc) => utc.AddHours(7).ToString("dd/MM/yyyy HH:mm");
}
public class ReportService(AppDbContext db)
{
    public async Task<DashboardVm> GetAsync(DateTime from, DateTime to)
    {
        var start = VietnamTime.ToUtc(from);
        var end = VietnamTime.ToUtc(to.AddDays(1));
        var orders = db.Orders.AsNoTracking().Where(x => x.Status == OrderStatus.Completed && (x.PaidAt ?? x.CreatedAt) >= start && (x.PaidAt ?? x.CreatedAt) < end);
        return new DashboardVm
        {
            From = from, To = to,
            Revenue = await orders.SumAsync(x => x.Total), OrderCount = await orders.CountAsync(),
            AwaitingDeliveryCount = await db.Orders.CountAsync(x => x.Status == OrderStatus.AwaitingDelivery),
            ProductCount = await db.Products.CountAsync(x => x.IsActive),
            LowStock = await db.Products.AsNoTracking().Where(x => x.IsActive && x.Stock <= x.LowStockThreshold).OrderBy(x => x.Stock).Take(10).ToListAsync(),
            RecentOrders = await db.Orders.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(6).ToListAsync(),
            Days = await orders.GroupBy(x => (x.PaidAt ?? x.CreatedAt).AddHours(7).Date).OrderBy(g => g.Key).Select(g => new DailyRevenue(g.Key, g.Sum(x => x.Total))).ToListAsync(),
            TopProducts = await db.OrderItems.AsNoTracking().Where(x => x.SaleOrder.Status == OrderStatus.Completed && (x.SaleOrder.PaidAt ?? x.SaleOrder.CreatedAt) >= start && (x.SaleOrder.PaidAt ?? x.SaleOrder.CreatedAt) < end)
                .GroupBy(x => new { x.ProductId, x.Product.Name }).OrderByDescending(g => g.Sum(x => x.Quantity)).Take(5)
                .Select(g => new TopProduct(g.Key.Name, g.Sum(x => x.Quantity), g.Sum(x => x.UnitPrice * x.Quantity))).ToListAsync()
        };
    }
}

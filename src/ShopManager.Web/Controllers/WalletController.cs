using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.Services;

namespace ShopManager.Web.Controllers;

public class WalletController(AppDbContext db, MomoSandbox momo, OrderService orders) : Controller
{
    [Authorize(Roles = Roles.Customer), HttpPost]
    public async Task<IActionResult> Pay(int id, CancellationToken cancellation)
    {
        var owner = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == id && x.CreatedById == owner, cancellation);
        if (order is null || !order.PayWithMomo) return NotFound();
        if (order.Status != OrderStatus.AwaitingPayment) return RedirectToAction("Details", "Orders", new { id });

        if (momo.Enabled)
        {
            if (MomoSandbox.ValidPayUrl(order.MomoPayUrl)) return Redirect(order.MomoPayUrl!);
            try
            {
                if (!order.MomoStarted)
                {
                    order.MomoStarted = true;
                    await db.SaveChangesAsync(cancellation);
                }
                var url = await momo.CreateAsync(order.RequestId, order.Id, (long)order.Total, cancellation);
                if (url is not null)
                {
                    order.MomoPayUrl = url;
                    await db.SaveChangesAsync(cancellation);
                    return Redirect(url);
                }
            }
            catch (HttpRequestException) { }
            catch (JsonException) { }
            catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { }
            catch (DbUpdateConcurrencyException) { }
        }

        TempData["Error"] = "Chưa mở được thanh toán MoMo. Hãy kiểm tra cấu hình sandbox hoặc thử lại trên cùng đơn.";
        return RedirectToAction("Details", "Orders", new { id });
    }

    [AllowAnonymous, HttpPost, IgnoreAntiforgeryToken, RequestSizeLimit(16384)]
    public async Task<IActionResult> Notify([FromBody] MomoNotification? notification, CancellationToken cancellation)
    {
        if (!ModelState.IsValid || notification is null || !momo.Verify(notification)
            || notification.RequestId != notification.OrderId
            || !Guid.TryParseExact(notification.OrderId, "N", out var paymentId))
            return BadRequest();

        var order = await db.Orders.SingleOrDefaultAsync(x => x.RequestId == paymentId, cancellation);
        if (order is not { PayWithMomo: true, MomoStarted: true } || order.Total != notification.Amount)
            return BadRequest();

        try
        {
            if (notification.ResultCode == 0)
            {
                if (notification.TransId <= 0) return BadRequest();
                if (order.MomoTransactionId == notification.TransId) return NoContent();
                if (order.Status != OrderStatus.AwaitingPayment || order.MomoTransactionId is not null)
                    return Conflict();
                order.MomoTransactionId = notification.TransId;
                order.PaidAt = DateTime.UtcNow;
                order.Status = OrderStatus.AwaitingDelivery;
                await db.SaveChangesAsync(cancellation);
            }
            else if (notification.ResultCode is 1001 or 1002 or 1003 or 1004 or 1005 or 1006 or 1007 or 1017 or 1026
                && order.Status == OrderStatus.AwaitingPayment)
            {
                await orders.CancelAsync(order.Id, $"MoMo sandbox: giao dịch kết thúc với mã {notification.ResultCode}.",
                    order.CreatedById, momoDeclined: true);
            }
        }
        catch (BusinessException) { return Conflict(); }
        catch (DbUpdateException) { return Conflict(); }
        return NoContent();
    }
}

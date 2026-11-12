using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.ViewModels;

namespace ShopManager.Web.Services;

public class BusinessException(string message) : Exception(message);

public class OrderService(AppDbContext db)
{
    public const string SampleVoucher = "SHOP10";
    public const int VoucherPercent = 10;
    public const int VoucherLimit = 30000;

    public async Task<int> CheckoutAsync(CheckoutForm form, string userId, ApplicationUser? onlineCustomer = null)
    {
        if (form.RequestId == Guid.Empty) throw new BusinessException("Mã yêu cầu không hợp lệ. Hãy tải lại trang.");
        var previous = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == form.RequestId);
        if (previous != null)
        {
            if (previous.CreatedById != userId) throw new BusinessException("Mã yêu cầu không hợp lệ.");
            return previous.Id;
        }
        if (onlineCustomer is not null && (string.IsNullOrWhiteSpace(form.ShippingAddress) || form.ShippingAddress.Trim().Length > 300))
            throw new BusinessException("Nhập địa chỉ giao hàng từ 1 đến 300 ký tự.");
        if (form.Lines.Count is 0 or > 100 || form.Lines.Any(x => x.Quantity is <= 0 or > 1000000)
            || form.Lines.Select(x => x.ProductId).Distinct().Count() != form.Lines.Count)
            throw new BusinessException("Giỏ hàng cần 1–100 sản phẩm khác nhau với số lượng hợp lệ.");
        if (form.CashReceived != decimal.Truncate(form.CashReceived) || form.CashReceived > 1000000000000000m)
            throw new BusinessException("Tiền khách đưa phải là số nguyên đồng hợp lệ.");

        var voucher = onlineCustomer is null ? null : form.VoucherCode?.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(voucher) && voucher != SampleVoucher)
            throw new BusinessException("Mã voucher không hợp lệ.");

        await using var transaction = await db.Database.BeginTransactionAsync();
        var ids = form.Lines.Select(x => x.ProductId).ToList();
        var products = await db.Products.Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToDictionaryAsync(x => x.Id);
        var customer = form.CustomerId.HasValue ? await db.Customers.FindAsync(form.CustomerId.Value) : null;
        if (form.CustomerId.HasValue && customer == null) throw new BusinessException("Khách hàng không tồn tại.");
        var order = new SaleOrder
        {
            RequestId = form.RequestId, CreatedById = userId, CustomerId = customer?.Id,
            CustomerName = onlineCustomer?.FullName ?? customer?.Name ?? "Khách vãng lai",
            CustomerPhone = onlineCustomer?.PhoneNumber ?? customer?.Phone,
            ShippingAddress = onlineCustomer is null ? null : form.ShippingAddress!.Trim(),
            CashReceived = onlineCustomer is null ? form.CashReceived : 0,
            Status = onlineCustomer is null ? OrderStatus.Completed : OrderStatus.AwaitingDelivery
        };
        foreach (var line in form.Lines.OrderBy(x => x.ProductId))
        {
            if (!products.TryGetValue(line.ProductId, out var product) || !product.IsActive)
                throw new BusinessException("Có sản phẩm không còn kinh doanh. Hãy tải lại danh sách.");
            if (line.ExpectedPrice != product.Price) throw new BusinessException($"Giá {product.Name} đã thay đổi. Hãy tải lại trang để xác nhận giá mới.");
            if (line.Quantity > product.Stock) throw new BusinessException($"{product.Name} chỉ còn {product.Stock} sản phẩm.");
            product.Stock -= line.Quantity;
            order.Items.Add(new OrderItem { ProductId = product.Id, Sku = product.Sku, ProductName = product.Name, UnitPrice = product.Price, Quantity = line.Quantity });
            db.StockMovements.Add(new StockMovement { ProductId = product.Id, Delta = -line.Quantity, Type = MovementType.Sale, SaleOrder = order, CreatedById = userId, Note = "Bán hàng" });
        }
        var subtotal = order.Items.Sum(x => x.LineTotal);
        if (voucher == SampleVoucher)
        {
            order.VoucherCode = voucher;
            order.Discount = Math.Min(VoucherLimit, decimal.Truncate(subtotal * VoucherPercent / 100));
        }
        order.Total = subtotal - order.Discount;
        if (onlineCustomer is null && order.Total > order.CashReceived) throw new BusinessException("Tiền khách đưa chưa đủ thanh toán.");
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return order.Id;
    }

    public async Task StartDeliveryAsync(int orderId)
    {
        var order = await db.Orders.FindAsync(orderId);
        if (order is null || order.Status != OrderStatus.AwaitingDelivery)
            throw new BusinessException("Chỉ đơn chờ giao mới có thể bắt đầu giao hàng.");
        order.Status = OrderStatus.OutForDelivery;
        await db.SaveChangesAsync();
    }

    public async Task CompleteDeliveryAsync(int orderId)
    {
        var order = await db.Orders.FindAsync(orderId);
        if (order is null || order.Status != OrderStatus.OutForDelivery)
            throw new BusinessException("Chỉ xác nhận giao thành công khi đơn đang giao.");
        order.CashReceived = order.Total;
        order.PaidAt = DateTime.UtcNow;
        order.Status = OrderStatus.Completed;
        await db.SaveChangesAsync();
    }

    public async Task CancelAsync(int orderId, string reason, string userId)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500) throw new BusinessException("Nhập lý do hủy từ 1 đến 500 ký tự.");
        await using var transaction = await db.Database.BeginTransactionAsync();
        var order = await db.Orders.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == orderId)
            ?? throw new BusinessException("Không tìm thấy đơn hàng.");
        if (order.Status == OrderStatus.Cancelled) throw new BusinessException("Đơn đã hủy, không thể hoàn kho lần nữa.");
        var ids = order.Items.Select(x => x.ProductId).ToList();
        var products = await db.Products.Where(x => ids.Contains(x.Id)).OrderBy(x => x.Id).ToDictionaryAsync(x => x.Id);
        foreach (var item in order.Items.OrderBy(x => x.ProductId))
        {
            var product = products[item.ProductId];
            if (product.Stock > int.MaxValue - item.Quantity) throw new BusinessException("Tồn kho vượt giới hạn.");
            product.Stock += item.Quantity;
            db.StockMovements.Add(new StockMovement { ProductId = item.ProductId, Delta = item.Quantity, Type = MovementType.Cancellation, SaleOrderId = order.Id, CreatedById = userId, Note = reason.Trim() });
        }
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancelledById = userId;
        order.CancelReason = reason.Trim();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task ReceiveAsync(ReceiptForm form, string userId)
    {
        if (form.RequestId == Guid.Empty || form.Quantity is <= 0 or > 1000000 || string.IsNullOrWhiteSpace(form.Note) || form.Note.Length > 500)
            throw new BusinessException("Thông tin nhập kho không hợp lệ.");
        var previous = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == form.RequestId);
        if (previous != null)
        {
            if (previous.CreatedById != userId) throw new BusinessException("Mã yêu cầu không hợp lệ.");
            return;
        }
        await using var transaction = await db.Database.BeginTransactionAsync();
        var product = await db.Products.FindAsync(form.ProductId) ?? throw new BusinessException("Không tìm thấy sản phẩm.");
        if (product.Stock > int.MaxValue - form.Quantity) throw new BusinessException("Tồn kho vượt giới hạn.");
        product.Stock += form.Quantity;
        db.StockMovements.Add(new StockMovement { ProductId = product.Id, Delta = form.Quantity, Type = MovementType.Receipt, Note = form.Note.Trim(), CreatedById = userId, RequestId = form.RequestId });
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}

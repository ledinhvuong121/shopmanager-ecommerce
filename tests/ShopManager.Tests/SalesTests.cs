using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using ShopManager.Web.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ShopManager.Web.Data;
using ShopManager.Web.Models;
using ShopManager.Web.Services;
using ShopManager.Web.ViewModels;

namespace ShopManager.Tests;

// Real SQL Server is required: validates rowversion, constraints and transactional rollback.
public class SqlFixture : IAsyncLifetime
{
    private readonly string database = "ShopManager_Tests_" + Guid.NewGuid().ToString("N");
    public string UserId { get; } = Guid.NewGuid().ToString();
    public string ConnectionString => new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
        Environment.GetEnvironmentVariable("SHOPMANAGER_TEST_SQL") ?? "Server=.;Trusted_Connection=True;TrustServerCertificate=True") { InitialCatalog = database }.ConnectionString;
    public AppDbContext Open(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(ConnectionString).AddInterceptors(interceptors).Options);
    public async Task InitializeAsync()
    {
        await using var db = Open();
        await db.Database.MigrateAsync();
        db.Users.Add(new ApplicationUser { Id = UserId, UserName = "integration", FullName = "Integration test" });
        await db.SaveChangesAsync();
    }
    public async Task DisposeAsync()
    {
        // Only the unique database created by this fixture can be deleted.
        if (!database.StartsWith("ShopManager_Tests_")) throw new InvalidOperationException();
        await using var db = Open();
        await db.Database.EnsureDeletedAsync();
    }
    public async Task<Product> ProductAsync(int stock = 10)
    {
        await using var db = Open();
        var p = new Product { Sku = Guid.NewGuid().ToString("N"), Name = "Sản phẩm kiểm thử", Price = 25000, Stock = stock, Category = new Category { Name = Guid.NewGuid().ToString("N") } };
        db.Products.Add(p); await db.SaveChangesAsync(); return p;
    }
}

public class SalesTests(SqlFixture fixture) : IClassFixture<SqlFixture>
{
    private static CheckoutForm Cart(Product p, int quantity = 3) => new() { Lines = [new CartLine { ProductId = p.Id, Quantity = quantity, ExpectedPrice = p.Price }], CashReceived = 100000 };

    [Fact]
    public async Task Sale_updates_stock_total_change_and_immutable_price_snapshot()
    {
        var p = await fixture.ProductAsync();
        int id;
        await using (var db = fixture.Open()) id = await new OrderService(db).CheckoutAsync(Cart(p), fixture.UserId);
        await using (var db = fixture.Open()) { var product = await db.Products.FindAsync(p.Id); product!.Price = 40000; await db.SaveChangesAsync(); }
        await using var verify = fixture.Open();
        var order = await verify.Orders.Include(x => x.Items).SingleAsync(x => x.Id == id);
        Assert.Equal(75000, order.Total); Assert.Equal(25000, order.Change);
        Assert.Equal(25000, Assert.Single(order.Items).UnitPrice);
        Assert.Equal(7, (await verify.Products.FindAsync(p.Id))!.Stock);
        Assert.Equal(-3, (await verify.StockMovements.SingleAsync(x => x.SaleOrderId == id)).Delta);
    }

    [Fact]
    public async Task Online_order_reserves_stock_and_collects_discounted_total_after_delivery()
    {
        var product = await fixture.ProductAsync();
        await using var db = fixture.Open();
        var customer = await db.Users.FindAsync(fixture.UserId);
        var form = Cart(product, 2);
        form.ShippingAddress = "12 Nguyễn Huệ, TP. Hồ Chí Minh";
        var service = new OrderService(db);
        form.VoucherCode = "INVALID";
        await Assert.ThrowsAsync<BusinessException>(() => service.CheckoutAsync(form, fixture.UserId, customer));
        form.VoucherCode = " shop10 ";

        var id = await service.CheckoutAsync(form, fixture.UserId, customer);
        Assert.Equal(id, await service.CheckoutAsync(form, fixture.UserId, customer));
        var order = (await db.Orders.FindAsync(id))!;
        Assert.Equal(OrderStatus.AwaitingDelivery, order.Status);
        Assert.Equal(form.ShippingAddress, order.ShippingAddress);
        Assert.Equal("SHOP10", order.VoucherCode);
        Assert.Equal(5000, order.Discount);
        Assert.Equal(45000, order.Total);
        Assert.Equal(0, order.CashReceived);
        Assert.Equal(8, (await db.Products.FindAsync(product.Id))!.Stock);

        await Assert.ThrowsAsync<BusinessException>(() => service.CompleteDeliveryAsync(id));
        Assert.Equal(0, order.CashReceived);
        await service.StartDeliveryAsync(id);
        Assert.Equal(OrderStatus.OutForDelivery, order.Status);
        Assert.Null(order.PaidAt);
        await service.CompleteDeliveryAsync(id);
        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.NotNull(order.PaidAt);
        Assert.Equal(order.Total, order.CashReceived);
        Assert.Equal(8, (await db.Products.FindAsync(product.Id))!.Stock);
        await Assert.ThrowsAsync<BusinessException>(() => service.CompleteDeliveryAsync(id));
    }

    [Fact]
    public async Task Wallet_order_requires_signed_matching_notification_and_delivers_without_cash()
    {
        var product = await fixture.ProductAsync();
        await using var db = fixture.Open();
        var customer = await db.Users.FindAsync(fixture.UserId);
        var form = Cart(product, 1);
        form.ShippingAddress = "Địa chỉ kiểm thử";
        form.PayWithMomo = true;
        var service = new OrderService(db);
        var id = await service.CheckoutAsync(form, fixture.UserId, customer);
        var order = (await db.Orders.FindAsync(id))!;
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        await Assert.ThrowsAsync<BusinessException>(() => service.StartDeliveryAsync(id));
        order.MomoStarted = true;
        await db.SaveChangesAsync();

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Momo:PartnerCode"] = "TEST", ["Momo:AccessKey"] = "access",
            ["Momo:SecretKey"] = "secret", ["Momo:PublicBaseUrl"] = "https://shop.example.com"
        }).Build();
        using var client = new HttpClient();
        var controller = new WalletController(db, new MomoSandbox(client, config), service);
        var key = form.RequestId.ToString("N");
        var callback = new MomoNotification("TEST", key, key, 25000, "ShopManager", "momo_wallet",
            12345, 0, "OK", "qr", 1000, "", new string('0', 64));
        Assert.IsType<BadRequestResult>(await controller.Notify(callback, default));
        Assert.Null(order.PaidAt);

        var raw = $"accessKey=access&amount=25000&extraData=&message=OK&orderId={key}&orderInfo=ShopManager&orderType=momo_wallet&partnerCode=TEST&payType=qr&requestId={key}&responseTime=1000&resultCode=0&transId=12345";
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes("secret"), Encoding.UTF8.GetBytes(raw)));
        callback = callback with { Signature = signature };
        Assert.IsType<NoContentResult>(await controller.Notify(callback, default));
        Assert.IsType<NoContentResult>(await controller.Notify(callback, default));
        Assert.Equal(OrderStatus.AwaitingDelivery, order.Status);
        Assert.NotNull(order.PaidAt);
        await service.StartDeliveryAsync(id);
        await service.CompleteDeliveryAsync(id);
        Assert.Equal(0, order.CashReceived);
        Assert.Equal(9, (await db.Products.FindAsync(product.Id))!.Stock);
    }

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(11)]
    public async Task Invalid_quantity_or_overselling_never_changes_stock(int quantity)
    {
        var p = await fixture.ProductAsync();
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<BusinessException>(() => new OrderService(db).CheckoutAsync(Cart(p, quantity), fixture.UserId));
        await using var verify = fixture.Open();
        Assert.Equal(10, (await verify.Products.FindAsync(p.Id))!.Stock);
        Assert.False(await verify.OrderItems.AnyAsync(x => x.ProductId == p.Id));
    }

    [Fact]
    public async Task Invalid_second_line_rolls_back_entire_cart()
    {
        var p = await fixture.ProductAsync();
        var form = Cart(p); form.Lines.Add(new CartLine { ProductId = int.MaxValue, Quantity = 1, ExpectedPrice = 1 });
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<BusinessException>(() => new OrderService(db).CheckoutAsync(form, fixture.UserId));
        await using var verify = fixture.Open();
        Assert.Equal(10, (await verify.Products.FindAsync(p.Id))!.Stock);
        Assert.False(await verify.Orders.AnyAsync(x => x.RequestId == form.RequestId));
    }

    [Fact]
    public async Task Insufficient_cash_and_changed_price_are_rejected()
    {
        var p = await fixture.ProductAsync();
        var shortCash = Cart(p); shortCash.CashReceived = 1;
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<BusinessException>(() => new OrderService(db).CheckoutAsync(shortCash, fixture.UserId));
        var wrongPrice = Cart(p); wrongPrice.Lines[0].ExpectedPrice = 1;
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<BusinessException>(() => new OrderService(db).CheckoutAsync(wrongPrice, fixture.UserId));
        await using var verify = fixture.Open(); Assert.Equal(10, (await verify.Products.FindAsync(p.Id))!.Stock);
    }

    [Fact]
    public async Task Duplicate_request_returns_existing_order_without_second_stock_deduction()
    {
        var p = await fixture.ProductAsync(); var form = Cart(p); int first;
        await using (var db = fixture.Open()) first = await new OrderService(db).CheckoutAsync(form, fixture.UserId);
        await using (var db = fixture.Open()) Assert.Equal(first, await new OrderService(db).CheckoutAsync(form, fixture.UserId));
        await using var verify = fixture.Open(); Assert.Equal(7, (await verify.Products.FindAsync(p.Id))!.Stock);
        Assert.Equal(1, await verify.Orders.CountAsync(x => x.RequestId == form.RequestId));
    }

    [Fact]
    public async Task Cancellation_restocks_once_and_preserves_order()
    {
        var p = await fixture.ProductAsync(); int id;
        await using (var db = fixture.Open()) id = await new OrderService(db).CheckoutAsync(Cart(p), fixture.UserId);
        await using (var db = fixture.Open()) await new OrderService(db).CancelAsync(id, "Khách đổi ý", fixture.UserId);
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<BusinessException>(() => new OrderService(db).CancelAsync(id, "Lặp", fixture.UserId));
        await using var verify = fixture.Open(); Assert.Equal(10, (await verify.Products.FindAsync(p.Id))!.Stock);
        Assert.Equal(OrderStatus.Cancelled, (await verify.Orders.FindAsync(id))!.Status);
        Assert.Equal(1, await verify.StockMovements.CountAsync(x => x.SaleOrderId == id && x.Type == MovementType.Cancellation));
    }

    [Fact]
    public async Task Duplicate_receipt_is_idempotent()
    {
        var p = await fixture.ProductAsync(0); var form = new ReceiptForm { ProductId = p.Id, Quantity = 10, Note = "Nhập hàng" };
        await using (var db = fixture.Open()) await new OrderService(db).ReceiveAsync(form, fixture.UserId);
        await using (var db = fixture.Open()) await new OrderService(db).ReceiveAsync(form, fixture.UserId);
        await using var verify = fixture.Open(); Assert.Equal(10, (await verify.Products.FindAsync(p.Id))!.Stock);
        Assert.Equal(1, await verify.StockMovements.CountAsync(x => x.RequestId == form.RequestId));
    }

    [Fact]
    public async Task Empty_cart_duplicate_lines_and_inactive_product_are_rejected()
    {
        var p = await fixture.ProductAsync();
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<BusinessException>(() => new OrderService(db).CheckoutAsync(new CheckoutForm(), fixture.UserId));
        var form = Cart(p); form.Lines.Add(form.Lines[0]);
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<BusinessException>(() => new OrderService(db).CheckoutAsync(form, fixture.UserId));
        await using (var db = fixture.Open()) { var product = await db.Products.FindAsync(p.Id); product!.IsActive = false; await db.SaveChangesAsync(); }
        await using (var db = fixture.Open()) await Assert.ThrowsAsync<BusinessException>(() => new OrderService(db).CheckoutAsync(Cart(p), fixture.UserId));
    }

    [Fact]
    public async Task Concurrent_buyers_of_last_item_only_allow_one_sale()
    {
        var p = await fixture.ProductAsync(1);
        var barrier = new SaveBarrier();
        async Task<bool> Buy()
        {
            await using var db = fixture.Open(barrier);
            try { await new OrderService(db).CheckoutAsync(Cart(p, 1), fixture.UserId); return true; }
            catch (DbUpdateException) { return false; }
        }
        var result = await Task.WhenAll(Buy(), Buy());
        Assert.Single(result, x => x);
        await using var verify = fixture.Open(); Assert.Equal(0, (await verify.Products.FindAsync(p.Id))!.Stock);
        Assert.Equal(1, await verify.OrderItems.CountAsync(x => x.ProductId == p.Id));
    }

    [Fact]
    public async Task Concurrent_cancellations_only_restock_once()
    {
        var p = await fixture.ProductAsync(); int id;
        await using (var db = fixture.Open()) id = await new OrderService(db).CheckoutAsync(Cart(p), fixture.UserId);
        var barrier = new SaveBarrier();
        async Task<bool> Cancel()
        {
            await using var db = fixture.Open(barrier);
            try { await new OrderService(db).CancelAsync(id, "Kiểm thử đồng thời", fixture.UserId); return true; }
            catch (DbUpdateException) { return false; }
        }
        Assert.Single(await Task.WhenAll(Cancel(), Cancel()), x => x);
        await using var verify = fixture.Open(); Assert.Equal(10, (await verify.Products.FindAsync(p.Id))!.Stock);
    }

    [Fact]
    public async Task Report_uses_vietnam_date_boundaries_and_excludes_cancelled_orders()
    {
        // Isolated historical date so other tests cannot contribute to this period.
        var date = new DateTime(2001, 2, 3);
        await using (var db = fixture.Open())
        {
            db.Orders.AddRange(
                Order(VietnamTime.ToUtc(date), 100, OrderStatus.Completed),
                Order(VietnamTime.ToUtc(date.AddDays(1)).AddTicks(-1), 200, OrderStatus.Completed),
                Order(VietnamTime.ToUtc(date).AddTicks(-1), 400, OrderStatus.Completed),
                Order(VietnamTime.ToUtc(date.AddDays(1)), 800, OrderStatus.Completed),
                Order(VietnamTime.ToUtc(date), 1600, OrderStatus.Cancelled));
            await db.SaveChangesAsync();
        }
        await using var verify = fixture.Open();
        var report = await new ReportService(verify).GetAsync(date, date);
        Assert.Equal(300, report.Revenue); Assert.Equal(2, report.OrderCount);
        Assert.Equal(date, Assert.Single(report.Days).Date);
    }
    private SaleOrder Order(DateTime time, decimal total, OrderStatus status) => new() { RequestId = Guid.NewGuid(), CreatedById = fixture.UserId, CreatedAt = time, Total = total, CashReceived = total, Status = status };
}

public class SaveBarrier : SaveChangesInterceptor
{
    private int arrivals;
    private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref arrivals) == 2) gate.TrySetResult();
        await gate.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        return result;
    }
}


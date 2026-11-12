using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ShopManager.Web.Models;

public static class Roles
{
    public const string Manager = "Manager";
    public const string Staff = "Staff";
    public const string Customer = "Customer";
    public const string Employees = Manager + "," + Staff;
    public const string BuyersAndEmployees = Employees + "," + Customer;
}

public class ApplicationUser : IdentityUser
{
    [MaxLength(100)] public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class Category
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
}

public class Product
{
    public int Id { get; set; }
    [MaxLength(40)] public string Sku { get; set; } = "";
    [MaxLength(150)] public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public int LowStockThreshold { get; set; } = 5;
    public bool IsActive { get; set; } = true;
    [Timestamp] public byte[] RowVersion { get; set; } = [];
}

public class Customer
{
    public int Id { get; set; }
    [MaxLength(100)] public string Name { get; set; } = "";
    [MaxLength(20)] public string Phone { get; set; } = "";
    [MaxLength(250)] public string? Address { get; set; }
}

public enum OrderStatus { Completed, Cancelled, AwaitingDelivery, OutForDelivery, AwaitingPayment }
public enum MovementType { Receipt, Sale, Cancellation }

public class SaleOrder
{
    public int Id { get; set; }
    public string Code => $"HD{Id:D6}";
    public Guid RequestId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedById { get; set; } = "";
    public ApplicationUser CreatedBy { get; set; } = null!;
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    [MaxLength(100)] public string CustomerName { get; set; } = "Khách vãng lai";
    [MaxLength(20)] public string? CustomerPhone { get; set; }
    [MaxLength(300)] public string? ShippingAddress { get; set; }
    public DateTime? PaidAt { get; set; }
    public bool PayWithMomo { get; set; }
    public bool MomoStarted { get; set; }
    [MaxLength(2048)] public string? MomoPayUrl { get; set; }
    public long? MomoTransactionId { get; set; }
    [MaxLength(32)] public string? VoucherCode { get; set; }
    public decimal Discount { get; set; }
    public decimal Total { get; set; }
    public decimal CashReceived { get; set; }
    public decimal Change => CashReceived - Total;
    public OrderStatus Status { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledById { get; set; }
    [MaxLength(500)] public string? CancelReason { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = [];
    public List<OrderItem> Items { get; set; } = [];
}

public class OrderItem
{
    public int Id { get; set; }
    public int SaleOrderId { get; set; }
    public SaleOrder SaleOrder { get; set; } = null!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    [MaxLength(40)] public string Sku { get; set; } = "";
    [MaxLength(150)] public string ProductName { get; set; } = "";
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => UnitPrice * Quantity;
}

public class ProductReview
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int Rating { get; set; }
    [MaxLength(500)] public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StockMovement
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Delta { get; set; }
    public MovementType Type { get; set; }
    [MaxLength(500)] public string Note { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedById { get; set; } = "";
    public ApplicationUser CreatedBy { get; set; } = null!;
    public int? SaleOrderId { get; set; }
    public SaleOrder? SaleOrder { get; set; }
    public Guid? RequestId { get; set; }
}

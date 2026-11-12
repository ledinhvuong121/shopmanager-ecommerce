using System.ComponentModel.DataAnnotations;
using ShopManager.Web.Models;

namespace ShopManager.Web.ViewModels;

public class LoginForm
{
    [Required, StringLength(256), Display(Name = "Tên tài khoản hoặc email")] public string Identifier { get; set; } = "";
    [Required, DataType(DataType.Password), Display(Name = "Mật khẩu")] public string Password { get; set; } = "";
    public string? ReturnUrl { get; set; }
}
public class PasswordForm
{
    [Required, DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")] public string CurrentPassword { get; set; } = "";
    [Required, MinLength(10), DataType(DataType.Password), Display(Name = "Mật khẩu mới")] public string NewPassword { get; set; } = "";
    [Compare(nameof(NewPassword)), DataType(DataType.Password), Display(Name = "Nhập lại mật khẩu mới")] public string ConfirmPassword { get; set; } = "";
}
public class ProductForm
{
    public int Id { get; set; }
    [Required, StringLength(40), RegularExpression(@"^[A-Za-z0-9_-]+$", ErrorMessage = "Mã hàng chỉ gồm chữ, số, dấu - và _."), Display(Name = "Mã sản phẩm")] public string Sku { get; set; } = "";
    [Required, StringLength(150), Display(Name = "Tên sản phẩm")] public string Name { get; set; } = "";
    [Range(1, int.MaxValue), Display(Name = "Danh mục")] public int CategoryId { get; set; }
    [Range(typeof(decimal), "1", "1000000000"), Display(Name = "Giá bán (₫)")] public decimal Price { get; set; }
    [Range(0, 1000000), Display(Name = "Cảnh báo khi tồn ≤")] public int LowStockThreshold { get; set; } = 5;
    [Display(Name = "Đang kinh doanh")] public bool IsActive { get; set; } = true;
    public string? RowVersion { get; set; }
}
public class CustomerForm
{
    public int Id { get; set; }
    [Required, StringLength(100), Display(Name = "Tên khách hàng")] public string Name { get; set; } = "";
    [Required, RegularExpression(@"^[0-9+ ()-]{8,20}$", ErrorMessage = "Số điện thoại không hợp lệ."), Display(Name = "Số điện thoại")] public string Phone { get; set; } = "";
    [StringLength(250), Display(Name = "Địa chỉ")] public string? Address { get; set; }
}
public class CategoryForm
{
    public int Id { get; set; }
    [Required, StringLength(100), Display(Name = "Tên danh mục")] public string Name { get; set; } = "";
}
public class ReceiptForm
{
    public Guid RequestId { get; set; } = Guid.NewGuid();
    [Range(1, int.MaxValue), Display(Name = "Sản phẩm")] public int ProductId { get; set; }
    [Range(1, 1000000), Display(Name = "Số lượng nhập")] public int Quantity { get; set; } = 1;
    [Required, StringLength(500), Display(Name = "Nội dung nhập kho")] public string Note { get; set; } = "";
}
public class CartLine
{
    public int ProductId { get; set; }
    [Range(1, 1000000)] public int Quantity { get; set; }
    public decimal ExpectedPrice { get; set; }
}
public class CheckoutForm
{
    public Guid RequestId { get; set; } = Guid.NewGuid();
    public int? CustomerId { get; set; }
    [StringLength(300), Display(Name = "Địa chỉ giao hàng")] public string? ShippingAddress { get; set; }
    [Range(typeof(decimal), "0", "1000000000000000"), Display(Name = "Tiền khách đưa (₫)")] public decimal CashReceived { get; set; }
    [StringLength(32), Display(Name = "Mã voucher")] public string? VoucherCode { get; set; }
    public List<CartLine> Lines { get; set; } = [];
}
public class CustomerRegistrationForm
{
    [Required, StringLength(100), Display(Name = "Họ tên")] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(256), Display(Name = "Email")] public string Email { get; set; } = "";
    [Required, Phone, StringLength(20), Display(Name = "Số điện thoại")] public string Phone { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 10), DataType(DataType.Password), Display(Name = "Mật khẩu")] public string Password { get; set; } = "";
    [Required, Compare(nameof(Password)), DataType(DataType.Password), Display(Name = "Nhập lại mật khẩu")] public string ConfirmPassword { get; set; } = "";
}

public class StaffForm
{
    [Required, StringLength(100), Display(Name = "Họ tên")] public string FullName { get; set; } = "";
    [Required, EmailAddress, Display(Name = "Email")] public string Email { get; set; } = "";
    [Required, MinLength(10), DataType(DataType.Password), Display(Name = "Mật khẩu ban đầu")] public string Password { get; set; } = "";
}
public interface IPageModel
{
    int Page { get; }
    int TotalPages { get; }
    int TotalCount { get; }
    string? Search { get; }
}
public class PagedList<T> : IPageModel
{
    public List<T> Items { get; set; } = [];
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
    public string? Search { get; set; }
}
public class DashboardVm
{
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
    public int AwaitingDeliveryCount { get; set; }
    public int ProductCount { get; set; }
    public List<Product> LowStock { get; set; } = [];
    public List<SaleOrder> RecentOrders { get; set; } = [];
    public List<DailyRevenue> Days { get; set; } = [];
    public List<TopProduct> TopProducts { get; set; } = [];
}
public record ProductRating(int Count, double Average);
public record DailyRevenue(DateTime Date, decimal Revenue);
public record TopProduct(string Name, int Quantity, decimal Revenue);

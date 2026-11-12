using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Models;

namespace ShopManager.Web.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, bool sampleData)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var email = configuration["Seed:AdminEmail"] ?? throw new InvalidOperationException("Cần đặt Seed__AdminEmail.");
        var password = configuration["Seed:AdminPassword"] ?? throw new InvalidOperationException("Cần đặt Seed__AdminPassword (ít nhất 10 ký tự gồm chữ hoa/thường, số và ký tự đặc biệt).");
        await db.Database.MigrateAsync();
        foreach (var role in new[] { Roles.Manager, Roles.Staff, Roles.Customer })
            if (!await roles.RoleExistsAsync(role)) Ensure(await roles.CreateAsync(new IdentityRole(role)));
        var admin = await users.FindByEmailAsync(email);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = email, Email = email, FullName = "Quản lý cửa hàng", EmailConfirmed = true };
            Ensure(await users.CreateAsync(admin, password));
            Ensure(await users.AddToRoleAsync(admin, Roles.Manager));
        }
        if (!await users.IsInRoleAsync(admin, Roles.Manager)) throw new InvalidOperationException("Email khởi tạo đã thuộc tài khoản không phải quản lý.");
        if (sampleData && !await db.Products.AnyAsync())
        {
            var categories = new List<Category>();
            foreach (var name in new[] { "Đồ uống", "Thực phẩm", "Chăm sóc cá nhân", "Gia dụng" })
            {
                var category = await db.Categories.SingleOrDefaultAsync(x => x.Name == name) ?? new Category { Name = name };
                if (category.Id == 0) db.Categories.Add(category);
                categories.Add(category);
            }
            var catalog = new (string sku, string name, int category, decimal price, int stock)[]
            {
                ("DR001", "Cà phê sữa Highlands 235 ml", 0, 18000, 24),
                ("DR002", "Trà ô long Tea+ 455 ml", 0, 12000, 36),
                ("DR003", "Nước khoáng La Vie 500 ml", 0, 6000, 48),
                ("FD001", "Mì Hảo Hảo tôm chua cay", 1, 4500, 60),
                ("FD002", "Bánh Oreo 133 g", 1, 22000, 4),
                ("FD003", "Sữa tươi Vinamilk 1 lít", 1, 35000, 18),
                ("PC001", "Kem đánh răng P/S 180 g", 2, 32000, 12),
                ("PC002", "Dầu gội Dove 170 g", 2, 49000, 3),
                ("HH001", "Nước rửa chén Sunlight 750 g", 3, 31000, 16),
                ("HH002", "Khăn giấy hộp 180 tờ", 3, 19000, 2)
            };
            foreach (var p in catalog)
            {
                var product = new Product { Sku = p.sku, Name = p.name, Category = categories[p.category], Price = p.price, Stock = p.stock };
                db.Products.Add(product);
                db.StockMovements.Add(new StockMovement { Product = product, Delta = p.stock, Type = MovementType.Receipt, Note = "Nhập tồn đầu kỳ — dữ liệu mẫu", CreatedById = admin.Id, RequestId = Guid.NewGuid() });
            }
            db.Customers.AddRange(new Customer { Name = "Nguyễn An (mẫu)", Phone = "0900000001", Address = "TP. Hồ Chí Minh" }, new Customer { Name = "Trần Minh (mẫu)", Phone = "0900000002", Address = "Hà Nội" });
            await db.SaveChangesAsync();
        }
        Console.WriteLine("Đã khởi tạo ShopManager. Không thay đổi mật khẩu tài khoản đã tồn tại.");
    }
    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Models;

namespace ShopManager.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<SaleOrder> Orders => Set<SaleOrder>();
    public DbSet<ProductReview> ProductReviews => Set<ProductReview>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Product>().HasIndex(x => x.Sku).IsUnique();
        b.Entity<Product>().Property(x => x.Price).HasPrecision(18, 0);
        b.Entity<Product>().ToTable(t =>
        {
            t.HasCheckConstraint("CK_Product_Stock", "[Stock] >= 0");
            t.HasCheckConstraint("CK_Product_Price", "[Price] > 0");
        });
        b.Entity<SaleOrder>().HasIndex(x => x.RequestId).IsUnique();
        b.Entity<SaleOrder>().HasIndex(x => x.CreatedAt);
        b.Entity<SaleOrder>().HasIndex(x => x.MomoTransactionId).IsUnique();
        b.Entity<ProductReview>().HasKey(x => new { x.ProductId, x.UserId });
        b.Entity<ProductReview>().ToTable(t => t.HasCheckConstraint("CK_ProductReview_Rating", "[Rating] BETWEEN 1 AND 5"));
        b.Entity<SaleOrder>().Property(x => x.Discount).HasPrecision(18, 0);
        b.Entity<SaleOrder>().Property(x => x.Total).HasPrecision(18, 0);
        b.Entity<SaleOrder>().Property(x => x.CashReceived).HasPrecision(18, 0);
        b.Entity<OrderItem>().Property(x => x.UnitPrice).HasPrecision(18, 0);
        b.Entity<OrderItem>().ToTable(t => t.HasCheckConstraint("CK_Item_Quantity", "[Quantity] > 0"));
        b.Entity<StockMovement>().HasIndex(x => x.RequestId).IsUnique().HasFilter("[RequestId] IS NOT NULL");
        b.Entity<StockMovement>().HasIndex(x => x.CreatedAt);
        b.Entity<Customer>().HasIndex(x => x.Phone);
        foreach (var fk in b.Model.GetEntityTypes().Where(x => !x.GetTableName()!.StartsWith("AspNet"))
                     .SelectMany(x => x.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}

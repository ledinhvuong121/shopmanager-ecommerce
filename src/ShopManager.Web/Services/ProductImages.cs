namespace ShopManager.Web.Services;

public static class ProductImages
{
    public static string? Find(string sku) => SampleImages.GetValueOrDefault(sku);

    private static readonly Dictionary<string, string> SampleImages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DR001"] = "/images/products/DR001.jpg",
        ["DR002"] = "/images/products/DR002.jpg",
        ["DR003"] = "/images/products/DR003.jpg",
        ["FD001"] = "/images/products/FD001.jpg",
        ["FD002"] = "/images/products/FD002.jpg",
        ["FD003"] = "/images/products/FD003.webp",
        ["HH001"] = "/images/products/HH001.jpg",
        ["HH002"] = "/images/products/HH002.jpg",
        ["PC001"] = "/images/products/PC001.webp",
        ["PC002"] = "/images/products/PC002.png",
    };
}

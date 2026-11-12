namespace ShopManager.Web.Services;

public static class ProductImages
{
    public static string? Find(string sku) => SampleImages.GetValueOrDefault(sku);

    private static readonly Dictionary<string, string> SampleImages = new(StringComparer.OrdinalIgnoreCase)
    {
    };
}

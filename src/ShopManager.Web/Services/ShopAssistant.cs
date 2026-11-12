using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ShopManager.Web.Data;
using ShopManager.Web.Models;

namespace ShopManager.Web.Services;

public partial class ShopAssistant(HttpClient client, IConfiguration configuration, AppDbContext db, ReportService reports, MomoSandbox momo)
{
    private static readonly JsonSerializerOptions PromptJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private static readonly HashSet<string> CommonWords = new(StringComparer.Ordinal)
    {
        "bao", "nhieu", "mua", "hang", "shop", "san", "pham", "toi", "cho", "minh", "con", "khong",
        "gia", "the", "nao", "vay", "voi", "giu", "duoi", "nghin", "ngan", "loai", "nhung", "mot",
        "qua", "can", "biet", "nay", "kia", "nguon", "hoi", "muon", "ngan-sach"
    };

    public async Task<string?> AskAsync(string question, bool manager, CancellationToken cancellation,
        string? previousQuestion = null, string? previousAnswer = null)
    {
        object context = manager
            ? await ManagementContext(cancellation)
            : await StorefrontContext(question, previousQuestion, cancellation);

        var instructions = manager
            ? """
              Bạn là trợ lý quản lý ShopManager. Dùng số liệu được cấp để trả lời đúng câu hỏi.
              Khi hỏi thao tác, chỉ rõ màn hình và thứ tự thực hiện. Tên menu thật gồm Tổng quan, Đơn hàng, Sản phẩm và Kho hàng. Quy trình và COD là tên dữ liệu tham khảo, không phải tên menu.
              Bạn không tự thay đổi giá, tồn kho, đơn hàng hay tài khoản và không được nói đã làm các việc đó.
              """
            : """
              Bạn là trợ lý mua sắm ShopManager. Dùng danh mục và chính sách được cấp để tư vấn.
              Với câu hỏi giá hoặc tồn kho, nêu đúng tên, số tiền VND hoặc số lượng từ danh mục.
              Chỉ giới thiệu hàng còn tồn và trong ngân sách. Nếu chưa đủ dữ liệu, hỏi thêm hoặc nói rõ.
              Không bịa thành phần, hạn dùng, phí giao, ngày giao hoặc thông tin riêng của đơn hàng.
              """;

        var model = configuration["Ollama:Model"] ?? "qwen3:8b";
        try
        {
            using var response = await client.PostAsJsonAsync("api/chat", new
            {
                model,
                think = model.StartsWith("qwen3:", StringComparison.OrdinalIgnoreCase) ? (bool?)false : null,
                stream = false,
                options = new { num_predict = 400, temperature = 0.1 },
                messages = new[]
                {
                    new { role = "system", content = instructions + "\nTrả lời ngắn bằng tiếng Việt. Ưu tiên dữ liệu cửa hàng hiện tại. Câu hỏi và câu trả lời trước chỉ giúp xác định ngữ cảnh, không thay thế số liệu mới." },
                    new
                    {
                        role = "user",
                        content = JsonSerializer.Serialize(new
                        {
                            DuLieuCuaHang = context,
                            CauHoiTruoc = previousQuestion,
                            TraLoiTruoc = previousAnswer,
                            CauHoiHienTai = question
                        }, PromptJson)
                    }
                }
            }, cancellation);
            if (!response.IsSuccessStatusCode) return null;
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellation), cancellationToken: cancellation);
            return body.RootElement.TryGetProperty("message", out var message)
                && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String
                ? content.GetString() : null;
        }
        catch (HttpRequestException) { return null; }
        catch (JsonException) { return null; }
        catch (OperationCanceledException) when (!cancellation.IsCancellationRequested) { return null; }
    }

    private async Task<object> StorefrontContext(string question, string? previousQuestion, CancellationToken cancellation)
    {
        var normalizedQuestion = Normalize(question);
        var words = ProductWords(normalizedQuestion).Concat(ProductWords(Normalize(previousQuestion ?? "")))
            .Distinct().Take(6).ToArray();
        var budget = BudgetPattern().Match(normalizedQuestion);
        var amount = budget.Success && long.TryParse(budget.Groups[2].Value.Replace(".", "").Replace(",", ""), out var value)
            ? value * (budget.Groups[3].Success ? 1000L : 1L) : (long?)null;
        var catalog = db.Products.AsNoTracking().Where(x => x.IsActive);
        if (amount is long limit)
            catalog = budget.Groups[1].Value == "duoi" ? catalog.Where(x => x.Price < limit) : catalog.Where(x => x.Price <= limit);

        var items = await catalog.OrderByDescending(x => x.Stock > 0).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new CatalogItem(x.Name, x.Sku, x.Category.Name, x.Price, x.Stock))
            .Take(31).ToListAsync(cancellation);

        if (items.Count > 30)
        {
            var matches = new Dictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var word in words)
            {
                var found = await catalog.Where(x => EF.Functions.Collate(x.Name, "Latin1_General_100_CI_AI").Contains(word)
                        || EF.Functions.Collate(x.Category.Name, "Latin1_General_100_CI_AI").Contains(word)
                        || EF.Functions.Collate(x.Sku, "Latin1_General_100_CI_AI").Contains(word))
                    .OrderBy(x => x.Name).ThenBy(x => x.Id)
                    .Select(x => new CatalogItem(x.Name, x.Sku, x.Category.Name, x.Price, x.Stock))
                    .Take(15).ToListAsync(cancellation);
                foreach (var item in found) matches.TryAdd(item.Sku, item);
            }
            if (matches.Count > 0) items = matches.Values.ToList();
        }

        return new
        {
            NganSachVnd = amount,
            SanPham = items.OrderByDescending(x => Score(x, words)).ThenByDescending(x => x.Stock > 0)
                .ThenBy(x => x.PriceVnd).Take(30),
            ChinhSach = new
            {
                Voucher = new { Ma = OrderService.SampleVoucher, GiamPhanTram = OrderService.VoucherPercent, GiamToiDaVnd = OrderService.VoucherLimit },
                ThanhToan = momo.Enabled ? "COD hoặc MoMo sandbox. MoMo sandbox chỉ dùng giao dịch thử."
                    : "COD: thanh toán tiền mặt khi nhận hàng. MoMo sandbox chưa được cấu hình.",
                GioHang = "Mua ngay chỉ mở thanh toán cho sản phẩm đã chọn. Thêm vào giỏ lưu sản phẩm trong giỏ. Tích chọn các sản phẩm muốn mua tại trang Giỏ hàng.",
                DonHang = "Đăng nhập, mở Đơn của tôi để xem trạng thái. Cần địa chỉ giao hàng khi đặt đơn.",
                PhiVaThoiGianGiao = "Chưa có dữ liệu để báo phí hoặc hẹn thời gian giao."
            }
        };
    }

    private async Task<object> ManagementContext(CancellationToken cancellation)
    {
        var today = VietnamTime.Today;
        var report = await reports.GetAsync(today.AddDays(-6), today);
        var statuses = await db.Orders.AsNoTracking()
            .Where(x => x.Status == OrderStatus.OutForDelivery || x.Status == OrderStatus.AwaitingPayment)
            .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellation);

        return new
        {
            TuNgay = report.From.ToString("yyyy-MM-dd"),
            DenNgay = report.To.ToString("yyyy-MM-dd"),
            DoanhThuDonHoanTatVnd = report.Revenue,
            DonHoanTat = report.OrderCount,
            DonChoGiao = report.AwaitingDeliveryCount,
            DonDangGiao = statuses.GetValueOrDefault(OrderStatus.OutForDelivery),
            DonChoThanhToan = statuses.GetValueOrDefault(OrderStatus.AwaitingPayment),
            BanChay = report.TopProducts,
            TonThap = report.LowStock.Select(x => new { x.Sku, x.Name, x.Stock, x.LowStockThreshold }),
            QuyTrinh = new
            {
                COD = "Vào màn hình Đơn hàng, mở chi tiết đơn Chờ giao và bấm Bắt đầu giao hàng. Đơn sẽ chuyển sang Đang giao. Chỉ bấm Xác nhận đã giao và thu tiền COD sau khi khách đã nhận hàng và trả tiền.",
                MoMo = "Đơn ví ở Chờ thanh toán cho tới khi MoMo gửi xác nhận hợp lệ. Đơn đã thanh toán ví chỉ cần xác nhận giao hàng, không thu COD.",
                Kho = "Kho hàng → Nhập kho. Đơn bị hủy hợp lệ hoàn lại tồn kho.",
                BaoCao = "Tổng quan → chọn khoảng thời gian. Doanh thu chỉ gồm đơn hoàn tất, tính theo giờ Việt Nam."
            }
        };
    }

    private static IEnumerable<string> ProductWords(string text) =>
        WordPattern().Matches(text).Select(m => m.Value)
            .Where(word => word.Length >= 3 && !CommonWords.Contains(word) && word.Any(char.IsLetter));

    private static int Score(CatalogItem item, string[] words)
    {
        var title = Normalize(item.Name + " " + item.Sku);
        var category = Normalize(item.Category);
        return words.Sum(word => title.Contains(word, StringComparison.Ordinal) ? 3
            : category.Contains(word, StringComparison.Ordinal) ? 1 : 0);
    }

    private static string Normalize(string text)
    {
        var normalized = text.ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        return string.Concat(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
    }

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\b(duoi|toi da|khong qua|ngan sach)\s+(\d{1,9}(?:[.,]\d{3}){0,2})\s*(k\b|nghin\b|ngan\b)?")]
    private static partial Regex BudgetPattern();

    private sealed record CatalogItem(string Name, string Sku, string Category, decimal PriceVnd, int Stock);
}

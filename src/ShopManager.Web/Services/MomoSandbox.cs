using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ShopManager.Web.Services;

public class MomoSandbox(HttpClient client, IConfiguration config)
{
    private string PartnerCode => config["Momo:PartnerCode"] ?? "";
    private string AccessKey => config["Momo:AccessKey"] ?? "";
    private string SecretKey => config["Momo:SecretKey"] ?? "";
    private string PublicBaseUrl => config["Momo:PublicBaseUrl"]?.TrimEnd('/') ?? "";

    public bool Enabled => !string.IsNullOrWhiteSpace(PartnerCode) && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey)
        && Uri.TryCreate(PublicBaseUrl, UriKind.Absolute, out var url) && url.Scheme == "https"
        && !url.IsLoopback && string.IsNullOrEmpty(url.Query) && string.IsNullOrEmpty(url.Fragment);

    public async Task<string?> CreateAsync(Guid paymentId, int orderNumber, long amount, CancellationToken cancellation)
    {
        var orderId = paymentId.ToString("N");
        var requestId = orderId;
        var orderInfo = $"ShopManager HD{orderNumber:D6}";
        var redirectUrl = $"{PublicBaseUrl}/Orders/Details/{orderNumber}";
        var ipnUrl = $"{PublicBaseUrl}/Wallet/Notify";
        const string requestType = "captureWallet";
        const string extraData = "";
        var raw = FormattableString.Invariant($"accessKey={AccessKey}&amount={amount}&extraData={extraData}&ipnUrl={ipnUrl}&orderId={orderId}&orderInfo={orderInfo}&partnerCode={PartnerCode}&redirectUrl={redirectUrl}&requestId={requestId}&requestType={requestType}");
        using var response = await client.PostAsJsonAsync("v2/gateway/api/create", new
        {
            partnerCode = PartnerCode, requestId, amount, orderId, orderInfo, redirectUrl, ipnUrl,
            requestType, extraData, autoCapture = true, lang = "vi",
            signature = Convert.ToHexString(Sign(raw)).ToLowerInvariant()
        }, cancellation);
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<MomoCreateResult>(cancellationToken: cancellation);
        return result is { ResultCode: 0 } && result.OrderId == orderId && result.RequestId == requestId
            && ValidPayUrl(result.PayUrl) ? result.PayUrl : null;
    }

    public bool Verify(MomoNotification message)
    {
        if (!Enabled || message.PartnerCode != PartnerCode || message.OrderType != "momo_wallet"
            || message.Signature?.Length != 64 || message.ResultCode is null)
            return false;
        var raw = FormattableString.Invariant($"accessKey={AccessKey}&amount={message.Amount}&extraData={message.ExtraData}&message={message.Message}&orderId={message.OrderId}&orderInfo={message.OrderInfo}&orderType={message.OrderType}&partnerCode={message.PartnerCode}&payType={message.PayType}&requestId={message.RequestId}&responseTime={message.ResponseTime}&resultCode={message.ResultCode}&transId={message.TransId}");
        try { return CryptographicOperations.FixedTimeEquals(Sign(raw), Convert.FromHexString(message.Signature)); }
        catch (FormatException) { return false; }
    }

    public static bool ValidPayUrl(string? url) => url?.Length <= 2048
        && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https"
        && uri.Host == "test-payment.momo.vn" && uri.IsDefaultPort && string.IsNullOrEmpty(uri.UserInfo);

    private byte[] Sign(string value) => HMACSHA256.HashData(Encoding.UTF8.GetBytes(SecretKey), Encoding.UTF8.GetBytes(value));

    private sealed record MomoCreateResult(int? ResultCode, string? OrderId, string? RequestId, string? PayUrl);
}

public sealed record MomoNotification(
    string? PartnerCode, string? OrderId, string? RequestId, long Amount, string? OrderInfo,
    string? OrderType, long TransId, int? ResultCode, string? Message, string? PayType,
    long ResponseTime, string? ExtraData, string? Signature);

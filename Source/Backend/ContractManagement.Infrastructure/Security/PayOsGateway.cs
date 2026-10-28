using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using PayOS.Models.Webhooks;

namespace ContractManagement.API.Domains.Payments;

/// <summary>
/// Cấu hình PayOS, đọc từ section "PayOS" (appsettings.Development.json / user-secrets / biến môi trường).
/// Không đưa key vào code hay frontend.
/// </summary>
public sealed class PayOsOptions
{
    public const string SectionName = "PayOS";

    public string? ClientId { get; set; }
    public string? ApiKey { get; set; }
    public string? ChecksumKey { get; set; }

    /// <summary>Màn (5) của Frontend; PayOS redirect về đây (kèm ?subscription={publicId}) khi thanh toán xong hoặc bị hủy.</summary>
    public string ResultPageUrl { get; set; } = "http://localhost:3000/signup/result";

    /// <summary>Thời gian sống của link thanh toán; hết hạn thì giải phóng mã tổ chức.</summary>
    public int PaymentLinkExpiryMinutes { get; set; } = 15;

    public bool IsConfigured() =>
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(ChecksumKey);
}

public sealed record PayOsLink(string PaymentLinkId, string CheckoutUrl);

/// <summary>Trạng thái link; Reference là mã giao dịch ngân hàng gần nhất (nếu đã có tiền vào).</summary>
public sealed record PayOsPaymentInfo(PaymentLinkStatus Status, long AmountPaid, string? Reference);

/// <summary>Lớp mỏng quanh PayOSClient để service test được mà không gọi mạng.</summary>
public interface IPayOsGateway
{
    Task<PayOsLink> CreatePaymentLinkAsync(CreatePaymentLinkRequest request, CancellationToken cancellationToken);

    Task<PayOsPaymentInfo> GetPaymentAsync(long orderCode, CancellationToken cancellationToken);

    Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken);

    /// <summary>Kiểm tra chữ ký webhook bằng Checksum Key; ném exception nếu sai.</summary>
    Task<WebhookData> VerifyWebhookAsync(Webhook webhook);
}

public sealed class PayOsGateway : IPayOsGateway, IDisposable
{
    private readonly Lazy<PayOSClient> _client;

    public PayOsGateway(IOptions<PayOsOptions> options)
    {
        _client = new Lazy<PayOSClient>(() =>
        {
            var value = options.Value;
            if (!value.IsConfigured())
                throw new InvalidOperationException("PayOS chưa được cấu hình (PayOS:ClientId/ApiKey/ChecksumKey).");
            return new PayOSClient(new PayOSOptions
            {
                ClientId = value.ClientId, ApiKey = value.ApiKey, ChecksumKey = value.ChecksumKey
            });
        });
    }

    internal PayOSClient Client => _client.Value;

    public async Task<PayOsLink> CreatePaymentLinkAsync(CreatePaymentLinkRequest request, CancellationToken cancellationToken)
    {
        var response = await Client.PaymentRequests.CreateAsync(request);
        return new PayOsLink(response.PaymentLinkId, response.CheckoutUrl);
    }

    public async Task<PayOsPaymentInfo> GetPaymentAsync(long orderCode, CancellationToken cancellationToken)
    {
        var link = await Client.PaymentRequests.GetAsync(orderCode);
        return new PayOsPaymentInfo(link.Status, link.AmountPaid, link.Transactions?.LastOrDefault()?.Reference);
    }

    public Task CancelAsync(long orderCode, string reason, CancellationToken cancellationToken) =>
        Client.PaymentRequests.CancelAsync(orderCode, reason);

    public Task<WebhookData> VerifyWebhookAsync(Webhook webhook) => Client.Webhooks.VerifyAsync(webhook);

    public void Dispose()
    {
        if (_client.IsValueCreated) _client.Value.Dispose();
    }
}

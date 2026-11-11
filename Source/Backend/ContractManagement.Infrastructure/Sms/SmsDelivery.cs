using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Sms;

public sealed class EsmsOptions
{
    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = "https://rest.esms.vn/MainService.svc/json/";
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Brandname { get; set; } = "";
    public string SmsType { get; set; } = "2";
    public bool Sandbox { get; set; } = true;
    public string CallbackUrl { get; set; } = "";
    public string[] AllowedCallbackIps { get; set; } = [];
    /// <summary>Development only: accept callbacks from any IP.</summary>
    public bool AllowAnyCallbackIp { get; set; }
    public int TemporaryPasswordHours { get; set; } = 24;
    public string TemporaryPasswordTemplate { get; set; } =
        "ConTrack: Ban co hop dong can xem. Dang nhap {portalUrl} bang SDT {phone}, mat khau tam {password}, het han sau 24 gio.";
    public string ReviewNoticeTemplate { get; set; } = "ConTrack: Ban co hop dong {contractCode} can xem lai. Dang nhap {portalUrl} de xem.";
    public string ReviewOtpTemplate { get; set; } = "ConTrack: Ma xac nhan xem hop dong cua ban la {otp}, het han sau 5 phut.";
}

public static class SmsPurpose
{
    public const string TemporaryPassword = "CUSTOMER_TEMP_PASSWORD", ReviewNotice = "CONTRACT_REVIEW_NOTICE",
        ElectronicSigningOtp = "ELECTRONIC_SIGNING_OTP";
}

public sealed class EsmsSmsSender(HttpClient http, IOptions<EsmsOptions> options) : ISmsSender
{
    public async Task<SmsSendResult> SendAsync(string phone, string content, string requestId, CancellationToken ct)
    {
        var o = options.Value;
        var body = new Dictionary<string, string>
        {
            ["ApiKey"] = o.ApiKey, ["SecretKey"] = o.SecretKey, ["Content"] = content, ["Phone"] = phone, ["Brandname"] = o.Brandname,
            ["SmsType"] = o.SmsType, ["IsUnicode"] = "0", ["Sandbox"] = o.Sandbox ? "1" : "0", ["RequestId"] = requestId, ["CallbackUrl"] = o.CallbackUrl
        };
        using var response = await http.PostAsJsonAsync(o.BaseUrl.TrimEnd('/') + "/SendMultipleMessage_V4_post_json/", body, ct);
        if ((int)response.StatusCode >= 500) throw new HttpRequestException($"eSMS HTTP {(int)response.StatusCode}");
        if (!response.IsSuccessStatusCode) return new("HTTP" + (int)response.StatusCode, null, "eSMS từ chối yêu cầu.");
        var result = await response.Content.ReadFromJsonAsync<EsmsResponse>(ct) ?? throw new HttpRequestException("eSMS empty response");
        return new(result.CodeResult ?? "", result.SmsId, result.ErrorMessage);
    }

    private sealed record EsmsResponse(string? CodeResult, [property: JsonPropertyName("SMSID")] string? SmsId, string? ErrorMessage);
}

public sealed class FakeSmsSender(ILogger<FakeSmsSender> logger) : ISmsSender
{
    public Task<SmsSendResult> SendAsync(string phone, string content, string requestId, CancellationToken ct)
    {
        // Never log the content: it may hold a temporary password.
        logger.LogInformation("Fake SMS {RequestId} to {Phone}, {Length} characters.", requestId, PhoneNumber.Mask(phone), content.Length);
        return Task.FromResult(new SmsSendResult("100", "FAKE-" + requestId, null));
    }
}

public sealed class SmsOutboxWriter(IDataProtectionProvider protection, IOptions<EsmsOptions> options)
{
    public const string ProtectorPurpose = "ConTrack.SmsOutbox.v1";
    private readonly IDataProtector protector = protection.CreateProtector(ProtectorPurpose);

    /// <summary>Adds the row to the context; the caller saves it in its own transaction.</summary>
    public SmsOutbox Enqueue(MasterDbContext db, string purpose, long customerId, long? documentId, string phone, string content, DateTime now)
    {
        var expires = purpose == SmsPurpose.TemporaryPassword ? now.AddHours(options.Value.TemporaryPasswordHours) : now.AddHours(72);
        var row = new SmsOutbox
        {
            SmsOutboxPurpose = purpose, CustomerId = customerId, ContractDocumentId = documentId,
            SmsOutboxPhoneCiphertext = protector.Protect(Encoding.UTF8.GetBytes(phone)), SmsOutboxContentCiphertext = protector.Protect(Encoding.UTF8.GetBytes(content)),
            SmsOutboxPhoneMasked = PhoneNumber.Mask(phone), SmsOutboxDeliveryExpiresAt = expires, SmsOutboxStatus = "PENDING", SmsOutboxAttemptCount = 0,
            SmsOutboxProvider = "ESMS", SmsOutboxRequestId = Guid.NewGuid().ToString("N"), SmsOutboxBrandname = options.Value.Brandname,
            SmsOutboxIsSandbox = options.Value.Sandbox, CreatedAt = now
        };
        db.SmsOutboxes.Add(row);
        return row;
    }

    public string TemporaryPasswordMessage(string portalUrl, string phone, string password) => options.Value.TemporaryPasswordTemplate
        .Replace("{portalUrl}", portalUrl).Replace("{phone}", phone).Replace("{password}", password);
    public string ReviewNoticeMessage(string portalUrl, string contractCode) => options.Value.ReviewNoticeTemplate
        .Replace("{portalUrl}", portalUrl).Replace("{contractCode}", contractCode);

    public string Decrypt(byte[] value) => Encoding.UTF8.GetString(protector.Unprotect(value));
}

/// <summary>Leases pending SMS rows, sends them and clears ciphertext once they leave the queue. Same pattern as CustomerOtpDeliveryOutboxWorker.</summary>
public sealed class SmsOutboxWorker(IServiceScopeFactory scopes, ILogger<SmsOutboxWorker> logger) : BackgroundService
{
    internal const int MaxAttempts = 5;
    private static readonly TimeSpan[] Backoff = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnce(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception e) { logger.LogError(e, "SMS outbox cycle failed."); }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    internal async Task RunOnce(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISmsSender>();
        var writer = scope.ServiceProvider.GetRequiredService<SmsOutboxWriter>();
        var now = DateTime.UtcNow;
        var ids = await db.SmsOutboxes.AsNoTracking()
            .Where(x => (x.SmsOutboxStatus == "PENDING" && (x.SmsOutboxNextAttemptAt == null || x.SmsOutboxNextAttemptAt <= now))
                || (x.SmsOutboxStatus == "PROCESSING" && x.SmsOutboxLeaseUntil <= now))
            .OrderBy(x => x.SmsOutboxId).Select(x => x.SmsOutboxId).Take(20).ToListAsync(ct);
        foreach (var id in ids)
        {
            db.ChangeTracker.Clear();
            var row = await db.SmsOutboxes.SingleAsync(x => x.SmsOutboxId == id, ct);
            now = DateTime.UtcNow;
            if (row.SmsOutboxDeliveryExpiresAt <= now || row.SmsOutboxAttemptCount >= MaxAttempts || row.SmsOutboxPhoneCiphertext is null || row.SmsOutboxContentCiphertext is null)
            {
                Finish(row, row.SmsOutboxDeliveryExpiresAt <= now ? "EXPIRED" : "FAILED", now, "Hết hạn hoặc vượt số lần gửi.");
                await TrySave(db, ct);
                continue;
            }
            row.SmsOutboxStatus = "PROCESSING"; row.SmsOutboxLeaseId = Guid.NewGuid(); row.SmsOutboxLeaseUntil = now.AddMinutes(1); row.SmsOutboxAttemptCount++;
            if (!await TrySave(db, ct)) continue; // another worker took the lease
            try
            {
                var result = await sender.SendAsync(writer.Decrypt(row.SmsOutboxPhoneCiphertext), writer.Decrypt(row.SmsOutboxContentCiphertext), row.SmsOutboxRequestId, ct);
                row.SmsOutboxCodeResult = Truncate(result.CodeResult, 10); row.SmsOutboxProviderSmsId = Truncate(result.SmsId, 100);
                if (result.CodeResult == "100") { Finish(row, "ACCEPTED", DateTime.UtcNow, null); row.SmsOutboxSentAt = DateTime.UtcNow; }
                else Finish(row, "FAILED", DateTime.UtcNow, Truncate($"eSMS {result.CodeResult}: {result.ErrorMessage}", 1000));
                logger.LogInformation("SMS {OutboxId} {Purpose} sandbox={Sandbox}: eSMS {Code} {Error}", row.SmsOutboxId, row.SmsOutboxPurpose,
                    row.SmsOutboxIsSandbox, result.CodeResult, result.ErrorMessage);
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                now = DateTime.UtcNow;
                if (row.SmsOutboxAttemptCount >= MaxAttempts) Finish(row, "FAILED", now, "Lỗi kết nối eSMS, đã hết số lần thử.");
                else
                {
                    row.SmsOutboxStatus = "PENDING"; row.SmsOutboxLeaseId = null; row.SmsOutboxLeaseUntil = null;
                    row.SmsOutboxNextAttemptAt = now + Backoff[Math.Min(row.SmsOutboxAttemptCount - 1, Backoff.Length - 1)];
                    row.SmsOutboxLastFailure = "Lỗi kết nối eSMS, sẽ thử lại.";
                }
            }
            await TrySave(db, ct);
        }
    }

    internal static void Finish(SmsOutbox row, string status, DateTime now, string? failure)
    {
        row.SmsOutboxStatus = status; row.SmsOutboxLeaseId = null; row.SmsOutboxLeaseUntil = null; row.SmsOutboxNextAttemptAt = null;
        row.SmsOutboxPhoneCiphertext = null; row.SmsOutboxContentCiphertext = null;
        if (failure is not null) { row.SmsOutboxFailedAt = now; row.SmsOutboxLastFailure = failure; }
    }

    private static async Task<bool> TrySave(MasterDbContext db, CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); return true; }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    private static string? Truncate(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}

public sealed class SmsCallbackProcessor(MasterDbContext db)
{
    /// <summary>Stores the raw callback then applies it. Re-processing the same callback leaves the same state.</summary>
    public async Task Process(IQueryCollection query, string rawQuery, string? sourceIp, CancellationToken ct)
    {
        string? Get(string key) => query.TryGetValue(key, out var v) ? v.ToString() : null; // IQueryCollection is case-insensitive
        int? Int(string key) => int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;
        var smsId = Get("SMSID"); var requestId = Get("RequestId"); var sendStatus = Get("SendStatus");
        var row = smsId is { Length: > 0 } ? await db.SmsOutboxes.FirstOrDefaultAsync(x => x.SmsOutboxProvider == "ESMS" && x.SmsOutboxProviderSmsId == smsId, ct) : null;
        if (row is null && requestId is { Length: > 0 }) row = await db.SmsOutboxes.FirstOrDefaultAsync(x => x.SmsOutboxRequestId == requestId, ct);
        db.SmsProviderCallbacks.Add(new SmsProviderCallback
        {
            SmsOutboxId = row?.SmsOutboxId, SmsProviderCallbackProviderSmsId = Cut(smsId, 100), SmsProviderCallbackRequestId = Cut(requestId, 50),
            SmsProviderCallbackSendStatus = Cut(sendStatus, 5), SmsProviderCallbackRawQuery = Cut(rawQuery, 4000)!, SmsProviderCallbackSourceIp = Cut(sourceIp, 45),
            SmsProviderCallbackIsProcessed = row is not null, ReceivedAt = DateTime.UtcNow
        });
        if (row is not null)
        {
            var success = Int("SendSuccess"); var failed = Int("SendFailed");
            row.SmsOutboxSendStatus = Cut(sendStatus, 5); row.SmsOutboxSendSuccess = success; row.SmsOutboxSendFailed = failed;
            row.SmsOutboxTelcoId = Cut(Get("telcoid"), 10);
            if (decimal.TryParse(Get("TotalPrice"), NumberStyles.Number, CultureInfo.InvariantCulture, out var price)) row.SmsOutboxTotalPrice = price;
            if (sendStatus == "5" && success > 0) { row.SmsOutboxStatus = "DELIVERED"; row.SmsOutboxDeliveredAt ??= DateTime.UtcNow; }
            else if (sendStatus == "4" || failed > 0) { row.SmsOutboxStatus = "FAILED"; row.SmsOutboxFailedAt ??= DateTime.UtcNow; }
            row.SmsOutboxPhoneCiphertext = null; row.SmsOutboxContentCiphertext = null;
        }
        await db.SaveChangesAsync(ct);
    }

    private static string? Cut(string? value, int max) => value is null || value.Length <= max ? value : value[..max];
}

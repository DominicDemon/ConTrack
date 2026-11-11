using System.Security.Cryptography;
using System.Text;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Sms;

/// <summary>Phone check and one-time code for the review link.</summary>
// shortcut: challenges live in this process's memory, so a restart cancels pending codes; move to a table before running more than one API instance.
public sealed class ReviewLinkOtp(IMemoryCache cache, ISmsSender sms, IOptions<EsmsOptions> options, IConfiguration config, IHostEnvironment environment)
{
    public const int MaxAttempts = 5, MaxSendsPerHour = 5, MaxPhoneFailures = 5;
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5), Cooldown = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan PhoneLock = TimeSpan.FromMinutes(15);

    private sealed class Challenge(byte[] hash, DateTime sentAt) { public byte[] Hash = hash; public DateTime SentAt = sentAt; public int Attempts; }
    private sealed class Counter { public int Value; }

    public async Task Issue(ReviewLinkAccess link, string? phone, CancellationToken ct)
    {
        var failures = cache.GetOrCreate($"otp-phone-fail:{link.ReviewId}", e => { e.AbsoluteExpirationRelativeToNow = PhoneLock; return new Counter(); })!;
        if (Volatile.Read(ref failures.Value) >= MaxPhoneFailures) throw new InternalAppException(429, "Nhập sai số điện thoại quá nhiều lần. Thử lại sau 15 phút.");
        if (PhoneNumber.Normalize(phone) != link.Phone)
        {
            Interlocked.Increment(ref failures.Value);
            throw new InternalAppException(422, "Số điện thoại không khớp với số trong hợp đồng.");
        }
        var key = Key(link.ReviewId);
        var now = DateTime.UtcNow;
        if (cache.TryGetValue(key, out Challenge? previous) && now - previous!.SentAt < Cooldown) throw new InternalAppException(429, "Vui lòng chờ 60 giây rồi gửi lại mã.");
        var sends = cache.GetOrCreate($"otp-sends:{link.ReviewId}", e => { e.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1); return new Counter(); })!;
        if (Interlocked.Increment(ref sends.Value) > MaxSendsPerHour) throw new InternalAppException(429, "Đã gửi mã quá nhiều lần. Thử lại sau 1 giờ.");
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        cache.Set(key, new Challenge(Hash(link.ReviewId, code), now), Lifetime);
        if (environment.IsDevelopment() && config.GetValue<bool>("CustomerPortal:ConsoleDelivery"))
        {
            Console.WriteLine($"[DEV] OTP review {link.ReviewId} ({PhoneNumber.Mask(link.Phone)}): {code} - het han sau {Lifetime.TotalMinutes:0} phut");
            return;
        }
        var result = await sms.SendAsync(link.Phone, options.Value.ReviewOtpTemplate.Replace("{otp}", code), Guid.NewGuid().ToString("N"), ct);
        if (result.CodeResult == "100") return;
        cache.Remove(key);
        throw new InternalAppException(503, "Không gửi được mã xác nhận. Vui lòng thử lại sau.");
    }

    /// <summary>Throws unless the code matches. A code works once and dies after MaxAttempts wrong tries.</summary>
    public void Verify(long reviewId, string? code)
    {
        var key = Key(reviewId);
        if (!cache.TryGetValue(key, out Challenge? challenge)) throw new InternalAppException(422, "Mã đã hết hạn. Hãy gửi lại mã mới.");
        var attempts = Interlocked.Increment(ref challenge!.Attempts);
        if (attempts <= MaxAttempts && code is { Length: 6 } && CryptographicOperations.FixedTimeEquals(challenge.Hash, Hash(reviewId, code))) { cache.Remove(key); return; }
        if (attempts >= MaxAttempts) { cache.Remove(key); throw new InternalAppException(429, "Nhập sai quá nhiều lần. Hãy gửi lại mã mới."); }
        throw new InternalAppException(422, $"Mã không đúng. Còn {MaxAttempts - attempts} lần thử.");
    }

    private static string Key(long reviewId) => $"otp:{reviewId}";
    private static byte[] Hash(long reviewId, string code) => SHA256.HashData(Encoding.UTF8.GetBytes($"{reviewId}:{code}"));
}

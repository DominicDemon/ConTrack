using Microsoft.Extensions.Caching.Memory;

namespace ContractManagement.API.Common.Security;

/// <summary>
/// Khóa tài khoản tạm thời sau nhiều lần đăng nhập sai liên tiếp (chống dò mật khẩu theo từng tài khoản).
/// Bổ sung cho rate limit theo IP ("login" policy) – kẻ dò dùng nhiều IP vẫn bị khóa theo tài khoản.
/// shortcut: lưu trong bộ nhớ server (như session) nên restart là mở khóa và chỉ đúng khi chạy 1 instance;
/// chuyển sang cột DB / Redis khi chạy nhiều instance.
/// </summary>
public sealed class LoginThrottle
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;

    public LoginThrottle(IMemoryCache cache, TimeProvider clock)
    {
        _cache = cache;
        _clock = clock;
    }

    public static string EmployeeKey(int tenantId, string? account) =>
        $"login:employee:{tenantId}:{(account ?? string.Empty).Trim().ToLowerInvariant()}";

    public static string SystemAdminKey(string? username) =>
        $"login:system-admin:{(username ?? string.Empty).Trim().ToLowerInvariant()}";

    /// <summary>Thời gian còn bị khóa; null nếu được phép thử đăng nhập.</summary>
    public TimeSpan? GetLockoutRemaining(string key)
    {
        if (!_cache.TryGetValue(key, out State? state) || state!.LockedUntil is not { } until) return null;
        var remaining = until - _clock.GetUtcNow();
        return remaining > TimeSpan.Zero ? remaining : null;
    }

    /// <summary>Ghi một lần sai; trả true nếu lần này làm tài khoản bị khóa.</summary>
    public bool RecordFailure(string key)
    {
        var state = _cache.TryGetValue(key, out State? existing) && existing!.LockedUntil is null ? existing : new State();
        state.FailedCount++;
        if (state.FailedCount >= MaxFailedAttempts)
            state.LockedUntil = _clock.GetUtcNow() + LockoutDuration;
        // Đếm lần sai trong cửa sổ trượt: không sai thêm trong 15 phút thì quên.
        _cache.Set(key, state, LockoutDuration);
        return state.LockedUntil is not null;
    }

    public void Reset(string key) => _cache.Remove(key);

    private sealed class State
    {
        public int FailedCount { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }
}

using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;

namespace ContractManagement.API.Common.Security;

/// <summary>
/// Chống dò mật khẩu cho các endpoint đăng nhập: giới hạn theo IP (policy "login") + khóa theo tài khoản (LoginThrottle).
/// </summary>
public static class LoginRateLimit
{
    public const string PolicyName = "login";
    public const string RateLimitedCode = "LoginRateLimited";
    public const string LockedCode = "LoginLocked";

    /// <summary>Số lần gọi login tối đa mỗi IP trong một phút.</summary>
    public const int PermitPerMinute = 10;

    public static IServiceCollection AddLoginProtection(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<LoginThrottle>();
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = PermitPerMinute,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await response.WriteAsJsonAsync(new AuthorizationErrorResponse(RateLimitedCode,
                    "Bạn đã thử đăng nhập quá nhiều lần. Vui lòng thử lại sau ít phút."), cancellationToken);
            };
        });
        return services;
    }

    /// <summary>429 khi tài khoản đang bị khóa; không cho biết tài khoản có tồn tại hay không.</summary>
    public static IActionResult Locked(ControllerBase controller, TimeSpan remaining)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        controller.Response.Headers.RetryAfter = ((int)Math.Ceiling(remaining.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        return controller.StatusCode(StatusCodes.Status429TooManyRequests, new AuthorizationErrorResponse(LockedCode,
            $"Tài khoản tạm khóa do đăng nhập sai {LoginThrottle.MaxFailedAttempts} lần. Vui lòng thử lại sau {minutes} phút."));
    }

    /// <summary>Dùng khi controller được tạo ngoài DI (test); bản trong DI là singleton dùng chung.</summary>
    internal static LoginThrottle CreateStandaloneThrottle() =>
        new(new MemoryCache(new MemoryCacheOptions()), TimeProvider.System);
}

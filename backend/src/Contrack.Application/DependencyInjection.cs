using Microsoft.Extensions.DependencyInjection;

namespace Contrack.Application;

public static class DependencyInjection
{
    /// <summary>Đăng ký service của tầng Application (validator, use case) khi bắt đầu có module.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}

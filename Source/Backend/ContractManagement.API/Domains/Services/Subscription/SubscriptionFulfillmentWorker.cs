namespace ContractManagement.API.Domains.Services.Subscription;

/// <summary>
/// Chạy nền: mỗi 10 giây kích hoạt tenant cho đơn đã thanh toán, mỗi 30 giây đối soát PayOS.
/// shortcut: không có lease giữa nhiều instance API; chạy nhiều instance thì cần khóa (sp_getapplock) trước khi kích hoạt.
/// </summary>
public sealed class SubscriptionFulfillmentWorker : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(10);
    private const int ReconcileEveryTicks = 3;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SubscriptionFulfillmentWorker> _logger;

    public SubscriptionFulfillmentWorker(IServiceScopeFactory scopes, ILogger<SubscriptionFulfillmentWorker> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var tick = 0; !stoppingToken.IsCancellationRequested; tick++)
        {
            try
            {
                await using var scope = _scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<SubscriptionFulfillmentService>();
                if (tick % ReconcileEveryTicks == 0) await service.ReconcileAsync(stoppingToken);
                await service.ProvisionPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning("Subscription fulfillment scan failed: {ErrorCode}", ex.GetType().Name);
            }
            try { await Task.Delay(Tick, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}

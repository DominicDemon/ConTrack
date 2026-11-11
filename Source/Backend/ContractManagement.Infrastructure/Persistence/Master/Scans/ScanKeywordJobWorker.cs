using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>Reads the keywords of uploaded scans in the background, one job at a time.</summary>
public sealed class ScanKeywordJobWorker(IServiceScopeFactory scopes, ILogger<ScanKeywordJobWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var requeued = await scope.ServiceProvider.GetRequiredService<ScanKeywordJobRunner>().RequeueInterruptedAsync(stoppingToken);
            if (requeued > 0)
            {
                logger.LogInformation("Queued again {Count} OCR job(s) interrupted by the last shutdown", requeued);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not queue again the OCR jobs interrupted by the last shutdown");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var ranOne = false;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                ranOne = await scope.ServiceProvider.GetRequiredService<ScanKeywordJobRunner>().RunNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // The database may be down for a moment; the next loop tries again.
                logger.LogError(exception, "Background OCR of a signed scan failed");
            }

            if (!ranOne)
            {
                try
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }
}

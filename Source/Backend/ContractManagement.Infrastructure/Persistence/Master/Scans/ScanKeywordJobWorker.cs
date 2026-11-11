using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>Reads the keywords of uploaded scans in the background, one job at a time.</summary>
public sealed class ScanKeywordJobWorker(IServiceScopeFactory scopes, ILogger<ScanKeywordJobWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);

    /// <summary>How often a database without the OCR tables is looked at again (the scripts may be run meanwhile).</summary>
    private static readonly TimeSpan MissingTablesDelay = TimeSpan.FromMinutes(5);

    /// <summary>SQL Server "Invalid object name": the table is not there.</summary>
    private const int InvalidObjectName = 208;

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
        catch (Exception exception) when (IsMissingTable(exception))
        {
            // Logged below, once, by the loop.
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not queue again the OCR jobs interrupted by the last shutdown");
        }

        var tablesMissing = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            var ranOne = false;
            var delay = IdleDelay;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                ranOne = await scope.ServiceProvider.GetRequiredService<ScanKeywordJobRunner>().RunNextAsync(stoppingToken);
                if (tablesMissing)
                {
                    tablesMissing = false;
                    logger.LogInformation("The OCR tables are there now; reading uploaded scans again");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception) when (IsMissingTable(exception))
            {
                if (!tablesMissing)
                {
                    tablesMissing = true;
                    logger.LogWarning(
                        "The database has no OCR_JOB table, so scans are not read. Run Artifacts/Database/Master/006-009 to use the scan features");
                }

                delay = MissingTablesDelay;
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
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    private static bool IsMissingTable(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: InvalidObjectName })
            {
                return true;
            }
        }

        return false;
    }
}

namespace Contrack.Worker;

/// <summary>
/// Job mẫu để kiểm tra worker chạy được. Thay bằng Hangfire và các job thật
/// (CREATE_DATABASE, RUN_MIGRATION, CREATE_ADMIN, OCR, reminders) ở các iteration sau.
/// </summary>
public sealed class HeartbeatWorker(ILogger<HeartbeatWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("ConTrack worker is running at {Time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}

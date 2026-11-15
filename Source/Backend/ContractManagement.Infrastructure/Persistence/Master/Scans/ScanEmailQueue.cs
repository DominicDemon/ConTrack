using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Application.Features.Internal.Scans;
using ContractManagement.Domain.Entities.Master;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContractManagement.Infrastructure.Persistence.Master.Scans;

/// <summary>
/// Sends scan emails after the request has answered: uploading a 5 MB attachment to the mail server takes most of a
/// minute, and the employee should not wait for it. The opened file stays in memory only (it is never written to disk
/// unencrypted), so emails still waiting when the server stops are not sent; their senders see no "sent" result.
/// Also holds the hourly limit of recipients per employee, so one account cannot mail out the archive.
/// </summary>
public sealed class ScanEmailQueue(
    IEmailTransport transport,
    IServiceScopeFactory scopes,
    IOptions<ScanEmailOptions> options,
    ILogger<ScanEmailQueue> logger) : BackgroundService
{
    public const string EmailedAction = "ScanEmailed";

    private static readonly TimeSpan KeepResults = TimeSpan.FromHours(1);
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly Channel<Job> _waiting = Channel.CreateUnbounded<Job>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<string, Job> _jobs = new();
    private readonly Dictionary<long, List<DateTime>> _recent = [];

    /// <summary>Counts the recipients against the employee's hourly limit, or refuses them all.</summary>
    public void Reserve(long employeeId, int recipients)
    {
        var limit = options.Value.MaxRecipientsPerHour;
        lock (_recent)
        {
            var now = DateTime.UtcNow;
            if (!_recent.TryGetValue(employeeId, out var times))
            {
                _recent[employeeId] = times = [];
            }

            times.RemoveAll(time => now - time > Window);
            if (times.Count + recipients > limit)
            {
                var wait = times.Count == 0 ? Window : Window - (now - times[0]);
                throw new InternalAppException(429,
                    $"Mỗi giờ gửi tối đa {limit} email bản scan (bạn đã gửi {times.Count}). Thử lại sau {Math.Max(1, (int)Math.Ceiling(wait.TotalMinutes))} phút.");
            }

            times.AddRange(Enumerable.Repeat(now, recipients));
        }
    }

    public ScanEmailJobDto Enqueue(long employeeId, string entity, long entityId, string fileName, bool withNote, IReadOnlyList<EmailMessage> messages)
    {
        foreach (var old in _jobs.Values.Where(job => job.FinishedAt is { } finished && DateTime.UtcNow - finished > KeepResults).ToList())
        {
            _jobs.TryRemove(old.Id, out _);
        }

        var job = new Job(Guid.NewGuid().ToString("N"), employeeId, entity, entityId, fileName, withNote, messages);
        _jobs[job.Id] = job;
        _waiting.Writer.TryWrite(job);
        return job.ToDto();
    }

    /// <summary>The progress of a sending, for the employee who started it only.</summary>
    public ScanEmailJobDto? Find(string jobId, long employeeId) =>
        _jobs.TryGetValue(jobId, out var job) && job.EmployeeId == employeeId ? job.ToDto() : null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var job in _waiting.Reader.ReadAllAsync(stoppingToken))
        {
            for (var index = 0; index < job.Messages!.Count; index++)
            {
                try
                {
                    await transport.SendAsync(job.Messages[index], stoppingToken);
                    job.Set(index, ScanEmailStates.Sent, null);
                }
                catch (Exception exception) when (exception is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning(exception, "Sending a scan by email failed for one recipient");
                    job.Set(index, ScanEmailStates.Failed, exception is TimeoutException
                        ? "Máy chủ email không phản hồi kịp."
                        : "Máy chủ email từ chối gửi tới địa chỉ này.");
                }
            }

            job.Finish();
            await AuditAsync(job);
        }
    }

    private async Task AuditAsync(Job job)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
            var result = job.ToDto().Recipients;
            db.AuditLogs.Add(new AuditLog
            {
                ActionType = EmailedAction,
                EntityName = job.Entity,
                EntityId = job.EntityId.ToString(),
                ActorType = "Employee",
                ActorId = job.EmployeeId,
                NewData = JsonSerializer.Serialize(new
                {
                    file = job.FileName,
                    sent = result.Where(recipient => recipient.Status == ScanEmailStates.Sent).Select(recipient => recipient.Email),
                    failed = result.Where(recipient => recipient.Status == ScanEmailStates.Failed).Select(recipient => recipient.Email),
                    withNote = job.WithNote,
                }, Json),
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not write the audit row of a scan email");
        }
    }

    private sealed class Job(string id, long employeeId, string entity, long entityId, string fileName, bool withNote, IReadOnlyList<EmailMessage> messages)
    {
        private readonly (string Email, string Status, string? Reason)[] _recipients =
            messages.Select(message => (message.Recipient, ScanEmailStates.Waiting, (string?)null)).ToArray();

        public string Id => id;
        public long EmployeeId => employeeId;
        public string Entity => entity;
        public long EntityId => entityId;
        public string FileName => fileName;
        public bool WithNote => withNote;

        /// <summary>Released once sent: each message holds a copy reference to the opened file.</summary>
        public IReadOnlyList<EmailMessage>? Messages { get; private set; } = messages;

        public DateTime? FinishedAt { get; private set; }

        public void Set(int index, string status, string? reason)
        {
            lock (_recipients)
            {
                _recipients[index] = (_recipients[index].Email, status, reason);
            }
        }

        public void Finish()
        {
            Messages = null;
            FinishedAt = DateTime.UtcNow;
        }

        public ScanEmailJobDto ToDto()
        {
            lock (_recipients)
            {
                return new ScanEmailJobDto(id, FinishedAt is not null,
                    _recipients.Select(recipient => new ScanEmailRecipientDto(recipient.Email, recipient.Status, recipient.Reason)).ToList());
            }
        }
    }
}

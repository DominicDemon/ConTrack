using System.Net;
using ContractManagement.API.Domains.Email;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>A contract that ends within <see cref="ContractExpiryReminders.ReminderDays"/>: a signed contract document or a scanned paper contract.</summary>
public sealed record ExpiringContract(string Kind, long Id, string Code, string Name, DateOnly ExpireDate, int DaysLeft, long? EmployeeId, string? EmployeeName);

/// <summary>Reminds the person in charge and the board 30 and 7 days before a contract expires (in-app notification and email).</summary>
public sealed class ContractExpiryReminders(MasterDbContext db, IEmailTransport email, IConfiguration configuration, ILogger<ContractExpiryReminders> logger)
{
    public const string DocumentKind = "CONTRACT_DOCUMENT", ScannedKind = "CONTRACT";
    /// <summary>Reminder points, largest first: a contract first seen inside the smaller window gets only that reminder.</summary>
    public static readonly int[] ReminderDays = [30, 7];

    /// <summary>Vietnam has no daylight saving time, so its date is UTC+7.</summary>
    public static DateOnly Today(DateTime utcNow) => DateOnly.FromDateTime(utcNow.AddHours(7));

    public async Task<List<ExpiringContract>> Expiring(DateOnly today, long? employeeId, CancellationToken ct)
    {
        var start = today.ToDateTime(TimeOnly.MinValue);
        var until = today.AddDays(ReminderDays[0] + 1).ToDateTime(TimeOnly.MinValue);
        var documents = await (from d in db.ContractDocuments.AsNoTracking()
                               join v in db.ContractVersions.AsNoTracking() on d.ContractDocumentCurrentVersionId equals v.ContractVersionId
                               where d.TenantId == null
                                   && (d.ContractDocumentStatus == ContractDocumentStatus.Signed || d.ContractDocumentStatus == ContractDocumentStatus.Completed)
                                   && v.ContractVersionExpireDate >= start && v.ContractVersionExpireDate < until
                                   && (employeeId == null || d.EmployeeId == employeeId)
                               select new { d.ContractDocumentId, d.ContractDocumentCode, d.ContractDocumentName, Expire = v.ContractVersionExpireDate!.Value, Employee = (long?)d.EmployeeId })
            .ToListAsync(ct);
        var scanned = await db.Contracts.AsNoTracking()
            .Where(c => c.SourceType == "SCANNED" && c.ExpirationDate >= start && c.ExpirationDate < until && (employeeId == null || c.EmployeeId == employeeId))
            .Select(c => new { c.ContractId, c.ContractCode, c.ContractName, Expire = c.ExpirationDate!.Value, Employee = c.EmployeeId })
            .ToListAsync(ct);
        var owners = documents.Select(d => d.Employee).Concat(scanned.Select(c => c.Employee)).OfType<long>().Distinct().ToList();
        var names = await db.Employees.AsNoTracking().Where(e => owners.Contains(e.EmployeeId)).ToDictionaryAsync(e => e.EmployeeId, e => e.EmployeeDisplayName, ct);
        ExpiringContract Row(string kind, long id, string? code, string name, DateTime expire, long? owner)
        {
            var date = DateOnly.FromDateTime(expire);
            return new(kind, id, code ?? $"HD-{id}", name, date, date.DayNumber - today.DayNumber, owner, owner is long o ? names.GetValueOrDefault(o) : null);
        }
        return documents.Select(d => Row(DocumentKind, d.ContractDocumentId, d.ContractDocumentCode, d.ContractDocumentName, d.Expire, d.Employee))
            .Concat(scanned.Select(c => Row(ScannedKind, c.ContractId, c.ContractCode, c.ContractName, c.Expire, c.Employee)))
            .OrderBy(c => c.ExpireDate).ThenBy(c => c.Code).ToList();
    }

    /// <summary>Sends each reminder that is due and not sent yet; the NOTIFICATION event key makes a rerun or restart send nothing twice.</summary>
    public async Task<int> SendDue(DateOnly today, CancellationToken ct)
    {
        var sent = 0;
        foreach (var contract in await Expiring(today, null, ct))
        {
            var point = ReminderDays.Last(days => contract.DaysLeft <= days);
            var key = $"expiry:{contract.Kind}:{contract.Id}:{contract.ExpireDate:yyyyMMdd}:{point}";
            if (await db.Notifications.AnyAsync(n => n.NotificationEventkey == key, ct)) continue;

            var recipients = await (from er in db.EmployeeRoles
                                    join r in db.SystemRoles on er.SystemRoleId equals r.SystemRoleId
                                    join e in db.Employees on er.EmployeeId equals e.EmployeeId
                                    where r.SystemRoleCode == "INTERNAL_ADMIN" && r.SystemRoleStatus == "ACTIVE" && e.EmployeeStatus == "ACTIVE"
                                    select er.EmployeeId).ToListAsync(ct);
            if (contract.EmployeeId is long owner) recipients.Add(owner);
            recipients = recipients.Distinct().ToList();

            var title = contract.DaysLeft == 0 ? $"Hợp đồng {contract.Code} hết hạn hôm nay" : $"Hợp đồng {contract.Code} còn {contract.DaysLeft} ngày hết hạn";
            var body = $"{contract.Name} — hết hạn ngày {contract.ExpireDate:dd/MM/yyyy}.";
            var now = DateTime.UtcNow;
            var notification = new Notification
            {
                NotificationEventkey = key, NotificationModulecode = "CONTRACT", NotificationEventcode = $"EXPIRY_{point}",
                NotificationSubjecttype = contract.Kind, NotificationSubjectid = contract.Id,
                NotificationTitle = title, NotificationBody = body.Length > 1000 ? body[..1000] : body, NotificationCreatedatutc = now
            };
            db.Notifications.Add(notification);
            await db.SaveChangesAsync(ct);
            foreach (var employee in recipients)
                db.NotificationRecipients.Add(new NotificationRecipient { NotificationId = notification.NotificationId, EmployeeId = employee, NotificationRecipientCreatedatutc = now });
            await db.SaveChangesAsync(ct);
            sent++;

            // Email is best effort: the in-app notification above is the record that the reminder went out.
            var link = $"{(configuration["AccountRecovery:SystemAdminFrontendBaseUrl"] ?? "http://localhost:3001").TrimEnd('/')}/workspace/"
                + (contract.Kind == DocumentKind ? $"contracts/drafts/{contract.Id}" : "scanned-contracts");
            var addresses = await db.Employees.AsNoTracking()
                .Where(e => recipients.Contains(e.EmployeeId) && e.EmployeeEmail != null && e.EmployeeEmail != "")
                .Select(e => e.EmployeeEmail!).Distinct().ToListAsync(ct);
            foreach (var address in addresses)
            {
                try
                {
                    await email.SendAsync(new EmailMessage(address, title, $"{body}\nMở hợp đồng: {link}",
                        $"<p>{WebUtility.HtmlEncode(body)}</p><p><a href=\"{WebUtility.HtmlEncode(link)}\">Mở hợp đồng</a></p>"), ct);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "Không gửi được email nhắc hạn hợp đồng {Code} tới {Address}.", contract.Code, address);
                }
            }
        }
        return sent;
    }
}

/// <summary>Checks for due expiry reminders shortly after start and then every six hours.</summary>
public sealed class ContractExpiryReminderWorker(IServiceScopeFactory scopes, ILogger<ContractExpiryReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var sent = await scope.ServiceProvider.GetRequiredService<ContractExpiryReminders>()
                    .SendDue(ContractExpiryReminders.Today(DateTime.UtcNow), stoppingToken);
                if (sent > 0) logger.LogInformation("Đã gửi {Count} nhắc hạn hợp đồng.", sent);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogError(e, "Kiểm tra hợp đồng sắp hết hạn bị lỗi; sẽ thử lại sau.");
            }
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}

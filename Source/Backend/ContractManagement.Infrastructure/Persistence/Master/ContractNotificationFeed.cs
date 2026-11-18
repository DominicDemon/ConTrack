using ContractManagement.Application.Features.Internal;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Infrastructure.Persistence.Master;

/// <summary>One contract notification of the signed-in employee; DocumentId opens the contract to act on.</summary>
public sealed record ContractNotificationItem(long Id, string Title, string Body, string EventCode, long DocumentId, DateTime CreatedAt, DateTime? ReadAt);
public sealed record ContractNotificationPage(List<ContractNotificationItem> Items, int UnreadCount);

/// <summary>
/// The contract bell of the internal workspace: rows of NOTIFICATION_RECIPIENT for the employee, module CONTRACT
/// (sent for review, customer comments and answers, Hành chính's decisions, ready to sign, expiry reminders).
/// </summary>
public sealed class ContractNotificationFeed(MasterDbContext db)
{
    public async Task<ContractNotificationPage> List(InternalUser actor, int limit, CancellationToken ct)
    {
        var mine = from r in db.NotificationRecipients.AsNoTracking()
            join n in db.Notifications.AsNoTracking() on r.NotificationId equals n.NotificationId
            where r.EmployeeId == actor.Id && n.NotificationModulecode == "CONTRACT" && r.NotificationRecipientDismissedatutc == null
            select new { r, n };
        var items = await mine.OrderByDescending(x => x.n.NotificationCreatedatutc).ThenByDescending(x => x.r.NotificationRecipientId)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(x => new ContractNotificationItem(x.r.NotificationRecipientId, x.n.NotificationTitle, x.n.NotificationBody, x.n.NotificationEventcode,
                x.n.NotificationSubjectid, x.n.NotificationCreatedatutc, x.r.NotificationRecipientReadatutc)).ToListAsync(ct);
        return new(items, await mine.CountAsync(x => x.r.NotificationRecipientReadatutc == null, ct));
    }

    /// <summary>Marks one of the employee's notifications read (another employee's id is ignored); null id = all.</summary>
    public async Task<int> MarkRead(InternalUser actor, long? recipientId, CancellationToken ct)
    {
        var unread = await db.NotificationRecipients
            .Where(r => r.EmployeeId == actor.Id && r.NotificationRecipientReadatutc == null && (recipientId == null || r.NotificationRecipientId == recipientId)
                && db.Notifications.Any(n => n.NotificationId == r.NotificationId && n.NotificationModulecode == "CONTRACT"))
            .ToListAsync(ct);
        var now = DateTime.UtcNow;
        foreach (var row in unread) row.NotificationRecipientReadatutc = now;
        await db.SaveChangesAsync(ct);
        return unread.Count;
    }
}

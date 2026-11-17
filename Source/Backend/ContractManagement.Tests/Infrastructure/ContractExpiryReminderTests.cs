using ContractManagement.API.Domains.Email;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>Reminders 30 and 7 days before a contract expires, each sent once.</summary>
public sealed class ContractExpiryReminderTests
{
    private sealed class Mailbox : IEmailTransport
    {
        public List<EmailMessage> Sent { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) { Sent.Add(message); return Task.CompletedTask; }
    }

    private static readonly DateOnly Today = new(2026, 11, 17);

    private static void Document(MasterDbContext db, long id, int status, int daysLeft)
    {
        db.ContractDocuments.Add(new ContractDocument { ContractDocumentId = id, ContractDocumentCode = $"HD-{id}", ContractDocumentName = $"Hợp đồng {id}",
            ContractDocumentStatus = status, ContractDocumentCurrentVersionId = id, EmployeeId = 5, CustomerId = 1, RowVersion = [1] });
        db.ContractVersions.Add(new ContractVersion { ContractVersionId = id, ContractDocumentId = id, ContractVersionCurrencycode = "VND",
            ContractVersionExpireDate = Today.AddDays(daysLeft).ToDateTime(TimeOnly.MinValue), ContractVersionRowversion = [1] });
    }

    [Fact]
    public async Task SendsThe30And7DayRemindersOnceToTheOwnerAndTheBoard()
    {
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        db.Employees.AddRange(
            new Employee { EmployeeId = 5, EmployeeCode = "S", EmployeePasswordHash = "x", EmployeeDisplayName = "Sale", EmployeeUsername = "sale", EmployeeStatus = "ACTIVE", EmployeeEmail = "sale@x.vn" },
            new Employee { EmployeeId = 9, EmployeeCode = "B", EmployeePasswordHash = "x", EmployeeDisplayName = "BGĐ", EmployeeUsername = "boss", EmployeeStatus = "ACTIVE", EmployeeEmail = "boss@x.vn" });
        db.SystemRoles.Add(new SystemRole { SystemRoleId = 1, SystemRoleCode = "INTERNAL_ADMIN", SystemRoleName = "BGĐ", SystemRoleScope = "INTERNAL", SystemRoleStatus = "ACTIVE" });
        db.EmployeeRoles.Add(new EmployeeRole { EmployeeId = 9, SystemRoleId = 1 });
        Document(db, 1, ContractDocumentStatus.Signed, 20);   // inside 30 days
        Document(db, 2, ContractDocumentStatus.Signed, 5);    // inside 7 days: only the 7-day reminder
        Document(db, 3, ContractDocumentStatus.Draft, 5);     // not signed: no reminder
        Document(db, 4, ContractDocumentStatus.Signed, 45);   // too early
        db.Contracts.Add(new Contract { ContractId = 7, ContractCode = "SCAN-7", ContractName = "Hợp đồng giấy", SourceType = "SCANNED", EmployeeId = 5,
            ExpirationDate = Today.AddDays(30).ToDateTime(TimeOnly.MinValue) });
        await db.SaveChangesAsync();
        var mailbox = new Mailbox();
        var reminders = new ContractExpiryReminders(db, mailbox, new ConfigurationBuilder().Build(), NullLogger<ContractExpiryReminders>.Instance);

        Assert.Equal(3, await reminders.SendDue(Today, default));
        Assert.Equal(["expiry:CONTRACT:7:20261217:30", "expiry:CONTRACT_DOCUMENT:1:20261207:30", "expiry:CONTRACT_DOCUMENT:2:20261122:7"],
            db.Notifications.Select(n => n.NotificationEventkey).AsEnumerable().Order(StringComparer.Ordinal));
        Assert.All(db.Notifications.ToList(), n => Assert.Equal(2, db.NotificationRecipients.Count(r => r.NotificationId == n.NotificationId)));
        Assert.Equal(6, mailbox.Sent.Count);
        Assert.Contains(mailbox.Sent, m => m.Subject == "Hợp đồng HD-2 còn 5 ngày hết hạn" && m.Recipient == "sale@x.vn");

        Assert.Equal(0, await reminders.SendDue(Today, default));   // a rerun or restart sends nothing twice
        Assert.Equal(1, await reminders.SendDue(Today.AddDays(13), default)); // document 1 is now 7 days from its end
        Assert.Equal(2, (await reminders.Expiring(Today, 5, default)).Count(c => c.Kind == ContractExpiryReminders.DocumentKind));
        Assert.Empty(await reminders.Expiring(Today, 9, default));
    }
}

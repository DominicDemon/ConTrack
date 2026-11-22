using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>The contract bell: who is told, once per event, and only their own items can be read.</summary>
public sealed class ContractNotificationFeedTests
{
    private static readonly InternalUser Sale = new(5, "Sale", "sale", ["draft.write"]);
    private static readonly InternalUser Office = new(7, "Hành chính", "legal", ["contract.check"]);

    [Fact]
    public async Task NotifiesOwnerAndGivenPeopleOnceAndReadsOnlyOwnItems()
    {
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var doc = new ContractDocument { ContractDocumentId = 1, ContractDocumentCode = "HD-1", ContractDocumentName = "Hợp đồng 1", EmployeeId = Sale.Id, RowVersion = [1] };
        var now = DateTime.UtcNow;
        // Customer comment: the Sale in charge and Hành chính; the same event key is not sent twice.
        await ReviewData.Notify(db, doc, "comment:1", "CUSTOMER_COMMENT", "Khách góp ý hợp đồng", "HD-1: sửa giá", [Office.Id], now, default);
        await ReviewData.Notify(db, doc, "comment:1", "CUSTOMER_COMMENT", "Khách góp ý hợp đồng", "HD-1: sửa giá", [Office.Id], now, default);
        // Sale submits: only Hành chính, never the Sale who caused it.
        await ReviewData.Notify(db, doc, "pre-send:1", "SUBMITTED_TO_OFFICE", "Hợp đồng chờ Hành chính duyệt", "HD-1", [Office.Id, Sale.Id], now.AddSeconds(1), default,
            includeOwner: false, except: Sale.Id);

        var feed = new ContractNotificationFeed(db);
        var sale = await feed.List(Sale, 20, default);
        var office = await feed.List(Office, 20, default);
        Assert.Equal((1, 1), (sale.Items.Count, sale.UnreadCount));
        Assert.Equal(["SUBMITTED_TO_OFFICE", "CUSTOMER_COMMENT"], office.Items.Select(i => i.EventCode));
        Assert.All(office.Items, i => Assert.Equal(1, i.DocumentId));   // each item opens the contract

        // Hành chính cannot mark the Sale's item read; marking their own works, then "all".
        Assert.Equal(0, await feed.MarkRead(Office, sale.Items[0].Id, default));
        Assert.Equal(1, await feed.MarkRead(Office, office.Items[0].Id, default));
        Assert.Equal(1, (await feed.List(Office, 20, default)).UnreadCount);
        await feed.MarkRead(Office, null, default);
        Assert.Equal(0, (await feed.List(Office, 20, default)).UnreadCount);
        Assert.Equal(1, (await feed.List(Sale, 20, default)).UnreadCount);
    }
}

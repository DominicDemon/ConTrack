using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>After the customer agrees, Hành chính approves the final version and it goes to Sếp to sign (Sếp does not approve).</summary>
public sealed class FinalCheckTurnsTests
{
    private static readonly InternalUser Office = new(7, "Hành chính", "legal", ["contract.check"]);
    private static readonly InternalUser Director = new(9, "Sếp", "boss", ["contract.approve"]);

    [Fact]
    public async Task OfficeApprovesTheAgreedVersionAndDirectorOnlySigns()
    {
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        db.ContractDocuments.Add(new ContractDocument { ContractDocumentId = 1, ContractDocumentCode = "HD-1", ContractDocumentName = "Hợp đồng 1",
            ContractDocumentStatus = ContractDocumentStatus.PendingApproval, ContractDocumentCurrentVersionId = 10, EmployeeId = 5, CustomerId = 1, RowVersion = [1] });
        db.ContractReviewRequests.Add(new ContractReviewRequest { ContractReviewRequestId = 20, ContractDocumentId = 1, ContractVersionId = 10, CustomerId = 1,
            ContractReviewRequestStatus = ContractReviewStatus.Approved, ContractReviewRequestDocumentHash = "h", Rowversion = [1] });
        db.ContractApprovalRequests.Add(new ContractApprovalRequest { ContractApprovalRequestId = 30, ContractDocumentId = 1, ContractVersionId = 10,
            ApprovalWorkflowId = 1, SubmittedByEmployeeId = 5, ContractApprovalRequestStatus = 0, ContractReviewRequestId = 20, ContractApprovalRequestRowversion = [1] });
        await db.SaveChangesAsync();
        // Only the members used by the final check are needed.
        var repo = new ContractReviewRepository(db, null!, null!, new ConfigurationBuilder().Build(), null!);

        // Sếp only signs and cannot approve the agreed version.
        Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => repo.ResolveInternalApproval(1, new(true, null), Director, default))).Status);

        var approved = await repo.ResolveInternalApproval(1, new(true, "Đã đối chiếu"), Office, default);
        Assert.Equal(1, approved.Status);
        Assert.Equal(ContractDocumentStatus.PendingSignature, (await db.ContractDocuments.AsNoTracking().SingleAsync()).ContractDocumentStatus);
        // Sếp is told the contract is ready to sign.
        Assert.Contains(await db.Notifications.AsNoTracking().Select(n => n.NotificationEventcode).ToListAsync(), code => code == "READY_TO_SIGN");
    }
}

using System.Text.Json;
using ContractManagement.API.Domains.Email;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Sms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>Customer notifications without a table: derived from the contract's data, plus an email after each step.</summary>
public sealed class CustomerContractNoticeTests
{
    private sealed class Mailbox : IEmailTransport
    {
        public List<EmailMessage> Sent { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) { Sent.Add(message); return Task.CompletedTask; }
    }

    private static MasterDbContext Seed()
    {
        var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var now = DateTime.UtcNow;
        db.Customers.Add(new Customer { CustomerId = 3, CustomerDisplayName = "Khách 3", CustomerPersonalPhone = "0901234567", CustomerPasswordHash = "x", CustomerEmail = "khach@x.vn", CustomerStatus = 1 });
        db.ContractDocuments.Add(new ContractDocument { ContractDocumentId = 1, ContractDocumentCode = "HD-1", ContractDocumentName = "Hợp đồng 1", CustomerId = 3,
            EmployeeId = 5, ContractDocumentCurrentVersionId = 10, ContractDocumentStatus = ContractDocumentStatus.PendingSignature, RowVersion = [1] });
        db.ContractDynamicValues.Add(new ContractDynamicValue { ContractDocumentId = 1, ContractVersionId = 10, FieldKey = InternalRepository.SnapshotKey,
            FieldValue = JsonSerializer.Serialize(new DraftContent(), InternalRepository.Json) });
        db.ContractReviewRequests.AddRange(
            new ContractReviewRequest { ContractReviewRequestId = 20, ContractDocumentId = 1, ContractVersionId = 10, CustomerId = 3, ContractReviewRequestStatus = ContractReviewStatus.Approved,
                ContractReviewRequestDocumentHash = "h", SentAt = now.AddHours(-3), FirstViewedAt = now.AddHours(-2), Rowversion = [1] },
            new ContractReviewRequest { ContractReviewRequestId = 21, ContractDocumentId = 1, ContractVersionId = 11, CustomerId = 3, ContractReviewRequestStatus = ContractReviewStatus.Sent,
                ContractReviewRequestDocumentHash = "h", SentAt = now.AddMinutes(-5), Rowversion = [1] });
        db.ContractNegotiationComments.AddRange(
            new ContractNegotiationComment { ContractNegotiationCommentId = 30, ContractDocumentId = 1, ContractVersionId = 10, ContractReviewRequestId = 20, AuthorCustomerId = 3,
                ContractNegotiationCommentContent = "Sửa giá", ContractNegotiationCommentSource = "ExternalFeedback", CreatedAt = now.AddHours(-2), Rowversion = [1] },
            new ContractNegotiationComment { ContractNegotiationCommentId = 31, ContractDocumentId = 1, ContractVersionId = 10, ContractReviewRequestId = 20, RecordedByEmployeeId = 5,
                ContractNegotiationCommentParentcommentid = 30, ContractNegotiationCommentContent = "Đã sửa", ContractNegotiationCommentSource = "InternalNote", CreatedAt = now.AddHours(-1), Rowversion = [1] });
        db.ContractApprovalRequests.Add(new ContractApprovalRequest { ContractApprovalRequestId = 40, ContractDocumentId = 1, ContractVersionId = 10, ApprovalWorkflowId = 1,
            SubmittedByEmployeeId = 5, ContractApprovalRequestStatus = 1, ContractReviewRequestId = 20, ContractApprovalRequestResolvedAt = now.AddMinutes(-50), ContractApprovalRequestRowversion = [1] });
        db.ContractElectronicSignatures.Add(new ContractElectronicSignature { ContractElectronicSignatureId = 50, ContractDocumentId = 1, ContractVersionId = 10,
            ContractElectronicPartytype = 1, ContractElectronicStatus = 1, ContractElectronicSignedat = now.AddMinutes(-40), ContractElectronicSignername = "Sếp",
            ContractElectronicSignertype = "InternalEmployee", ContractElectronicSignaturetype = "DRAWN", ContractElectronicSignerphone = "0900000000", CreatedAt = now, Rowversion = [1] });
        db.SaveChanges();
        return db;
    }

    [Fact]
    public async Task BellListsEachStepOfTheCustomersContractsNewestFirst()
    {
        await using var db = Seed();
        var notices = await new CustomerPortalRepository(db, Options.Create(new EsmsOptions())).Notices(3, default);
        Assert.Equal(["NEW_CONTRACT", "COMPANY_SIGNED", "COMPANY_APPROVED", "STAFF_REPLY"], notices.Select(n => n.Code));
        Assert.Equal([21L, 20, 20, 20], notices.Select(n => n.ReviewId));   // each opens its contract on the portal
        Assert.Empty(await new CustomerPortalRepository(db, Options.Create(new EsmsOptions())).Notices(99, default));   // another customer sees nothing
    }

    [Fact]
    public async Task EmailGoesToTheContractsCustomerWithAPortalLink()
    {
        await using var db = Seed();
        var mailbox = new Mailbox();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["CustomerPortal:BaseUrl"] = "https://portal.test/portal" }).Build();
        var mailer = new CustomerContractMailer(db, mailbox, config, NullLogger<CustomerContractMailer>.Instance);
        // A prospect has no portal login: the email points back to the link email instead of the portal.
        Assert.True(await mailer.Send(1, "Công ty đã ký hợp đồng, mời bạn ký", "Mời bạn ký.", default, "Mời bạn ký trên link."));
        var prospect = Assert.Single(mailbox.Sent);
        Assert.Contains("Mời bạn ký trên link.", prospect.TextBody);
        Assert.DoesNotContain("https://portal.test", prospect.TextBody);
        mailbox.Sent.Clear();

        var customerId = await db.ContractDocuments.Where(d => d.ContractDocumentId == 1).Select(d => d.CustomerId).SingleAsync();
        (await db.Customers.SingleAsync(c => c.CustomerId == customerId)).CustomerPersonalPhoneVerifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        Assert.True(await mailer.Send(1, "Công ty đã ký hợp đồng, mời bạn ký", "Mời bạn ký.", default));
        var message = Assert.Single(mailbox.Sent);
        Assert.Equal("khach@x.vn", message.Recipient);
        Assert.Contains("https://portal.test/portal/contracts/21", message.TextBody);
        Assert.Contains("HD-1", message.TextBody);
        Assert.False(await mailer.Send(404, "x", "y", default));   // unknown contract: nothing sent, no exception
    }

    /// <summary>The derived queries run on SQL Server for every customer of the database.</summary>
    [MasterDatabaseFact]
    public async Task NoticesQueryRunsOnTheLiveDatabase()
    {
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")).Options);
        var portal = new CustomerPortalRepository(db, Options.Create(new EsmsOptions()));
        foreach (var customer in await db.Customers.AsNoTracking().Select(c => c.CustomerId).ToListAsync())
            Assert.All(await portal.Notices(customer, default), n => Assert.False(string.IsNullOrEmpty(n.Key)));
    }
}

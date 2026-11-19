using ContractManagement.API.Domains.Email;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using ContractManagement.Infrastructure.Sms;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ContractManagement.Tests.Infrastructure;

/// <summary>A prospect reads and signs through the link; staff create the portal account only after both sides signed.</summary>
public sealed class CustomerAccountTests
{
    private sealed class Mailbox : IEmailTransport
    {
        public List<EmailMessage> Sent { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) { Sent.Add(message); return Task.CompletedTask; }
    }

    private static readonly InternalUser Sale = new(7, "Sale", "sale", ["contract.write"]);

    [Fact]
    public async Task AccountIsCreatedOnlyAfterSigningAndEmailsPhoneAndTemporaryPassword()
    {
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var customer = new Customer { CustomerId = 3, CustomerDisplayName = "An", CustomerPersonalPhone = "0912345678", CustomerEmail = "an@example.com",
            CustomerStatus = 1, CustomerPasswordHash = "random", CreatedAt = DateTime.UtcNow };
        db.Customers.Add(customer);
        var doc = new ContractDocument { ContractDocumentId = 1, CustomerId = 3, EmployeeId = Sale.Id, ContractDocumentName = "HĐ", ContractDocumentCode = "HD-1",
            ContractDocumentStatus = ContractDocumentStatus.PendingSignature, CreatedDate = DateTime.UtcNow, RowVersion = [1] };
        db.ContractDocuments.Add(doc);
        await db.SaveChangesAsync();
        var mailbox = new Mailbox();
        var config = new ConfigurationBuilder().Build();
        var repo = new ContractReviewRepository(db, null!, null!, null!, config, null!, Options.Create(new EsmsOptions()),
            customerMail: new CustomerContractMailer(db, mailbox, config, NullLogger<CustomerContractMailer>.Instance));

        Assert.False(await ReviewData.HasAccount(db, 3, default));
        Assert.Equal("Khách tiềm năng An", await ReviewData.CustomerLabel(db, 3, default));
        // Not signed yet, and a prospect has no password to reset.
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => repo.CreateCustomerAccount(3, Sale, default))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => repo.ResetTemporaryPassword(3, Sale, default))).Status);

        doc.ContractDocumentStatus = ContractDocumentStatus.Signed;
        await db.SaveChangesAsync();
        var created = await repo.CreateCustomerAccount(3, Sale, default);
        Assert.True(created.EmailSent);
        Assert.Equal("an***@example.com", created.EmailMasked);
        Assert.Null(created.DevelopmentPassword);
        var mail = Assert.Single(mailbox.Sent);
        Assert.Equal("an@example.com", mail.Recipient);
        var password = System.Text.RegularExpressions.Regex.Match(mail.TextBody, "Mật khẩu tạm: (\\S+)").Groups[1].Value;
        Assert.Contains("0912345678", mail.TextBody);
        Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<Customer>().VerifyHashedPassword(customer, customer.CustomerPasswordHash, password));
        Assert.True(await ReviewData.HasAccount(db, 3, default));
        Assert.Equal("Khách An", await ReviewData.CustomerLabel(db, 3, default));

        // Login works with the emailed password (expiry counted from the audit row) and asks for a new password first.
        var session = await new CustomerPortalRepository(db, Options.Create(new EsmsOptions())).Login("0912345678", password, default);
        Assert.True(session!.MustChangePassword);

        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => repo.CreateCustomerAccount(3, Sale, default))).Status);
        Assert.True((await repo.ResetTemporaryPassword(3, Sale, default)).EmailSent);
        Assert.Equal(2, mailbox.Sent.Count);
        // Three issues within the hour are the limit.
        Assert.Equal(429, (await Assert.ThrowsAsync<InternalAppException>(async () =>
        { await repo.ResetTemporaryPassword(3, Sale, default); await repo.ResetTemporaryPassword(3, Sale, default); })).Status);
    }

    [Fact]
    public void LinkCanBeReissuedWhileOpenOrAgreedForSigning()
    {
        Assert.Null(ReviewData.CanReissueLink(ContractReviewStatus.Sent));
        Assert.Null(ReviewData.CanReissueLink(ContractReviewStatus.Approved));
        Assert.NotNull(ReviewData.CanReissueLink(ContractReviewStatus.ChangesRequested));
        Assert.Equal("a***@b.vn", ReviewLink.MaskEmail("a@b.vn"));
    }
}

using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using ContractManagement.Domain.Entities.Master;
using System.Text.Json;
using ContractManagement.API.Domains.Email;
using ContractManagement.Infrastructure.Sms;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Security.Cryptography;

namespace ContractManagement.Tests.Infrastructure;

[Collection("InternalMaster")]
public class InternalApprovalTests
{
    private sealed class Mailbox : IEmailTransport
    {
        public List<EmailMessage> Messages { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken ct) { Messages.Add(message); return Task.CompletedTask; }
    }
    [MasterDatabaseFact]
    public async Task EnforcesSequentialApprovalReturnLockAndSnapshotIntegrity()
    {
        var connection = Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")!;
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(connection).Options);
        var storage = new InternalTemplateStorage(Path.Combine(Path.GetTempPath(), "contrack-approval-tests"));
        var repository = new InternalRepository(db, storage);
        var word = new InternalWordTemplates(db, storage, new InternalWordDocument(new ConfigurationBuilder().Build()), new EphemeralDataProtectionProvider());
        var approvals = new InternalApprovalService(db, word);
        var progress = new ContractProgressService(db, new ContractChecklistService(db, word));
        var employee = await db.Employees.Where(e => e.EmployeeStatus == "ACTIVE").Select(e => e.EmployeeId).FirstAsync();
        var sale = new InternalUser(employee, "Sale test", "test", ["draft.write"]);
        var office = sale with { Permissions = ["contract.check", "template.manage"] };
        var director = sale with { Permissions = ["contract.approve"] };
        var templateId = await db.ContractTemplates.Select(t => t.ContractTemplateId).FirstAsync();
        var template = new TemplateDefinition(templateId, "Test approval", "test", true, [new("CUSTOMER_CONTACT_PHONE", "Điện thoại"),new("CUSTOMER_EMAIL", "Email")], [new("PAY", "Thanh toán", "Thanh toán đầy đủ")]);
        using var documentBytes = new MemoryStream();
        using (var document = WordprocessingDocument.Create(documentBytes, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            document.AddMainDocumentPart().Document = new Document(new Body(new Paragraph(new Run(
                new Text("APPROVED WORD VERSION 1 | {{CONTRACT_NUMBER}} | {{PAYMENT_METHOD}} | {{TOTAL_AMOUNT}} | {{PAYMENT_SCHEDULE}}")))));
        var approvedWord = documentBytes.ToArray();
        Directory.CreateDirectory(storage.Root);
        var wordKey = Guid.NewGuid().ToString("N") + ".docx";
        await File.WriteAllBytesAsync(Path.Combine(storage.Root, wordKey), approvedWord);
        template = template with { WordStorageKey = wordKey, WordSha256 = Convert.ToHexString(SHA256.HashData(approvedWord)), ManualWordContent = true };
        var content = new DraftContent { TemplateId = templateId, CustomerId = await db.Customers.Select(c => c.CustomerId).FirstAsync(),
            Name = "TEST — Duyệt hai cấp", PartyType = "PERSON", PaymentMethod = "BANK_TRANSFER", TemplateSnapshot = template, Terms = template.Terms,
            Items = [new(null, "Dịch vụ", 1, 100000, 0, 0)], Milestones = [new("PAY1", "Thanh toán", "PAY", 100)] };
        content.Fields["CUSTOMER_CONTACT_PHONE"] = "0901234567";
        content.Fields["CUSTOMER_EMAIL"] = "approval@example.invalid";
        DraftValidation.Prepare(content, template);
        var draft = await repository.Save(null, new(content, null, null), sale, default);
        Assert.Matches(@"^HD-\d{8}-\d{6,}$", draft.Code);
        Assert.Equal(draft.Code, draft.Content.ContractNumber);
        async Task<ApprovalAction> Input(string? note = null)
        {
            draft = (await repository.Draft(draft.Id, default))!;
            var state = await approvals.View(draft.Id, sale, default);
            return new(draft.RowVersion, draft.VersionRowVersion, state.RowVersion, note);
        }
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => approvals.RequireApproved(draft.Id,
            db.ContractDocuments.Single(d => d.ContractDocumentId == draft.Id).ContractDocumentCurrentVersionId!.Value, default))).Status);
        await approvals.Comment(draft.Id, new(draft.RowVersion, draft.VersionRowVersion, "Kiểm tra thanh toán", "PAY"), sale, default);
        await approvals.Act(draft.Id, "submit", await Input(), sale, default);
        var waiting = (await progress.List(new(Search: draft.Code, OwnerId: sale.Id, Stage: "OFFICE", Mine: true), office, default)).Items.Single(x => x.Id == draft.Id);
        draft = (await repository.Draft(draft.Id, default))!;
        await approvals.Comment(draft.Id, new(draft.RowVersion, draft.VersionRowVersion, "Nhận xét không đổi giờ chờ", "PAY"), sale, default);
        Assert.Equal(waiting.EnteredAt, (await progress.List(new(Search: draft.Code, OwnerId: sale.Id, Stage: "OFFICE", Mine: true), office, default)).Items.Single(x => x.Id == draft.Id).EnteredAt);
        var request = await Input();
        Assert.Equal("OFFICE", (await approvals.View(draft.Id, office, default)).Stage);
        Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => approvals.Act(draft.Id, "approve", request, director, default))).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => repository.Save(draft.Id, new(content, draft.RowVersion, draft.VersionRowVersion), sale, default))).Status);
        await approvals.Act(draft.Id, "return", await Input("Bổ sung nội dung"), office, default);
        Assert.False(((await repository.Draft(draft.Id, default))!).Locked);
        await approvals.Act(draft.Id, "submit", await Input(), sale, default);
        request = await Input();
        async Task<int> ConcurrentApprove()
        {
            await using var otherDb = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(connection).Options);
            var otherWord = new InternalWordTemplates(otherDb, storage, new InternalWordDocument(new ConfigurationBuilder().Build()), new EphemeralDataProtectionProvider());
            try { await new InternalApprovalService(otherDb, otherWord).Act(draft.Id, "approve", request, office, default); return 200; }
            catch (InternalAppException e) { return e.Status; }
        }
        var results = await Task.WhenAll(ConcurrentApprove(), ConcurrentApprove());
        Assert.Equal(new[] { 200, 409 }, results.Order().ToArray());
        db.ChangeTracker.Clear();
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => approvals.Act(draft.Id, "approve", request, office, default))).Status);
        Assert.Equal("DIRECTOR", (await approvals.View(draft.Id, director, default)).Stage);
        Assert.Contains((await progress.List(new(Search: draft.Code, Stage: "DIRECTOR", Mine: true), director, default)).Items, x => x.Id == draft.Id);
        await approvals.Act(draft.Id, "approve", await Input(), director, default);
        var version = await db.ContractDocuments.Where(d => d.ContractDocumentId == draft.Id).Select(d => d.ContractDocumentCurrentVersionId!.Value).SingleAsync();
        await approvals.RequireApproved(draft.Id, version, default);
        var snapshot = await db.ContractDynamicValues.SingleAsync(v => v.ContractDocumentId == draft.Id && v.ContractVersionId == version && v.FieldKey == InternalRepository.SnapshotKey);
        var original = snapshot.FieldValue;
        snapshot.FieldValue = snapshot.FieldValue.Replace("Test approval", "Changed approval");
        await db.SaveChangesAsync();
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => approvals.RequireApproved(draft.Id, version, default))).Status);
        snapshot.FieldValue = original;
        await db.SaveChangesAsync();
        Assert.Equal("APPROVED", (await approvals.View(draft.Id, sale, default)).Stage);
        var config = new ConfigurationBuilder().Build();
        var mailbox = new Mailbox();
        var reviews = new ContractReviewRepository(db, repository, new SmsOutboxWriter(new EphemeralDataProtectionProvider(), Options.Create(new EsmsOptions())),
            new ReviewLinkMailer(mailbox, config, new HostingEnvironment(), NullLogger<ReviewLinkMailer>.Instance), config, approvals);
        request = await Input();
        var sent = await reviews.SendReview(draft.Id, new(null, request.RowVersion, request.VersionRowVersion, true, DateTimeOffset.UtcNow.AddDays(1)), sale, default);
        Assert.Equal("SENT", sent.Status);
        Assert.Contains((await progress.List(new(Search: draft.Code, Stage: "CUSTOMER"), sale, default)).Items, x => x.Id == draft.Id);
        Assert.Single(mailbox.Messages);
        var delivered = InternalWordDocument.Blocks(await word.CustomerWord(sent.Id, content.CustomerId, false, default));
        Assert.Contains(delivered, block => block.Text.Contains(draft.Code) && block.Text.Contains("Chuyển khoản")
            && block.Text.Contains("100.000") && block.Text.Contains("Thanh toán: 100.000 VND"));
        var customerPdf = await word.CustomerWord(sent.Id, content.CustomerId, true, default);
        using (var pdf = UglyToad.PdfPig.PdfDocument.Open(customerPdf))
            Assert.Contains("APPROVED WORD VERSION 1", pdf.GetPage(1).Text);
        Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => word.CustomerWord(sent.Id, -1, false, default))).Status);
        var portal = new CustomerPortalRepository(db, Options.Create(new EsmsOptions()));
        var customerView = await portal.Review(content.CustomerId, sent.Id, default);
        await portal.Respond(content.CustomerId, sent.Id, new("REQUEST_CHANGES", "Sửa điều khoản", customerView.DocumentHash, customerView.RowVersion), null, null, default);
        draft = (await repository.Draft(draft.Id, default))!;
        draft = await reviews.CreateVersion(draft.Id, new(draft.RowVersion, "Theo góp ý khách"), sale, default);
        Assert.Equal(2, draft.VersionNo);
        Assert.Equal(draft.Code, draft.Content.ContractNumber);
        Assert.Equal("DRAFT", (await approvals.View(draft.Id, sale, default)).Stage);
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => reviews.SendReview(draft.Id, new(null, draft.RowVersion, draft.VersionRowVersion, true, DateTimeOffset.UtcNow.AddDays(1)), sale, default))).Status);
        await approvals.Act(draft.Id, "submit", await Input(), sale, default);
        await approvals.Act(draft.Id, "approve", await Input(), office, default);
        await approvals.Act(draft.Id, "approve", await Input(), director, default);
        request = await Input();
        var resent = await reviews.SendReview(draft.Id, new(null, request.RowVersion, request.VersionRowVersion, true, DateTimeOffset.UtcNow.AddDays(1)), sale, default);
        Assert.Equal("SENT", resent.Status);
        Assert.Equal(2, mailbox.Messages.Count);
        var currentCustomerView = await portal.Review(content.CustomerId, resent.Id, default);
        await portal.Respond(content.CustomerId, resent.Id, new("APPROVE", null, currentCustomerView.DocumentHash, currentCustomerView.RowVersion), null, null, default);
        var finalCheck = await reviews.InternalApproval(draft.Id, director, default);
        Assert.NotNull(finalCheck);
        Assert.Equal(0, finalCheck.Status);
        Assert.Contains((await progress.List(new(Search: draft.Code, Stage: "FINAL_CHECK", Mine: true), director, default)).Items, row => row.Id == draft.Id);
        Assert.Equal(409, (await Assert.ThrowsAsync<InternalAppException>(() => reviews.SendReview(draft.Id, new(null, request.RowVersion, request.VersionRowVersion, true, DateTimeOffset.UtcNow.AddDays(1)), sale, default))).Status);
        Assert.Equal(2, mailbox.Messages.Count);
    }

    [MasterDatabaseFact]
    public async Task PrepareLocalThreeRoleFixtureWhenRequested()
    {
        var target = Environment.GetEnvironmentVariable("CONTRACK_APPROVAL_FIXTURE");
        if (string.IsNullOrEmpty(target)) return;
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>().UseSqlServer(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")).Options);
        var accounts = new Dictionary<string, object>();
        foreach (var (key, role) in new[] { ("sale", "INTERNAL_SALE"), ("office", "INTERNAL_LEGAL"), ("director", "INTERNAL_ADMIN") })
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var password = EmployeeAdminRules.TemporaryPassword();
            var employee = new Employee { EmployeeCode = "TEST_" + suffix, EmployeeUsername = "test." + key + "." + suffix,
                EmployeeDisplayName = "TEST " + key, EmployeeStatus = "ACTIVE", CreatedAt = DateTime.UtcNow };
            employee.EmployeePasswordHash = new PasswordHasher<Employee>().HashPassword(employee, password);
            db.Employees.Add(employee); await db.SaveChangesAsync();
            db.EmployeeRoles.Add(new() { EmployeeId = employee.EmployeeId, SystemRoleId = await db.SystemRoles.Where(r => r.SystemRoleCode == role).Select(r => r.SystemRoleId).SingleAsync(), CreatedAt = DateTime.UtcNow, SystemMemberRoleAssignedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            accounts[key] = new { username = employee.EmployeeUsername, password };
        }
        await File.WriteAllTextAsync(target, JsonSerializer.Serialize(accounts));
    }
}

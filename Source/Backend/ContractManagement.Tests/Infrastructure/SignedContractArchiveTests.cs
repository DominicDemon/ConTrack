using System.Security.Cryptography;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal.Scans;
using ContractManagement.Infrastructure.Persistence.Master;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace ContractManagement.Tests.Infrastructure;

public sealed class SignedContractArchiveTests
{
    private static InternalUser Sale(long id) => new(id, "Sale", "sale", ["draft.write"]);
    private static CustomerSigningService Service(MasterDbContext db, string root) =>
        new(db, new InternalTemplateStorage(Path.Combine(root, "templates")), null!, null!,
            new EphemeralDataProtectionProvider(), null!, null!, null!);

    [MasterDatabaseFact]
    public async Task ExistingSignedPdfsMatchCustomerPortalOnMasterDatabase()
    {
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")).Options);
        var templateRoot = Environment.GetEnvironmentVariable("CONTRACK_INTERNAL_TEMPLATE_ROOT")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../private-storage/internal-templates"));
        var service = Service(db, Path.GetDirectoryName(templateRoot)!);
        var portal = new CustomerPortalRepository(db, Microsoft.Extensions.Options.Options.Create(new ContractManagement.Infrastructure.Sms.EsmsOptions()));
        var office = new InternalUser(0, "Office", "office", ["contract.check"]);
        var director = new InternalUser(0, "Director", "director", ["contract.approve"]);
        var page = await service.SignedContracts(office, null, 1, default);
        foreach (var row in page.Items.Where(row => !row.PaperSigned))
        {
            var doc = await db.ContractDocuments.AsNoTracking().SingleAsync(d => d.ContractDocumentId == row.DocumentId);
            var review = await db.ContractReviewRequests.AsNoTracking().FirstAsync(r => r.ContractDocumentId == row.DocumentId
                && r.ContractVersionId == row.VersionId && r.CustomerId == doc.CustomerId);
            Assert.True((await portal.Reviews(doc.CustomerId, default)).Single(r => r.Id == review.ContractReviewRequestId).IsSigned);
            Assert.True((await portal.Review(doc.CustomerId, review.ContractReviewRequestId, default)).IsSigned);
            var expected = SHA256.HashData(await service.PortalFinalDocument(doc.CustomerId, review.ContractReviewRequestId, default));
            foreach (var user in new[] { office, director, Sale(doc.EmployeeId) })
            {
                var bytes = await service.InternalFinalDocument(user, row.DocumentId, row.VersionId, default);
                Assert.Equal(expected, SHA256.HashData(bytes));
            }
            Assert.DoesNotContain((await service.SignedContracts(Sale(-1), row.Code, 1, default)).Items, r => r.DocumentId == doc.ContractDocumentId);
            Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => service.InternalFinalDocument(Sale(-1), row.DocumentId, row.VersionId, default))).Status);
        }
    }

    [Fact]
    public async Task ArchiveRequiresBothPartiesOnSameVersionAndScopesEveryRead()
    {
        await using var db = new MasterDbContext(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var root = Directory.CreateTempSubdirectory();
        try
        {
            var signing = Service(db, root.FullName);
            for (var id = 1; id <= 2; id++)
            {
                db.Employees.Add(new Employee { EmployeeId = id, EmployeeDisplayName = $"Sale {id}", EmployeeUsername = $"sale{id}",
                    EmployeeCode = $"S{id}", EmployeeStatus = "ACTIVE", EmployeePasswordHash = "x" });
                db.Customers.Add(new Customer { CustomerId = id, CustomerDisplayName = $"Khách {id}", CustomerPersonalPhone = $"090000000{id}", CustomerPasswordHash = "x" });
                db.ContractDocuments.Add(new ContractDocument { ContractDocumentId = id, EmployeeId = id, CustomerId = id,
                    ContractDocumentCode = $"HD-{id}", ContractDocumentName = $"Hợp đồng {id}", RowVersion = [1] });
                db.ContractVersions.Add(new ContractVersion { ContractVersionId = id, ContractDocumentId = id, ContractVersionVersionNo = 1,
                    ContractVersionCurrencycode = "VND", ContractVersionRowversion = [1] });
                AddSignature(id, 1);
            }
            AddSignature(1, 2);
            await db.SaveChangesAsync();
            Assert.Empty((await signing.SignedContracts(Sale(2), null, 1, default)).Items);
            var office = new InternalUser(3, "Office", "office", ["contract.check"]);
            var director = new InternalUser(4, "Director", "director", ["contract.approve"]);
            Assert.Single((await signing.SignedContracts(director, null, 1, default)).Items);
            var paper = new ContractDocumentFile { ContractDocumentFileId = 2, ContractDocumentId = 2, ContractVersionId = 2,
                ContractDocumentFileRole = ContractDocumentFileRoles.SignedPaper };
            var evidence = new ContractSignedEvidence { ContractDocumentFileId = 2, Rowversion = [1],
                ContractSignedEvidenceStatus = (int)SignedEvidenceStatus.PendingReview };
            db.AddRange(paper, evidence, new ContractReviewRequest { ContractReviewRequestId = 2, ContractDocumentId = 2, ContractVersionId = 2,
                CustomerId = 2, ContractReviewRequestDocumentHash = "test", ContractReviewRequestStatus = "APPROVED", Rowversion = [1] });
            await db.SaveChangesAsync();
            var portal = new CustomerPortalRepository(db, Microsoft.Extensions.Options.Options.Create(new ContractManagement.Infrastructure.Sms.EsmsOptions()));
            Assert.False(Assert.Single(await portal.Reviews(2, default)).IsSigned);
            Assert.Empty(await portal.Reviews(1, default));
            Assert.Empty((await signing.SignedContracts(Sale(2), null, 1, default)).Items);
            evidence.ContractSignedEvidenceStatus = (int)SignedEvidenceStatus.Accepted;
            await db.SaveChangesAsync();
            Assert.True(Assert.Single(await portal.Reviews(2, default)).IsSigned);
            Assert.True(Assert.Single((await signing.SignedContracts(Sale(2), null, 1, default)).Items).PaperSigned);
            Assert.Empty(await signing.SignedPayments(Sale(2), 2, 2, default));
            Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => signing.SignedPayments(Sale(1), 2, 2, default))).Status);
            db.RemoveRange(evidence, paper);
            await db.SaveChangesAsync();
            AddSignature(2, 2);
            await db.SaveChangesAsync();
            foreach (var user in new[] { office, director })
                Assert.Equal(2, (await signing.SignedContracts(user, null, 1, default)).Items.Count);
            foreach (var id in new[] { 1, 2 })
                Assert.Equal(id, Assert.Single((await signing.SignedContracts(Sale(id), null, 1, default)).Items).DocumentId);
            Assert.Empty((await signing.SignedContracts(Sale(2), "HD-1", 1, default)).Items);
            Assert.Equal(2L, Assert.Single((await signing.SignedContracts(office, "Khách 2", 1, default)).Items).DocumentId);
            var denied = await Assert.ThrowsAsync<InternalAppException>(() => signing.InternalFinalDocument(Sale(2), 1, 1, default));
            var missing = await Assert.ThrowsAsync<InternalAppException>(() => signing.InternalFinalDocument(Sale(2), 999, 999, default));
            Assert.Equal(404, denied.Status);
            Assert.Equal((missing.Status, missing.Message), (denied.Status, denied.Message));
            Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => signing.InternalFinalDocument(office, 1, 2, default))).Status);
            Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => signing.InternalFinalDocument(office, 1, 1, default))).Status);
            var bytes = "%PDF-1.7 test final"u8.ToArray();
            var dir = Directory.CreateDirectory(Path.Combine(root.FullName, "signing"));
            await File.WriteAllBytesAsync(Path.Combine(dir.FullName, "final.pdf"), bytes);
            var file = new FileStorage { FileStorageFileid = 1, FileStorageObjecttype = "CONTRACT_SIGNING", FileStorageFiletype = "FULLY_SIGNED_PDF",
                FileStorageFilename = "final.pdf", FileStorageFilepath = "final.pdf", FileStorageStoragekey = "final.pdf",
                FileStorageContenttype = "application/pdf", FileStorageSha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
            db.FileStorages.Add(file);
            db.ContractDocumentFiles.Add(new ContractDocumentFile { ContractDocumentId = 1, ContractVersionId = 1, FileStorageFileid = 1, IsPrimary = true });
            db.ContractReviewRequests.Add(new ContractReviewRequest { ContractReviewRequestId = 1, ContractDocumentId = 1, ContractVersionId = 1,
                CustomerId = 1, ContractReviewRequestDocumentHash = "test", ContractReviewRequestStatus = "APPROVED", Rowversion = [1] });
            await db.SaveChangesAsync();
            var customerPdf = await signing.PortalFinalDocument(1, 1, default);
            foreach (var user in new[] { Sale(1), office, director })
                Assert.Equal(customerPdf, await signing.InternalFinalDocument(user, 1, 1, default));
            file.FileStorageFiletype = "COMPANY_SIGNED_PDF_REFORMATTED";
            await db.SaveChangesAsync();
            Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => signing.InternalFinalDocument(office, 1, 1, default))).Status);
        }
        finally { root.Delete(true); }

        void AddSignature(int id, int party) => db.ContractElectronicSignatures.Add(new ContractElectronicSignature {
            ContractDocumentId = id, ContractVersionId = id, ContractElectronicPartytype = party, ContractElectronicStatus = 1,
            ContractElectronicSignedat = DateTime.UtcNow, ContractElectronicSignername = "Signer", ContractElectronicSignertype = "Test",
            ContractElectronicSignaturetype = "DRAWN", ContractElectronicSignerphone = "0900000000", Rowversion = [1] });
    }

    [MasterDatabaseFact]
    public async Task SignedPaymentConfirmationUpdatesOnlyItsVersionAndNotifiesSaleOnce()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseSqlServer(Environment.GetEnvironmentVariable("CONTRACK_MASTER_TEST_CONNECTION")).Options;
        await using var db = new MasterDbContext(options);
        var seller = await db.Employees.Select(e => e.EmployeeId).FirstAsync();
        var sale = Sale(seller);
        var office = new InternalUser(seller, "Office", "office", ["contract.check"]);
        var director = new InternalUser(seller, "Director", "director", ["contract.approve"]);
        var code = $"TEST-PAY-{Guid.NewGuid():N}";
        var doc = new ContractDocument
        {
            EmployeeId = seller, CustomerId = await db.Customers.Select(c => c.CustomerId).FirstAsync(),
            ContractTemplateId = await db.ContractTemplates.Select(t => t.ContractTemplateId).FirstAsync(),
            ContractDocumentCode = code, ContractDocumentName = "Kiểm thử xác nhận thanh toán",
            ContractDocumentType = 1, CreatedDate = DateTime.UtcNow
        };
        db.ContractDocuments.Add(doc);
        await db.SaveChangesAsync();
        var id = doc.ContractDocumentId;
        var service = Service(db, Path.GetTempPath());
        try
        {
            async Task<long> Version(int n, int parties)
            {
                var v = new ContractVersion { ContractDocumentId = id, ContractTemplateId = doc.ContractTemplateId,
                    ContractVersionVersionNo = n, ContractVersionCurrencycode = "VND", ContractVersionIslocked = true, ContractVersionCreatedDate = DateTime.UtcNow };
                db.ContractVersions.Add(v);
                await db.SaveChangesAsync();
                var version = v.ContractVersionId;
                foreach (var party in Enumerable.Range(1, parties))
                    db.ContractElectronicSignatures.Add(new ContractElectronicSignature { ContractDocumentId = id, ContractVersionId = version,
                        ContractElectronicPartytype = party, ContractElectronicStatus = 1, ContractElectronicSignedat = DateTime.UtcNow,
                        ContractElectronicSignername = "Test", ContractElectronicSignertype = party == 1 ? "InternalEmployee" : "ExternalCustomer",
                        ContractElectronicSignaturetype = "DRAWN", ContractElectronicSignerphone = "0900000000", CreatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync();
                return version;
            }
            async Task<long[]> Plan(long version, int count)
            {
                var term = new ContractTerm { ContractDocumentId = id, ContractVersionId = version, ContractTermTermcode = "PAY",
                    ContractTermTermtitle = "Thanh toán", ContractTermTermkind = 1, CreatedAt = DateTime.UtcNow };
                db.ContractTerms.Add(term);
                await db.SaveChangesAsync();
                var rows = Enumerable.Range(1, count).Select(i => new ContractPaymentMilestone { ContractDocumentId = id,
                    ContractVersionId = version, ContractTermId = term.ContractTermTermid, ContractPaymentMilestoneCode = $"PAY{i}",
                    ContractPaymentMilestoneTitlevi = $"Đợt {i}", ContractPaymentMilestoneAmount = 100000 / count,
                    ContractPaymentMilestonePaymentpercent = 100 / count, ContractPaymentMilestoneDisplayorder = i,
                    ContractPaymentMilestoneDueanchor = 1, ContractPaymentMilestoneDaycountmode = 1, CreatedAt = DateTime.UtcNow }).ToList();
                db.ContractPaymentMilestones.AddRange(rows);
                await db.SaveChangesAsync();
                return rows.Select(p => p.ContractPaymentMilestoneId).ToArray();
            }
            async Task<SignedPaymentSummary> Summary(long version) => (await service.SignedContracts(sale, null, 1, default, id))
                .Items.Single(r => r.VersionId == version).Payment;
            async Task Confirm(long version, long milestone)
            {
                await using var session = new MasterDbContext(options);
                await Service(session, Path.GetTempPath()).ConfirmSignedPayment(office, id, version, milestone, default);
            }

            var single = await Version(1, 2);
            var one = Assert.Single(await Plan(single, 1));
            var split = await Version(2, 2);
            var two = await Plan(split, 2);
            var empty = await Version(3, 2);
            var incomplete = await Version(4, 1);
            Assert.Equal(new SignedPaymentSummary(0, 0, null), await Summary(empty));
            Assert.Equal(new SignedPaymentSummary(1, 0, null), await Summary(single));
            Assert.Equal(new SignedPaymentSummary(2, 0, null), await Summary(split));
            Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => service.ConfirmSignedPayment(sale, id, split, two[0], default))).Status);
            Assert.Equal(403, (await Assert.ThrowsAsync<InternalAppException>(() => service.ConfirmSignedPayment(director, id, split, two[0], default))).Status);
            Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => service.SignedPayments(Sale(-1), id, split, default))).Status);
            Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => Confirm(incomplete, two[0]))).Status);
            Assert.Equal(404, (await Assert.ThrowsAsync<InternalAppException>(() => Confirm(single, two[0]))).Status);
            await Task.WhenAll(Confirm(split, two[1]), Confirm(split, two[1]));
            Assert.Equal(new SignedPaymentSummary(2, 1, 2), await Summary(split));
            Assert.Single(await db.Notifications.Where(n => n.NotificationEventkey == $"signed-payment:{two[1]}").ToListAsync());
            await Confirm(split, two[0]);
            await Confirm(single, one);
            Assert.Equal(new SignedPaymentSummary(1, 1, null), await Summary(single));
            Assert.Equal(new SignedPaymentSummary(2, 2, null), await Summary(split));
            Assert.All(await service.SignedPayments(director, id, split, default), p => Assert.True(p.Paid && p.PaidAt.HasValue));
            var notices = db.Notifications.Where(n => n.NotificationSubjectid == id && n.NotificationEventcode == "SIGNED_PAYMENT_CONFIRMED");
            Assert.Equal(3, await notices.CountAsync());
            Assert.Equal(3, await db.NotificationRecipients.CountAsync(r => r.EmployeeId == seller && notices.Any(n => n.NotificationId == r.NotificationId)));
        }
        finally
        {
            var notices = db.Notifications.Where(n => n.NotificationSubjectid == id && n.NotificationEventcode == "SIGNED_PAYMENT_CONFIRMED");
            await db.NotificationRecipients.Where(r => notices.Any(n => n.NotificationId == r.NotificationId)).ExecuteDeleteAsync();
            await notices.ExecuteDeleteAsync();
            var milestones = db.ContractPaymentMilestones.Where(p => p.ContractDocumentId == id);
            var keys = (await milestones.Select(p => p.ContractPaymentMilestoneId).ToListAsync()).Select(p => p.ToString()).ToList();
            await db.AuditLogs.Where(a => a.EntityName == "CONTRACT_PAYMENT_MILESTONE" && keys.Contains(a.EntityId!)).ExecuteDeleteAsync();
            await milestones.ExecuteDeleteAsync();
            await db.ContractTerms.Where(t => t.ContractDocumentId == id).ExecuteDeleteAsync();
            await db.ContractElectronicSignatures.Where(s => s.ContractDocumentId == id).ExecuteDeleteAsync();
            await db.ContractVersions.Where(v => v.ContractDocumentId == id).ExecuteDeleteAsync();
            await db.ContractDocuments.Where(d => d.ContractDocumentId == id).ExecuteDeleteAsync();
        }
    }
}

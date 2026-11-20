using System.Security.Cryptography;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
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
        var office = new InternalUser(0, "Office", "office", ["contract.check"]);
        var director = new InternalUser(0, "Director", "director", ["contract.approve"]);
        var page = await service.SignedContracts(office, null, 1, default);
        foreach (var row in page.Items)
        {
            var doc = await db.ContractDocuments.AsNoTracking().SingleAsync(d => d.ContractDocumentId == row.DocumentId);
            var review = await db.ContractReviewRequests.AsNoTracking().FirstAsync(r => r.ContractDocumentId == row.DocumentId
                && r.ContractVersionId == row.VersionId && r.CustomerId == doc.CustomerId);
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
}

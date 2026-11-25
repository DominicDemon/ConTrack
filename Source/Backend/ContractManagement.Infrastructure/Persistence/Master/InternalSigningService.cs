using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ContractManagement.Application.Features.Contracts.Signing;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using ContractManagement.Infrastructure.Files;
using Microsoft.EntityFrameworkCore;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfPigDocument = UglyToad.PdfPig.PdfDocument;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalSigningService(
    MasterDbContext db,
    InternalTemplateStorage storage,
    InternalWordTemplates word,
    IElectronicSigningPdfService signatures,
    CustomerContractMailer? customerMail = null) : IInternalSigningService
{
    static InternalSigningService()
    {
        if (OperatingSystem.IsWindows())
            GlobalFontSettings.UseWindowsFontsUnderWindows = true;
    }

    private const int CompanyParty = 1;
    private const int CustomerParty = 2;
    private const int Pending = 0;
    private const int Signed = 1;
    private const int OriginalContractFileRole = 1;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string signingRoot = Path.GetFullPath(Path.Combine(storage.Root, "..", "signing"));

    public async Task<IReadOnlyList<CompanySigningQueueItem>> Queue(InternalUser actor, CancellationToken ct)
    {
        RequireManager(actor);
        var rows = await (from signature in db.ContractElectronicSignatures.AsNoTracking()
            join document in db.ContractDocuments.AsNoTracking() on signature.ContractDocumentId equals document.ContractDocumentId
            join version in db.ContractVersions.AsNoTracking() on signature.ContractVersionId equals version.ContractVersionId
            join customer in db.Customers.AsNoTracking() on document.CustomerId equals customer.CustomerId
            where document.TenantId == null && document.ContractDocumentStatus == ContractDocumentStatus.PendingSignature
                && signature.ContractElectronicPartytype == CompanyParty && signature.ContractElectronicStatus == Pending
                && document.ContractDocumentCurrentVersionId == version.ContractVersionId
            orderby signature.CreatedAt
            select new CompanySigningQueueItem(signature.ContractElectronicSignatureId, document.ContractDocumentId,
                version.ContractVersionId, document.ContractDocumentCode ?? "", document.ContractDocumentName,
                customer.CustomerDisplayName, signature.ContractElectronicSignername, signature.CreatedAt))
            .Take(100).ToListAsync(ct);
        return rows;
    }

    public Task<SigningStartResult> Start(long documentId, StartCompanySigningRequest request, InternalUser actor, CancellationToken ct) =>
        StartCore(documentId, request, actor, administrativeApproval: false, ct);

    public async Task<SigningStartResult?> StartAfterFinalApproval(long documentId, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanCheck) throw new InternalAppException(403, "Chỉ Hành chính duyệt mới được chuyển hợp đồng vào hàng đợi ký công ty.");
        var (document, _, _, content) = await Approved(documentId, ct);
        InternalDraftService.RequireAccess(actor, document.EmployeeId);
        if (content.PartyType == InternalDraftPolicy.Business) return null;
        var fields = content.Fields;
        var signerName = fields.GetValueOrDefault("PROVIDER_REPRESENTATIVE_NAME")?.Trim();
        if (string.IsNullOrWhiteSpace(signerName))
            signerName = fields.GetValueOrDefault("PROVIDER_LEGAL_NAME")?.Trim();
        if (string.IsNullOrWhiteSpace(signerName))
            throw new InternalAppException(409, "Hợp đồng đang thiếu tên đại diện công ty.");
        // Company signing is an authenticated employee signature, not an OTP flow.
        // The provider contact phone is optional here; the manager's verified identity
        // is attached when they actually sign.
        var signerPhone = fields.GetValueOrDefault("PROVIDER_PHONE");
        return await StartCore(documentId,
            new StartCompanySigningRequest(signerName, signerPhone ?? string.Empty, fields.GetValueOrDefault("PROVIDER_REPRESENTATIVE_TITLE")),
            actor, administrativeApproval: true, ct);
    }

    private Task<SigningStartResult> StartCore(long documentId, StartCompanySigningRequest request, InternalUser actor,
        bool administrativeApproval, CancellationToken ct) =>
        InTransaction(async () =>
        {
            if (administrativeApproval)
            {
                if (!actor.CanCheck) throw new InternalAppException(403, "Chỉ Hành chính duyệt mới được chuyển hợp đồng vào hàng đợi ký công ty.");
            }
            else RequireManager(actor);
            var (document, version, customer, content) = await Approved(documentId, ct);
            var versionId = version.ContractVersionId;
            if (content.PartyType == InternalDraftPolicy.Business)
                throw new InternalAppException(409, "Hợp đồng doanh nghiệp không ký điện tử: in hợp đồng, ký và đóng dấu đỏ rồi tải bản scan ở màn Bản ký giấy.");

            if (administrativeApproval)
            {
                var existing = await db.ContractElectronicSignatures.AsNoTracking()
                    .Where(x => x.ContractDocumentId == documentId && x.ContractVersionId == versionId && x.ContractElectronicPartytype == CompanyParty)
                    .OrderBy(x => x.ContractElectronicSignatureId).FirstOrDefaultAsync(ct);
                if (existing is not null)
                    return new SigningStartResult(existing.ContractElectronicSignatureId, documentId, versionId,
                        (document.ContractDocumentCode ?? $"contract-{documentId}") + ".pdf", existing.CreatedAt);
            }

            var name = request.SignerName?.Trim() ?? "";
            var phone = PhoneNumber.Normalize(request.SignerPhone);
            if (name.Length is < 2 or > 200) throw new ArgumentException("Nhập họ tên người đại diện ký (từ 2 đến 200 ký tự).");
            if (phone is null && !administrativeApproval) throw new ArgumentException("Nhập số điện thoại người đại diện hợp lệ.");

            if (await db.ContractElectronicSignatures.AnyAsync(x => x.ContractDocumentId == documentId && x.ContractVersionId == versionId, ct))
                throw new InternalAppException(409, "Phiên bản này đã có luồng ký.");
            var customerPhone = PhoneNumber.Normalize(customer.CustomerPersonalPhone)
                ?? throw new InternalAppException(409, "Số điện thoại khách hàng không hợp lệ.");
            var rendered = await RenderForSigning(document, version, customer, content, ct);
            var pdf = rendered.Content;
            var hash = Convert.ToHexString(SHA256.HashData(pdf)).ToLowerInvariant();
            var key = $"{documentId}-{versionId}-{Guid.NewGuid():N}.pdf";
            Directory.CreateDirectory(signingRoot);
            await File.WriteAllBytesAsync(Path.Combine(signingRoot, key), pdf, ct);
            var now = DateTime.UtcNow;
            try
            {
                var file = new FileStorage
                {
                    FileStorageObjectid = documentId, FileStorageObjecttype = "CONTRACT_SIGNING",
                    FileStorageFiletype = "ORIGINAL_PDF", FileStorageFilename = (document.ContractDocumentCode ?? $"contract-{documentId}") + ".pdf",
                    FileStorageFilepath = key, FileStorageStoragekey = key, FileStorageContenttype = "application/pdf",
                    FileStorageFilesize = pdf.Length, FileStorageSha256 = hash, FileStorageUploadedbyemployeeid = actor.Id,
                    FileStorageUploadeddate = now
                };
                db.FileStorages.Add(file);
                await db.SaveChangesAsync(ct);
                db.ContractDocumentFiles.Add(new ContractDocumentFile
                {
                    ContractDocumentId = documentId, ContractVersionId = versionId, FileStorageFileid = file.FileStorageFileid,
                    ContractDocumentFileRole = OriginalContractFileRole, IsPrimary = true, CreatedDate = now, CreatedEmployeeId = actor.Id
                });
                var companySigner = new ContractElectronicSignature
                {
                    ContractDocumentId = documentId, ContractVersionId = versionId, ContractElectronicPartytype = CompanyParty,
                    ContractElectronicSignertype = "InternalEmployee", EmployeeId = administrativeApproval ? null : actor.Id, ContractElectronicSignername = name,
                    ContractElectronicSignertitle = Clean(request.SignerTitle, 100), ContractElectronicSignerphone = phone ?? "",
                    ContractElectronicSignaturetype = "DRAWN", ContractElectronicStatus = Pending, CreatedAt = now,
                    ContractElectronicDocumentHashAtSigning = version.ContractVersionSnapshothash,
                    ContractElectronicPagenumber = rendered.Company.PageNumber, ContractElectronicPositionx = rendered.Company.X,
                    ContractElectronicPositiony = rendered.Company.Y, ContractElectronicWidth = rendered.Company.Width,
                    ContractElectronicHeight = rendered.Company.Height
                };
                var customerSigner = new ContractElectronicSignature
                {
                    ContractDocumentId = documentId, ContractVersionId = versionId, ContractElectronicPartytype = CustomerParty,
                    ContractElectronicSignertype = "ExternalCustomer", ContractElectronicSignername = customer.CustomerDisplayName,
                    ContractElectronicSignerphone = customerPhone, ContractElectronicSigneremail = customer.CustomerEmail,
                    ContractElectronicSignaturetype = "DRAWN", ContractElectronicStatus = Pending, CreatedAt = now,
                    ContractElectronicDocumentHashAtSigning = version.ContractVersionSnapshothash,
                    ContractElectronicPagenumber = rendered.Customer.PageNumber, ContractElectronicPositionx = rendered.Customer.X,
                    ContractElectronicPositiony = rendered.Customer.Y, ContractElectronicWidth = rendered.Customer.Width,
                    ContractElectronicHeight = rendered.Customer.Height
                };
                db.ContractElectronicSignatures.AddRange(companySigner, customerSigner);
                document.UpdatedDate = now;
                ReviewData.Audit(db, "SIGNING_STARTED", "CONTRACT_DOCUMENTS", documentId, "EMPLOYEE", actor.Id, now);
                await db.SaveChangesAsync(ct);
                return new SigningStartResult(companySigner.ContractElectronicSignatureId, documentId, versionId, file.FileStorageFilename, now);
            }
            catch
            {
                File.Delete(Path.Combine(signingRoot, key));
                throw;
            }
        }, ct);

    public async Task<(byte[] Pdf, string FileName)> PrintPdf(long documentId, InternalUser actor, CancellationToken ct)
    {
        var (document, version, customer, content) = await Approved(documentId, ct);
        if (!actor.CanManageAll && document.EmployeeId != actor.Id && !actor.Permissions.Contains("contract.signed_evidence.view"))
            throw new InternalAppException(403, "Chỉ người phụ trách, quản lý hoặc bộ phận lưu bản ký giấy được in hợp đồng.");
        if (content.PartyType != InternalDraftPolicy.Business)
            throw new InternalAppException(409, "Hợp đồng cá nhân ký điện tử bằng OTP, không in để ký giấy.");
        var pdf = content.TemplateSnapshot?.WordStorageKey is not null
            ? await word.Render(content, content.TemplateSnapshot, pdf: true, ct)
            : RenderContractPdf(document, version, customer, content, content.TemplateSnapshot?.PrintFormat ?? await ContractFormatSettings.Read(db, content.TemplateId, ct)).Content;
        return (pdf, (document.ContractDocumentCode ?? $"contract-{documentId}") + ".pdf");
    }

    private async Task<SigningPdfLayout> RenderForSigning(ContractDocument document, ContractVersion version, Customer customer,
        DraftContent content, CancellationToken ct)
    {
        if (content.TemplateSnapshot?.WordStorageKey is { Length: > 0 })
        {
            var pdf = await word.Render(content, content.TemplateSnapshot, pdf: true, ct);
            var companyName = content.Fields.GetValueOrDefault("PROVIDER_REPRESENTATIVE_NAME") ?? "";
            var customerName = content.Fields.GetValueOrDefault("CUSTOMER_CONTACT_NAME") ?? customer.CustomerDisplayName;
            var company = LocateSigningSlot(pdf, companyName, "ĐẠI DIỆN BÊN B", rightSide: true);
            var customerSlot = LocateSigningSlot(pdf, customerName, "ĐẠI DIỆN BÊN A", rightSide: false);
            return new(pdf, company, customerSlot);
        }

        var rendered = RenderContractPdf(document, version, customer, content, content.TemplateSnapshot?.PrintFormat ?? await ContractFormatSettings.Read(db, content.TemplateId, ct));
        return new(rendered.Content,
            new(rendered.SignaturePage, rendered.CompanySignatureX, rendered.SignatureY, rendered.SignatureWidth, rendered.SignatureHeight),
            new(rendered.SignaturePage, rendered.CustomerSignatureX, rendered.SignatureY, rendered.SignatureWidth, rendered.SignatureHeight));
    }

    private static PdfSignaturePlacement LocateSigningSlot(byte[] pdfBytes, string signerName, string partyLabel, bool rightSide)
    {
        using var pdf = PdfPigDocument.Open(pdfBytes);
        var expectedName = Tokens(signerName);
        // The signature block's party label comes first: the signer's name also appears in the parties section
        // ("Đại diện: ..."), and matching it there stamped the signature over page one.
        var expectedLabel = Tokens(partyLabel);
        for (var pageNumber = pdf.NumberOfPages; pageNumber >= 1; pageNumber--)
        {
            var page = pdf.GetPage(pageNumber);
            var words = page.GetWords().ToList();
            var label = FindSequence(words, expectedLabel);
            if (label is null) continue;
            // A name printed under the label, in the same column, is where the signer signs above.
            var column = words.Where(w => w.BoundingBox.Top < label.Bottom
                && w.BoundingBox.Right > label.Left - 60 && w.BoundingBox.Left < label.Right + 60).ToList();
            var name = FindSequence(column, expectedName);
            return name is not null && label.Bottom - name.Top < 200
                ? Placement(pageNumber, page.Width, page.Height, name, aboveName: true)
                : Placement(pageNumber, page.Width, page.Height, label, aboveName: false);
        }

        // No label: a name on the last page can only be the signature block.
        var last = pdf.GetPage(pdf.NumberOfPages);
        var lastName = FindSequence(last.GetWords().ToList(), expectedName);
        if (lastName is not null)
            return Placement(pdf.NumberOfPages, last.Width, last.Height, lastName, aboveName: true);

        var lastPage = pdf.GetPage(pdf.NumberOfPages);
        var width = Math.Min(174m, (decimal)lastPage.Width * 0.42m);
        var center = (decimal)lastPage.Width * (rightSide ? 0.73m : 0.27m);
        var x = Math.Clamp(center - width / 2, 10m, (decimal)lastPage.Width - width - 10m);
        return new(pdf.NumberOfPages, x, Math.Max(20m, (decimal)lastPage.Height - 150m), width, 54m);
    }

    private static PdfSignaturePlacement Placement(int pageNumber, double pageWidth, double pageHeight, TextBounds bounds, bool aboveName)
    {
        var width = Math.Min(190m, (decimal)pageWidth * 0.46m);
        var center = (decimal)(bounds.Left + bounds.Right) / 2m;
        var x = Math.Clamp(center - width / 2, 10m, (decimal)pageWidth - width - 10m);
        var textTop = (decimal)pageHeight - (decimal)bounds.Top;
        var y = aboveName ? textTop - 54m : (decimal)pageHeight - (decimal)bounds.Bottom + 8m;
        y = Math.Clamp(y, 10m, (decimal)pageHeight - 64m);
        return new(pageNumber, x, y, width, 54m);
    }

    private static TextBounds? FindSequence(IReadOnlyList<UglyToad.PdfPig.Content.Word> words, string[] expected)
    {
        if (expected.Length == 0) return null;
        for (var start = words.Count - expected.Length; start >= 0; start--)
        {
            var matches = true;
            for (var offset = 0; offset < expected.Length; offset++)
            {
                if (NormalizeToken(words[start + offset].Text) != expected[offset]) { matches = false; break; }
            }
            if (!matches) continue;
            var selected = words.Skip(start).Take(expected.Length).ToList();
            return new(selected.Min(w => w.BoundingBox.Left), selected.Min(w => w.BoundingBox.Bottom),
                selected.Max(w => w.BoundingBox.Right), selected.Max(w => w.BoundingBox.Top));
        }
        return null;
    }

    private static string[] Tokens(string value) => value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
        .Select(NormalizeToken).Where(token => token.Length > 0).ToArray();

    private static string NormalizeToken(string value) => new(value.Normalize(NormalizationForm.FormD)
        .Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character) != System.Globalization.UnicodeCategory.NonSpacingMark
            && char.IsLetterOrDigit(character)).Select(char.ToLowerInvariant).ToArray());

    private sealed record TextBounds(double Left, double Bottom, double Right, double Top);
    private sealed record SigningPdfLayout(byte[] Content, PdfSignaturePlacement Company, PdfSignaturePlacement Customer);

    /// <summary>The current version of a contract awaiting signature: locked, approved by the customer and the manager, its snapshot matching the approved hash.</summary>
    private async Task<(ContractDocument Document, ContractVersion Version, Customer Customer, DraftContent Content)> Approved(long documentId, CancellationToken ct)
    {
        var document = await db.ContractDocuments.SingleOrDefaultAsync(x => x.ContractDocumentId == documentId && x.TenantId == null, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy hồ sơ hợp đồng.");
        if (document.ContractDocumentStatus != ContractDocumentStatus.PendingSignature || document.ContractDocumentCurrentVersionId is not long versionId)
            throw new InternalAppException(409, "Hồ sơ chưa được khách hàng duyệt và quản lý phê duyệt, hoặc không còn ở trạng thái chờ ký.");
        var version = await db.ContractVersions.SingleAsync(x => x.ContractVersionId == versionId && x.ContractDocumentId == documentId, ct);
        if (!version.ContractVersionIslocked || string.IsNullOrWhiteSpace(version.ContractVersionSnapshothash))
            throw new InternalAppException(409, "Phiên bản chưa được khóa và bảo vệ bằng mã kiểm tra.");
        var reviewApproved = await db.ContractReviewRequests.AnyAsync(x => x.ContractDocumentId == documentId
            && x.ContractVersionId == versionId && x.ContractReviewRequestStatus == ContractReviewStatus.Approved, ct);
        var approvalApproved = await db.ContractApprovalRequests.AnyAsync(x => x.ContractDocumentId == documentId
            && x.ContractVersionId == versionId && x.ContractApprovalRequestStatus == 1, ct);
        if (!reviewApproved || !approvalApproved) throw new InternalAppException(409, "Cần có xác nhận của khách và phê duyệt nội bộ cho cùng phiên bản.");
        var customer = await db.Customers.SingleAsync(x => x.CustomerId == document.CustomerId && x.CustomerStatus == 1, ct);
        var snapshot = await db.ContractDynamicValues.AsNoTracking().SingleOrDefaultAsync(x => x.ContractDocumentId == documentId
            && x.ContractVersionId == versionId && x.FieldKey == InternalRepository.SnapshotKey, ct)
            ?? throw new InternalAppException(409, "Thiếu nội dung hợp đồng đã khóa.");
        if (!SnapshotHash.Compute(snapshot.FieldValue).Equals(version.ContractVersionSnapshothash, StringComparison.OrdinalIgnoreCase))
            throw new InternalAppException(409, "Nội dung hợp đồng không khớp mã kiểm tra đã duyệt.");
        var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, Json)
            ?? throw new InternalAppException(409, "Nội dung hợp đồng không hợp lệ.");
        return (document, version, customer, content);
    }

    public async Task<byte[]> CompanyDocument(long signatureId, InternalUser actor, CancellationToken ct)
    {
        RequireManager(actor);
        var signature = await db.ContractElectronicSignatures.SingleOrDefaultAsync(x => x.ContractElectronicSignatureId == signatureId
            && x.ContractElectronicPartytype == CompanyParty && x.ContractElectronicStatus == Pending, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy yêu cầu ký của công ty.");
        var (document, version, customer, content) = await Approved(signature.ContractDocumentId, ct);
        var customerSignature = await db.ContractElectronicSignatures.SingleAsync(x => x.ContractDocumentId == signature.ContractDocumentId
            && x.ContractVersionId == signature.ContractVersionId && x.ContractElectronicPartytype == CustomerParty
            && x.ContractElectronicStatus == Pending, ct);
        var primary = await (from relation in db.ContractDocumentFiles
            join file in db.FileStorages on relation.FileStorageFileid equals file.FileStorageFileid
            where relation.ContractDocumentId == signature.ContractDocumentId && relation.ContractVersionId == signature.ContractVersionId
                && relation.IsPrimary
            select new { Relation = relation, File = file }).SingleOrDefaultAsync(ct)
            ?? throw new InternalAppException(404, "Không tìm thấy PDF hợp đồng.");
        var path = primary.File;
        var bytes = await ReadFile(path.FileStorageStoragekey, ct);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(path.FileStorageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InternalAppException(409, "File PDF không khớp mã kiểm tra đã lưu.");
        SigningPdfLayout rendered;
        try
        {
            rendered = await RenderForSigning(document, version, customer, content, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Keep an existing signing session usable while a Word template or PDF renderer is temporarily unavailable.
            return bytes;
        }
        var renderedHash = Convert.ToHexString(SHA256.HashData(rendered.Content)).ToLowerInvariant();
        if (renderedHash.Equals(path.FileStorageSha256, StringComparison.OrdinalIgnoreCase))
        {
            SetPlacement(signature, rendered.Company);
            SetPlacement(customerSignature, rendered.Customer);
            await db.SaveChangesAsync(ct);
            return bytes;
        }

        // Refresh the unsigned source in place. Keeping the existing primary relation avoids violating
        // databases that enforce a single primary PDF per contract version.
        var key = $"{signature.ContractDocumentId}-{signature.ContractVersionId}-{Guid.NewGuid():N}-word-template.pdf";
        Directory.CreateDirectory(signingRoot);
        await File.WriteAllBytesAsync(Path.Combine(signingRoot, key), rendered.Content, ct);
        try
        {
            path.FileStorageFilepath = key;
            path.FileStorageStoragekey = key;
            path.FileStorageFilesize = rendered.Content.Length;
            path.FileStorageSha256 = renderedHash;
            path.FileStorageUploadedbyemployeeid = actor.Id;
            path.FileStorageUploadeddate = DateTime.UtcNow;
            SetPlacement(signature, rendered.Company);
            SetPlacement(customerSignature, rendered.Customer);
            await db.SaveChangesAsync(ct);
            return rendered.Content;
        }
        catch
        {
            File.Delete(Path.Combine(signingRoot, key));
            // The existing signed session must remain viewable if refreshing its unsigned PDF fails.
            db.Entry(path).State = EntityState.Unchanged;
            return bytes;
        }
    }

    private static void SetPlacement(ContractElectronicSignature signature, PdfSignaturePlacement placement)
    {
        signature.ContractElectronicPagenumber = placement.PageNumber;
        signature.ContractElectronicPositionx = placement.X;
        signature.ContractElectronicPositiony = placement.Y;
        signature.ContractElectronicWidth = placement.Width;
        signature.ContractElectronicHeight = placement.Height;
    }

    public async Task<SigningStartResult> SignCompany(long signatureId, CompanySignatureRequest request, InternalUser actor, CancellationToken ct)
    {
        var result = await InTransaction(async () =>
        {
            RequireManager(actor);
            if (!request.Confirmed) throw new ArgumentException("Cần xác nhận đã kiểm tra hợp đồng trước khi ký.");
            var company = await db.ContractElectronicSignatures.SingleOrDefaultAsync(x => x.ContractElectronicSignatureId == signatureId
                && x.ContractElectronicPartytype == CompanyParty, ct) ?? throw new InternalAppException(404, "Không tìm thấy bên ký công ty.");
            if (company.ContractElectronicStatus != Pending) throw new InternalAppException(409, "Chữ ký công ty đã được xử lý.");
            var document = await db.ContractDocuments.SingleAsync(x => x.ContractDocumentId == company.ContractDocumentId && x.TenantId == null, ct);
            var version = await db.ContractVersions.SingleAsync(x => x.ContractVersionId == company.ContractVersionId, ct);
            if (document.ContractDocumentCurrentVersionId != version.ContractVersionId
                || document.ContractDocumentStatus != ContractDocumentStatus.PendingSignature
                || !version.ContractVersionIslocked
                || company.ContractElectronicDocumentHashAtSigning != version.ContractVersionSnapshothash)
                throw new InternalAppException(409, "Hợp đồng đã thay đổi sau khi phê duyệt.");
            var customer = await db.ContractElectronicSignatures.SingleAsync(x => x.ContractDocumentId == company.ContractDocumentId
                && x.ContractVersionId == company.ContractVersionId && x.ContractElectronicPartytype == CustomerParty, ct);
            if (customer.ContractElectronicStatus != Pending) throw new InternalAppException(409, "Bên khách không ở trạng thái chờ ký.");
            var fileInfo = await (from relation in db.ContractDocumentFiles join stored in db.FileStorages on relation.FileStorageFileid equals stored.FileStorageFileid
                where relation.ContractDocumentId == company.ContractDocumentId && relation.ContractVersionId == company.ContractVersionId && relation.IsPrimary
                select new { Relation = relation, Storage = stored }).SingleAsync(ct);
            var file = fileInfo.Storage;
            var source = await ReadFile(file.FileStorageStoragekey, ct);
            if (!Convert.ToHexString(SHA256.HashData(source)).Equals(file.FileStorageSha256, StringComparison.OrdinalIgnoreCase))
                throw new InternalAppException(409, "File PDF không khớp mã kiểm tra đã lưu.");
            var png = DecodeSignature(request.SignaturePngBase64);
            var placement = new PdfSignaturePlacement(company.ContractElectronicPagenumber ?? 1,
                company.ContractElectronicPositionx ?? 50, company.ContractElectronicPositiony ?? 680,
                company.ContractElectronicWidth ?? 190, company.ContractElectronicHeight ?? 54);
            var signed = signatures.StampSignature(source, png, placement);
            var signedKey = $"{company.ContractDocumentId}-{company.ContractVersionId}-company-{Guid.NewGuid():N}.pdf";
            await File.WriteAllBytesAsync(Path.Combine(signingRoot, signedKey), signed, ct);
            try
            {
                var now = DateTime.UtcNow;
                // Record the full name of the authenticated director who drew this signature.
                company.EmployeeId = actor.Id;
                company.ContractElectronicSignername = actor.Name.Trim();
                company.ContractElectronicSignertype = "InternalEmployee";
                company.ContractElectronicStatus = Signed;
                company.ContractElectronicSignedat = now;
                company.ContractElectronicSignatureimagestoragekey = "embedded-in-signed-pdf";
                company.ContractElectronicIpaddress = null;
                company.ContractElectronicUseragent = "Internal SystemAdmin signing";
                company.ContractElectronicDocumentHashAtSigning = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
                version.ContractVersionSigndate = now;
                fileInfo.Relation.IsPrimary = false;
                var signedFile = new FileStorage
                {
                    FileStorageObjectid = company.ContractDocumentId, FileStorageObjecttype = "CONTRACT_SIGNING",
                    FileStorageFiletype = "COMPANY_SIGNED_PDF", FileStorageFilename = Path.GetFileNameWithoutExtension(file.FileStorageFilename) + "-company-signed.pdf",
                    FileStorageFilepath = signedKey, FileStorageStoragekey = signedKey, FileStorageContenttype = "application/pdf",
                    FileStorageFilesize = signed.Length, FileStorageSha256 = Convert.ToHexString(SHA256.HashData(signed)),
                    FileStorageUploadedbyemployeeid = actor.Id, FileStorageUploadeddate = now
                };
                db.FileStorages.Add(signedFile);
                await db.SaveChangesAsync(ct);
                db.ContractDocumentFiles.Add(new ContractDocumentFile
                {
                    ContractDocumentId = company.ContractDocumentId, ContractVersionId = company.ContractVersionId,
                    FileStorageFileid = signedFile.FileStorageFileid, ContractDocumentFileRole = OriginalContractFileRole,
                    IsPrimary = true, CreatedDate = now, CreatedEmployeeId = actor.Id
                });
                var phone = await db.ContractPhoneVerifies.SingleOrDefaultAsync(x => x.ContractDocumentId == company.ContractDocumentId
                    && x.ContractPhoneVerifyReason == "CUSTOMER_ELECTRONIC_SIGNING", ct);
                var accessLink = await db.ContractAccessLinks.SingleOrDefaultAsync(x => x.ContractDocumentId == company.ContractDocumentId
                    && x.ContractVersionId == company.ContractVersionId && x.RevokedAt == null, ct);
                if (phone is null)
                {
                    phone = new ContractPhoneVerify { ContractDocumentId = company.ContractDocumentId, ContractPhoneVerifyPhoneSource = "CUSTOMER",
                        ContractPhoneVerifyPhoneNumberNormalized = customer.ContractElectronicSignerphone, ContractPhoneVerifyReason = "CUSTOMER_ELECTRONIC_SIGNING",
                        CreatedByEmployeeid = actor.Id, CreatedAt = now };
                    db.ContractPhoneVerifies.Add(phone);
                    await db.SaveChangesAsync(ct);
                }
                if (accessLink is null)
                {
                    accessLink = new ContractAccessLink { TenantId = null, ContractDocumentId = company.ContractDocumentId,
                        ContractVersionId = company.ContractVersionId, ContractAccessLinkVerificationPhoneId = phone.ContractPhoneVerifyId,
                        ContractAccessLinkTokenhash = Convert.ToHexString(SHA256.HashData(RandomNumberGenerator.GetBytes(32))).ToLowerInvariant(),
                        CreatedByEmployeeId = actor.Id, CreatedAt = now, ExpiresAt = now.AddDays(7) };
                    db.ContractAccessLinks.Add(accessLink);
                }
                else
                {
                    accessLink.ContractAccessLinkTokenhash = Convert.ToHexString(SHA256.HashData(RandomNumberGenerator.GetBytes(32))).ToLowerInvariant();
                    accessLink.ExpiresAt = now.AddDays(7);
                    accessLink.ActivatedAt = null;
                }
                document.UpdatedDate = now;
                ReviewData.Audit(db, "COMPANY_SIGNED", "CONTRACT_ELECTRONIC_SIGNATURE", signatureId, "EMPLOYEE", actor.Id, now);
                await db.SaveChangesAsync(ct);
                return new SigningStartResult(signatureId, company.ContractDocumentId, company.ContractVersionId, signedFile.FileStorageFilename, now);
            }
            catch
            {
                File.Delete(Path.Combine(signingRoot, signedKey));
                throw;
            }
        }, ct);
        // After the commit: it is now the customer's turn to sign on the portal.
        if (customerMail is not null)
            await customerMail.Send(result.DocumentId, "Công ty đã ký hợp đồng, mời bạn ký",
                "Đại diện công ty đã ký hợp đồng. Bạn mở hợp đồng trên Cổng khách hàng, xác nhận mã OTP gửi qua email và ký để hoàn tất.", ct);
        return result;
    }

    private async Task<T> InTransaction<T>(Func<Task<T>> action, CancellationToken ct) => await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try { var result = await action(); await transaction.CommitAsync(ct); return result; }
        catch { await transaction.RollbackAsync(ct); throw; }
    });

    private async Task<byte[]> ReadFile(string key, CancellationToken ct)
    {
        if (Path.GetFileName(key) != key) throw new InvalidDataException("Invalid signing storage key.");
        return await File.ReadAllBytesAsync(Path.Combine(signingRoot, key), ct);
    }

    /// <param name="format">The company print format (Thiết lập tham số hợp đồng); null keeps the original layout.</param>
    internal static RenderedContractPdf RenderContractPdf(ContractDocument document, ContractVersion version, Customer customer, DraftContent content, ContractFormat? format = null)
    {
        using var pdf = new PdfDocument();
        var pages = new List<PdfPage>();
        XGraphics graphics = null!;
        PdfPage page = null!;
        var pageNumber = 0;
        var marginLeft = format is null ? 85 : ContractFormat.MmToPoints(format.MarginLeftMm);
        var marginRight = format is null ? 57 : ContractFormat.MmToPoints(format.MarginRightMm);
        var marginTop = format is null ? 57 : ContractFormat.MmToPoints(format.MarginTopMm);
        var marginBottom = format is null ? 57 : ContractFormat.MmToPoints(format.MarginBottomMm);
        const double footerHeight = 43;
        double y = 0;

        var navy = XColor.FromArgb(18, 25, 29);
        var teal = XColor.FromArgb(18, 25, 29);
        var muted = XColor.FromArgb(70, 78, 82);
        var border = XColor.FromArgb(184, 198, 198);
        var pale = XColor.FromArgb(242, 248, 246);
        var family = format?.FontFamily ?? (content.UseTimesNewRoman ? DraftContent.ContractFont : "Arial");
        double titleSize = format?.TitleFontSize ?? 17, headingSize = format?.HeadingFontSize ?? 13, bodySize = format?.BodyFontSize ?? 13;
        var lineSpacing = format?.LineSpacing ?? 1;
        var titleFont = new XFont(family, titleSize, XFontStyleEx.Bold);
        var headingFont = new XFont(family, headingSize, XFontStyleEx.Bold);
        var bodyFont = new XFont(family, bodySize, XFontStyleEx.Regular);
        var smallFont = new XFont(family, Math.Max(8, bodySize - 2), XFontStyleEx.Regular);
        var labelFont = new XFont(family, bodySize, XFontStyleEx.Bold);
        var italicFont = new XFont(family, bodySize, XFontStyleEx.Italic);
        var labelItalicFont = new XFont(family, bodySize, XFontStyleEx.BoldItalic);
        string Title(string text) => format?.TitleUppercase == true ? text.ToUpperInvariant() : text;
        // Bilingual layout of the company's printed contracts: English first, Vietnamese (in italics) under or after it.
        var bilingual = content.LanguageMode == InternalDraftPolicy.Bilingual;
        string L(string vi, string en) => bilingual ? $"{en} / {vi}" : vi;
        string Both(string key, string vi) => bilingual && content.FieldsEn?.GetValueOrDefault(key) is { Length: > 0 } en && en != vi ? $"{en}\n{vi}" : vi;
        var innerWidth = 0d;
        var bottom = 0d;

        void DrawCentered(string text, XFont font, XBrush brush, double top, double height)
        {
            var textWidth = graphics.MeasureString(text, font).Width;
            graphics.DrawString(text, font, brush,
                new XRect(marginLeft + Math.Max(0, (innerWidth - textWidth) / 2), top, innerWidth, height), XStringFormats.TopLeft);
        }

        void NewPage(bool first = false)
        {
            graphics?.Dispose();
            page = pdf.AddPage();
            pages.Add(page);
            pageNumber++;
            graphics = XGraphics.FromPdfPage(page);
            innerWidth = page.Width.Point - marginLeft - marginRight;
            bottom = page.Height.Point - marginBottom - footerHeight;

            if (first)
            {
                y = marginTop;
                if (format?.ShowNationalMotto ?? true)
                {
                    DrawCentered("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", headingFont, new XSolidBrush(navy), y, headingSize + 2); y += headingSize + 4;
                    DrawCentered("Độc lập – Tự do – Hạnh phúc", smallFont, new XSolidBrush(navy), y, smallFont.Size + 2); y += smallFont.Size + 10;
                }
                if (bilingual && !string.IsNullOrWhiteSpace(content.NameEn))
                {
                    DrawCentered(Title(content.NameEn), titleFont, new XSolidBrush(navy), y, titleSize * 1.4); y += titleSize * 1.3;
                    DrawCentered(Title(document.ContractDocumentName), new XFont(family, bodySize, XFontStyleEx.BoldItalic), new XSolidBrush(navy), y, bodySize * 1.5); y += bodySize * 1.8;
                }
                else { DrawCentered(Title(document.ContractDocumentName), titleFont, new XSolidBrush(navy), y, titleSize * 1.4); y += titleSize * 1.45; }
                DrawCentered($"{L("Số", "No.")}: {document.ContractDocumentCode ?? $"HD-{document.ContractDocumentId}"}", smallFont, new XSolidBrush(muted), y, 13); y += 24;
            }
            else
            {
                y = marginTop;
                graphics.DrawString(document.ContractDocumentName, labelFont, new XSolidBrush(navy),
                    new XRect(marginLeft, y, innerWidth * .7, 14), XStringFormats.TopLeft);
                graphics.DrawString($"{document.ContractDocumentCode} · Phiên bản {version.ContractVersionVersionNo}", smallFont, new XSolidBrush(muted),
                    new XRect(marginLeft + innerWidth * .7, y, innerWidth * .3, 14), XStringFormats.TopRight);
                y += 22;
            }
        }

        bool EnsureSpace(double height)
        {
            if (y + height <= bottom) return false;
            if (pageNumber >= 100) throw new InvalidOperationException("Hợp đồng vượt quá giới hạn 100 trang.");
            NewPage();
            return true;
        }

        List<string> Wrap(string text, XFont font, double maxWidth)
        {
            var lines = new List<string>();
            foreach (var paragraph in (text ?? string.Empty).Replace("\r", string.Empty).Split('\n'))
            {
                var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0) { lines.Add(string.Empty); continue; }
                var line = string.Empty;
                foreach (var word in words)
                {
                    var candidate = line.Length == 0 ? word : $"{line} {word}";
                    if (graphics.MeasureString(candidate, font).Width <= maxWidth)
                    {
                        line = candidate;
                        continue;
                    }
                    if (line.Length > 0) lines.Add(line);
                    if (graphics.MeasureString(word, font).Width <= maxWidth)
                    {
                        line = word;
                        continue;
                    }
                    line = string.Empty;
                    foreach (var character in word)
                    {
                        var next = line + character;
                        if (line.Length > 0 && graphics.MeasureString(next, font).Width > maxWidth)
                        { lines.Add(line); line = character.ToString(); }
                        else line = next;
                    }
                }
                if (line.Length > 0) lines.Add(line);
            }
            return lines;
        }

        void AddParagraph(string text, XFont font, XBrush brush, double indent = 0, double spaceAfter = 5)
        {
            var lineHeight = font.Size * 1.2 * lineSpacing;
            foreach (var line in Wrap(text, font, innerWidth - indent))
            {
                EnsureSpace(lineHeight + 2);
                if (line.Length > 0)
                    graphics.DrawString(line, font, brush, new XRect(marginLeft + indent, y, innerWidth - indent, lineHeight + 2), XStringFormats.TopLeft);
                y += lineHeight;
            }
            y += spaceAfter;
        }

        void AddSectionTitle(string title)
        {
            EnsureSpace(22);
            graphics.DrawString(title, headingFont, new XSolidBrush(navy), new XRect(marginLeft, y, innerWidth, 19), XStringFormats.TopLeft);
            y += 24;
        }

        string Field(string key, string fallback = "") => content.Fields.GetValueOrDefault(key)?.Trim() is { Length: > 0 } value ? value : fallback;
        var customerRows = new List<(string Label, string Value)>
        {
            (L("Tên pháp lý", "Name"), Both("CUSTOMER_COMPANY", Field("CUSTOMER_COMPANY", customer.CustomerDisplayName))),
            (L("Đại diện", "Represented by"), Field("CUSTOMER_CONTACT_NAME", customer.CustomerDisplayName)),
            (L("Chức vụ", "Position"), Both("CUSTOMER_CONTACT_TITLE", Field("CUSTOMER_CONTACT_TITLE"))),
            (L("Địa chỉ", "Address"), Both("CUSTOMER_ADDRESS", Field("CUSTOMER_ADDRESS"))),
            (L("Điện thoại", "Tel"), Field("CUSTOMER_CONTACT_PHONE", customer.CustomerPersonalPhone ?? string.Empty)),
            ("Email", Field("CUSTOMER_EMAIL", customer.CustomerEmail ?? string.Empty)),
            (L("Mã số thuế", "Tax code"), Field("CUSTOMER_TAX_CODE"))
        }.Where(item => !string.IsNullOrWhiteSpace(item.Value)).ToList();
        var providerRows = new List<(string Label, string Value)>
        {
            (L("Tên pháp lý", "Name"), Both("PROVIDER_LEGAL_NAME", Field("PROVIDER_LEGAL_NAME"))),
            (L("Đại diện", "Represented by"), Field("PROVIDER_REPRESENTATIVE_NAME")),
            (L("Chức vụ", "Position"), Both("PROVIDER_REPRESENTATIVE_TITLE", Field("PROVIDER_REPRESENTATIVE_TITLE"))),
            (L("Địa chỉ", "Address"), Both("PROVIDER_ADDRESS", Field("PROVIDER_ADDRESS"))),
            (L("Điện thoại", "Tel"), Field("PROVIDER_PHONE")),
            (L("Mã số thuế", "Tax code"), Field("PROVIDER_TAX_CODE")),
            (L("Tài khoản", "Bank account"), string.Join(" · ", new[] { Field("PROVIDER_BANK_ACCOUNT_NUMBER"), Both("PROVIDER_BANK_NAME", Field("PROVIDER_BANK_NAME")) }.Where(x => x.Length > 0)))
        }.Where(item => !string.IsNullOrWhiteSpace(item.Value)).ToList();
        void AddPartyDetails(string title, List<(string Label, string Value)> rows)
        {
            AddParagraph(title, labelFont, new XSolidBrush(navy), spaceAfter: 3);
            foreach (var row in rows.Skip(1))
                AddParagraph($"{row.Label}: {row.Value}", bodyFont, new XSolidBrush(navy), spaceAfter: 2);
            y += 7;
        }

        void DrawItemsHeader(double[] columns)
        {
            var labels = new[] { "STT", L("Hạng mục", "Item"), L("Đơn vị tính", "Unit"), L("Số lượng", "Qty"), L("Đơn giá", "Unit price"), L("Thành tiền", "Amount") };
            var x = marginLeft;
            for (var i = 0; i < columns.Length; i++)
            {
                var rect = new XRect(x, y, columns[i], 28);
                graphics.DrawRectangle(new XPen(border, 0.6), new XSolidBrush(pale), rect);
                graphics.DrawString(labels[i], labelFont, new XSolidBrush(navy), new XRect(x + 5, y + 5, columns[i] - 10, 18), XStringFormats.TopLeft);
                x += columns[i];
            }
            y += 28;
        }

        NewPage(first: true);
        if (!string.IsNullOrWhiteSpace(Field("CONTRACT_LEGAL_BASES")))
        {
            AddSectionTitle(L("CĂN CỨ HỢP ĐỒNG", "LEGAL BASES"));
            AddParagraph(Field("CONTRACT_LEGAL_BASES"), bodyFont, new XSolidBrush(navy));
        }

        AddPartyDetails($"{L("BÊN A", "PARTY A")}: {Both("CUSTOMER_COMPANY", Field("CUSTOMER_COMPANY", customer.CustomerDisplayName))}", customerRows);
        AddPartyDetails($"{L("BÊN B", "PARTY B")}: {Both("PROVIDER_LEGAL_NAME", Field("PROVIDER_LEGAL_NAME", "[THÔNG TIN CẦN BỔ SUNG]"))}", providerRows);
        var effective = content.EffectiveDate?.ToString("dd/MM/yyyy") ?? "[THÔNG TIN CẦN BỔ SUNG]";
        var expire = content.ExpireDate?.ToString("dd/MM/yyyy") ?? "[THÔNG TIN CẦN BỔ SUNG]";
        AddParagraph((bilingual ? $"Term: from {effective} to {expire}.\n" : "") + $"Thời hạn hiệu lực: từ {effective} đến {expire}.",
            smallFont, new XSolidBrush(muted), spaceAfter: 7);

        AddSectionTitle(L("HẠNG MỤC HỢP ĐỒNG", "CONTRACT ITEMS"));
        var columns = new[] { innerWidth * .07, innerWidth * .31, innerWidth * .15, innerWidth * .12, innerWidth * .17, innerWidth * .18 };
        DrawItemsHeader(columns);
        for (var itemIndex = 0; itemIndex < content.Items.Count; itemIndex++)
        {
            var item = content.Items[itemIndex];
            var itemLines = Wrap(item.Name, bodyFont, columns[1] - 10);
            var values = new[] { (itemIndex + 1).ToString(), item.Name, "[THÔNG TIN CẦN BỔ SUNG]", item.Quantity.ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")),
                FormatMoney(item.UnitPrice), FormatMoney(item.Quantity * item.UnitPrice) };
            var maxLines = values.Select((value, index) => (index == 1 ? itemLines : Wrap(value, bodyFont, columns[index] - 10)).Count).Max();
            var rowHeight = Math.Max(30, maxLines * 18 + 12);
            if (EnsureSpace(rowHeight)) DrawItemsHeader(columns);
            var x = marginLeft;
            for (var i = 0; i < columns.Length; i++)
            {
                var rect = new XRect(x, y, columns[i], rowHeight);
                graphics.DrawRectangle(new XPen(border, 0.55), XBrushes.White, rect);
                var lines = i == 1 ? itemLines : Wrap(values[i], bodyFont, columns[i] - 10);
                var textY = y + (rowHeight - lines.Count * 18) / 2;
                foreach (var line in lines)
                {
                    var align = i >= 3 ? XStringFormats.TopRight : (i == 0 ? XStringFormats.TopCenter : XStringFormats.TopLeft);
                    graphics.DrawString(line, bodyFont, new XSolidBrush(navy), new XRect(x + 5, textY, columns[i] - 10, 18), align);
                    textY += 18;
                }
                x += columns[i];
            }
            y += rowHeight;
        }

        const string currency = "VND";
        var totals = new[]
        {
            (L("Tạm tính", "Subtotal"), content.Totals.Subtotal), (L("Giảm giá", "Discount"), content.Totals.Discount),
            ($"VAT ({content.VatPercent:0.##}%)", content.Totals.Vat)
        };
        y += 7;
        foreach (var total in totals)
        {
            EnsureSpace(17);
            graphics.DrawString(total.Item1, smallFont, new XSolidBrush(muted), new XRect(marginLeft + innerWidth * .55, y, innerWidth * .23, 16), XStringFormats.TopLeft);
            graphics.DrawString($"{FormatMoney(total.Item2)} VND", smallFont, new XSolidBrush(navy), new XRect(marginLeft + innerWidth * .78, y, innerWidth * .22, 16), XStringFormats.TopRight);
            y += 19;
        }
        EnsureSpace(27);
        graphics.DrawLine(new XPen(border, 0.8), marginLeft + innerWidth * .55, y, marginLeft + innerWidth, y); y += 7;
        graphics.DrawString(L("TỔNG GIÁ TRỊ", "TOTAL"), labelFont, new XSolidBrush(navy), new XRect(marginLeft + innerWidth * .55, y, innerWidth * .23, 20), XStringFormats.TopLeft);
        graphics.DrawString($"{FormatMoney(content.Totals.Total)} {currency}", new XFont(family, 13, XFontStyleEx.Bold), new XSolidBrush(teal),
            new XRect(marginLeft + innerWidth * .78, y, innerWidth * .22, 20), XStringFormats.TopRight);
        y += 27;

        if (content.Milestones.Count > 0)
        {
            AddSectionTitle(L("LỊCH THANH TOÁN", "PAYMENT SCHEDULE"));
            AddParagraph($"{L("Thanh toán toàn bộ", "Total payment")}: {FormatMoney(content.Totals.Total)} {currency}", labelFont,
                new XSolidBrush(navy), spaceAfter: 3);
            foreach (var milestone in content.Milestones)
            {
                var due = milestone.Anchor == "DATE"
                    ? milestone.DueDate?.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) ?? "Chưa chọn ngày"
                    : $"{milestone.OffsetDays} ngày lịch sau {(milestone.Anchor == "SIGNED" ? "ngày ký đủ" : "ngày bàn giao")}";
                AddParagraph($"{milestone.Title}: {FormatMoney(milestone.Amount)} {currency}", labelFont, new XSolidBrush(navy), spaceAfter: 1);
                AddParagraph(due, bodyFont, new XSolidBrush(navy), indent: 8, spaceAfter: 1);
                if (!string.IsNullOrWhiteSpace(milestone.Condition))
                    AddParagraph(milestone.Condition, bodyFont, new XSolidBrush(navy), indent: 8, spaceAfter: 4);
            }
        }

        AddSectionTitle(L("ĐIỀU KHOẢN HỢP ĐỒNG", "TERMS AND CONDITIONS"));
        if (content.Terms.Count == 0)
            AddParagraph("[THÔNG TIN CẦN BỔ SUNG]", bodyFont, new XSolidBrush(navy));
        for (var i = 0; i < content.Terms.Count; i++)
        {
            var term = content.Terms[i];
            if (bilingual && !string.IsNullOrWhiteSpace(term.TitleEn))
            {
                AddParagraph($"ARTICLE {i + 1}: {term.TitleEn}", labelFont, new XSolidBrush(navy), spaceAfter: 1);
                AddParagraph($"ĐIỀU {i + 1}: {term.Title}", labelItalicFont, new XSolidBrush(navy), spaceAfter: 3);
                if (!string.IsNullOrWhiteSpace(term.ContentEn)) AddParagraph(term.ContentEn, bodyFont, new XSolidBrush(navy), indent: 8, spaceAfter: 3);
                AddParagraph(term.Content, italicFont, new XSolidBrush(navy), indent: 8, spaceAfter: 9);
                continue;
            }
            AddParagraph($"Điều {i + 1}. {term.Title}", labelFont, new XSolidBrush(navy), spaceAfter: 3);
            AddParagraph(term.Content, bodyFont, new XSolidBrush(navy), indent: 8, spaceAfter: 9);
        }

        // Label (18) · signature box (gap 2 + 48) · name (gap 4 + 20): the stamped box is painted white,
        // so it must start below the label or it hides the label's lower half.
        const double signatureBlockHeight = 120, signatureBoxOffset = 20, signatureBoxHeight = 48, signerNameOffset = 72;
        EnsureSpace(signatureBlockHeight);
        var signatureTop = y + 12;
        var slotWidth = (innerWidth - 24) / 2;
        var customerX = marginLeft;
        var companyX = marginLeft + slotWidth + 24;
        void DrawSignatureSlot(double x, string party)
        {
            graphics.DrawString(party, labelFont, new XSolidBrush(navy), new XRect(x, signatureTop, slotWidth, 18), XStringFormats.TopCenter);
            var fullName = party.Contains("BÊN A", StringComparison.OrdinalIgnoreCase)
                ? Field("CUSTOMER_CONTACT_NAME", customer.CustomerDisplayName)
                : Field("PROVIDER_REPRESENTATIVE_NAME", Field("PROVIDER_LEGAL_NAME", "[THÔNG TIN CẦN BỔ SUNG]"));
            graphics.DrawString(fullName, labelFont, new XSolidBrush(navy), new XRect(x, signatureTop + signerNameOffset, slotWidth, 20), XStringFormats.TopCenter);
        }
        DrawSignatureSlot(customerX, bilingual ? "PARTY A / ĐẠI DIỆN BÊN A" : "ĐẠI DIỆN BÊN A");
        DrawSignatureSlot(companyX, bilingual ? "PARTY B / ĐẠI DIỆN BÊN B" : "ĐẠI DIỆN BÊN B");
        y = signatureTop + signatureBlockHeight;

        graphics.Dispose();
        for (var i = 0; i < pages.Count; i++)
        {
            using var footer = XGraphics.FromPdfPage(pages[i]);
            var footerY = pages[i].Height.Point - marginBottom + 8;
            footer.DrawLine(new XPen(border, 0.5), marginLeft, footerY, pages[i].Width.Point - marginRight, footerY);
            footer.DrawString($"Trang {i + 1} / {pages.Count}", smallFont, new XSolidBrush(muted),
                new XRect(marginLeft, footerY + 5, innerWidth, 17), XStringFormats.TopRight);
        }
        using var stream = new MemoryStream();
        pdf.Save(stream);
        return new RenderedContractPdf(stream.ToArray(), pages.Count,
            (int)(companyX + (slotWidth - 190) / 2), (int)(customerX + (slotWidth - 190) / 2),
            (int)Math.Ceiling(signatureTop + signatureBoxOffset), 190, (int)signatureBoxHeight);
    }

    private static string FormatMoney(decimal amount) => amount.ToString("#,##0.##", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    internal sealed record RenderedContractPdf(byte[] Content, int SignaturePage, int CompanySignatureX, int CustomerSignatureX,
        int SignatureY, int SignatureWidth, int SignatureHeight);

    private static string? Clean(string? input, int max) => string.IsNullOrWhiteSpace(input) ? null : input.Trim()[..Math.Min(input.Trim().Length, max)];
    // Sếp (contract.approve) signs for the company and opens the signing session; Hành chính and Sale do not.
    private static void RequireManager(InternalUser actor) { if (!actor.CanApprove) throw new InternalAppException(403, "Chỉ Sếp (người ký cho công ty) được tạo phiên ký và ký."); }
    /// <summary>A drawn signature must really be a PNG of at most 1 MB (UploadedImages).</summary>
    private static byte[] DecodeSignature(string input) =>
        ContractManagement.Application.Common.Files.UploadedImages.SignaturePng(input, "Ảnh chữ ký PNG không hợp lệ.");
}

using System.Security.Cryptography;
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

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalSigningService(
    MasterDbContext db,
    InternalTemplateStorage storage,
    IElectronicSigningPdfService signatures) : IInternalSigningService
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
        InTransaction(async () =>
        {
            RequireManager(actor);
            var name = request.SignerName.Trim();
            var phone = PhoneNumber.Normalize(request.SignerPhone);
            if (name.Length is < 2 or > 200) throw new ArgumentException("Nháº­p há» tÃªn ngÆ°á»i Ä‘áº¡i diá»‡n kÃ½.");
            if (phone is null) throw new ArgumentException("Sá»‘ Ä‘iá»‡n thoáº¡i ngÆ°á»i Ä‘áº¡i diá»‡n khÃ´ng há»£p lá»‡.");

            var document = await db.ContractDocuments.SingleOrDefaultAsync(x => x.ContractDocumentId == documentId && x.TenantId == null, ct)
                ?? throw new InternalAppException(404, "KhÃ´ng tÃ¬m tháº¥y há»“ sÆ¡ há»£p Ä‘á»“ng.");
            if (document.ContractDocumentStatus != ContractDocumentStatus.PendingSignature || document.ContractDocumentCurrentVersionId is not long versionId)
                throw new InternalAppException(409, "Há»“ sÆ¡ chÆ°a Ä‘Æ°á»£c duyá»‡t hoáº·c khÃ´ng cÃ²n á»Ÿ tráº¡ng thÃ¡i chá» kÃ½.");
            if (await db.ContractElectronicSignatures.AnyAsync(x => x.ContractDocumentId == documentId && x.ContractVersionId == versionId, ct))
                throw new InternalAppException(409, "PhiÃªn báº£n nÃ y Ä‘Ã£ cÃ³ luá»“ng kÃ½.");
            var version = await db.ContractVersions.SingleAsync(x => x.ContractVersionId == versionId && x.ContractDocumentId == documentId, ct);
            if (!version.ContractVersionIslocked || string.IsNullOrWhiteSpace(version.ContractVersionSnapshothash))
                throw new InternalAppException(409, "PhiÃªn báº£n chÆ°a Ä‘Æ°á»£c khÃ³a vÃ  báº£o vá»‡ hash.");
            var reviewApproved = await db.ContractReviewRequests.AnyAsync(x => x.ContractDocumentId == documentId
                && x.ContractVersionId == versionId && x.ContractReviewRequestStatus == ContractReviewStatus.Approved, ct);
            var approvalApproved = await db.ContractApprovalRequests.AnyAsync(x => x.ContractDocumentId == documentId
                && x.ContractVersionId == versionId && x.ContractApprovalRequestStatus == 1, ct);
            if (!reviewApproved || !approvalApproved) throw new InternalAppException(409, "Cáº§n cÃ³ xÃ¡c nháº­n cá»§a khÃ¡ch vÃ  phÃª duyá»‡t ná»™i bá»™ cho cÃ¹ng phiÃªn báº£n.");

            var customer = await db.Customers.SingleAsync(x => x.CustomerId == document.CustomerId && x.CustomerStatus == 1, ct);
            var customerPhone = PhoneNumber.Normalize(customer.CustomerPersonalPhone)
                ?? throw new InternalAppException(409, "Sá»‘ Ä‘iá»‡n thoáº¡i khÃ¡ch hÃ ng khÃ´ng há»£p lá»‡.");
            var snapshot = await db.ContractDynamicValues.AsNoTracking().SingleOrDefaultAsync(x => x.ContractDocumentId == documentId
                && x.ContractVersionId == versionId && x.FieldKey == InternalRepository.SnapshotKey, ct)
                ?? throw new InternalAppException(409, "Thiáº¿u snapshot há»£p Ä‘á»“ng Ä‘Ã£ khÃ³a.");
            if (!SnapshotHash.Compute(snapshot.FieldValue).Equals(version.ContractVersionSnapshothash, StringComparison.OrdinalIgnoreCase))
                throw new InternalAppException(409, "Snapshot há»£p Ä‘á»“ng khÃ´ng khá»›p hash Ä‘Ã£ duyá»‡t.");
            var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, Json)
                ?? throw new InternalAppException(409, "Snapshot há»£p Ä‘á»“ng khÃ´ng há»£p lá»‡.");
            var rendered = RenderContractPdf(document, version, customer, content);
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
                    ContractElectronicSignertype = "InternalEmployee", EmployeeId = actor.Id, ContractElectronicSignername = name,
                    ContractElectronicSignertitle = Clean(request.SignerTitle, 100), ContractElectronicSignerphone = phone,
                    ContractElectronicSignaturetype = "DRAWN", ContractElectronicStatus = Pending, CreatedAt = now,
                    ContractElectronicDocumentHashAtSigning = version.ContractVersionSnapshothash,
                    ContractElectronicPagenumber = rendered.SignaturePage, ContractElectronicPositionx = rendered.CompanySignatureX,
                    ContractElectronicPositiony = rendered.SignatureY, ContractElectronicWidth = rendered.SignatureWidth,
                    ContractElectronicHeight = rendered.SignatureHeight
                };
                var customerSigner = new ContractElectronicSignature
                {
                    ContractDocumentId = documentId, ContractVersionId = versionId, ContractElectronicPartytype = CustomerParty,
                    ContractElectronicSignertype = "ExternalCustomer", ContractElectronicSignername = customer.CustomerDisplayName,
                    ContractElectronicSignerphone = customerPhone, ContractElectronicSigneremail = customer.CustomerEmail,
                    ContractElectronicSignaturetype = "DRAWN", ContractElectronicStatus = Pending, CreatedAt = now,
                    ContractElectronicDocumentHashAtSigning = version.ContractVersionSnapshothash,
                    ContractElectronicPagenumber = rendered.SignaturePage, ContractElectronicPositionx = rendered.CustomerSignatureX,
                    ContractElectronicPositiony = rendered.SignatureY, ContractElectronicWidth = rendered.SignatureWidth,
                    ContractElectronicHeight = rendered.SignatureHeight
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

    public async Task<byte[]> CompanyDocument(long signatureId, InternalUser actor, CancellationToken ct)
    {
        RequireManager(actor);
        var signature = await db.ContractElectronicSignatures.AsNoTracking().SingleOrDefaultAsync(x => x.ContractElectronicSignatureId == signatureId
            && x.ContractElectronicPartytype == CompanyParty && x.ContractElectronicStatus == Pending, ct)
            ?? throw new InternalAppException(404, "KhÃ´ng tÃ¬m tháº¥y yÃªu cáº§u kÃ½ cá»§a cÃ´ng ty.");
        var path = await (from document in db.ContractDocuments.AsNoTracking()
            join relation in db.ContractDocumentFiles.AsNoTracking() on document.ContractDocumentId equals relation.ContractDocumentId
            join file in db.FileStorages.AsNoTracking() on relation.FileStorageFileid equals file.FileStorageFileid
            where document.TenantId == null && document.ContractDocumentId == signature.ContractDocumentId
                && relation.ContractVersionId == signature.ContractVersionId && relation.IsPrimary
            select file)
            .SingleOrDefaultAsync(ct) ?? throw new InternalAppException(404, "KhÃ´ng tÃ¬m tháº¥y PDF há»£p Ä‘á»“ng.");
        var bytes = await ReadFile(path.FileStorageStoragekey, ct);
        if (!Convert.ToHexString(SHA256.HashData(bytes)).Equals(path.FileStorageSha256, StringComparison.OrdinalIgnoreCase))
            throw new InternalAppException(409, "File PDF khÃ´ng khá»›p mÃ£ kiá»ƒm tra Ä‘Ã£ lÆ°u.");
        return bytes;
    }

    public Task<SigningStartResult> SignCompany(long signatureId, CompanySignatureRequest request, InternalUser actor, CancellationToken ct) =>
        InTransaction(async () =>
        {
            RequireManager(actor);
            if (!request.Confirmed) throw new ArgumentException("Cáº§n xÃ¡c nháº­n Ä‘Ã£ kiá»ƒm tra há»£p Ä‘á»“ng trÆ°á»›c khi kÃ½.");
            var company = await db.ContractElectronicSignatures.SingleOrDefaultAsync(x => x.ContractElectronicSignatureId == signatureId
                && x.ContractElectronicPartytype == CompanyParty, ct) ?? throw new InternalAppException(404, "KhÃ´ng tÃ¬m tháº¥y bÃªn kÃ½ cÃ´ng ty.");
            if (company.ContractElectronicStatus != Pending) throw new InternalAppException(409, "Chá»¯ kÃ½ cÃ´ng ty Ä‘Ã£ Ä‘Æ°á»£c xá»­ lÃ½.");
            var document = await db.ContractDocuments.SingleAsync(x => x.ContractDocumentId == company.ContractDocumentId && x.TenantId == null, ct);
            var version = await db.ContractVersions.SingleAsync(x => x.ContractVersionId == company.ContractVersionId, ct);
            if (document.ContractDocumentCurrentVersionId != version.ContractVersionId
                || document.ContractDocumentStatus != ContractDocumentStatus.PendingSignature
                || !version.ContractVersionIslocked
                || company.ContractElectronicDocumentHashAtSigning != version.ContractVersionSnapshothash)
                throw new InternalAppException(409, "Há»£p Ä‘á»“ng Ä‘Ã£ thay Ä‘á»•i sau khi phÃª duyá»‡t.");
            var customer = await db.ContractElectronicSignatures.SingleAsync(x => x.ContractDocumentId == company.ContractDocumentId
                && x.ContractVersionId == company.ContractVersionId && x.ContractElectronicPartytype == CustomerParty, ct);
            if (customer.ContractElectronicStatus != Pending) throw new InternalAppException(409, "BÃªn khÃ¡ch khÃ´ng á»Ÿ tráº¡ng thÃ¡i chá» kÃ½.");
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
                company.ContractElectronicWidth ?? 170, company.ContractElectronicHeight ?? 55);
            var signed = signatures.StampSignature(source, png, placement);
            var signedKey = $"{company.ContractDocumentId}-{company.ContractVersionId}-company-{Guid.NewGuid():N}.pdf";
            await File.WriteAllBytesAsync(Path.Combine(signingRoot, signedKey), signed, ct);
            try
            {
                var now = DateTime.UtcNow;
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

    internal static RenderedContractPdf RenderContractPdf(ContractDocument document, ContractVersion version, Customer customer, DraftContent content)
    {
        using var pdf = new PdfDocument();
        var pages = new List<PdfPage>();
        XGraphics graphics = null!;
        PdfPage page = null!;
        var pageNumber = 0;
        const double margin = 44;
        const double footerHeight = 34;
        double y = 0;

        var navy = XColor.FromArgb(24, 51, 73);
        var teal = XColor.FromArgb(18, 122, 116);
        var muted = XColor.FromArgb(88, 105, 117);
        var border = XColor.FromArgb(201, 214, 220);
        var pale = XColor.FromArgb(241, 247, 247);
        var titleFont = new XFont("Arial", 16, XFontStyleEx.Bold);
        var headingFont = new XFont("Arial", 10, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 9.5, XFontStyleEx.Regular);
        var smallFont = new XFont("Arial", 8, XFontStyleEx.Regular);
        var labelFont = new XFont("Arial", 8.5, XFontStyleEx.Bold);
        var innerWidth = 0d;
        var bottom = 0d;

        void DrawCentered(string text, XFont font, XBrush brush, double top, double height)
        {
            var textWidth = graphics.MeasureString(text, font).Width;
            graphics.DrawString(text, font, brush,
                new XRect(margin + Math.Max(0, (innerWidth - textWidth) / 2), top, innerWidth, height), XStringFormats.TopLeft);
        }

        void NewPage(bool first = false)
        {
            graphics?.Dispose();
            page = pdf.AddPage();
            pages.Add(page);
            pageNumber++;
            graphics = XGraphics.FromPdfPage(page);
            innerWidth = page.Width.Point - margin * 2;
            bottom = page.Height.Point - footerHeight;

            if (first)
            {
                y = 34;
                DrawCentered("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM", headingFont, new XSolidBrush(navy), y, 15); y += 16;
                DrawCentered("Độc lập – Tự do – Hạnh phúc", smallFont, new XSolidBrush(navy), y, 13); y += 17;
                var ruleY = y;
                graphics.DrawLine(new XPen(border, 0.8), margin + 105, ruleY, page.Width.Point - margin - 105, ruleY); y += 17;
                DrawCentered(document.ContractDocumentName, titleFont, new XSolidBrush(navy), y, 24); y += 25;
                DrawCentered($"Số: {document.ContractDocumentCode ?? $"HD-{document.ContractDocumentId}"}  ·  Phiên bản {version.ContractVersionVersionNo}", smallFont, new XSolidBrush(muted), y, 13); y += 20;
                var badge = new XRect(margin, y, innerWidth, 22);
                graphics.DrawRectangle(new XPen(border, 0.7), new XSolidBrush(pale), badge);
                DrawCentered("BẢN CHỐT HỢP ĐỒNG · CHỜ KÝ", smallFont, new XSolidBrush(teal), y + 4, 14);
                y += 34;
            }
            else
            {
                y = 34;
                graphics.DrawString(document.ContractDocumentName, labelFont, new XSolidBrush(navy),
                    new XRect(margin, y, innerWidth * .7, 14), XStringFormats.TopLeft);
                graphics.DrawString($"{document.ContractDocumentCode} · Phiên bản {version.ContractVersionVersionNo}", smallFont, new XSolidBrush(muted),
                    new XRect(margin + innerWidth * .7, y, innerWidth * .3, 14), XStringFormats.TopRight);
                y += 19;
                graphics.DrawLine(new XPen(border, 0.7), margin, y, margin + innerWidth, y);
                y += 18;
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
            var lineHeight = font.Size * 1.45;
            foreach (var line in Wrap(text, font, innerWidth - indent))
            {
                EnsureSpace(lineHeight + 2);
                if (line.Length > 0)
                    graphics.DrawString(line, font, brush, new XRect(margin + indent, y, innerWidth - indent, lineHeight + 2), XStringFormats.TopLeft);
                y += lineHeight;
            }
            y += spaceAfter;
        }

        void AddSectionTitle(string title)
        {
            EnsureSpace(24);
            graphics.DrawRectangle(new XSolidBrush(teal), margin, y + 2, 3, 13);
            graphics.DrawString(title, headingFont, new XSolidBrush(navy), new XRect(margin + 10, y, innerWidth - 10, 17), XStringFormats.TopLeft);
            y += 23;
        }

        string Field(string key, string fallback = "") => content.Fields.GetValueOrDefault(key)?.Trim() is { Length: > 0 } value ? value : fallback;
        var customerRows = new List<(string Label, string Value)>
        {
            ("Tên pháp lý", Field("CUSTOMER_COMPANY", customer.CustomerDisplayName)),
            ("Đại diện", Field("CUSTOMER_CONTACT_NAME", customer.CustomerDisplayName)),
            ("Chức vụ", Field("CUSTOMER_CONTACT_TITLE")),
            ("Địa chỉ", Field("CUSTOMER_ADDRESS")),
            ("Điện thoại", Field("CUSTOMER_CONTACT_PHONE", customer.CustomerPersonalPhone ?? string.Empty)),
            ("Email", Field("CUSTOMER_EMAIL", customer.CustomerEmail ?? string.Empty)),
            ("Mã số thuế", Field("CUSTOMER_TAX_CODE"))
        }.Where(item => !string.IsNullOrWhiteSpace(item.Value)).ToList();
        var providerRows = new List<(string Label, string Value)>
        {
            ("Tên pháp lý", Field("PROVIDER_LEGAL_NAME")),
            ("Đại diện", Field("PROVIDER_REPRESENTATIVE_NAME")),
            ("Chức vụ", Field("PROVIDER_REPRESENTATIVE_TITLE")),
            ("Địa chỉ", Field("PROVIDER_ADDRESS")),
            ("Điện thoại", Field("PROVIDER_PHONE")),
            ("Mã số thuế", Field("PROVIDER_TAX_CODE")),
            ("Tài khoản", string.Join(" · ", new[] { Field("PROVIDER_BANK_ACCOUNT_NUMBER"), Field("PROVIDER_BANK_NAME") }.Where(x => x.Length > 0)))
        }.Where(item => !string.IsNullOrWhiteSpace(item.Value)).ToList();

        double CardHeight(List<(string Label, string Value)> rows, double valueWidth) => 26 + rows.Sum(row => Math.Max(15, Wrap(row.Value, smallFont, valueWidth).Count * 10.5 + 4)) + 9;
        void DrawPartyCard(double x, double width, string title, List<(string Label, string Value)> rows, double cardHeight)
        {
            var card = new XRect(x, y, width, cardHeight);
            graphics.DrawRectangle(new XPen(border, 0.7), XBrushes.White, card);
            graphics.DrawRectangle(new XSolidBrush(pale), x, y, width, 22);
            graphics.DrawString(title, labelFont, new XSolidBrush(navy), new XRect(x + 9, y + 5, width - 18, 13), XStringFormats.TopLeft);
            var rowY = y + 29;
            foreach (var row in rows)
            {
                graphics.DrawString(row.Label, smallFont, new XSolidBrush(muted), new XRect(x + 9, rowY, 62, 13), XStringFormats.TopLeft);
                var lines = Wrap(row.Value, smallFont, width - 85);
                foreach (var line in lines)
                {
                    graphics.DrawString(line, smallFont, new XSolidBrush(navy), new XRect(x + 75, rowY, width - 84, 13), XStringFormats.TopLeft);
                    rowY += 10.5;
                }
                rowY += 4;
            }
        }

        void DrawItemsHeader(double[] columns)
        {
            var labels = new[] { "Hạng mục", "SL", "Đơn giá", "Thành tiền" };
            var x = margin;
            for (var i = 0; i < columns.Length; i++)
            {
                var rect = new XRect(x, y, columns[i], 23);
                graphics.DrawRectangle(new XPen(border, 0.6), new XSolidBrush(pale), rect);
                graphics.DrawString(labels[i], labelFont, new XSolidBrush(navy), new XRect(x + 6, y + 5, columns[i] - 12, 13), XStringFormats.TopLeft);
                x += columns[i];
            }
            y += 23;
        }

        NewPage(first: true);
        if (!string.IsNullOrWhiteSpace(Field("CONTRACT_LEGAL_BASES")))
        {
            AddSectionTitle("CĂN CỨ HỢP ĐỒNG");
            AddParagraph(Field("CONTRACT_LEGAL_BASES"), bodyFont, new XSolidBrush(navy));
        }

        AddSectionTitle("THÔNG TIN CÁC BÊN");
        var gap = 12d;
        var cardWidth = (innerWidth - gap) / 2;
        var cardValueWidth = cardWidth - 84;
        var cardsHeight = Math.Max(CardHeight(customerRows, cardValueWidth), CardHeight(providerRows, cardValueWidth));
        EnsureSpace(cardsHeight + 12);
        var cardTop = y;
        DrawPartyCard(margin, cardWidth, $"BÊN A · {Field("CUSTOMER_COMPANY", customer.CustomerDisplayName)}", customerRows, cardsHeight);
        DrawPartyCard(margin + cardWidth + gap, cardWidth, $"BÊN B · {Field("PROVIDER_LEGAL_NAME", "NHÀ CUNG CẤP")}", providerRows, cardsHeight);
        y = cardTop + cardsHeight + 13;
        AddParagraph($"Thời hạn hiệu lực: từ {content.EffectiveDate?.ToString("dd/MM/yyyy") ?? "—"} đến {content.ExpireDate?.ToString("dd/MM/yyyy") ?? "—"}.",
            smallFont, new XSolidBrush(muted), spaceAfter: 7);

        AddSectionTitle("HẠNG MỤC VÀ GIÁ TRỊ HỢP ĐỒNG");
        var columns = new[] { innerWidth * .42, innerWidth * .10, innerWidth * .22, innerWidth * .26 };
        DrawItemsHeader(columns);
        foreach (var item in content.Items)
        {
            var itemLines = Wrap(item.Name, bodyFont, columns[0] - 12);
            var rowHeight = Math.Max(24, itemLines.Count * 12 + 10);
            if (EnsureSpace(rowHeight)) DrawItemsHeader(columns);
            var values = new[] { item.Name, item.Quantity.ToString("0.####", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")),
                FormatMoney(item.UnitPrice), FormatMoney(item.Quantity * item.UnitPrice) };
            var x = margin;
            for (var i = 0; i < columns.Length; i++)
            {
                var rect = new XRect(x, y, columns[i], rowHeight);
                graphics.DrawRectangle(new XPen(border, 0.55), XBrushes.White, rect);
                var lines = i == 0 ? itemLines : Wrap(values[i], bodyFont, columns[i] - 12);
                var textY = y + (rowHeight - lines.Count * 12) / 2;
                foreach (var line in lines)
                {
                    graphics.DrawString(line, bodyFont, new XSolidBrush(navy), new XRect(x + 6, textY, columns[i] - 12, 13), XStringFormats.TopLeft);
                    textY += 12;
                }
                x += columns[i];
            }
            y += rowHeight;
        }

        var currency = string.IsNullOrWhiteSpace(version.ContractVersionCurrencycode) ? "VND" : version.ContractVersionCurrencycode;
        var totals = new[]
        {
            ("Tạm tính", content.Totals.Subtotal), ("Giảm giá", content.Totals.Discount),
            ($"VAT ({content.VatPercent:0.##}%)", content.Totals.Vat)
        };
        y += 7;
        foreach (var total in totals)
        {
            EnsureSpace(17);
            graphics.DrawString(total.Item1, smallFont, new XSolidBrush(muted), new XRect(margin + innerWidth * .55, y, innerWidth * .23, 14), XStringFormats.TopLeft);
            graphics.DrawString($"{FormatMoney(total.Item2)} {currency}", smallFont, new XSolidBrush(navy), new XRect(margin + innerWidth * .78, y, innerWidth * .22, 14), XStringFormats.TopRight);
            y += 16;
        }
        EnsureSpace(27);
        graphics.DrawLine(new XPen(border, 0.8), margin + innerWidth * .55, y, margin + innerWidth, y); y += 5;
        graphics.DrawString("TỔNG GIÁ TRỊ", labelFont, new XSolidBrush(navy), new XRect(margin + innerWidth * .55, y, innerWidth * .23, 18), XStringFormats.TopLeft);
        graphics.DrawString($"{FormatMoney(content.Totals.Total)} {currency}", new XFont("Arial", 11, XFontStyleEx.Bold), new XSolidBrush(teal),
            new XRect(margin + innerWidth * .78, y, innerWidth * .22, 18), XStringFormats.TopRight);
        y += 27;

        AddSectionTitle("ĐIỀU KHOẢN HỢP ĐỒNG");
        for (var i = 0; i < content.Terms.Count; i++)
        {
            var term = content.Terms[i];
            AddParagraph($"Điều {i + 1}. {term.Title}", labelFont, new XSolidBrush(navy), spaceAfter: 3);
            AddParagraph(term.Content, bodyFont, new XSolidBrush(navy), indent: 8, spaceAfter: 9);
        }

        const double signatureBlockHeight = 132;
        EnsureSpace(signatureBlockHeight);
        var signatureTop = y;
        AddSectionTitle("ĐẠI DIỆN CÁC BÊN KÝ TÊN");
        var signatureY = y - 4;
        var slotWidth = (innerWidth - 24) / 2;
        var customerX = margin;
        var companyX = margin + slotWidth + 24;
        void DrawSignatureCard(double x, string party, string name, string title)
        {
            var rect = new XRect(x, signatureY, slotWidth, 99);
            graphics.DrawRectangle(new XPen(border, 0.7), XBrushes.White, rect);
            graphics.DrawRectangle(new XSolidBrush(pale), x, signatureY, slotWidth, 21);
            graphics.DrawString(party, labelFont, new XSolidBrush(navy), new XRect(x + 8, signatureY + 4, slotWidth - 16, 13), XStringFormats.TopCenter);
            graphics.DrawString(name, labelFont, new XSolidBrush(navy), new XRect(x + 8, signatureY + 27, slotWidth - 16, 13), XStringFormats.TopCenter);
            if (!string.IsNullOrWhiteSpace(title))
                graphics.DrawString(title, smallFont, new XSolidBrush(muted), new XRect(x + 8, signatureY + 41, slotWidth - 16, 12), XStringFormats.TopCenter);
            graphics.DrawLine(new XPen(border, 0.8), x + 18, signatureY + 84, x + slotWidth - 18, signatureY + 84);
            graphics.DrawString("Ký và ghi rõ họ tên", smallFont, new XSolidBrush(muted), new XRect(x + 8, signatureY + 86, slotWidth - 16, 12), XStringFormats.TopCenter);
        }
        DrawSignatureCard(customerX, "ĐẠI DIỆN BÊN A · KHÁCH HÀNG", Field("CUSTOMER_CONTACT_NAME", customer.CustomerDisplayName), Field("CUSTOMER_CONTACT_TITLE"));
        DrawSignatureCard(companyX, "ĐẠI DIỆN BÊN B · CÔNG TY", Field("PROVIDER_REPRESENTATIVE_NAME"), Field("PROVIDER_REPRESENTATIVE_TITLE"));
        y = signatureTop + signatureBlockHeight;

        graphics.Dispose();
        for (var index = 0; index < pages.Count; index++)
        {
            using var footer = XGraphics.FromPdfPage(pages[index], XGraphicsPdfPageOptions.Append);
            footer.DrawLine(new XPen(border, 0.6), margin, pages[index].Height.Point - 31, pages[index].Width.Point - margin, pages[index].Height.Point - 31);
            footer.DrawString($"{document.ContractDocumentCode} · {document.ContractDocumentName}", smallFont, new XSolidBrush(muted),
                new XRect(margin, pages[index].Height.Point - 25, innerWidth - 60, 13), XStringFormats.TopLeft);
            footer.DrawString($"Trang {index + 1}/{pages.Count}", smallFont, new XSolidBrush(muted),
                new XRect(pages[index].Width.Point - margin - 60, pages[index].Height.Point - 25, 60, 13), XStringFormats.TopRight);
        }

        using var stream = new MemoryStream();
        pdf.Save(stream);
        return new RenderedContractPdf(stream.ToArray(), pages.Count, (int)(companyX + 27), (int)(customerX + 27),
            (int)(signatureY + 38), 174, 42);
    }

    private static string FormatMoney(decimal amount) => amount.ToString("#,##0.##", System.Globalization.CultureInfo.GetCultureInfo("vi-VN"));

    internal sealed record RenderedContractPdf(byte[] Content, int SignaturePage, int CompanySignatureX, int CustomerSignatureX,
        int SignatureY, int SignatureWidth, int SignatureHeight);

    private static string? Clean(string? input, int max) => string.IsNullOrWhiteSpace(input) ? null : input.Trim()[..Math.Min(input.Trim().Length, max)];
    private static void RequireManager(InternalUser actor) { if (!actor.CanManageAll) throw new InternalAppException(403, "Chỉ quản trị viên công ty được quản lý luồng ký."); }
    private static byte[] DecodeSignature(string input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 1_400_000) throw new ArgumentException("Ảnh chữ ký PNG không hợp lệ.");
        var comma = input.IndexOf(',');
        var data = comma >= 0 ? input[(comma + 1)..] : input;
        try { return Convert.FromBase64String(data); } catch (FormatException) { throw new ArgumentException("Ảnh chữ ký PNG không hợp lệ."); }
    }
}

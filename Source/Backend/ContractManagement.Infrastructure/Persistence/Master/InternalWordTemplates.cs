using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalWordTemplates(MasterDbContext db, InternalTemplateStorage storage, InternalWordDocument word,
    IDataProtectionProvider protection, IMemoryCache? previewCache = null)
{
    private readonly IMemoryCache cache = previewCache ?? new MemoryCache(new MemoryCacheOptions());
    private readonly IDataProtector protector = protection.CreateProtector("InternalWordTemplates.Preview.v1");
    private sealed record Receipt(long Actor, string Fingerprint, string SourceHash, string DocumentHash, DateTime Expires);
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string Fingerprint(WordTemplateInput input) => Hash(JsonSerializer.SerializeToUtf8Bytes(input));
    private static DraftField[] CoreFields => DraftCompletionPolicy.PartyFields.Concat(
        DraftCompletionPolicy.ComputedKeys.Select(key => new DraftField(key, key))).ToArray();

    public async Task<WordExtraction> Extract(TemplateFileInput input, long actor, CancellationToken ct)
    {
        var document = await word.Normalize(input, ct);
        var blocks = InternalWordDocument.Blocks(document, true);
        if (blocks.Sum(b => b.Text.Length) > 100000 || blocks.Count > 2000) throw new ArgumentException("Mẫu quá lớn để chỉnh sửa.");
        // A signed normalization receipt prevents replacing the converted document with an unrelated file.
        var receipt = new Receipt(actor, input.FileName, Hash(InternalWordDocument.Decode(input.Base64)), Hash(document), DateTime.UtcNow.AddHours(8));
        return new(Path.GetFileNameWithoutExtension(input.FileName), Convert.ToBase64String(document), blocks, CoreFields.ToList())
        { SourceToken = protector.Protect(JsonSerializer.Serialize(receipt)) };
    }

    private Receipt ReadReceipt(string token, long actor)
    {
        try
        {
            var receipt = JsonSerializer.Deserialize<Receipt>(protector.Unprotect(token));
            if (receipt is null || receipt.Actor != actor || receipt.Expires < DateTime.UtcNow)
                throw new ArgumentException("Phiên xem trước đã hết hạn. Hãy xem trước lại.");
            return receipt;
        }
        catch (CryptographicException) { throw new ArgumentException("Xác nhận xem trước không hợp lệ."); }
    }

    private byte[] Prepare(WordTemplateInput input, long actor)
    {
        var receipt = ReadReceipt(input.SourceToken, actor);
        var original = InternalWordDocument.Decode(input.Base64);
        var document = InternalWordDocument.Decode(input.DocumentBase64);
        if (receipt.Fingerprint != input.FileName || receipt.SourceHash != Hash(original) || receipt.DocumentHash != Hash(document))
            throw new ArgumentException("File đã thay đổi. Hãy tải file lên lại.");
        if (input.DocumentType is < 1 or > 5 || string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 500)
            throw new ArgumentException("Nhập tên và chọn loại mẫu hợp lệ.");
        if (input.Fields is null || input.Blocks is null || input.SampleValues is null
            || input.Fields.Any(f => f is null || CoreFields.Any(c => c.Key == f.Key))
            || input.Blocks.Any(b => b is null || b.Text is null || b.Location is null)
            || input.SampleValues.Any(p => p.Value is null))
            throw new ArgumentException("Trường mở rộng trùng trường lõi hoặc dữ liệu không hợp lệ.");
        var fields = DraftCompletionPolicy.PartyFields.Concat(input.Fields).ToList();
        InternalWordDocument.Validate(document);
        var edited = InternalWordDocument.Edit(document, input.Blocks, input.Tables, input.Additions, input.BodyOrder);
        var terms = InternalWordDocument.Blocks(edited).Select(b => new DraftTerm(b.Id, b.Location, b.Text)).ToList();
        // Validate field definitions using the existing policy, then scan every paragraph including headers/tables.
        DraftCompletionPolicy.ValidateTemplate(new(0, input.Name, "", false, fields, [new("BODY", "Nội dung", "")]));
        if (!input.ManualContent) DraftCompletionPolicy.ValidatePlaceholders(terms, fields);
        if (!terms.Any(t => !string.IsNullOrWhiteSpace(t.Content))) throw new ArgumentException("Mẫu phải có nội dung.");
        var allowed = fields.Select(f => f.Key).Concat(DraftCompletionPolicy.ComputedKeys).ToHashSet();
        if (input.SampleValues.Any(p => !allowed.Contains(p.Key) || p.Value.Length > 4000))
            throw new ArgumentException("Dữ liệu thử không hợp lệ.");
        foreach (var field in fields)
        {
            var value = input.SampleValues.GetValueOrDefault(field.Key, "");
            if (string.IsNullOrWhiteSpace(value)) continue;
            if (field.DataType == "NUMBER" && (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) || n < field.Min || n > field.Max)
                || field.DataType == "DATE" && !DateOnly.TryParseExact(value, "yyyy-MM-dd", out _)
                || field.DataType == "SELECT" && !(field.Options?.Contains(value) ?? false))
                throw new ArgumentException("Dữ liệu thử sai kiểu: " + field.Label);
        }
        return edited;
    }

    public async Task<WordPreview> Preview(WordTemplateInput input, long actor, CancellationToken ct)
    {
        var document = Prepare(input, actor);
        var sample = new Dictionary<string, string>
        {
            ["CONTRACT_NUMBER"] = "HD-20261117-000001",
            ["VAT"] = "100.000",
            ["TOTAL_AMOUNT"] = "1.100.000",
            ["PAYMENT_METHOD"] = "Chuyển khoản",
            ["PAYMENT_SCHEDULE"] = "Đợt 1: 1.100.000 VND — 5 ngày lịch sau ngày ký đủ"
        };
        var previewDocument = !input.ManualContent
            ? InternalWordDocument.Fill(document, input.SampleValues)
            : input.DocumentType == 1 && InternalWordDocument.Blocks(document).Any(block =>
                sample.Keys.Any(key => block.Text.Contains("{{" + key + "}}", StringComparison.Ordinal)))
                ? InternalWordDocument.Fill(document, sample, preserveUnknown: true)
                : document;
        var pdf = await word.Pdf(previewDocument, ct);
        var receipt = new Receipt(actor, Fingerprint(input), Hash(InternalWordDocument.Decode(input.Base64)), Hash(document), DateTime.UtcNow.AddHours(2));
        var token = protector.Protect(JsonSerializer.Serialize(receipt));
        return new(Convert.ToBase64String(pdf), token);
    }

    public async Task<TemplateDefinition> Activate(WordActivation request, long actor, CancellationToken ct)
    {
        var input = request.Input;
        if (input.DocumentType == 1) ContractChecklistService.ValidateRules(input.ChecklistRules);
        var receipt = ReadReceipt(request.PreviewToken, actor);
        if (receipt.Fingerprint != Fingerprint(input)) throw new ArgumentException("Nội dung đã thay đổi. Hãy xem trước lại trước khi kích hoạt.");
        var document = Prepare(input, actor);
        if (receipt.DocumentHash != Hash(document)) throw new ArgumentException("Bản mẫu không khớp với bản xem trước.");
        // Deterministic code makes a repeated activation of the same preview idempotent.
        var code = "WORD_" + Hash(Encoding.UTF8.GetBytes(request.PreviewToken))[..40];
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var existing = await db.ContractTemplates.SingleOrDefaultAsync(t => t.ContractTemplateCode == code, ct);
            if (existing is not null)
            {
                var metadata = await db.FileStorages.SingleAsync(f => f.FileStorageObjecttype == "INTERNAL_TEMPLATE" && f.FileStorageObjectid == existing.ContractTemplateId, ct);
                return await storage.Read(metadata, ct);
            }
            var paths = new List<string>();
            var commitAttempted = false;
            try
            {
                var template = new ContractTemplate { ContractTemplateCode = code, ContractTemplateName = input.Name.Trim(),
                    ContractTemplateDocumentType = input.DocumentType, ContractTemplateLanguageMode = 1, IsActive = true,
                    CreatedEmployeeId = checked((int)actor), CreatedDate = DateTime.UtcNow };
                db.ContractTemplates.Add(template);
                await db.SaveChangesAsync(ct);
                Directory.CreateDirectory(storage.Root);
                if (input.DocumentType == 1)
                    db.Set<ContractChecklist>().Add(new() { TemplateId = template.ContractTemplateId, Configured = true,
                        Rules = JsonSerializer.Serialize(input.ChecklistRules) });
                async Task<string> Store(byte[] bytes, string extension, string objectType, string mime, string filename)
                {
                    var key = Guid.NewGuid().ToString("N") + extension;
                    var path = Path.Combine(storage.Root, key);
                    paths.Add(path);
                    await File.WriteAllBytesAsync(path, bytes, ct);
                    db.FileStorages.Add(new FileStorage { FileStorageObjecttype = objectType, FileStorageObjectid = template.ContractTemplateId,
                        FileStorageFiletype = objectType, FileStorageFilename = filename, FileStorageFilepath = key, FileStorageStoragekey = key,
                        FileStorageContenttype = mime, FileStorageFilesize = bytes.Length, FileStorageSha256 = Hash(bytes),
                        FileStorageUploadedbyemployeeid = actor, FileStorageUploadeddate = DateTime.UtcNow });
                    return key;
                }
                var ext = Path.GetExtension(input.FileName).ToLowerInvariant();
                await Store(InternalWordDocument.Decode(input.Base64), ext, "INTERNAL_TEMPLATE_SOURCE", ext == ".doc" ? "application/msword" : InternalWordDocument.Mime, Path.GetFileName(input.FileName));
                var key = await Store(document, ".docx", "INTERNAL_TEMPLATE_WORD", InternalWordDocument.Mime, input.Name + ".docx");
                var fields = DraftCompletionPolicy.PartyFields.Concat(input.Fields).ToList();
                var blocks = InternalWordDocument.Blocks(document);
                var terms = Sections(blocks);
                // A draft holds at most 100 terms: a file with more sections falls back to consecutive 20,000-character bodies.
                if (terms.Count > 100)
                {
                    var grouped = new List<DraftTerm>();
                    foreach (var term in blocks.Where(b => !string.IsNullOrWhiteSpace(b.Text))
                        .Select(b => new DraftTerm("WORD_" + b.Id, b.Location, InternalDraftPolicy.Text(b.Text, 20000), false)))
                    {
                        if (grouped.Count == 0 || grouped[^1].Content.Length + term.Content.Length + 1 > 20000)
                            grouped.Add(new("WORD_BODY_" + grouped.Count, "Nội dung Word " + (grouped.Count + 1), term.Content, false));
                        else grouped[^1] = grouped[^1] with { Content = grouped[^1].Content + "\n" + term.Content };
                    }
                    terms = grouped;
                }
                var definition = new TemplateDefinition(template.ContractTemplateId, template.ContractTemplateName, code, false,
                    fields, terms, true, input.DocumentType, key, Hash(document), input.ManualContent);
                await Store(JsonSerializer.SerializeToUtf8Bytes(definition, InternalRepository.Json), ".json", "INTERNAL_TEMPLATE", "application/json", input.Name + ".json");
                foreach (var field in fields) db.ContractTemplateFields.Add(new() { ContractTemplateId = template.ContractTemplateId,
                    ContractTemplateFieldKey = field.Key, ContractTemplateFieldLabel = field.Label, ContractTemplateFieldDataType = field.DataType,
                    IsRequired = field.Required, ExtraOptions = JsonSerializer.Serialize(new { field.Options, field.Min, field.Max }, InternalRepository.Json) });
                db.AuditLogs.Add(new() { ActionType = "CREATE", EntityName = "CONTRACT_TEMPLATE", EntityId = template.ContractTemplateId.ToString(),
                    ActorType = "EMPLOYEE", ActorId = actor, CreatedAt = DateTime.UtcNow,
                    NewData = JsonSerializer.Serialize(new { checklistRules = input.ChecklistRules }) });
                await db.SaveChangesAsync(ct);
                commitAttempted = true;
                await tx.CommitAsync(ct);
                return definition;
            }
            catch
            {
                // A lost commit response can mean the rows are already durable. Verify before removing their files.
                if (commitAttempted)
                {
                    await tx.DisposeAsync();
                    db.ChangeTracker.Clear();
                    var committed = await db.ContractTemplates.AsNoTracking().SingleOrDefaultAsync(t => t.ContractTemplateCode == code, CancellationToken.None);
                    if (committed is not null)
                    {
                        var metadata = await db.FileStorages.AsNoTracking().SingleAsync(f => f.FileStorageObjecttype == "INTERNAL_TEMPLATE"
                            && f.FileStorageObjectid == committed.ContractTemplateId, CancellationToken.None);
                        return await storage.Read(metadata, CancellationToken.None);
                    }
                }
                foreach (var path in paths) { try { File.Delete(path); } catch (IOException) { } }
                throw;
            }
        });
    }

    /// <summary>One term per section of the Word file: each heading (Điều N, an all-caps title, a Word heading style) starts
    /// a term titled by it; text before the first heading and header/footer text form their own terms. Long sections continue.</summary>
    public static List<DraftTerm> Sections(IEnumerable<WordBlock> blocks)
    {
        var terms = new List<DraftTerm>();
        var group = "";
        foreach (var block in blocks)
        {
            var text = block.Text.Trim();
            if (text.Length == 0) continue;
            var location = block.Location.Split(" · ")[0];
            if (block.Heading)
            {
                // Consecutive title lines ("HỢP ĐỒNG" / "CUNG CẤP PHẦN MỀM") form one title rather than empty terms.
                var heading = Regex.Replace(text, @"\s+", " ");
                var merge = terms.Count > 0 && group == location && terms[^1].Content.Length == 0 && terms[^1].Title != "Phần mở đầu"
                    && terms[^1].Title.Length + heading.Length < 200;
                if (merge) heading = terms[^1].Title + " " + heading;
                var title = heading.Length <= 200 ? heading : heading[..200] + "…";
                var term = new DraftTerm(merge ? terms[^1].Key : "WORD_" + block.Id, title, title == heading ? "" : InternalDraftPolicy.Text(text, 20000), false);
                if (merge) terms[^1] = term; else terms.Add(term);
                group = location;
                continue;
            }
            if (location != group || terms.Count == 0)
            {
                terms.Add(new("WORD_" + block.Id, location == "Nội dung" ? "Phần mở đầu" : location, "", false));
                group = location;
            }
            var last = terms[^1];
            if (last.Content.Length + text.Length + 1 > 20000)
                terms.Add(new("WORD_" + block.Id, last.Title.TrimEnd('…') + " (tiếp)", InternalDraftPolicy.Text(text, 20000), false));
            else terms[^1] = last with { Content = last.Content.Length == 0 ? text : last.Content + "\n" + text };
        }
        return terms;
    }

    public async Task<WordPreview> EditorPreview(WordEditorPreview input, TemplateDefinition source, long? draftId, long actor, CancellationToken ct)
    {
        var extracted = ReadReceipt(input.Document.SourceToken, actor);
        if (!string.Equals(extracted.SourceHash, source.WordSha256, StringComparison.OrdinalIgnoreCase))
            throw new InternalAppException(409, "Nguồn Word đã thay đổi.");
        var bytes = Prepare(input.Document, actor);
        input.Content.Terms = source.Terms.ToList();
        var pdf = await RenderBytes(input.Content, source with { WordSha256 = Hash(bytes) }, bytes, true, ct);
        var token = protector.Protect(JsonSerializer.Serialize(new Receipt(actor, DraftSignature(input.Content, source, draftId), extracted.SourceHash, Hash(bytes), DateTime.UtcNow.AddMinutes(30))));
        cache.Set("draft-word:" + token, bytes, TimeSpan.FromMinutes(30));
        return new(Convert.ToBase64String(pdf), token);
    }

    private static string DraftSignature(DraftContent content, TemplateDefinition template, long? draftId) =>
        Hash(JsonSerializer.SerializeToUtf8Bytes(new { draftId, template.Id, Values = PrintValues(content, template).OrderBy(p => p.Key, StringComparer.Ordinal) }));

    internal byte[] ReviewedDocument(string token, long actor, TemplateDefinition template, long? draftId, DraftContent content)
    {
        var receipt = ReadReceipt(token, actor);
        if (receipt.Fingerprint != DraftSignature(content, template, draftId)) throw new ArgumentException("Thông tin hợp đồng đã thay đổi. Cập nhật lại bản xem trước.");
        if (template.WordStorageKey == null || !string.Equals(template.WordSha256, receipt.SourceHash, StringComparison.OrdinalIgnoreCase))
            throw new InternalAppException(409, "Nguồn Word đã thay đổi.");
        if (!cache.TryGetValue("draft-word:" + token, out byte[]? bytes) || bytes == null || Hash(bytes) != receipt.DocumentHash)
            throw new ArgumentException("Cập nhật lại bản xem trước trước khi lưu.");
        return bytes;
    }

    public async Task SaveDraftWord(long id, WordDraftSave request, InternalUser actor, CancellationToken ct)
    {
        if (!actor.CanCheck && !actor.CanWrite) throw new InternalAppException(403, "Bạn không có quyền sửa Word.");
        var receipt = ReadReceipt(request.Document.PreviewToken, actor.Id);
        if (receipt.Fingerprint != Fingerprint(request.Document.Input)) throw new ArgumentException("Hãy xem trước lại nội dung đã sửa.");
        var bytes = Prepare(request.Document.Input, actor.Id);
        if (receipt.DocumentHash != Hash(bytes)) throw new ArgumentException("Nội dung không khớp bản đã xem trước.");
        var key = Guid.NewGuid().ToString("N") + ".docx";
        var path = Path.Combine(storage.Root, key);
        Directory.CreateDirectory(storage.Root);
        await File.WriteAllBytesAsync(path, bytes, ct);
        try
        {
            await ReviewData.InTransaction(db, async () =>
            {
                var doc = await db.ContractDocuments.SingleOrDefaultAsync(d => d.ContractDocumentId == id && d.TenantId == null, ct)
                    ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
                if (!actor.CanEditDraftTerms(doc.EmployeeId)) throw new InternalAppException(403, "Bạn không có quyền sửa Word của nháp này.");
                var version = await db.ContractVersions.SingleAsync(v => v.ContractVersionId == doc.ContractDocumentCurrentVersionId, ct);
                ReviewData.Expect(doc.RowVersion, request.RowVersion);
                ReviewData.Expect(version.ContractVersionRowversion, request.VersionRowVersion);
                if (!ContractReviewPolicy.CanEdit(doc.ContractDocumentStatus, version.ContractVersionIslocked)) throw new InternalAppException(409, "Trả hồ sơ về chỉnh sửa trước khi sửa Word.");
                var snapshot = await db.ContractDynamicValues.SingleAsync(x => x.ContractDocumentId == id && x.ContractVersionId == version.ContractVersionId && x.FieldKey == InternalRepository.SnapshotKey, ct);
                var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, InternalRepository.Json)!;
                var template = content.TemplateSnapshot ?? throw new InternalAppException(409, "Thiếu bản chụp mẫu.");
                if (template.WordStorageKey is null || !string.Equals(template.WordSha256, receipt.SourceHash, StringComparison.OrdinalIgnoreCase))
                    throw new InternalAppException(409, "Bản Word đã thay đổi. Mở lại trình sửa để lấy bản mới.");
                var terms = Sections(InternalWordDocument.Blocks(bytes));
                content.TemplateSnapshot = template with { WordStorageKey = key, WordSha256 = Hash(bytes), ManualWordContent = true, Terms = terms };
                content.Terms = terms;
                var storedTerms = await db.ContractTerms.Where(t => t.ContractDocumentId == id && t.ContractVersionId == version.ContractVersionId).ToListAsync(ct);
                foreach (var term in terms)
                {
                    var stored = storedTerms.FirstOrDefault(t => t.ContractTermTermcode == term.Key);
                    if (stored is null)
                    {
                        stored = new() { ContractDocumentId = id, ContractVersionId = version.ContractVersionId, ContractTermTermcode = term.Key, CreatedAt = DateTime.UtcNow };
                        db.ContractTerms.Add(stored);
                    }
                    stored.ContractTermTermtitle = term.Title;
                    stored.ContractTermTermcontent = term.Content;
                    stored.ContractTermDisplayorder = terms.IndexOf(term);
                    stored.ContractTermIsnegotiable = true;
                }
                foreach (var removed in storedTerms.Where(t => terms.All(x => x.Key != t.ContractTermTermcode)))
                {
                    removed.ContractTermTermcontent = "";
                    removed.ContractTermTermtitle = "Nội dung đã xóa — chọn lại liên kết thanh toán";
                }
                DraftValidation.Recheck(content);
                snapshot.FieldValue = JsonSerializer.Serialize(content, InternalRepository.Json);
                var now = DateTime.UtcNow;
                doc.UpdatedDate = now;
                version.ContractVersionChangenote = "Sửa nội dung Word " + now.ToString("O");
                db.FileStorages.Add(new() { FileStorageObjecttype = "CONTRACT_VERSION", FileStorageObjectid = version.ContractVersionId,
                    FileStorageStoragekey = key, FileStorageSha256 = Hash(bytes), FileStorageContenttype = InternalWordDocument.Mime, FileStorageFilesize = bytes.Length,
                    FileStorageFilename = request.Document.Input.Name + ".docx", FileStorageFilepath = key, FileStorageFiletype = "DOCX",
                    FileStorageUploadedbyemployeeid = actor.Id, FileStorageUploadeddate = now });
                ReviewData.Audit(db, "EDIT_WORD", "CONTRACT_DOCUMENTS", id, "EMPLOYEE", actor.Id, now);
                await db.SaveChangesAsync(ct);
                return true;
            }, ct);
        }
        catch
        {
            db.ChangeTracker.Clear();
            if (!await db.FileStorages.AnyAsync(f => f.FileStorageStoragekey == key, CancellationToken.None)) File.Delete(path);
            throw;
        }
    }

    public async Task<byte[]> CustomerWord(long reviewId, long customerId, bool pdf, CancellationToken ct)
    {
        var review = await db.ContractReviewRequests.AsNoTracking().SingleOrDefaultAsync(r => r.ContractReviewRequestId == reviewId && r.CustomerId == customerId, ct)
            ?? throw new InternalAppException(404, "Không tìm thấy hợp đồng.");
        var snapshot = await db.ContractDynamicValues.AsNoTracking().SingleAsync(x => x.ContractDocumentId == review.ContractDocumentId && x.ContractVersionId == review.ContractVersionId && x.FieldKey == InternalRepository.SnapshotKey, ct);
        if (SnapshotHash.Compute(snapshot.FieldValue) != review.ContractReviewRequestDocumentHash) throw new InternalAppException(409, "Nội dung không khớp bản đã gửi.");
        var content = JsonSerializer.Deserialize<DraftContent>(snapshot.FieldValue, InternalRepository.Json)!;
        return await Render(content, content.TemplateSnapshot!, pdf, ct);
    }

    public async Task<byte[]> ReadWord(TemplateDefinition template, CancellationToken ct)
    {
        if (template.WordStorageKey is null || Path.GetFileName(template.WordStorageKey) != template.WordStorageKey)
            throw new ArgumentException("Mẫu này không có bản Word.");
        var bytes = await File.ReadAllBytesAsync(Path.Combine(storage.Root, template.WordStorageKey), ct);
        if (Hash(bytes) != template.WordSha256) throw new ArgumentException("File mẫu không khớp bản đã lưu.");
        return bytes;
    }
    public async Task<byte[]> PreviewStoredWord(TemplateDefinition template, bool pdf, CancellationToken ct)
    {
        var bytes = await ReadWord(template, ct);
        return pdf ? await word.Pdf(bytes, ct) : bytes;
    }
    public async Task<byte[]> Render(DraftContent content, TemplateDefinition template, bool pdf, CancellationToken ct)
    {
        return await RenderBytes(content, template, await ReadWord(template, ct), pdf, ct);
    }

    private async Task<byte[]> RenderBytes(DraftContent content, TemplateDefinition template, byte[] bytes, bool pdf, CancellationToken ct)
    {
        InternalDraftPolicy.Prepare(content, template, forPreview: true);
        var values = PrintValues(content, template);
        // Manual Word content keeps its own text; only placeholders that have a draft value are filled.
        var filled = InternalWordDocument.Fill(bytes, values, preserveUnknown: template.ManualWordContent);
        if (!pdf) return filled;
        // The print depends only on the placeholders the file uses: an edit to anything else reuses the last PDF of this
        // template instead of starting LibreOffice (~2-3 s). One entry per template keeps memory bounded while typing.
        var used = cache.GetOrCreate("word-placeholders:" + template.WordSha256, entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromHours(1);
            return InternalWordDocument.Blocks(bytes).SelectMany(b => Placeholder.Matches(b.Text).Select(m => m.Groups[1].Value)).ToHashSet();
        })!;
        var signature = template.ManualWordContent + "|" + string.Join("\u0001", used.Order().Select(k => k + "\u0002" + values.GetValueOrDefault(k)));
        var lastKey = "word-pdf-last:" + template.WordSha256;
        if (cache.TryGetValue(lastKey, out (string Signature, byte[] Pdf) last) && last.Signature == signature) return last.Pdf;
        var result = await word.Pdf(filled, ct);
        cache.Set(lastKey, (signature, result), new MemoryCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(10) });
        return result;
    }
    private static Dictionary<string,string> PrintValues(DraftContent content, TemplateDefinition template)
    {
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        var legacy = content.ContractNumber is null && content.PaymentMethod is null;
        string Money(decimal amount) => amount.ToString(legacy ? "N2" : "#,0.##", culture);
        string Due(DraftMilestone milestone) => milestone.Anchor == "DATE"
            ? milestone.DueDate?.ToString("dd/MM/yyyy", culture) ?? "Chưa chọn ngày"
            : $"{milestone.OffsetDays} ngày lịch sau {(milestone.Anchor == "SIGNED" ? "ngày ký đủ" : "ngày bàn giao")}";
        return new Dictionary<string, string>(template.ManualWordContent
            ? content.Fields.Where(field => field.Key.StartsWith("CUSTOMER_", StringComparison.Ordinal))
            : content.Fields)
        {
            ["CONTRACT_NAME"] = content.Name, ["EFFECTIVE_DATE"] = content.EffectiveDate?.ToString("dd/MM/yyyy") ?? "",
            ["EXPIRE_DATE"] = content.ExpireDate?.ToString("dd/MM/yyyy") ?? "",
            ["CONTRACT_NUMBER"] = content.ContractNumber ?? "",
            ["PAYMENT_METHOD"] = InternalDraftPolicy.PaymentMethodLabel(content.PaymentMethod),
            ["SUBTOTAL"] = Money(content.Totals.Subtotal), ["DISCOUNT"] = Money(content.Totals.Discount),
            ["VAT"] = Money(content.Totals.Vat), ["TOTAL_AMOUNT"] = Money(content.Totals.Total),
            ["CONTRACT_TOTAL_AMOUNT"] = Money(content.Totals.Total), ["CONTRACT_CURRENCY"] = "VND",
            ["PAYMENT_SCHEDULE"] = string.Join("; ", content.Milestones.Select(m =>
                legacy ? $"{m.Title}: {Money(m.Amount)}"
                    : $"{m.Title}: {Money(m.Amount)} VND — {Due(m)}{(string.IsNullOrEmpty(m.Condition) ? "" : "; " + m.Condition)}"))
        };
    }
    private static readonly Regex Placeholder = new(@"\{\{([A-Z][A-Z0-9_]*)\}\}", RegexOptions.Compiled);
}

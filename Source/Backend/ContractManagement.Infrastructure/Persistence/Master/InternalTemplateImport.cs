using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Entities.Master;
using ContractManagement.Domain.Internal;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalTemplateImport(MasterDbContext db, InternalTemplateStorage storage) : IInternalTemplateImport
{
    private const int MaxBytes = 10 * 1024 * 1024;
    public TemplateExtraction Extract(TemplateFileInput input)
    {
        var bytes = Decode(input);
        try
        {
            string text;
            if (Path.GetExtension(input.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                if (!bytes.AsSpan().StartsWith("%PDF-"u8)) throw new ArgumentException("File không phải PDF hợp lệ.");
                using var pdf = PdfDocument.Open(bytes);
                if (pdf.NumberOfPages > 100) throw new ArgumentException("Chỉ hỗ trợ tối đa 100 trang mỗi mẫu.");
                var pages = pdf.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)).ToList();
                text = string.Join("\n\n", pages);
                if (text.Length > 100000) throw new ArgumentException("Nội dung mẫu vượt 100.000 ký tự.");
                return new(Path.GetFileNameWithoutExtension(input.FileName), text.Trim(),
                    pages.Any(string.IsNullOrWhiteSpace) ? "Có trang không đọc được chữ (có thể là bản scan). Hãy bổ sung và kiểm tra nội dung đầy đủ trước khi lưu." : null);
            }
            using var stream = new MemoryStream(bytes);
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                if (zip.Entries.Sum(e => e.Length) > 50 * 1024 * 1024 || zip.Entries.Count > 2000)
                    throw new ArgumentException("File Word giải nén quá lớn.");
                if (zip.Entries.Any(e => e.FullName?.Contains("vbaProject", StringComparison.OrdinalIgnoreCase) == true))
                    throw new ArgumentException("Không hỗ trợ file chứa macro.");
            }
            stream.Position = 0;
            using var word = WordprocessingDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = 2_000_000 });
            var body = word.MainDocumentPart?.Document?.Body ?? throw new ArgumentException("File Word không có nội dung.");
            text = string.Join("\n", body.Descendants<Paragraph>().Select(p => string.Concat(p.Descendants<Text>().Select(t => t.Text))));
            if (text.Length > 100000) throw new ArgumentException("Nội dung mẫu vượt 100.000 ký tự.");
            return new(Path.GetFileNameWithoutExtension(input.FileName), text.Trim(),
                "Chỉ lấy nội dung chữ; bảng, ảnh, header/footer và bố cục gốc cần kiểm tra lại. Preview dùng bố cục văn bản của hệ thống.");
        }
        catch (ArgumentException) { throw; }
        catch (Exception e) when (e is not OperationCanceledException)
        { throw new ArgumentException("Không đọc được file. Hãy dùng DOCX/PDF hợp lệ, không đặt mật khẩu."); }
    }

    private static byte[] Decode(TemplateFileInput input)
    {
        var ext = Path.GetExtension(input.FileName ?? "").ToLowerInvariant();
        if (ext is not ".docx" and not ".pdf") throw new ArgumentException("Chọn Word (.docx) hoặc PDF. File .doc cần lưu lại thành .docx.");
        if (string.IsNullOrWhiteSpace(input.Base64) || input.Base64.Length > (MaxBytes + 2L) / 3 * 4)
            throw new ArgumentException("File phải có nội dung và không quá 10 MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(input.Base64); } catch (FormatException) { throw new ArgumentException("Dữ liệu file không hợp lệ."); }
        if (bytes.Length == 0 || bytes.Length > MaxBytes) throw new ArgumentException("File không được vượt 10 MB.");
        return bytes;
    }

    public async Task<TemplateDefinition> Save(TemplateImportInput input, long actor, CancellationToken ct)
    {
        // Validate the original again: preview is not a trusted validation boundary.
        Extract(new(input.FileName, input.Base64));
        var name = InternalDraftPolicy.Text(input.Name, 200);
        var content = InternalDraftPolicy.Text(input.Content, 100000);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Nhập tên mẫu và nội dung đã kiểm tra.");
        var original = Decode(new(input.FileName, input.Base64));
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var files = new List<string>();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var template = new ContractTemplate { ContractTemplateCode = "UPLOAD_" + Guid.NewGuid().ToString("N"), ContractTemplateName = name,
                    ContractTemplateDescription = "Mẫu tải lên; người soạn đã kiểm tra nội dung, chưa xác nhận phê duyệt của công ty.",
                    ContractTemplateDocumentType = 1, ContractTemplateLanguageMode = 1, IsActive = true, CreatedEmployeeId = checked((int)actor), CreatedDate = DateTime.UtcNow };
                db.ContractTemplates.Add(template); await db.SaveChangesAsync(ct);
                var terms = new List<DraftTerm>();
                for (var offset = 0; offset < content.Length; offset += 18000)
                    terms.Add(new("IMPORT_" + terms.Count, "Nội dung mẫu — phần " + (terms.Count + 1), content.Substring(offset, Math.Min(18000, content.Length - offset))));
                // Keep customer identity in the saved snapshot for the draft list; imported text is edited explicitly.
                if (input.Terms is { Count: > 0 }) terms = input.Terms.Select(t => t with { Key = InternalDraftPolicy.Text(t.Key, 100), Title = InternalDraftPolicy.Text(t.Title, 500), Content = InternalDraftPolicy.Text(t.Content, 20000) }).ToList();
                if (terms.Any(t => string.IsNullOrWhiteSpace(t.Key) || string.IsNullOrWhiteSpace(t.Title) || string.IsNullOrWhiteSpace(t.Content)) || terms.Select(t => t.Key).Distinct().Count() != terms.Count) throw new ArgumentException("Điều khoản cần mã duy nhất, tiêu đề và nội dung.");
                var fields = DraftCompletionPolicy.PartyFields.Concat(input.Fields ?? []).ToList();
                var definition = new TemplateDefinition(template.ContractTemplateId, name, "upload-v1-" + Guid.NewGuid().ToString("N"), false, fields, terms, true);
                DraftCompletionPolicy.ValidateTemplate(definition);
                Directory.CreateDirectory(storage.Root);
                async Task Store(byte[] bytes, string extension, string objectType, string mime)
                {
                    var key = Guid.NewGuid().ToString("N") + extension;
                    var path = Path.Combine(storage.Root, key); files.Add(path);
                    await File.WriteAllBytesAsync(path, bytes, ct);
                    db.FileStorages.Add(new FileStorage { FileStorageObjecttype = objectType, FileStorageObjectid = template.ContractTemplateId,
                        FileStorageFiletype = extension == ".json" ? "TEMPLATE_JSON" : "TEMPLATE_SOURCE", FileStorageFilename = key,
                        FileStorageFilepath = key, FileStorageStoragekey = key, FileStorageContenttype = mime, FileStorageFilesize = bytes.Length,
                        FileStorageSha256 = Convert.ToHexString(SHA256.HashData(bytes)), FileStorageUploadedbyemployeeid = actor, FileStorageUploadeddate = DateTime.UtcNow });
                }
                await Store(original, Path.GetExtension(input.FileName).ToLowerInvariant(), "INTERNAL_TEMPLATE_SOURCE", Path.GetExtension(input.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf" : "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
                await Store(JsonSerializer.SerializeToUtf8Bytes(definition, InternalRepository.Json), ".json", "INTERNAL_TEMPLATE", "application/json");
                foreach (var field in fields) db.ContractTemplateFields.Add(new() { ContractTemplateId = template.ContractTemplateId, ContractTemplateFieldKey = field.Key,
                    ContractTemplateFieldLabel = field.Label, ContractTemplateFieldDataType = field.DataType, IsRequired = field.Required,
                    ExtraOptions = JsonSerializer.Serialize(new { field.Options, field.Min, field.Max }, InternalRepository.Json) });
                db.AuditLogs.Add(new() { ActionType = "CREATE", EntityName = "CONTRACT_TEMPLATE", EntityId = template.ContractTemplateId.ToString(), ActorType = "EMPLOYEE", ActorId = actor, CreatedAt = DateTime.UtcNow });
                await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return definition;
            }
            catch { foreach (var path in files) if (File.Exists(path)) File.Delete(path); throw; }
        });
    }
}

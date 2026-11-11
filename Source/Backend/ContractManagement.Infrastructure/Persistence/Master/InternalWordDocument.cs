using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using ContractManagement.Application.Features.Internal;
using ContractManagement.Domain.Internal;
using ContractManagement.Domains.Services.ContractTemplate;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace ContractManagement.Infrastructure.Persistence.Master;

public sealed class InternalWordDocument(IConfiguration configuration)
{
    public const string Mime = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    public static byte[] Decode(string encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded) || encoded.Length > 14_000_000)
            throw new ArgumentException("File phải có nội dung và không quá 10 MB.");
        byte[] bytes;
        try { bytes = System.Convert.FromBase64String(encoded); }
        catch (FormatException) { throw new ArgumentException("Dữ liệu file không hợp lệ."); }
        if (bytes.Length == 0 || bytes.Length > 10 * 1024 * 1024) throw new ArgumentException("File vượt quá 10 MB.");
        return bytes;
    }

    public async Task<byte[]> Normalize(TemplateFileInput input, CancellationToken ct)
    {
        var bytes = Decode(input.Base64);
        var extension = Path.GetExtension(input.FileName).ToLowerInvariant();
        if (extension == ".doc")
        {
            byte[] signature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
            if (!bytes.AsSpan().StartsWith(signature)) throw new ArgumentException("File không phải Word DOC hợp lệ.");
            // Compound-file directory names are UTF-16. Reject encrypted documents and VBA storage before conversion.
            var directory = Encoding.Unicode.GetString(bytes);
            if (!directory.Contains("WordDocument", StringComparison.Ordinal)
                || new[] { "VBA", "Macros", "EncryptedPackage", "EncryptionInfo" }.Any(directory.Contains))
                throw new ArgumentException("DOC không hợp lệ, có macro hoặc được mã hóa.");
            for (var offset = 512; offset + 128 <= bytes.Length; offset += 128)
            {
                if (Encoding.Unicode.GetString(bytes, offset, 26) != "WordDocument\0") continue;
                var shift = BitConverter.ToUInt16(bytes, 30);
                if (shift is not (9 or 12)) throw new ArgumentException("Cấu trúc DOC không hợp lệ.");
                var position = ((long)BitConverter.ToUInt32(bytes, offset + 116) + 1) * (1 << shift);
                if (position + 12 > bytes.Length || position < 512) throw new ArgumentException("Cấu trúc DOC không hợp lệ.");
                var flags = BitConverter.ToUInt16(bytes, (int)position + 10);
                if ((flags & 0x8100) != 0) throw new ArgumentException("Không hỗ trợ DOC được mã hóa hoặc đặt mật khẩu.");
                break;
            }
            bytes = await Convert(bytes, ".doc", "docx:Office Open XML Text", ".docx", ct);
        }
        else if (extension != ".docx") throw new ArgumentException("Chọn file Word .doc hoặc .docx.");
        Validate(bytes);
        return bytes;
    }

    public static void Validate(byte[] bytes)
    {
        try
        {
            using var stream = new MemoryStream(bytes);
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                if (zip.Entries.Count > 2000 || zip.Entries.Sum(e => e.Length) > 50 * 1024 * 1024
                    || zip.Entries.Any(e => e.FullName.Contains("vbaProject", StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("DOCX quá lớn hoặc chứa macro.");
            }
            stream.Position = 0;
            using var doc = WordprocessingDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = 2_000_000 });
            if (doc.DocumentType != WordprocessingDocumentType.Document || doc.MainDocumentPart?.Document?.Body is null
                || doc.MainDocumentPart.VbaProjectPart is not null)
                throw new ArgumentException("DOCX không có nội dung hợp lệ.");
        }
        catch (ArgumentException) { throw; }
        catch (Exception e) when (e is IOException or InvalidDataException or OpenXmlPackageException or System.Xml.XmlException)
        { throw new ArgumentException("Không đọc được DOCX. Kiểm tra định dạng và mật khẩu."); }
    }

    private static IEnumerable<(string Location, OpenXmlPartRootElement Root)> Roots(WordprocessingDocument doc)
    {
        var main = doc.MainDocumentPart!;
        yield return ("Nội dung", main.Document!);
        foreach (var part in main.HeaderParts.OrderBy(p => p.Uri.ToString())) yield return ("Đầu trang", part.Header!);
        foreach (var part in main.FooterParts.OrderBy(p => p.Uri.ToString())) yield return ("Chân trang", part.Footer!);
        if (main.FootnotesPart?.Footnotes is { } footnotes) yield return ("Chú thích", footnotes);
        if (main.EndnotesPart?.Endnotes is { } endnotes) yield return ("Chú thích cuối", endnotes);
    }

    private static IEnumerable<Text> TextNodes(Paragraph paragraph) => paragraph.Descendants<Text>()
        .Where(text => ReferenceEquals(text.Ancestors<Paragraph>().FirstOrDefault(), paragraph));

    private static List<(WordBlock Block, Paragraph Paragraph)> Paragraphs(WordprocessingDocument doc, bool includeImages = false)
    {
        var result = new List<(WordBlock, Paragraph)>();
        var tables = Roots(doc).SelectMany(r => r.Root.Descendants<Table>()).ToList();
        foreach (var (location, root) in Roots(doc))
        foreach (var paragraph in root.Descendants<Paragraph>())
        {
            var text = string.Concat(TextNodes(paragraph).Select(t => t.Text));
            var cell = paragraph.Ancestors<TableCell>().FirstOrDefault();
            var row = cell?.Ancestors<TableRow>().FirstOrDefault();
            var table = row?.Ancestors<Table>().FirstOrDefault();
            var merge = cell?.TableCellProperties?.VerticalMerge;
            result.Add((new(result.Count.ToString(), cell is null ? location : location + " · Ô bảng", text,
                table is null ? null : tables.IndexOf(table),
                table is null ? null : table.Elements<TableRow>().ToList().IndexOf(row!),
                row is null ? null : row.Elements<TableCell>().ToList().IndexOf(cell!),
                cell?.TableCellProperties?.GridSpan?.Val?.Value ?? 1,
                merge is null ? null : merge.Val?.Value == MergedCellValues.Restart ? "restart" : "continue"), paragraph));
            if (includeImages)
            {
                var images = new List<WordImagePreview>();
                var owner = root.OpenXmlPart;
                var relationships = paragraph.Descendants<A.Blip>().Select(b => b.Embed?.Value)
                    .Concat(paragraph.Descendants<DocumentFormat.OpenXml.Vml.ImageData>().Select(b => b.RelationshipId?.Value));
                foreach (var id in relationships.Where(id => id is not null).Distinct())
                {
                    if (owner?.TryGetPartById(id!, out var part) == true && part is ImagePart image
                        && image.ContentType is "image/png" or "image/jpeg")
                    {
                        using var imageStream = image.GetStream();
                        using var data = new MemoryStream(); imageStream.CopyTo(data);
                        if (data.Length <= 5 * 1024 * 1024) images.Add(new(image.ContentType, System.Convert.ToBase64String(data.ToArray())));
                    }
                }
                if (images.Count > 0) result[^1] = (result[^1].Item1 with { Images = images }, paragraph);
            }
        }
        return result;
    }

    public static List<WordBlock> Blocks(byte[] bytes, bool includeImages = false)
    {
        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);
        return Paragraphs(doc, includeImages).Select(p => p.Block).ToList();
    }

    public static byte[] Edit(byte[] bytes, IReadOnlyList<WordBlock> blocks, IReadOnlyList<WordTable>? tables = null,
        IReadOnlyList<WordContent>? additions = null)
    {
        using var stream = new MemoryStream();
        stream.Write(bytes);
        stream.Position = 0;
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var paragraphs = Paragraphs(doc);
            if (blocks.Count != paragraphs.Count || blocks.Select(b => b.Id).Distinct().Count() != blocks.Count
                || blocks.Sum(b => b.Text.Length) > 100000)
                throw new ArgumentException("Danh sách đoạn văn không khớp với file.");
            for (var i = 0; i < paragraphs.Count; i++)
            {
                if (blocks[i].Id != paragraphs[i].Block.Id || blocks[i].Text.Length > 20000)
                    throw new ArgumentException("Đoạn văn không hợp lệ.");
                Replace(paragraphs[i].Paragraph, paragraphs[i].Block.Text, blocks[i].Text);
            }
            tables ??= [];
            if (tables.Count > 20 || tables.Any(t => t is null || string.IsNullOrWhiteSpace(t.Id))
                || tables.Select(t => t.Id).Distinct().Count() != tables.Count)
                throw new ArgumentException("Tối đa 20 bảng mới, mỗi bảng cần mã riêng.");
            var anchors = new Dictionary<string, OpenXmlElement>();
            var pending = new List<(string Anchor, long Order, OpenXmlElement Node)>();
            var addedLength = 0;
            foreach (var table in tables)
            {
                var source = paragraphs.FirstOrDefault(p => p.Block.Id == table.AfterBlockId).Paragraph;
                if (source is null || source.Ancestors<Body>().FirstOrDefault() is null)
                    throw new ArgumentException("Chỉ chèn bảng tại vị trí trong nội dung chính.");
                if (table.Rows is null || table.Rows.Count is < 1 or > 50
                    || table.Rows.Any(r => r is null || r.Count is < 1 or > 12 || r.Any(c => c is null || c.Text is null || c.Text.Length > 20000 || c.ColSpan is < 1 or > 12)))
                    throw new ArgumentException("Bảng cần 1–50 hàng và 1–12 cột hợp lệ.");
                var columns = table.Rows[0].Sum(c => c.ColSpan);
                if (columns > 12 || table.Rows.Any(r => r.Sum(c => c.ColSpan) != columns))
                    throw new ArgumentException("Số cột và ô gộp giữa các hàng không khớp.");
                addedLength += table.Rows.Sum(r => r.Sum(c => c.Text.Length));
                if (addedLength + blocks.Sum(b => b.Text.Length) > 100000)
                    throw new ArgumentException("Nội dung mẫu vượt quá giới hạn.");
                var value = table.Borders ? BorderValues.Single : BorderValues.Nil;
                var created = new Table(new TableProperties(
                    new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
                    new TableBorders(new TopBorder { Val = value, Size = 4 }, new LeftBorder { Val = value, Size = 4 },
                        new BottomBorder { Val = value, Size = 4 }, new RightBorder { Val = value, Size = 4 },
                        new InsideHorizontalBorder { Val = value, Size = 4 }, new InsideVerticalBorder { Val = value, Size = 4 })),
                    new TableGrid(Enumerable.Range(0, columns).Select(_ => new GridColumn { Width = (9000 / columns).ToString() })));
                foreach (var cells in table.Rows)
                {
                    var row = new TableRow();
                    foreach (var cell in cells)
                    {
                        var element = new TableCell(new TableCellProperties(new GridSpan { Val = cell.ColSpan }));
                        element.Append(cell.Text.Split('\n').Select(line => new Paragraph(new Run(new Text(line) { Space = SpaceProcessingModeValues.Preserve }))));
                        row.Append(element);
                    }
                    created.Append(row);
                }
                pending.Add((table.AfterBlockId, table.Order, created));
            }
            additions ??= [];
            if (additions.Count > 50 || additions.Any(a => a is null || string.IsNullOrWhiteSpace(a.Id))
                || additions.Select(a => a.Id).Distinct().Count() != additions.Count)
                throw new ArgumentException("Tối đa 50 nội dung chèn thêm, mỗi nội dung cần mã riêng.");
            long imageBytes = 0;
            uint drawingId = doc.MainDocumentPart!.Document!.Descendants<DW.DocProperties>().Select(p => p.Id?.Value ?? 0).DefaultIfEmpty().Max() + 1;
            foreach (var item in additions)
            {
                var formatSource = paragraphs.FirstOrDefault(p => p.Block.Id == item.AfterBlockId).Paragraph;
                if (formatSource is null || formatSource.Ancestors<Body>().FirstOrDefault() is null)
                    throw new ArgumentException("Chọn vị trí chèn trong nội dung chính.");
                if (item.Title is null || item.Text is null || item.Title.Length > 500 || item.Text.Length > 20000)
                    throw new ArgumentException("Tiêu đề hoặc nội dung chèn thêm quá dài.");
                addedLength += item.Title.Length + item.Text.Length;
                if (addedLength + blocks.Sum(b => b.Text.Length) > 100000) throw new ArgumentException("Nội dung mẫu vượt quá giới hạn.");
                if (item.Kind == "clause" || item.Kind == "paragraph")
                {
                    if (item.Kind == "clause")
                        pending.Add((item.AfterBlockId, item.Order, InheritParagraph(formatSource, item.Title, true)));
                    foreach (var line in item.Text.Split('\n'))
                        pending.Add((item.AfterBlockId, item.Order, InheritParagraph(formatSource, line)));
                }
                else if (item.Kind == "image")
                {
                    var image = Decode(item.ImageBase64 ?? "");
                    imageBytes += image.Length;
                    if (image.Length > 5 * 1024 * 1024 || imageBytes > 10 * 1024 * 1024)
                        throw new ArgumentException("Mỗi ảnh tối đa 5 MB, tổng ảnh mới tối đa 10 MB.");
                    var (mime, width, height) = ImageDimensions(image);
                    if (item.WidthCm is < 1 or > 16 || item.Align is not ("left" or "center" or "right"))
                        throw new ArgumentException("Chiều rộng ảnh phải từ 1 đến 16 cm và chọn căn lề hợp lệ.");
                    long cx = (long)(item.WidthCm * 360000), cy = cx * height / width;
                    if (cy > 24 * 360000) { cx = cx * (24 * 360000) / cy; cy = 24 * 360000; }
                    var main = doc.MainDocumentPart!;
                    var relationshipId = "rIdConTrack" + System.Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.Id)))[..24];
                    var part = main.AddImagePart(mime, relationshipId);
                    using (var imageStream = new MemoryStream(image)) part.FeedData(imageStream);
                    var pic = new PIC.Picture(new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = drawingId, Name = "Ảnh chèn thêm" }, new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(new A.Blip { Embed = main.GetIdOfPart(part) }, new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
                    var drawing = new Drawing(new DW.Inline(new DW.Extent { Cx = cx, Cy = cy },
                        new DW.DocProperties { Id = drawingId++, Name = "Ảnh chèn thêm", Description = item.Title },
                        new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                        new A.Graphic(new A.GraphicData(pic) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })));
                    var align = item.Align == "left" ? JustificationValues.Left : item.Align == "right" ? JustificationValues.Right : JustificationValues.Center;
                    pending.Add((item.AfterBlockId, item.Order, new Paragraph(new ParagraphProperties(new Justification { Val = align }), new Run(drawing))));
                    if (!string.IsNullOrWhiteSpace(item.Title))
                    {
                        var caption = InheritParagraph(formatSource, item.Title);
                        caption.ParagraphProperties!.RemoveAllChildren<NumberingProperties>();
                        caption.ParagraphProperties.Justification = new Justification { Val = align };
                        pending.Add((item.AfterBlockId, item.Order, caption));
                    }
                }
                else throw new ArgumentException("Loại nội dung chèn thêm không hợp lệ.");
            }
            foreach (var item in pending.OrderBy(p => p.Order))
            {
                var source = paragraphs.FirstOrDefault(p => p.Block.Id == item.Anchor).Paragraph;
                if (source is null || source.Ancestors<Body>().FirstOrDefault() is null)
                    throw new ArgumentException("Chọn vị trí chèn trong nội dung chính.");
                OpenXmlElement anchor = source.Ancestors<Table>().LastOrDefault() ?? (OpenXmlElement)source;
                // Several additions at the same position keep their submitted order.
                var key = paragraphs.First(p => ReferenceEquals(p.Paragraph, source)).Block.Id;
                if (anchors.TryGetValue(key, out var previous)) anchor = previous;
                anchor.InsertAfterSelf(item.Node);
                anchors[key] = item.Node;
            }
        }
        return StablePackage(stream.ToArray());
    }

    private static Paragraph InheritParagraph(Paragraph source, string text, bool heading = false)
    {
        var properties = source.ParagraphProperties?.CloneNode(true) as ParagraphProperties ?? new ParagraphProperties();
        // Copy visual formatting and style references, not document boundaries or tracked edits.
        properties.RemoveAllChildren<SectionProperties>();
        properties.RemoveAllChildren<PageBreakBefore>();
        properties.RemoveAllChildren<ParagraphPropertiesChange>();
        var run = source.Elements<Run>().Where(r => r.Descendants<Text>().Any(t => !string.IsNullOrWhiteSpace(t.Text)))
            .OrderByDescending(r => r.InnerText.Length).FirstOrDefault();
        var runProperties = run?.RunProperties?.CloneNode(true) as RunProperties ?? new RunProperties();
        runProperties.RemoveAllChildren<RunPropertiesChange>();
        if (heading)
        {
            properties.RemoveAllChildren<NumberingProperties>();
            properties.KeepNext = new KeepNext();
            runProperties.Bold = new Bold();
        }
        return new Paragraph(properties, new Run(runProperties, new Text(text.TrimEnd('\r')) { Space = SpaceProcessingModeValues.Preserve }));
    }

    // New ZIP entries otherwise carry the current time, so an identical image edit can hash differently at activation.
    private static byte[] StablePackage(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var source = new ZipArchive(input, ZipArchiveMode.Read);
        using var output = new MemoryStream();
        using (var destination = new ZipArchive(output, ZipArchiveMode.Create, true))
        foreach (var entry in source.Entries.OrderBy(e => e.FullName, StringComparer.Ordinal))
        {
            var copy = destination.CreateEntry(entry.FullName, CompressionLevel.Optimal);
            copy.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var read = entry.Open(); using var write = copy.Open(); read.CopyTo(write);
        }
        return output.ToArray();
    }

    private static (string Mime, int Width, int Height) ImageDimensions(byte[] bytes)
    {
        int width = 0, height = 0;
        string mime;
        if (bytes.Length >= 33 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})
            && Encoding.ASCII.GetString(bytes, 12, 4) == "IHDR")
        {
            mime = "image/png";
            width = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4));
            height = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4));
        }
        else if (bytes.Length > 4 && bytes[0] == 255 && bytes[1] == 216)
        {
            mime = "image/jpeg";
            var offset = 2;
            while (offset + 4 <= bytes.Length)
            {
                if (bytes[offset++] != 255) break;
                while (offset < bytes.Length && bytes[offset] == 255) offset++;
                if (offset >= bytes.Length) break;
                var marker = bytes[offset++];
                if (marker is 0xD9 or 0xDA || offset + 2 > bytes.Length) break;
                var length = (bytes[offset] << 8) | bytes[offset + 1];
                if (length < 2 || offset + length > bytes.Length) break;
                if (marker is 0xC0 or 0xC1 or 0xC2 && length >= 8)
                { height = (bytes[offset + 3] << 8) | bytes[offset + 4]; width = (bytes[offset + 5] << 8) | bytes[offset + 6]; break; }
                offset += length;
            }
        }
        else throw new ArgumentException("Chỉ hỗ trợ ảnh PNG hoặc JPEG đúng định dạng.");
        if (width <= 0 || height <= 0 || width > 12000 || height > 12000 || (long)width * height > 40000000)
            throw new ArgumentException("Ảnh lỗi hoặc kích thước vượt quá 40 triệu điểm ảnh.");
        return (mime, width, height);
    }

    // Keep unchanged prefix/suffix runs, including their formatting. Replacement inherits the first affected run.
    private static void Replace(Paragraph paragraph, string oldText, string newText)
    {
        if (oldText == newText) return;
        var nodes = TextNodes(paragraph).ToList();
        if (nodes.Count == 0) { paragraph.Append(new Run(new Text(newText) { Space = SpaceProcessingModeValues.Preserve })); return; }
        var prefix = 0;
        while (prefix < oldText.Length && prefix < newText.Length && oldText[prefix] == newText[prefix]) prefix++;
        var suffix = 0;
        while (suffix < oldText.Length - prefix && suffix < newText.Length - prefix
            && oldText[oldText.Length - suffix - 1] == newText[newText.Length - suffix - 1]) suffix++;
        var replacement = newText.Substring(prefix, newText.Length - prefix - suffix);
        var end = oldText.Length - suffix;
        var offset = 0;
        var inserted = false;
        foreach (var node in nodes)
        {
            var original = node.Text;
            var nodeEnd = offset + original.Length;
            if (nodeEnd >= prefix && offset <= end)
            {
                var left = original[..Math.Clamp(prefix - offset, 0, original.Length)];
                var right = original[Math.Clamp(end - offset, 0, original.Length)..];
                node.Text = left + (inserted ? "" : replacement) + right;
                node.Space = SpaceProcessingModeValues.Preserve;
                inserted = true;
            }
            offset = nodeEnd;
        }
    }

    public static byte[] Fill(byte[] bytes, IReadOnlyDictionary<string, string> values)
    {
        using var stream = new MemoryStream();
        stream.Write(bytes);
        stream.Position = 0;
        using (var doc = WordprocessingDocument.Open(stream, true))
        foreach (var (_, paragraph) in Paragraphs(doc))
        {
            var original = string.Concat(TextNodes(paragraph).Select(t => t.Text));
            foreach (Match match in Regex.Matches(original, @"\{\{([A-Z][A-Z0-9_]*)\}\}").Reverse())
            {
                var current = string.Concat(TextNodes(paragraph).Select(t => t.Text));
                var replacement = values.GetValueOrDefault(match.Groups[1].Value, "");
                Replace(paragraph, current, current[..match.Index] + replacement + current[(match.Index + match.Length)..]);
            }
        }
        return stream.ToArray();
    }

    public Task<byte[]> Pdf(byte[] bytes, CancellationToken ct) => Convert(bytes, ".docx", "pdf:writer_pdf_Export", ".pdf", ct);

    private async Task<byte[]> Convert(byte[] bytes, string extension, string format, string outputExtension, CancellationToken ct)
    {
        var executable = LibreOfficeContractTemplatePdfRenderer.ResolveExecutablePath(configuration["TemplatePdfRendering:ExecutablePath"])
            ?? throw new ArgumentException("Không tìm thấy LibreOffice. Cấu hình TemplatePdfRendering:ExecutablePath trên máy chủ.");
        var root = Path.Combine(Path.GetTempPath(), "contrack-word-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var output = Path.Combine(root, "output");
        Directory.CreateDirectory(output);
        try
        {
            var input = Path.Combine(root, "document" + extension);
            await File.WriteAllBytesAsync(input, bytes, ct);
            var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "--headless", "--nologo", "--nodefault", "--norestore",
                "-env:UserInstallation=" + new Uri(Path.Combine(root, "profile")).AbsoluteUri, "--convert-to", format, "--outdir", output, input })
                start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new ArgumentException("Không khởi động được LibreOffice.");
            var stdout = process.StandardOutput.ReadToEndAsync(ct);
            var stderr = process.StandardError.ReadToEndAsync(ct);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(true);
                await process.WaitForExitAsync(CancellationToken.None);
                if (ct.IsCancellationRequested) throw;
                throw new ArgumentException("Chuyển đổi Word vượt quá 60 giây. Kiểm tra file và thử lại.");
            }
            await Task.WhenAll(stdout, stderr);
            var target = Path.Combine(output, "document" + outputExtension);
            if (process.ExitCode != 0 || !File.Exists(target) || new FileInfo(target).Length is 0 or > 25_000_000)
                throw new ArgumentException("Không chuyển đổi được Word. File có thể bị lỗi hoặc đặt mật khẩu.");
            var result = await File.ReadAllBytesAsync(target, ct);
            if (outputExtension == ".pdf" && !result.AsSpan().StartsWith("%PDF-"u8)) throw new ArgumentException("PDF xem trước không hợp lệ.");
            return result;
        }
        finally { try { Directory.Delete(root, true); } catch (IOException) { } }
    }
}

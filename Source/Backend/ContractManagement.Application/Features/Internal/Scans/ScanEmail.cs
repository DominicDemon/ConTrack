using System.Text;
using ContractManagement.API.Domains.Email;

namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>What is sent: the scanned file and a short summary of the contract it belongs to.</summary>
public sealed record ScanEmailContent(
    string Title,
    IReadOnlyList<(string Label, string Value)> Details,
    string FileName,
    string ContentType,
    byte[] Content);

/// <summary>The outcome per address; an address that failed can be tried again.</summary>
public sealed record ScanEmailResultDto(IReadOnlyList<string> Sent, IReadOnlyList<ScanEmailFailureDto> Failed);

public sealed record ScanEmailFailureDto(string Email, string Reason);

/// <summary>Checks the addresses and the note an employee typed, and writes the email that carries a scan.</summary>
public static class ScanEmail
{
    public const int MaxRecipients = 10;
    public const int MaxNoteLength = 1000;

    /// <summary>Gmail refuses mail above 25 MB; the attachment grows by a third when encoded.</summary>
    public const int MaxAttachmentBytes = 18 * 1024 * 1024;

    /// <summary>
    /// The addresses to send to: trimmed, without duplicates (ignoring case). Each entry may itself hold several
    /// addresses separated by commas, semicolons, spaces or new lines, as when a list is pasted.
    /// </summary>
    public static IReadOnlyList<string> Recipients(IEnumerable<string?>? typed)
    {
        var addresses = (typed ?? [])
            .SelectMany(entry => (entry ?? "").Split([',', ';', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            .Select(address => address.Trim())
            .Where(address => address.Length > 0)
            .DistinctBy(address => address, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (addresses.Count == 0)
        {
            throw new ArgumentException("Hãy nhập ít nhất một email.");
        }

        if (addresses.Count > MaxRecipients)
        {
            throw new ArgumentException($"Mỗi lần gửi tối đa {MaxRecipients} email.");
        }

        var invalid = addresses.Where(address => !SmtpEmailOptions.IsEmailAddress(address)).ToList();
        if (invalid.Count > 0)
        {
            throw new ArgumentException($"Email không hợp lệ: {string.Join(", ", invalid)}.");
        }

        return addresses;
    }

    public static string? Note(string? typed)
    {
        var note = typed?.Trim();
        if (string.IsNullOrEmpty(note))
        {
            return null;
        }

        if (note.Length > MaxNoteLength)
        {
            throw new ArgumentException($"Lời nhắn không được quá {MaxNoteLength} ký tự.");
        }

        return note;
    }

    /// <summary>One email per address, so no recipient sees the others.</summary>
    public static EmailMessage Compose(string recipient, ScanEmailContent content, string senderName, string? note)
    {
        var subject = $"Hợp đồng: {content.Title}";

        var text = new StringBuilder()
            .AppendLine($"{senderName} gửi bạn bản scan hợp đồng \"{content.Title}\".")
            .AppendLine();
        if (note is not null)
        {
            text.AppendLine("Lời nhắn:").AppendLine(note).AppendLine();
        }

        foreach (var (label, value) in content.Details)
        {
            text.AppendLine($"{label}: {value}");
        }

        text.AppendLine()
            .AppendLine($"Tệp đính kèm: {content.FileName}")
            .AppendLine("Tài liệu nội bộ: vui lòng không chuyển tiếp cho người không liên quan.");

        var html = new StringBuilder()
            .Append("<div style=\"font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#1f2937\">")
            .Append($"<p><strong>{Encode(senderName)}</strong> gửi bạn bản scan hợp đồng <strong>{Encode(content.Title)}</strong>.</p>");
        if (note is not null)
        {
            html.Append("<p style=\"margin:12px 0 4px\"><strong>Lời nhắn:</strong></p>")
                .Append($"<p style=\"margin:0;white-space:pre-line;border-left:3px solid #d1d5db;padding-left:10px\">{Encode(note)}</p>");
        }

        html.Append("<table style=\"margin-top:16px;border-collapse:collapse\">");
        foreach (var (label, value) in content.Details)
        {
            html.Append($"<tr><td style=\"padding:4px 16px 4px 0;color:#6b7280\">{Encode(label)}</td><td style=\"padding:4px 0\">{Encode(value)}</td></tr>");
        }

        html.Append("</table>")
            .Append($"<p style=\"margin-top:16px\">Tệp đính kèm: <strong>{Encode(content.FileName)}</strong></p>")
            .Append("<p style=\"color:#6b7280;font-size:12px\">Tài liệu nội bộ: vui lòng không chuyển tiếp cho người không liên quan.</p>")
            .Append("</div>");

        return new EmailMessage(recipient, subject, text.ToString(), html.ToString(),
            Attachments: [new EmailAttachment(content.FileName, content.ContentType, content.Content)]);
    }

    /// <summary>Only the characters that mean something in HTML; Vietnamese letters stay as they are.</summary>
    private static string Encode(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&#39;");
}

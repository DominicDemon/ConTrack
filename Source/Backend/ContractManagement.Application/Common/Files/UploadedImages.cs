namespace ContractManagement.Application.Common.Files;

/// <summary>
/// Server-side checks for images sent inline as base64 (support ticket pictures, drawn signatures): the browser's checks
/// are only a convenience, anyone can call the API directly. The bytes must really be the declared image type.
/// </summary>
public static class UploadedImages
{
    public const int MaxSupportImages = 3;
    public const long MaxSupportImageBytes = 5 * 1024 * 1024;

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];

    /// <summary>The image type the bytes start with: image/png, image/jpeg, image/gif, image/webp; null for anything else.</summary>
    public static string? Detect(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith(Png) ? "image/png"
        : bytes.StartsWith(Jpeg) ? "image/jpeg"
        : bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8) ? "image/gif"
        : bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8) ? "image/webp"
        : null;

    /// <summary>
    /// The pictures of one support message: at most three data:image URLs (PNG, JPEG, GIF, WEBP) of up to 5 MB each,
    /// whose bytes match the declared type. Links to other sites are refused. Returns the cleaned list.
    /// </summary>
    public static List<string> SupportImages(IEnumerable<string?>? urls)
    {
        var list = (urls ?? []).Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u!.Trim()).Distinct().ToList();
        if (list.Count > MaxSupportImages) throw new ArgumentException($"Mỗi tin nhắn đính kèm tối đa {MaxSupportImages} hình ảnh.");
        foreach (var url in list) DataImage(url, MaxSupportImageBytes, "Hình ảnh đính kèm");
        return list;
    }

    /// <summary>Decodes a data:image/…;base64 URL and checks size and real type; returns the bytes and their type.</summary>
    public static (byte[] Bytes, string ContentType) DataImage(string url, long maxBytes, string label, params string[] allowed)
    {
        const string prefix = "data:";
        var comma = url.IndexOf(',');
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || comma < 0 || !url[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"{label} phải là ảnh tải lên từ máy (PNG, JPG, GIF hoặc WEBP), không nhận liên kết.");
        var declared = url[prefix.Length..comma][..^";base64".Length].ToLowerInvariant();
        if (declared == "image/jpg") declared = "image/jpeg";
        if (url.Length - comma - 1 > (maxBytes + 2) / 3 * 4 + 4) throw new ArgumentException($"{label} vượt quá {maxBytes / (1024 * 1024)} MB.");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(url[(comma + 1)..]); }
        catch (FormatException) { throw new ArgumentException($"{label} không đọc được."); }
        if (bytes.Length == 0 || bytes.Length > maxBytes) throw new ArgumentException($"{label} trống hoặc vượt quá {maxBytes / (1024 * 1024)} MB.");
        var actual = Detect(bytes) ?? throw new ArgumentException($"{label} không phải ảnh PNG, JPG, GIF hoặc WEBP.");
        if (declared != actual) throw new ArgumentException($"{label} có nội dung không khớp định dạng {declared}.");
        if (allowed.Length > 0 && !allowed.Contains(actual)) throw new ArgumentException($"{label} phải là {string.Join(", ", allowed)}.");
        return (bytes, actual);
    }

    /// <summary>A drawn signature: a PNG (plain base64 or a data URL) of at most 1 MB.</summary>
    public static byte[] SignaturePng(string? input, string error)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 1_400_000) throw new ArgumentException(error);
        var comma = input.IndexOf(',');
        byte[] bytes;
        try { bytes = Convert.FromBase64String(comma >= 0 ? input[(comma + 1)..] : input); }
        catch (FormatException) { throw new ArgumentException(error); }
        if (bytes.Length == 0 || bytes.Length > 1024 * 1024 || Detect(bytes) != "image/png") throw new ArgumentException(error);
        return bytes;
    }
}

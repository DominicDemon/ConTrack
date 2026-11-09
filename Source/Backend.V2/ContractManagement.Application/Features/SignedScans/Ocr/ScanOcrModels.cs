using ContractManagement.Domain.Common;

namespace ContractManagement.Application.Features.SignedScans.Ocr;

/// <summary>A page image for the OCR engine; <see cref="Dpi"/> is known only for rendered PDF pages.</summary>
public sealed record ScanPageImage(byte[] Content, int? Dpi);

/// <summary>A keyword read from a scan. <see cref="Value"/> is normalised for searching.</summary>
public sealed record ScanKeyword(ScanKeywordType Type, string Value, int Page);

/// <summary><see cref="Text"/> is the text of the pages read, each after a "--- Trang n ---" line.</summary>
public sealed record ScanKeywordResult(
    IReadOnlyList<ScanKeyword> Keywords,
    int PagesRead,
    int PageCount,
    string Text = "");

public enum ScanFileKind
{
    Pdf,
    Png,
    Jpeg
}

public static class ScanFileKinds
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    public static ScanFileKind? Detect(ReadOnlySpan<byte> content) =>
        content.StartsWith("%PDF-"u8) ? ScanFileKind.Pdf
        : content.StartsWith(PngSignature) ? ScanFileKind.Png
        : content.StartsWith(JpegSignature) ? ScanFileKind.Jpeg
        : null;
}

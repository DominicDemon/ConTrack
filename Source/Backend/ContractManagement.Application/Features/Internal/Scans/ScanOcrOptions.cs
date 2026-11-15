using ContractManagement.Domain.Internal.Scans;
using System.Text.RegularExpressions;

namespace ContractManagement.Application.Features.Internal.Scans;

public sealed class ScanOcrOptions
{
    public const string SectionName = "ScanOcr";

    private static readonly Regex LanguagePattern = new(@"^[A-Za-z_]+(\+[A-Za-z_]+)*$");

    /// <summary>tesseract executable; empty = the usual install folders, then the PATH.</summary>
    public string? ExecutablePath { get; set; }

    /// <summary>Folder holding the .traineddata files; empty = Tesseract's own tessdata folder.</summary>
    public string? TessdataPath { get; set; }

    public string Languages { get; set; } = "vie+eng";

    public int PageTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Tesseract page segmentation mode (--psm). 4 reads the page as one column of lines, which keeps a label and
    /// its value on the same line ("Tax code / MST : 0312345678"); the automatic mode 3 splits a two-column
    /// party block into a column of labels and a column of values, and the keywords cannot be paired again.
    /// </summary>
    public int PageSegMode { get; set; } = 4;

    /// <summary>Pages read when taking search keywords from a scan.</summary>
    public int KeywordMaxPages { get; set; } = 30;

    /// <summary>Largest scan accepted, matching the upload limit for signed evidence.</summary>
    public long MaxFileBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    /// 200 is enough for printed contract text and reads about a third faster than 300; a phone photo
    /// joined into a PDF has no more detail than that anyway.
    /// </summary>
    public int RasterDpi { get; set; } = 200;

    /// <summary>Pages read at the same time, each by its own Tesseract process.</summary>
    public int MaxParallelPages { get; set; } = 4;

    /// <summary>An oversized PDF page is rendered at a lower DPI so neither side exceeds this.</summary>
    public int MaxPixelsPerSide { get; set; } = 5000;

    public static bool IsValidLanguages(string? languages) =>
        !string.IsNullOrWhiteSpace(languages) && LanguagePattern.IsMatch(languages);
}

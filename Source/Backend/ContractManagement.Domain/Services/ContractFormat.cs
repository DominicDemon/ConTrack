namespace ContractManagement.Domain.Internal;

/// <summary>
/// How contracts drawn by the system are printed (Thiết lập tham số hợp đồng), one setting for the whole company.
/// Word templates keep the layout of their own file. Sizes are in points, margins in millimetres.
/// The defaults are the layout used before the setting existed.
/// </summary>
public sealed record ContractFormat(string FontFamily = "Times New Roman", double TitleFontSize = 17, double HeadingFontSize = 13, double BodyFontSize = 13,
    double MarginTopMm = 20, double MarginBottomMm = 20, double MarginLeftMm = 30, double MarginRightMm = 20, double LineSpacing = 1.0,
    bool TitleUppercase = true, bool ShowNationalMotto = true)
{
    /// <summary>Fonts every Windows server has and that carry Vietnamese letters.</summary>
    public static readonly string[] Fonts = ["Times New Roman", "Arial", "Tahoma", "Calibri"];
    public static readonly ContractFormat Default = new();

    public static double MmToPoints(double mm) => mm * 72 / 25.4;

    public ContractFormat Validated()
    {
        if (!Fonts.Contains(FontFamily)) throw new ArgumentException("Chọn phông chữ trong danh sách.");
        if (TitleFontSize is < 10 or > 32 || HeadingFontSize is < 9 or > 24 || BodyFontSize is < 9 or > 20)
            throw new ArgumentException("Cỡ chữ tiêu đề từ 10–32, đề mục từ 9–24, nội dung từ 9–20.");
        if (new[] { MarginTopMm, MarginBottomMm, MarginLeftMm, MarginRightMm }.Any(m => m is < 10 or > 50))
            throw new ArgumentException("Lề trang phải từ 10 đến 50 mm.");
        if (LineSpacing is < 1 or > 2) throw new ArgumentException("Giãn dòng từ 1 đến 2.");
        return this with
        {
            TitleFontSize = Math.Round(TitleFontSize, 1), HeadingFontSize = Math.Round(HeadingFontSize, 1), BodyFontSize = Math.Round(BodyFontSize, 1),
            MarginTopMm = Math.Round(MarginTopMm), MarginBottomMm = Math.Round(MarginBottomMm), MarginLeftMm = Math.Round(MarginLeftMm),
            MarginRightMm = Math.Round(MarginRightMm), LineSpacing = Math.Round(LineSpacing, 2)
        };
    }
}

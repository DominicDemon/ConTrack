using System.Globalization;
using System.Text;

namespace ContractManagement.Application.Features.SignedScans.Ocr;

public static class VietnameseText
{
    /// <summary>"Mã số thuế" becomes "Ma so thue", so labels match however OCR read the accents.</summary>
    public static string StripDiacritics(string value)
    {
        var decomposed = value.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}

using System.Text.RegularExpressions;
using ContractManagement.Domain.Common;

namespace ContractManagement.Application.Features.SignedScans.Ocr;

/// <summary>
/// Reads the keywords a signed contract is searched by (contract code, tax codes, phone numbers,
/// party names) from the text of one scanned page. A bare 10-digit number could be a tax code or a
/// phone, so those two are taken only after their label; fax numbers are left out.
/// </summary>
public static class ContractScanKeywordExtractor
{
    public const int MaximumPerType = 20;

    /// <summary>A longer contract code, tax code or phone number is not one.</summary>
    public const int MaximumValueLength = 64;

    /// <summary>Longest party name kept; <c>CONTRACT_SCAN_KEYWORD.contract_scan_keyword_value</c> holds 400.</summary>
    public const int MaximumNameLength = 200;

    private const int MinimumNameLength = 3;

    private const int MinimumCodeLength = 4;
    private const int MinimumAccountNumberLength = 9;
    private const int MinimumSearchLength = 4;
    private const int MinimumSearchDigits = 6;
    private const int PhoneLength = 10;
    private const int LongLandlineLength = 11;

    // "Số:" / "No.", also when OCR reads the label as "S6:" or "S0:". Not "Tài khoản số:", which is
    // followed by a bank account number.
    private static readonly Regex LabelledCode = new(
        @"(?<!KHOAN\s*)\b(?:S[O06]|NO)\b\s*[:.]\s*([A-Z0-9][A-Z0-9\-/._]{3,})", RegexOptions.Compiled);

    // The code the system itself prints (HD-yyyyMMdd-000001), found even without its label.
    private static readonly Regex SystemCode = new(
        @"\bHD-[0-9OQILSB]{8}-[0-9OQILSB]{6}\b", RegexOptions.Compiled);

    private static readonly Regex SpacedHyphen = new(
        @"(?<=[A-Z0-9])[ \t]*-[ \t]*(?=[A-Z0-9])", RegexOptions.Compiled);

    private static readonly Regex LabelledTaxCode = new(
        @"(?:MA\s*SO\s*THUE|\bMST\b|TAX\s*(?:CODE|NUMBER|ID|NO)\b|VAT\s*(?:CODE|NO)\b)"
        + @"[\s:.\-]{0,8}(?<!\d)(\d{13}|\d{10}(?:\s?-\s?\d{3})?)(?!\d)",
        RegexOptions.Compiled);

    // Fax is never listed: "FAX" is not one of the labels.
    private static readonly Regex LabelledPhone = new(
        @"(?:DIEN\s*THOAI|\bSDT\b|\bDT\b|\bTEL\b|PHONE|MOBILE|HOTLINE)"
        + @"[\s:.\-()]{0,8}(?<run>[+\d][\d \-.()+/,;]{8,40})",
        RegexOptions.Compiled);

    // The label of a party line, before its colon: "BÊN A", "Bên B (Bên bán)", "Khách hàng", "Tên công ty".
    private static readonly Regex PartyLabel = new(
        @"^\s*(?:BEN\s*[AB]|BEN\s*(?:MUA|BAN|THUE|CHO\s*THUE|CUNG\s*CAP|SU\s*DUNG|DICH\s*VU)"
        + @"|KHACH\s*HANG|TEN\s*(?:KHACH\s*HANG|CONG\s*TY|DOANH\s*NGHIEP|DON\s*VI|TO\s*CHUC))"
        + @"\s*(?:\([^)]*\))?\s*$",
        RegexOptions.Compiled);

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    public static IReadOnlyList<ScanKeyword> Extract(string? pageText, int page = 1)
    {
        if (string.IsNullOrWhiteSpace(pageText))
        {
            return [];
        }

        var text = VietnameseText.StripDiacritics(pageText).ToUpperInvariant();
        var keywords = new List<ScanKeyword>();
        AddCodes(keywords, SpacedHyphen.Replace(text, "-"), page);
        AddTaxCodes(keywords, text, page);
        AddPhoneNumbers(keywords, text, page);
        AddPartyNames(keywords, pageText, page);

        return keywords
            .Where(keyword => keyword.Value.Length <= (keyword.Type == ScanKeywordType.CustomerName
                ? MaximumNameLength
                : MaximumValueLength))
            .GroupBy(keyword => (keyword.Type, keyword.Value.ToUpperInvariant()))
            .Select(group => group.First())
            .GroupBy(keyword => keyword.Type)
            .SelectMany(group => group.Take(MaximumPerType))
            .ToList();
    }

    /// <summary>
    /// The forms of what a user typed in the search box that can match a stored keyword: the text
    /// itself as a code (upper case, no accents or spaces) and its digits as a phone or tax code,
    /// where <c>+84 912 345 678</c> also matches the stored <c>0912345678</c>. At most three.
    /// </summary>
    public static IReadOnlyList<string> SearchTerms(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return [];
        }

        var plain = VietnameseText.StripDiacritics(typed).ToUpperInvariant();
        var terms = new List<string>();
        var code = SpacedHyphen.Replace(plain.Trim(), "-").Replace(" ", string.Empty);
        if (code.Length >= MinimumSearchLength)
        {
            terms.Add(code);
        }

        var digits = Digits(plain);
        if (digits.Length >= MinimumSearchDigits)
        {
            terms.Add(digits);
            if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length > MinimumSearchDigits)
            {
                terms.Add("0" + digits[2..]);
            }
        }

        return terms.Distinct().ToList();
    }

    /// <summary>
    /// A keyword typed by staff (when OCR missed it) in the form OCR keywords are stored in; null
    /// when it is not a contract code, a 10 or 13 digit tax code or a 10 or 11 digit phone number.
    /// </summary>
    public static string? NormaliseTyped(ScanKeywordType type, string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var plain = VietnameseText.StripDiacritics(typed).ToUpperInvariant().Trim();
        switch (type)
        {
            case ScanKeywordType.CustomerName:
                return NormaliseName(typed);
            case ScanKeywordType.ContractCode:
                var code = NormaliseCode(SpacedHyphen.Replace(plain, "-").Replace(" ", string.Empty));
                return code is { Length: <= MaximumValueLength } ? code : null;
            case ScanKeywordType.TaxCode:
                var taxCode = Digits(plain);
                return taxCode.Length is 10 or 13 ? taxCode : null;
            case ScanKeywordType.PhoneNumber:
                var digits = Digits(plain);
                if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length > PhoneLength)
                {
                    digits = "0" + digits[2..];
                }

                // Typed by hand, a number that is too long is a mistake rather than trailing text.
                var phone = NormalisePhone(digits);
                return phone is not null && phone.Length == digits.Length ? phone : null;
            default:
                return null;
        }
    }

    /// <summary>
    /// Party names, taken from the original text so the accents stay: the part after the colon of a
    /// line whose label (compared without accents) names a party.
    /// </summary>
    private static void AddPartyNames(List<ScanKeyword> keywords, string pageText, int page)
    {
        foreach (var line in pageText.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var label = VietnameseText.StripDiacritics(line[..colon]).ToUpperInvariant();
            if (!PartyLabel.IsMatch(label))
            {
                continue;
            }

            var name = NormaliseName(line[(colon + 1)..]);
            if (name is not null)
            {
                keywords.Add(new ScanKeyword(ScanKeywordType.CustomerName, name, page));
            }
        }
    }

    /// <summary>Spaces collapsed and stray punctuation trimmed; null when no name is left.</summary>
    private static string? NormaliseName(string raw)
    {
        var name = Spaces.Replace(raw, " ").Trim().Trim('.', ',', ';', ':', '-', '_', '|', '"', '\'');
        return name.Length is >= MinimumNameLength and <= MaximumNameLength && name.Any(char.IsLetter)
            ? name
            : null;
    }

    private static void AddCodes(List<ScanKeyword> keywords, string text, int page)
    {
        var raws = LabelledCode.Matches(text).Select(found => found.Groups[1].Value)
            .Concat(SystemCode.Matches(text).Select(found => found.Value));
        foreach (var raw in raws)
        {
            var code = NormaliseCode(raw);
            if (code is not null)
            {
                keywords.Add(new ScanKeyword(ScanKeywordType.ContractCode, code, page));
            }
        }
    }

    private static void AddTaxCodes(List<ScanKeyword> keywords, string text, int page)
    {
        foreach (Match match in LabelledTaxCode.Matches(text))
        {
            keywords.Add(new ScanKeyword(ScanKeywordType.TaxCode, Digits(match.Groups[1].Value), page));
        }
    }

    private static void AddPhoneNumbers(List<ScanKeyword> keywords, string text, int page)
    {
        foreach (Match match in LabelledPhone.Matches(text))
        {
            // "0912 345 678 / 0987 654 321": each part is a number of its own.
            foreach (var part in match.Groups["run"].Value.Split('/', ',', ';'))
            {
                var phone = NormalisePhone(part);
                if (phone is not null)
                {
                    keywords.Add(new ScanKeyword(ScanKeywordType.PhoneNumber, phone, page));
                }
            }
        }
    }

    private static string? NormaliseCode(string raw)
    {
        var code = raw.Trim().TrimEnd('.', '-', '/', '_');
        if (code.Length < MinimumCodeLength)
        {
            return null;
        }

        // A long run of digits alone is a bank account, tax code or phone number, not a contract code.
        if (code.Length >= MinimumAccountNumberLength && code.All(char.IsAsciiDigit))
        {
            return null;
        }

        // Segments after the prefix are numbers: "HD-2026-OOO7" is "HD-2026-0007".
        var parts = code.Split('-');
        for (var index = 1; index < parts.Length; index++)
        {
            var part = parts[index];
            if (part.Any(char.IsAsciiDigit) && part.All(character =>
                    char.IsAsciiDigit(character) || "OQILSB".Contains(character)))
            {
                parts[index] = new string(part.Select(Digitise).ToArray());
            }
        }

        return string.Join('-', parts);
    }

    private static char Digitise(char character) => character switch
    {
        'O' or 'Q' => '0',
        'I' or 'L' => '1',
        'S' => '5',
        'B' => '8',
        var other => other
    };

    // +84 912 345 678, 84912345678 and 0912.345.678 all become 0912345678.
    private static string? NormalisePhone(string part)
    {
        var digits = Digits(part);
        if (digits.StartsWith("84", StringComparison.Ordinal) && digits.Length > PhoneLength)
        {
            digits = "0" + digits[2..];
        }

        if (digits.Length < PhoneLength || digits[0] != '0' || digits[1] == '0')
        {
            return null;
        }

        // Mobile numbers have 10 digits; landlines 10 or 11 (area code 2xx plus 7 or 8 digits).
        return digits[1] == '2' && digits.Length >= LongLandlineLength
            ? digits[..LongLandlineLength]
            : digits[..PhoneLength];
    }

    private static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());
}

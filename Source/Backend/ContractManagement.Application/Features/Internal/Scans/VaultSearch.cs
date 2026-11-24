using System.Text;

namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>
/// The search box of the customer's vault. A document matches when the whole query, without spaces and punctuation,
/// is inside one of its fields ("HDK 2026 0456", "hdk20260456" and "0977 665 544" all find HDK-2026-0456 / 0977665544),
/// or when every word of the query is somewhere in the document ("thuê kho", "doi tac may in"). Accents, case and
/// punctuation never matter, the folder counts, and a phone typed as +84… is read as 0….
/// </summary>
public static class VaultSearch
{
    /// <summary>What a document is searched by.</summary>
    public sealed record Document(string Title, string? Note, string? Category, string FileName, IReadOnlyList<string> Keywords);

    public static bool Matches(string? query, Document document)
    {
        var words = Words(query);
        if (words.Length == 0) return true;
        var fields = new[] { document.Title, document.Note, document.Category, document.FileName }
            .Concat(document.Keywords).Where(field => !string.IsNullOrWhiteSpace(field)).Select(field => Words(field!)).ToList();

        // The whole query as one run of letters and digits, inside a single field.
        var compactFields = fields.Select(field => string.Concat(field)).ToList();
        if (Variants(string.Concat(words)).Any(compact => compact.Length >= 3 && compactFields.Any(field => field.Contains(compact, StringComparison.Ordinal))))
            return true;

        // Otherwise each word, anywhere in the document.
        var text = " " + string.Join(" ", fields.Select(field => string.Join(" ", field))) + " ";
        return words.All(word => Variants(word).Any(variant => text.Contains(variant, StringComparison.Ordinal)));
    }

    /// <summary>Lower case, no accents, split on anything that is not a letter or a digit.</summary>
    public static string[] Words(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var plain = VietnameseText.StripDiacritics(value).ToLowerInvariant();
        var builder = new StringBuilder(plain.Length);
        foreach (var character in plain) builder.Append(char.IsLetterOrDigit(character) ? character : ' ');
        return builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>A Vietnamese mobile typed with the country code (84…) is also tried in the 0… form it is stored in.</summary>
    private static IEnumerable<string> Variants(string word)
    {
        yield return word;
        if (word.Length is >= 10 and <= 12 && word.StartsWith("84", StringComparison.Ordinal) && word.All(char.IsAsciiDigit))
            yield return "0" + word[2..];
    }
}

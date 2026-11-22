namespace ContractManagement.Application.Features.Internal.Scans;

/// <summary>
/// What a key an employee chooses for themselves must look like. The same rules are shown and checked in the form
/// (SystemAdmin lib/file-key-policy.ts); the server checks them again and also that the key is not the login
/// password. The key travels in an HTTP header, so only visible ASCII characters are allowed.
/// </summary>
public static class FileKeyPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 64;

    /// <summary>Words that make a key easy to guess, whatever is added around them.</summary>
    private static readonly string[] CommonWords =
    [
        "password", "passw0rd", "matkhau", "123456", "654321", "qwerty", "asdfgh", "zxcvbn", "abc123", "iloveyou",
        "admin", "contrack", "welcome", "letmein", "111111", "000000", "123123", "1q2w3e", "baomat", "hopdong",
    ];

    /// <summary>The employee's own details a key must not contain.</summary>
    public sealed record Owner(string? Username, string? Email, string? Phone);

    /// <summary>The first rule the key breaks, as a message for the employee; null when it is acceptable.</summary>
    public static string? Problem(string? key, Owner owner)
    {
        if (string.IsNullOrEmpty(key) || key.Length < MinLength)
        {
            return $"Key cần ít nhất {MinLength} ký tự.";
        }

        if (key.Length > MaxLength)
        {
            return $"Key tối đa {MaxLength} ký tự.";
        }

        if (key.Any(character => character is < '!' or > '~'))
        {
            return "Key chỉ gồm chữ không dấu, số và ký tự đặc biệt; không có dấu cách hay chữ có dấu.";
        }

        if (!key.Any(char.IsAsciiLetterUpper) || !key.Any(char.IsAsciiLetterLower) || !key.Any(char.IsAsciiDigit)
            || key.All(char.IsAsciiLetterOrDigit))
        {
            return "Key cần có chữ hoa, chữ thường, số và ký tự đặc biệt.";
        }

        if (HasRun(key))
        {
            return "Key không được có 4 ký tự liền nhau giống nhau hoặc theo thứ tự (aaaa, 1234, abcd).";
        }

        var lower = key.ToLowerInvariant();
        if (CommonWords.FirstOrDefault(lower.Contains) is { } word)
        {
            return $"Key chứa \"{word}\", quá dễ đoán.";
        }

        foreach (var part in OwnerParts(owner))
        {
            if (lower.Contains(part))
            {
                return "Key không được chứa tên đăng nhập, email hay số điện thoại của bạn.";
            }
        }

        return null;
    }

    private static bool HasRun(string key)
    {
        for (var i = 0; i + 3 < key.Length; i++)
        {
            var same = true;
            var up = true;
            var down = true;
            for (var j = 1; j < 4; j++)
            {
                var step = char.ToLowerInvariant(key[i + j]) - char.ToLowerInvariant(key[i + j - 1]);
                same &= step == 0;
                up &= step == 1;
                down &= step == -1;
            }

            if (same || ((up || down) && char.IsAsciiLetterOrDigit(key[i]) && char.IsAsciiLetterOrDigit(key[i + 3])))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> OwnerParts(Owner owner)
    {
        if (owner.Username?.Trim().ToLowerInvariant() is { Length: >= 3 } username)
        {
            yield return username;
        }

        if (owner.Email?.Split('@')[0].Trim().ToLowerInvariant() is { Length: >= 3 } mailbox)
        {
            yield return mailbox;
        }

        var digits = new string((owner.Phone ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.Length >= 6)
        {
            // The last six digits are what people reuse, and they are also inside the full number.
            yield return digits[^6..];
        }
    }
}

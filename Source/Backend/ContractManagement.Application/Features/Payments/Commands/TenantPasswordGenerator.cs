using System.Security.Cryptography;

namespace ContractManagement.API.Domains.Services.Subscription;

/// <summary>
/// Mật khẩu tạm 12 ký tự cho Manager đầu tiên: ≥1 số, ≥1 chữ hoa, ≥1 chữ thường (khớp PasswordRules).
/// Dùng RandomNumberGenerator, không dùng Random. Bỏ ký tự dễ nhầm (0/O, 1/l/I).
/// </summary>
public static class TenantPasswordGenerator
{
    public const int Length = 12;

    private const string Digits = "23456789";
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnopqrstuvwxyz";
    private const string All = Digits + Upper + Lower;

    public static string Generate()
    {
        var chars = new char[Length];
        chars[0] = Pick(Digits);
        chars[1] = Pick(Upper);
        chars[2] = Pick(Lower);
        for (var i = 3; i < Length; i++) chars[i] = Pick(All);
        RandomNumberGenerator.Shuffle(chars.AsSpan());
        return new string(chars);
    }

    private static char Pick(string alphabet) => alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
}

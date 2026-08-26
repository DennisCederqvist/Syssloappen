using System.Security.Cryptography;
using System.Text;

namespace Syssloappen.Api.Authentication;

public static class HouseholdInvitationService
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const int RawCodeLength = 8;

    public static GeneratedHouseholdInvitation Generate()
    {
        Span<char> rawCode = stackalloc char[RawCodeLength];

        for (var index = 0; index < rawCode.Length; index++)
        {
            rawCode[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        var code = string.Create(9, rawCode.ToArray(), static (formatted, raw) =>
        {
            raw.AsSpan(0, 4).CopyTo(formatted);
            formatted[4] = '-';
            raw.AsSpan(4, 4).CopyTo(formatted[5..]);
        });

        return new GeneratedHouseholdInvitation(code, Hash(code));
    }

    public static string Hash(string code)
    {
        var normalized = code.Trim().Replace("-", string.Empty).Replace(" ", string.Empty).ToUpperInvariant();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}

public sealed record GeneratedHouseholdInvitation(string Code, string Hash);
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Syssloappen.Api.Data;

namespace Syssloappen.Api.Authentication;

public static class FamilyCodeService
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const int RawCodeLength = 12;

    public const int FormattedCodeLength = 14;

    public static async Task<GeneratedFamilyCode> GenerateUniqueAsync(AppDbContext dbContext)
    {
        while (true)
        {
            var code = Generate();
            var hash = Hash(code);

            if (!await dbContext.Households.AnyAsync(household => household.FamilyCodeHash == hash))
            {
                return new GeneratedFamilyCode(code, hash, Normalize(code)[^4..]);
            }
        }
    }

    public static string Normalize(string code)
    {
        var normalized = new StringBuilder(RawCodeLength);

        foreach (var character in code.Trim().ToUpperInvariant())
        {
            if (character is not '-' && !char.IsWhiteSpace(character))
            {
                normalized.Append(character);
            }
        }

        return normalized.ToString();
    }

    public static string Hash(string code)
    {
        var bytes = Encoding.UTF8.GetBytes(Normalize(code));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string Generate()
    {
        Span<char> rawCode = stackalloc char[RawCodeLength];

        for (var index = 0; index < rawCode.Length; index++)
        {
            rawCode[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return string.Create(
            FormattedCodeLength,
            rawCode.ToArray(),
            static (formatted, raw) =>
            {
                raw.AsSpan(0, 4).CopyTo(formatted);
                formatted[4] = '-';
                raw.AsSpan(4, 4).CopyTo(formatted[5..]);
                formatted[9] = '-';
                raw.AsSpan(8, 4).CopyTo(formatted[10..]);
            });
    }
}

public sealed record GeneratedFamilyCode(string Code, string Hash, string LastFour);

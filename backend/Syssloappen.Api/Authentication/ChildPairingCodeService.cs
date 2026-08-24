using System.Security.Cryptography;
using System.Text;

namespace Syssloappen.Api.Authentication;

public static class ChildPairingCodeService
{
    private const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";

    public const int CodeLength = 8;

    public static string Generate()
    {
        Span<char> characters = stackalloc char[CodeLength];

        for (var index = 0; index < characters.Length; index++)
        {
            characters[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(characters);
    }

    public static string Normalize(string code) => code.Trim().ToUpperInvariant();

    public static string Hash(string code)
    {
        var bytes = Encoding.UTF8.GetBytes(Normalize(code));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

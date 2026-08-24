using System.Security.Cryptography;
using System.Text;

namespace Syssloappen.Api.Authentication;

public static class ChildDeviceSessionService
{
    // Activity may renew the cookie for seven days, but never beyond thirty days.
    public static readonly TimeSpan RenewableLifetime = TimeSpan.FromDays(7);

    public static readonly TimeSpan MaximumLifetime = TimeSpan.FromDays(30);

    public static readonly TimeSpan RenewalThreshold = TimeSpan.FromDays(1);

    public static readonly TimeSpan ActivityUpdateInterval = TimeSpan.FromMinutes(5);

    public const string SessionIdClaim = "syssloappen:child_session_id";

    public const string SessionSecretClaim = "syssloappen:child_session_secret";

    public static string GenerateSecret() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    public static string HashSecret(string secret)
    {
        var bytes = Encoding.UTF8.GetBytes(secret);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }
}

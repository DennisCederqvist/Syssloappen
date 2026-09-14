namespace Syssloappen.Api.Services;

/// <summary>Bound from configuration section "Email:Resend". See docs/HANDOFF.md for how the
/// API key is supplied in each environment (never committed to git).</summary>
public sealed class ResendOptions
{
    public const string SectionName = "Email:Resend";

    /// <summary>Resend API key, sending-scoped. Must stay server-side only.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>From header, e.g. "Sysslo &lt;no-reply@sysslo.dxcode.se&gt;". The domain must be
    /// verified in Resend before this can be used.</summary>
    public string FromAddress { get; set; } = string.Empty;
}

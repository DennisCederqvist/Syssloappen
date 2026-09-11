namespace Syssloappen.Api.Services;

/// <summary>Bound from configuration section "Storage:Supabase". See docs/HANDOFF.md for how the
/// service-role key is supplied in each environment (never committed to git).</summary>
public sealed class SupabaseStorageOptions
{
    public const string SectionName = "Storage:Supabase";

    /// <summary>Project URL, e.g. https://xxxxx.supabase.co (no trailing slash).</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>Service-role key — grants write access to Storage, must stay server-side only.</summary>
    public string ServiceKey { get; set; } = string.Empty;

    /// <summary>Storage bucket name, e.g. "reward-images". Expected to be public-read.</summary>
    public string Bucket { get; set; } = string.Empty;
}

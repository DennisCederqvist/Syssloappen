namespace Syssloappen.Api.Services;

/// <summary>
/// Persists an already-processed reward image (see <see cref="RewardImageProcessor"/>) and
/// returns the URL clients should use to fetch it. Implementations never see the raw upload —
/// only the resized/compressed WebP bytes — so the storage budget is protected regardless of
/// which implementation is active.
/// </summary>
public interface IRewardImageStorage
{
    Task<string> SaveAsync(byte[] webpContent, string fileName, CancellationToken cancellationToken = default);

    /// <summary>Removes a previously-saved image given the URL <see cref="SaveAsync"/> returned
    /// for it. Best-effort — callers should not fail the whole request if this fails, since the
    /// new image is already saved by the time this runs.</summary>
    Task DeleteAsync(string url, CancellationToken cancellationToken = default);
}

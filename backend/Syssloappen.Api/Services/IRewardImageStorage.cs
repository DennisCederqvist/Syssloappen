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
}

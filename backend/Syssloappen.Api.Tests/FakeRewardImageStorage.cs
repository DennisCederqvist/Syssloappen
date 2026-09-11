using Syssloappen.Api.Services;

namespace Syssloappen.Api.Tests;

/// <summary>In-memory stand-in for image storage so tests never touch disk or a real
/// Supabase project. Records what was saved so tests can assert on it.</summary>
public sealed class FakeRewardImageStorage : IRewardImageStorage
{
    public List<(string FileName, int ByteCount)> SavedFiles { get; } = [];
    public List<string> DeletedUrls { get; } = [];

    public Task<string> SaveAsync(byte[] webpContent, string fileName, CancellationToken cancellationToken = default)
    {
        SavedFiles.Add((fileName, webpContent.Length));
        return Task.FromResult($"/reward-images/{fileName}");
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        DeletedUrls.Add(url);
        return Task.CompletedTask;
    }
}

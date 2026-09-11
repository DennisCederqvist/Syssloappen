namespace Syssloappen.Api.Services;

/// <summary>
/// Development-only storage: writes under wwwroot/reward-images, served back out via
/// UseStaticFiles. Never used once <c>Storage:Provider</c> is set to <c>Supabase</c>.
/// </summary>
public sealed class LocalDiskRewardImageStorage(IWebHostEnvironment environment) : IRewardImageStorage
{
    private const string RelativeFolder = "reward-images";

    public async Task<string> SaveAsync(byte[] webpContent, string fileName, CancellationToken cancellationToken = default)
    {
        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrEmpty(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        var folderPath = Path.Combine(webRootPath, RelativeFolder);
        Directory.CreateDirectory(folderPath);

        var filePath = Path.Combine(folderPath, fileName);
        await File.WriteAllBytesAsync(filePath, webpContent, cancellationToken);

        return $"/{RelativeFolder}/{fileName}";
    }

    public Task DeleteAsync(string url, CancellationToken cancellationToken = default)
    {
        var fileName = url[(url.LastIndexOf('/') + 1)..];
        var webRootPath = environment.WebRootPath;
        if (string.IsNullOrEmpty(webRootPath))
        {
            webRootPath = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        var filePath = Path.Combine(webRootPath, RelativeFolder, fileName);
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }
}

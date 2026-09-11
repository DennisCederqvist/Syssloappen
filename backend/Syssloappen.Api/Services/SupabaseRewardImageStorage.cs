using System.Net.Http.Headers;
using Microsoft.Extensions.Options;

namespace Syssloappen.Api.Services;

/// <summary>
/// Uploads reward photos to a Supabase Storage bucket via its plain REST API
/// (no Supabase SDK needed for a single upload call). Requires the bucket to already exist and
/// be configured public-read, so the returned URL works directly in an &lt;img&gt; tag.
/// </summary>
public sealed class SupabaseRewardImageStorage(HttpClient httpClient, IOptions<SupabaseStorageOptions> options)
    : IRewardImageStorage
{
    public async Task<string> SaveAsync(byte[] webpContent, string fileName, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        var objectPath = $"{config.Bucket}/{fileName}";
        var requestUri = $"{config.Url}/storage/v1/object/{objectPath}";

        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ServiceKey);
        // Supabase's Storage API requires both headers — Authorization alone is rejected with
        // "Invalid Compact JWS", confirmed against a real project while wiring this up.
        request.Headers.Add("apikey", config.ServiceKey);
        request.Headers.Add("x-upsert", "true");
        request.Content = new ByteArrayContent(webpContent);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/webp");

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Supabase Storage upload failed ({(int)response.StatusCode}): {body}");
        }

        return $"{config.Url}/storage/v1/object/public/{objectPath}";
    }
}

using SkiaSharp;

namespace Syssloappen.Api.Services;

/// <summary>
/// Decodes an uploaded reward photo and re-encodes it as a small WebP thumbnail, regardless of
/// what the client sent. This is the one place the Supabase Storage 1GB budget is actually
/// protected — trusting client-side compression alone would let a single unconverted phone
/// photo (several MB) blow past it.
/// </summary>
public static class RewardImageProcessor
{
    // Reward photos only ever render as small UI thumbnails, so 800px on the longest edge is
    // already more resolution than any layout in the app uses.
    private const int MaxDimension = 800;
    private const int WebpQuality = 75;

    /// <exception cref="InvalidDataException">The stream could not be decoded as an image.</exception>
    public static async Task<byte[]> ToCompressedWebpAsync(Stream content, CancellationToken cancellationToken = default)
    {
        await using var memory = new MemoryStream();
        await content.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        using var original = SKBitmap.Decode(memory)
            ?? throw new InvalidDataException("The file could not be decoded as an image.");

        var scale = Math.Min(1.0, MaxDimension / (double)Math.Max(original.Width, original.Height));
        using var resized = scale < 1.0
            ? original.Resize(
                new SKImageInfo((int)(original.Width * scale), (int)(original.Height * scale)),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))
            : original;
        if (resized is null) throw new InvalidDataException("The image could not be resized.");

        using var image = SKImage.FromBitmap(resized);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, WebpQuality);
        return encoded.ToArray();
    }
}

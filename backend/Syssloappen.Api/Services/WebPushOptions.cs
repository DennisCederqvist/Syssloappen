namespace Syssloappen.Api.Services;

public sealed class WebPushOptions
{
    public const string SectionName = "WebPush";

    public string PublicKey { get; set; } = string.Empty;

    public string PrivateKey { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;
}

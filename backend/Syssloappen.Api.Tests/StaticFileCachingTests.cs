using Syssloappen.Api.Services;
using Xunit;

namespace Syssloappen.Api.Tests;

public class StaticFileCachingTests
{
    [Theory]
    [InlineData("/main-N7VN2EGQ.js", "main-N7VN2EGQ.js")]
    [InlineData("/chunk--ToVUsKH.js", "chunk--ToVUsKH.js")]
    [InlineData("/styles-RDPY56DM.css", "styles-RDPY56DM.css")]
    public void HashedBundlesAreCachedForAYear(string path, string fileName) =>
        Assert.Equal("public, max-age=31536000, immutable", StaticFileCaching.For(path, fileName));

    [Theory]
    [InlineData("/", "index.html")]
    [InlineData("/vuxen", "index.html")]
    [InlineData("/ngsw.json", "ngsw.json")]
    [InlineData("/ngsw-worker.js", "ngsw-worker.js")]
    [InlineData("/manifest.webmanifest", "manifest.webmanifest")]
    [InlineData("/i18n/sv.json", "sv.json")]
    public void EntryFilesAreAlwaysRevalidated(string path, string fileName) =>
        Assert.Equal("no-cache", StaticFileCaching.For(path, fileName));

    [Theory]
    [InlineData("/logo/mark.webp", "mark.webp")]
    [InlineData("/icons/icon-96x96.png", "icon-96x96.png")]
    [InlineData("/fonts/inter-latin.woff2", "inter-latin.woff2")]
    public void StaticAssetsAreCachedForAWeek(string path, string fileName) =>
        Assert.Equal("public, max-age=604800", StaticFileCaching.For(path, fileName));

    [Theory]
    [InlineData("/favicon.ico", "favicon.ico")]
    [InlineData("/uploads/reward-images/abc.webp", "abc.webp")]
    public void OtherFilesGetNoHeader(string path, string fileName) =>
        Assert.Null(StaticFileCaching.For(path, fileName));
}

using AwesomeAssertions;
using NoPremium2.Infrastructure;
using Xunit;

namespace NoPremium2.Tests.Infrastructure;

public sealed class SessionPageSaverTests
{
    // ── MakeSlug ──────────────────────────────────────────────────────

    [Fact]
    public void MakeSlug_UrlWithWwwPrefix_StripsWww()
    {
        var result = SessionPageSaver.MakeSlug("https://www.nopremium.pl/files");

        result.Should().StartWith("nopremium.pl");
        result.Should().NotContain("www.");
    }

    [Fact]
    public void MakeSlug_UrlWithoutWww_UsesHostAsIs()
    {
        var result = SessionPageSaver.MakeSlug("https://nopremium.pl/files");

        result.Should().StartWith("nopremium.pl");
    }

    [Fact]
    public void MakeSlug_UrlWithPath_IncludesPathInSlug()
    {
        var result = SessionPageSaver.MakeSlug("https://www.nopremium.pl/files");

        result.Should().Contain("files");
    }

    [Fact]
    public void MakeSlug_UrlWithSlashesInPath_ReplacesWithUnderscores()
    {
        var result = SessionPageSaver.MakeSlug("https://nopremium.pl/a/b/c");

        result.Should().Be("nopremium.pl_a_b_c");
    }

    [Fact]
    public void MakeSlug_RootUrl_ReturnsHostOnly()
    {
        var result = SessionPageSaver.MakeSlug("https://nopremium.pl/");

        result.Should().Be("nopremium.pl");
    }

    [Fact]
    public void MakeSlug_InvalidUrl_ReturnsUnknown()
    {
        var result = SessionPageSaver.MakeSlug("not-a-url");

        result.Should().Be("unknown");
    }

    [Fact]
    public void MakeSlug_LongUrl_IsTruncatedTo60Chars()
    {
        var longPath = new string('a', 100);
        var result = SessionPageSaver.MakeSlug($"https://nopremium.pl/{longPath}");

        result.Length.Should().BeLessThanOrEqualTo(60);
    }

    [Fact]
    public void MakeSlug_DoesNotContainInvalidFileNameChars()
    {
        var result = SessionPageSaver.MakeSlug("https://www.nopremium.pl/files?q=1&r=2");

        var invalid = Path.GetInvalidFileNameChars();
        result.Should().NotContainAny(invalid.Select(c => c.ToString()));
    }
}

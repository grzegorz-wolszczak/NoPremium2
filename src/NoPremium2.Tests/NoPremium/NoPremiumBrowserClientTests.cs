using AwesomeAssertions;
using NoPremium2.NoPremium;
using Xunit;

namespace NoPremium2.Tests.NoPremium;

public sealed class NoPremiumBrowserClientTests
{
    // ── ParseSize ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("1",    "GB", 1_073_741_824L)]          // 1024^3
    [InlineData("512",  "MB", 536_870_912L)]             // 512 * 1024^2
    [InlineData("100",  "KB", 102_400L)]                 // 100 * 1024
    [InlineData("1024", "B",  1_024L)]
    [InlineData("1",    "TB", 1_099_511_627_776L)]       // 1024^4
    public void ParseSize_IntegerInput_ReturnsCorrectBytes(string value, string unit, long expected)
        => NoPremiumBrowserClient.ParseSize(value, unit).Should().Be(expected);

    [Fact]
    public void ParseSize_CommaDecimalSeparator_TreatedSameAsDot()
    {
        // "22,34" should normalise to "22.34" — result must equal dot variant
        var withComma = NoPremiumBrowserClient.ParseSize("22,34", "GB");
        var withDot   = NoPremiumBrowserClient.ParseSize("22.34", "GB");

        withComma.Should().Be(withDot);
    }

    [Fact]
    public void ParseSize_FractionalGb_IsConsistentWithDataSizeConverter()
    {
        // Use DataSizeConverter as the reference to avoid floating-point constant mismatch
        var expected = NoPremium2.Infrastructure.DataSizeConverter.ParseToBytes("22.34GB");

        NoPremiumBrowserClient.ParseSize("22.34", "GB").Should().Be(expected);
    }

    [Fact]
    public void ParseSize_InvalidNumber_ReturnsZero()
        => NoPremiumBrowserClient.ParseSize("abc", "GB").Should().Be(0L);

    [Fact]
    public void ParseSize_EmptyValue_ReturnsZero()
        => NoPremiumBrowserClient.ParseSize("", "MB").Should().Be(0L);

    [Fact]
    public void ParseSize_WhitespaceValue_ReturnsZero()
        => NoPremiumBrowserClient.ParseSize("   ", "GB").Should().Be(0L);
}

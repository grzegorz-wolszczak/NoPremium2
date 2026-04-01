using AwesomeAssertions;
using NoPremium2.Infrastructure;
using Xunit;

namespace NoPremium2.Tests.Infrastructure;

public sealed class DataSizeConverterTests
{
    // ── ParseToBytes ──────────────────────────────────────────────────

    [Theory]
    [InlineData("1B",   1L)]
    [InlineData("1KB",  1024L)]
    [InlineData("1MB",  1024L * 1024)]
    [InlineData("1GB",  1024L * 1024 * 1024)]
    [InlineData("1TB",  1024L * 1024 * 1024 * 1024)]
    public void ParseToBytes_WholeUnits_ReturnsCorrectBytes(string input, long expected)
        => DataSizeConverter.ParseToBytes(input).Should().Be(expected);

    [Fact]
    public void ParseToBytes_512MB_ReturnsCorrectBytes()
        => DataSizeConverter.ParseToBytes("512MB").Should().Be(512L * 1024 * 1024);

    [Fact]
    public void ParseToBytes_3GB_ReturnsCorrectBytes()
        => DataSizeConverter.ParseToBytes("3GB").Should().Be(3L * 1024 * 1024 * 1024);

    [Theory]
    [InlineData("1.5gb")]
    [InlineData("1.5GB")]
    [InlineData("1.5Gb")]
    public void ParseToBytes_IsCaseInsensitive(string input)
        => DataSizeConverter.ParseToBytes(input).Should().Be((long)(1.5 * 1024 * 1024 * 1024));

    [Fact]
    public void ParseToBytes_FractionalSize_ReturnsCorrectBytes()
        => DataSizeConverter.ParseToBytes("2.5GB").Should().Be((long)(2.5 * 1024 * 1024 * 1024));

    [Fact]
    public void ParseToBytes_PlainNumber_ReturnsAsBytes()
        => DataSizeConverter.ParseToBytes("1024").Should().Be(1024L);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ParseToBytes_EmptyOrWhitespace_ThrowsFormatException(string input)
        => ((Action)(() => DataSizeConverter.ParseToBytes(input))).Should().Throw<FormatException>();

    [Theory]
    [InlineData("invalid")]
    [InlineData("XYZPB")]
    [InlineData("abc MB")]
    public void ParseToBytes_UnrecognizedFormat_ThrowsFormatException(string input)
        => ((Action)(() => DataSizeConverter.ParseToBytes(input))).Should()
            .Throw<FormatException>()
            .WithMessage($"*{input}*");

    // ── FormatBytes ───────────────────────────────────────────────────

    [Fact]
    public void FormatBytes_LessThan1KB_FormatsAsBytes()
        => DataSizeConverter.FormatBytes(512L).Should().Be("512 b");

    [Fact]
    public void FormatBytes_ExactlyOneKB_FormatsAsKB()
        => DataSizeConverter.FormatBytes(1024L).Should().Be("1.00KB (1024 b)");

    [Fact]
    public void FormatBytes_ExactlyOneMB_FormatsAsMB()
        => DataSizeConverter.FormatBytes(1024L * 1024).Should().Be("1.00MB (1048576 b)");

    [Fact]
    public void FormatBytes_ExactlyOneGB_FormatsAsGB()
        => DataSizeConverter.FormatBytes(1024L * 1024 * 1024).Should().Be("1.00GB (1073741824 b)");

    [Fact]
    public void FormatBytes_512MB_FormatsCorrectly()
        => DataSizeConverter.FormatBytes(512L * 1024 * 1024).Should().Be("512.00MB (536870912 b)");

    [Fact]
    public void FormatBytes_ZeroBytes_FormatsAsBytes()
        => DataSizeConverter.FormatBytes(0L).Should().Be("0 b");
}

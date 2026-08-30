using AwesomeAssertions;
using NoPremium2.Browser;
using Xunit;

namespace NoPremium2.Tests.Browser;

public sealed class DevToolsActivePortReaderTests
{
    // ── Parse ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_ValidTwoLines_ReturnsPortAndWsPath()
    {
        var info = DevToolsActivePortReader.Parse("44523\n/devtools/browser/abc-123");

        info.Should().NotBeNull();
        info!.Port.Should().Be(44523);
        info.WebSocketPath.Should().Be("/devtools/browser/abc-123");
    }

    [Fact]
    public void Parse_PortOnly_ReturnsPortNullPath()
    {
        var info = DevToolsActivePortReader.Parse("44523");

        info!.Port.Should().Be(44523);
        info.WebSocketPath.Should().BeNull();
    }

    [Fact]
    public void Parse_CrlfAndWhitespace_Trimmed()
    {
        var info = DevToolsActivePortReader.Parse("  44523  \r\n  /devtools/browser/x  \r\n");

        info!.Port.Should().Be(44523);
        info.WebSocketPath.Should().Be("/devtools/browser/x");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    public void Parse_Invalid_ReturnsNull(string? content)
    {
        DevToolsActivePortReader.Parse(content).Should().BeNull();
    }

    // ── Read ─────────────────────────────────────────────────────────

    [Fact]
    public void Read_FileMissing_ReturnsNull()
    {
        using var dir = new TempDir();
        new DevToolsActivePortReader().Read(dir.Path).Should().BeNull();
    }

    [Fact]
    public void Read_ValidFile_ReturnsInfo()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "DevToolsActivePort"), "51000\n/devtools/browser/z");

        var info = new DevToolsActivePortReader().Read(dir.Path);

        info!.Port.Should().Be(51000);
    }

    [Fact]
    public void Read_StaleFileContents_StillParses_ReaderDoesNotCheckLiveness()
    {
        using var dir = new TempDir();
        File.WriteAllText(Path.Combine(dir.Path, "DevToolsActivePort"), "9\n/devtools/browser/dead");

        new DevToolsActivePortReader().Read(dir.Path)!.Port.Should().Be(9);
    }

    private sealed class TempDir : IDisposable
    {
        public string Path { get; } = Directory.CreateTempSubdirectory("dtap-test-").FullName;
        public void Dispose() { try { Directory.Delete(Path, true); } catch { } }
    }
}

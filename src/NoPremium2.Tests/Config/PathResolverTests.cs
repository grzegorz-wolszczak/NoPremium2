using AwesomeAssertions;
using NoPremium2.Config;
using Xunit;

namespace NoPremium2.Tests.Config;

public sealed class PathResolverTests : IDisposable
{
    // Two temp directories to simulate "config dir" and "app dir"
    private readonly string _configDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
    private readonly string _appDir    = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

    public PathResolverTests()
    {
        Directory.CreateDirectory(_configDir);
        Directory.CreateDirectory(_appDir);
    }

    public void Dispose()
    {
        Directory.Delete(_configDir, recursive: true);
        Directory.Delete(_appDir,    recursive: true);
    }

    private string ConfigFile => Path.Combine(_configDir, "config.json");
    private PathResolver Sut() => new(ConfigFile, _appDir);

    private string CreateFileIn(string dir, string name = "links.json")
    {
        var path = Path.Combine(dir, name);
        File.WriteAllText(path, "{}");
        return path;
    }

    // ── Absolute paths ────────────────────────────────────────────────

    [Fact]
    public void Resolve_AbsolutePath_ExistingFile_ReturnsAbsolutePath()
    {
        var absPath = CreateFileIn(_configDir, "links.json");

        var result = Sut().Resolve(absPath);

        result.Should().Be(absPath);
    }

    [Fact]
    public void Resolve_AbsolutePath_MissingFile_ThrowsFileNotFoundException()
    {
        var absPath = Path.Combine(_configDir, "doesnotexist.json");

        var act = () => Sut().Resolve(absPath);

        act.Should().Throw<FileNotFoundException>()
            .WithMessage($"*{absPath}*");
    }

    // ── Relative: only in config dir ─────────────────────────────────

    [Fact]
    public void Resolve_RelativePath_OnlyInConfigDir_ReturnsConfigDirAbsolutePath()
    {
        CreateFileIn(_configDir, "links.json");

        var result = Sut().Resolve("links.json");

        result.Should().Be(Path.GetFullPath(Path.Combine(_configDir, "links.json")));
    }

    // ── Relative: only in app dir ─────────────────────────────────────

    [Fact]
    public void Resolve_RelativePath_OnlyInAppDir_ReturnsAppDirAbsolutePath()
    {
        CreateFileIn(_appDir, "links.json");

        var result = Sut().Resolve("links.json");

        result.Should().Be(Path.GetFullPath(Path.Combine(_appDir, "links.json")));
    }

    // ── Relative: in both dirs (same path) ───────────────────────────

    [Fact]
    public void Resolve_RelativePath_BothDirsButSameAbsolutePath_ReturnsPath()
    {
        // If configDir and appDir happen to be the same, it's not ambiguous
        var sut = new PathResolver(
            Path.Combine(_configDir, "config.json"),
            _configDir); // same dir for both

        CreateFileIn(_configDir, "links.json");

        var result = sut.Resolve("links.json");

        result.Should().Be(Path.GetFullPath(Path.Combine(_configDir, "links.json")));
    }

    // ── Relative: in both dirs (different paths) — ambiguous ─────────

    [Fact]
    public void Resolve_RelativePath_InBothDirs_ThrowsInvalidOperationException()
    {
        CreateFileIn(_configDir, "links.json");
        CreateFileIn(_appDir,    "links.json");

        var act = () => Sut().Resolve("links.json");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Ambiguous*links.json*")
            .WithMessage($"*{Path.GetFullPath(Path.Combine(_configDir, "links.json"))}*")
            .WithMessage($"*{Path.GetFullPath(Path.Combine(_appDir,    "links.json"))}*");
    }

    // ── Relative: found in neither ────────────────────────────────────

    [Fact]
    public void Resolve_RelativePath_NotFoundAnywhere_ThrowsFileNotFoundException()
    {
        var act = () => Sut().Resolve("missing.json");

        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*missing.json*")
            .WithMessage($"*{Path.GetFullPath(Path.Combine(_configDir, "missing.json"))}*")
            .WithMessage($"*{Path.GetFullPath(Path.Combine(_appDir,    "missing.json"))}*");
    }

    // ── Relative with subdirectory ────────────────────────────────────

    [Fact]
    public void Resolve_RelativePathWithSubdir_OnlyInConfigDir_ResolvesCorrectly()
    {
        var subDir = Path.Combine(_configDir, "data");
        Directory.CreateDirectory(subDir);
        File.WriteAllText(Path.Combine(subDir, "links.json"), "{}");

        var result = Sut().Resolve(Path.Combine("data", "links.json"));

        result.Should().Be(Path.GetFullPath(Path.Combine(_configDir, "data", "links.json")));
    }
}

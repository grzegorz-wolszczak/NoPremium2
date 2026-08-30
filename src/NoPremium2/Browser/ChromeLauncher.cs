using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NoPremium2.Config;

namespace NoPremium2.Browser;

public sealed class ChromeLauncher : IVivaldiLauncher
{
    private static readonly string[] CandidatePaths =
        new[] { "/usr/bin/google-chrome", "/usr/bin/google-chrome-stable", "/usr/bin/chromium-browser", "/usr/bin/chromium" };

    private static readonly string DefaultProfileDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", DefaultConstants.ChromeProfileDirName);

    /// <summary>Grace period before a dead launched process is treated as a handoff.</summary>
    private static readonly TimeSpan HandoffGrace = TimeSpan.FromMilliseconds(2000);

    private readonly AppSettings _settings;
    private readonly ICdpChecker _cdpChecker;
    private readonly IDevToolsActivePortReader _dtapReader;
    private readonly ILogger<ChromeLauncher> _logger;
    private readonly string _executablePath;

    public ChromeLauncher(
        AppSettings settings,
        ICdpChecker cdpChecker,
        IDevToolsActivePortReader dtapReader,
        ILogger<ChromeLauncher> logger)
    {
        _settings = settings;
        _cdpChecker = cdpChecker;
        _dtapReader = dtapReader;
        _logger = logger;
        _executablePath = FindExecutable()
            ?? throw new InvalidOperationException(
                "Chrome not found. Searched: " + string.Join(", ", CandidatePaths));
    }

    public static string? FindExecutable() =>
        CandidatePaths.FirstOrDefault(File.Exists);

    public Process Launch(int port, string profileDir)
    {
        var dir = string.IsNullOrWhiteSpace(profileDir) ? DefaultProfileDir : profileDir;
        Directory.CreateDirectory(dir);
        _logger.LogInformation("Launching Chrome, CDP port {Port}, profile: {Profile}", port, dir);

        // Launch via 'setsid' so the browser runs in a new session.
        // setsid(1) calls the setsid() syscall BEFORE exec'ing the browser, so the browser
        // is never in the terminal's foreground process group and won't receive SIGINT on CTRL+C.
        // No startUrl argument — passing a URL on the command line opens a second tab alongside
        // the default new-tab page. LoginService navigates explicitly.
        var startInfo = new ProcessStartInfo
        {
            FileName = "setsid",
            Arguments = $"{_executablePath} --remote-debugging-port={port} --user-data-dir=\"{dir}\" " +
                        $"--no-first-run --no-default-browser-check",
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Chrome");

        VivaldiLauncher.DrainOutput(process);

        _logger.LogInformation("Chrome started, PID: {Pid}", process.Id);
        return process;
    }

    public Task WaitForCdpAsync(int port, string profileDir, Process? launched, CancellationToken ct = default)
        => CdpWaiter.WaitAsync(port, profileDir, launched, _cdpChecker, _dtapReader,
                               _settings.CdpReadyTimeoutMs, HandoffGrace, _logger, ct);
}

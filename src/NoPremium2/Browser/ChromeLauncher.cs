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

    private readonly AppSettings _settings;
    private readonly ICdpChecker _cdpChecker;
    private readonly ILogger<ChromeLauncher> _logger;
    private readonly string _executablePath;

    public ChromeLauncher(AppSettings settings, ICdpChecker cdpChecker, ILogger<ChromeLauncher> logger)
    {
        _settings = settings;
        _cdpChecker = cdpChecker;
        _logger = logger;
        _executablePath = FindExecutable()
            ?? throw new InvalidOperationException(
                "Chrome not found. Searched: " + string.Join(", ", CandidatePaths));
    }

    public static string? FindExecutable() =>
        CandidatePaths.FirstOrDefault(File.Exists);

    public Process Launch(int port, string profileDir, string startUrl)
    {
        var dir = string.IsNullOrWhiteSpace(profileDir) ? DefaultProfileDir : profileDir;
        Directory.CreateDirectory(dir);
        _logger.LogInformation("Launching Chrome, CDP port {Port}, profile: {Profile}", port, dir);

        var startInfo = new ProcessStartInfo
        {
            FileName = _executablePath,
            Arguments = $"--remote-debugging-port={port} --user-data-dir=\"{dir}\" " +
                        $"--no-first-run --no-default-browser-check {startUrl}",
            UseShellExecute = false,
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Chrome");

        _logger.LogInformation("Chrome started, PID: {Pid}", process.Id);
        return process;
    }

    public async Task WaitForCdpAsync(int port, CancellationToken ct = default)
    {
        _logger.LogDebug("Waiting for Chrome CDP on port {Port}...", port);
        var deadline = DateTime.UtcNow.AddMilliseconds(_settings.CdpReadyTimeoutMs);
        int attempt = 0;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct);
            attempt++;
            if (await _cdpChecker.IsRespondingAsync(port))
            {
                _logger.LogInformation("Chrome CDP ready after {Attempt} attempts", attempt);
                return;
            }
        }
        throw new TimeoutException(
            $"Chrome CDP on port {port} did not start within {_settings.CdpReadyTimeoutMs}ms");
    }
}

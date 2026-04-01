using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace NoPremium2.Browser;

public interface IVivaldiLauncher
{
    Process Launch(int port, string profileDir, string startUrl);
    Task WaitForCdpAsync(int port, CancellationToken ct = default);
}

public sealed class VivaldiLauncher : IVivaldiLauncher
{
    private static readonly string[] CandidatePaths =
        new[] { "/usr/bin/vivaldi", "/usr/bin/vivaldi-stable" };

    public static string? FindExecutable() =>
        CandidatePaths.FirstOrDefault(File.Exists);

    private readonly AppSettings _settings;
    private readonly ICdpChecker _cdpChecker;
    private readonly ILogger<VivaldiLauncher> _logger;

    public VivaldiLauncher(AppSettings settings, ICdpChecker cdpChecker, ILogger<VivaldiLauncher> logger)
    {
        _settings = settings;
        _cdpChecker = cdpChecker;
        _logger = logger;
    }

    public Process Launch(int port, string profileDir, string startUrl)
    {
        Directory.CreateDirectory(profileDir);
        _logger.LogInformation("Launching Vivaldi, CDP port {Port}, profile: {Profile}", port, profileDir);

        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.VivaldiPath,
            Arguments = $"--remote-debugging-port={port} --user-data-dir=\"{profileDir}\" --no-first-run --no-default-browser-check {startUrl}",
            UseShellExecute = false,
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Vivaldi");

        _logger.LogInformation("Vivaldi started, PID: {Pid}", process.Id);
        return process;
    }

    public async Task WaitForCdpAsync(int port, CancellationToken ct = default)
    {
        _logger.LogDebug("Waiting for CDP on port {Port}...", port);
        var deadline = DateTime.UtcNow.AddMilliseconds(_settings.CdpReadyTimeoutMs);
        int attempt = 0;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct);
            attempt++;
            if (await _cdpChecker.IsRespondingAsync(port))
            {
                _logger.LogInformation("CDP ready after {Attempt} attempts", attempt);
                return;
            }
            _logger.LogDebug("CDP not ready yet (attempt {Attempt})", attempt);
        }
        throw new TimeoutException($"CDP on port {port} did not start within {_settings.CdpReadyTimeoutMs}ms");
    }
}

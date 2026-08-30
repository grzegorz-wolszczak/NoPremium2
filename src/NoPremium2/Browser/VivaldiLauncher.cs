using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace NoPremium2.Browser;

public interface IVivaldiLauncher
{
    Process Launch(int port, string profileDir);
    Task WaitForCdpAsync(int port, string profileDir, Process? launched, CancellationToken ct = default);
}

public sealed class VivaldiLauncher : IVivaldiLauncher
{
    private static readonly string[] CandidatePaths =
        new[] { "/usr/bin/vivaldi", "/usr/bin/vivaldi-stable" };

    public static string? FindExecutable() =>
        CandidatePaths.FirstOrDefault(File.Exists);

    /// <summary>Grace period before a dead launched process is treated as a handoff.</summary>
    private static readonly TimeSpan HandoffGrace = TimeSpan.FromMilliseconds(2000);

    private readonly AppSettings _settings;
    private readonly ICdpChecker _cdpChecker;
    private readonly IDevToolsActivePortReader _dtapReader;
    private readonly ILogger<VivaldiLauncher> _logger;

    public VivaldiLauncher(
        AppSettings settings,
        ICdpChecker cdpChecker,
        IDevToolsActivePortReader dtapReader,
        ILogger<VivaldiLauncher> logger)
    {
        _settings = settings;
        _cdpChecker = cdpChecker;
        _dtapReader = dtapReader;
        _logger = logger;
    }

    public Process Launch(int port, string profileDir)
    {
        Directory.CreateDirectory(profileDir);
        _logger.LogInformation("Launching Vivaldi, CDP port {Port}, profile: {Profile}", port, profileDir);

        // Launch via 'setsid' so the browser runs in a new session.
        // setsid(1) calls the setsid() syscall BEFORE exec'ing the browser, so the browser
        // is never in the terminal's foreground process group and won't receive SIGINT on CTRL+C.
        // No startUrl argument — passing a URL on the command line opens a second tab alongside
        // the default new-tab page, resulting in two tabs. LoginService navigates to the login
        // page explicitly, so no URL is needed at launch time.
        var startInfo = new ProcessStartInfo
        {
            FileName = "setsid",
            Arguments = $"{_settings.VivaldiPath} --remote-debugging-port={port} --user-data-dir=\"{profileDir}\" --no-first-run --no-default-browser-check",
            UseShellExecute = false,
            RedirectStandardError = true,   // suppress Chromium GCM/internal noise from stdout
            RedirectStandardOutput = true,
        };

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start Vivaldi");

        // Drain both pipes — Chromium is chatty on stderr and an unread pipe deadlocks
        // the browser once the OS buffer (~64 KB) fills.
        DrainOutput(process);

        _logger.LogInformation("Vivaldi started, PID: {Pid}", process.Id);
        return process;
    }

    public Task WaitForCdpAsync(int port, string profileDir, Process? launched, CancellationToken ct = default)
        => CdpWaiter.WaitAsync(port, profileDir, launched, _cdpChecker, _dtapReader,
                               _settings.CdpReadyTimeoutMs, HandoffGrace, _logger, ct);

    internal static void DrainOutput(Process process)
    {
        process.OutputDataReceived += static (_, _) => { };
        process.ErrorDataReceived += static (_, _) => { };
        try
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
        catch (InvalidOperationException)
        {
            // redirection not enabled (shouldn't happen here) — nothing to drain
        }
    }
}

/// <summary>Shared CDP-ready polling with handoff detection, used by both launchers.</summary>
internal static class CdpWaiter
{
    public static async Task WaitAsync(
        int port,
        string profileDir,
        Process? launched,
        ICdpChecker cdpChecker,
        IDevToolsActivePortReader dtapReader,
        int timeoutMs,
        TimeSpan handoffGrace,
        ILogger logger,
        CancellationToken ct)
    {
        logger.LogDebug("Waiting for CDP on port {Port}...", port);
        var start = DateTime.UtcNow;
        var deadline = start.AddMilliseconds(timeoutMs);
        int attempt = 0;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct);
            attempt++;

            if (await cdpChecker.IsRespondingAsync(port, ct))
            {
                logger.LogInformation("CDP ready after {Attempt} attempts", attempt);
                return;
            }

            // Handoff signal 1: Chromium wrote DevToolsActivePort for a DIFFERENT port.
            var info = dtapReader.Read(profileDir);
            if (info is not null && info.Port != port)
                throw HandoffException(port, info.Port);

            // Handoff signal 2: the process we started exited almost immediately
            // without CDP ever coming up (ProcessSingleton forwarded the launch).
            if (launched is { HasExited: true } && DateTime.UtcNow - start > handoffGrace)
                throw HandoffException(port, null);

            logger.LogDebug("CDP not ready yet (attempt {Attempt})", attempt);
        }

        throw new TimeoutException($"CDP on port {port} did not start within {timeoutMs}ms");
    }

    private static InvalidOperationException HandoffException(int requestedPort, int? actualPort)
    {
        var actual = actualPort is int p
            ? $"Another browser instance is already running for this profile (it is on CDP port {p})."
            : "The launched browser exited immediately — another instance is already running for this profile and took over the launch.";
        return new InvalidOperationException(
            $"Browser did not open CDP port {requestedPort}. {actual} " +
            "Zamknij wszystkie okna przeglądarki tego profilu i uruchom NoPremium2 ponownie.");
    }
}

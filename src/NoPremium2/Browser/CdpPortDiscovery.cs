using Microsoft.Extensions.Logging;

namespace NoPremium2.Browser;

public interface IProcessCmdlineReader
{
    IEnumerable<(int Pid, string? Cmdline)> GetAll();
}

public sealed class LinuxProcessCmdlineReader : IProcessCmdlineReader
{
    public IEnumerable<(int Pid, string? Cmdline)> GetAll()
    {
        foreach (var dir in Directory.GetDirectories("/proc"))
        {
            if (!int.TryParse(Path.GetFileName(dir), out int pid)) continue;
            string cmdlinePath = Path.Combine(dir, "cmdline");
            string? cmdline = null;
            try { cmdline = File.Exists(cmdlinePath) ? File.ReadAllText(cmdlinePath) : null; } catch { }
            if (cmdline != null)
                yield return (pid, cmdline);
        }
    }
}

public interface ICdpPortDiscovery
{
    /// <summary>
    /// Finds a running browser that owns <paramref name="profileDir"/> (matched via
    /// <c>--user-data-dir</c>) and has a responding CDP port. Returns null if none.
    /// </summary>
    Task<int?> FindExistingPortAsync(string profileDir);
}

public sealed class CdpPortDiscovery : ICdpPortDiscovery
{
    private readonly IProcessCmdlineReader _cmdlineReader;
    private readonly ICdpChecker _cdpChecker;
    private readonly ILogger<CdpPortDiscovery> _logger;

    public CdpPortDiscovery(IProcessCmdlineReader cmdlineReader, ICdpChecker cdpChecker, ILogger<CdpPortDiscovery> logger)
    {
        _cmdlineReader = cmdlineReader;
        _cdpChecker = cdpChecker;
        _logger = logger;
    }

    public async Task<int?> FindExistingPortAsync(string profileDir)
    {
        foreach (var (pid, cmdline) in _cmdlineReader.GetAll())
        {
            int? port = ParsePort(cmdline);
            if (port is null) continue;

            // Only trust a port belonging to a browser started against OUR profile —
            // otherwise we could hijack the user's personal browser on another profile.
            if (!CmdlineMatchesProfile(cmdline, profileDir))
            {
                _logger.LogDebug("Ignoring PID {Pid} CDP port {Port} — different --user-data-dir", pid, port);
                continue;
            }

            if (await _cdpChecker.IsRespondingAsync(port.Value))
            {
                _logger.LogDebug("Found browser (PID {Pid}) with CDP on port {Port} for {Profile}", pid, port, profileDir);
                return port;
            }
        }
        _logger.LogDebug("No existing browser with open CDP port found for {Profile}", profileDir);
        return null;
    }

    /// <summary>Parses --remote-debugging-port=XXXX from a null-delimited cmdline string.</summary>
    public static int? ParsePort(string? cmdline)
        => ParseFlagInt(cmdline, "--remote-debugging-port=");

    /// <summary>Parses --user-data-dir=PATH from a null-delimited cmdline string.</summary>
    public static string? ParseUserDataDir(string? cmdline)
        => ParseFlagString(cmdline, "--user-data-dir=");

    /// <summary>True if <paramref name="cmdline"/> is a browser launched with <c>--user-data-dir</c> == <paramref name="profileDir"/>.</summary>
    public static bool CmdlineMatchesProfile(string? cmdline, string profileDir)
    {
        var dir = ParseUserDataDir(cmdline);
        if (dir is null) return false;
        return string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(dir)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(profileDir)),
            StringComparison.Ordinal);
    }

    private static string? ParseFlagString(string? cmdline, string flag)
    {
        if (cmdline is null) return null;
        int idx = cmdline.IndexOf(flag, StringComparison.Ordinal);
        if (idx < 0) return null;
        int start = idx + flag.Length;
        int end = cmdline.IndexOf('\0', start);
        string value = end > start ? cmdline[start..end] : cmdline[start..];
        value = value.Trim().Trim('"');
        return value.Length > 0 ? value : null;
    }

    private static int? ParseFlagInt(string? cmdline, string flag)
    {
        string? value = ParseFlagString(cmdline, flag);
        return value is not null && int.TryParse(value, out int n) && n > 0 ? n : null;
    }
}

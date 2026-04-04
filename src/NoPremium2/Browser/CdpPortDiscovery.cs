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
    Task<int?> FindExistingPortAsync();
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

    public async Task<int?> FindExistingPortAsync()
    {
        foreach (var (pid, cmdline) in _cmdlineReader.GetAll())
        {
            int? port = ParsePort(cmdline);
            if (port is null) continue;
            if (await _cdpChecker.IsRespondingAsync(port.Value))
            {
                _logger.LogDebug("Found browser (PID {Pid}) with CDP on port {Port}", pid, port);
                return port;
            }
        }
        _logger.LogDebug("No existing browser with open CDP port found");
        return null;
    }

    /// <summary>Parses --remote-debugging-port=XXXX from a null-delimited cmdline string.</summary>
    public static int? ParsePort(string? cmdline)
    {
        if (cmdline is null) return null;
        const string flag = "--remote-debugging-port=";
        int idx = cmdline.IndexOf(flag, StringComparison.Ordinal);
        if (idx < 0) return null;
        int start = idx + flag.Length;
        int end = cmdline.IndexOf('\0', start);
        string portStr = end > start ? cmdline[start..end] : cmdline[start..];
        return int.TryParse(portStr.Trim(), out int port) && port > 0 ? port : null;
    }
}

using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace NoPremium2.Browser;

public interface IProcessCmdlineReader
{
    IEnumerable<(int Pid, string? Cmdline)> GetByName(string processName);
}

public sealed class LinuxProcessCmdlineReader : IProcessCmdlineReader
{
    public IEnumerable<(int Pid, string? Cmdline)> GetByName(string processName)
    {
        foreach (var proc in Process.GetProcessesByName(processName))
        {
            string path = $"/proc/{proc.Id}/cmdline";
            string? cmdline = null;
            try { cmdline = File.Exists(path) ? File.ReadAllText(path) : null; } catch { }
            yield return (proc.Id, cmdline);
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
        foreach (var (pid, cmdline) in _cmdlineReader.GetByName("vivaldi"))
        {
            int? port = ParsePort(cmdline);
            if (port is null) continue;
            if (await _cdpChecker.IsRespondingAsync(port.Value))
            {
                _logger.LogDebug("Found Vivaldi (PID {Pid}) with CDP on port {Port}", pid, port);
                return port;
            }
        }
        _logger.LogDebug("No existing Vivaldi with open CDP port found");
        return null;
    }

    /// <summary>Parses --remote-debugging-port=XXXX from a null-delimited cmdline string.</summary>
    internal static int? ParsePort(string? cmdline)
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

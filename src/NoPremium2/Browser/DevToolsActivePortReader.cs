namespace NoPremium2.Browser;

/// <summary>Parsed contents of a Chromium <c>DevToolsActivePort</c> file.</summary>
public sealed record DevToolsActivePortInfo(int Port, string? WebSocketPath);

public interface IDevToolsActivePortReader
{
    /// <summary>
    /// Reads <c>&lt;profileDir&gt;/DevToolsActivePort</c>. Returns null if the file is
    /// absent or unparseable. Does NOT verify the port is still alive — a stale file
    /// is left behind after a browser crash.
    /// </summary>
    DevToolsActivePortInfo? Read(string profileDir);
}

public sealed class DevToolsActivePortReader : IDevToolsActivePortReader
{
    public const string FileName = "DevToolsActivePort";

    public DevToolsActivePortInfo? Read(string profileDir)
    {
        try
        {
            var path = Path.Combine(profileDir, FileName);
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Line 1 = the actual TCP port the DevTools HTTP server bound to.
    /// Line 2 (optional) = the browser-level WebSocket target path.
    /// </summary>
    public static DevToolsActivePortInfo? Parse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        var lines = content.Split('\n');
        if (!int.TryParse(lines[0].Trim(), out int port) || port <= 0) return null;

        string? wsPath = lines.Length > 1 && lines[1].Trim().Length > 0
            ? lines[1].Trim()
            : null;

        return new DevToolsActivePortInfo(port, wsPath);
    }
}

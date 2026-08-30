using Microsoft.Extensions.Logging;

namespace NoPremium2.Browser;

public enum ProfileLockStatus
{
    /// <summary>No <c>SingletonLock</c> present — profile is free.</summary>
    NoLock,
    /// <summary>Lock present and the owning browser process is alive on this host.</summary>
    OwnerAlive,
    /// <summary>Lock present but stale — owner gone, wrong host, or PID reused.</summary>
    OwnerDead,
}

public readonly record struct ProfileLockResult(ProfileLockStatus Status, int? OwnerPid);

public interface ISingletonLockReader
{
    /// <summary>Returns the raw symlink target of <c>&lt;profileDir&gt;/SingletonLock</c> (<c>host-pid</c>), or null.</summary>
    string? ReadTarget(string profileDir);
}

public sealed class SingletonLockReader : ISingletonLockReader
{
    public const string FileName = "SingletonLock";

    public string? ReadTarget(string profileDir)
    {
        try
        {
            var path = Path.Combine(profileDir, FileName);
            // SingletonLock is a symlink whose target ("host-pid") is not a real path,
            // so resolve one level without following to a filesystem entry.
            var target = File.ResolveLinkTarget(path, returnFinalTarget: false);
            return target?.Name;
        }
        catch
        {
            return null;
        }
    }
}

public interface IProfileLockInspector
{
    ProfileLockResult Inspect(string profileDir);
}

public sealed class ProfileLockInspector : IProfileLockInspector
{
    private readonly ISingletonLockReader _lockReader;
    private readonly IProcessCmdlineReader _cmdlineReader;
    private readonly ILogger<ProfileLockInspector> _logger;

    public ProfileLockInspector(
        ISingletonLockReader lockReader,
        IProcessCmdlineReader cmdlineReader,
        ILogger<ProfileLockInspector> logger)
    {
        _lockReader = lockReader;
        _cmdlineReader = cmdlineReader;
        _logger = logger;
    }

    public ProfileLockResult Inspect(string profileDir)
    {
        var target = _lockReader.ReadTarget(profileDir);
        if (target is null)
            return new ProfileLockResult(ProfileLockStatus.NoLock, null);

        var parsed = ParseSingletonLockTarget(target);
        if (parsed is null)
        {
            _logger.LogDebug("SingletonLock target '{Target}' unparseable — treating as stale", target);
            return new ProfileLockResult(ProfileLockStatus.OwnerDead, null);
        }

        var (host, pid) = parsed.Value;
        if (!string.Equals(host, Environment.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("SingletonLock owned by another host '{Host}' — treating as stale", host);
            return new ProfileLockResult(ProfileLockStatus.OwnerDead, null);
        }

        // Verify the PID is alive AND is actually a browser for THIS profile
        // (guards against PID reuse by an unrelated process).
        foreach (var (procPid, cmdline) in _cmdlineReader.GetAll())
        {
            if (procPid != pid) continue;
            if (cmdline is not null && CdpPortDiscovery.CmdlineMatchesProfile(cmdline, profileDir))
                return new ProfileLockResult(ProfileLockStatus.OwnerAlive, pid);

            _logger.LogDebug("PID {Pid} alive but not a browser for {Profile} — stale lock (PID reuse)", pid, profileDir);
            return new ProfileLockResult(ProfileLockStatus.OwnerDead, null);
        }

        _logger.LogDebug("SingletonLock PID {Pid} not running — stale lock", pid);
        return new ProfileLockResult(ProfileLockStatus.OwnerDead, null);
    }

    /// <summary>Splits a <c>host-pid</c> target on the LAST '-' (hostnames may contain '-').</summary>
    public static (string Host, int Pid)? ParseSingletonLockTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;

        int dash = target.LastIndexOf('-');
        if (dash <= 0 || dash == target.Length - 1) return null;

        string host = target[..dash];
        if (!int.TryParse(target[(dash + 1)..], out int pid) || pid <= 0) return null;

        return (host, pid);
    }
}

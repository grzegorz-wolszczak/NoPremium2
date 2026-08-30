using NoPremium2.Config;

namespace NoPremium2;

public sealed record AppSettings
{
    public string VivaldiPath { get; init; } = "/usr/bin/vivaldi";
    public string ProfileDir { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", DefaultConstants.VivaldiProfileDirName);
    public string LoginUrl { get; init; } = DefaultConstants.LoginUrl;
    public int CdpReadyTimeoutMs { get; init; } = DefaultConstants.CdpReadyTimeoutMs;
    public int TurnstileTimeoutMs { get; init; } = DefaultConstants.TurnstileTimeoutMs;

    /// <summary>
    /// When a browser is already running for our profile but its CDP port is
    /// unreachable: true = kill it and relaunch, false (default) = abort with a
    /// message asking the user to close the window.
    /// </summary>
    public bool KillStaleBrowser { get; init; } = DefaultConstants.KillStaleBrowser;

    /// <summary>Creates AppSettings populated from a BaseConfig instance.</summary>
    /// <param name="config">Source configuration.</param>
    /// <param name="profileDir">Resolved browser user-data-dir (browser-specific).</param>
    /// <param name="browserPath">Path to the browser executable, or null to keep the default.</param>
    public static AppSettings From(Config.BaseConfig config, string profileDir, string? browserPath = null) => new()
    {
        ProfileDir = profileDir,
        VivaldiPath = browserPath ?? new AppSettings().VivaldiPath,
        LoginUrl = config.LoginUrl,
        CdpReadyTimeoutMs = config.CdpReadyTimeoutMs,
        TurnstileTimeoutMs = config.TurnstileTimeoutMs,
        KillStaleBrowser = config.KillStaleBrowser,
    };
}

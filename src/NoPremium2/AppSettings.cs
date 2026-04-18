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

    /// <summary>Creates AppSettings populated from an AppConfig instance.</summary>
    public static AppSettings From(Config.BaseConfig config) => new()
    {
        LoginUrl = config.LoginUrl,
        CdpReadyTimeoutMs = config.CdpReadyTimeoutMs,
        TurnstileTimeoutMs = config.TurnstileTimeoutMs,
    };
}
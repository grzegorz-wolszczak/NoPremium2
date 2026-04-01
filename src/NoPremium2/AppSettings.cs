namespace NoPremium2;

public sealed record AppSettings
{
    public string VivaldiPath { get; init; } = "/usr/bin/vivaldi";
    public string ProfileDir { get; init; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config", "vivaldi-nopremium");
    public string LoginUrl { get; init; } = "https://www.nopremium.pl/login";
    public int CdpReadyTimeoutMs { get; init; } = 10_000;
    public int TurnstileTimeoutMs { get; init; } = 120_000;

    /// <summary>Creates AppSettings populated from an AppConfig instance.</summary>
    public static AppSettings From(Config.AppConfig config) => new()
    {
        LoginUrl = config.LoginUrl,
        CdpReadyTimeoutMs = config.CdpReadyTimeoutMs,
        TurnstileTimeoutMs = config.TurnstileTimeoutMs,
    };
}

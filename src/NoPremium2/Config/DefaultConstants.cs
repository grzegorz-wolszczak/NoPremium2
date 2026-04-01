namespace NoPremium2.Config;

/// <summary>
/// Single source of truth for all default configuration values.
/// Every default used across the codebase must be defined here.
/// </summary>
public static class DefaultConstants
{
    // ── Scheduling ────────────────────────────────────────────────────
    public const string ScheduleStartTime      = "23:00";
    public const string ScheduleEndTime        = "23:55";
    public const int    ScheduleIntervalMinutes = 5;

    // ── Transfer consumer ─────────────────────────────────────────────
    /// <summary>3 GB in bytes — minimum premium transfer to keep in reserve.</summary>
    public const long ReserveTransferBytes = 3_221_225_472L;

    // ── Keepalive ─────────────────────────────────────────────────────
    /// <summary>How often the keepalive navigation runs (HH:mm:ss).</summary>
    public const string KeepaliveInterval = "01:00:00";

    // ── Browser / login ───────────────────────────────────────────────
    public const string LoginUrl          = "https://www.nopremium.pl/login";
    public const int    CdpReadyTimeoutMs = 10_000;
    public const int    TurnstileTimeoutMs = 120_000;

    // ── Browser profile directory names ──────────────────────────────
    public const string ChromeProfileDirName  = "chrome-nopremium";
    public const string VivaldiProfileDirName = "vivaldi-nopremium";
}

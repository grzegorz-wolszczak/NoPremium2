namespace NoPremium2.Config;

public sealed record AppConfig
{
    // --- Required ---
    public string NoPremiumUsername { get; init; } = "";
    public string NoPremiumPassword { get; init; } = "";
    public string EmailUsername { get; init; } = "";
    public string EmailPassword { get; init; } = "";
    /// <summary>IMAP server in "host:port" format, e.g. "imap.gmx.com:993"</summary>
    public string EmailImapServer { get; init; } = "";
    public string LinksFilePath { get; init; } = "";

    // --- Optional: log directory ---
    /// <summary>
    /// Directory for log files. Empty/null = default (Logs/ next to binary).
    /// Relative path = relative to binary directory.
    /// Absolute path = used as-is. Directory is created on startup.
    /// </summary>
    public string LogFileDir { get; init; } = "";

    // --- Optional: transfer-consumer schedule ---
    public TransferConsumerConfig TransferConsumer { get; init; } = new();

    // --- Optional: voucher-consumer schedule ---
    public VoucherConsumerConfig VoucherConsumer { get; init; } = new();

    // --- Optional: general ---
    /// <summary>How often keepalive navigation runs, e.g. "01:00:00"</summary>
    public string KeepaliveInterval { get; init; } = DefaultConstants.KeepaliveInterval;

    // --- Browser / login (internal, not in user config file) ---
    public string LoginUrl { get; init; } = DefaultConstants.LoginUrl;
    public int CdpReadyTimeoutMs { get; init; } = DefaultConstants.CdpReadyTimeoutMs;
    public int TurnstileTimeoutMs { get; init; } = DefaultConstants.TurnstileTimeoutMs;
}

public sealed record TransferConsumerConfig
{
    public string StartTime { get; init; } = DefaultConstants.ScheduleStartTime;
    public string EndTime { get; init; } = DefaultConstants.ScheduleEndTime;
    public int IntervalMinutes { get; init; } = DefaultConstants.ScheduleIntervalMinutes;
    /// <summary>Minimum premium transfer to keep in reserve (bytes). Default = 3 GB.</summary>
    public long ReserveTransferBytes { get; init; } = DefaultConstants.ReserveTransferBytes;
}

public sealed record VoucherConsumerConfig
{
    public string StartTime { get; init; } = DefaultConstants.ScheduleStartTime;
    public string EndTime { get; init; } = DefaultConstants.ScheduleEndTime;
    public int IntervalMinutes { get; init; } = DefaultConstants.ScheduleIntervalMinutes;
}

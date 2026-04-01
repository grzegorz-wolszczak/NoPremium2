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

    // --- Optional: transfer-consumer schedule ---
    public TransferConsumerConfig TransferConsumer { get; init; } = new();

    // --- Optional: voucher-consumer schedule ---
    public VoucherConsumerConfig VoucherConsumer { get; init; } = new();

    // --- Optional: general ---
    /// <summary>How often keepalive navigation runs, e.g. "01:00:00"</summary>
    public string KeepaliveInterval { get; init; } = "01:00:00";

    // --- Browser / login (internal, not in user config file) ---
    public string LoginUrl { get; init; } = "https://www.nopremium.pl/login";
    public int CdpReadyTimeoutMs { get; init; } = 10_000;
    public int TurnstileTimeoutMs { get; init; } = 120_000;
}

public sealed record TransferConsumerConfig
{
    public string StartTime { get; init; } = "23:00";
    public string EndTime { get; init; } = "23:55";
    public int IntervalMinutes { get; init; } = 5;
    /// <summary>Minimum premium transfer to keep in reserve (bytes). Default = 3 GB.</summary>
    public long ReserveTransferBytes { get; init; } = 3_221_225_472L;
}

public sealed record VoucherConsumerConfig
{
    public string StartTime { get; init; } = "23:00";
    public string EndTime { get; init; } = "23:55";
    public int IntervalMinutes { get; init; } = 5;
}

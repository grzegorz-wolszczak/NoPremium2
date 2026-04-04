using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NoPremium2.Config;

public static class ConfigLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig LoadAppConfig(string filePath, ILogger logger)
    {
        if (!File.Exists(filePath))
            ExitWithError($"Config file not found: {filePath}");

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            ExitWithError($"Cannot read config file '{filePath}': {ex.Message}");
            return null!; // unreachable
        }

        AppConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            ExitWithError($"Cannot parse config file '{filePath}': {ex.Message}");
            return null!;
        }

        if (config is null)
        {
            ExitWithError("Config file is empty or could not be parsed.");
            return null!;
        }

        // Apply defaults for empty optional values
        config = ApplyDefaults(config);

        // Validate required fields
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(config.NoPremiumUsername)) missing.Add("NoPremiumUsername");
        if (string.IsNullOrWhiteSpace(config.NoPremiumPassword)) missing.Add("NoPremiumPassword");
        if (string.IsNullOrWhiteSpace(config.EmailUsername)) missing.Add("EmailUsername");
        if (string.IsNullOrWhiteSpace(config.EmailPassword)) missing.Add("EmailPassword");
        if (string.IsNullOrWhiteSpace(config.EmailImapServer)) missing.Add("EmailImapServer");
        if (string.IsNullOrWhiteSpace(config.LinksFilePath)) missing.Add("LinksFilePath");

        if (missing.Count > 0)
            ExitWithError($"Missing required configuration fields: {string.Join(", ", missing)}");

        logger.LogInformation("Config loaded from: {Path}", filePath);
        return config;
    }

    /// <summary>
    /// Resolves the LinksFilePath from AppConfig (may be relative) then loads the file.
    /// </summary>
    public static LinksConfig LoadLinksConfig(string configFilePath, AppConfig config)
    {
        string resolvedPath;
        try
        {
            var resolver = new PathResolver(configFilePath, AppContext.BaseDirectory);
            resolvedPath = resolver.Resolve(config.LinksFilePath);
        }
        catch (FileNotFoundException ex)
        {
            ExitWithError($"Links file not found. {ex.Message}");
            return null!;
        }
        catch (InvalidOperationException ex)
        {
            ExitWithError(ex.Message);
            return null!;
        }

        return LoadLinksConfig(resolvedPath);
    }

    public static LinksConfig LoadLinksConfig(string filePath)
    {
        if (!File.Exists(filePath))
            ExitWithError($"Links file not found: {filePath}");

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            ExitWithError($"Cannot read links file '{filePath}': {ex.Message}");
            return null!;
        }

        LinksConfig? links;
        try
        {
            links = JsonSerializer.Deserialize<LinksConfig>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            ExitWithError($"Cannot parse links file '{filePath}': {ex.Message}");
            return null!;
        }

        if (links is null || links.Links.Count == 0)
            ExitWithError($"Links file '{filePath}' is empty or has no entries.");

        return links!;
    }

    public static (string Host, int Port) ParseImapServer(string imapServer)
    {
        var parts = imapServer.Split(':', 2);
        if (parts.Length != 2 || !int.TryParse(parts[1], out int port))
        {
            ExitWithError($"Invalid EmailImapServer format '{imapServer}'. Expected 'host:port', e.g. 'imap.gmx.com:993'");
            return default; // unreachable
        }
        return (parts[0], port);
    }

    public static AppConfig ApplyDefaults(AppConfig config)
    {
        var tc = config.TransferConsumer;
        var vc = config.VoucherConsumer;

        return config with
        {
            KeepaliveInterval = NullOrEmpty(config.KeepaliveInterval) ? DefaultConstants.KeepaliveInterval : config.KeepaliveInterval,
            TransferConsumer = tc with
            {
                StartTime        = NullOrEmpty(tc.StartTime)    ? DefaultConstants.ScheduleStartTime      : tc.StartTime,
                EndTime          = NullOrEmpty(tc.EndTime)      ? DefaultConstants.ScheduleEndTime        : tc.EndTime,
                IntervalMinutes  = tc.IntervalMinutes  <= 0     ? DefaultConstants.ScheduleIntervalMinutes : tc.IntervalMinutes,
                ReserveTransferBytes = tc.ReserveTransferBytes <= 0 ? DefaultConstants.ReserveTransferBytes : tc.ReserveTransferBytes,
            },
            VoucherConsumer = vc with
            {
                StartTime       = NullOrEmpty(vc.StartTime)   ? DefaultConstants.ScheduleStartTime      : vc.StartTime,
                EndTime         = NullOrEmpty(vc.EndTime)     ? DefaultConstants.ScheduleEndTime        : vc.EndTime,
                IntervalMinutes = vc.IntervalMinutes <= 0     ? DefaultConstants.ScheduleIntervalMinutes : vc.IntervalMinutes,
            },
        };
    }

    public static bool NullOrEmpty(string? s) => string.IsNullOrWhiteSpace(s);

    private static void ExitWithError(string message)
    {
        Console.Error.WriteLine($"[STARTUP ERROR] {message}");
        Environment.Exit(1);
    }
}

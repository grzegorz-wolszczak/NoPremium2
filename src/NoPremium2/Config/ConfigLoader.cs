using System.Text.Json;
using Microsoft.Extensions.Logging;
using NoPremium2.Infrastructure;

namespace NoPremium2.Config;

public  class ConfigLoader
{
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public ConfigLoader(ILogger logger)
    {
        _logger = logger;
    }

    public AppConfig LoadConfig(string filePath)
    {
        BaseConfig config = LoadAppConfig(filePath);
        LinksConfig links = LoadLinksConfig(filePath, config);
        ValidateLinks(links, filePath);
        ValidateScheduleOverlap(config);
        var (imapHost, imapPort) = ParseImapServer(config.EmailImapServer);

        string logDir;
        if (string.IsNullOrWhiteSpace(config.LogFileDir))
        {
            logDir = Path.Combine(AppContext.BaseDirectory, "Logs");
        }
        else if (Path.IsPathRooted(config.LogFileDir))
        {
            logDir = config.LogFileDir;
        }
        else
        {
            logDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, config.LogFileDir));
        }

        return new AppConfig()
        {
            LogDir = logDir,
            BaseConfig = config,
            LinksConfig = links,
            MailConfig = new MailConfig()
            {
                MailHost = imapHost,
                MailPort = imapPort,
            }
        };
    }

    /// <summary>
    /// Validates that every link entry has a non-empty URL and a parseable Size field.
    /// Calls Environment.Exit(1) on the first invalid entry.
    /// </summary>
    private static void ValidateLinks(LinksConfig links, string configFilePath)
    {
        var errors = new List<string>();

        for (int i = 0; i < links.Links.Count; i++)
        {
            var entry = links.Links[i];
            string label = string.IsNullOrWhiteSpace(entry.Name) ? $"[{i}]" : $"'{entry.Name}'";

            if (string.IsNullOrWhiteSpace(entry.Url))
                errors.Add($"  Link {label}: missing URL");

            if (string.IsNullOrWhiteSpace(entry.Size))
            {
                errors.Add($"  Link {label}: missing Size");
            }
            else
            {
                try
                {
                    DataSizeConverter.ParseToBytes(entry.Size);
                }
                catch (FormatException)
                {
                    errors.Add($"  Link {label}: cannot parse Size '{entry.Size}'");
                }
            }

            if (errors.Count > 0)
            {
                Console.Error.WriteLine($"[STARTUP ERROR] Links file referenced from '{configFilePath}' contains invalid entries:");
                foreach (var e in errors)
                    Console.Error.WriteLine(e);
                Environment.Exit(1);
            }
        }
    }

    /// <summary>
    /// Validates that the TransferConsumer and VoucherConsumer schedules do not overlap.
    /// Calls Environment.Exit(1) if they do.
    /// </summary>
    private static void ValidateScheduleOverlap(BaseConfig config)
    {
        var tc = config.TransferConsumer;
        var vc = config.VoucherConsumer;

        var tcStart = Services.ScheduleHelper.ParseTimeOnly(tc.StartTime, Config.DefaultConstants.ScheduleStartTime);
        var tcEnd   = Services.ScheduleHelper.ParseTimeOnly(tc.EndTime,   Config.DefaultConstants.ScheduleEndTime);
        var vcStart = Services.ScheduleHelper.ParseTimeOnly(vc.StartTime, Config.DefaultConstants.ScheduleStartTime);
        var vcEnd   = Services.ScheduleHelper.ParseTimeOnly(vc.EndTime,   Config.DefaultConstants.ScheduleEndTime);

        if (Services.ScheduleHelper.SchedulesOverlap(tcStart, tcEnd, vcStart, vcEnd))
        {
            Console.Error.WriteLine(
                $"[STARTUP ERROR] TransferConsumer schedule ({tc.StartTime}–{tc.EndTime}) overlaps with " +
                $"VoucherConsumer schedule ({vc.StartTime}–{vc.EndTime}). " +
                "These schedules must not overlap to avoid browser navigation conflicts.");
            Environment.Exit(1);
        }
    }


    public BaseConfig LoadAppConfig(string filePath)
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

        BaseConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<BaseConfig>(json, JsonOptions);
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

        _logger.LogInformation("Config loaded from: {Path}", filePath);
        return config;
    }

    /// <summary>
    /// Resolves the LinksFilePath from AppConfig (may be relative) then loads the file.
    /// </summary>
    public LinksConfig LoadLinksConfig(string configFilePath, BaseConfig config)
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

    private static LinksConfig LoadLinksConfig(string filePath)
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

    public static BaseConfig ApplyDefaults(BaseConfig config)
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
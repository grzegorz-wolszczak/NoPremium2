using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoPremium2.Browser;
using NoPremium2.Config;
using NoPremium2.Email;
using NoPremium2.Infrastructure;
using NoPremium2.Login;
using NoPremium2.NoPremium;
using NoPremium2.Services;
using Serilog;

namespace NoPremium2;

internal sealed class Program
{
    public static async Task Main(string[] args)
    {
        // ─────────────────────────────────────────────────────────────────────
        // 1.  Parse CLI argument: path to config file
        // ─────────────────────────────────────────────────────────────────────
        if (args.Length == 0)
        {
            Console.Error.WriteLine("[STARTUP ERROR] Usage: NoPremium2 <path-to-config.json>");
            Environment.Exit(1);
        }

        string configFilePath = args[0];

        // ─────────────────────────────────────────────────────────────────────
        // 2.  Bootstrap minimal logger for startup diagnostics
        // ─────────────────────────────────────────────────────────────────────
        var bootstrapLogger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        bootstrapLogger.Information("NoPremium2 starting up");

        // ─────────────────────────────────────────────────────────────────────
        // 3.  Load and validate configuration
        // ─────────────────────────────────────────────────────────────────────
        ILogger<Program> msBootstrapLogger = new LoggerFactory()
            .AddSerilog(bootstrapLogger)
            .CreateLogger<Program>();

        var configLoader = new ConfigLoader(msBootstrapLogger);
        // ConfigLoader calls Environment.Exit(1) on any validation error
        //BaseConfig config = configLoader.LoadAppConfig(configFilePath);
        //LinksConfig links = configLoader.LoadLinksConfig(configFilePath, config);
        //var (imapHost, imapPort) = ConfigLoader.ParseImapServer(config.EmailImapServer);

        var appConfig = configLoader.LoadConfig(configFilePath);

        // ─────────────────────────────────────────────────────────────────────
        // 4.  Single instance guard
        // ─────────────────────────────────────────────────────────────────────
        var instanceGuard = new SingleInstanceGuard();
        if (!instanceGuard.TryAcquire(out int? existingPid, out DateTime? existingStart))
        {
            var startedAt = existingStart.HasValue ? $", started at {existingStart:yyyy-MM-dd HH:mm:ss}" : "";
            Console.Error.WriteLine(
                $"[STARTUP ERROR] Another instance of NoPremium2 is already running (PID {existingPid}{startedAt}). Exiting.");
            Environment.Exit(1);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5.  Detect browser (Chrome > Vivaldi)
        // ─────────────────────────────────────────────────────────────────────
        string? chromePath = ChromeLauncher.FindExecutable();
        string? vivaldiPath = VivaldiLauncher.FindExecutable();

        if (chromePath is null && vivaldiPath is null)
        {
            Console.Error.WriteLine(
                "[STARTUP ERROR] No supported browser found. Please install Google Chrome or Vivaldi.");
            instanceGuard.Dispose();
            Environment.Exit(1);
        }

        bool useChrome = chromePath is not null;
        bootstrapLogger.Information("Browser: {Browser}", useChrome ? $"Chrome ({chromePath})" : $"Vivaldi ({vivaldiPath})");

        // ─────────────────────────────────────────────────────────────────────
        // 6.  Resolve log directory (Task 3: configurable LogFileDir)
        // ─────────────────────────────────────────────────────────────────────

        var logDir = appConfig.LogDir;
        try
        {
            Directory.CreateDirectory(logDir);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[STARTUP ERROR] Cannot create log directory '{logDir}': {ex.Message}");
            instanceGuard.Dispose();
            Environment.Exit(1);
        }

        var now = DateTime.Now;
        string logFile = LogFileHelper.ResolveLogFilePath(logDir, now);
        LogFileHelper.DeleteOldLogs(logDir, now);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                logFile,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        var config = appConfig.BaseConfig;
        var links = appConfig.LinksConfig;
        var emailConfig = appConfig.MailConfig;

        // ─────────────────────────────────────────────────────────────────────
        // 7.  Log effective configuration (passwords masked)
        // ─────────────────────────────────────────────────────────────────────
        Log.Information("Log directory:            {LogDir}", logDir);
        Log.Information("=== NoPremium2 Configuration ===");
        Log.Information("Config file:              {Path}", configFilePath);
        Log.Information("Links file:               {Path}", config.LinksFilePath);
        Log.Information("Links count:              {Count}", links.Links.Count);
        Log.Information("NoPremium username:       {User}", config.NoPremiumUsername);
        Log.Information("NoPremium password:       {Pass}", "***");
        Log.Information("Email username:           {User}", config.EmailUsername);
        Log.Information("Email password:           {Pass}", "***");
        Log.Information("Email IMAP:               {Host}:{Port}", emailConfig.MailHost, emailConfig.MailPort);
        Log.Information("Transfer consumer:        {Start}–{End} every {Interval} min, reserve {Reserve} bytes",
            config.TransferConsumer.StartTime, config.TransferConsumer.EndTime,
            config.TransferConsumer.IntervalMinutes, config.TransferConsumer.ReserveTransferBytes);
        Log.Information("Voucher consumer:         {Start}–{End} every {Interval} min",
            config.VoucherConsumer.StartTime, config.VoucherConsumer.EndTime, config.VoucherConsumer.IntervalMinutes);
        Log.Information("Keepalive interval:       {Interval}", config.KeepaliveInterval);
        Log.Information("================================");

        // ─────────────────────────────────────────────────────────────────────
        // 8.  Build the DI host
        // ─────────────────────────────────────────────────────────────────────
        var host = Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                // Configuration objects
                services.AddSingleton(config);
                services.AddSingleton(links);

                // AppSettings (used by existing LoginService / BrowserManager)
                services.AddSingleton(AppSettings.From(config));

                // HTTP client (for CDP check)
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                services.AddSingleton(http);

                // Infrastructure
                services.AddSingleton<ITimeService, TimeService>();
                //services.AddSingleton<NoPremium2.Infrastructure.SessionPageSaver>();

                // Browser infrastructure
                services.AddSingleton<ICdpChecker, HttpCdpChecker>();
                services.AddSingleton<IProcessCmdlineReader, LinuxProcessCmdlineReader>();
                services.AddSingleton<ICdpPortDiscovery, CdpPortDiscovery>();
                services.AddSingleton<IPortAllocator, PortAllocator>();
                services.AddSingleton<IBrowserConnector, PlaywrightBrowserConnector>();
                services.AddSingleton<IBrowserManager, BrowserManager>();

                // Register the correct browser launcher based on detection
                if (useChrome)
                    services.AddSingleton<IVivaldiLauncher, ChromeLauncher>();
                else
                    services.AddSingleton<IVivaldiLauncher, VivaldiLauncher>();

                // Login service
                services.AddSingleton<ILoginService, LoginService>();

                // Browser session provider (shared between all services)
                services.AddSingleton<IBrowserSessionProvider, BrowserSessionProvider>();

                // Email
                services.AddSingleton(new VoucherCodeExtractor());
                services.AddSingleton<IEmailService>(sp => new EmailService(
                    emailConfig.MailHost, emailConfig.MailPort,
                    config.EmailUsername, config.EmailPassword,
                    sp.GetRequiredService<VoucherCodeExtractor>(),
                    sp.GetRequiredService<ILogger<EmailService>>()));

                // NoPremium browser client
                services.AddSingleton<NoPremiumBrowserClient>();

                // Shared consumer activity state (used to pause keepalive during consumer runs)
                services.AddSingleton<ConsumerActivityState>();

                // Hosted background services
                services.AddHostedService<KeepaliveService>();
                services.AddHostedService<TransferConsumerService>();
                services.AddHostedService<VoucherConsumerService>();
            })
            .Build();

        // ─────────────────────────────────────────────────────────────────────
        // 9.  Startup verification: login to nopremium + test email connection
        // ─────────────────────────────────────────────────────────────────────
        var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
        var sessionProvider = host.Services.GetRequiredService<IBrowserSessionProvider>();
        var emailService = host.Services.GetRequiredService<IEmailService>();

        using var startupCts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

        try
        {
            startupLogger.LogInformation("Startup check: launching browser and logging in to nopremium.pl...");
            await sessionProvider.InitializeAsync(startupCts.Token);
            startupLogger.LogInformation("Startup check: nopremium.pl login OK");
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Startup check FAILED: could not log in to nopremium.pl");
            instanceGuard.Dispose();
            Environment.Exit(1);
        }

        try
        {
            startupLogger.LogInformation("Startup check: testing email (IMAP) connection...");
            // Just connect and check — we don't want to consume vouchers during startup check
            await emailService.GetUnreadVouchersAsync(startupCts.Token);
            startupLogger.LogInformation("Startup check: email connection OK");
        }
        catch (Exception ex)
        {
            startupLogger.LogError(ex, "Startup check FAILED: could not connect to email server");
            instanceGuard.Dispose();
            Environment.Exit(1);
        }

        startupLogger.LogInformation("All startup checks passed. Starting background services...");

        // ─────────────────────────────────────────────────────────────────────
        // 10. Run until Ctrl+C
        // ─────────────────────────────────────────────────────────────────────
        try
        {
            await host.RunAsync();
        }
        finally
        {
            // host.RunAsync() already disposes the host (incl. DI container → BrowserSessionProvider).
            // Only flush logs and release the single-instance lock here.
            await Log.CloseAndFlushAsync();
            instanceGuard.Dispose();
        }
    }




}
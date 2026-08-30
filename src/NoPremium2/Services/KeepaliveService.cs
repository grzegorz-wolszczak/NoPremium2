using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoPremium2.Browser;
using NoPremium2.Config;
using NoPremium2.Login;

namespace NoPremium2.Services;

public sealed class KeepaliveService : BackgroundService
{
    private readonly IBrowserSessionProvider _sessionProvider;
    private readonly ILoginService _loginService;
    private readonly string _username;
    private readonly string _password;
    private readonly ConsumerActivityState _activity;
    private readonly TimeSpan _interval;
    private readonly ILogger<KeepaliveService> _logger;

    public KeepaliveService(
        IBrowserSessionProvider sessionProvider,
        ILoginService loginService,
        ConsumerActivityState activity,
        BaseConfig config,
        ILogger<KeepaliveService> logger)
    {
        _sessionProvider = sessionProvider;
        _loginService = loginService;
        _username = config.NoPremiumUsername;
        _password = config.NoPremiumPassword;
        _activity = activity;
        _logger = logger;

        if (!TimeSpan.TryParse(config.KeepaliveInterval, out _interval))
        {
            _logger.LogWarning("Invalid KeepaliveInterval '{Value}', using default {Default}",
                config.KeepaliveInterval, DefaultConstants.KeepaliveInterval);
            TimeSpan.TryParse(DefaultConstants.KeepaliveInterval, out _interval);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Keepalive service started (interval: {Interval})", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (_activity.IsActive)
            {
                _logger.LogDebug("Keepalive skipped: a consumer is currently active");
                continue;
            }

            try
            {
                await _sessionProvider.UsePageAsync(async page =>
                {
                    var result = await _loginService.LoginAsync(page, _username, _password);
                    if (!result.Success)
                        _logger.LogError("Keepalive re-login failed, still on: {Url}", result.FinalUrl);
                }, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Keepalive navigation failed");
            }
        }

        _logger.LogInformation("Keepalive service stopped");
    }
}
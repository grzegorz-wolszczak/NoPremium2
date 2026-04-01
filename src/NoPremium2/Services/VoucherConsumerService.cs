using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoPremium2.Browser;
using NoPremium2.Config;
using NoPremium2.Email;
using NoPremium2.Infrastructure;
using NoPremium2.NoPremium;

namespace NoPremium2.Services;

public sealed class VoucherConsumerService : BackgroundService
{
    private readonly IBrowserSessionProvider _sessionProvider;
    private readonly IEmailService _emailService;
    private readonly NoPremiumBrowserClient _client;
    private readonly ITimeService _timeService;
    private readonly VoucherConsumerConfig _config;
    private readonly ILogger<VoucherConsumerService> _logger;

    private readonly TimeOnly _startTime;
    private readonly TimeOnly _endTime;
    private readonly TimeSpan _interval;
    private DateTime? _lastRunAt;

    public VoucherConsumerService(
        IBrowserSessionProvider sessionProvider,
        IEmailService emailService,
        NoPremiumBrowserClient client,
        ITimeService timeService,
        AppConfig config,
        ILogger<VoucherConsumerService> logger)
    {
        _sessionProvider = sessionProvider;
        _emailService = emailService;
        _client = client;
        _timeService = timeService;
        _config = config.VoucherConsumer;
        _logger = logger;

        _startTime = ScheduleHelper.ParseTimeOnly(_config.StartTime, "23:00");
        _endTime = ScheduleHelper.ParseTimeOnly(_config.EndTime, "23:55");
        _interval = TimeSpan.FromMinutes(_config.IntervalMinutes > 0 ? _config.IntervalMinutes : DefaultConstants.ScheduleIntervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "VoucherConsumerService started. Schedule: {Start}–{End} every {Interval} min",
            _startTime, _endTime, _interval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = await _timeService.GetLocalTimeAsync(stoppingToken);
                var wait = ScheduleHelper.TimeUntilNextRun(now, _startTime, _endTime, _interval, _lastRunAt);

                if (wait > TimeSpan.Zero)
                {
                    _logger.LogDebug("VoucherConsumer: next run in {Wait}", wait);
                    await Task.Delay(wait < TimeSpan.FromMinutes(1) ? wait : TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                _lastRunAt = now;
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;
                _logger.LogError(ex, "VoucherConsumerService run failed");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch { }
            }
        }

        _logger.LogInformation("VoucherConsumerService stopped");
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("VoucherConsumer: starting run");

        // Step 1: Read emails (no browser needed — pure IMAP)
        List<VoucherEmail> vouchers;
        try
        {
            vouchers = await _emailService.GetUnreadVouchersAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read emails, skipping run");
            return;
        }

        _logger.LogInformation("Found {Count} voucher(s) to consume", vouchers.Count);
        if (vouchers.Count == 0) return;

        // Step 2: For each voucher, consume via browser then mark email as seen
        var seenUids = new List<MailKit.UniqueId>();

        foreach (var voucher in vouchers)
        {
            ct.ThrowIfCancellationRequested();

            VoucherResult result = VoucherResult.UnknownResponse;
            try
            {
                result = await _sessionProvider.UsePageAsync(
                    async page => await _client.ConsumeVoucherAsync(page, voucher.Code, ct),
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error consuming voucher '{Code}'", voucher.Code);
                continue;
            }

            // Mark as seen regardless of result (except unknown — don't want to lose it if something weird happened)
            switch (result)
            {
                case VoucherResult.Success:
                case VoucherResult.InvalidCode:
                case VoucherResult.AlreadyUsed:
                case VoucherResult.Expired:
                    seenUids.Add(voucher.Uid);
                    break;
                case VoucherResult.CaptchaDetected:
                    _logger.LogError("CAPTCHA detected, stopping voucher processing for this run");
                    goto done;
                case VoucherResult.UnknownResponse:
                    _logger.LogWarning("Voucher '{Code}' had unknown response — not marking as seen", voucher.Code);
                    break;
            }
        }

        done:
        // Step 3: Mark consumed emails as seen
        if (seenUids.Count > 0)
        {
            try
            {
                await _emailService.MarkAsSeenAsync(seenUids, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to mark {Count} email(s) as seen", seenUids.Count);
            }
        }

        _logger.LogInformation("VoucherConsumer run complete");
    }
}

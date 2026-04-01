using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NoPremium2.Browser;
using NoPremium2.Config;
using NoPremium2.Infrastructure;
using NoPremium2.NoPremium;

namespace NoPremium2.Services;

public sealed class TransferConsumerService : BackgroundService
{
    private readonly IBrowserSessionProvider _sessionProvider;
    private readonly NoPremiumBrowserClient _client;
    private readonly ITimeService _timeService;
    private readonly LinksConfig _links;
    private readonly TransferConsumerConfig _config;
    private readonly ILogger<TransferConsumerService> _logger;

    private readonly TimeOnly _startTime;
    private readonly TimeOnly _endTime;
    private readonly TimeSpan _interval;

    // Tracks URLs already queued in the current day's session (resets at midnight)
    private readonly HashSet<string> _queuedToday = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastQueuedDate = DateTime.MinValue;

    private DateTime? _lastRunAt;

    public TransferConsumerService(
        IBrowserSessionProvider sessionProvider,
        NoPremiumBrowserClient client,
        ITimeService timeService,
        LinksConfig links,
        AppConfig config,
        ILogger<TransferConsumerService> logger)
    {
        _sessionProvider = sessionProvider;
        _client = client;
        _timeService = timeService;
        _links = links;
        _config = config.TransferConsumer;
        _logger = logger;

        _startTime = ScheduleHelper.ParseTimeOnly(_config.StartTime, "23:00");
        _endTime = ScheduleHelper.ParseTimeOnly(_config.EndTime, "23:55");
        _interval = TimeSpan.FromMinutes(_config.IntervalMinutes > 0 ? _config.IntervalMinutes : 5);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "TransferConsumerService started. Schedule: {Start}–{End} every {Interval} min. Reserve: {Reserve}",
            _startTime, _endTime, _interval.TotalMinutes,
            DataSizeConverter.FormatBytes(_config.ReserveTransferBytes));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = await _timeService.GetLocalTimeAsync(stoppingToken);
                var wait = ScheduleHelper.TimeUntilNextRun(now, _startTime, _endTime, _interval, _lastRunAt);

                if (wait > TimeSpan.Zero)
                {
                    _logger.LogDebug("TransferConsumer: next run in {Wait}", wait);
                    await Task.Delay(wait < TimeSpan.FromMinutes(1) ? wait : TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                _lastRunAt = now;
                ResetDailyQueueIfNeeded(now);
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TransferConsumerService run failed");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch { }
            }
        }

        _logger.LogInformation("TransferConsumerService stopped");
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("TransferConsumer: starting run");

        await _sessionProvider.UsePageAsync(async page =>
        {
            // Navigate to /files and read current transfer
            await page.GotoAsync("https://www.nopremium.pl/files",
                new() { WaitUntil = Microsoft.Playwright.WaitUntilState.DOMContentLoaded, Timeout = 30_000 });

            var transferInfo = await _client.ReadTransferInfoAsync(page);
            if (transferInfo is null)
            {
                _logger.LogWarning("Could not read transfer info, skipping run");
                return;
            }

            _logger.LogInformation("Current transfer: {Info}", transferInfo);

            if (transferInfo.PremiumBytes <= _config.ReserveTransferBytes)
            {
                _logger.LogInformation(
                    "Premium transfer ({Premium}) is at or below reserve ({Reserve}), nothing to consume",
                    DataSizeConverter.FormatBytes(transferInfo.PremiumBytes),
                    DataSizeConverter.FormatBytes(_config.ReserveTransferBytes));
                return;
            }

            long budget = transferInfo.PremiumBytes - _config.ReserveTransferBytes;
            _logger.LogInformation("Budget to consume: {Budget}", DataSizeConverter.FormatBytes(budget));

            var toQueue = SelectLinks(budget);
            if (toQueue.Count == 0)
            {
                _logger.LogInformation("No new links to queue (all already queued today or list exhausted)");
                return;
            }

            _logger.LogInformation("Selected {Count} link(s) to queue:", toQueue.Count);
            foreach (var link in toQueue)
                _logger.LogInformation("  - {Name} ({Size})", link.Name, link.Size);

            var queued = await _client.AddLinksToQueueAsync(page, toQueue.Select(l => l.Url), ct);

            foreach (var link in toQueue)
                _queuedToday.Add(link.Url);

            _logger.LogInformation("TransferConsumer run complete: {Queued} file(s) queued", queued);

        }, ct);
    }

    private List<LinkEntry> SelectLinks(long budgetBytes)
    {
        var selected = new List<LinkEntry>();
        long remaining = budgetBytes;

        foreach (var link in _links.Links)
        {
            if (_queuedToday.Contains(link.Url)) continue;

            long sizeBytes;
            try { sizeBytes = DataSizeConverter.ParseToBytes(link.Size); }
            catch { sizeBytes = 0; }

            if (sizeBytes > remaining) continue;

            selected.Add(link);
            remaining -= sizeBytes;

            if (remaining <= 0) break;
        }

        return selected;
    }

    private void ResetDailyQueueIfNeeded(DateTime now)
    {
        if (now.Date > _lastQueuedDate)
        {
            _queuedToday.Clear();
            _lastQueuedDate = now.Date;
            _logger.LogDebug("Daily queue tracker reset for new day {Date}", now.Date.ToShortDateString());
        }
    }
}

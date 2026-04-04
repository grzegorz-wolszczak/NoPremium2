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
    private readonly ConsumerActivityState _activity;
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
        ConsumerActivityState activity,
        ILogger<TransferConsumerService> logger)
    {
        _sessionProvider = sessionProvider;
        _client = client;
        _timeService = timeService;
        _links = links;
        _config = config.TransferConsumer;
        _activity = activity;
        _logger = logger;

        _startTime = ScheduleHelper.ParseTimeOnly(_config.StartTime, "23:00");
        _endTime = ScheduleHelper.ParseTimeOnly(_config.EndTime, "23:55");
        _interval = TimeSpan.FromMinutes(_config.IntervalMinutes > 0 ? _config.IntervalMinutes : DefaultConstants.ScheduleIntervalMinutes);
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
                if (stoppingToken.IsCancellationRequested)
                    break;
                _logger.LogError(ex, "TransferConsumerService run failed");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch { }
            }
        }

        _logger.LogInformation("TransferConsumerService stopped");
    }

    private async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("TransferConsumer: starting run");

        _activity.Enter();
        try
        {
            await _sessionProvider.UsePageAsync(async page =>
            {
                var candidates = _links.Links
                    .Where(l => !_queuedToday.Contains(l.Url))
                    .ToList();

                if (candidates.Count == 0)
                {
                    _logger.LogInformation("No new links to process (all already queued today)");
                    return;
                }

                // Before adding new links: remove any already-completed entries from the queue
                // so the same links can be re-added (server rejects duplicates).
                // Pass all link names — RemoveCompletedLinksAsync will only touch "Zakończono" entries.
                await _client.RemoveCompletedLinksAsync(page, _links.Links.Select(l => l.Name), ct);

                int addedThisRun = 0;

                foreach (var link in candidates)
                {
                    ct.ThrowIfCancellationRequested();

                    // Fresh transfer read before every link — page header is always current
                    var transferInfo = await _client.ReadTransferInfoAsync(page);
                    if (transferInfo is null)
                    {
                        _logger.LogWarning("Could not read transfer info, stopping run");
                        break;
                    }

                    // Stop if premium has dropped to or below the safe reserve
                    if (transferInfo.PremiumBytes <= _config.ReserveTransferBytes)
                    {
                        _logger.LogInformation(
                            "Premium transfer ({Premium}) reached reserve ({Reserve}), stopping for today",
                            DataSizeConverter.FormatBytes(transferInfo.PremiumBytes),
                            DataSizeConverter.FormatBytes(_config.ReserveTransferBytes));
                        break;
                    }

                    long remaining = transferInfo.PremiumBytes - _config.ReserveTransferBytes;

                    long linkSize;
                    try { linkSize = DataSizeConverter.ParseToBytes(link.Size); }
                    catch { linkSize = 0; }

                    // Skip this link if consuming it would breach the reserve
                    if (linkSize > remaining)
                    {
                        _logger.LogDebug(
                            "Skipping '{Name}' ({Size}) — exceeds remaining budget {Budget}",
                            link.Name, link.Size, DataSizeConverter.FormatBytes(remaining));
                        continue;
                    }

                    _logger.LogInformation(
                        "Queuing '{Name}' ({Size}) — premium remaining after reserve: {Budget}",
                        link.Name, link.Size, DataSizeConverter.FormatBytes(remaining));

                    var queued = await _client.AddLinksToQueueAsync(page, new[] { link.Url }, ct);

                    _queuedToday.Add(link.Url);
                    addedThisRun += queued;

                    if (queued == 0)
                        _logger.LogWarning("'{Name}' was not recognised by nopremium.pl", link.Name);
                }

                _logger.LogInformation("TransferConsumer run complete: {Count} file(s) queued this run", addedThisRun);

            }, ct);
        }
        finally
        {
            _activity.Exit();
        }
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

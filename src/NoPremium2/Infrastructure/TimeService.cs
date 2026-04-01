using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NoPremium2.Infrastructure;

public interface ITimeService
{
    Task<DateTime> GetLocalTimeAsync(CancellationToken ct = default);
}

public sealed class TimeService : ITimeService
{
    // worldtimeapi.org returns JSON with "datetime" field (ISO 8601 with offset)
    private const string TimeApiUrl = "http://worldtimeapi.org/api/ip";

    private readonly HttpClient _http;
    private readonly ILogger<TimeService> _logger;

    public TimeService(HttpClient http, ILogger<TimeService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<DateTime> GetLocalTimeAsync(CancellationToken ct = default)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var json = await _http.GetStringAsync(TimeApiUrl, cts.Token);
            using var doc = JsonDocument.Parse(json);

            var dateTimeStr = doc.RootElement.GetProperty("datetime").GetString()
                ?? throw new InvalidOperationException("Missing 'datetime' field in response");

            var dt = DateTimeOffset.Parse(dateTimeStr, null,
                System.Globalization.DateTimeStyles.RoundtripKind);

            _logger.LogDebug("External time fetched: {Time}", dt);
            return dt.LocalDateTime;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Time server request timed out, falling back to local clock");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch time from external server, falling back to local clock");
        }

        return DateTime.Now;
    }
}

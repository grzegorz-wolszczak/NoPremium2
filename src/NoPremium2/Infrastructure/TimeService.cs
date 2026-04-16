using System.Globalization;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

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
        //return await TryGetLocalTime(ct);
        try
        {
            return await GetTimeFromNist(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Failed to get time from NIST server, falling back local clock: {ExType}:{Message}",ex.GetType().FullName, ex.Message);
        }
        return DateTime.Now;
    }

    private async Task<DateTime> GetTimeFromNist(CancellationToken ct)
    {
        var client = new TcpClient("time.nist.gov", 13);

        using var streamReader = new StreamReader(client.GetStream());
        var response = await streamReader.ReadToEndAsync(ct);
        var utcDateTimeString = response.Substring(7, 17);
        var localDateTime = DateTime.ParseExact(utcDateTimeString, "yy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        return localDateTime;
    }

    private async Task<DateTime> TryGetTimeFromTimeServer(CancellationToken ct)
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
    }
}
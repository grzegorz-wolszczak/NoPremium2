using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NoPremium2.Infrastructure;

public interface ITimeService
{
    Task<DateTime> GetLocalTimeAsync(CancellationToken ct = default);
}

public sealed class TimeService : ITimeService
{
    // Tried in order after NTP fails. Field name is the JSON property containing a parseable datetime string.
    private static readonly (string Url, string Field)[] HttpApis = new[]
    {
        // Zone-based endpoints return local time for the given timezone — more reliable than /ip variants
        ("https://worldtimeapi.org/api/timezone/Europe/Warsaw",              "datetime"),
        ("https://timeapi.io/api/time/current/zone?timeZone=Europe%2FWarsaw", "dateTime"),
    };

    private readonly HttpClient _http;
    private readonly ILogger<TimeService> _logger;

    public TimeService(HttpClient http, ILogger<TimeService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<DateTime> GetLocalTimeAsync(CancellationToken ct = default)
    {
        // 1. NTP UDP — most reliable (no HTTP/SSL, pool.ntp.org is extremely stable)
        var ntpTime = await TryNtpAsync(ct);
        if (ntpTime.HasValue)
        {
            _logger.LogDebug("NTP time: {Time}", ntpTime.Value);
            return ntpTime.Value;
        }

        // 2. HTTP fallbacks
        foreach (var (url, field) in HttpApis)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(5));

                var json = await _http.GetStringAsync(url, cts.Token);
                using var doc = JsonDocument.Parse(json);

                var dateTimeStr = doc.RootElement.GetProperty(field).GetString()
                    ?? throw new InvalidOperationException($"Missing '{field}' in response");

                // Parse with RoundtripKind: preserves UTC offset if present; otherwise treats as local
                var dt = DateTime.Parse(dateTimeStr, null,
                    System.Globalization.DateTimeStyles.RoundtripKind);

                _logger.LogDebug("HTTP time from {Url}: {Time}", url, dt);
                return dt.Kind == DateTimeKind.Utc ? dt.ToLocalTime() : dt;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogDebug("Time server {Url} timed out", url);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Time server {Url} failed", url);
            }
        }

        _logger.LogWarning("All time servers failed, falling back to local clock");
        return DateTime.Now;
    }

    private async Task<DateTime?> TryNtpAsync(CancellationToken ct)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var ntpData = new byte[48];
            ntpData[0] = 0x1B; // LI=0, VN=3, Mode=3 (client request)

            var addresses = await Dns.GetHostAddressesAsync("pool.ntp.org", AddressFamily.InterNetwork, cts.Token);
            if (addresses.Length == 0) return null;

            using var udp = new UdpClient();
            udp.Connect(new IPEndPoint(addresses[0], 123));

            await udp.SendAsync(ntpData, ntpData.Length);
            var result = await udp.ReceiveAsync(cts.Token);
            var recv = result.Buffer;
            if (recv.Length < 48) return null;

            // Transmit Timestamp: bytes 40–47 (4-byte NTP seconds + 4-byte fraction)
            // NTP epoch is 1900-01-01 UTC
            ulong secs = ((ulong)recv[40] << 24) | ((ulong)recv[41] << 16) | ((ulong)recv[42] << 8) | recv[43];
            ulong frac = ((ulong)recv[44] << 24) | ((ulong)recv[45] << 16) | ((ulong)recv[46] << 8) | recv[47];
            var ms = secs * 1000UL + frac * 1000UL / 0x100000000UL;

            return new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ms).ToLocalTime();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "NTP query to pool.ntp.org failed");
            return null;
        }
    }
}

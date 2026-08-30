namespace NoPremium2.Browser;

public interface ICdpChecker
{
    Task<bool> IsRespondingAsync(int port, CancellationToken ct = default);
}

public sealed class HttpCdpChecker : ICdpChecker
{
    /// <summary>Per-attempt timeout — keep short so retry loops stay fast.</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromMilliseconds(1500);

    private readonly HttpClient _http;
    public HttpCdpChecker(HttpClient http) => _http = http;

    public async Task<bool> IsRespondingAsync(int port, CancellationToken ct = default)
    {
        // Chromium binds the remote-debugging endpoint to 127.0.0.1 (IPv4) only.
        // Using "localhost" is an IPv4/IPv6 coin flip depending on /etc/hosts (RFC 6724).
        try
        {
            using var timeoutCts = new CancellationTokenSource(AttemptTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var resp = await _http.GetAsync($"http://127.0.0.1:{port}/json/version", linked.Token);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

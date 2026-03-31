namespace NoPremium2.Browser;

public interface ICdpChecker
{
    Task<bool> IsRespondingAsync(int port);
}

public sealed class HttpCdpChecker : ICdpChecker
{
    private readonly HttpClient _http;
    public HttpCdpChecker(HttpClient http) => _http = http;

    public async Task<bool> IsRespondingAsync(int port)
    {
        try
        {
            var resp = await _http.GetAsync($"http://localhost:{port}/json/version");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

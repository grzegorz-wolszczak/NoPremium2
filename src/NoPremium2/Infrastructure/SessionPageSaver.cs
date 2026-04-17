using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace NoPremium2.Infrastructure;

/// <summary>
/// Saves the full HTML of every page the browser navigates to during a session.
/// Creates sessions/{YYYYMMDD_HHmmss}/ at startup; each page load writes
/// {seq:D3}_{HHmmss_fff}_{url-slug}.html inside that directory.
/// Designed for post-mortem debugging: inspect actual HTML, JS-rendered DOM, selectors, etc.
/// </summary>
public sealed class _SessionPageSaver
{
    public string SessionDir { get; }

    private int _counter;
    private readonly ILogger<_SessionPageSaver> _logger;

    public _SessionPageSaver(ILogger<_SessionPageSaver> logger)
    {
        _logger = logger;
        var sessionName = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        SessionDir = Path.Combine(AppContext.BaseDirectory, "sessions", sessionName);
        Directory.CreateDirectory(SessionDir);
        _logger.LogInformation("Session page saver ready: {Dir}", SessionDir);
    }

    /// <summary>
    /// Subscribes to page.Load so every navigation in this page is auto-captured.
    /// Call once per IPage instance (on session start / reconnect).
    /// </summary>
    public void AttachToPage(IPage page)
    {
        page.Load += async (_, _) =>
        {
            try { await CaptureAsync(page); }
            catch (Exception ex) { _logger.LogDebug(ex, "Session capture failed for {Url}", page.Url); }
        };
        _logger.LogDebug("Session page saver attached to page");
    }

    /// <summary>Explicitly capture the current page state (e.g. after form submission / AJAX).</summary>
    public async Task CaptureAsync(IPage page)
    {
        int seq = Interlocked.Increment(ref _counter);
        var ts = DateTime.Now.ToString("HHmmss_fff");
        var slug = MakeSlug(page.Url);
        var filename = $"{seq:D3}_{ts}_{slug}.html";
        var filePath = Path.Combine(SessionDir, filename);

        var html = await page.ContentAsync();
        await File.WriteAllTextAsync(filePath, html, System.Text.Encoding.UTF8);
        _logger.LogDebug("Saved page snapshot: {File}", filename);
    }

    public static string MakeSlug(string rawUrl)
    {
        try
        {
            var uri = new Uri(rawUrl);
            var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? uri.Host[4..] : uri.Host;
            var path = uri.AbsolutePath.Trim('/').Replace('/', '_');
            var slug = string.IsNullOrEmpty(path) ? host : $"{host}_{path}";

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(slug.Length);
            foreach (char c in slug)
                sb.Append(invalid.Contains(c) ? '_' : c);

            var result = sb.ToString();
            return result.Length > 60 ? result[..60] : result;
        }
        catch
        {
            return "unknown";
        }
    }
}

using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using NoPremium2.Config;
using NoPremium2.Infrastructure;

namespace NoPremium2.NoPremium;

public enum VoucherResult
{
    Success,
    InvalidCode,
    AlreadyUsed,
    Expired,
    CaptchaDetected,
    UnknownResponse,
}

public sealed class NoPremiumBrowserClient
{
    private const string FilesUrl = "https://www.nopremium.pl/files";
    private const string VoucherUrl = "https://www.nopremium.pl/voucher";

    // Regex to parse transfer info from the panel header element (#signed)
    // Example: "Pozostały transfer: 2097.52 GB (w tym 22.34 GB transferu Premium + 2075.18 GB transferu dodatkowego)"
    private static readonly Regex TransferRegex = new(
        @"w tym\s+(?<premium>[\d.,]+)\s*(?<premUnit>GB|MB|TB|KB)\s+transferu Premium" +
        @"\s*\+\s*(?<extra>[\d.,]+)\s*(?<extraUnit>GB|MB|TB|KB)\s+transferu dodatkowego",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TotalTransferRegex = new(
        @"Pozostały transfer:\s*(?<total>[\d.,]+)\s*(?<totalUnit>GB|MB|TB|KB)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogger<NoPremiumBrowserClient> _logger;

    public NoPremiumBrowserClient(ILogger<NoPremiumBrowserClient> logger)
    {
        _logger = logger;
    }

    // ──────────────────────────────────────────────────────────────────
    // Transfer info
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Reads current transfer amounts from the page header. Page can be any nopremium.pl page.</summary>
    public async Task<TransferInfo?> ReadTransferInfoAsync(IPage page)
    {
        try
        {
            // The transfer info is shown in the top panel, old XPath: //div[@id='signed']
            var signedDiv = page.Locator("#signed");
            var text = await signedDiv.InnerTextAsync(new() { Timeout = 5_000 });

            return ParseTransferText(text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read transfer info from page");
            return null;
        }
    }

    internal TransferInfo? ParseTransferText(string text)
    {
        var totalMatch = TotalTransferRegex.Match(text);
        var detailMatch = TransferRegex.Match(text);

        if (!detailMatch.Success)
        {
            _logger.LogWarning("Could not parse transfer detail from text: {Text}", text);
            return null;
        }

        long premium = ParseSize(detailMatch.Groups["premium"].Value, detailMatch.Groups["premUnit"].Value);
        long extra = ParseSize(detailMatch.Groups["extra"].Value, detailMatch.Groups["extraUnit"].Value);
        long total = totalMatch.Success
            ? ParseSize(totalMatch.Groups["total"].Value, totalMatch.Groups["totalUnit"].Value)
            : premium + extra;

        var info = new TransferInfo(total, premium, extra);
        _logger.LogDebug("Transfer info: {Info}", info);
        return info;
    }

    private static long ParseSize(string value, string unit)
    {
        var normalized = value.Replace(',', '.');
        if (!double.TryParse(normalized, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
            return 0;
        return DataSizeConverter.ParseToBytes($"{d}{unit}");
    }

    // ──────────────────────────────────────────────────────────────────
    // Add links to download queue
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to /files, enters the given URLs in the textarea, submits,
    /// selects all valid (recognised) files, and clicks "Dodaj zaznaczone".
    /// Returns the number of files successfully queued, or -1 on error.
    /// </summary>
    public async Task<int> AddLinksToQueueAsync(IPage page, IEnumerable<string> urls, CancellationToken ct = default)
    {
        var urlList = urls.ToList();
        if (urlList.Count == 0) return 0;

        if (!page.Url.Contains("/files"))
        {
            _logger.LogInformation("Navigating to /files to queue {Count} link(s)", urlList.Count);
            await page.GotoAsync(FilesUrl, new() { WaitUntil = WaitUntilState.Load, Timeout = 30_000 });
        }
        else
        {
            _logger.LogInformation("Already on /files, queuing {Count} link(s)", urlList.Count);
        }

        // Guard: if server redirected us away from /files (e.g. session expired → login page), fail fast
        if (!page.Url.Contains("/files"))
        {
            throw new InvalidOperationException(
                $"Navigation to /files was redirected to '{page.Url}'. Session may have expired.");
        }

        // Fill the links textarea. From old code, the form field name is 'links'.
        var textarea = page.Locator("textarea[name='links']");
        await textarea.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        await textarea.FillAsync(string.Join("\n", urlList));

        // Click the submit button. It's near the textarea and says "Dodaj" (exact match avoids "Dodaj zaznaczone")
        // Try id first, then text fallback
        var submitBtn = page.Locator("#addlinks").First;
        if (!await submitBtn.IsVisibleAsync())
            submitBtn = page.GetByRole(AriaRole.Button, new() { Name = "Dodaj", Exact = true }).First;

        await submitBtn.ClickAsync();
        _logger.LogDebug("Submitted links, waiting for processing...");

        // Wait for the "Przetwarzane pliki" section to appear
        // It contains processed (valid) files with checkboxes
        var processedSection = page.Locator("text=Przetwarzane pliki").First;
        try
        {
            await processedSection.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 30_000 });
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("No 'Przetwarzane pliki' section appeared — all links may be unrecognised");
            return 0;
        }

        // Check all checkboxes in the processed files table
        var checkboxes = page.Locator("input[type='checkbox']");
        int checkboxCount = await checkboxes.CountAsync();
        if (checkboxCount == 0)
        {
            _logger.LogWarning("No checkboxes found in processed files section");
            return 0;
        }

        for (int i = 0; i < checkboxCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var cb = checkboxes.Nth(i);
            if (!await cb.IsCheckedAsync())
                await cb.CheckAsync();
        }

        _logger.LogInformation("Selected {Count} checkbox(es), clicking 'Dodaj zaznaczone'", checkboxCount);

        // Click "Dodaj zaznaczone"
        var addSelectedBtn = page.GetByRole(AriaRole.Button, new() { Name = "Dodaj zaznaczone" });
        if (!await addSelectedBtn.IsVisibleAsync())
            addSelectedBtn = page.Locator("button:has-text('Dodaj zaznaczone')");

        await addSelectedBtn.ClickAsync();

        // Wait for the intermediate loading screen to pass
        // (it shows a spinner "Trwa dodawanie plików do kolejek, proszę czekać...")
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 60_000 });
        _logger.LogInformation("Links queued successfully");

        return checkboxCount;
    }

    // ──────────────────────────────────────────────────────────────────
    // Voucher consumption
    // ──────────────────────────────────────────────────────────────────

    /// <summary>Navigates to /voucher and attempts to redeem the given code.</summary>
    public async Task<VoucherResult> ConsumeVoucherAsync(IPage page, string code, CancellationToken ct = default)
    {
        _logger.LogInformation("Navigating to /voucher to consume code '{Code}'", code);
        await page.GotoAsync(VoucherUrl, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });

        // Fill the voucher code input. Form field name from old code: 'voucher'
        var voucherInput = page.Locator("input[name='voucher']");
        await voucherInput.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 10_000 });
        await voucherInput.FillAsync(code);

        // Click "Doładuj" button
        var doladujBtn = page.GetByRole(AriaRole.Button, new() { Name = "Doładuj" });
        if (!await doladujBtn.IsVisibleAsync())
            doladujBtn = page.Locator("input[value='Doładuj']");

        await doladujBtn.ClickAsync();
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, new() { Timeout = 15_000 });

        // Read the result from the page body
        var bodyText = await page.Locator("body").InnerTextAsync();
        return InterpretVoucherResponse(bodyText, code);
    }

    private VoucherResult InterpretVoucherResponse(string bodyText, string code)
    {
        // From old code + confirmed by screenshots
        if (bodyText.Contains("Konto doładowano pomyślnie", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Voucher '{Code}' consumed successfully (+2 GB extra transfer)", code);
            return VoucherResult.Success;
        }
        if (bodyText.Contains("Przepisz kod z obrazka", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogError("CAPTCHA detected on voucher page — manual intervention required");
            return VoucherResult.CaptchaDetected;
        }
        if (bodyText.Contains("Wprowadzony kod nie istnieje", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Voucher code '{Code}' is invalid (does not exist)", code);
            return VoucherResult.InvalidCode;
        }
        if (bodyText.Contains("Wprowadzony kod został już wykorzystany", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Voucher code '{Code}' was already used", code);
            return VoucherResult.AlreadyUsed;
        }
        if (bodyText.Contains("Wprowadzony kod stracił ważność", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Voucher code '{Code}' has expired", code);
            return VoucherResult.Expired;
        }

        _logger.LogWarning("Unknown voucher response for code '{Code}'. Page text snippet: {Snippet}",
            code, bodyText.Length > 300 ? bodyText[..300] : bodyText);
        return VoucherResult.UnknownResponse;
    }

    // ──────────────────────────────────────────────────────────────────
    // Keepalive navigation
    // ──────────────────────────────────────────────────────────────────

    private static readonly string[] KeepaliveUrls =
        new[] { "https://www.nopremium.pl/help", "https://www.nopremium.pl/offer" };

    private int _keepaliveIndex;

    public async Task NavigateKeepaliveAsync(IPage page)
    {
        var url = KeepaliveUrls[_keepaliveIndex % KeepaliveUrls.Length];
        _keepaliveIndex++;
        _logger.LogDebug("Keepalive navigation to {Url}", url);
        await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
    }
}

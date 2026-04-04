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

    private readonly SessionPageSaver _sessionPageSaver;
    private readonly ILogger<NoPremiumBrowserClient> _logger;

    public NoPremiumBrowserClient(SessionPageSaver sessionPageSaver, ILogger<NoPremiumBrowserClient> logger)
    {
        _sessionPageSaver = sessionPageSaver;
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

    public static long ParseSize(string value, string unit)
    {
        var normalized = value.Replace(',', '.');
        if (!double.TryParse(normalized, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double d))
            return 0;
        // Use InvariantCulture when formatting d to avoid locale-dependent decimal separators
        // (e.g. Polish locale would format 22.34 as "22,34", which DataSizeConverter would
        //  parse as 2234 treating the comma as a thousands separator).
        return DataSizeConverter.ParseToBytes(
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{d}{unit}"));
    }

    // ──────────────────────────────────────────────────────────────────
    // Remove completed links from download queue
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to /files, finds all queue rows with status "Zakończono" whose display
    /// name starts with one of the provided <paramref name="linkNames"/>, checks their
    /// checkboxes and clicks "Usuń zaznaczone". Returns the number of deleted entries.
    ///
    /// Name matching: the queue shows file names with optional suffixes like "_720p.mp4";
    /// the link name from config is matched as a case-insensitive prefix of the queue entry
    /// (after stripping mid-word line-break newlines that the site inserts for column width).
    /// </summary>
    public async Task<int> RemoveCompletedLinksAsync(IPage page, IEnumerable<string> linkNames, CancellationToken ct = default)
    {
        var nameSet = linkNames
            .Select(n => n.Trim())
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (nameSet.Count == 0) return 0;

        _logger.LogDebug("Navigating to /files to check for completed entries to clean up");
        await page.GotoAsync(FilesUrl, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60_000 });

        // Collect completed entries via JS. Each completed row has:
        //   td[id^="status"] div.finish  — "Zakończono"
        //   td[id^="action"]             — file name (may contain <br> for column wrapping)
        //   input[name="sid[]"]          — checkbox with file ID as value
        // We strip newlines (inserted by the site mid-word to fit column width) before matching.
        // Format: "displayText\x1Fid" pairs joined by "\x1E" (ASCII record separator).
        var raw = await page.EvaluateAsync<string>(
            "() => {" +
            "  var rows = document.querySelectorAll('#downloadFilesArea tr.table_content');" +
            "  var parts = [];" +
            "  for (var i = 0; i < rows.length; i++) {" +
            "    var row = rows[i];" +
            "    if (!row.querySelector('td[id^=\"status\"] div.finish')) continue;" +
            "    var ac = row.querySelector('td[id^=\"action\"]');" +
            "    if (!ac) continue;" +
            "    var cb = row.querySelector('input[name=\"sid[]\"]');" +
            "    if (!cb) continue;" +
            "    var txt = (ac.innerText || ac.textContent || '').replace(/\\n/g, ' ').replace(/\\s+/g, ' ').trim();" +
            "    parts.push(txt + '\\x1F' + cb.value);" +
            "  }" +
            "  return parts.join('\\x1E');" +
            "}");

        if (string.IsNullOrEmpty(raw))
        {
            _logger.LogDebug("No completed entries found in /files queue");
            return 0;
        }

        // Parse "text\x1Fid" pairs
        var entries = raw.Split('\x1E', StringSplitOptions.RemoveEmptyEntries)
            .Select(e =>
            {
                var sep = e.IndexOf('\x1F');
                return sep < 0 ? ((string Text, string Id)?)null : (e[..sep], e[(sep + 1)..]);
            })
            .Where(e => e.HasValue)
            .Select(e => e!.Value)
            .ToList();

        _logger.LogDebug("Found {Total} completed entry/entries in queue total", entries.Count);

        // Match: queue file name contains the config link name (case-insensitive).
        // We use Contains rather than StartsWith because the site sometimes prepends text
        // to the file name (e.g. "Zagrajmy w Heroes 3: #1 - ..." when config says "Heroes 3: #1 - ...").
        var toDelete = new List<(string Text, string Id)>();
        var unmatched = new List<string>();
        foreach (var e in entries)
        {
            if (nameSet.Any(n => e.Text.Contains(n, StringComparison.OrdinalIgnoreCase)))
                toDelete.Add(e);
            else
                unmatched.Add(e.Text);
        }

        if (unmatched.Count > 0)
            _logger.LogDebug("Completed entries not matching any config name ({Count}): {Names}",
                unmatched.Count, string.Join(", ", unmatched.Select(t => $"'{t}'")));

        if (toDelete.Count == 0)
        {
            _logger.LogDebug("None of the completed entries match the provided link names");
            return 0;
        }

        _logger.LogInformation("Deleting {Count} completed entry/entries from /files queue", toDelete.Count);
        foreach (var (text, _) in toDelete)
            _logger.LogDebug("  Deleting: '{Text}'", text);

        // Tick each checkbox
        foreach (var (_, id) in toDelete)
        {
            var cb = page.Locator($"input[name='sid[]'][value='{id}']");
            await cb.CheckAsync();
        }

        // Override window.confirm to auto-accept (handles sites that use native confirm dialog)
        await page.EvaluateAsync("() => { window.confirm = function() { return true; }; }");

        // Register Playwright Dialog handler as a fallback for native browser dialogs
        async void AcceptDialog(object? _, IDialog d) { try { await d.AcceptAsync(); } catch { } }
        page.Dialog += AcceptDialog;

        try
        {
            var deleteBtn = page.Locator("input[value='Usuń zaznaczone']");
            await deleteBtn.ClickAsync();

            // jquery.modal creates a .blocker.current overlay when showing a custom modal.
            // Wait briefly; if one appears, capture it and click the confirm button inside.
            var blocker = page.Locator(".blocker.current");
            bool modalAppeared = false;
            try
            {
                await blocker.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 3_000 });
                modalAppeared = true;
            }
            catch { /* no modal — delete was immediate (window.confirm bypassed or no confirmation) */ }

            if (modalAppeared)
            {
                _logger.LogDebug("jQuery confirmation modal appeared — capturing and confirming");
                await _sessionPageSaver.CaptureAsync(page);

                // Find confirmation button by common Polish text patterns
                var confirmBtn = page.Locator(".blocker.current")
                    .Locator("button, input[type='button'], input[type='submit'], a")
                    .Filter(new LocatorFilterOptions
                    {
                        HasTextRegex = new System.Text.RegularExpressions.Regex(
                            "Tak|Usuń|OK|Potwierdź",
                            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                    })
                    .First;

                await confirmBtn.ClickAsync(new() { Timeout = 10_000 });
                _logger.LogDebug("Clicked modal confirm button");
            }
        }
        finally
        {
            page.Dialog -= AcceptDialog;
        }

        // Wait for the deletion AJAX to complete
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30_000 });
        await _sessionPageSaver.CaptureAsync(page);

        _logger.LogInformation("Deleted {Count} entry/entries from /files queue", toDelete.Count);
        return toDelete.Count;
    }

    // ──────────────────────────────────────────────────────────────────
    // Add links to download queue
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Navigates to /files, enters the given URLs in the textarea, submits,
    /// waits for server-side processing, selects all valid (recognised) files,
    /// and clicks "Dodaj zaznaczone".
    /// Returns the number of URLs submitted if at least one was recognised, 0 otherwise.
    /// </summary>
    public async Task<int> AddLinksToQueueAsync(IPage page, IEnumerable<string> urls, CancellationToken ct = default)
    {
        var urlList = urls.ToList();
        if (urlList.Count == 0) return 0;

        _logger.LogInformation("Navigating to /files to queue {Count} link(s)", urlList.Count);
        await page.GotoAsync(FilesUrl, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 60_000 });

        if (!page.Url.Contains("/files"))
            throw new InvalidOperationException(
                $"Navigation to /files was redirected to '{page.Url}'. Session may have expired.");

        // Phase 1→2: Fill textarea and submit.
        // Confirmed id: 'filesList'. Submit button calls submitFiles() via AJAX — NOT a form POST.
        var textarea = page.Locator("#filesList");
        await textarea.WaitForAsync(new() { State = WaitForSelectorState.Visible, Timeout = 15_000 });
        await textarea.FillAsync(string.Join("\n", urlList));

        var submitBtn = page.Locator("input[onclick*='submitFiles']");
        await submitBtn.ClickAsync();
        _logger.LogDebug("Submit button clicked");

        // Capture page state immediately after click so we can inspect what submitFiles() did
        // synchronously (before async AJAX changes). Useful for diagnosing why #insertPanel
        // might not hide when expected.
        await _sessionPageSaver.CaptureAsync(page);

        var domAfterClick = await page.EvaluateAsync<string>(
            "() => { var ip=document.getElementById('insertPanel'); var pp=document.getElementById('progressPanel'); var ta=document.getElementById('filesList'); " +
            "return JSON.stringify({ insertPanelDisplay: ip ? ip.style.display : 'NOT_FOUND', progressPanelDisplay: pp ? pp.style.display : 'NOT_FOUND', textareaValue: (ta ? ta.value : '').substring(0, 80) }); }");
        _logger.LogDebug("DOM state right after click: {State}", domAfterClick);

        // Phase 3: Wait for submitFiles() AJAX processing to complete.
        //
        // Two-step DOM wait — avoids the NetworkIdle race condition:
        //
        //   Step 3a: Wait for #insertPanel to be hidden.
        //     submitFiles() hides #insertPanel and shows #progressPanel synchronously when it starts.
        //     This confirms the JS ran and the AJAX request was dispatched.
        //
        //   Step 3b: Wait for #progressPanel to be hidden.
        //     #progressPanel ("Trwa przetwarzanie plików...") is visible while the server processes.
        //     It is hidden again only when the AJAX response arrives and the DOM is updated — this
        //     is the reliable signal that processing is complete (results/"Dodaj zaznaczone" visible).
        //
        // NOTE: We cannot wait for #progressPanel visible→hidden in a single step, because the
        // server can respond in < 400 ms and Playwright's selector polling would miss the brief
        // visible state. Instead we wait for #insertPanel hidden first, at which point
        // #progressPanel is guaranteed to be visible (confirmed by DOM snapshots), then we wait
        // for #progressPanel hidden.
        _logger.LogDebug("Waiting for submitFiles() to start (#insertPanel hidden)...");
        var insertPanel = page.Locator("#insertPanel");
        await insertPanel.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 30_000 });
        _logger.LogDebug("submitFiles() started — now waiting for #progressPanel to hide (AJAX complete)...");

        var progressPanel = page.Locator("#progressPanel");
        await progressPanel.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 120_000 });
        _logger.LogDebug("Server processing complete (#progressPanel hidden)");

        await _sessionPageSaver.CaptureAsync(page);

        // Phase 4: Click "Dodaj zaznaczone" if any links were recognised.
        // The button only appears when at least one link was accepted by the server.
        var addSelectedBtn = page.Locator("input[value='Dodaj zaznaczone']");
        if (!await addSelectedBtn.IsVisibleAsync())
        {
            _logger.LogWarning("No valid files recognised by nopremium.pl for the submitted link(s)");
            return 0;
        }

        await addSelectedBtn.ClickAsync();
        _logger.LogDebug("Clicked 'Dodaj zaznaczone'");

        // Phase 5: #insertLoading ("Trwa umieszczanie plików na liście pobierania") may appear briefly.
        var insertLoading = page.Locator("#insertLoading");
        if (await insertLoading.IsVisibleAsync())
        {
            _logger.LogDebug("Waiting for insert-loading overlay to finish...");
            await insertLoading.WaitForAsync(new() { State = WaitForSelectorState.Hidden, Timeout = 60_000 });
        }

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 30_000 });
        await _sessionPageSaver.CaptureAsync(page);

        _logger.LogInformation("Successfully queued {Count} link(s)", urlList.Count);
        return urlList.Count;
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

    // ──────────────────────────────────────────────────────────────────
    // Diagnostics
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Logs the page title, all textarea name/id attributes, and first 800 chars of page text.
    /// Used to identify the correct form selectors when things break.
    /// </summary>
    private async Task LogPageDiagnosticsAsync(IPage page)
    {
        try
        {
            var title = await page.TitleAsync();
            _logger.LogDebug("Page diagnostics — title: '{Title}', URL: {Url}", title, page.Url);

            // Log all textareas
            var allTextareas = page.Locator("textarea");
            int taCount = await allTextareas.CountAsync();
            _logger.LogDebug("Textareas on page: {Count}", taCount);
            for (int i = 0; i < taCount; i++)
            {
                var t = allTextareas.Nth(i);
                var name    = await t.GetAttributeAsync("name") ?? "(none)";
                var id      = await t.GetAttributeAsync("id")   ?? "(none)";
                var visible = await t.IsVisibleAsync();
                var valLen  = (await t.InputValueAsync()).Length;
                _logger.LogDebug("  textarea[{I}]: name='{Name}', id='{Id}', visible={Visible}, valueLength={Len}", i, name, id, visible, valLen);
            }

            // Log all buttons and submit inputs
            var allButtons = page.Locator("button, input[type='submit'], input[type='button']");
            int btnCount = await allButtons.CountAsync();
            _logger.LogDebug("Buttons/submits on page: {Count}", btnCount);
            for (int i = 0; i < btnCount; i++)
            {
                var b       = allButtons.Nth(i);
                var tag     = await b.EvaluateAsync<string>("el => el.tagName");
                var bId     = await b.GetAttributeAsync("id")    ?? "(none)";
                var bName   = await b.GetAttributeAsync("name")  ?? "(none)";
                var bType   = await b.GetAttributeAsync("type")  ?? "(none)";
                var bValue  = await b.GetAttributeAsync("value") ?? "(none)";
                var bText   = (await b.InnerTextAsync()).Trim();
                var visible = await b.IsVisibleAsync();
                _logger.LogDebug("  btn[{I}]: <{Tag}> id='{Id}', name='{Name}', type='{Type}', value='{Value}', text='{Text}', visible={Visible}",
                    i, tag, bId, bName, bType, bValue, bText, visible);
            }

            // Body snippet (2000 chars)
            var bodyText = await page.Locator("body").InnerTextAsync(new() { Timeout = 5_000 });
            var snippet = bodyText.Length > 2000 ? bodyText[..2000] : bodyText;
            _logger.LogDebug("Page body snippet:{NewLine}{Snippet}", Environment.NewLine, snippet);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Page diagnostics failed");
        }
    }
}

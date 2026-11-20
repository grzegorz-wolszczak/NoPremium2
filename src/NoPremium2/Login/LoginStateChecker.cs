using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace NoPremium2.Login;

/// <summary>
/// Decides whether the page currently shown in the browser belongs to a logged-in
/// nopremium.pl session. Every check is logged (logged in → Information, not → Warning).
/// </summary>
public interface ILoginStateChecker
{
    /// <param name="page">Page to inspect (its current URL and DOM — no navigation is done).</param>
    /// <param name="context">Short description of where the check happens, used in the log line.</param>
    Task<bool> IsLoggedInAsync(IPage page, string context);
}

public sealed class LoginStateChecker : ILoginStateChecker
{
    /// <summary>Text shown in the <c>#signed</c> header panel only for a logged-in user.</summary>
    internal const string SignedPanelMarker = "Pozostały transfer";

    private readonly ILogger<LoginStateChecker> _logger;

    public LoginStateChecker(ILogger<LoginStateChecker> logger) => _logger = logger;

    /// <summary>
    /// Pure decision: logged in only when the URL is a nopremium.pl page other than /login
    /// and the <c>#signed</c> panel shows the remaining-transfer text.
    /// </summary>
    public static bool IsLoggedIn(string url, string? signedPanelText)
    {
        if (!IsNoPremiumUrl(url)) return false;
        if (IsLoginUrl(url)) return false;
        return signedPanelText is not null
               && signedPanelText.Contains(SignedPanelMarker, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNoPremiumUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Host.Equals("nopremium.pl", StringComparison.OrdinalIgnoreCase)
            || uri.Host.EndsWith(".nopremium.pl", StringComparison.OrdinalIgnoreCase));

    public static bool IsLoginUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase);

    public async Task<bool> IsLoggedInAsync(IPage page, string context)
    {
        var url = page.Url;
        string? signedText = null;

        if (IsNoPremiumUrl(url) && !IsLoginUrl(url))
        {
            try
            {
                var signed = page.Locator("#signed");
                if (await signed.CountAsync() > 0)
                    signedText = await signed.InnerTextAsync(new() { Timeout = 5_000 });
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Login check ({Context}): could not read #signed panel", context);
            }
        }

        bool loggedIn = IsLoggedIn(url, signedText);

        if (loggedIn)
            _logger.LogInformation("Login check ({Context}): LOGGED IN (URL: {Url})", context, url);
        else
            _logger.LogWarning("Login check ({Context}): NOT LOGGED IN (URL: {Url}, reason: {Reason})",
                context, url, DescribeReason(url, signedText));

        return loggedIn;
    }

    internal static string DescribeReason(string url, string? signedText)
    {
        if (!IsNoPremiumUrl(url)) return "page is not a nopremium.pl page";
        if (IsLoginUrl(url)) return "redirected to /login";
        if (signedText is null) return "#signed panel missing";
        return "#signed panel has no transfer info";
    }
}

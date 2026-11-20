namespace NoPremium2.Login;

/// <summary>
/// Thrown when a nopremium.pl page turns out to be logged out (e.g. after navigation the
/// server redirected to /login). <see cref="Browser.BrowserSessionProvider"/> catches it,
/// logs in again and retries the action once.
/// </summary>
public sealed class SessionExpiredException : NoPremiumException
{
    public string Url { get; }

    public SessionExpiredException(string context, string url)
        : base($"nopremium.pl session expired ({context}), page URL: {url}")
    {
        Url = url;
    }
}

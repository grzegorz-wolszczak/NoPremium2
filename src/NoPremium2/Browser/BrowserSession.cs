using System.Diagnostics;
using Microsoft.Playwright;

namespace NoPremium2.Browser;

public sealed class BrowserSession : IDisposable
{
    private readonly IPlaywright _playwright;

    public IBrowser Browser { get; }
    public IPage Page { get; }
    public bool IsOwned { get; }
    public Process? OwnedProcess { get; }

    internal BrowserSession(IPlaywright playwright, IBrowser browser, IPage page, bool isOwned, Process? ownedProcess)
    {
        _playwright = playwright;
        Browser = browser;
        Page = page;
        IsOwned = isOwned;
        OwnedProcess = ownedProcess;
    }

    public void KillOwnedBrowser()
    {
        if (!IsOwned || OwnedProcess is null || OwnedProcess.HasExited) return;
        OwnedProcess.Kill(entireProcessTree: true);
        OwnedProcess.Dispose();
    }

    public void Dispose() => _playwright.Dispose();
}

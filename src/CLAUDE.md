# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Run tests
dotnet test

# Run a single test class
dotnet test --filter "ClassName=BrowserManagerTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~LoginServiceTests.LoginAsync_WhenCredentialsValid_ReturnsSuccess"

# Run the application (credentials required)
NPREMIUM_U=<username> NPREMIUM_P=<password> dotnet run --project NoPremium2/NoPremium2.csproj
```

## Architecture

.NET 10 console app that automates login to `www.nopremium.pl` (a file hosting aggregator) using Vivaldi browser via Playwright CDP. The app manages browser sessions, handles Cloudflare Turnstile CAPTCHA, and fills login credentials.

**Entry point**: `Program.cs` — reads env vars (`NPREMIUM_U`, `NPREMIUM_P`), sets up DI, calls `IBrowserManager.GetOrLaunchAsync()` then `ILoginService.LoginAsync()`.

### Module breakdown

**`Browser/`** — CDP session lifecycle:
- `BrowserManager` orchestrates: discovers existing Vivaldi → allocates port → launches if needed → connects via Playwright
- `CdpPortDiscovery` finds a running Vivaldi by reading `/proc/{pid}/cmdline` (Linux-specific)
- `VivaldiLauncher` starts Vivaldi with `--remote-debugging-port` and an isolated profile under `~/.config/vivaldi-nopremium`
- `BrowserSession` wraps the Playwright objects and tracks whether this process was launched (owned) vs. pre-existing

**`Login/`** — page automation:
- `LoginService` navigates to the login URL, waits up to 120s for Turnstile CAPTCHA resolution, fills `input[name='login']` / `input[name='password']`, submits `#login_box_form`, and returns a `LoginResult`

**`AppSettings`** — record with defaults (Vivaldi path, profile dir, login URL, timeouts); registered in DI.

### Testing

xUnit + NSubstitute + AwesomeAssertions. Tests mock Playwright interfaces (`IPage`, `IElementHandle`, etc.) via NSubstitute. Tests live in `NoPremium2.Tests/` mirroring the production namespace structure.

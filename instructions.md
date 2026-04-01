# NoPremium2 — Pełna dokumentacja techniczna

---

## Spis treści

1. [Kontekst biznesowy](#1-kontekst-biznesowy)
2. [Dlaczego nowa aplikacja](#2-dlaczego-nowa-aplikacja)
3. [Wymagania funkcjonalne](#3-wymagania-funkcjonalne)
4. [Architektura — przegląd](#4-architektura--przegląd)
5. [Struktura projektu](#5-struktura-projektu)
6. [Konfiguracja](#6-konfiguracja)
7. [Moduł Browser — zarządzanie przeglądarką](#7-moduł-browser--zarządzanie-przeglądarką)
8. [Moduł Login — logowanie do nopremium.pl](#8-moduł-login--logowanie-do-nopremiumpl)
9. [Moduł NoPremium — klient przeglądarki](#9-moduł-nopremium--klient-przeglądarki)
10. [Moduł Email — klient IMAP](#10-moduł-email--klient-imap)
11. [Serwisy tła (hosted services)](#11-serwisy-tła-hosted-services)
12. [Infrastruktura pomocnicza](#12-infrastruktura-pomocnicza)
13. [Startup — Program.cs](#13-startup--programcs)
14. [Testy jednostkowe](#14-testy-jednostkowe)
15. [Zależności NuGet](#15-zależności-nuget)
16. [Decyzje projektowe i szczegóły implementacji](#16-decyzje-projektowe-i-szczegóły-implementacji)

---

## 1. Kontekst biznesowy

Strona **www.nopremium.pl** to agregator kont premium serwisów hostingowych (filefactory, mediafire, fastshare, wrzucaj.pl itd.). Użytkownik z subskrypcją otrzymuje codziennie o północy **20 GB transferu premium** (bufor max 25 GB). Gdy go nie używa — marnuje się.

Za każde zużyte 20 GB strona wysyła na email **voucher na 2 GB transferu dodatkowego** (ważny 7 dni). Voucher można zrealizować na stronie `/voucher` — dodaje 2 GB do kumulującego się transferu dodatkowego (osobna pula, nie resetuje się).

**Panel górny strony** wyświetla: `Pozostały transfer: 2100.18 GB (w tym 25 GB transferu Premium + 2075.18 GB transferu dodatkowego)`. Pobieranie zużywa najpierw transfer premium, potem dodatkowy.

**Cel aplikacji**: Automatycznie co wieczór konsumować transfer premium (poprzez kolejkowanie plików do pobrania) żeby:
- transfer premium nigdy nie osiągnął 25 GB (bo wtedy nowy nie jest doliczyty)
- nie zużywać transferu dodatkowego (gromadzić go)
- automatycznie konsumować vouchery z emaila żeby gromadzić transfer dodatkowy

---

## 2. Dlaczego nowa aplikacja

Stare aplikacje (transfer-consumer, voucher-consumer) logowały się przez HttpClient / REST. Od pewnego czasu nopremium.pl chroni się przez **Cloudflare Turnstile** — CAPTCHA, którą musi rozwiązać człowiek. Proste requesty HTTP nie przechodzą.

**Rozwiązanie**: Aplikacja uruchamia prawdziwą przeglądarkę (Chrome lub Vivaldi), podłącza się do niej przez **Playwright CDP** i steruje nią programowo. Przeglądarka jest utrzymywana otwarta przez cały czas działania aplikacji — Cloudflare widzi normalną sesję przeglądarki.

---

## 3. Wymagania funkcjonalne

### 3.1 Uruchomienie

```
NoPremium2 <path-to-config.json>
```

Aplikacja kończy się z kodem 1 (i czytelnym błędem na stderr) gdy:
- brak argumentu CLI
- plik konfiguracyjny nie istnieje / nie da się odczytać / sparsować
- brakuje wymaganych pól w configu
- plik z linkami nie istnieje / nie da się odczytać / sparsować / jest pusty
- linki mają nieprawidłowe pola (brak URL, brak Size, nieczytelny Size)
- nie znaleziono Chrome ani Vivaldi
- już działa inna instancja tej aplikacji (blokada PID)
- startup check: logowanie do nopremium.pl nie powiodło się
- startup check: test połączenia IMAP nie powiódł się

Po pomyślnym starcie: loguje konfigurację (hasła zamaskowane `***`), uruchamia serwisy tła, działa do Ctrl+C.

### 3.2 Plik konfiguracyjny (JSON)

```json
{
  "NoPremiumUsername": "user@example.com",
  "NoPremiumPassword": "haslo",
  "EmailUsername": "user@gmx.com",
  "EmailPassword": "haslo_email",
  "EmailImapServer": "imap.gmx.com:993",
  "LinksFilePath": "links.json",
  "TransferConsumer": {
    "StartTime": "23:00",
    "EndTime": "23:55",
    "IntervalMinutes": 5,
    "ReserveTransferBytes": 3221225472
  },
  "VoucherConsumer": {
    "StartTime": "23:00",
    "EndTime": "23:55",
    "IntervalMinutes": 5
  },
  "KeepaliveInterval": "01:00:00"
}
```

Poświadczenia **wyłącznie z pliku JSON** — bez zmiennych środowiskowych.

Pola opcjonalne: gdy pusty string, null lub 0 — aplikacja używa wartości domyślnych (nie crashuje).

### 3.3 Plik z linkami (JSON)

```json
{
  "Links": [
    { "Name": "Nazwa pliku", "Url": "https://...", "Size": "512MB" }
  ]
}
```

Pole `Size`: string z jednostką (B, KB, MB, GB, TB), obsługuje ułamki np. `"3.5GB"`. Walidowane przy starcie.

Ścieżka pliku z linkami może być absolutna lub relatywna. Relatywna jest szukana w:
1. katalogu pliku config.json
2. katalogu binarki aplikacji

Jeśli znaleziona w obu — błąd "ambiguous path" (użytkownik musi podać ścieżkę absolutną).

### 3.4 Obsługa przeglądarek

Priorytet: **Chrome > Vivaldi**. Wykrywanie przez sprawdzenie istnienia pliku wykonywalnego:
- Chrome: `/usr/bin/google-chrome`, `/usr/bin/google-chrome-stable`, `/usr/bin/chromium-browser`, `/usr/bin/chromium`
- Vivaldi: `/usr/bin/vivaldi`, `/usr/bin/vivaldi-stable`

Jeśli żadna nie znaleziona → błąd startu.

Profile przeglądarek (odizolowane od normalnego użytku):
- Chrome: `~/.config/chrome-nopremium`
- Vivaldi: `~/.config/vivaldi-nopremium`

### 3.5 Harmonogramy serwisów

Oba serwisy (TransferConsumer, VoucherConsumer) działają wg harmonogramu:
- aktywne w oknie czasowym `[StartTime, EndTime]`
- pierwsze uruchomienie: natychmiast gdy wchodzi w okno
- kolejne: co `IntervalMinutes` minut
- poza oknem: czeka do kolejnego dnia o `StartTime`

Okno może przekraczać północ (np. `23:00–00:30`) — obsługiwane.

---

## 4. Architektura — przegląd

```
Program.cs
  ├── ConfigLoader          — wczytanie i walidacja config.json + links.json
  ├── SingleInstanceGuard   — blokada PID w /tmp/nopremium2.pid
  ├── ChromeLauncher / VivaldiLauncher  — wykrycie przeglądarki
  ├── Serilog               — konfiguracja logowania (konsola + plik)
  └── IHost (DI container)
        ├── BrowserManager          — uruchamianie/podłączanie przeglądarki
        ├── BrowserSessionProvider  — singleton z sesją, mutex dostępu
        ├── LoginService            — logowanie do nopremium.pl
        ├── NoPremiumBrowserClient  — operacje na stronie nopremium.pl
        ├── EmailService            — IMAP (MailKit)
        ├── TimeService             — czas z worldtimeapi.org z fallback na DateTime.Now
        ├── KeepaliveService        — BackgroundService, keepalive co 1h
        ├── TransferConsumerService — BackgroundService, konsumpcja transferu 23:00–23:55
        └── VoucherConsumerService  — BackgroundService, konsumpcja voucherów 23:00–23:55
```

Cały dostęp do przeglądarki idzie przez `IBrowserSessionProvider.UsePageAsync(...)` — jest tam `SemaphoreSlim(1,1)` zapewniający wzajemne wykluczanie (jedna operacja na raz).

---

## 5. Struktura projektu

```
src/
├── NoPremium2/                    — projekt główny
│   ├── Program.cs
│   ├── AppSettings.cs
│   ├── Browser/
│   │   ├── BrowserManager.cs      — IBrowserManager, BrowserManager
│   │   ├── BrowserSession.cs      — BrowserSession (wrapper na Playwright objects)
│   │   ├── BrowserSessionProvider.cs — IBrowserSessionProvider, BrowserSessionProvider
│   │   ├── BrowserConnector.cs    — IBrowserConnector, PlaywrightBrowserConnector
│   │   ├── CdpChecker.cs          — ICdpChecker, HttpCdpChecker
│   │   ├── CdpPortDiscovery.cs    — ICdpPortDiscovery, CdpPortDiscovery, IProcessCmdlineReader
│   │   ├── ChromeLauncher.cs      — ChromeLauncher (implements IVivaldiLauncher)
│   │   ├── PortAllocator.cs       — IPortAllocator, PortAllocator
│   │   └── VivaldiLauncher.cs     — IVivaldiLauncher, VivaldiLauncher
│   ├── Config/
│   │   ├── AppConfig.cs           — AppConfig, TransferConsumerConfig, VoucherConsumerConfig
│   │   ├── ConfigLoader.cs        — ładowanie i walidacja configu
│   │   ├── DefaultConstants.cs    — wszystkie wartości domyślne
│   │   ├── LinksConfig.cs         — LinksConfig, LinkEntry
│   │   └── PathResolver.cs        — rozwiązywanie ścieżek relatywnych/absolutnych
│   ├── Email/
│   │   ├── EmailService.cs        — IEmailService, EmailService, VoucherEmail
│   │   └── VoucherCodeExtractor.cs — regex do wyciągania kodów z treści emaila
│   ├── Infrastructure/
│   │   ├── DataSizeConverter.cs   — ParseToBytes, FormatBytes
│   │   ├── LogFileHelper.cs       — ResolveLogFilePath, DeleteOldLogs
│   │   ├── SingleInstanceGuard.cs — blokada PID
│   │   └── TimeService.cs         — ITimeService, TimeService
│   ├── Login/
│   │   ├── LoginResult.cs         — record LoginResult(bool Success, string FinalUrl)
│   │   └── LoginService.cs        — ILoginService, LoginService
│   ├── NoPremium/
│   │   ├── NoPremiumBrowserClient.cs — operacje na stronie nopremium.pl
│   │   └── TransferInfo.cs        — record TransferInfo(Total, Premium, Extra)
│   └── Services/
│       ├── KeepaliveService.cs
│       ├── ScheduleHelper.cs      — logika harmonogramów
│       ├── TransferConsumerService.cs
│       └── VoucherConsumerService.cs
└── NoPremium2.Tests/              — projekt testów
    ├── AppSettingsTests.cs
    ├── Browser/
    │   ├── BrowserManagerTests.cs
    │   ├── BrowserSessionProviderTests.cs
    │   ├── CdpPortDiscoveryTests.cs
    │   └── PortAllocatorTests.cs
    ├── Config/
    │   ├── ConfigLoaderTests.cs
    │   ├── DefaultConstantsTests.cs
    │   └── PathResolverTests.cs
    ├── Infrastructure/
    │   ├── DataSizeConverterTests.cs
    │   └── LogFileHelperTests.cs
    ├── Login/
    │   └── LoginServiceTests.cs
    └── Services/
        └── ScheduleHelperTests.cs
```

---

## 6. Konfiguracja

### 6.1 `AppConfig` (record, `Config/AppConfig.cs`)

Pola wymagane (walidowane przy starcie):
- `NoPremiumUsername` — login do nopremium.pl
- `NoPremiumPassword` — hasło do nopremium.pl
- `EmailUsername` — login email (IMAP)
- `EmailPassword` — hasło email
- `EmailImapServer` — format `"host:port"` np. `"imap.gmx.com:993"`
- `LinksFilePath` — ścieżka do pliku z linkami

Pola opcjonalne:
- `TransferConsumer` — `TransferConsumerConfig` z polami: `StartTime`, `EndTime`, `IntervalMinutes`, `ReserveTransferBytes`
- `VoucherConsumer` — `VoucherConsumerConfig` z polami: `StartTime`, `EndTime`, `IntervalMinutes`
- `KeepaliveInterval` — string `"HH:mm:ss"`, default `"01:00:00"`

Pola wewnętrzne (nie w pliku usera, można nadpisać w configu dla testów):
- `LoginUrl` — default `"https://www.nopremium.pl/login"`
- `CdpReadyTimeoutMs` — default `10000`
- `TurnstileTimeoutMs` — default `120000`

### 6.2 `DefaultConstants` (`Config/DefaultConstants.cs`)

Jedyne miejsce gdzie są zakodowane wartości domyślne:
```csharp
ScheduleStartTime      = "23:00"
ScheduleEndTime        = "23:55"
ScheduleIntervalMinutes = 5
ReserveTransferBytes   = 3_221_225_472L  // 3 GB
KeepaliveInterval      = "01:00:00"
LoginUrl               = "https://www.nopremium.pl/login"
CdpReadyTimeoutMs      = 10_000
TurnstileTimeoutMs     = 120_000
ChromeProfileDirName   = "chrome-nopremium"
VivaldiProfileDirName  = "vivaldi-nopremium"
```

### 6.3 `ConfigLoader` (`Config/ConfigLoader.cs`)

Desializacja JSON: `PropertyNameCaseInsensitive = true`, komentarze dozwolone, trailing commas dozwolone.

`ApplyDefaults(AppConfig config)` — zwraca `config with { ... }` — uzupełnia puste/zerowe wartości z `DefaultConstants`. Wywoływana po deserializacji, przed walidacją.

`ParseImapServer(string)` → `(string Host, int Port)` — split po `:`, błąd jeśli nieprawidłowy format.

Błędy wywołują `Environment.Exit(1)` z komunikatem `[STARTUP ERROR] ...` na stderr. Metoda `ExitWithError(string)` jest prywatna. Program nie rzuca wyjątków na etapie ładowania configu.

### 6.4 `PathResolver` (`Config/PathResolver.cs`)

Rozwiązuje ścieżki relatywne w kontekście pliku configu i binarki:
1. Ścieżka absolutna → użyj bezpośrednio (błąd jeśli nie istnieje)
2. Ścieżka relatywna → szukaj w katalogu configu i w katalogu binarki
   - znaleziona w jednym → OK
   - znaleziona w obu (różne pliki) → `InvalidOperationException` (ambiguous)
   - nie znaleziona nigdzie → `FileNotFoundException`

### 6.5 `AppSettings` (`AppSettings.cs`)

Record używany przez `BrowserManager`, `LoginService`, launchery. Zawiera `VivaldiPath`, `ProfileDir`, `LoginUrl`, `CdpReadyTimeoutMs`, `TurnstileTimeoutMs`. `AppSettings.From(AppConfig config)` tworzy instancję z configu (tylko pola timeoutów i LoginUrl — VivaldiPath i ProfileDir mają swoje hardcoded defaults w recordzie).

---

## 7. Moduł Browser — zarządzanie przeglądarką

### 7.1 Przepływ `BrowserManager.GetOrLaunchAsync()`

```
1. CdpPortDiscovery.FindExistingPortAsync()
   → szuka procesów Chrome/Vivaldi z --remote-debugging-port=XXXX w cmdline
   → weryfikuje czy port odpowiada (GET http://localhost:{port}/json/version)
   → jeśli znaleziony: użyj istniejącego (isOwned=false)
   
2. Jeśli nie znaleziono:
   → PortAllocator.GetFreePort() — TcpListener(port=0) → odczyt portu → Stop()
   → IVivaldiLauncher.Launch(port, profileDir, loginUrl) — Process.Start()
   → IVivaldiLauncher.WaitForCdpAsync(port) — polling co 500ms, max 10s
   → isOwned=true, zachowuje Process

3. PlaywrightBrowserConnector.ConnectAsync(port)
   → Playwright.CreateAsync()
   → chromium.ConnectOverCDPAsync("http://localhost:{port}")
   → czeka na context (polling co 300ms, max 20 prób)
   → bierze page: context.Pages[0] lub NewPageAsync()
   
4. Zwraca BrowserSession(playwright, browser, page, isOwned, ownedProcess)
```

### 7.2 `CdpPortDiscovery` (`Browser/CdpPortDiscovery.cs`)

Sprawdza procesy w kolejności: `chrome`, `google-chrome`, `chromium`, `chromium-browser`, `vivaldi`.

`LinuxProcessCmdlineReader.GetByName(name)` — `Process.GetProcessesByName(name)` + odczyt `/proc/{pid}/cmdline` (null-delimited string).

`ParsePort(cmdline)` — szuka `--remote-debugging-port=XXXX` w null-delimited string. Metoda `internal static` dla testowalności.

### 7.3 `BrowserSession` (`Browser/BrowserSession.cs`)

Wrapper na obiekty Playwright. Pola: `Browser`, `Page`, `IsOwned`, `OwnedProcess`.

`KillOwnedBrowser()` — `OwnedProcess.Kill(entireProcessTree: true)` jeśli `IsOwned && !HasExited`.

`Dispose()` — `_playwright.Dispose()`.

### 7.4 `BrowserSessionProvider` (`Browser/BrowserSessionProvider.cs`)

Singleton. Zarządza jedną sesją przeglądarki, mutex przez `SemaphoreSlim(1,1)`.

```csharp
Task<T> UsePageAsync<T>(Func<IPage, Task<T>> action, CancellationToken ct)
```

Wewnętrznie wywołuje `EnsureSessionAsync(ct)`:
- jeśli sesja żyje (`browser.IsConnected && !page.IsClosed`) → zwróć page
- jeśli martwa → zaloguj ostrzeżenie, dispose starej sesji, `_session = null`
- uruchom nową sesję: `_browserManager.GetOrLaunchAsync(ct)` + `_loginService.LoginAsync(...)`
- rzuć `InvalidOperationException` jeśli login nie powiódł się

Obsługa zamkniętej przeglądarki przy Ctrl+C:
```csharp
catch (Microsoft.Playwright.PlaywrightException) when (ct.IsCancellationRequested)
{
    throw new OperationCanceledException(ct);
}
```
→ konwertuje `PlaywrightException` (rzucane gdy przeglądarka zamknięta podczas oczekiwania) na `OperationCanceledException` — niewidoczne jako błąd przy shutdown.

`DisposeAsync()` — idempotentne przez `Interlocked.Exchange(ref _session, null)`. Wywołuje `KillOwnedBrowser()` + `Dispose()`. Przeglądarka jest zamykana tylko jeśli aplikacja ją uruchomiła (`IsOwned`).

**Ważne**: `host.RunAsync()` wewnętrznie robi `Dispose()` na hoście (co disposes kontener DI → `BrowserSessionProvider`). Program.cs **nie wywołuje** `DisposeAsync()` explicite — żeby uniknąć podwójnego dispose.

`IAsyncDisposable.DisposeAsync()` jest wywoływane przez kontener DI przy shutdown.

### 7.5 `IVivaldiLauncher` — wspólny interfejs

```csharp
Process Launch(int port, string profileDir, string startUrl);
Task WaitForCdpAsync(int port, CancellationToken ct = default);
```

Implementowany przez `VivaldiLauncher` i `ChromeLauncher` (ChromeLauncher implements IVivaldiLauncher — nazwa historyczna).

Argumenty uruchomienia przeglądarki:
```
--remote-debugging-port={port}
--user-data-dir="{profileDir}"
--no-first-run
--no-default-browser-check
{startUrl}
```

`WaitForCdpAsync` — polling co 500ms do deadline (teraz + `CdpReadyTimeoutMs`), sprawdza `ICdpChecker.IsRespondingAsync(port)`.

---

## 8. Moduł Login — logowanie do nopremium.pl

### 8.1 `LoginService.LoginAsync(page, login, password)`

```
1. Jeśli page.Url NIE zawiera "/login":
   → GotoAsync(loginUrl, WaitUntil=DOMContentLoaded, Timeout=60s)

2. Sprawdź page.Url po nawigacji:
   → jeśli NIE zawiera "/login" → "Already logged in" → return Success(url)
   
3. Uruchom RACE (Task.WhenAny):
   a) waitForForm = loginInput.WaitForAsync(Visible, Timeout=TurnstileTimeoutMs)
   b) waitForManualLogin = page.WaitForURLAsync(url => !url.Contains("/login"), Timeout=TurnstileTimeoutMs)
   
4. Po Task.WhenAny sprawdź:
   → jeśli page.Url NIE zawiera "/login" → "Manual login detected" → return Success(url)
   
5. await waitForForm (resurface błąd jeśli form wait faktycznie się nie powiódł)

6. Wypełnij formularz:
   → loginForm = page.Locator("#login_box_form")
   → loginInput = loginForm.Locator("input[name='login']")
   → passwordInput = loginForm.Locator("input[name='password']")
   → submitBtn = page.Locator("#button_input")
   → loginInput.FillAsync(login)
   → passwordInput.FillAsync(password)
   → submitBtn.ClickAsync()
   
7. page.WaitForURLAsync(url => !url.Contains("/login"), Timeout=30s)
8. return LoginResult(success = !url.Contains("/login"), finalUrl = page.Url)
```

**Kluczowe**: `TurnstileTimeoutMs = 120s` — tyle czeka na pojawienie się formularza po rozwiązaniu Cloudflare przez użytkownika.

**RACE pattern** jest konieczny: jeśli użytkownik ręcznie się zalogował zanim aplikacja zdążyła wypełnić formularz, strona mogła już opuścić `/login` — `waitForForm` nigdy by nie ukończył, ale `waitForManualLogin` się zakończy.

`LoginResult` — `record sealed (bool Success, string FinalUrl)`.

---

## 9. Moduł NoPremium — klient przeglądarki

### 9.1 `NoPremiumBrowserClient` (`NoPremium/NoPremiumBrowserClient.cs`)

#### `ReadTransferInfoAsync(page)`

Pobiera tekst z `#signed` (div w nagłówku strony, widoczny na każdej podstronie nopremium.pl). Parsuje regexem:

- `TotalTransferRegex`: `Pozostały transfer:\s*(?<total>[\d.,]+)\s*(?<totalUnit>GB|MB|TB|KB)`
- `TransferRegex`: `w tym\s+(?<premium>[\d.,]+)\s*(?<premUnit>GB|MB|TB|KB)\s+transferu Premium\s*\+\s*(?<extra>[\d.,]+)\s*(?<extraUnit>GB|MB|TB|KB)\s+transferu dodatkowego`

`ParseSize(value, unit)` — normalizuje przecinek na kropkę, parsuje przez `double.TryParse` z `InvariantCulture`, konwertuje przez `DataSizeConverter.ParseToBytes($"{d}{unit}")`.

Zwraca `TransferInfo?(TotalBytes, PremiumBytes, ExtraBytes)` lub `null` przy błędzie.

#### `AddLinksToQueueAsync(page, urls, ct)`

```
1. Jeśli NIE na /files → GotoAsync(FilesUrl, Load, Timeout=30s)
   Jeśli już na /files → tylko logi, nie nawiguj ponownie
   
2. Guard: jeśli po nawigacji nadal NIE na /files → InvalidOperationException (sesja wygasła?)

3. textarea = page.Locator("textarea[name='links']")
   WaitForAsync(Visible, Timeout=30s)
   FillAsync(urls joined by "\n")

4. submitBtn = page.Locator("#addlinks").First
   jeśli nie widoczny: fallback → GetByRole(Button, "Dodaj", Exact=true).First
   ClickAsync()

5. processedSection = page.Locator("text=Przetwarzane pliki").First
   WaitForAsync(Visible, Timeout=30s)
   → jeśli TimeoutException → return 0 (żaden link nierozpoznany)

6. Checkboxes = page.Locator("input[type='checkbox']")
   Zaznacz każdy niezaznaczony checkbox

7. addSelectedBtn = GetByRole(Button, "Dodaj zaznaczone")
   jeśli nie widoczny: fallback → Locator("button:has-text('Dodaj zaznaczone')")
   ClickAsync()

8. WaitForLoadStateAsync(DOMContentLoaded, Timeout=60s)
9. return checkboxCount
```

#### `ConsumeVoucherAsync(page, code, ct)`

```
1. GotoAsync(VoucherUrl="/voucher", DOMContentLoaded, Timeout=30s)
2. voucherInput = Locator("input[name='voucher']")
   WaitForAsync(Visible, Timeout=10s)
   FillAsync(code)
3. doladujBtn = GetByRole(Button, "Doładuj")
   jeśli nie widoczny: fallback → Locator("input[value='Doładuj']")
   ClickAsync()
4. WaitForLoadStateAsync(DOMContentLoaded, Timeout=15s)
5. bodyText = page.Locator("body").InnerTextAsync()
6. InterpretVoucherResponse(bodyText, code) → VoucherResult
```

`VoucherResult` enum:
- `Success` — "Konto doładowano pomyślnie"
- `CaptchaDetected` — "Przepisz kod z obrazka"
- `InvalidCode` — "Wprowadzony kod nie istnieje"
- `AlreadyUsed` — "Wprowadzony kod został już wykorzystany"
- `Expired` — "Wprowadzony kod stracił ważność"
- `UnknownResponse` — żadne z powyższych

#### `NavigateKeepaliveAsync(page)`

Round-robin między `https://www.nopremium.pl/help` i `https://www.nopremium.pl/offer`. `GotoAsync(DOMContentLoaded, Timeout=30s)`.

### 9.2 `TransferInfo` (`NoPremium/TransferInfo.cs`)

```csharp
public sealed record TransferInfo(long TotalBytes, long PremiumBytes, long ExtraBytes)
```

`ToString()` → `"Total: 10.23GB (10737418240 b) (Premium: ... + Extra: ...)"` — używa `DataSizeConverter.FormatBytes`.

---

## 10. Moduł Email — klient IMAP

### 10.1 `EmailService` (`Email/EmailService.cs`)

Używa **MailKit**. SSL zawsze włączone (`useSsl: true`). Certyfikaty akceptowane bez weryfikacji (`ServerCertificateValidationCallback = (_, _, _, _) => true` — elastyczność dla różnych providerów).

#### `GetUnreadVouchersAsync(ct)`

```
1. ImapClient → ConnectAsync(host, port, ssl=true) + AuthenticateAsync
2. inbox.OpenAsync(ReadOnly)
3. SearchAsync(SearchQuery.NotSeen)
4. Dla każdego UID: GetMessageAsync → TextBody ?? HtmlBody → VoucherCodeExtractor.ExtractFrom()
5. Jeśli kod znaleziony: dodaj VoucherEmail{Uid, Code} do wyników
6. DisconnectAsync(quit=true)
7. Return List<VoucherEmail>
```

#### `MarkAsSeenAsync(uids, ct)`

```
1. ImapClient → Connect + Authenticate
2. inbox.OpenAsync(ReadWrite)
3. Dla każdego UID: AddFlagsAsync(uid, MessageFlags.Seen, silent=true)
4. DisconnectAsync(quit=true)
```

### 10.2 `VoucherCodeExtractor` (`Email/VoucherCodeExtractor.cs`)

Regex: `kod do.adowuj.cy:\s+.*?\b(?<code>[0-9a-fA-F]{10,})\b`

Wyciąga hex-kod (min 10 znaków) po frazie "Twój kod doładowujący:" z treści emaila. Punkt `.` w regexie obsługuje polskie znaki (ł, ó) zamiast escape'owania.

### 10.3 `VoucherEmail` (`Email/EmailService.cs`)

```csharp
public sealed class VoucherEmail
{
    public UniqueId Uid { get; init; }
    public string Code { get; init; } = "";
}
```

---

## 11. Serwisy tła (hosted services)

Wszystkie dziedziczą z `BackgroundService`. Wzorzec pętli:
```
while (!stoppingToken.IsCancellationRequested)
{
    try {
        now = timeService.GetLocalTimeAsync(ct)
        wait = ScheduleHelper.TimeUntilNextRun(...)
        if (wait > 0) { Delay(min(wait, 1min)); continue; }
        _lastRunAt = now
        await RunAsync(ct)
    }
    catch (OperationCanceledException) { break; }
    catch (Exception ex) {
        if (stoppingToken.IsCancellationRequested) break;
        LogError; Delay(1min);
    }
}
```

**Ważne**: `if (stoppingToken.IsCancellationRequested) break;` w catch na Exception — zapobiega pętlowaniu po `OperationCanceledException` rzuconym przez `Task.Delay` gdy CT jest już anulowany.

### 11.1 `TransferConsumerService`

Dodatkowy stan: `HashSet<string> _queuedToday` — reset gdy `now.Date > _lastQueuedDate` (nowy dzień). Zapobiega ponownemu kolejkowaniu tych samych linków w tym samym dniu.

`RunAsync(ct)`:
```
1. UsePageAsync: GotoAsync("/files", Load, 30s)
2. ReadTransferInfoAsync → jeśli null: skip
3. Jeśli premium <= ReserveTransferBytes: log "nothing to consume", return
4. budget = premium - reserve
5. SelectLinks(budget) → lista linków których suma Size <= budget, pomiń już kolejkowane dziś
6. AddLinksToQueueAsync(page, links.Select(l => l.Url))
7. Dodaj URL-e do _queuedToday
```

`SelectLinks(budget)`: iteruje po liście linków greedy (pierwszy pasujący), pomija już kolejkowane i te których Size > remaining.

### 11.2 `VoucherConsumerService`

`RunAsync(ct)`:
```
1. emailService.GetUnreadVouchersAsync() → List<VoucherEmail>
2. Jeśli pusta: return
3. Dla każdego vouchera:
   a. UsePageAsync: client.ConsumeVoucherAsync(page, code)
   b. Zbierz seenUids dla Success/InvalidCode/AlreadyUsed/Expired
   c. CaptchaDetected: LogError, goto done (przerwij przetwarzanie)
   d. UnknownResponse: nie oznaczaj jako seen
4. done: emailService.MarkAsSeenAsync(seenUids)
```

### 11.3 `KeepaliveService`

```
while (!ct.IsCancellationRequested)
{
    Delay(_interval, ct)  // czeka interwał (1h)
    UsePageAsync: client.NavigateKeepaliveAsync(page)
}
```

Błędy nawigacji logowane jako Warning (nie zatrzymują usługi).

### 11.4 `ScheduleHelper` (`Services/ScheduleHelper.cs`)

`TimeUntilNextRun(now, startTime, endTime, interval, lastRunAt?)` → `TimeSpan`:
- `TimeSpan.Zero` = uruchom teraz
- `> 0` = czekaj tyle

Logika:
1. Sprawdź czy `currentTime ∈ [startTime, endTime]` (obsługuje przekroczenie północy: jeśli `start > end`, okno przekracza północ)
2. Poza oknem: czekaj do następnego dnia o `startTime`
3. W oknie, `lastRunAt == null`: uruchom natychmiast
4. W oknie, czas od ostatniego < interwał: czekaj resztę interwału
5. W oknie, czas od ostatniego >= interwał: uruchom teraz

`ParseTimeOnly(str, default)` — próbuje `"HH:mm"` potem `"H:mm"`, fallback na default.

---

## 12. Infrastruktura pomocnicza

### 12.1 `DataSizeConverter` (`Infrastructure/DataSizeConverter.cs`)

#### `ParseToBytes(string sizeStr) → long`

Obsługiwane jednostki: TB, GB, MB, KB, B (case-insensitive). Rozpatruje od największej do najmniejszej (żeby "KB" nie matchował "B").
- Sprawdza czy string kończy się jednostką
- Parsuje liczbę przez `double.TryParse` z `InvariantCulture` (obsługuje ułamki)
- Fallback: `long.TryParse` (plain bytes bez jednostki)
- Rzuca `FormatException` gdy nie można sparsować

#### `FormatBytes(long bytes) → string`

Format: `{value:F2}{unit} ({bytes} b)` — separator dziesiętny to zawsze **kropka** (InvariantCulture), brak spacji między liczbą a jednostką, raw bytes w nawiasach z małym 'b'.

```
>= 1 GB  → "10.23GB (10737418240 b)"
>= 1 MB  → "512.00MB (536870912 b)"
>= 1 KB  → "1.00KB (1024 b)"
< 1 KB   → "512 b"
```

Implementacja używa `string.Create(CultureInfo.InvariantCulture, $"...")`.

### 12.2 `LogFileHelper` (`Infrastructure/LogFileHelper.cs`)

#### `ResolveLogFilePath(logDir, now) → string`

Zwraca ścieżkę do pliku logu dla bieżącego uruchomienia:
- Format: `logs_YYYYMMDD.NN.log` (NN = zero-padded numer uruchomienia, zaczyna od 01)
- Liczy istniejące pliki pasujące do wzorca `logs_{today}.??.log` i używa `count + 1`

Przykłady: `logs_20260401.01.log`, `logs_20260401.02.log`

#### `DeleteOldLogs(logDir, now, retentionDays=30)`

Usuwa pliki `logs_????????.??.log` których data (z nazwy) jest starsza niż `now - retentionDays`.

### 12.3 `SingleInstanceGuard` (`Infrastructure/SingleInstanceGuard.cs`)

PID file: `/tmp/nopremium2.pid` (`Path.GetTempPath() + "nopremium2.pid"`).

`TryAcquire()`:
1. Jeśli plik istnieje: odczytaj PID, sprawdź `Process.GetProcessById(pid)`
   - Proces istnieje → return false (existingPid, existingStartTime)
   - `ArgumentException` → stale lock, kontynuuj
2. Zapisz bieżący PID do pliku (non-fatal jeśli błąd zapisu)
3. `_acquired = true`, return true

`Dispose()` — usuwa plik PID jeśli `_acquired`.

### 12.4 `TimeService` (`Infrastructure/TimeService.cs`)

`GetLocalTimeAsync(ct)`:
1. Pobiera czas z `http://worldtimeapi.org/api/ip` (timeout 5s)
2. Parsuje pole `"datetime"` jako `DateTimeOffset` (ISO 8601 z offsetem)
3. Zwraca `.LocalDateTime`
4. Fallback na `DateTime.Now` przy timeout lub jakimkolwiek błędzie (loguje Warning)

---

## 13. Startup — Program.cs

Kolejność kroków przy starcie:

```
1. Walidacja argumentu CLI → config file path
2. Bootstrap logger (Serilog, tylko konsola)
3. ConfigLoader.LoadAppConfig → AppConfig (exit 1 przy błędzie)
4. ConfigLoader.LoadLinksConfig → LinksConfig
5. ValidateLinks(links) → walidacja URL i Size każdego wpisu
6. ConfigLoader.ParseImapServer → (host, port)
7. SingleInstanceGuard.TryAcquire → exit 1 jeśli już działa
8. ChromeLauncher.FindExecutable() / VivaldiLauncher.FindExecutable()
   → exit 1 jeśli żadna
   → Chrome ma priorytet
9. Konfiguracja Serilog:
   → logDir = AppContext.BaseDirectory + "/Logs"
   → Directory.CreateDirectory(logDir)
   → logFile = LogFileHelper.ResolveLogFilePath(logDir, DateTime.Now)
   → LogFileHelper.DeleteOldLogs(logDir, DateTime.Now)
   → Log.Logger = MinLevel.Debug, Console + File (oba z pełnym formatem timestamps)
10. Log konfiguracji (hasła jako "***")
11. Host.CreateDefaultBuilder → UseSerilog → ConfigureServices:
    → AppConfig, LinksConfig — singleton
    → AppSettings.From(config) — singleton
    → HttpClient (Timeout=10s) — singleton
    → TimeService, HttpCdpChecker, LinuxProcessCmdlineReader,
       CdpPortDiscovery, PortAllocator, PlaywrightBrowserConnector,
       BrowserManager — singletony
    → ChromeLauncher lub VivaldiLauncher jako IVivaldiLauncher — singleton
    → LoginService, BrowserSessionProvider — singletony
    → VoucherCodeExtractor, EmailService — singletony
    → NoPremiumBrowserClient — singleton
    → KeepaliveService, TransferConsumerService, VoucherConsumerService — AddHostedService
    .Build()
12. Startup check (timeout 3 min):
    → sessionProvider.InitializeAsync(ct) — login do nopremium.pl
    → emailService.GetUnreadVouchersAsync(ct) — test IMAP
    → exit 1 przy błędzie
13. host.RunAsync() — działa do Ctrl+C
14. finally: Log.CloseAndFlushAsync(), instanceGuard.Dispose()
    UWAGA: host.RunAsync() wewnętrznie disposes host (DI container → BrowserSessionProvider → KillBrowser)
```

**Format logów**:
- Konsola: `[yyyy-MM-dd HH:mm:ss.fff zzz] [LVL] SourceContext Message`
- Plik: identyczny format jak konsola

**Logowanie konfiguracji**:
```
=== NoPremium2 Configuration ===
Config file: ...
Links file: ...
Links count: N
NoPremium username: user
NoPremium password: ***
Email username: user
Email password: ***
Email IMAP: host:port
Transfer consumer: START–END every N min, reserve X bytes
Voucher consumer: START–END every N min
Keepalive interval: HH:mm:ss
================================
```

---

## 14. Testy jednostkowe

### Stack testowy

- **xUnit** — framework
- **NSubstitute** — mocki (interfejsy Playwright: `IPage`, `ILocator` itd.)
- **AwesomeAssertions** — asercje (`.Should().Be(...)`)
- `[InternalsVisibleTo("NoPremium2.Tests")]` — dostęp do internal metod

### Pliki testowe

#### `LoginServiceTests` (5 testów)
- `LoginAsync_WhenAlreadyOnLoginPage_DoesNotNavigate`
- `LoginAsync_WhenNotOnLoginPage_NavigatesToLoginUrl`
- `LoginAsync_FillsCredentialsAndSubmits` — **skomplikowany mock**:
  - Dwa wywołania `WaitForURLAsync`: 1. nigdy się nie kończy (race), 2. kończy się i zmienia URL
  - Counter `waitForUrlCallCount` w Returns callback rozróżnia wywołania
- `LoginAsync_WhenRedirectedAwayFromLogin_ReturnsSuccess`
- `LoginAsync_WhenStillOnLoginPage_ReturnsFailure`

Trick z `SetupPage()`:
```csharp
page.WaitForURLAsync(Arg.Any<Func<string, bool>>(), Arg.Any<PageWaitForURLOptions?>())
    .Returns(Task.CompletedTask)
    .AndDoes(_ => page.Url.Returns(postLoginUrl));
```
Dla testu `FillsCredentialsAndSubmits` ten wzorzec NIE działa (bo pierwsza WaitForURLAsync to race i musi być niekończąca) — używa counter zamiast tego.

#### `BrowserManagerTests`

Mockuje `ICdpPortDiscovery`, `IPortAllocator`, `IVivaldiLauncher`, `IBrowserConnector`. Testuje:
- użycie istniejącego portu vs launch nowego
- właściwe przekazywanie parametrów

#### `CdpPortDiscoveryTests`

Testuje `ParsePort()` bezpośrednio (internal static). Różne przypadki: z portem, bez portu, null, null-delimiter.

#### `PortAllocatorTests`

Sprawdza że `GetFreePort()` zwraca port > 0.

#### `BrowserSessionProviderTests`

Testuje `IsSessionAlive()` (internal static) dla różnych kombinacji `IsConnected`/`IsClosed`.

#### `ScheduleHelperTests`

Testuje `TimeUntilNextRun()` i `ParseTimeOnly()`. Scenariusze:
- przed oknem, w oknie, po oknie
- okno przekraczające północ
- lastRunAt null vs ustawione

#### `LogFileHelperTests` (12 testów)

Testuje `ResolveLogFilePath()` i `DeleteOldLogs()`. Używa `Path.GetTempPath()` jako tymczasowego katalogu. Sprawdza:
- numery plików (01, 02...)
- usuwanie starych vs zachowywanie nowych

#### `ConfigLoaderTests` (14 testów)

Testuje `ParseImapServer()` i `ApplyDefaults()`. Sprawdza zachowanie defaults dla różnych kombinacji pustych/zerowych wartości.

#### `AppSettingsTests` (4 testy)

Testuje `AppSettings.From(config)` — przekazywanie wartości z AppConfig.

#### `DataSizeConverterTests`

`ParseToBytes`: jednostki, ułamki, case-insensitive, plain number, wyjątki.

`FormatBytes`:
- `< 1KB` → `"512 b"`
- `>= 1KB` → `"1.00KB (1024 b)"`
- `>= 1MB` → `"512.00MB (536870912 b)"`
- `>= 1GB` → `"1.00GB (1073741824 b)"`
- `0` → `"0 b"`

#### `DefaultConstantsTests`

Sprawdza że stałe mają oczekiwane wartości (regression test).

#### `PathResolverTests`

Testuje wszystkie ścieżki kodu: absolutna, relatywna (jeden wynik), relatywna (dwa wyniki = błąd), nie znaleziono.

---

## 15. Zależności NuGet

### Projekt główny (`NoPremium2.csproj`)

Target frameworks: `net8.0` i `net10.0`. `LangVersion=10`. `Nullable=enable`.

| Pakiet | Wersja | Uwagi |
|--------|--------|-------|
| Microsoft.Extensions.DependencyInjection | 8.0.0 / 10.0.5 | Per TFM |
| Microsoft.Extensions.Hosting | 8.0.0 / 10.0.5 | Per TFM |
| Microsoft.Extensions.Logging | 8.0.0 / 10.0.5 | Per TFM |
| MailKit | 4.12.0 | IMAP |
| Microsoft.Playwright | 1.58.0 | CDP browser automation |
| Serilog | 4.2.0 | |
| Serilog.Extensions.Hosting | 8.0.0 | |
| Serilog.Extensions.Logging | 8.0.0 | |
| Serilog.Sinks.Console | 6.0.0 | |
| Serilog.Sinks.File | 6.0.0 | |

### Projekt testowy (`NoPremium2.Tests.csproj`)

| Pakiet | Wersja |
|--------|--------|
| Microsoft.NET.Test.Sdk | 17.12.0 |
| xunit | 2.9.3 |
| xunit.runner.visualstudio | 2.8.2 |
| AwesomeAssertions | * (najnowsza) |
| NSubstitute | 5.* |

---

## 16. Decyzje projektowe i szczegóły implementacji

### Jedna aplikacja zamiast dwóch

Obie funkcjonalności (transfer + voucher) w jednej aplikacji z shared sesją przeglądarki. Zaleta: logowanie przez Cloudflare tylko raz, sesja utrzymywana przez keepalive.

### Playwright CDP zamiast HTTP

Cloudflare Turnstile wymaga prawdziwej przeglądarki. Playwright łączy się przez Chrome DevTools Protocol do istniejącej lub nowo uruchomionej przeglądarki. Aplikacja NIE zamyka i NIE otwiera nowej przeglądarki przy każdej operacji.

### Wykrywanie istniejącej przeglądarki

Jeśli przeglądarka (Chrome/Vivaldi) jest już uruchomiona z `--remote-debugging-port`, aplikacja podłącza się do niej bez zabijania. Po zakończeniu aplikacji przeglądarka zostaje (nie jest zamykana). Jeśli aplikacja uruchomiła przeglądarkę samodzielnie — zabija ją przy shutdown.

### Mutex dostępu do przeglądarki

`SemaphoreSlim(1,1)` w `BrowserSessionProvider` zapewnia że tylko jeden serwis naraz może korzystać ze strony. Serwisy współdzielą jedną kartę przeglądarki.

### Obsługa zamkniętej przeglądarki

`EnsureSessionAsync` sprawdza `browser.IsConnected && !page.IsClosed` przy każdym wywołaniu. Jeśli sesja martwa — automatycznie reconnect + re-login.

### Graceful shutdown (Ctrl+C)

Problem: Playwright rzuca `PlaywrightException` (nie `OperationCanceledException`) gdy przeglądarka jest zamykana. Bez obsługi to tworzy ERROR logi przy czystym shutdown.

Rozwiązanie w `BrowserSessionProvider.UsePageAsync`:
```csharp
catch (Microsoft.Playwright.PlaywrightException) when (ct.IsCancellationRequested)
{
    throw new OperationCanceledException(ct);
}
```

Dodatkowo w pętlach serwisów:
```csharp
catch (Exception ex)
{
    if (stoppingToken.IsCancellationRequested) break; // ← kluczowe
    ...
}
```

### Czas zewnętrzny

`worldtimeapi.org/api/ip` z timeout 5s. Fallback na `DateTime.Now`. Używany zamiast `DateTime.Now` bo zegar maszyny może być niezsynchronizowany.

### Separator dziesiętny w FormatBytes

Zawsze kropka (`CultureInfo.InvariantCulture`) niezależnie od locale systemu (Polska używa przecinka — bez tego wyświetlałoby np. "10,23GB").

### Format pliku logu

`logs_YYYYMMDD.NN.log` (np. `logs_20260401.01.log`). Zmiana z rolling Serilog na fixed path — dlatego że rolling Serilog tworzy jeden plik na dzień niezależnie od liczby uruchomień, a wymaganiem jest nowy plik na każde uruchomienie tego samego dnia.

### Konfiguracja per-TFM w .csproj

`Microsoft.Extensions.*` muszą pasować do target framework version. Inne pakiety (MailKit, Playwright, Serilog) są TFM-niezależne.

### InternalsVisibleTo

```xml
<AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
  <_Parameter1>NoPremium2.Tests</_Parameter1>
</AssemblyAttribute>
```

Pozwala testom na dostęp do `internal` metod: `CdpPortDiscovery.ParsePort()`, `NoPremiumBrowserClient.ParseTransferText()`, `BrowserSessionProvider.IsSessionAlive()`, `ConfigLoader.ApplyDefaults()`, `ConfigLoader.LoadLinksConfig(string)`.

### Podwójny dispose — pułapka

`host.RunAsync()` wewnętrznie wywołuje `host.Dispose()` w swoim `finally` bloku. Ten dispose propaguje przez kontener DI do `BrowserSessionProvider.DisposeAsync()`. Program.cs NIE może wywołać `DisposeAsync()` explicite — doprowadziłoby to do podwójnego dispose i potencjalnego błędu przy próbie zabicia już zabitego procesu. `Interlocked.Exchange(ref _session, null)` zabezpiecza przed tym w samym `DisposeAsync`.

### Email — brak cache sesji IMAP

`EmailService` tworzy nowe `ImapClient` przy każdym wywołaniu (connect + auth + disconnect). Nie ma persistent IMAP connection. Upraszcza zarządzanie połączeniem kosztem niewielkiego narzutu.

### VoucherConsumer — obsługa CAPTCHA

Jeśli na stronie vouchera pojawi się CAPTCHA — serwis zatrzymuje przetwarzanie voucherów dla tej iteracji (goto done). Emaile z voucherami nie są oznaczane jako przeczytane — zostaną przetworzone w kolejnej iteracji.

---

## Polecenia developerskie

```bash
# Build
dotnet build

# Testy wszystkie
dotnet test

# Test konkretnej klasy
dotnet test --filter "FullyQualifiedName~LoginServiceTests"

# Uruchomienie (wymaga pliku config)
dotnet run --project NoPremium2/NoPremium2.csproj -- config.json
```

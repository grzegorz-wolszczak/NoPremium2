# NoPremium2 — Architektura i decyzje projektowe

Wymagania: `REQUIREMENTS.md`. Ten dokument opisuje *jak* aplikacja jest zbudowana
i *dlaczego* — nie jest specyfikacją krok-po-kroku (szczegóły są w kodzie).

---

## 1. Kontekst biznesowy

Strona **www.nopremium.pl** to agregator kont premium serwisów hostingowych
(filefactory, mediafire, fastshare, wrzucaj.pl itd.). Użytkownik z subskrypcją
otrzymuje codziennie o północy pulę **transferu premium** (bufor max ~25 GB).
Gdy go nie używa — marnuje się, bo nowa pula nie kumuluje się ponad bufor.

Za każde zużyte 20 GB strona wysyła na email **voucher na 2 GB transferu
dodatkowego** (ważny 7 dni). Voucher realizuje się na `/voucher` — dodaje 2 GB do
osobnej, kumulującej się puli transferu dodatkowego.

Nagłówek strony pokazuje: `Pozostały transfer: 2100.18 GB (w tym 25 GB transferu
Premium + 2075.18 GB transferu dodatkowego)`. Pobieranie zużywa najpierw transfer
premium, potem dodatkowy.

**Cel aplikacji**: automatycznie co wieczór konsumować transfer premium
(kolejkując pliki do pobrania), żeby premium nie przepadał i nie trzeba było
ruszać transferu dodatkowego; oraz realizować vouchery z maila, by transfer
dodatkowy narastał.

## 2. Dlaczego przeglądarka + Playwright CDP

Stare aplikacje logowały się przez `HttpClient` / REST. Nopremium.pl chroni
logowanie przez **Cloudflare Turnstile** (CAPTCHA wymagająca człowieka) — surowe
żądania HTTP nie przechodzą.

Rozwiązanie: aplikacja uruchamia prawdziwą przeglądarkę (Chrome lub Vivaldi),
podłącza się do niej przez **Playwright CDP** i steruje nią programowo.
Przeglądarka jest utrzymywana otwarta przez cały czas działania aplikacji —
Cloudflare widzi normalną, długożyjącą sesję. Człowiek rozwiązuje CAPTCHA raz,
ręcznie, w oknie przeglądarki.

---

## 3. Architektura

```
Program.cs
  ├── ConfigLoader          — wczytanie + walidacja config.json i links.json,
  │                           parsowanie "host:port", rozwiązanie katalogu logów
  ├── SingleInstanceGuard   — blokada PID w /tmp/nopremium2.pid
  ├── Chrome/VivaldiLauncher.FindExecutable — wykrycie przeglądarki (Chrome > Vivaldi)
  ├── Serilog               — konsola + plik
  └── IHost (kontener DI)
        ├── BrowserManager          — wykrycie / uruchomienie / podłączenie przeglądarki
        ├── BrowserSessionProvider  — jedna sesja, mutex dostępu, auto-reconnect + relogin
        ├── LoginService            — logowanie przez Turnstile
        ├── NoPremiumBrowserClient  — operacje na stronach nopremium.pl
        ├── EmailService            — IMAP (MailKit)
        ├── TimeService             — czas (zegar systemowy)
        ├── ConsumerActivityState   — licznik aktywnych consumerów (pauzuje keepalive)
        ├── KeepaliveService        — re-login co interwał
        ├── TransferConsumerService — konsumpcja transferu wg harmonogramu
        └── VoucherConsumerService  — realizacja voucherów wg harmonogramu
```

**Zasada dostępu do przeglądarki**: wszystkie serwisy współdzielą jedną kartę.
Każda operacja idzie przez `IBrowserSessionProvider.UsePageAsync(...)`, gdzie
`SemaphoreSlim(1,1)` zapewnia, że w danej chwili działa tylko jedna.

---

## 4. Struktura projektu

```
src/
├── NoPremium2/
│   ├── Program.cs                — bootstrap, DI, startup checks, pętla główna
│   ├── AppSettings.cs            — ustawienia przeglądarki/logowania (z BaseConfig)
│   ├── NoPremiumException.cs
│   ├── Browser/
│   │   ├── BrowserManager.cs             — orkiestracja (resolve → connect | launch | kill)
│   │   ├── ExistingBrowserResolver.cs    — kolejność wykrywania działającej przeglądarki
│   │   ├── DevToolsActivePortReader.cs   — odczyt <profil>/DevToolsActivePort
│   │   ├── ProfileLockInspector.cs       — SingletonLock (symlink host-pid) + SingletonLockReader
│   │   ├── CdpPortDiscovery.cs           — skan /proc/*/cmdline, IProcessCmdlineReader
│   │   ├── CdpChecker.cs                 — sonda HTTP /json/version na 127.0.0.1
│   │   ├── StaleBrowserKiller.cs         — kill po pidzie + sprzątanie plików Singleton
│   │   ├── VivaldiLauncher.cs            — launcher + CdpWaiter (wspólne polling z fail-fast)
│   │   ├── ChromeLauncher.cs             — launcher (implementuje IVivaldiLauncher)
│   │   ├── PortAllocator.cs
│   │   ├── BrowserConnector.cs           — Playwright ConnectOverCDP
│   │   ├── BrowserSession.cs             — wrapper obiektów Playwright
│   │   └── BrowserSessionProvider.cs     — sesja + mutex + reconnect/relogin
│   ├── Config/
│   │   ├── BaseConfig.cs         — BaseConfig, AppConfig (wrapper), LinksConfig, MailConfig,
│   │   │                           TransferConsumerConfig, VoucherConsumerConfig
│   │   ├── ConfigLoader.cs       — ładowanie + walidacja
│   │   ├── DefaultConstants.cs   — wszystkie wartości domyślne
│   │   └── PathResolver.cs       — rozwiązywanie ścieżek relatywnych/absolutnych
│   ├── Email/
│   │   ├── EmailService.cs       — IEmailService, EmailService, VoucherEmail
│   │   └── VoucherCodeExtractor.cs
│   ├── Infrastructure/
│   │   ├── DataSizeConverter.cs  — ParseToBytes / FormatBytes
│   │   ├── LogFileHelper.cs      — nazwa pliku logu, retencja
│   │   ├── SingleInstanceGuard.cs
│   │   ├── TimeService.cs        — zwraca DateTime.Now (ścieżka internetowa wyłączona)
│   │   └── SessionPageSaver.cs   — wyłączony (nieużywany)
│   ├── Login/
│   │   ├── LoginResult.cs        — record LoginResult(bool Success, string FinalUrl)
│   │   └── LoginService.cs
│   ├── NoPremium/
│   │   ├── NoPremiumBrowserClient.cs
│   │   └── TransferInfo.cs       — record TransferInfo(TotalBytes, PremiumBytes, ExtraBytes)
│   └── Services/
│       ├── KeepaliveService.cs
│       ├── ScheduleHelper.cs
│       ├── ConsumerActivityState.cs
│       ├── TransferConsumerService.cs
│       └── VoucherConsumerService.cs
└── NoPremium2.Tests/            — Browser/ Config/ Infrastructure/ Login/ NoPremium/ Services/
```

---

## 5. Moduły — odpowiedzialności

### Config
`BaseConfig` to rekord konfiguracji użytkownika (deserializowany wprost z JSON),
opakowany przez `AppConfig { BaseConfig, LinksConfig, MailConfig, LogDir }`.
`ConfigLoader.LoadConfig` robi wszystko w jednym kroku: ładuje config i plik
linków, waliduje wpisy, sprawdza nienakładanie się okien consumerów, parsuje
`EmailImapServer` ("host:port") do `MailConfig`, rozwiązuje katalog logów.
Błędy → `Environment.Exit(1)` z `[STARTUP ERROR] ...`. `DefaultConstants` to
jedyne miejsce z wartościami domyślnymi; `ApplyDefaults` uzupełnia puste/zerowe
pola. `PathResolver` rozwiązuje ścieżki względne wobec katalogu configu i binarki
(znaleziona w obu → błąd „ambiguous”).

### Browser
`BrowserManager.GetOrLaunchAsync` woła `ExistingBrowserResolver.ResolveAsync`,
który szuka działającej przeglądarki dla *naszego* profilu w kolejności:
1. `DevToolsActivePort` (plik pisany przez Chromium; linia 1 = port) →
   weryfikacja sondą HTTP na `127.0.0.1`;
2. skan `/proc/*/cmdline` **zawężony do `--user-data-dir` == nasz profil**
   (`CdpPortDiscovery`) — żeby nie przejąć prywatnej przeglądarki użytkownika;
3. `SingletonLock` (`ProfileLockInspector`) — symlink `host-pid`; weryfikuje
   `/proc/<pid>`, hostname i zgodność profilu w cmdline (ochrona przed reużyciem PID).

Wynik: `Found(port)` → podłącz; `NotRunning` → uruchom nową
(`PortAllocator` → `IVivaldiLauncher.Launch` → `WaitForCdpAsync`);
`RunningButUnreachable` → `InvalidOperationException` z instrukcją, albo
`StaleBrowserKiller.Kill` + relaunch gdy `KillStaleBrowser: true`.

`BrowserSessionProvider` trzyma jedną sesję, serializuje dostęp muteksem i przy
martwej sesji robi reconnect + ponowne logowanie. Przeglądarka **zostaje
uruchomiona po zamknięciu aplikacji** (nigdy nie jest zabijana przy shutdown).

### Login
`LoginService.LoginAsync` nawiguje na stronę logowania (jeśli trzeba) i czeka —
maksymalnie `TurnstileTimeoutMs` (120 s) — na *jedno z dwojga*: pojawienie się
formularza albo ręczne zalogowanie użytkownika (URL opuszcza `/login`). Ten
wyścig jest konieczny, bo użytkownik może zalogować się szybciej, niż aplikacja
zdąży wypełnić formularz. Po pojawieniu się formularza: wypełnia, submituje,
czeka na opuszczenie `/login`.

### NoPremium
`NoPremiumBrowserClient` — operacje na stronie:
- `ReadTransferInfoAsync` — parsuje nagłówek `#signed` (dwa regexy) → `TransferInfo`.
- `AddLinksToQueueAsync` — wkleja URL-e na `/files`, dwuetapowe czekanie na DOM,
  klika „Dodaj zaznaczone”.
- `RemoveCompletedLinksAsync` — usuwa z kolejki wpisy „Zakończono” przed
  kolejkowaniem (serwis odrzuca duplikaty); obsługuje natywny `confirm` i modal jQuery.
- `ConsumeVoucherAsync` — realizuje kod na `/voucher`, interpretuje odpowiedź →
  `VoucherResult` (`Success` / `InvalidCode` / `AlreadyUsed` / `Expired` /
  `CaptchaDetected` / `UnknownResponse`).

### Email
`EmailService` (MailKit): nowy `ImapClient` na każdą operację (connect + auth +
disconnect — brak trwałego połączenia), SSL zawsze, certyfikaty akceptowane bez
weryfikacji. `GetUnreadVouchersAsync` szuka nieprzeczytanych, wyciąga kod przez
`VoucherCodeExtractor` (regex na treści). `MarkAsSeenAsync` ustawia flagę Seen.

### Services
Wszystkie dziedziczą `BackgroundService`. Consumery liczą czas do następnego
biegu przez `ScheduleHelper.TimeUntilNextRun` (okno `[start,end]`, pierwszy bieg
natychmiast, potem co interwał, poza oknem do jutra, obsługa przekraczania
północy). `ScheduleHelper.SchedulesOverlap` pilnuje przy starcie, że okna obu
consumerów są rozłączne. `ConsumerActivityState` to interlocked-licznik: gdy
consumer działa, `KeepaliveService` pomija swój tick. Wyjątek w pętli serwisu
jest logowany, po czym następuje pauza ~1 min; anulowanie kończy pętlę.

- `TransferConsumerService` — usuwa ukończone wpisy, potem kolejkuje linki
  pojedynczo, sprawdzając transfer premium przed każdym; pomija linki większe od
  nadwyżki nad `ReserveTransferBytes`; `_queuedToday` blokuje podwójne
  kolejkowanie w dobie.
- `VoucherConsumerService` — czyta maile, realizuje kody, oznacza jako
  przeczytane wg wyniku; CAPTCHA → przerywa iterację bez oznaczania.
- `KeepaliveService` — pełny re-login co `KeepaliveInterval`.

### Infrastructure
`DataSizeConverter` — parsowanie/formatowanie rozmiarów, separator dziesiętny
zawsze kropka (`InvariantCulture`). `LogFileHelper` — `logs_RRRRMMDD.NN.log` z
`NN` = max istniejący + 1, retencja 30 dni. `SingleInstanceGuard` — blokada PID
z weryfikacją, czy proces żyje. `TimeService` — obecnie zwraca `DateTime.Now`
(ścieżka pobierania czasu z sieci jest w kodzie, ale wyłączona).

---

## 6. Decyzje projektowe i wnioski

### Architektura
- **Jedna aplikacja zamiast dwóch** (transfer + voucher) — współdzielona sesja
  przeglądarki, więc Cloudflare przechodzi się tylko raz.
- **Playwright CDP zamiast HTTP** — Turnstile wymaga prawdziwej przeglądarki.
  Aplikacja nie otwiera/zamyka przeglądarki per operacja.
- **Jedna karta + `SemaphoreSlim(1,1)`** — serwisy nie wchodzą sobie w drogę.
- **Auto reconnect + relogin** — `EnsureSessionAsync` sprawdza
  `browser.IsConnected && !page.IsClosed` przy każdym wywołaniu.

### Przeglądarka
- **`Ctrl+C` nie zamyka przeglądarki** — launcher startuje ją przez `setsid`,
  więc trafia do nowej sesji, poza foreground process group terminala (SIGINT jej
  nie dosięga). P/Invoke `setpgid` próbowano i porzucono (dziecko zdążyło
  `exec()` zanim rodzic zdążył wywołać).
- **Przeglądarka zostaje po zamknięciu aplikacji** — bezwarunkowo. Umożliwia to
  ponowne podłączenie przy następnym starcie.
- **Wykrywanie po `/proc/*/cmdline`, nie po nazwie procesu** — `/usr/bin/vivaldi`
  to skrypt, który `exec`uje `vivaldi-bin`; szukamy `--remote-debugging-port=`
  bezpośrednio w cmdline.
- **Kolejność wykrywania**: `DevToolsActivePort` → skan `/proc` zawężony do
  profilu → `SingletonLock`. Skan zawężony do `--user-data-dir`, żeby nie przejąć
  prywatnej przeglądarki użytkownika.
- **`127.0.0.1`, nie `localhost`** — Chromium binduje port CDP tylko na IPv4;
  `localhost` bywa rozwiązywany do `::1` (zależnie od `/etc/hosts`).
- **Fail-fast na „handoff”** — gdy druga instancja przeglądarki na tym samym
  profilu przejmie uruchomienie (ProcessSingleton), `WaitForCdpAsync` wykrywa to
  (inny port w `DevToolsActivePort` albo proces zniknął) i zawodzi od razu,
  zamiast czekać pełny timeout.
- **Drain stdout/stderr uruchomionej przeglądarki** — nieczytany pipe zakleszcza
  Chromium po zapełnieniu ~64 KB bufora (przy okazji wycisza szum GCM).
- **Jedna karta przy starcie** — bez `startUrl` w linii poleceń (otwierał drugą
  kartę); `LoginService` nawiguje jawnie.
- **`KillStaleBrowser`** (config, domyślnie `false`) — gdy przeglądarka trzyma
  profil, ale CDP jest nieosiągalny: `false` = przerwij z instrukcją, `true` =
  ubij i uruchom nową.
- **`AppSettings.ProfileDir` jest per-przeglądarka** (`chrome-nopremium` /
  `vivaldi-nopremium`), wybierany w `Program.cs` — czytniki portu/locka i launcher
  patrzą w ten sam katalog.

### Logi
- **Nowy plik na każde uruchomienie**, nie na dzień — numer = max istniejący + 1
  (nigdy nie wypełnia luk po skasowanych plikach).
- **`LogFileDir`**: pusty → `Logs/` obok binarki; względny → względem binarki;
  absolutny → wprost. Nieutworzenie katalogu → błąd startu. Pierwsza linia logu
  podaje użyty katalog.
- **Separator dziesiętny zawsze kropka** (`InvariantCulture`) — bez tego polski
  locale wyświetlałby „10,23GB”, a `2,34` byłoby parsowane jako `234`.

### Integracja z nopremium.pl `/files`
- **Dwuetapowe czekanie na DOM** po submit: `#insertPanel` znika (synchronicznie)
  → `#progressPanel` znika (po odpowiedzi AJAX). `NetworkIdle` się ściga —
  przetworzenie jednego linku potrafi trwać < 400 ms.
- **Deduplikacja**: serwis odrzuca powtórzone linki, więc wpisy „Zakończono”
  trzeba usunąć z kolejki przed ponownym dodaniem (`RemoveCompletedLinksAsync`,
  przed każdym biegiem `TransferConsumerService`).
- **Dopasowanie nazw**: strona wstawia `<br>` między słowami; `innerText` zamienia
  je na `\n`. Zamieniamy `\n` na spację (nie usuwamy), zwijamy białe znaki i
  porównujemy z nazwą z configu jako `Contains` bez rozróżniania wielkości liter.
- **Potwierdzenie usunięcia** obsługuje natywny `window.confirm` i modal jQuery
  `.blocker.current` (przycisk `Tak|Usuń|OK|Potwierdź`).

### Email
- **Brak cache sesji IMAP** — nowy `ImapClient` na operację. Prostsze zarządzanie
  połączeniem kosztem niewielkiego narzutu.
- **Certyfikaty IMAP bez weryfikacji** — elastyczność wobec różnych dostawców.

### Voucher
- **CAPTCHA na `/voucher`** → serwis przerywa iterację; maile nie są oznaczane
  jako przeczytane (ponowna próba następnym razem).

### Shutdown
- **Podwójny dispose** — `host.RunAsync()` sam disposuje kontener DI (i
  `BrowserSessionProvider`), więc `Program.cs` nie woła `DisposeAsync` jawnie;
  `Interlocked.Exchange(ref _session, null)` zabezpiecza idempotencję.
- **`Ctrl+C` bez `ERROR`** — `PlaywrightException` przy anulowaniu jest
  konwertowany na `OperationCanceledException`; pętle serwisów `break`ują na
  anulowaniu wewnątrz ogólnego `catch (Exception)`.

---

## 7. Testy

Stack: **xUnit + NSubstitute + AwesomeAssertions**. `[InternalsVisibleTo]` daje
testom dostęp do metod `internal`. Logika czysta (parsery rozmiarów, harmonogram,
`ParsePort` / `ParseUserDataDir`, `DevToolsActivePort` / `SingletonLock`, ścieżki)
jest `public static` i pokryta bezpośrednio. ~208 testów, `net10.0`.

Katalogi: `Browser/`, `Config/`, `Infrastructure/`, `Login/`, `NoPremium/`,
`Services/` (+ `AppSettingsTests.cs`) — układ lustrzany do projektu głównego.
Mocki interfejsów Playwright (`IPage`, `ILocator`, …) przez NSubstitute; testy
`LoginService` używają licznika wywołań do rozróżnienia dwóch `WaitForURLAsync`
w wyścigu Turnstile.

---

## 8. Polecenia developerskie

```bash
cd src

dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~LoginServiceTests"
dotnet run --project NoPremium2/NoPremium2.csproj -- config.json
```

Wersje pakietów NuGet — patrz `NoPremium2/NoPremium2.csproj` i
`NoPremium2.Tests/NoPremium2.Tests.csproj`.

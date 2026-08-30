# NoPremium2 — Wymagania

Surowe wymagania aplikacji. Architektura i decyzje projektowe: `instructions.md`.

---

## 1. Kontekst i cel

**www.nopremium.pl** to agregator kont premium serwisów hostingowych. Subskrybent
dostaje codziennie o północy pulę **transferu premium** (bufor max ~25 GB, nie
kumuluje się — nadwyżka przepada). Za każde zużyte 20 GB serwis wysyła mailem
**voucher na 2 GB transferu dodatkowego** (ważny 7 dni), realizowany na stronie
`/voucher`; transfer dodatkowy kumuluje się w osobnej puli.

Cel aplikacji: co wieczór automatycznie konsumować transfer premium (kolejkując
pliki do pobrania na `/files`), żeby nie przepadał, oraz realizować vouchery z
maila — tak by gromadzić transfer dodatkowy i nie ruszać go na bieżąco.

Serwis chroni logowanie przez **Cloudflare Turnstile**, więc aplikacja steruje
prawdziwą przeglądarką (Chrome / Vivaldi) przez Playwright CDP zamiast surowych
żądań HTTP.

---

## 2. Wymagania funkcjonalne

### FR-1 — Uruchomienie i walidacja startowa
- Wywołanie: `NoPremium2 <ścieżka-do-config.json>`.
- Aplikacja kończy się kodem `1` z czytelnym błędem na stderr, gdy:
  - brak argumentu CLI,
  - plik konfiguracyjny nie istnieje / nie da się odczytać / sparsować,
  - brak wymaganych pól w konfiguracji,
  - plik z linkami nie istnieje / nie da się odczytać / sparsować / jest pusty,
  - wpis linku jest nieprawidłowy (brak URL, brak `Size`, nieczytelny `Size`),
  - okna czasowe obu consumerów się nakładają (zob. FR-10),
  - nie znaleziono ani Chrome, ani Vivaldi,
  - katalogu logów nie da się utworzyć,
  - działa już inna instancja aplikacji (blokada PID),
  - startowy test logowania do nopremium.pl nie powiódł się,
  - startowy test połączenia IMAP nie powiódł się.
- Po pomyślnym starcie: loguje efektywną konfigurację (hasła zamaskowane `***`),
  uruchamia serwisy tła, działa do `Ctrl+C`.

### FR-2 — Plik konfiguracyjny (JSON)
- Pola wymagane: `NoPremiumUsername`, `NoPremiumPassword`, `EmailUsername`,
  `EmailPassword`, `EmailImapServer` (format `"host:port"`), `LinksFilePath`.
- Pola opcjonalne (przy pustym stringu / `null` / `0` → wartość domyślna, bez
  crasha): `TransferConsumer`, `VoucherConsumer`, `KeepaliveInterval`,
  `LogFileDir`, `KillStaleBrowser`.
- Parser JSON toleruje komentarze i przecinki końcowe; nazwy pól bez
  rozróżniania wielkości liter.
- Poświadczenia **wyłącznie z pliku JSON** — bez zmiennych środowiskowych.

### FR-3 — Plik z linkami (JSON)
- Kształt: `{ "Links": [ { "Name": "...", "Url": "https://...", "Size": "512MB" } ] }`.
- `Size`: liczba + jednostka (`B`, `KB`, `MB`, `GB`, `TB`), obsługuje ułamki
  (`"3.5GB"`). Walidowane przy starcie.
- Ścieżka pliku może być absolutna lub relatywna. Relatywna szukana w katalogu
  `config.json` oraz w katalogu binarki; znaleziona w obu (różne pliki) → błąd
  „ambiguous” (wymagana ścieżka absolutna); nieznaleziona → błąd.

### FR-4 — Logowanie do nopremium.pl (Cloudflare Turnstile)
- Nawigacja do strony logowania, jeśli aktualny URL jej nie dotyczy.
- Jeśli serwer przekieruje poza `/login` → uznane za „już zalogowany”.
- W przeciwnym razie: czekaj **do 120 s** na jedno z: pojawienie się formularza
  logowania (po rozwiązaniu Turnstile przez człowieka) **albo** ręczne
  zalogowanie się użytkownika (URL opuszcza `/login`).
- Gdy pojawi się formularz: wpisz login i hasło, kliknij submit, poczekaj (do
  30 s) aż URL opuści `/login`. Wynik: sukces/porażka + końcowy URL.

### FR-5 — Konsument transferu (TransferConsumerService)
- Działa wg harmonogramu (FR-10).
- Przed kolejkowaniem: usuwa z kolejki `/files` wpisy ze statusem „Zakończono”
  (dopasowane do nazw linków z konfiguracji) — serwis odrzuca duplikaty.
- Kolejkuje kolejne linki, dopóki `transfer premium > ReserveTransferBytes`;
  pomija link, którego rozmiar przekracza dostępną nadwyżkę, i próbuje następny.
- Nie kolejkuje tego samego linku dwa razy w ciągu tej samej doby.

### FR-6 — Konsument voucherów (VoucherConsumerService)
- Działa wg harmonogramu (FR-10).
- Czyta nieprzeczytane maile przez IMAP, wyciąga kod doładowujący, realizuje go
  na `/voucher`.
- Oznacza mail jako przeczytany dla wyników: sukces, kod nieistniejący, kod już
  wykorzystany, kod wygasły.
- Wynik „nieznana odpowiedź” → nie oznacza (ponowna próba w kolejnej iteracji).
- Wykrycie CAPTCHA na stronie vouchera → przerywa przetwarzanie tej iteracji,
  nie oznacza żadnego maila.

### FR-7 — Keepalive (KeepaliveService)
- Co `KeepaliveInterval` (domyślnie 1 h) wykonuje pełne ponowne logowanie, aby
  podtrzymać sesję przeglądarki / Cloudflare.
- Pomija swój tick, gdy w danej chwili działa któryś z consumerów.
- Błąd keepalive jest logowany, nie zatrzymuje serwisu.

### FR-8 — Obsługa przeglądarek
- Priorytet: **Chrome > Vivaldi**. Wykrywanie przez sprawdzenie istnienia pliku
  wykonywalnego pod znanymi ścieżkami.
- Osobne, odizolowane profile: `~/.config/chrome-nopremium`,
  `~/.config/vivaldi-nopremium`.
- Przeglądarka jest utrzymywana otwarta przez cały czas działania aplikacji i
  **pozostaje uruchomiona po jej zamknięciu**.

### FR-9 — Ponowne podłączenie do działającej przeglądarki
- Przy starcie, zanim aplikacja cokolwiek uruchomi, wykrywa działającą
  przeglądarkę dla swojego profilu w kolejności:
  1. plik `DevToolsActivePort` w profilu → weryfikacja portu sondą HTTP,
  2. skan procesów `/proc` **zawężony do `--user-data-dir` == nasz profil**
     (nie wolno przejąć prywatnej przeglądarki użytkownika),
  3. `SingletonLock` (właściciel profilu żyje, ale bez dostępnego portu CDP).
- Reakcja:
  - port odpowiada → podłącz się do istniejącej przeglądarki,
  - nic nie działa → uruchom nową,
  - przeglądarka trzyma profil, ale port CDP nieosiągalny → przerwij z czytelną
    instrukcją; albo (gdy `KillStaleBrowser: true`) ubij ją i uruchom nową.
- Uruchamianie nowej przeglądarki nie może czekać pełnego timeoutu, gdy nastąpił
  „handoff” do już działającej instancji — musi zawieść szybko z komunikatem.

### FR-10 — Harmonogramy serwisów
- Oba consumery mają okno `[StartTime, EndTime]` i `IntervalMinutes`.
- W oknie: pierwszy bieg natychmiast po wejściu w okno, kolejne co
  `IntervalMinutes`.
- Poza oknem: czekaj do `StartTime` następnego dnia.
- Okno może przekraczać północ (np. `23:00–00:30`).
- **Okna czasowe TransferConsumer i VoucherConsumer nie mogą się nakładać** —
  nakładanie kończy start błędem (kod `1`).
  > Uwaga: domyślne wartości obu consumerów są identyczne, więc uruchomienie z
  > domyślną konfiguracją wymaga ręcznego ustawienia rozłącznych okien.

### FR-11 — Logowanie (logi)
- Serilog do konsoli **i** do pliku, identyczny format z pełnym znacznikiem
  czasu, poziomem i źródłem.
- **Nowy plik logu na każde uruchomienie**: `logs_RRRRMMDD.NN.log`, gdzie `NN` =
  (najwyższy istniejący numer z danego dnia) + 1.
- Retencja: pliki starsze niż 30 dni są usuwane przy starcie.
- Katalog logów konfigurowalny (`LogFileDir`): pusty → `Logs/` obok binarki;
  ścieżka względna → względem katalogu binarki; absolutna → wprost. Katalog jest
  tworzony przy starcie.
- Pierwsza linia logu podaje użyty katalog logów. Hasła w logach zamaskowane.

---

## 3. Wymagania niefunkcjonalne

- **Niezawodność sesji**: wykrycie martwej sesji przeglądarki (rozłączony CDP lub
  zamknięta karta) → automatyczne ponowne podłączenie i ponowne zalogowanie.
- **Odporność serwisów tła**: wyjątek w pętli serwisu jest logowany i nie kończy
  aplikacji — ponowna próba po ~1 min. Anulowanie (`Ctrl+C`) kończy pętlę czysto.
- **Graceful shutdown**: `Ctrl+C` nie generuje logów `ERROR` — wyjątki Playwright
  powstałe przy zamykaniu przeglądarki są traktowane jak anulowanie.
- **Pojedyncza instancja**: równoległe uruchomienie drugiej instancji jest
  blokowane plikiem PID.
- **Współbieżność**: wszystkie serwisy współdzielą jedną kartę przeglądarki;
  dostęp serializowany muteksem (jedna operacja na raz).
- **Locale-niezależność**: separator dziesiętny w parsowaniu i formatowaniu
  rozmiarów to zawsze kropka, niezależnie od ustawień systemu.

---

## 4. Wymagania wydajnościowe / czasowe

| Parametr | Wartość |
|---|---|
| Oczekiwanie na formularz po Turnstile | 120 s |
| Timeout gotowości CDP po starcie przeglądarki | 10 s (+ szybkie przerwanie przy „handoff”) |
| Startowy zestaw testów (login + IMAP) | 3 min |
| Sonda HTTP portu CDP | 1,5 s / próba |
| Sonda `DevToolsActivePort` | 5 prób co 300 ms |
| Timeout `HttpClient` | 5 s |
| Domyślny interwał keepalive | 1 h |
| Domyślny interwał consumerów | 5 min |

---

## 5. Środowisko i zależności zewnętrzne

- System: Linux.
- Zainstalowany Google Chrome / Chromium **lub** Vivaldi.
- Runtime: .NET 10.
- Serwer poczty IMAP z SSL. Certyfikaty serwera IMAP są akceptowane bez
  weryfikacji (elastyczność wobec różnych dostawców).
- Źródło czasu: zegar systemowy.

---

## 6. Ograniczenia i założenia

- CAPTCHA Cloudflare rozwiązuje **człowiek** ręcznie w oknie przeglądarki —
  aplikacja tylko na to czeka.
- Jedna sesja przeglądarki na cały czas działania — aplikacja nie otwiera i nie
  zamyka przeglądarki dla pojedynczych operacji.
- Automatyzacja opiera się na selektorach HTML strony nopremium.pl (`/files`,
  `/voucher`, nagłówek z transferem) — zmiana strony może wymagać aktualizacji
  selektorów.

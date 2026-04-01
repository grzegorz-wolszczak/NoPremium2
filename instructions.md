# Kontekst

Strona www.nopremium.pl oferuję usługę sprzedaży transferu zciągania z serwisów hostingowych. 
Jest to strona która 'agreguje' konta premium różnych serwisów hostingowych takich jak filefactory, mediafire, fastshare, wrzucaj.pl itd..
Dzięki stronie www.nopremium.pl uzytkownik może pobierać pliki z tych serwisów nie mając w nich wykupionych kont, bo strona nopremium stanowi niejako proxy do tych stron.
Strona nopremium.pl posiada różne payment plany dla swoch użytkoników. Można kupić transfer w Gigabajtach jednorazowo, ale można też kupić transfer w postaci subskrypcji. Ten właśnie sposób mam wykupiony ja.
Transfer w subskrypcji polega na tym że codziennie, strona nopremium daje użytknownikom możliwość zciągnięcia 20 GB. Te 20 GB zostaje 'dodane' tuż z nowym dniem (o pólnocy.) Ilość dostępnego transferu wyświetla się w panelu na górze. 
Strona pokazuje to w formacie: Pozostały transefer: XXX GB (w tym YYY GB transferu Premium + ZZZ GB transferu dodatkowego).
Tak więc transfer jest podzielnoy na dwie grupy , transfer premium i transfer dodatkowy.
Ten tranfer dodawany codziennie dolicza sie do transferu premium, ztym że maxymalnie może go być 25GB. To znaczy że jeżeli użytkonik dziennie nie wykorzysta swojego 20 GB to zostanie od 'zmarnowany' bo maxymalnie dziennie w transferze premium (jakby buforze) może być max 25GB.
Za każde wykorzystane (zkonsumowane) 20GB transferu na stronie, strona wysyła na maila ze specjalnym voucherem (przypadkowy ciag liter i znaków) na 2GB tzw dodatkowego tranferu. Ten kod można wykorzystać jako tak zwany kod doładowujący. W tym celu wchodzi się na podstronę  https://www.nopremium.pl/voucher i tam w specjalne pole wpisuje się kod vouchera , klika przycisk Doładuj i wtedy 2GB jest dodawane do tzw transferu dodatkowego. Transfer dodatkowy sie kumuluje za każdym razem gdy konsumujemy voucher.

Dlatego właśnie gdy strona na górze w panelu wyświetla ilość danych do zciągania to wyświetla to w formacie np:
Pozostały transfer: 2100.18 GB (w tym 25 GB transferu Premium + 2075.18 GB transferu dodatkowego)

To oznacza że gdyby użytkownik chciał zciągnąć jakieś pliki to w danym momencie czasu mógłby zciągnąć 2100.18 GB. Gdy pliki są zciągane najperw transfer jest uszczuplany z transferu premium (tego co się odanawia codzienie), gdy tego zabraknie, dalszy jest zciągany z tranferu dodatkowego.

Ponieważ strona codziennie dodaje 20GB nowego transferu premium, chcemy ten transfer za każdym razem konsumować, tak by nie osiągał oni nigdy 25GB a jednocześnie , nigdy nie chcemy używac transferu dodatkowego bo jego własnie chcemy gromadzić na zapas , konsumując vouchery.

# Jak to działało kiedyś i co się zmieniło.
W celu automatycznej konsumpcji transferu i gromadzeniu transferu dodatkowego poprzez uzyskiwanie voucherów i ich konsumowanie, zostały stworzone dwie aplikacje. Jedna transfer-consumer a druga voucher-consumer.

## Aplikacja transfer-consumer (transfer konsumer)
transfer-consumer to aplikacja konsolowa która była odpalana w regularnych odstępach. Jednorazowe odpalenie aplikacji polegało na tym ze logowała sie do nopremium.pl , sprawdzała czy jest transfer premium do skonsumowania (, jezeli nie to się wyłączała.) Następnie z podanej listy linków do pobrania (na youtube) - dodawała do konsumowania te linki na stronie nopremium , monitorujac za każdym razem czy przypadkiem nie zejdzie z transferem premium do zera i nie zacznie konsumowan transferu dodatwkoego co by było nieprawidłowe. Po jednorazowym przebiegu (próbie konsumpcji wszystkich linków z listy) aplikacja sie konczyla. 

### Model uruchomienia
Ta apliakacja była uruchamiana w cron jobie , w godzinach od 23:00 do 00:00 kilka razy, tak żeby pod koniec dnia skonsumowac to co zostało z transferu premium (zachwując pewną reszkę np 1~2 GB) i żeby nowy dzień zaczął się z doładowaniem 20 GB i dodaniem do transferu premium.. Aplikacja uruchamiała sie pod koniec dnia dlatego że w trakcie dnia mogła zajść potrzeba zciągnięcia jakichś plików i wtedy do tego był własnie uzywany transfer premium (jeżeli był).

## Aplikacja voucher-consumer.
Gdy strona nopremium.pl zarejestowała, że użytkownik wykorzystał 20GB poprzez żciaganie plików przez strone nopremium.pl to generaowała voucher z doładowaniem transferu dodatkowego na 2GB. Nastepnie wysyłała ten voucher na adres email ktory jest ustawiony na koncie użytkownika nopremium.pl. Uwaga! Vouchery mają czas przydatności przez 7 dni. Potem expirują
Voucher consumer to aplikacja która stanowiła uzupełnienie tranfer-konsumera. Do konsumpcji tych voucherów została stworzona właśnie ta aplikacja. Jej działanie polegało na:
1. Logowaniu się konto emailowe i sprawdzeniu, czy nie przyszły nowe emaile z kodami voucherowymi od nopremium.pl (jeżeli nie to się kończyła)
2. Następnie pobierała wszystkie kody voucher z maili i logowała sie na stronie www.nopremium.pl i tam konsumowała te vouchery. Po każdej konsumpcji vouchera  - odznaczała wiadomośc mailową jako przeczytaną na koncie mailowym żeby już potrakotwać ten voucher jako skonsumowany.

### Model uruchomienia
Aplikacja voucher konsumer była uruchamiana raz dzienie mniej więcej o 4 rano. Godzina nie była bardzo ważna gdyż ona miała zapewnić tylko że wszystkie vouchery z maila zostaną skonsumowane. Mogła by uruchamniać się rzadziej ale nie maxymalnie co 7 dni bo po 7 dniach vouchery traciłby ważność. Dla jasności jednak pozostaniemy przy uruchomieniu jednorazowo codziennie. 

# Dlaczego porzebna jest nowa aplikacja działająca inaczej.
Dotychczas logowanie do nopremium.pl odbywało sie w prosty sposób. Używany był HTTP Client (lub jakaś biblioteka c# e.g. Flug, RestAssured , etc) i dzięki przesyłaniu prostych requestów REST-owych można było poradzić sobie z logiką programu.
Aplikacja była uruchamiana wiele razy (cron) i nie stanowiło to problemlu ponieważ używanie REST interefejsu było stabilne. Ale od pewnego czasu strona nopremium.pl wprowadziła cloudflare i nie można się już łatwo logować automatycznie. To badzo utrudnia życie legalnym użytkownikom strony nopremium.pl ponieważ nie mogą oni gromadzić sobie w sposób automtyczny transferu dodatkowego - obiecanego przecież w regulaminie podczas zakupu subskrypcji - tylko muszą oni teraz osobiście, codzinnie nie zapomnieć zuzyć 20 GB żeby dostać vouchery. Dlatego też musi powstać nowa aplikacja która przywróci mechanizm automatycznego zbierania transferu dodatkowego bez udziału ludzkiej interwencji. 

# Nowy model działania aplikacji (NoPremium2)

## Kod starych aplikacji z podstawową logiką 
Poprzednie aplikacje voucher-consumer i transfer-consumer są zlokalizowane w folderze old-code.
Należy się z nimi zapoznać w celu zrozulmienia jak działy mechanizmy konsumpcji transferu i voucherów.
W bazowych założeniach ta logika będzie identyczna (szczególnie voucher-konsumer) i dużą część tego kodu może wykorzystać ponownie.

## Zakres zmian w działaniu względem starego rozwiązania.
Nowe aplikacja nie będzie już podzielona na dwie osobne, ale będzie jedną którą będzie realizowała te dwa zadania.
Założeniem jest że aplikacja będzie działa non stop - na jakieś maszynie wirtualnej z system linux. Dzięki temu aplikacja tylko na początku będzie musiała sie logować - i poradzić sobie z cloudflare - a potem 'sesja' przeglądarki będzie już 'podtrzymywana' przez aplikację.
Aplikacja będzie miała dwa background/hosted serwisy , jeden do obsługi funkcjonalności tranfer-konsumer a drugi voucher-consumer.
Po uruchomieniu aplikacja uruchomi więc przeglądarkę i utrzymując ją cały czas otwartą będzie wykonywała zadania transfer-consumer i voucher-consumer


## Wymagania

### Wymagania funkcjonalne
- Aplikacja musi udostępniać możliwość podania pliku konfiguracyjnego jako parametr wejściowy (format pliku: json). Tam powinny być możliwe do podania następujące wartości konfiguracyjne.

    **Wymagane:**
    - Email user account (login do konta email)
    - Email user password (hasło do konta email)
    - Email IMAP server — adres serwera IMAP wraz z portem, np. `"imap.gmx.com:993"` (ze starego kodu: `imap.gmx.com`, port `993`, SSL)
    - Nopremium user account
    - Nopremium user password
    - Ścieżka do pliku z listą linków do konsumpcji transferu (plik JSON, format opisany niżej). Jeżeli plik nie istnieje lub nie da się go odczytać — aplikacja ma nie wystartować i podać czytelny błąd.

    **Opcjonalne — voucher-consumer:**
    - Godzina startowa od której voucher-consumer ma zacząć swoje działanie (np. `"23:00"`) — default: `"23:00"`
    - Godzina końcowa o której voucher-consumer ma zakończyć swoje działanie (np. `"23:55"`) — default: `"23:55"`
    - Interwał (w minutach) jaki ma dzielić poszczególne uruchomienia voucher-consumera (np. `5`) — default: `5`

    **Opcjonalne — transfer-consumer:**
    - Godzina startowa od której transfer-consumer ma zacząć swoje działanie (np. `"23:00"`) — default: `"23:00"`
    - Godzina końcowa o której transfer-consumer ma zakończyć swoje działanie (np. `"23:55"`) — default: `"23:55"`
    - Interwał (w minutach) jaki ma dzielić poszczególne uruchomienia transfer-consumera (np. `5`) — default: `5`
    - Minimalna rezerwa transferu premium w bajtach — ilość bajtów transferu premium jaka ma zostać zachowana (nie skonsumowana). Default: `3221225472` (3 GB). Jednostka: bajty (tak jak w starym kodzie).

    **Opcjonalne — ogólne:**
    - Interwał keepalive — jak często aplikacja ma robić "jałowe" przejście po stronie nopremium.pl w celu podtrzymania sesji. Format: `"HH:mm:ss"`, np. `"01:00:00"`. Default: `"01:00:00"`.

    **Zasady obsługi wartości opcjonalnych:**
    - Plik konfiguracyjny powinien zawierać wszystkie opcjonalne pola wypełnione wartościami domyślnymi (aby użytkownik wiedział jakie są dostępne opcje).
    - Jeżeli użytkownik usunie wpis z pliku lub ustawi wartość na pusty string `""` lub `null` — aplikacja ma to obsłużyć gracefully (nie crashować) i użyć zakodowanej na stałe wartości domyślnej.
    - Jeżeli jakaś wartość **wymagana** nie zostanie podana — program ma się zakończyć informując o wszystkich brakujących polach (zbiorczo).

- Musi być możliwość przerwania działania aplikacji po naciśnięciu Ctrl+C.
- Aplikacja ma supportować przeglądarki Chrome oraz Vivaldi. Na starcie musi wykryć która z nich jest zainstalowana w systemie. Jeżeli żadna — aplikacja ma się zakończyć z błędem. Jeżeli są obie — priorytet ma Chrome, potem Vivaldi.

### Format pliku z linkami do konsumpcji transferu

Plik JSON zawierający tablicę linków, podobny do starego `nopremium.config.json` z transfer-consumera. Format:
```json
{
  "Links": [
    {
      "Name": "Nazwa pliku",
      "Url": "https://...",
      "Size": "512MB"
    }
  ]
}
```
Pole `Size` podawane jako string z jednostką (MB, GB) — tak jak w starym kodzie. Aplikacja nie startuje jeśli plik nie istnieje lub nie da się go sparsować.

### Start aplikacji
Aplikacja powinna nie wystartowąć t.j zgłosić błąd przy starcie na std output i wyjsć z kodem błędu 1 jezeli:
- jeżeli plik konfiguracyjny nie istnieje albo nie da sie odczytać lub sparsować.
- brakuje poświadczeń do zalogowania się do nopremium - czy to w zmiennych środowiskowych czy to w jakimś pliku konfiguracyjnym
- brakuje poświadczeń do zalogowania sie do konta email - czy to zmiennych środkowiskowych czy to w jakimś pliku konfiguracyjnym
- w systemie jest już uruchomiona inna instancja tej aplikacji - wtedy w logu podać PID tego procesu i jeżeli to możliwe date jego utworzenia.
- Przy starcie aplikacji , musi zostać pierwsze zalogowanie się do nopremium.pl i testowe zalogowanie się do email account. Jeżeli cokolwiek sie nie uda to aplikacja ma przestać działać. Jeżeli wszystko sie uda , ma sie wyswitlić log na konsole że sie udało.

### Logowanie:
Logowanie aplikacji ma sie wyświetlać zarówno na output jak i do pliku z logami który ma być w tym samym katalogu w którym jest binarka aplikacji w podkatalogu Logi
Pliki z logami musi być rotowany - to znaczy - jeżeli jakaś akcja została wykonana pewnego dnia to ma sie tam pojawic plik o nazwie logi_YYYYMMDD.log. W podfolderze ma być maksymlanie logów z 30 ostatnich dni. Może są do tego jakieś nugety które to automatyzują. Nie wymyślaj swojego kodu, staraj sie wykorzystac istniejące biblioteki. 

- Po rozpoczęciu aplikacji , ma ona wyświetlic swoją konfigurację (hasła oczywiście zamakowane) - czyli wszystkie parametry z jakimi zostła skonfiguroana z pliku.
- Co do treści logów, to sprawdź kod poprzednich aplikacji i loguj to samo w tych samych sytuacjach.


### Inne wymagania
- Aplikacja będzie często używała czasu. Np do określenia która jest godzina i czy należy zacząć konsumować transfer czy też jest za wcześnie (np przed 23:00) i zignorować tę iterację. - Pobieranie czasu musi umiec pobierać czas lokalny i w tym celu odpytywać serwer zewnetrzny bo zegar na komputrze na którym będzie uruchamian aplikacja może być nieaktualny. To sie już zdarzało.

- Aplikacja musi wykryawać ze przeglądarka się zamknęła i wtrakcie realiozwania zadań musi otworzyć nową i się do niej podłączyć.


- W celu 'podtrzymania sesji przeglądarki' aplikacja od czasu do czasu będzie robiła 'jałowe' przejścia do stron w stylu https://www.nopremium.pl/help albo https://www.nopremium.pl/offer tylko po to żęby strona wykryła ruch i możliwe że odświerzyła sobie jakieś cistaczka czy inne numery sessji , tak by nie cloudlflare zawsze myślał że użytkonik siedi przy komputerzenie i wymagała ponownego klikania w okienko "udowodnij że jesteś człowiekm"

---

## Ustalenia dodatkowe (uzupełnienia z Q&A — nie ujęte w pierwotnym pliku)

### Konfiguracja email
Pole "Email login page" z pierwotnych wymagań oznacza konfigurację IMAP. W pliku konfiguracyjnym powinno być jedno pole w formacie `"host:port"`, np. `"imap.gmx.com:993"`. Połączenie zawsze używa SSL. Aplikacja parsuje ten string rozdzielając po `:`.

### Plik z linkami (transfer-consumer)
- Ścieżka do pliku jest **wymaganym** parametrem w głównym pliku konfiguracyjnym.
- Format pliku: JSON z tablicą `Links`, każdy element ma pola `Name` (string), `Url` (string), `Size` (string z jednostką np. `"512MB"`, `"3GB"`). Identyczny format jak stary `nopremium.config.json` z transfer-consumera (bez pola `PreserveTransferBytes` — to idzie teraz do głównego configa).
- Jeżeli plik nie istnieje lub nie da się go odczytać/sparsować — aplikacja nie startuje i wyświetla czytelny błąd.

### Harmonogramy (transfer-consumer i voucher-consumer)
Oba serwisy mają **niezależne** zestawy parametrów harmonogramu w konfiguracji:
- Godzina startowa
- Godzina końcowa
- Interwał w minutach

Pierwsze uruchomienie danego serwisu następuje o godzinie startowej zakresu, następne co `interwał` minut, ostatnie przed godziną końcową.

### Rezerwa transferu premium
Opcjonalny parametr w konfiguracji (bajty, long). Default: `3221225472` (3 GB). Transfer-consumer zatrzymuje konsumpcję gdy ilość pozostałego transferu premium spadnie do lub poniżej tej wartości.

### Keepalive
Opcjonalny parametr w konfiguracji, format `"HH:mm:ss"`, np. `"01:00:00"`. Default: `"01:00:00"` (1 godzina). Aplikacja w osobnym wątku/timerze co ten interwał nawiguje przeglądarką do jednej ze stron podtrzymujących sesję (np. `/help`, `/offer`).

### Interakcja z nopremium.pl
Wszystkie operacje na stronie **nopremium.pl** (logowanie, dodawanie linków do kolejki, konsumowanie voucherów) wykonywane są **przez przeglądarkę** (Playwright, klikanie w elementy strony) — nie przez HttpClient. Aplikacja utrzymuje otwartą przeglądarkę przez cały czas działania.

### Komunikacja z serwerem email (IMAP)
Do odczytu emaili z voucherami **NIE jest potrzebna przeglądarka**. Wystarczy zwykły klient IMAP (biblioteka MailKit) — tak jak robiła to stara aplikacja voucher-consumer. Przeglądarka jest potrzebna tylko do zaredeemowania kodów na stronie nopremium.pl.

### Poświadczenia — tylko z pliku konfiguracyjnego
W nowej implementacji poświadczenia (nopremium i email) pochodzą **wyłącznie z pliku JSON** podanego jako argument CLI. Zmienne środowiskowe nie są obsługiwane (inaczej niż sugerowały pierwotne wymagania).

**Konsumpcja transferu** = dodanie linku do kolejki pobierania na stronie `/files` — dokładnie tak jak robi to użytkownik ręcznie przez formularz na stronie. Stary kod robił to samo przez HTTP POST.

### Obsługa wartości opcjonalnych w konfigu
- Plik konfiguracyjny powinien mieć opcjonalne pola wstępnie wypełnione wartościami domyślnymi (jako dokumentacja dla użytkownika).
- Jeżeli pole jest nieobecne, `null` lub pusty string `""` — aplikacja gracefully używa zakodowanej wartości domyślnej (nie crashuje).
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
- Aplikacja musi wykryawać ze przeglądarka się zamknęła i wtrakcie realiozwania zadań musi otworzyć nową i się do niej podłączyć.
-Aplikacja musi wykrywać że inna instancja jest już uruchomiona w systemie i w takim wypadku druga instancja musi się sama wyłączyć.
- Pobieranie czasu musi pobierać czas lokalny i odpoytwyąc serwer zewnetrzny bo zegar na virtualce może być nieaktualny. To sie już zdarzało
- Po starcie aplikacji musi być możliwość przerwania jej działania po niaciśnięciu Ctrl+C
- Od czasu do czasu 'pozorne' przejścia do stron w stylu https://www.nopremium.pl/help albo https://www.nopremium.pl/offer tylko po to żęby strona wykryła ruch i możliwe że odświerzyła sobie jakieś cistaczka czy inne numery sessji , tak by nie cloudlflare zawsze myślał że użytkonik siedzy przy komputerzenie i wymagała ponownego klikania w okienko "udowodnij że jesteś człowiekm"
### Start aplikacji
Aplikacja powinna nie wystartowąć - zgłosić błąd przy starcie jeżeli.
- brakuje poświadczeń do zalogowania się do nopremium - czy to w zmiennych środowiskowych czy to w jakimś pliku konfiguracyjnym
- brakuje poświadczeń do zalogowania sie do konta email - czy to zmiennych środkowiskowych czy to w jakimś pliku konfiguracyjnym
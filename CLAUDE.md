# CLAUDE.md

Wskazówki dla Claude Code przy pracy z tym repo.

## Co to jest

Konsolowa aplikacja .NET 10 automatyzująca konto na **www.nopremium.pl**: co
wieczór konsumuje transfer premium (kolejkuje pliki do pobrania na `/files`) i
realizuje vouchery z maila, żeby gromadzić transfer dodatkowy. Steruje prawdziwą
przeglądarką (Chrome / Vivaldi) przez Playwright CDP, bo logowanie chroni
Cloudflare Turnstile.

## Build / testy / uruchomienie

```bash
cd src
dotnet build
dotnet test
dotnet test --filter "FullyQualifiedName~ScheduleHelperTests"      # jedna klasa
dotnet run --project NoPremium2/NoPremium2.csproj -- <config.json>  # uruchomienie
```

## Konwencje

- .NET 10, jeden target (`net10.0`).
- Testy: xUnit + NSubstitute + AwesomeAssertions, w `NoPremium2.Tests/`
  z układem katalogów lustrzanym do `NoPremium2/`.
- Logika czysta (parsery, harmonogram, rozmiary) jest `public static` i pokryta
  testami jednostkowymi.
- Wszystkie wartości domyślne w jednym miejscu: `Config/DefaultConstants.cs`.
- Poświadczenia wyłącznie z pliku konfiguracyjnego JSON — nigdy ze zmiennych
  środowiskowych.

## Dokumentacja

- `REQUIREMENTS.md` — wymagania (funkcjonalne / niefunkcjonalne / wydajnościowe / …).
- `instructions.md` — architektura i decyzje projektowe.

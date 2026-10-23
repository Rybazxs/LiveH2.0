# LiveH OS 2.0 — Foundation 0.0.1

## Zasada nadrzędna

LOCAL FIRST. Brak konta, backendu, analityki telemetrycznej i dostępu do Internetu w tej paczce.
Ta wersja nie przechowuje danych użytkownika i nie ma jeszcze bazy SQLite. Pierwszy
lokalny magazyn danych powinien trafić do osobnej paczki po uzgodnieniu kontraktów
oraz szyfrowania i migracji schematu. Nie deklarujemy szyfrowania, którego nie ma.

## Granice zależności

- `LiveH.Contracts` — wspólne modele odczytowe, routing, interfejsy. Nie zna UI.
- `LiveH.Core` — neutralny koordynator nawigacji, zależny wyłącznie od Contracts.
- `LiveH.Modules.Today` — czytnik pulpitu, zna tylko Contracts; obecnie jawnie pusty.
- `LiveH.App` — powłoka Android/MAUI, kompozycja w `MauiProgram.cs`, wizualne widoki.
- `tests/LiveH.ArchitectureChecks` — testy zero zależności zewnętrznych.

Kierunek: App -> Contracts/Core/Feature; Core -> Contracts; Feature -> Contracts.
Core nie importuje funkcji. Today nie czyta bazy BioOS ani Missions.

## Zakres patch-001

- Prawdziwy projekt Visual Studio 2026 (.NET 10 / MAUI / Android).
- UI: czarne tło, lokalny nagłówek, data, pusta sekcja BioOS, podsumowanie misji 0/0.
- Nawigacja: Dzisiaj, Misje, Track, Więcej; gesty prawo/lewo między trzema pierwszymi.
- Więcej: Campus LightOS, BioOS, Analityka, Ustawienia — na razie puste widoki.
- Szkielet wymiany danych w oparciu o `ITodayReadModelSource`.

## Poza zakresem

SQLite, dane Garmin, CSV, trackowanie, Neural/Bio/Performance/Stealth/FullSync,
jakakolwiek diagnostyka, synchronizacja lub automatyzacja. Dodanie ich nie może
wymuszać wprowadzania kodu domenowego do `MainPage`.

## Standard przyszłych paczek

Każda paczka będzie miała manifest zakresu zmian, numer wersji, zestaw zmienionych
plików, instrukcję zastosowania i testów, kontrolę zależności oraz wycofanie zmian.
Skrypt v0.0.1 jest bootstrapem i nigdy nie nadpisuje istniejącego `src` projektu.

## Prywatność i bezpieczeństwo

Android `allowBackup=false` i `usesCleartextTraffic=false`. Brak uprawnień do
zdrowia, lokalizacji, Bluetooth i sieci. Nie należy dodawać danych zdrowotnych
przed wdrożeniem szyfrowania, zgód i kontroli uprawnień. Integracje zewnętrzne
mogą wymagać chmur dostawców mimo lokalnego magazynu aplikacji.

# PATCH-007 — MissionOS v0.1

## Granice architektury

- `LiveH.Contracts.Missions` — publiczne DTO, typ terminu, identyfikatory, repozytorium i widok podsumowania.
- `LiveH.Modules.Missions` — walidacja, magazyn lokalny i obliczanie dzisiejszych/zaległych zadań; brak referencji do MAUI i innych modułów.
- `LiveH.Modules.Today` — adapter publicznego podsumowania; nie otwiera pliku misji.
- `LiveH.App` — widoki i DI (jedyny punkt tworzący repozytorium i ścieżkę danych).

## Przechowywanie danych

`FileSystem.AppDataDirectory/missions-v1.json` — prywatny katalog aplikacji Android.
Nie jest przesyłany na GitHub. `android:allowBackup="false"` jest już ustawione w manifeście.
Zapis przez plik tymczasowy, zastąpienie oryginału dopiero po zakończeniu zapisu.
Format `schemaVersion: 1`; nieznana wersja i uszkodzone pliki nie są automatycznie nadpisywane.
**Nie odinstalowuj aplikacji bez wcześniejszej kopii danych** — uninstall może skasować jej pamięć prywatną.

## Model misji

- ID: trwały UUID (łącznik z przyszłymi sesjami TrackOS i analityką)
- tytuł, notatka
- tryb terminu: urgent / dzień / dokładny zakres dat i godzin
- szacowana liczba minut (opcjonalnie)
- opcjonalne `CategoryId` (jeszcze bez katalogu kategorii)
- identyfikator strefy czasowej, daty lokalne, czas utworzenia UTC, czas wykonania UTC
- status otwarta / ukończona

## Reguły

- Urgent nie ma sztucznego terminu: występuje jako pilne do zrobienia aż do zakończenia.
- Dzienna staje się zaległa po upływie wskazanego dnia.
- Zakres staje się zaległy po upływie daty i godziny końca.
- Dzisiaj pokazuje ukończone i otwarte zaplanowane na dany dzień oraz aktywne pilne; zaległe są odseparowane.
- Usuwanie wymaga potwierdzenia.

## Ograniczenia wersji

Brak edycji misji, cykliczności, kategorii i automatycznego TrackOS.
Kalendarz i analityka zostaną podłączone przez `MissionRecord.Id` i jawne kontrakty.
Nie ma zdalnej synchronizacji ani kont użytkowników.

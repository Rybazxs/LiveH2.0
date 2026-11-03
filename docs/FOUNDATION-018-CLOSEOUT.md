# LiveH OS 2.0 - FOUNDATION 018 (kod bazowy)

Stan kodu: PATCH-018 UX FOUNDATION na bazie PATCH-010.
Dokument zamkniecia etapu, bez zmian schematu przechowywanych danych.

## Zakres bazowy
- MissionOS: misje, priorytet, status, edycja, odroczenie i historia.
- GroupsOS: hierarchia grup, identyfikatory, walidacja cykli, persystencja.
- TrackOS: niezalezne sesje Start/Pause/Resume/Stop, checkpoints, przypiecie do misji.
- TodayOS: read model; brak dodatkowego zrodla prawdy.
- Kalendarz: projekcja MissionOS, bez osobnej tabeli. Biezace bez daty i zaplanowane w odrebnych sekcjach, licznik tylko zaplanowanych.
- Wspolna karta misji w Today/Missions/Groups, zachowania kart maja wspolne zrodlo.
- UX PATCH-018: reuse widokow, ochrona pionowego scrolla, cache i weryfikacja dat.

## Zasady na kolejny etap
- Funkcje i dane nalezy implementowac w module, UI jest adapterem.
- Nie dodawac drugiej bazy do kalendarza ani kopii misji.
- Nie zmieniac TrackOS przy pracach w innych modulach bez testow regresji.
- Kluczowe testy: ArchitectureChecks, Android build oraz reczny test scrolla/gestow na emulatorze.

## Status weryfikacji
Zestaw zrodel z historycznych paczek 010 -> 011B -> 012B -> 012D -> 013B -> 014B -> 015 -> 016 -> 017 -> 018
odtworzony i zweryfikowany hashami. Bez lokalnego srodowiska MAUI/Android wytworca ZIP
NIE MOGL skompilowac aplikacji ani potwierdzic FPS/UX na urzadzeniu.
Instalator Windows uruchamia testy i kompilacje. Ostateczne uznanie UX za zamkniete
wymaga recznego przegladu na emulatorze/uzywanym telefonie.

## Po zakonczeniu
Rozpoczec prace nad kolejnymi modulami. Nie wydawac nowego APK podczas prac koncepcyjnych.

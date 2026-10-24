# LiveH OS 2.0 — PATCH-011 / GroupOS

## Granice modułów

- `LiveH.Modules.Groups` odwołuje się **wyłącznie** do `LiveH.Contracts`.
- Własność danych grup należy do `groups-v1.json`, w katalogu prywatnym aplikacji Android.
- MissionOS przechowuje opcjonalny `GroupId` i kompatybilne pole `GroupName`; **nie otwiera** magazynu grup.
- `LiveH.App` stanowi warstwę kompozycji: zna publiczne `IGroupRepository` i `IMissionRepository`.
- Today i MissionOS nie zależą od implementacji GroupOS. CampusOS, NotesOS, DocumentsOS i TrackOS pozostają osobnymi modułami.

## Funkcje

- Grupy nadrzędne i dowolnie zagnieżdżone podgrupy (`ParentGroupId`).
- Tworzenie, edycja nazw, przenoszenie do innej grupy, archiwizowanie/przywracanie.
- Ochrona przed cyklem, nieistniejącym rodzicem i powtórzoną nazwą w obrębie jednego poziomu.
- Widok misji przypisanych bezpośrednio do grupy i licznik misji we wszystkich podgrupach.
- Tworzenie misji z wybranej grupy, przypisywanie istniejącej misji lub odpinanie jej od grupy.
- Migracja dawnych etykiet `GroupName` z PATCH-008: po otwarciu Misji brakujące grupy główne powstają idempotentnie, a misje otrzymują `GroupId`. Dane misji są zachowane.
- Archiwizowanie nie usuwa misji, a archiwalnych gałęzi nie można wybierać do nowych przypisań.

## Ograniczenia PATCH-011

- Nie ma jeszcze systemu notatek ani załączników PDF; nie mieszać plików użytkownika do magazynu grup.
- Widok folderu pokazuje bezpośrednio przypisane misje; liczniki zawierają również potomków.
- Przeniesienie misji między grupami nie zmienia terminu, statusu, odroczeń ani priorytetu.
- Brak automatycznego push do GitHub i brak zmiany danych użytkownika na urządzeniu podczas instalacji kodu.

## Testy

`dotnet run --project tests/LiveH.ArchitectureChecks/LiveH.ArchitectureChecks.csproj`

Następnie Android w Visual Studio: Rebuild Solution → F5.

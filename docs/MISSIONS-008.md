# LiveH OS / PATCH-008 — Odroczenie i opcjonalna grupa

Zmiany wyłącznie wewnątrz MissionOS i formularza Misje. Nie dotykamy LiveH.Core,
na­wigacji, TodayView ani istniejących danych użytkownika.

## Działanie
- Każda niewykonana misja ma `ODROCZ` → `JUTRO` lub wybór daty.
- Misja na cito po przeniesieniu otrzymuje datę jednodniową.
- Misja w danym dniu otrzymuje nową datę.
- Misja od–do zachowuje lokalną godzinę startu i długość przedziału.
- Nie można przenosić na daty przeszłe ani odraczać wykonanych misji.
- Każda zmiana daty zapisuje historię w lokalnym pliku JSON misji.
- Formularz zawiera opcjonalne pole `Grupa`. Na razie jest to etykieta tekstowa,
  bez tworzenia listy grup i bez mieszania tego z przyszłą kategorią (`CategoryId`).
- Dotychczasowe pliki danych `schemaVersion=1` pozostają obsługiwane.

## Test ręczny
1. Utwórz misję `Na cito`, wpisz grupę, sprawdź wyświetlanie i restart aplikacji.
2. Wybierz `ODROCZ` → `JUTRO`: etykieta terminu powinna się zmienić.
3. Utwórz misję `od–do`, odrocz na konkretny dzień: godziny zostają bez zmian.
4. Z `Dzisiaj` sprawdź zmianę licznika i zaległości po ponownym wejściu.
5. Sprawdź, że ukończenie i usuwanie nadal działają.

## Dane i bezpieczeństwo
Bez internetu i bez kasowania istniejących misji. Instalator tworzy kopię
zmienianych plików źródłowych (nie modyfikuje prywatnej bazy telefonu).
Zalecany commit po zakończeniu testów: `feat(missions): reschedule and optional group`.

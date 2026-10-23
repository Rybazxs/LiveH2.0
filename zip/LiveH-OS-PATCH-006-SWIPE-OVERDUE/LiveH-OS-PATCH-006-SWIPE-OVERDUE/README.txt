LIVEH OS 2.0 | PATCH-006 | SWIPE + ZALEGLE

ZMIANY:
- Gest poziomy w glownych ekranach: DZISIAJ <-> MISJE <-> TRACK.
- Bez zapetlania i bez swipe w otwartym menu WIECEJ.
- Obsluga gestow na przewijanej powierzchni, a nie rodzicu ScrollView.
- Panel ZALEGLE: 0 = zielony; wiecej niz 0 = rozowy; blad odczytu = neutralny.
- Dolne menu i przyciski zachowuja dotychczasowe dzialanie.
- Bez zmian w LiveH.Core, kontraktach i danych.

WYMAGA: zainstalowany PATCH-005, bez dodatkowych modyfikacji czterech plikow.
Skrypt kontroluje SHA256 i tworzy kopie zapasowe. Odmawia nadpisania nieznanej wersji.

1. Zatrzymaj debugowanie: Shift+F5.
2. ZIP wloz do E:\Private\Projekty\LiveH OS 2.0\zip i wybierz 'Wyodrebnij wszystkie'.
3. Komenda (dwa foldery o identycznej nazwie):

powershell -NoProfile -ExecutionPolicy Bypass -File "E:\Private\Projekty\LiveH OS 2.0\zip\LiveH-OS-PATCH-006-SWIPE-OVERDUE\LiveH-OS-PATCH-006-SWIPE-OVERDUE\APPLY-PATCH.ps1"

4. Visual Studio: Build > Rebuild Solution > F5.
5. Test: DZISIAJ -> lewo MISJE -> lewo TRACK -> prawo MISJE -> prawo DZISIAJ.
   WIECEJ > BioOS (gest nie zmienia strony), pionowe przewijanie ekranu Dzisiaj.
   Zielony panel ZALEGLE przy 0.

UWAGA: poprawka nie zostala skompilowana na Androidzie w tym srodowisku. Zweryfikuj na swoim emulatorze.

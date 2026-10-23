LiveH OS 2.0 / PATCH-005 UI REFINEMENT

TYLKO WYGLAD: MainPage.xaml i Views/TodayView.xaml.

1. Separator pod LiveH OS / Version.
2. DZIŚ nieco wyzej, mniejsze odstepy.
3. Ostre narozniki paneli BioOS / Twoj dzien / Zalegle.
4. Twoj dzien kompaktowy; sekcja DO ZROBIENIA wewnatrz.
5. Osobny panel Zalegle z faktycznym licznikiem (na razie 0 z pustego read-modelu).

Nie ma jeszcze listy poszczegolnych misji, poniewaz obecny kontrakt udostepnia tylko sumy. Placeholder jasno o tym informuje. Nie ma sztucznych danych.

WARUNKI: zainstalowany PATCH-004C, plik TodayView.xaml niezmieniony od Foundation.
Skrypt porownuje SHA256, robi kopie zapasowe i nie nadpisuje nieznanych wersji.

Wgraj ZIP do E:\Private\Projekty\LiveH OS 2.0\zip, wybierz Wyodrebnij wszystkie.

Uruchom w PowerShell:
powershell -NoProfile -ExecutionPolicy Bypass -File "E:\Private\Projekty\LiveH OS 2.0\zip\LiveH-OS-PATCH-005-UI-REFINE\LiveH-OS-PATCH-005-UI-REFINE\APPLY-PATCH.ps1"

Nastepnie w Visual Studio: Build > Rebuild Solution > F5.
Przed uruchomieniem zatrzymaj debugowanie Shift+F5.

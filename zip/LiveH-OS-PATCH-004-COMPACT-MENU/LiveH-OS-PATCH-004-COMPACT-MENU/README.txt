LIVEH OS 2.0 - PATCH-004-COMPACT-MENU

Zakres: tylko MainPage.xaml i MainPage.xaml.cs, bez zmian w Core, Contracts, Module Today, Android Theme czy danych.

Zmiany:
- Dolna nawigacja zmniejszona do ok. 55 dp lacznie z marginesami.
- Przyciski zachowuja obszar dotyku 48 dp (Android).
- Zamiast duzych kafelkow: subtelny cyan, cienka linia aktywnej karty.
- Menu WIECEJ zwezone i zmniejszone.
- Swipe, routing i zachowanie nawigacji niezmienione.

INSTALACJA:
1. W Visual Studio zatrzymaj debugowanie (Shift+F5).
2. ZIP zapisz w E:\Private\Projekty\LiveH OS 2.0\zip i rozpakuj: Wyodrebnij wszystkie.
3. Foldery po rozpakowaniu beda podwojne - tak ma byc.
4. Uruchom APPLY-PATCH.ps1 z wewnetrznego folderu.
5. Visual Studio: Build > Rebuild Solution > F5.

SKRYPT:
powershell -NoProfile -ExecutionPolicy Bypass -File "E:\Private\Projekty\LiveH OS 2.0\zip\LiveH-OS-PATCH-004-COMPACT-MENU\LiveH-OS-PATCH-004-COMPACT-MENU\APPLY-PATCH.ps1"

Skrypt odmawia nadpisania zmienionych plikow i tworzy kopie zapasowa przed aktualizacja.
Nie modyfikuje repo Git.

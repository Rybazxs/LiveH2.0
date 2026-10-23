LIVEH OS PATCH-004B -- MENU REFINE

Cel: zmniejszyc *istniejace* menu, nie przeprojektowac jego wygladu.

Zmiany:
- wysokosc klikalnych zakladek: 66 -> 48 dp (minimum zalecane dla dotyku)
- padding paska pionowo: 8 -> 4 dp
- font menu: 12 -> 11 dp
- pozostaja oryginalne ciemnoniebieskie tla, zaokraglenia i aktywny kafelek
- nazwy: DZISIAJ / MISJE / TRACK / WIECEJ
- panel WIECEJ zachowany z pierwszej wersji
- brak zmian w Core, mod. Dzisiaj i logice swipe/nawigacji

W razie zainstalowania PATCH-004 przywraca oryginalny kod podswietlania zakladek
i naklada mniejsze wymiary. Dziala tez bez PATCH-004.

Bezpiecznik: sprawdza SHA256 wszystkich nadpisywanych plikow.
Obce zmiany sa blokowane. Kopia zapasowa powstaje w zip/_backups.

Instalacja: rozpakuj ZIP standardowo przez Windows i uruchom
...\zip\LiveH-OS-PATCH-004B-MENU-REFINE\LiveH-OS-PATCH-004B-MENU-REFINE\APPLY-PATCH.ps1

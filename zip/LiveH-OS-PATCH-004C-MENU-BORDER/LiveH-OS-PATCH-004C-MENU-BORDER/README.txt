LIVEH OS | PATCH-004C MENU BORDER

Wymaga zainstalowanego PATCH-004B.

JEDYNA ZMIANA: cienka ramka OsBorder o szerokosci 1 dp wokol calego dolnego menu.
Wysokosc przyciskow 48 dp, czcionka 11, logika nawigacji oraz Core pozostaja bez zmian.

INSTALACJA:
1. W Visual Studio zatrzymaj debugowanie (Shift+F5).
2. Zapisz ZIP w E:\Private\Projekty\LiveH OS 2.0\zip i uzyj Wyodrebnij wszystkie.
3. Uruchom:

powershell -NoProfile -ExecutionPolicy Bypass -File "E:\Private\Projekty\LiveH OS 2.0\zip\LiveH-OS-PATCH-004C-MENU-BORDER\LiveH-OS-PATCH-004C-MENU-BORDER\APPLY-PATCH.ps1"

4. Build -> Rebuild Solution -> F5.

Backup tworzony w katalogu zip\_backups\PATCH-004C-...
Plik XAML jest sprawdzany przed nadpisaniem za pomoca SHA256.

LiveH OS 2.0 — pierwsza paczka FOUNDATION 0.0.1
===============================================

1. Zapisz ZIP do:
   E:\Private\Projekty\LiveH OS 2.0\zip

2. W Eksploratorze Windows: prawy przycisk na ZIP > Wyodrebnij wszystkie.
   Domyslna sciezka powinna byc:
   E:\Private\Projekty\LiveH OS 2.0\zip\LiveH-OS-FOUNDATION-0.0.1

3. Wklej do PowerShell JEDNA komende:

powershell -NoProfile -ExecutionPolicy Bypass -File "E:\Private\Projekty\LiveH OS 2.0\zip\LiveH-OS-FOUNDATION-0.0.1\START-LIVEH.ps1"

SKRYPT:
- sprawdzi .NET SDK i utworzy pliki projektu w folderze glownym;
- uruchomi szybkie testy rdzenia;
- zbuduje Android Debug APK;
- zainstaluje i uruchomi APK, jezeli dziala jedno urzadzenie/emulator ADB;
- otworzy LiveH.sln w Visual Studio (jesli Windows zna skojarzenie plikow SLN);
- zapisze dziennik w folderze logs.

WYMAGANE: Visual Studio 2026 z obciazeniem .NET MAUI / Android, .NET 10 SDK,
Android SDK oraz emulator lub telefon z wlaczonym debugowaniem USB.

UWAGA: ta paczka nie jest gotowym APK. Buduje go na Twoim komputerze.
Projekt jest tylko na Android, bez kodu starej aplikacji. NIE importuj starego src.

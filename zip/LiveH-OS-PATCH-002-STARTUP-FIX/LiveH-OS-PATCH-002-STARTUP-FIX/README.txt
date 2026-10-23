LiveH OS 2.0 — PATCH-002 / STARTUP FIX

Objaw: widoczne tylko logo startowe; aplikacja zamyka sie przed pokazaniem Today.
Z logu: XamlParseException: StaticResource not found for key OsBlack.

Przyczyna: App(MainPage mainPage) tworzy MainPage przez Dependency Injection
przed wykonaniem App.InitializeComponent(). Slowniki Colors.xaml i Buttons.xaml
nie sa wtedy jeszcze zaladowane.

Poprawka: wstrzykniecie IServiceProvider do App i utworzenie MainPage dopiero
w CreateWindow(), po InitializeComponent().

Instalacja:
1) Zatrzymaj debugowanie w Visual Studio (Shift+F5).
2) Wypakuj ta paczke do folderu zip obok Foundation.
3) Uruchom:
   powershell -NoProfile -ExecutionPolicy Bypass -File "E:\Private\Projekty\LiveH OS 2.0\zip\LiveH-OS-PATCH-002-STARTUP-FIX\APPLY-PATCH.ps1"
4) W Visual Studio: Build > Rebuild Solution, nastepnie uruchom LiveH.App (F5).

Patch dotyczy tylko: src/LiveH.App/App.xaml.cs
Nie rusza danych, nie nadpisuje innych modulow, tworzy kopie zapasowa.
Skrypt odmowi nadpisania pliku, jesli nie zgadza sie z oryginalem.

Poprawka nie zostala skompilowana na Androidzie w tym srodowisku.

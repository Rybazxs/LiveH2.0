LiveH OS 2.0 / PATCH-003 / ANDROID-THEME-FIX

Problem: Android.RuntimeException / screen_toolbar / ActionBarContextView during MainActivity startup.
Probable root cause: Foundation styles.xml defines Maui.MainTheme with parent Maui.MainTheme.NoActionBar.
Framework hierarchy: Maui.MainTheme.NoActionBar -> Maui.MainTheme -> Maui.MainTheme.Base.
The override creates a circular style inheritance and breaks native theme inflation.

Fix: replace the custom styles.xml with an empty Android resource container.
The MAUI framework's own Maui.MainTheme remains intact. Android black theme is
maintained through existing MainActivity.cs, Colors and MAUI UI design.

Only 1 project file changed: src/LiveH.App/Platforms/Android/Resources/values/styles.xml
App.xaml.cs from PATCH-002 is NOT overwritten.

Install with Windows Explorer "Wyodrebnij wszystkie" to
E:\Private\Projekty\LiveH OS 2.0\zip\LiveH-OS-PATCH-003-ANDROID-THEME-FIX\LiveH-OS-PATCH-003-ANDROID-THEME-FIX
then run the command from ChatGPT. Stop VS debugging first (Shift+F5).

Installer checks source/target SHA256, makes a backup, and refuses to overwrite
unexpected edits. Run Build > Rebuild Solution, then F5.
No Android emulator build performed here.

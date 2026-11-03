using Android.App;
using Android.Content.PM;
using Android.OS;
using System.Runtime.Versioning;

namespace LiveH.App;

[Activity(
    Name = "com.liveh.os.MainActivity",
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                           ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnNewIntent(Android.Content.Intent? intent)
    {
        base.OnNewIntent(intent);
        // Handle the newly delivered intent directly. Android API 35 introduced
        // a SetIntent overload with ComponentCaller, so do not depend on it.
        OpenTrackIfRequested(intent);
    }

    protected override void OnResume()
    {
        base.OnResume();
        OpenTrackIfRequested();
    }

    private void OpenTrackIfRequested(Android.Content.Intent? receivedIntent = null)
    {
        var launchIntent = receivedIntent ?? Intent;
        if (launchIntent?.Action != "com.liveh.os.OPEN_TRACK") return;
        launchIntent.SetAction(string.Empty);
        Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
        {
            if (Microsoft.Maui.Controls.Application.Current?.Windows.FirstOrDefault()?.Page is MainPage page)
                page.OpenTrack();
        });
    }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // On Android 15+ the page draws under transparent system bars.
        // The page background is black; on older versions color the bars explicitly.
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
            ConfigureLegacySystemBars();
    }

    [UnsupportedOSPlatform("android35.0")]
    private void ConfigureLegacySystemBars()
    {
        Window?.SetStatusBarColor(Android.Graphics.Color.Black);
        Window?.SetNavigationBarColor(Android.Graphics.Color.Black);
    }
}

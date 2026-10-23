using Android.App;
using Android.Content.PM;
using Android.OS;

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
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        // On Android 15+ the page draws under transparent system bars.
        // The page background is black; on older versions color the bars explicitly.
        if (Build.VERSION.SdkInt < BuildVersionCodes.VanillaIceCream)
        {
            Window?.SetStatusBarColor(Android.Graphics.Color.Black);
            Window?.SetNavigationBarColor(Android.Graphics.Color.Black);
        }
    }
}

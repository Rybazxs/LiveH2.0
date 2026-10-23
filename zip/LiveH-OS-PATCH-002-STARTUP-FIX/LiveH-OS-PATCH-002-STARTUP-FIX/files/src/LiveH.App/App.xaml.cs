using Microsoft.Extensions.DependencyInjection;

namespace LiveH.App;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    // Critical startup rule: load App.xaml resources BEFORE creating a XAML page.
    // Do not resolve MainPage in the App constructor: DI would construct it first,
    // and StaticResource lookup (e.g. OsBlack) would fail on Android startup.
    public App(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();
        UserAppTheme = AppTheme.Dark;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // At this point App.InitializeComponent has already loaded shared styles.
        return new Window(_services.GetRequiredService<MainPage>());
    }
}

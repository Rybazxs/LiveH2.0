using LiveH.Contracts.Today;
using LiveH.Core.Navigation;
using LiveH.Modules.Today;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace LiveH.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        // Composition root: modules can only be swapped here, via their contracts.
        builder.Services.AddSingleton<NavigationCoordinator>();
        builder.Services.AddSingleton<ITodayReadModelSource, EmptyTodayReadModelSource>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}

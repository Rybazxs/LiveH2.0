using LiveH.Contracts.Today;
using LiveH.Contracts.Missions;
using Microsoft.Maui.Storage;
using LiveH.Core.Navigation;
using LiveH.Modules.Today;
using LiveH.Modules.Missions;
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
        // Local-only MissionOS store; never place private data under the project folder.
        builder.Services.AddSingleton<IMissionRepository>(_ => new JsonMissionRepository(
            Path.Combine(FileSystem.AppDataDirectory, "missions-v1.json")));
        builder.Services.AddSingleton<IMissionDashboardSource, MissionDashboardSource>();
        builder.Services.AddSingleton<ITodayReadModelSource, MissionTodayReadModelSource>();
        builder.Services.AddSingleton<MainPage>();

        return builder.Build();
    }
}

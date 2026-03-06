using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Bannister.Services;
using Bannister.Views;
using ConversationPractice.Services;
using ConversationPractice.Views;
using Microsoft.Maui.LifecycleEvents;

namespace Bannister;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<AppMain>()
            .UseMauiCommunityToolkit();

        // Set window to fullscreen/maximized
        builder.ConfigureLifecycleEvents(events =>
        {
#if WINDOWS
            events.AddWindows(windows => windows
                .OnWindowCreated(window =>
                {
                    IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
                    var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                    var presenter = appWindow.Presenter as Microsoft.UI.Windowing.OverlappedPresenter;
                    if (presenter != null)
                    {
                        presenter.Maximize();
                    }
                }));
#endif
        });



        // Services
        builder.Services.AddSingleton<DatabaseService>();
        builder.Services.AddSingleton<AuthService>();
        builder.Services.AddSingleton<GameService>();
        builder.Services.AddSingleton<ActivityService>();
        builder.Services.AddSingleton<ExpService>();
        builder.Services.AddSingleton<DragonService>();
        builder.Services.AddSingleton<AttemptService>();
        builder.Services.AddSingleton<BackupService>();
        builder.Services.AddSingleton<StreakService>();
        builder.Services.AddSingleton<NewHabitService>();

        // Conversation Practice Module
        var dbPath = Path.Combine(FileSystem.AppDataDirectory, "bannister.db");
        builder.Services.AddSingleton(new ConversationService(dbPath));

        // Pages
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<GamesListPage>();
        builder.Services.AddTransient<NewHabitsPage>();
        builder.Services.AddTransient<ActivityGamePage>();
        builder.Services.AddTransient<SetupGoalPage>();
        builder.Services.AddTransient<ActivityCreationPage>();
        builder.Services.AddTransient<BatchAddActivitiesPage>();
        builder.Services.AddTransient<ManageActivitiesPage>();
        builder.Services.AddTransient<AttemptsPage>();
        builder.Services.AddTransient<DragonAttemptsPage>();
        builder.Services.AddTransient<ActiveDragonsPage>();
        builder.Services.AddTransient<SlainDragonsPage>();
        builder.Services.AddTransient<ExpiredActivitiesPage>();
        builder.Services.AddTransient<ExpLogPage>();
        builder.Services.AddTransient<DragonTreePage>();

        // Conversation Practice Pages
        builder.Services.AddTransient<ConversationListPage>();
        builder.Services.AddTransient<AddScenarioPage>();
        builder.Services.AddTransient<SetAutoAwardPage>();
        builder.Services.AddTransient<AutoAwardConfirmationPage>();    
        builder.Services.AddTransient<StreakDashboardPage>();
        builder.Services.AddTransient<StreakAttemptsPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
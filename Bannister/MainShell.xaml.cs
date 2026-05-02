namespace Bannister;
using Bannister.Views;

public partial class MainShell : Shell
{
    public MainShell()
    {
        InitializeComponent();

        Routing.RegisterRoute("gameslist", typeof(GamesListPage));
        Routing.RegisterRoute("activitygrid", typeof(ActivityGamePage));
        Routing.RegisterRoute("setupgoal", typeof(SetupGoalPage));
        Routing.RegisterRoute("addactivity", typeof(ActivityCreationPage));
        Routing.RegisterRoute("attempts", typeof(AttemptsPage));
        Routing.RegisterRoute("dragonattempts", typeof(DragonAttemptsPage));
        Routing.RegisterRoute("activedragons", typeof(ActiveDragonsPage));
        Routing.RegisterRoute("slaindragons", typeof(SlainDragonsPage));
        Routing.RegisterRoute("expiredactivities", typeof(ExpiredActivitiesPage));
        Routing.RegisterRoute("explog", typeof(ExpLogPage));
        Routing.RegisterRoute("dragontree", typeof(DragonTreePage));
        Routing.RegisterRoute("streakdashboard", typeof(StreakDashboardPage));
        Routing.RegisterRoute("streakattempts", typeof(StreakAttemptsPage));
        Routing.RegisterRoute("habits", typeof(HabitsHomePage));
        Routing.RegisterRoute("newhabits", typeof(NewHabitsPage));
        Routing.RegisterRoute("databases", typeof(DatabasesPage));
    }
}



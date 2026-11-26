using Bannister.Services;

namespace BannisterTools;

public partial class App : Application
{
    public App(MainPage mainPage)
    {
        InitializeComponent();
        // Shared views use the same named defaults; Tools keeps its own purple page canvas.
        foreach (var token in ModeTheme.Get(ActivityDisplayMode.All).Tokens)
            Resources["Mode" + token.Key] = token.Value;
        Resources["ModePageBackground"] = mainPage.BackgroundColor;
        MainPage = mainPage;
    }
}

using Bannister.Services;

namespace BannisterTools;

public partial class App : Application
{
    public App(MainPage mainPage)
    {
        InitializeComponent();
        MainPage = mainPage;
    }
}

using Bannister.Services;

namespace Bannister.Views;

public class DailyDeadlinesPage : ContentPage
{
    private readonly DailyDeadlinesView _view;

    public DailyDeadlinesPage(
        AuthService auth,
        DailyDeadlineService service,
        ResetTimeService resetTime,
        DatabaseService db,
        PrivacyModeService privacyMode,
        GameService games)
    {
        Title = "Daily Deadlines";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        _view = new DailyDeadlinesView(
            () => auth.CurrentUsername,
            service,
            resetTime,
            db,
            privacyMode,
            games,
            compact: false,
            showAlert: (title, message) => DisplayAlert(title, message, "OK"));
        Content = _view;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _view.ActivateAsync(showAlerts: true);
    }

    protected override void OnDisappearing()
    {
        _view.Deactivate();
        base.OnDisappearing();
    }
}

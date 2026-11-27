using Bannister.Services;

namespace Bannister.Views;

public sealed class UIColorsPage : ModeThemedPage
{
    private readonly ThemeColorsView _colors;

    public UIColorsPage(AuthService auth, AppSettingsService settings, PrivacyModeService privacy)
    {
        Title = "UI Colors";
        _colors = new ThemeColorsView(auth, settings, privacy);
        Content = new ScrollView { Content = _colors, Padding = 20 };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _colors.ActivateAsync();
    }

    protected override void OnDisappearing()
    {
        _colors.Deactivate();
        base.OnDisappearing();
    }
}

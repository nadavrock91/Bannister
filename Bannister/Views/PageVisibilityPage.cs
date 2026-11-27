using Bannister.Services;

namespace Bannister.Views;

public class PageVisibilityPage : Bannister.Views.ModeThemedPage
{
    private readonly HomeButtonVisibilityService _buttonVisibility;
    private readonly AuthService _auth;
    private VerticalStackLayout _container = null!;

    public PageVisibilityPage(
        HomeButtonVisibilityService buttonVisibility,
        AuthService auth)
    {
        _buttonVisibility = buttonVisibility;
        _auth = auth;
        Title = "Page Visibility";

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSectionAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 8
        };

        stack.Children.Add((new Label
        {
            Text = "Page Visibility",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = "Choose which pages appear on the home screen. " +
                   "Settings is always visible.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = new Thickness(0, 0, 0, 8)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        _container = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(_container);

        Content = new ScrollView { Content = stack };
    }

    private async Task LoadSectionAsync()
    {
        var allIds = HomePage.AllButtonIds;
        if (allIds.Count == 0) return;

        _container.Children.Clear();

        var settings = await _buttonVisibility
            .GetAllSettingsAsync(_auth.CurrentUsername, allIds);
        var validIds = allIds.ToHashSet(
            StringComparer.OrdinalIgnoreCase);

        foreach (var setting in settings.Where(
                     setting => validIds.Contains(setting.ButtonId)))
        {
            bool isSettingsBtn = setting.ButtonId.Equals(
                "Settings", StringComparison.OrdinalIgnoreCase);

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 12,
                Margin = new Thickness(0, 2, 0, 2)
            };

            row.Add((new Label
            {
                Text = setting.ButtonId,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            }).ThemeColor(Label.TextColorProperty, isSettingsBtn ? ThemeKey.CardText3 : ThemeKey.CardText1, ThemeKey.PageText2, classifyPageText: true), 0, 0);

            var toggle = new Switch
            {
                IsToggled = isSettingsBtn || setting.IsEnabled,
                IsEnabled = !isSettingsBtn,
                VerticalOptions = LayoutOptions.Center
            };

            if (!isSettingsBtn)
            {
                var capturedSetting = setting;
                toggle.Toggled += async (_, e) =>
                {
                    await _buttonVisibility.SetButtonEnabledAsync(
                        _auth.CurrentUsername,
                        capturedSetting.ButtonId,
                        e.Value);
                };
            }

            row.Add(toggle, 1, 0);
            _container.Children.Add(row);
        }
    }
}

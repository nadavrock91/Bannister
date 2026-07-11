using Bannister.Services;

namespace Bannister.Views;

public class HomePopupsSettingsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly HomePopupPreferenceService _service;
    private readonly VerticalStackLayout _cardsStack;

    private static readonly (string Key, string Title, string Description)[] PopupMeta =
    {
        ("streak_reset", "Streak Reset", "Notification when a streak has been reset due to missed days."),
        ("streak_escalation", "Streak Escalation", "Warning when a streak is at risk of resetting."),
        ("missed_fragments", "Missed Fragments", "Reminder about idea fragments that haven't been reviewed."),
        ("quick_input", "Quick Input", "Prompt to log a quick idea or thought."),
        ("days_since_escalation", "Days Since Escalation", "Alert when an activity hasn't been used in too long."),
        ("missed_activities", "Missed Activities", "Reminder about scheduled activities that were skipped."),
        ("habit_scolding", "Habit Scolding", "Nudge when your habit allowance has been stuck at 1 for too long.")
    };

    public HomePopupsSettingsPage(AuthService auth, HomePopupPreferenceService service)
    {
        _auth = auth;
        _service = service;

        Title = "HomePage Popups";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _cardsStack = new VerticalStackLayout { Spacing = 12 };
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 14,
            Children =
            {
                new Label
                {
                    Text = " HomePage Popups",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#1565C0")
                },
                new Label
                {
                    Text = "Control which popups appear when HomePage loads. Configure independently for primary and secondary device roles.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#666")
                },
                _cardsStack
            }
        };

        Content = new ScrollView { Content = stack };
    }

    private async Task LoadAsync()
    {
        var prefs = await _service.GetAllPreferencesAsync(_auth.CurrentUsername);

        _cardsStack.Children.Clear();
        foreach (var meta in PopupMeta)
        {
            var (primary, secondary) = prefs[meta.Key];
            _cardsStack.Children.Add(BuildPopupCard(meta.Key, meta.Title, meta.Description, primary, secondary));
        }
    }

    private View BuildPopupCard(string key, string title, string description, bool primary, bool secondary)
    {
        var primarySwitch = new Switch
        {
            IsToggled = primary,
            OnColor = Color.FromArgb("#2E7D32"),
            ThumbColor = Colors.White
        };
        primarySwitch.Toggled += async (_, e) =>
        {
            await _service.SetPreferenceAsync(_auth.CurrentUsername, key, "primary", e.Value);
        };

        var secondarySwitch = new Switch
        {
            IsToggled = secondary,
            OnColor = Color.FromArgb("#1565C0"),
            ThumbColor = Colors.White
        };
        secondarySwitch.Toggled += async (_, e) =>
        {
            await _service.SetPreferenceAsync(_auth.CurrentUsername, key, "secondary", e.Value);
        };

        var switchRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12
        };

        switchRow.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = "Primary device", FontSize = 12, TextColor = Color.FromArgb("#666") },
                primarySwitch
            }
        }, 0, 0);

        switchRow.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = "Secondary device", FontSize = 12, TextColor = Color.FromArgb("#666") },
                secondarySwitch
            }
        }, 1, 0);

        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 10,
            Padding = 14,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold },
                    new Label { Text = description, FontSize = 12, TextColor = Color.FromArgb("#666") },
                    switchRow
                }
            }
        };
    }
}

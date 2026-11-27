using Bannister.Services;

namespace Bannister.Views;

public class HomePopupsSettingsPage : Bannister.Views.ModeThemedPage
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
        ("habit_scolding", "Habit Scolding", "Nudge when your habit allowance has been stuck at 1 for too long."),
        ("subactivity", "Sub-Activities", "Prompt to check off sub-activity steps you completed today."),
        ("pending_prompts", "Pending Prompts", "Daily login reminders, weekly commitments, and habit slots that need attention."),
        ("life_path_checkin", "Life Path Check-in", "Daily prompt asking whether active focus blocks are still ongoing or have deviated.")
    };

    public HomePopupsSettingsPage(AuthService auth, HomePopupPreferenceService service)
    {
        _auth = auth;
        _service = service;

        Title = "HomePage Popups";


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
                (new Label
                {
                    Text = " HomePage Popups",
                    FontSize = 22,
                    FontAttributes = FontAttributes.Bold
                }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1, classifyPageText: true),
                (new Label
                {
                    Text = "Control which popups appear when HomePage loads. Configure independently for primary and secondary device roles.",
                    FontSize = 13
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true),
                _cardsStack
            }
        };

        Content = new ScrollView { Content = stack };
    }

    private async Task LoadAsync()
    {
        var prefs = await _service.GetAllPreferencesAsync(_auth.CurrentUsername);
        var seenMap = new Dictionary<string, bool>();

        foreach (var meta in PopupMeta)
        {
            seenMap[meta.Key] = _service.HasSeenTodayGate(meta.Key) &&
                await _service.IsSeenTodayAsync(_auth.CurrentUsername, meta.Key);
        }

        _cardsStack.Children.Clear();
        foreach (var meta in PopupMeta)
        {
            var (primary, secondary) = prefs[meta.Key];
            _cardsStack.Children.Add(BuildPopupCard(meta.Key, meta.Title, meta.Description, primary, secondary, seenMap[meta.Key]));
        }
    }

    private View BuildPopupCard(string key, string title, string description, bool primary, bool secondary, bool seenToday)
    {
        var primarySwitch = new Switch
        {
            IsToggled = primary,
            OnColor = AppColors.Get(ThemeKey.SuccessText),
            ThumbColor = AppColors.Get(ThemeKey.ButtonPrimaryText)
        };
        primarySwitch.Toggled += async (_, e) =>
        {
            await _service.SetPreferenceAsync(_auth.CurrentUsername, key, "primary", e.Value);
        };

        var secondarySwitch = new Switch
        {
            IsToggled = secondary,
            OnColor = AppColors.Get(ThemeKey.InfoText),
            ThumbColor = AppColors.Get(ThemeKey.ButtonPrimaryText)
        };
        secondarySwitch.Toggled += async (_, e) =>
        {
            await _service.SetPreferenceAsync(_auth.CurrentUsername, key, "secondary", e.Value);
        };

        var hasSeenTodayGate = _service.HasSeenTodayGate(key);
        var seenTodaySwitch = new Switch
        {
            IsToggled = seenToday,
            IsEnabled = hasSeenTodayGate,
            OnColor = AppColors.Get(ThemeKey.DisabledButtonBackground),
            ThumbColor = AppColors.Get(ThemeKey.ButtonPrimaryText)
        };
        seenTodaySwitch.Toggled += async (_, e) =>
        {
            await _service.SetSeenTodayAsync(_auth.CurrentUsername, key, e.Value);
        };

        var switchRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
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
                (new Label { Text = "Primary device", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true),
                primarySwitch
            }
        }, 0, 0);

        switchRow.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                (new Label { Text = "Secondary device", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true),
                secondarySwitch
            }
        }, 1, 0);

        var seenTodayColumn = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                (new Label
                {
                    Text = "Seen today",
                    FontSize = 12
                }).ThemeColor(Label.TextColorProperty, hasSeenTodayGate ? ThemeKey.CardText2 : ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true),
                seenTodaySwitch
            }
        };

        if (!hasSeenTodayGate)
        {
            seenTodayColumn.Children.Add((new Label
            {
                Text = "(no gate)",
                FontSize = 10,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        }

        switchRow.Add(seenTodayColumn, 2, 0);

        return (new Frame
        {
            CornerRadius = 10,
            Padding = 14,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold },
                    (new Label { Text = description, FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true),
                    switchRow
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
    }
}

using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ResetEnforcerDetailPage : ContentPage
{
    private readonly ResetEnforcerService _service;
    private readonly AuthService _auth;
    private ResetEnforcer _enforcer;

    private Image _enforcerImage = null!;
    private Label _resetCountLabel = null!;
    private Label _streakLabel = null!;
    private Label _levelLabel = null!;
    private Image _levelImage = null!;
    private VerticalStackLayout _conditionsContainer = null!;

    public ResetEnforcerDetailPage(
        ResetEnforcerService service,
        AuthService auth,
        ResetEnforcer enforcer)
    {
        _service = service;
        _auth = auth;
        _enforcer = enforcer;
        Title = enforcer.Name;
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Check auto level on every visit
        await _service.CheckAutoLevelAsync(_enforcer);
        var updated = await _service.GetEnforcerAsync(_enforcer.Id);
        if (updated != null) _enforcer = updated;
        await RefreshDisplayAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 14
        };

        // Enforcer image
        _enforcerImage = new Image
        {
            HeightRequest = 200,
            Aspect = AspectFromInt(_enforcer.ImageAspect),
            HorizontalOptions = LayoutOptions.Fill,
            IsVisible = false
        };
        stack.Children.Add(_enforcerImage);

        var imgBtnRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center
        };
        var changeImageBtn = new Button
        {
            Text = " Change Image",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 36,
            Padding = new Thickness(14, 0)
        };
        changeImageBtn.Clicked += async (_, _) =>
            await ChangeImageAsync();
        imgBtnRow.Children.Add(changeImageBtn);

        var cycleAspectBtn = new Button
        {
            Text = "⟳ Fit",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 36,
            Padding = new Thickness(10, 0)
        };
        cycleAspectBtn.Clicked += async (_, _) =>
        {
            _enforcer.ImageAspect = (_enforcer.ImageAspect + 1) % 3;
            await _service.UpdateEnforcerAsync(_enforcer);
            UpdateImageDisplay();
        };
        imgBtnRow.Children.Add(cycleAspectBtn);
        stack.Children.Add(imgBtnRow);

        // Name
        stack.Children.Add(new Label
        {
            Text = _enforcer.Name,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            HorizontalOptions = LayoutOptions.Center
        });

        // Stats
        _streakLabel = new Label
        {
            Text = $" {_enforcer.DaysInARow} days in a row",
            FontSize = 16,
            TextColor = Color.FromArgb("#2E7D32"),
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        stack.Children.Add(_streakLabel);

        _resetCountLabel = new Label
        {
            Text = $"Total Resets: {_enforcer.TotalResets}",
            FontSize = 14,
            TextColor = Color.FromArgb("#C62828"),
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        stack.Children.Add(_resetCountLabel);

        // Current level display
        _levelImage = new Image
        {
            HeightRequest = 100,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        stack.Children.Add(_levelImage);

        _levelLabel = new Label
        {
            Text = "",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        stack.Children.Add(_levelLabel);

        // Reset button
        var resetBtn = new Button
        {
            Text = "⚡ Reset",
            BackgroundColor = Color.FromArgb("#C62828"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 16,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        resetBtn.Clicked += async (_, _) => await DoResetAsync();
        stack.Children.Add(resetBtn);

        // Action buttons row
        var actionRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center
        };

        var levelsBtn = new Button
        {
            Text = " Manage Levels",
            BackgroundColor = Color.FromArgb("#E8EAF6"),
            TextColor = Color.FromArgb("#3949AB"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 38,
            Padding = new Thickness(12, 0)
        };
        levelsBtn.Clicked += async (_, _) =>
        {
            await Navigation.PushAsync(
                new EnforcerLevelsPage(_service, _enforcer));
        };
        actionRow.Children.Add(levelsBtn);

        var setLevelBtn = new Button
        {
            Text = "✏ Set Level",
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            TextColor = Color.FromArgb("#F57F17"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 38,
            Padding = new Thickness(12, 0)
        };
        setLevelBtn.Clicked += async (_, _) =>
            await ManualSetLevelAsync();
        actionRow.Children.Add(setLevelBtn);

        stack.Children.Add(actionRow);

        // Conditions section
        stack.Children.Add(new Label
        {
            Text = "Reset Conditions",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0"),
            Margin = new Thickness(0, 8, 0, 0)
        });
        stack.Children.Add(new Label
        {
            Text = "Define what triggers a reset for this enforcer.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        _conditionsContainer = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(_conditionsContainer);

        var addConditionBtn = new Button
        {
            Text = "+ New Reset Condition",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(14, 0)
        };
        addConditionBtn.Clicked += async (_, _) =>
            await AddConditionAsync();
        stack.Children.Add(addConditionBtn);

        Content = new ScrollView { Content = stack };

        UpdateImageDisplay();
    }

    private async Task RefreshDisplayAsync()
    {
        _streakLabel.Text =
            $" {_enforcer.DaysInARow} days in a row";
        _resetCountLabel.Text =
            $"Total Resets: {_enforcer.TotalResets}";

        // Update level display
        var levels = await _service.GetLevelsAsync(_enforcer.Id);
        if (_enforcer.CurrentLevelIndex >= 0 &&
            _enforcer.CurrentLevelIndex < levels.Count)
        {
            var current = levels[_enforcer.CurrentLevelIndex];
            _levelLabel.Text =
                $"{current.LevelIcon} {current.LevelDisplay} — {current.Name}";
            _levelLabel.TextColor = current.LevelNumber > 0
                ? Color.FromArgb("#2E7D32")
                : current.LevelNumber < 0
                    ? Color.FromArgb("#E65100")
                    : Color.FromArgb("#555");
            _levelLabel.IsVisible = true;

            if (!string.IsNullOrWhiteSpace(current.ImagePath) &&
                File.Exists(current.ImagePath))
            {
                _levelImage.Source =
                    ImageSource.FromFile(current.ImagePath);
                _levelImage.IsVisible = true;
            }
            else
            {
                _levelImage.IsVisible = false;
            }
        }
        else
        {
            _levelLabel.IsVisible = false;
            _levelImage.IsVisible = false;
        }

        // Refresh conditions
        var conditions = await _service.GetConditionsAsync(_enforcer.Id);
        _conditionsContainer.Children.Clear();
        if (conditions.Count == 0)
        {
            _conditionsContainer.Children.Add(new Label
            {
                Text = "No conditions defined yet.",
                FontSize = 12,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
        }
        else
        {
            foreach (var condition in conditions)
            {
                var condGrid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(new GridLength(36)),
                        new ColumnDefinition(new GridLength(36))
                    },
                    ColumnSpacing = 6,
                    RowDefinitions =
                    {
                        new RowDefinition(GridLength.Auto),
                        new RowDefinition(GridLength.Auto)
                    },
                    RowSpacing = 2
                };

                condGrid.Add(new Label
                {
                    Text = condition.Text,
                    FontSize = 13,
                    TextColor = Color.FromArgb("#222"),
                    VerticalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.WordWrap
                }, 0, 0);

                if (condition.HasTimeWindow)
                {
                    var timeText = new List<string>();
                    if (!string.IsNullOrWhiteSpace(condition.StartDisplay))
                        timeText.Add($"From: {condition.StartDisplay}");
                    if (!string.IsNullOrWhiteSpace(condition.EndDisplay))
                        timeText.Add($"To: {condition.EndDisplay}");
                    condGrid.Add(new Label
                    {
                        Text = string.Join("  ", timeText),
                        FontSize = 11,
                        TextColor = Color.FromArgb("#5B63EE"),
                        FontAttributes = FontAttributes.Italic
                    }, 0, 1);
                }

                var editCondBtn = new Button
                {
                    Text = "✏",
                    FontSize = 12,
                    HeightRequest = 34,
                    WidthRequest = 34,
                    CornerRadius = 6,
                    Padding = 0,
                    BackgroundColor = Color.FromArgb("#E3F2FD"),
                    TextColor = Color.FromArgb("#1565C0")
                };
                var capturedCond = condition;
                editCondBtn.Clicked += async (_, _) =>
                    await EditConditionTimeAsync(capturedCond);
                condGrid.Add(editCondBtn, 1, 0);

                var delCondBtn = new Button
                {
                    Text = "✕",
                    FontSize = 13,
                    HeightRequest = 34,
                    WidthRequest = 34,
                    CornerRadius = 6,
                    Padding = 0,
                    BackgroundColor = Color.FromArgb("#FFEBEE"),
                    TextColor = Color.FromArgb("#C62828")
                };
                var capturedCondId = condition.Id;
                delCondBtn.Clicked += async (_, _) =>
                {
                    bool confirm = await DisplayAlert(
                        "Delete Condition",
                        $"Delete \"{condition.Text}\"?",
                        "Delete", "Cancel");
                    if (!confirm) return;
                    await _service.DeleteConditionAsync(capturedCondId);
                    await RefreshDisplayAsync();
                };
                condGrid.Add(delCondBtn, 2, 0);
                _conditionsContainer.Children.Add(condGrid);
            }
        }
    }

    private async Task DoResetAsync()
    {
        bool confirm = await DisplayAlert(
            "Confirm Reset",
            $"Reset \"{_enforcer.Name}\"? " +
            "This will increment the reset count and reset the streak.",
            "Reset", "Cancel");
        if (!confirm) return;
        await _service.IncrementResetAsync(_enforcer.Id);
        var updated = await _service.GetEnforcerAsync(_enforcer.Id);
        if (updated != null) _enforcer = updated;
        await RefreshDisplayAsync();
    }

    private async Task ManualSetLevelAsync()
    {
        var levels = await _service.GetLevelsAsync(_enforcer.Id);
        if (levels.Count == 0)
        {
            await DisplayAlert("No Levels",
                "Add levels first via Manage Levels.", "OK");
            return;
        }

        var options = levels
            .Select(l => $"{l.LevelIcon} {l.LevelDisplay} — {l.Name}")
            .ToArray();

        string? choice = await DisplayActionSheet(
            "Set Current Level", "Cancel", null, options);
        if (choice == null || choice == "Cancel") return;

        int idx = Array.IndexOf(options, choice);
        if (idx < 0) return;

        _enforcer.CurrentLevelIndex = idx;
        await _service.UpdateEnforcerAsync(_enforcer);
        await RefreshDisplayAsync();
    }

    private async Task AddConditionAsync()
    {
        string? text = await DisplayPromptAsync(
            "New Reset Condition",
            "What triggers a reset?",
            "Add", "Cancel",
            placeholder: "e.g. Missed a daily commitment");
        if (string.IsNullOrWhiteSpace(text)) return;

        var condition = await _service.AddConditionAsync(
            _enforcer.Id, text.Trim());

        // Optionally set time window
        bool addTime = await DisplayAlert(
            "Time Window",
            "Add a start/end time window for this condition?",
            "Yes", "Skip");
        if (addTime)
            await EditConditionTimeAsync(condition);
        else
            await RefreshDisplayAsync();
    }

    private async Task EditConditionTimeAsync(
        ResetCondition condition)
    {
        // Start day
        string? startDayChoice = await DisplayActionSheet(
            "Start Day", "Any Day", null,
            "Sunday","Monday","Tuesday","Wednesday",
            "Thursday","Friday","Saturday");
        condition.StartDay = startDayChoice switch
        {
            "Sunday" => 0, "Monday" => 1, "Tuesday" => 2,
            "Wednesday" => 3, "Thursday" => 4,
            "Friday" => 5, "Saturday" => 6,
            _ => -1
        };

        // Start time
        string? startTimeInput = await DisplayPromptAsync(
            "Start Time",
            "Enter start time (HH:MM, 24h). Leave blank to skip.",
            "Set", "Skip",
            placeholder: "e.g. 09:00");
        condition.StartTime = ParseTimeToMinutes(startTimeInput);

        // End day
        string? endDayChoice = await DisplayActionSheet(
            "End Day", "Any Day", null,
            "Sunday","Monday","Tuesday","Wednesday",
            "Thursday","Friday","Saturday");
        condition.EndDay = endDayChoice switch
        {
            "Sunday" => 0, "Monday" => 1, "Tuesday" => 2,
            "Wednesday" => 3, "Thursday" => 4,
            "Friday" => 5, "Saturday" => 6,
            _ => -1
        };

        // End time
        string? endTimeInput = await DisplayPromptAsync(
            "End Time",
            "Enter end time (HH:MM, 24h). Leave blank to skip.",
            "Set", "Skip",
            placeholder: "e.g. 17:00");
        condition.EndTime = ParseTimeToMinutes(endTimeInput);

        await _service.UpdateConditionAsync(condition);
        await RefreshDisplayAsync();
    }

    private static int ParseTimeToMinutes(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return -1;
        var parts = input.Trim().Split(':');
        if (parts.Length == 2 &&
            int.TryParse(parts[0], out int h) &&
            int.TryParse(parts[1], out int m) &&
            h >= 0 && h <= 23 && m >= 0 && m <= 59)
            return h * 60 + m;
        return -1;
    }

    private void UpdateImageDisplay()
    {
        if (!string.IsNullOrWhiteSpace(_enforcer.ImagePath) &&
            File.Exists(_enforcer.ImagePath))
        {
            _enforcerImage.Source =
                ImageSource.FromFile(_enforcer.ImagePath);
            _enforcerImage.Aspect =
                AspectFromInt(_enforcer.ImageAspect);
            _enforcerImage.IsVisible = true;
        }
        else
        {
            _enforcerImage.IsVisible = false;
        }
    }

    private async Task ChangeImageAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select enforcer image",
                FileTypes = FilePickerFileType.Images
            });
            if (result == null) return;
            _enforcer.ImagePath = result.FullPath;
            await _service.UpdateEnforcerAsync(_enforcer);
            UpdateImageDisplay();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error",
                $"Could not pick image: {ex.Message}", "OK");
        }
    }

    private static Aspect AspectFromInt(int value) => value switch
    {
        1 => Aspect.AspectFill,
        2 => Aspect.Fill,
        _ => Aspect.AspectFit
    };
}

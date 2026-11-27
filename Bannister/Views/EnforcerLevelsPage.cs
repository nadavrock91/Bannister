using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class EnforcerLevelsPage : Bannister.Views.ModeThemedPage
{
    private readonly ResetEnforcerService _service;
    private readonly ResetEnforcer _enforcer;
    private VerticalStackLayout _levelsContainer = null!;

    public EnforcerLevelsPage(
        ResetEnforcerService service,
        ResetEnforcer enforcer)
    {
        _service = service;
        _enforcer = enforcer;
        Title = $"{enforcer.Name} — Levels";

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        stack.Children.Add((new Label
        {
            Text = "Define levels for this enforcer. Set triggers " +
                   "for automatic level changes based on streak days " +
                   "or total resets.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        var addBtn = (new Button
        {
            Text = "+ Add Level",
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        addBtn.Clicked += async (_, _) => await AddLevelAsync();
        stack.Children.Add(addBtn);

        _levelsContainer = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add(_levelsContainer);

        Content = new ScrollView { Content = stack };
    }

    private async Task RefreshAsync()
    {
        var levels = await _service.GetLevelsAsync(_enforcer.Id);
        _levelsContainer.Children.Clear();

        if (levels.Count == 0)
        {
            _levelsContainer.Children.Add((new Label
            {
                Text = "No levels defined yet.",
                FontSize = 12,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
            return;
        }

        foreach (var level in levels)
            _levelsContainer.Children.Add(BuildLevelCard(level));
    }

    private View BuildLevelCard(EnforcerLevel level)
    {
        var card = (new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, level.LevelNumber > 0 ? ThemeKey.HighlightLime : level.LevelNumber < 0 ? ThemeKey.WarningBackground : ThemeKey.SecondarySurface).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, level.LevelNumber > 0 ? ThemeKey.SuccessSelectedBackground : level.LevelNumber < 0 ? ThemeKey.HighlightAmberStrong : ThemeKey.Separator);

        var inner = new VerticalStackLayout { Spacing = 6 };

        // Name + type badge
        var headerRow = new HorizontalStackLayout { Spacing = 8 };
        headerRow.Children.Add(new Label
        {
            Text = level.LevelIcon,
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center
        });
        headerRow.Children.Add((new Label
        {
            Text = level.Name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));
        headerRow.Children.Add((new Label
        {
            Text = level.LevelDisplay,
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, level.LevelNumber > 0 ? ThemeKey.SuccessText : level.LevelNumber < 0 ? ThemeKey.WarningText : ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        inner.Children.Add(headerRow);

        // Image preview
        if (!string.IsNullOrWhiteSpace(level.ImagePath) &&
            File.Exists(level.ImagePath))
        {
            inner.Children.Add(new Image
            {
                Source = ImageSource.FromFile(level.ImagePath),
                HeightRequest = 80,
                Aspect = Aspect.AspectFit,
                HorizontalOptions = LayoutOptions.Start
            });
        }

        // Triggers
        if (level.LevelNumber > 0)
        {
            var triggerParts = new List<string>();
            if (level.TriggerDays >= 0)
                triggerParts.Add($"Streak ≥ {level.TriggerDays} days");
            if (level.TriggerResets >= 0)
                triggerParts.Add($"Resets ≥ {level.TriggerResets}");
            inner.Children.Add((new Label
            {
                Text = triggerParts.Count > 0
                    ? "Auto: " + string.Join(" OR ", triggerParts)
                    : "Manual only",
                FontSize = 12,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        }
        else
        {
            inner.Children.Add((new Label
            {
                Text = "Triggered by Reset button",
                FontSize = 12,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3, classifyPageText: true));
        }

        // Action buttons
        var btnRow = new HorizontalStackLayout { Spacing = 8 };

        var editBtn = (new Button
        {
            Text = "✏ Edit",
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 34,
            Padding = new Thickness(10, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        var capturedLevel = level;
        editBtn.Clicked += async (_, _) => await EditLevelAsync(capturedLevel);
        btnRow.Children.Add(editBtn);

        var delBtn = (new Button
        {
            Text = "✕ Delete",
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 34,
            Padding = new Thickness(10, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        delBtn.Clicked += async (_, _) =>
        {
            bool confirm = await DisplayAlert(
                "Delete Level",
                $"Delete level \"{capturedLevel.Name}\"?",
                "Delete", "Cancel");
            if (!confirm) return;
            await _service.DeleteLevelAsync(capturedLevel.Id);
            await RefreshAsync();
        };
        btnRow.Children.Add(delBtn);

        inner.Children.Add(btnRow);
        card.Content = inner;
        return card;
    }

    private async Task AddLevelAsync()
    {
        string? name = await DisplayPromptAsync(
            "Level Name", "Name this level:",
            "Next", "Cancel",
            placeholder: "e.g. Knight, Fallen");
        if (string.IsNullOrWhiteSpace(name)) return;

        string? levelNumInput = await DisplayPromptAsync(
            "Level Number",
            "Enter a level number. Positive = achievement (+1, +2...), " +
            "Negative = penalty (-1, -2...). 0 = neutral.",
            "Set", "Cancel",
            initialValue: "1",
            keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(levelNumInput)) return;
        if (!int.TryParse(levelNumInput.Trim(), out int levelNumber))
            levelNumber = 1;
        if (levelNumber == 0)
        {
            await DisplayAlert("Invalid",
                "Level 0 is not allowed. Use +1 or higher for " +
                "positive levels, -1 or lower for negative levels.",
                "OK");
            return;
        }

        string imagePath = "";
        bool pickImg = await DisplayAlert(
            "Level Image",
            "Add an image for this level?",
            "Pick Image", "Skip");
        if (pickImg)
        {
            try
            {
                var r = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select level image",
                    FileTypes = FilePickerFileType.Images
                });
                if (r != null) imagePath = r.FullPath;
            }
            catch { }
        }

        int triggerDays = -1;
        int triggerResets = -1;

        if (levelNumber > 0)
        {
            string? daysInput = await DisplayPromptAsync(
                "Auto Trigger: Streak Days",
                "Auto-set this level when streak reaches X days. " +
                "Leave blank to skip.",
                "Set", "Skip",
                placeholder: "e.g. 30",
                keyboard: Keyboard.Numeric);
            if (!string.IsNullOrWhiteSpace(daysInput) &&
                int.TryParse(daysInput.Trim(), out int pd) && pd >= 0)
                triggerDays = pd;

            string? resetsInput = await DisplayPromptAsync(
                "Auto Trigger: Total Resets",
                "Auto-set this level when total resets reaches X. " +
                "Leave blank to skip.",
                "Set", "Skip",
                placeholder: "e.g. 5",
                keyboard: Keyboard.Numeric);
            if (!string.IsNullOrWhiteSpace(resetsInput) &&
                int.TryParse(resetsInput.Trim(), out int pr) && pr >= 0)
                triggerResets = pr;
        }

        await _service.AddLevelAsync(
            _enforcer.Id, name.Trim(), imagePath,
            levelNumber, triggerDays, triggerResets);
        await RefreshAsync();
    }

    private async Task EditLevelAsync(EnforcerLevel level)
    {
        string? name = await DisplayPromptAsync(
            "Edit Level Name", "Rename this level:",
            "Save", "Cancel",
            initialValue: level.Name);
        if (!string.IsNullOrWhiteSpace(name))
            level.Name = name.Trim();

        string? levelNumInput = await DisplayPromptAsync(
            "Level Number",
            "Level number (positive=achievement, negative=penalty):",
            "Set", "Skip",
            initialValue: level.LevelNumber.ToString(),
            keyboard: Keyboard.Numeric);
        if (!string.IsNullOrWhiteSpace(levelNumInput) &&
            int.TryParse(levelNumInput.Trim(), out int newLevelNum))
        {
            if (newLevelNum == 0)
            {
                await DisplayAlert("Invalid",
                    "Level 0 is not allowed.", "OK");
                // Keep existing
            }
            else
            {
                level.LevelNumber = newLevelNum;
            }
        }

        if (level.LevelNumber > 0)
        {
            string? daysInput = await DisplayPromptAsync(
                "Auto Trigger: Streak Days",
                "Days trigger (blank = disable):",
                "Set", "Skip",
                initialValue: level.TriggerDays >= 0
                    ? level.TriggerDays.ToString() : "",
                keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(daysInput))
                level.TriggerDays = -1;
            else if (int.TryParse(daysInput.Trim(), out int d))
                level.TriggerDays = d >= 0 ? d : -1;

            string? resetsInput = await DisplayPromptAsync(
                "Auto Trigger: Total Resets",
                "Resets trigger (blank = disable):",
                "Set", "Skip",
                initialValue: level.TriggerResets >= 0
                    ? level.TriggerResets.ToString() : "",
                keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(resetsInput))
                level.TriggerResets = -1;
            else if (int.TryParse(resetsInput.Trim(), out int r))
                level.TriggerResets = r >= 0 ? r : -1;
        }
        else
        {
            // Negative levels cannot auto-trigger — clear any existing
            level.TriggerDays = -1;
            level.TriggerResets = -1;
        }

        bool changeImg = await DisplayAlert(
            "Change Image", "Change the level image?",
            "Pick New Image", "Keep Current");
        if (changeImg)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select level image",
                    FileTypes = FilePickerFileType.Images
                });
                if (result != null)
                    level.ImagePath = result.FullPath;
            }
            catch { }
        }

        await _service.UpdateLevelAsync(level);
        await RefreshAsync();
    }
}

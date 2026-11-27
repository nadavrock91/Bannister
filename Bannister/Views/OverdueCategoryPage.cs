using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class OverdueCategoryPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly CountdownService _countdowns;
    private readonly string _category; // null = all overdue
    
    private VerticalStackLayout _countdownsLayout;
    
    public OverdueCategoryPage(AuthService auth, CountdownService countdowns, string category)
    {
        _auth = auth;
        _countdowns = countdowns;
        _category = category;
        
        Title = category ?? "All Overdue";

        
        BuildUI();
    }
    
    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 20
        };
        
        // Header
        mainStack.Children.Add((new Label
        {
            Text = "📛 " + (_category ?? "All Overdue"),
            FontSize = 28,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorButtonBackground, ThemeKey.PageText1, classifyPageText: true));
        
        // Countdowns list
        _countdownsLayout = new VerticalStackLayout { Spacing = 12 };
        mainStack.Children.Add(_countdownsLayout);
        
        scrollView.Content = mainStack;
        Content = scrollView;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCountdownsAsync();
    }
    
    private async Task LoadCountdownsAsync()
    {
        _countdownsLayout.Children.Clear();
        
        List<Countdown> countdowns;
        if (_category == null)
        {
            countdowns = await _countdowns.GetOverdueCountdownsAsync(_auth.CurrentUsername);
        }
        else
        {
            countdowns = await _countdowns.GetOverdueBySubcategoryAsync(_auth.CurrentUsername, _category);
        }
        
        if (countdowns.Count == 0)
        {
            _countdownsLayout.Children.Add((new Label
            {
                Text = "No overdue countdowns.",
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }
        
        // Sort by most negative first
        countdowns = countdowns.OrderBy(c => c.ManualCount).ToList();
        
        foreach (var countdown in countdowns)
        {
            var card = CreateCountdownCard(countdown);
            _countdownsLayout.Children.Add(card);
        }
    }
    
    private Frame CreateCountdownCard(Countdown countdown)
    {
        var frame = (new Frame
        {
            CornerRadius = 12,
            Padding = 16
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.PublicCardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.ErrorButtonBackground);
        
        var mainGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        
        var stack = new VerticalStackLayout { Spacing = 8 };
        
        // Title row with count
        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        
        titleRow.Children.Add((new Label
        {
            Text = "📛 " + countdown.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.OnColorText, ThemeKey.PageText1, classifyPageText: true));
        
        // Count with +/- buttons
        var countRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End };
        
        var btnMinus = (new Button
        {
            Text = "-1",
            CornerRadius = 4,
            Padding = new Thickness(8, 4),
            FontSize = 14,
            HeightRequest = 36
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        btnMinus.Clicked += async (s, e) =>
        {
            await _countdowns.DecrementManualCountAsync(countdown);
            await LoadCountdownsAsync();
        };
        countRow.Children.Add(btnMinus);
        
        var countLabel = (new Label
        {
            Text = countdown.ManualCount.ToString(),
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 50,
            HorizontalTextAlignment = TextAlignment.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorButtonBackground, ThemeKey.PageText1, classifyPageText: true);
        countRow.Children.Add(countLabel);
        
        var btnPlus = (new Button
        {
            Text = "+1",
            CornerRadius = 4,
            Padding = new Thickness(8, 4),
            FontSize = 14,
            HeightRequest = 36
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        btnPlus.Clicked += async (s, e) =>
        {
            await _countdowns.IncrementManualCountAsync(countdown);
            await LoadCountdownsAsync();
        };
        countRow.Children.Add(btnPlus);
        
        Grid.SetColumn(countRow, 1);
        titleRow.Children.Add(countRow);
        
        stack.Children.Add(titleRow);
        
        // Category badge (if showing all)
        if (_category == null)
        {
            stack.Children.Add((new Label
            {
                Text = $"Category: {countdown.OverdueCategory}",
                FontSize = 12
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        }
        
        // Original category
        stack.Children.Add((new Label
        {
            Text = $"From: {countdown.Category} • Started at: {countdown.OriginalManualCount}",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        
        // Created date
        stack.Children.Add((new Label
        {
            Text = $"Created: {countdown.CreatedAt:MMM d, yyyy}",
            FontSize = 10
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        
        Grid.SetColumn(stack, 0);
        mainGrid.Children.Add(stack);
        
        // 3-dots menu button
        var menuButton = (new Button
        {
            Text = "⋮",
            FontSize = 24,
            BackgroundColor = Colors.Transparent,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = 0,
            VerticalOptions = LayoutOptions.Start
        }).ThemeColor(Button.TextColorProperty, ThemeKey.CardText3);
        menuButton.Clicked += async (s, e) => await OnCountdownTapped(countdown);
        
        Grid.SetColumn(menuButton, 1);
        mainGrid.Children.Add(menuButton);
        
        frame.Content = mainGrid;
        
        return frame;
    }
    
    private async Task OnCountdownTapped(Countdown countdown)
    {
        var actions = new List<string>
        {
            "✏️ Set Value",
            "📜 View History",
            "🔄 Change Category",
            "✅ Mark as Resolved",
            "↩️ Move Back to Active",
            "🗑️ Delete"
        };
        
        string result = await DisplayActionSheet(
            countdown.Name,
            "Close",
            null,
            actions.ToArray());
        
        if (string.IsNullOrEmpty(result) || result == "Close") return;
        
        switch (result)
        {
            case "✏️ Set Value":
                await SetManualValue(countdown);
                break;
            case "📜 View History":
                await ViewHistory(countdown);
                break;
            case "🔄 Change Category":
                await ChangeCategory(countdown);
                break;
            case "✅ Mark as Resolved":
                await ResolveCountdown(countdown);
                break;
            case "↩️ Move Back to Active":
                countdown.Status = "Active";
                countdown.IsManual = false;
                countdown.TargetDate = DateTime.Now.AddDays(Math.Abs(countdown.ManualCount));
                await _countdowns.UpdateCountdownAsync(countdown);
                await LoadCountdownsAsync();
                break;
            case "🗑️ Delete":
                bool confirmDelete = await DisplayAlert("Delete", $"Delete '{countdown.Name}'?", "Delete", "Keep");
                if (confirmDelete)
                {
                    await _countdowns.DeleteCountdownAsync(countdown.Id);
                    await LoadCountdownsAsync();
                }
                break;
        }
    }
    
    private async Task SetManualValue(Countdown countdown)
    {
        string input = await DisplayPromptAsync(
            "Set Value",
            $"Current value: {countdown.ManualCount}\nEnter new value (negative for overdue):",
            "Save",
            "Cancel",
            initialValue: countdown.ManualCount.ToString(),
            keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrWhiteSpace(input)) return;
        
        if (int.TryParse(input, out int newValue))
        {
            await _countdowns.SetManualCountAsync(countdown, newValue, "Manual edit");
            await LoadCountdownsAsync();
        }
    }
    
    private async Task ViewHistory(Countdown countdown)
    {
        var history = await _countdowns.GetHistoryAsync(countdown.Id);
        
        if (history.Count == 0)
        {
            await DisplayAlert("History", "No history yet.", "OK");
            return;
        }
        
        var historyLines = new List<string>();
        foreach (var entry in history.Take(20))
        {
            string change = entry.ChangeType switch
            {
                "Decrement" => $"⬇️ {entry.OldValue} → {entry.NewValue}",
                "Increment" => $"⬆️ {entry.OldValue} → {entry.NewValue}",
                "Edit" => $"✏️ {entry.OldValue} → {entry.NewValue}",
                "Created" => $"🆕 Started at {entry.NewValue}",
                "Overdue" => $"📛 Moved to overdue at {entry.NewValue}",
                _ => $"{entry.OldValue} → {entry.NewValue}"
            };
            
            string line = $"{entry.ChangedAt:MMM d, HH:mm} - {change}";
            if (!string.IsNullOrEmpty(entry.Note))
                line += $" ({entry.Note})";
            
            historyLines.Add(line);
        }
        
        await DisplayAlert($"History: {countdown.Name}", string.Join("\n", historyLines), "OK");
    }
    
    private async Task ChangeCategory(Countdown countdown)
    {
        var categories = await _countdowns.GetOverdueCategoriesAsync(_auth.CurrentUsername);
        categories.Add("+ New Category");
        
        string result = await DisplayActionSheet(
            "Move to Category",
            "Cancel",
            null,
            categories.ToArray());
        
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;
        
        string newCategory = result;
        
        if (result == "+ New Category")
        {
            newCategory = await DisplayPromptAsync(
                "New Category",
                "Enter category name:",
                "Create",
                "Cancel",
                placeholder: "e.g., Technical, Personal");
            
            if (string.IsNullOrWhiteSpace(newCategory)) return;
        }
        
        countdown.OverdueCategory = newCategory;
        await _countdowns.UpdateCountdownAsync(countdown);
        await LoadCountdownsAsync();
    }
    
    private async Task ResolveCountdown(Countdown countdown)
    {
        string notes = await DisplayPromptAsync(
            "Resolve",
            "Any notes about this resolution?",
            "Resolve",
            "Skip",
            placeholder: "Optional notes...");
        
        await _countdowns.ResolveCountdownAsync(countdown, true, notes ?? "");
        await LoadCountdownsAsync();
    }
}

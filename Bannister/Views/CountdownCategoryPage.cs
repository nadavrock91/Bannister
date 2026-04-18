using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class CountdownCategoryPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CountdownService _countdowns;
    private readonly string _category;
    
    private VerticalStackLayout _countdownsLayout;
    
    public CountdownCategoryPage(AuthService auth, CountdownService countdowns, string category)
    {
        _auth = auth;
        _countdowns = countdowns;
        _category = category;
        
        Title = category;
        BackgroundColor = Color.FromArgb("#1A1A2E");
        
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
        mainStack.Children.Add(new Label
        {
            Text = GetCategoryEmoji(_category) + " " + _category,
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        
        // Add Countdown button
        var btnAdd = new Button
        {
            Text = "+ Add Countdown",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50
        };
        btnAdd.Clicked += OnAddCountdownClicked;
        mainStack.Children.Add(btnAdd);
        
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
        
        var countdowns = await _countdowns.GetCountdownsByCategoryAsync(_auth.CurrentUsername, _category);
        
        // Filter out Overdue - they appear in the Overdue section only
        countdowns = countdowns.Where(c => c.Status != "Overdue").ToList();
        
        if (countdowns.Count == 0)
        {
            _countdownsLayout.Children.Add(new Label
            {
                Text = "No countdowns yet.\nTap '+ Add Countdown' to make a prediction!",
                TextColor = Color.FromArgb("#AAAAAA"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }
        
        // Group by status
        var active = countdowns.Where(c => c.Status == "Active").OrderBy(c => c.TargetDate).ToList();
        var resolved = countdowns.Where(c => c.Status == "Correct" || c.Status == "Wrong" || c.Status == "Cancelled")
            .OrderByDescending(c => c.ResolvedAt).ToList();
        
        if (active.Count > 0)
        {
            _countdownsLayout.Children.Add(new Label
            {
                Text = "⏳ Active",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#4CAF50"),
                Margin = new Thickness(0, 10, 0, 5)
            });
            
            foreach (var countdown in active)
            {
                var card = CreateCountdownCard(countdown);
                _countdownsLayout.Children.Add(card);
            }
        }
        
        if (resolved.Count > 0)
        {
            _countdownsLayout.Children.Add(new Label
            {
                Text = "📋 History",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#AAAAAA"),
                Margin = new Thickness(0, 20, 0, 5)
            });
            
            foreach (var countdown in resolved)
            {
                var card = CreateCountdownCard(countdown);
                _countdownsLayout.Children.Add(card);
            }
        }
    }
    
    private Frame CreateCountdownCard(Countdown countdown)
    {
        var frame = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D44"),
            BorderColor = countdown.StatusColor,
            CornerRadius = 12,
            Padding = 16
        };
        
        var mainGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        
        var stack = new VerticalStackLayout { Spacing = 8 };
        
        // Title row
        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        
        titleRow.Children.Add(new Label
        {
            Text = countdown.StatusEmoji + " " + countdown.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        
        // For manual countdowns (active), show count with buttons
        if (countdown.IsManual && countdown.Status == "Active")
        {
            var countRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End };
            
            var btnMinus = new Button
            {
                Text = "-1",
                BackgroundColor = Color.FromArgb("#F44336"),
                TextColor = Colors.White,
                CornerRadius = 4,
                Padding = new Thickness(8, 4),
                FontSize = 14,
                HeightRequest = 36
            };
            btnMinus.Clicked += async (s, e) =>
            {
                await _countdowns.DecrementManualCountAsync(countdown);
                await LoadCountdownsAsync();
            };
            countRow.Children.Add(btnMinus);
            
            var countLabel = new Label
            {
                Text = countdown.ManualCount.ToString(),
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                TextColor = countdown.StatusColor,
                VerticalOptions = LayoutOptions.Center,
                WidthRequest = 50,
                HorizontalTextAlignment = TextAlignment.Center
            };
            countRow.Children.Add(countLabel);
            
            var btnPlus = new Button
            {
                Text = "+1",
                BackgroundColor = Color.FromArgb("#4CAF50"),
                TextColor = Colors.White,
                CornerRadius = 4,
                Padding = new Thickness(8, 4),
                FontSize = 14,
                HeightRequest = 36
            };
            btnPlus.Clicked += async (s, e) =>
            {
                await _countdowns.IncrementManualCountAsync(countdown);
                await LoadCountdownsAsync();
            };
            countRow.Children.Add(btnPlus);
            
            Grid.SetColumn(countRow, 1);
            titleRow.Children.Add(countRow);
        }
        else
        {
            var daysLabel = new Label
            {
                Text = countdown.Status == "Active" ? countdown.DaysRemainingDisplay : countdown.Status,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = countdown.StatusColor,
                HorizontalOptions = LayoutOptions.End
            };
            Grid.SetColumn(daysLabel, 1);
            titleRow.Children.Add(daysLabel);
        }
        
        stack.Children.Add(titleRow);
        
        // Target date (only for auto countdowns)
        if (!countdown.IsManual)
        {
            stack.Children.Add(new Label
            {
                Text = $"Target: {countdown.TargetDate:MMMM d, yyyy}",
                FontSize = 14,
                TextColor = Color.FromArgb("#AAAAAA")
            });
        }
        else
        {
            // Show original count for manual
            stack.Children.Add(new Label
            {
                Text = $"Started at: {countdown.OriginalManualCount} • Manual countdown",
                FontSize = 12,
                TextColor = Color.FromArgb("#888888")
            });
        }
        
        // Description if exists
        if (!string.IsNullOrWhiteSpace(countdown.Description))
        {
            stack.Children.Add(new Label
            {
                Text = countdown.Description,
                FontSize = 12,
                TextColor = Color.FromArgb("#888888"),
                FontAttributes = FontAttributes.Italic
            });
        }
        
        // Postpone info
        if (countdown.PostponeCount > 0)
        {
            stack.Children.Add(new Label
            {
                Text = $"⏱️ Postponed {countdown.PostponeCount}x ({countdown.TotalDaysPostponed} days total)",
                FontSize = 11,
                TextColor = Color.FromArgb("#FF9800")
            });
        }
        
        // Resolution notes
        if (!string.IsNullOrWhiteSpace(countdown.ResolutionNotes))
        {
            stack.Children.Add(new Label
            {
                Text = $"📝 {countdown.ResolutionNotes}",
                FontSize = 11,
                TextColor = Color.FromArgb("#888888")
            });
        }
        
        // Created date
        stack.Children.Add(new Label
        {
            Text = $"Created: {countdown.CreatedAt:MMM d, yyyy}",
            FontSize = 10,
            TextColor = Color.FromArgb("#666666")
        });
        
        Grid.SetColumn(stack, 0);
        mainGrid.Children.Add(stack);
        
        // 3-dots menu button
        var menuButton = new Button
        {
            Text = "⋮",
            FontSize = 24,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#AAAAAA"),
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = 0,
            VerticalOptions = LayoutOptions.Start
        };
        menuButton.Clicked += async (s, e) => await OnCountdownTapped(countdown);
        
        Grid.SetColumn(menuButton, 1);
        mainGrid.Children.Add(menuButton);
        
        frame.Content = mainGrid;
        
        return frame;
    }
    
    private async Task OnCountdownTapped(Countdown countdown)
    {
        var actions = new List<string>();
        
        if (countdown.Status == "Active")
        {
            if (countdown.IsManual)
            {
                actions.Add("✏️ Set Value");
                actions.Add("📜 View History");
                actions.Add("⏳ Convert to Auto");
                
                // If at 0 or below, offer to move to overdue
                if (countdown.ManualCount <= 0)
                {
                    actions.Add("📛 Move to Overdue");
                }
            }
            else
            {
                actions.Add("📅 Edit Days");
                actions.Add("🔢 Convert to Manual");
                actions.Add("📛 Move to Overdue");
            }
            
            if (countdown.NeedsResolution)
            {
                actions.Add("✅ Mark as Correct");
                actions.Add("❌ Mark as Wrong");
            }
            
            if (!countdown.IsManual)
            {
                actions.Add("⏱️ Postpone");
            }
            
            actions.Add("✏️ Edit Name");
            actions.Add("🚫 Cancel");
        }
        else if (countdown.Status == "Overdue")
        {
            actions.Add("✏️ Set Value");
            actions.Add("📜 View History");
            actions.Add("✅ Mark as Resolved");
            actions.Add("↩️ Move Back to Active");
        }
        
        actions.Add("🗑️ Delete");
        
        string result = await DisplayActionSheet(
            countdown.Name,
            "Close",
            null,
            actions.ToArray());
        
        if (string.IsNullOrEmpty(result) || result == "Close") return;
        
        switch (result)
        {
            case "📅 Edit Days":
                await EditDays(countdown);
                break;
            case "🔢 Convert to Manual":
                await ConvertToManual(countdown);
                break;
            case "⏳ Convert to Auto":
                await ConvertToAuto(countdown);
                break;
            case "✏️ Set Value":
                await SetManualValue(countdown);
                break;
            case "📜 View History":
                await ViewHistory(countdown);
                break;
            case "📛 Move to Overdue":
                await MoveToOverdue(countdown);
                break;
            case "↩️ Move Back to Active":
                countdown.Status = "Active";
                await _countdowns.UpdateCountdownAsync(countdown);
                await LoadCountdownsAsync();
                break;
            case "✅ Mark as Correct":
                await ResolveCountdown(countdown, true);
                break;
            case "✅ Mark as Resolved":
                await ResolveCountdown(countdown, true);
                break;
            case "❌ Mark as Wrong":
                await ResolveCountdown(countdown, false);
                break;
            case "⏱️ Postpone":
                await PostponeCountdown(countdown);
                break;
            case "✏️ Edit Name":
                await EditCountdown(countdown);
                break;
            case "🚫 Cancel":
                bool confirm = await DisplayAlert("Cancel Countdown", $"Cancel '{countdown.Name}'?", "Yes", "No");
                if (confirm)
                {
                    await _countdowns.CancelCountdownAsync(countdown);
                    await LoadCountdownsAsync();
                }
                break;
            case "🗑️ Delete":
                bool confirmDelete = await DisplayAlert("Delete Countdown", $"Delete '{countdown.Name}'? This cannot be undone.", "Delete", "Keep");
                if (confirmDelete)
                {
                    await _countdowns.DeleteCountdownAsync(countdown.Id);
                    await LoadCountdownsAsync();
                }
                break;
        }
    }
    
    private async Task EditDays(Countdown countdown)
    {
        string input = await DisplayPromptAsync(
            "Edit Days",
            $"Current: {countdown.DaysRemaining} days\nEnter new number of days:",
            "Save",
            "Cancel",
            initialValue: countdown.DaysRemaining.ToString(),
            keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrWhiteSpace(input)) return;
        
        if (int.TryParse(input, out int newDays))
        {
            countdown.TargetDate = DateTime.Now.AddDays(newDays);
            await _countdowns.UpdateCountdownAsync(countdown);
            await LoadCountdownsAsync();
        }
    }
    
    private async Task MoveToOverdue(Countdown countdown)
    {
        string input = await DisplayPromptAsync(
            "Move to Overdue",
            "How many days overdue? (Enter a positive number, it will be stored as negative)\n\nExample: Enter 5 for -5 days overdue",
            "Next",
            "Cancel",
            placeholder: "e.g., 5",
            keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrWhiteSpace(input)) return;
        
        if (!int.TryParse(input, out int overdueDays) || overdueDays < 0)
        {
            await DisplayAlert("Invalid", "Please enter a positive number.", "OK");
            return;
        }
        
        // Ask for overdue category
        var existingCategories = await _countdowns.GetOverdueCategoriesAsync(_auth.CurrentUsername);
        existingCategories.Add("+ New Category");
        
        string categoryResult = await DisplayActionSheet(
            "Select Overdue Category",
            "Cancel",
            null,
            existingCategories.ToArray());
        
        if (string.IsNullOrEmpty(categoryResult) || categoryResult == "Cancel") return;
        
        string overdueCategory = categoryResult;
        
        if (categoryResult == "+ New Category")
        {
            overdueCategory = await DisplayPromptAsync(
                "New Overdue Category",
                "Enter category name:",
                "Create",
                "Cancel",
                placeholder: "e.g., Technical, Personal, Work");
            
            if (string.IsNullOrWhiteSpace(overdueCategory)) return;
        }
        
        // Use service method to move to overdue
        await _countdowns.MoveToOverdueAsync(countdown, overdueDays, overdueCategory);
        await LoadCountdownsAsync();
    }
    
    private async Task ConvertToManual(Countdown countdown)
    {
        int currentDays = countdown.DaysRemaining;
        
        bool confirm = await DisplayAlert(
            "Convert to Manual",
            $"Convert to manual countdown?\n\nCurrent days: {currentDays}\nThis will become your starting count.\n\nYou'll control the count with +1/-1 buttons.",
            "Convert",
            "Cancel");
        
        if (!confirm) return;
        
        countdown.IsManual = true;
        countdown.ManualCount = currentDays;
        countdown.OriginalManualCount = currentDays;
        
        await _countdowns.UpdateCountdownAsync(countdown);
        await _countdowns.AddHistoryAsync(countdown.Id, 0, currentDays, "Created", "Converted from auto");
        await LoadCountdownsAsync();
    }
    
    private async Task ConvertToAuto(Countdown countdown)
    {
        string daysInput = await DisplayPromptAsync(
            "Convert to Auto",
            "How many days until target date?",
            "Convert",
            "Cancel",
            initialValue: countdown.ManualCount.ToString(),
            keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrWhiteSpace(daysInput)) return;
        
        if (!int.TryParse(daysInput, out int days) || days <= 0)
        {
            await DisplayAlert("Invalid", "Please enter a valid number of days.", "OK");
            return;
        }
        
        countdown.IsManual = false;
        countdown.TargetDate = DateTime.Now.AddDays(days);
        countdown.OriginalTargetDate = countdown.TargetDate;
        
        await _countdowns.UpdateCountdownAsync(countdown);
        await DisplayAlert("Converted", $"Now counting down to {countdown.TargetDate:MMMM d, yyyy}", "OK");
        await LoadCountdownsAsync();
    }
    
    private async Task SetManualValue(Countdown countdown)
    {
        string input = await DisplayPromptAsync(
            "Set Value",
            $"Current value: {countdown.ManualCount}\nEnter new value:",
            "Save",
            "Cancel",
            initialValue: countdown.ManualCount.ToString(),
            keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrWhiteSpace(input)) return;
        
        if (int.TryParse(input, out int newValue))
        {
            string note = await DisplayPromptAsync(
                "Note (optional)",
                "Why are you changing this value?",
                "Save",
                "Skip",
                placeholder: "e.g., Correction, reset, etc.");
            
            await _countdowns.SetManualCountAsync(countdown, newValue, note ?? "");
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
        
        // Build history string
        var historyLines = new List<string>();
        foreach (var entry in history.Take(20)) // Show last 20 entries
        {
            string change = entry.ChangeType switch
            {
                "Decrement" => $"⬇️ {entry.OldValue} → {entry.NewValue}",
                "Increment" => $"⬆️ {entry.OldValue} → {entry.NewValue}",
                "Edit" => $"✏️ {entry.OldValue} → {entry.NewValue}",
                "Created" => $"🆕 Started at {entry.NewValue}",
                _ => $"{entry.OldValue} → {entry.NewValue}"
            };
            
            string line = $"{entry.ChangedAt:MMM d, HH:mm} - {change}";
            if (!string.IsNullOrEmpty(entry.Note))
                line += $" ({entry.Note})";
            
            historyLines.Add(line);
        }
        
        await DisplayAlert($"History: {countdown.Name}", string.Join("\n", historyLines), "OK");
    }
    
    private async Task ResolveCountdown(Countdown countdown, bool wasCorrect)
    {
        string notes = await DisplayPromptAsync(
            wasCorrect ? "Congratulations! 🎉" : "Better luck next time",
            "Any notes about this prediction?",
            "Save",
            "Skip",
            placeholder: "Optional notes...");
        
        await _countdowns.ResolveCountdownAsync(countdown, wasCorrect, notes ?? "");
        await LoadCountdownsAsync();
    }
    
    private async Task PostponeCountdown(Countdown countdown)
    {
        string result = await DisplayActionSheet(
            $"Postpone by how long?",
            "Cancel",
            null,
            "1 week",
            "2 weeks",
            "1 month",
            "3 months",
            "6 months",
            "1 year",
            "Custom...");
        
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;
        
        int days = result switch
        {
            "1 week" => 7,
            "2 weeks" => 14,
            "1 month" => 30,
            "3 months" => 90,
            "6 months" => 180,
            "1 year" => 365,
            "Custom..." => await GetCustomDays(),
            _ => 0
        };
        
        if (days > 0)
        {
            await _countdowns.PostponeCountdownAsync(countdown, days);
            await DisplayAlert("Postponed", $"New target: {countdown.TargetDate:MMM d, yyyy}", "OK");
            await LoadCountdownsAsync();
        }
    }
    
    private async Task<int> GetCustomDays()
    {
        string input = await DisplayPromptAsync(
            "Custom Postpone",
            "Enter number of days:",
            "OK",
            "Cancel",
            keyboard: Keyboard.Numeric);
        
        if (int.TryParse(input, out int days) && days > 0)
            return days;
        
        return 0;
    }
    
    private async Task EditCountdown(Countdown countdown)
    {
        string newName = await DisplayPromptAsync(
            "Edit Countdown",
            "Name:",
            "Next",
            "Cancel",
            initialValue: countdown.Name);
        
        if (string.IsNullOrWhiteSpace(newName)) return;
        
        string newDesc = await DisplayPromptAsync(
            "Edit Countdown",
            "Description (optional):",
            "Save",
            "Cancel",
            initialValue: countdown.Description);
        
        countdown.Name = newName;
        countdown.Description = newDesc ?? "";
        
        await _countdowns.UpdateCountdownAsync(countdown);
        await LoadCountdownsAsync();
    }
    
    private async void OnAddCountdownClicked(object sender, EventArgs e)
    {
        // Ask countdown type first
        string countdownType = await DisplayActionSheet(
            "Countdown Type",
            "Cancel",
            null,
            "⏳ Auto (counts down daily)",
            "🔢 Manual (you control the count)");
        
        if (string.IsNullOrEmpty(countdownType) || countdownType == "Cancel") return;
        
        bool isManual = countdownType.Contains("Manual");
        
        // Get name
        string name = await DisplayPromptAsync(
            "New Countdown",
            "What are you counting down?",
            "Next",
            "Cancel",
            placeholder: isManual ? "e.g., Books to read, Tasks remaining" : "e.g., Days till AGI");
        
        if (string.IsNullOrWhiteSpace(name)) return;
        
        // Get count/days
        string countInput = await DisplayPromptAsync(
            isManual ? "Starting Count" : "Days Until Event",
            isManual ? "Enter starting number:" : "How many days until you predict this will happen?",
            "Next",
            "Cancel",
            placeholder: isManual ? "e.g., 50" : "e.g., 365",
            keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrWhiteSpace(countInput)) return;
        
        if (!int.TryParse(countInput, out int count) || count < 0)
        {
            await DisplayAlert("Invalid", "Please enter a valid number.", "OK");
            return;
        }
        
        // Get description (optional)
        string description = await DisplayPromptAsync(
            "New Countdown",
            "Description (optional):",
            "Create",
            "Skip",
            placeholder: "Any additional details...");
        
        // Create the countdown
        Countdown countdown;
        if (isManual)
        {
            countdown = await _countdowns.CreateCountdownAsync(
                _auth.CurrentUsername,
                name,
                _category,
                DateTime.Now, // Not used for manual
                description ?? "",
                isManual: true,
                manualCount: count);
            
            await DisplayAlert("Countdown Created! 🔢", 
                $"'{name}'\nStarting count: {count}", 
                "OK");
        }
        else
        {
            DateTime targetDate = DateTime.Now.AddDays(count);
            countdown = await _countdowns.CreateCountdownAsync(
                _auth.CurrentUsername,
                name,
                _category,
                targetDate,
                description ?? "",
                isManual: false);
            
            await DisplayAlert("Countdown Created! ⏳", 
                $"'{name}'\nTarget: {targetDate:MMMM d, yyyy}\n{countdown.DaysRemainingDisplay}", 
                "OK");
        }
        
        await LoadCountdownsAsync();
    }
    
    private string GetCategoryEmoji(string category)
    {
        return category.ToLower() switch
        {
            "ai" => "🤖",
            "technology" => "💻",
            "personal" => "👤",
            "world events" => "🌍",
            "finance" => "💰",
            "sports" => "⚽",
            "movies" => "🎬",
            "science" => "🔬",
            "health" => "❤️",
            "politics" => "🏛️",
            "space" => "🚀",
            _ => "📌"
        };
    }
}

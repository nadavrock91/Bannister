using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class CountdownsHomePage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CountdownService _countdowns;
    
    private VerticalStackLayout _categoriesLayout;
    private VerticalStackLayout _expiredLayout;
    private Frame _expiredFrame;
    private Label _statsLabel;
    private Button _overdueButton;
    
    public CountdownsHomePage(AuthService auth, CountdownService countdowns)
    {
        _auth = auth;
        _countdowns = countdowns;
        
        Title = "Countdowns";
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
            Text = "⏳ Countdowns",
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        
        mainStack.Children.Add(new Label
        {
            Text = "Make predictions and track your accuracy",
            TextColor = Color.FromArgb("#AAAAAA"),
            FontSize = 14
        });
        
        // Stats
        _statsLabel = new Label
        {
            TextColor = Color.FromArgb("#4CAF50"),
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 10)
        };
        mainStack.Children.Add(_statsLabel);
        
        // Expired countdowns section (needs resolution)
        _expiredFrame = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D44"),
            BorderColor = Color.FromArgb("#FF9800"),
            CornerRadius = 12,
            Padding = 16,
            IsVisible = false
        };
        
        var expiredStack = new VerticalStackLayout { Spacing = 12 };
        expiredStack.Children.Add(new Label
        {
            Text = "⏰ Needs Resolution",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FF9800")
        });
        
        _expiredLayout = new VerticalStackLayout { Spacing = 8 };
        expiredStack.Children.Add(_expiredLayout);
        
        _expiredFrame.Content = expiredStack;
        mainStack.Children.Add(_expiredFrame);
        
        // Categories section
        mainStack.Children.Add(new Label
        {
            Text = "Categories",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            Margin = new Thickness(0, 10, 0, 0)
        });
        
        _categoriesLayout = new VerticalStackLayout { Spacing = 12 };
        mainStack.Children.Add(_categoriesLayout);
        
        // Add Category button
        var btnAddCategory = new Button
        {
            Text = "+ Add Category",
            BackgroundColor = Color.FromArgb("#3D3D5C"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            Margin = new Thickness(0, 10, 0, 0)
        };
        btnAddCategory.Clicked += OnAddCategoryClicked;
        mainStack.Children.Add(btnAddCategory);
        
        // Overdue section
        _overdueButton = new Button
        {
            Text = "📛 Overdue (0)",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            Margin = new Thickness(0, 20, 0, 0)
        };
        _overdueButton.Clicked += OnOverdueClicked;
        mainStack.Children.Add(_overdueButton);
        
        // View All button
        var btnViewAll = new Button
        {
            Text = "📊 View All Countdowns",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            Margin = new Thickness(0, 10, 0, 0)
        };
        btnViewAll.Clicked += OnViewAllClicked;
        mainStack.Children.Add(btnViewAll);
        
        scrollView.Content = mainStack;
        Content = scrollView;
    }
    
    private async void OnOverdueClicked(object sender, EventArgs e)
    {
        var page = new OverdueHomePage(_auth, _countdowns);
        await Navigation.PushAsync(page);
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }
    
    private async Task LoadDataAsync()
    {
        var username = _auth.CurrentUsername;
        
        // Load stats
        var stats = await _countdowns.GetStatsAsync(username);
        var accuracy = await _countdowns.GetAccuracyAsync(username);
        _statsLabel.Text = $"📈 {stats.active} active | ✅ {stats.correct} correct | ❌ {stats.wrong} wrong | Accuracy: {accuracy:F0}%";
        
        // Load overdue count
        var overdue = await _countdowns.GetOverdueCountdownsAsync(username);
        _overdueButton.Text = $"📛 Overdue ({overdue.Count})";
        
        // Load expired countdowns needing resolution
        _expiredLayout.Children.Clear();
        var expired = await _countdowns.GetExpiredCountdownsAsync(username);
        
        foreach (var countdown in expired)
        {
            var card = CreateExpiredCard(countdown);
            _expiredLayout.Children.Add(card);
        }
        
        // Show/hide expired section
        _expiredFrame.IsVisible = expired.Count > 0;
        
        // Load categories
        _categoriesLayout.Children.Clear();
        var categories = await _countdowns.GetCategoriesAsync(username);
        
        // Only add "misc" as default if no categories exist
        if (categories.Count == 0)
        {
            categories = new List<string> { "misc" };
        }
        
        // Ensure "misc" is always present
        if (!categories.Contains("misc"))
        {
            categories.Insert(0, "misc");
        }
        
        categories = categories.OrderBy(c => c).ToList();
        
        foreach (var category in categories)
        {
            var countdownsInCategory = await _countdowns.GetCountdownsByCategoryAsync(username, category);
            var activeCount = countdownsInCategory.Count(c => c.Status == "Active");
            
            var card = CreateCategoryCard(category, activeCount, countdownsInCategory.Count);
            _categoriesLayout.Children.Add(card);
        }
    }
    
    private Frame CreateCategoryCard(string category, int activeCount, int totalCount)
    {
        var frame = new Frame
        {
            BackgroundColor = Color.FromArgb("#2D2D44"),
            BorderColor = Colors.Transparent,
            CornerRadius = 12,
            Padding = 16
        };
        
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        
        var leftStack = new VerticalStackLayout { Spacing = 4 };
        
        leftStack.Children.Add(new Label
        {
            Text = GetCategoryEmoji(category) + " " + category,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        
        leftStack.Children.Add(new Label
        {
            Text = $"{activeCount} active • {totalCount} total",
            FontSize = 12,
            TextColor = Color.FromArgb("#AAAAAA")
        });
        
        Grid.SetColumn(leftStack, 0);
        grid.Children.Add(leftStack);
        
        var arrow = new Label
        {
            Text = "→",
            FontSize = 24,
            TextColor = Color.FromArgb("#5B63EE"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(arrow, 1);
        grid.Children.Add(arrow);
        
        frame.Content = grid;
        
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) => await OnCategoryTapped(category, totalCount);
        frame.GestureRecognizers.Add(tap);
        
        return frame;
    }
    
    private Frame CreateExpiredCard(Countdown countdown)
    {
        var frame = new Frame
        {
            BackgroundColor = Color.FromArgb("#3D2D2D"),
            BorderColor = Color.FromArgb("#FF9800"),
            CornerRadius = 8,
            Padding = 12
        };
        
        var stack = new VerticalStackLayout { Spacing = 8 };
        
        stack.Children.Add(new Label
        {
            Text = countdown.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        
        stack.Children.Add(new Label
        {
            Text = $"Target: {countdown.TargetDate:MMM d, yyyy} • {countdown.DaysRemainingDisplay}",
            FontSize = 12,
            TextColor = Color.FromArgb("#FF9800")
        });
        
        var buttonStack = new HorizontalStackLayout { Spacing = 8 };
        
        var btnCorrect = new Button
        {
            Text = "✅ I was right!",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(12, 6),
            FontSize = 12
        };
        btnCorrect.Clicked += async (s, e) => await ResolveCountdown(countdown, true);
        buttonStack.Children.Add(btnCorrect);
        
        var btnPostpone = new Button
        {
            Text = "⏱️ Postpone",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(12, 6),
            FontSize = 12
        };
        btnPostpone.Clicked += async (s, e) => await PostponeCountdown(countdown);
        buttonStack.Children.Add(btnPostpone);
        
        var btnWrong = new Button
        {
            Text = "❌ Wrong",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(12, 6),
            FontSize = 12
        };
        btnWrong.Clicked += async (s, e) => await ResolveCountdown(countdown, false);
        buttonStack.Children.Add(btnWrong);
        
        stack.Children.Add(buttonStack);
        frame.Content = stack;
        
        return frame;
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
        await LoadDataAsync();
    }
    
    private async Task PostponeCountdown(Countdown countdown)
    {
        string result = await DisplayActionSheet(
            $"Postpone '{countdown.Name}'?",
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
            await DisplayAlert("Postponed", $"New target: {countdown.TargetDate:MMM d, yyyy}\nPostpone count: {countdown.PostponeCount}", "OK");
            await LoadDataAsync();
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
    
    private async Task OnCategoryTapped(string category, int totalCount)
    {
        // If category has countdowns or is misc, just open it directly
        if (totalCount > 0 || category.ToLower() == "misc")
        {
            var page = new CountdownCategoryPage(_auth, _countdowns, category);
            await Navigation.PushAsync(page);
            return;
        }
        
        // Empty non-misc category - show options to open or remove
        string result = await DisplayActionSheet(
            category,
            "Cancel",
            null,
            "📂 Open",
            "🗑️ Remove Category");
        
        if (string.IsNullOrEmpty(result) || result == "Cancel") return;
        
        switch (result)
        {
            case "📂 Open":
                var openPage = new CountdownCategoryPage(_auth, _countdowns, category);
                await Navigation.PushAsync(openPage);
                break;
                
            case "🗑️ Remove Category":
                await DisplayAlert("Removed", $"Category '{category}' has been removed.", "OK");
                await LoadDataAsync();
                break;
        }
    }
    
    private async void OnAddCategoryClicked(object sender, EventArgs e)
    {
        string name = await DisplayPromptAsync(
            "New Category",
            "Enter category name:",
            "Create",
            "Cancel",
            placeholder: "e.g., Sports, Movies, Science");
        
        if (!string.IsNullOrWhiteSpace(name))
        {
            // Navigate to the new category page
            var page = new CountdownCategoryPage(_auth, _countdowns, name.Trim());
            await Navigation.PushAsync(page);
        }
    }
    
    private async void OnViewAllClicked(object sender, EventArgs e)
    {
        var page = new AllCountdownsPage(_auth, _countdowns);
        await Navigation.PushAsync(page);
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

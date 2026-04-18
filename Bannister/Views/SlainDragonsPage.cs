using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page showing all slain dragons with the same style as ActiveDragonsPage
/// </summary>
public class SlainDragonsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DragonService _dragons;
    private readonly AttemptService _attempts;

    private VerticalStackLayout _dragonsList;
    private ScrollView _mainScroll;
    private Picker _sortPicker;
    private string _currentSort = "Recently Slain";
    private Dictionary<string, bool> _expandedDragons = new();
    private double _savedScrollY = 0;

    public SlainDragonsPage(AuthService auth, DragonService dragons, AttemptService attempts)
    {
        _auth = auth;
        _dragons = dragons;
        _attempts = attempts;

        Title = "Slain Dragons";
        BackgroundColor = Color.FromArgb("#F5F7FC");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDragonsAsync();
    }

    private void BuildUI()
    {
        _mainScroll = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 20
        };

        // Header
        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#FF9800"),
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 4 };

        headerStack.Children.Add(new Label
        {
            Text = "🏆 Slain Dragons",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        headerStack.Children.Add(new Label
        {
            Text = "Dragons you have defeated (reached Level 100)",
            FontSize = 13,
            TextColor = Color.FromArgb("#FFFFFFCC")
        });

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Sort picker
        var sortStack = new HorizontalStackLayout 
        { 
            Spacing = 8, 
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 8, 0, 0)
        };
        sortStack.Children.Add(new Label
        {
            Text = "Sort by:",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });
        _sortPicker = new Picker
        {
            ItemsSource = new List<string>
            {
                "Recently Slain",
                "Oldest Slain",
                "Alphabetical (A-Z)",
                "Alphabetical (Z-A)",
                "Fastest Slay",
                "Longest Battle"
            },
            SelectedIndex = 0,
            WidthRequest = 180,
            TextColor = Colors.Black
        };
        _sortPicker.SelectedIndexChanged += async (s, e) =>
        {
            if (_sortPicker.SelectedItem != null)
            {
                _currentSort = _sortPicker.SelectedItem.ToString()!;
                await LoadDragonsAsync();
            }
        };
        sortStack.Children.Add(_sortPicker);
        mainStack.Children.Add(sortStack);

        // Info
        mainStack.Children.Add(new Label
        {
            Text = "💡 Click ▶ to expand attempt history for each dragon",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center
        });

        // Dragons list container
        _dragonsList = new VerticalStackLayout { Spacing = 16 };
        mainStack.Children.Add(_dragonsList);

        _mainScroll.Content = mainStack;
        Content = _mainScroll;
    }

    private async Task LoadDragonsAsync()
    {
        _dragonsList.Children.Clear();

        var dragons = await _dragons.GetSlainDragonsAsync(_auth.CurrentUsername);

        if (dragons.Count == 0)
        {
            _dragonsList.Children.Add(new Label
            {
                Text = "No slain dragons yet.\n\nReach Level 100 in a game to slay your first dragon!",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        // Get attempts for all dragons
        var dragonData = new List<(Dragon dragon, List<Attempt> attempts, TimeSpan slayDuration)>();
        foreach (var dragon in dragons)
        {
            var attempts = await _attempts.GetAttemptsForDragonAsync(_auth.CurrentUsername, dragon.Game, dragon.Title);
            var duration = dragon.SlainAt.HasValue ? dragon.SlainAt.Value - dragon.CreatedAt : TimeSpan.Zero;
            dragonData.Add((dragon, attempts, duration));
        }

        // Apply sorting
        IEnumerable<(Dragon dragon, List<Attempt> attempts, TimeSpan slayDuration)> sorted = _currentSort switch
        {
            "Recently Slain" => dragonData.OrderByDescending(d => d.dragon.SlainAt),
            "Oldest Slain" => dragonData.OrderBy(d => d.dragon.SlainAt),
            "Alphabetical (A-Z)" => dragonData.OrderBy(d => d.dragon.Title),
            "Alphabetical (Z-A)" => dragonData.OrderByDescending(d => d.dragon.Title),
            "Fastest Slay" => dragonData.OrderBy(d => d.slayDuration),
            "Longest Battle" => dragonData.OrderByDescending(d => d.slayDuration),
            _ => dragonData.OrderByDescending(d => d.dragon.SlainAt)
        };

        foreach (var (dragon, attempts, _) in sorted)
        {
            _dragonsList.Children.Add(BuildDragonSection(dragon, attempts));
        }
    }

    private string GetDragonKey(Dragon dragon) => $"{dragon.Game}|{dragon.Title}";

    private View BuildDragonSection(Dragon dragon, List<Attempt> attempts)
    {
        var container = new VerticalStackLayout { Spacing = 8 };

        var sortedAttempts = attempts.OrderByDescending(a => a.AttemptNumber).ToList();
        
        string dragonKey = GetDragonKey(dragon);

        if (!_expandedDragons.ContainsKey(dragonKey))
        {
            _expandedDragons[dragonKey] = false;
        }

        bool isExpanded = _expandedDragons[dragonKey];

        // Dragon header card
        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            BorderColor = Color.FromArgb("#FF9800"),
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 8 };

        // Title row with expand button and dragon image
        var titleRow = new HorizontalStackLayout { Spacing = 12 };

        // Expand/Collapse button
        var btnExpand = new Button
        {
            Text = isExpanded ? $"▼ ({sortedAttempts.Count})" : $"▶ ({sortedAttempts.Count})",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#FF9800"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 30,
            Padding = new Thickness(4, 0)
        };
        btnExpand.Clicked += (s, e) =>
        {
            _expandedDragons[dragonKey] = !_expandedDragons[dragonKey];
            _ = LoadDragonsAsync();
        };
        titleRow.Children.Add(btnExpand);

        // Dragon image (small)
        var dragonImage = new Image
        {
            WidthRequest = 40,
            HeightRequest = 40,
            Aspect = Aspect.AspectFill
        };
        string imagePath = GetFullImagePath(dragon.ImagePath);
        dragonImage.Source = !string.IsNullOrEmpty(imagePath) ? imagePath : "diet_dragon_default.png";
        titleRow.Children.Add(dragonImage);

        titleRow.Children.Add(new Label
        {
            Text = dragon.Title ?? "Unnamed Dragon",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.FillAndExpand
        });

        // Trophy badge
        titleRow.Children.Add(new Label
        {
            Text = "🏆 Slain!",
            FontSize = 14,
            TextColor = Color.FromArgb("#FF9800"),
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        });

        headerStack.Children.Add(titleRow);

        // Game name
        string gameDisplay = dragon.Game == "_standalone_" ? "Standalone Dragon" : dragon.Game;
        headerStack.Children.Add(new Label
        {
            Text = $"📁 {gameDisplay}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(40, 0, 0, 0)
        });

        // Slain info
        if (dragon.SlainAt.HasValue)
        {
            var duration = dragon.SlainAt.Value - dragon.CreatedAt;
            var infoRow = new HorizontalStackLayout { Spacing = 16, Margin = new Thickness(40, 4, 0, 0) };
            
            infoRow.Children.Add(new Label
            {
                Text = $"⚔️ Slain in {DragonService.FormatDuration(duration)}",
                FontSize = 14,
                TextColor = Color.FromArgb("#4CAF50"),
                FontAttributes = FontAttributes.Bold,
                VerticalOptions = LayoutOptions.Center
            });

            infoRow.Children.Add(new Label
            {
                Text = $"📅 {dragon.SlainAt.Value.ToLocalTime():MMM dd, yyyy}",
                FontSize = 12,
                TextColor = Color.FromArgb("#999"),
                VerticalOptions = LayoutOptions.Center
            });

            headerStack.Children.Add(infoRow);
        }

        // Buttons row
        var buttonsRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(40, 8, 0, 0) };

        // Edit Slay Date button
        var btnEditSlayDate = new Button
        {
            Text = "📅 Edit Slay Date",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            HorizontalOptions = LayoutOptions.Start
        };
        btnEditSlayDate.Clicked += async (s, e) => await EditSlayDateAsync(dragon);
        buttonsRow.Children.Add(btnEditSlayDate);

        // Add Attempt button
        var btnAddAttempt = new Button
        {
            Text = "+ Attempt",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            HorizontalOptions = LayoutOptions.Start
        };
        btnAddAttempt.Clicked += async (s, e) => await AddAttemptAsync(dragon, sortedAttempts);
        buttonsRow.Children.Add(btnAddAttempt);

        headerStack.Children.Add(buttonsRow);

        // Second row of buttons
        var buttonsRow2 = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(40, 4, 0, 0) };

        // Restore to Active button
        var btnRestore = new Button
        {
            Text = "♻️ Restore to Active",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            HorizontalOptions = LayoutOptions.Start
        };
        btnRestore.Clicked += async (s, e) => await RestoreToActiveAsync(dragon);
        buttonsRow2.Children.Add(btnRestore);

        // Move to Irrelevant button
        var btnIrrelevant = new Button
        {
            Text = "💤",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            WidthRequest = 40,
            HorizontalOptions = LayoutOptions.Start
        };
        btnIrrelevant.Clicked += async (s, e) => await MarkDragonIrrelevantAsync(dragon);
        buttonsRow2.Children.Add(btnIrrelevant);

        headerStack.Children.Add(buttonsRow2);

        headerFrame.Content = headerStack;
        container.Children.Add(headerFrame);

        // Attempt cards (if expanded)
        if (isExpanded && sortedAttempts.Count > 0)
        {
            var attemptsContainer = new FlexLayout
            {
                Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
                AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Start,
                Margin = new Thickness(24, 0, 0, 0)
            };

            foreach (var attempt in sortedAttempts)
            {
                attemptsContainer.Children.Add(BuildAttemptCard(attempt, dragon));
            }

            container.Children.Add(attemptsContainer);
        }

        return container;
    }

    private Frame BuildAttemptCard(Attempt attempt, Dragon dragon)
    {
        var isFailed = attempt.FailedAt.HasValue;
        
        var borderColor = isFailed ? Color.FromArgb("#F44336") : Color.FromArgb("#4CAF50");
        var bgColor = isFailed ? Color.FromArgb("#FFEBEE") : Color.FromArgb("#E8F5E9");

        var frame = new Frame
        {
            Padding = 10,
            CornerRadius = 8,
            BackgroundColor = bgColor,
            BorderColor = borderColor,
            HasShadow = false,
            WidthRequest = 120,
            Margin = new Thickness(4)
        };

        var stack = new VerticalStackLayout { Spacing = 4 };

        // Attempt number
        stack.Children.Add(new Label
        {
            Text = $"Attempt #{attempt.AttemptNumber}",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        // Duration
        stack.Children.Add(new Label
        {
            Text = $"{attempt.DurationDays} days",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = borderColor,
            HorizontalTextAlignment = TextAlignment.Center
        });

        // Status
        stack.Children.Add(new Label
        {
            Text = isFailed ? "Failed" : "Completed",
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center
        });

        // Tap to edit
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await EditAttemptAsync(attempt, dragon);
        frame.GestureRecognizers.Add(tapGesture);

        stack.Children.Add(new Label
        {
            Text = "tap to edit",
            FontSize = 9,
            TextColor = Color.FromArgb("#999"),
            FontAttributes = FontAttributes.Italic,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0)
        });

        frame.Content = stack;
        return frame;
    }

    private async Task EditAttemptAsync(Attempt attempt, Dragon dragon)
    {
        string action = await DisplayActionSheet(
            $"Attempt #{attempt.AttemptNumber} ({attempt.DurationDays} days)",
            "Cancel",
            null,
            "✏️ Edit Days",
            "🗑️ Delete Attempt");

        if (action == "✏️ Edit Days")
        {
            string daysStr = await DisplayPromptAsync(
                $"Edit Attempt #{attempt.AttemptNumber}",
                $"Current: {attempt.DurationDays} days\n\nEnter new number of days:",
                initialValue: attempt.DurationDays.ToString(),
                keyboard: Keyboard.Numeric,
                placeholder: "e.g., 30");

            if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int newDays) || newDays < 0)
            {
                return;
            }

            var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
            if (dbService == null) return;

            var conn = await dbService.GetConnectionAsync();
            
            var endTime = attempt.FailedAt ?? DateTime.UtcNow;
            attempt.StartedAt = endTime.AddDays(-newDays);
            await conn.UpdateAsync(attempt);

            _expandedDragons[GetDragonKey(dragon)] = true;
            _savedScrollY = _mainScroll.ScrollY;
            await LoadDragonsAsync();
            await RestoreScrollPositionAsync();
        }
        else if (action == "🗑️ Delete Attempt")
        {
            await DeleteAttemptAsync(attempt, dragon);
        }
    }

    private async Task DeleteAttemptAsync(Attempt attempt, Dragon dragon)
    {
        bool confirm = await DisplayAlert(
            "Delete Attempt",
            $"Delete Attempt #{attempt.AttemptNumber} ({attempt.DurationDays} days)?\n\nThis cannot be undone.",
            "Yes, Delete",
            "Cancel");

        if (confirm)
        {
            var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
            if (dbService == null) return;

            var conn = await dbService.GetConnectionAsync();
            await conn.DeleteAsync(attempt);

            // Renumber remaining attempts
            var remainingAttempts = await conn.Table<Attempt>()
                .Where(a => a.Username == _auth.CurrentUsername && a.Game == dragon.Game && a.DragonTitle == dragon.Title)
                .OrderBy(a => a.AttemptNumber)
                .ToListAsync();

            int num = 1;
            foreach (var a in remainingAttempts)
            {
                a.AttemptNumber = num++;
                await conn.UpdateAsync(a);
            }

            _expandedDragons[GetDragonKey(dragon)] = true;
            _savedScrollY = _mainScroll.ScrollY;
            await LoadDragonsAsync();
            await RestoreScrollPositionAsync();
        }
    }

    private async Task EditSlayDateAsync(Dragon dragon)
    {
        // Calculate current days to slay
        int currentDays = dragon.SlainAt.HasValue 
            ? (int)(dragon.SlainAt.Value - dragon.CreatedAt).TotalDays 
            : 0;

        string daysStr = await DisplayPromptAsync(
            "Edit Total Days to Slay",
            $"Current: {currentDays} days\n\nEnter total days it took to slay this dragon:",
            initialValue: currentDays.ToString(),
            keyboard: Keyboard.Numeric,
            placeholder: "e.g., 365");

        if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int newDays) || newDays < 0)
        {
            return;
        }

        var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
        if (dbService == null) return;

        var conn = await dbService.GetConnectionAsync();
        
        // Adjust CreatedAt to achieve desired duration (keeping SlainAt the same)
        if (dragon.SlainAt.HasValue)
        {
            dragon.CreatedAt = dragon.SlainAt.Value.AddDays(-newDays);
        }
        await conn.UpdateAsync(dragon);

        _savedScrollY = _mainScroll.ScrollY;
        await LoadDragonsAsync();
        await RestoreScrollPositionAsync();
    }

    private async Task AddAttemptAsync(Dragon dragon, List<Attempt> existingAttempts)
    {
        string daysStr = await DisplayPromptAsync(
            "Add Attempt",
            "How many days did this attempt last?",
            keyboard: Keyboard.Numeric,
            placeholder: "e.g., 30");

        if (string.IsNullOrEmpty(daysStr) || !int.TryParse(daysStr, out int days) || days < 0)
        {
            return;
        }

        string status = await DisplayActionSheet(
            "Attempt Status",
            "Cancel",
            null,
            "❌ Failed",
            "✅ Completed (Success)");

        if (status == "Cancel" || string.IsNullOrEmpty(status))
            return;

        bool isFailed = status.StartsWith("❌");

        var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
        if (dbService == null) return;

        var conn = await dbService.GetConnectionAsync();

        int newAttemptNumber = existingAttempts.Count > 0 
            ? existingAttempts.Max(a => a.AttemptNumber) + 1 
            : 1;

        var attempt = new Attempt
        {
            Username = _auth.CurrentUsername,
            Game = dragon.Game,
            DragonTitle = dragon.Title,
            AttemptNumber = newAttemptNumber,
            StartedAt = DateTime.UtcNow.AddDays(-days),
            FailedAt = isFailed ? DateTime.UtcNow : null,
            IsActive = false,
            CreatedAt = DateTime.UtcNow
        };

        await conn.InsertAsync(attempt);

        _expandedDragons[GetDragonKey(dragon)] = true;
        _savedScrollY = _mainScroll.ScrollY;
        await LoadDragonsAsync();
        await RestoreScrollPositionAsync();
    }

    private async Task RestoreToActiveAsync(Dragon dragon)
    {
        bool confirm = await DisplayAlert(
            "Restore to Active",
            $"Restore '{dragon.Title}' to Active Dragons?\n\nThis will clear the slain status.",
            "Yes, Restore",
            "Cancel");

        if (confirm)
        {
            var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
            if (dbService == null) return;

            var conn = await dbService.GetConnectionAsync();
            dragon.SlainAt = null;
            await conn.UpdateAsync(dragon);

            await DisplayAlert("Restored", $"'{dragon.Title}' has been restored to Active Dragons.", "OK");
            await LoadDragonsAsync();
        }
    }

    private async Task MarkDragonIrrelevantAsync(Dragon dragon)
    {
        bool confirm = await DisplayAlert(
            "Mark as Irrelevant",
            $"Mark '{dragon.Title}' as irrelevant?\n\nThis will move it to the Irrelevant Dragons list.",
            "Yes, Mark Irrelevant",
            "Cancel");

        if (confirm)
        {
            await _dragons.MarkDragonIrrelevantAsync(dragon.Id);
            await DisplayAlert("Moved", $"'{dragon.Title}' has been moved to Irrelevant Dragons.", "OK");
            await LoadDragonsAsync();
        }
    }

    private async Task RestoreScrollPositionAsync()
    {
        await Task.Delay(50);
        await _mainScroll.ScrollToAsync(0, _savedScrollY, false);
    }

    private static string GetFullImagePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return "";

        if (System.IO.Path.IsPathRooted(storedPath))
        {
            if (System.IO.File.Exists(storedPath))
                return storedPath;

            storedPath = System.IO.Path.GetFileName(storedPath);
        }

        string imagesFolder = System.IO.Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
        string fullPath = System.IO.Path.Combine(imagesFolder, storedPath);

        return System.IO.File.Exists(fullPath) ? fullPath : "";
    }
}

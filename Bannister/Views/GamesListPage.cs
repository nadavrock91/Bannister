using Bannister.Services;
using Bannister.Models;

namespace Bannister.Views;

// ViewModel for Games List with level info
public class GameListViewModel
{
    public Game Game { get; set; }
    public bool VisitedToday { get; set; }
    public int Level { get; set; }
    public string DisplayName => Game.DisplayName;
    public string LevelDisplay => $"Level {Level}";
    
    // NOT visited = highlighted (white with colored border), Visited = greyed out
    public Color BackgroundColor => VisitedToday ? Color.FromArgb("#F5F5F5") : Colors.White;
    public Color BorderColor => VisitedToday ? Color.FromArgb("#E0E0E0") : Color.FromArgb("#5B63EE");
    public Color TextColor => VisitedToday ? Color.FromArgb("#999") : Color.FromArgb("#5B63EE");
    public double CardOpacity => VisitedToday ? 0.6 : 1.0;
}

public class GamesListPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly ActivityService _activities;
    private readonly ExpService _exp;
    private readonly DatabaseService _db;
    private readonly ActivityGroupingService _groupingService;
    private bool _isNavigating = false;
    
    private FlexLayout _gamesGrid;
    private VerticalStackLayout _groupingsContainer;
    private Grid _loadingOverlay;
    private Button _restoreBtn;

    public GamesListPage(AuthService auth, GameService games, ActivityService activities, ExpService exp, DatabaseService db,
        ActivityGroupingService groupingService)
    {
        _auth = auth;
        _games = games;
        _activities = activities;
        _exp = exp;
        _db = db;
        _groupingService = groupingService;
        
        Title = "Games";
        BackgroundColor = Color.FromArgb("#6B73FF");
        
        BuildUI();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid();
        
        // Main content
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = "Games",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        mainStack.Children.Add(new Label
        {
            Text = "Select a game to play",
            TextColor = Colors.White,
            Opacity = 0.9
        });

        // Games grid container using FlexLayout for wrapping
        _gamesGrid = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Start,
            AlignContent = Microsoft.Maui.Layouts.FlexAlignContent.Start,
            Margin = new Thickness(0, 16, 0, 0)
        };
        mainStack.Children.Add(_gamesGrid);

        // Add Game button
        var btnAddGame = new Button
        {
            Text = "+ Add Game",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#5B63EE"),
            CornerRadius = 8,
            HeightRequest = 48,
            Margin = new Thickness(0, 16, 0, 0)
        };
        btnAddGame.Clicked += OnAddGameClicked;
        mainStack.Children.Add(btnAddGame);

        // ===== GROUPINGS SECTION =====
        mainStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#8B8FFF"),
            Margin = new Thickness(0, 20, 0, 8)
        });

        var groupingsHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        groupingsHeader.Children.Add(new Label
        {
            Text = "📂 Groupings",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalOptions = LayoutOptions.Center
        });

        var btnAddGrouping = new Button
        {
            Text = "+ New",
            BackgroundColor = Color.FromArgb("#E8EAF6"),
            TextColor = Color.FromArgb("#283593"),
            CornerRadius = 6,
            HeightRequest = 34,
            FontSize = 13,
            Padding = new Thickness(12, 0)
        };
        btnAddGrouping.Clicked += OnAddGroupingClicked;
        Grid.SetColumn(btnAddGrouping, 1);
        groupingsHeader.Children.Add(btnAddGrouping);

        mainStack.Children.Add(groupingsHeader);

        _groupingsContainer = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_groupingsContainer);

        // Restore Game button
        _restoreBtn = new Button
        {
            Text = "🔄 Restore Game",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#666"),
            CornerRadius = 8,
            HeightRequest = 44,
            Margin = new Thickness(0, 8, 0, 0),
            IsVisible = false
        };
        _restoreBtn.Clicked += OnRestoreGameClicked;
        mainStack.Children.Add(_restoreBtn);

        scrollView.Content = mainStack;
        mainGrid.Children.Add(scrollView);

        // Loading overlay
        _loadingOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#80000000"),
            InputTransparent = false
        };

        var loadingStack = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 16
        };

        loadingStack.Children.Add(new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.White,
            WidthRequest = 50,
            HeightRequest = 50
        });

        loadingStack.Children.Add(new Label
        {
            Text = "Loading game...",
            TextColor = Colors.White,
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center
        });

        _loadingOverlay.Children.Add(loadingStack);
        mainGrid.Children.Add(_loadingOverlay);

        Content = mainGrid;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        _isNavigating = false;
        _loadingOverlay.IsVisible = false;
        
        await LoadGamesAsync();
        await LoadGroupingsAsync();
    }

    private async Task LoadGamesAsync()
    {
        _gamesGrid.Children.Clear();
        
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);

        if (games.Count == 0)
        {
            // Check if there are inactive games first before creating defaults
            var inactiveGames = await _games.GetInactiveGamesAsync(_auth.CurrentUsername);
            if (inactiveGames.Count == 0)
            {
                await _games.CreateGameAsync(_auth.CurrentUsername, "Diet");
                games = await _games.GetGamesAsync(_auth.CurrentUsername);
            }
        }

        var gamesVisitedToday = await _games.GetGamesVisitedTodayAsync(_auth.CurrentUsername);

        foreach (var game in games)
        {
            var progress = await _exp.GetProgressAsync(_auth.CurrentUsername, game.GameId);
            var viewModel = new GameListViewModel
            {
                Game = game,
                VisitedToday = gamesVisitedToday.Contains(game.GameId),
                Level = progress.level
            };

            var frame = CreateGameCard(viewModel);
            _gamesGrid.Children.Add(frame);
        }

        // Show restore button if there are inactive games
        var inactive = await _games.GetInactiveGamesAsync(_auth.CurrentUsername);
        _restoreBtn.IsVisible = inactive.Count > 0;
        if (inactive.Count > 0)
        {
            _restoreBtn.Text = $"🔄 Restore Game ({inactive.Count} hidden)";
        }
    }

    private Frame CreateGameCard(GameListViewModel vm)
    {
        var frame = new Frame
        {
            BackgroundColor = vm.BackgroundColor,
            Padding = 12,
            CornerRadius = 12,
            BorderColor = vm.BorderColor,
            HasShadow = !vm.VisitedToday,
            WidthRequest = 140,
            HeightRequest = 100,
            Margin = new Thickness(6),
            Opacity = vm.CardOpacity
        };

        // Use Grid to overlay menu button
        var grid = new Grid();

        var stack = new VerticalStackLayout 
        { 
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center
        };

        stack.Children.Add(new Label
        {
            Text = vm.DisplayName,
            TextColor = vm.TextColor,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        });

        // Level display with checkmark if visited today
        stack.Children.Add(new Label
        {
            Text = vm.VisitedToday ? $"Level {vm.Level} ✓" : $"Level {vm.Level}",
            TextColor = vm.VisitedToday ? Color.FromArgb("#999") : Color.FromArgb("#666"),
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center
        });

        // Level progress indicator
        var levelColor = GetLevelColor(vm.Level);
        stack.Children.Add(new BoxView
        {
            Color = levelColor,
            HeightRequest = 4,
            CornerRadius = 2,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(10, 4, 10, 0)
        });

        grid.Children.Add(stack);

        // Menu button (3 dots) in top-right corner
        var menuBtn = new Button
        {
            Text = "⋮",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#999"),
            FontSize = 16,
            WidthRequest = 28,
            HeightRequest = 28,
            Padding = 0,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, -8, -8, 0)
        };
        var capturedVm = vm;
        menuBtn.Clicked += async (s, e) => await ShowGameMenuAsync(capturedVm);
        grid.Children.Add(menuBtn);

        frame.Content = grid;

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await OnGameTapped(vm);
        frame.GestureRecognizers.Add(tapGesture);

        return frame;
    }

    private async Task ShowGameMenuAsync(GameListViewModel vm)
    {
        var action = await DisplayActionSheet(
            vm.DisplayName,
            "Cancel",
            null,
            "Remove Game");

        if (action == "Remove Game")
        {
            bool confirm = await DisplayAlert(
                "Remove Game?",
                $"Remove \"{vm.DisplayName}\" from your games list?\n\nThis will NOT delete your activities or progress. You can restore it later.",
                "Remove",
                "Cancel");

            if (confirm)
            {
                await _games.RemoveGameAsync(vm.Game.Id);
                await LoadGamesAsync();
            }
        }
    }

    private async void OnRestoreGameClicked(object? sender, EventArgs e)
    {
        var inactiveGames = await _games.GetInactiveGamesAsync(_auth.CurrentUsername);
        
        if (inactiveGames.Count == 0)
        {
            await DisplayAlert("No Games", "No hidden games to restore.", "OK");
            return;
        }

        var gameNames = inactiveGames.Select(g => g.DisplayName).ToArray();
        var selected = await DisplayActionSheet("Restore Game", "Cancel", null, gameNames);

        if (!string.IsNullOrEmpty(selected) && selected != "Cancel")
        {
            var game = inactiveGames.FirstOrDefault(g => g.DisplayName == selected);
            if (game != null)
            {
                await _games.RestoreGameAsync(game.Id);
                await LoadGamesAsync();
            }
        }
    }

    private Color GetLevelColor(int level)
    {
        // Color gradient based on level
        if (level >= 50) return Color.FromArgb("#9C27B0"); // Purple - high level
        if (level >= 30) return Color.FromArgb("#2196F3"); // Blue
        if (level >= 15) return Color.FromArgb("#4CAF50"); // Green
        if (level >= 5) return Color.FromArgb("#FF9800");  // Orange
        return Color.FromArgb("#9E9E9E"); // Gray - low level
    }

    private async Task OnGameTapped(GameListViewModel vm)
    {
        if (_isNavigating) return;

        try
        {
            _isNavigating = true;
            _loadingOverlay.IsVisible = true;

            await Task.Delay(50);
            await NavigateToGameWithCatchUpAsync(vm.Game);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Navigation Error", ex.Message, "OK");
        }
        finally
        {
            _isNavigating = false;
            _loadingOverlay.IsVisible = false;
        }
    }

    private async Task NavigateToGameWithCatchUpAsync(Game game)
    {
        string encodedGameId = Uri.EscapeDataString(game.GameId);

        if (GameService.ShouldShowCatchUp(game, DateTime.Today, out _))
        {
            bool hasEligibleRows = await GameCatchUpPage.HasEligibleCatchUpActivitiesAsync(
                _auth.CurrentUsername,
                game,
                _activities,
                _exp);

            if (hasEligibleRows)
            {
                await Shell.Current.GoToAsync($"gamecatchup?gameId={encodedGameId}");
                return;
            }
        }

        await _games.UpdateLastVisitedAtAsync(_auth.CurrentUsername, game.GameId, DateTime.Now);
        await Shell.Current.GoToAsync($"activitygrid?gameId={encodedGameId}");
    }

    private async void OnAddGameClicked(object sender, EventArgs e)
    {
        string name = await DisplayPromptAsync(
            "New Game",
            "Enter game name:",
            "Create",
            "Cancel",
            placeholder: "e.g. Exercise, Learning");

        if (!string.IsNullOrWhiteSpace(name))
        {
            await _games.CreateGameAsync(_auth.CurrentUsername, name.Trim());
            await LoadGamesAsync();
        }
    }

    private async Task LoadGroupingsAsync()
    {
        _groupingsContainer.Children.Clear();

        var groupings = await _groupingService.GetGroupingsAsync(_auth.CurrentUsername);

        if (groupings.Count == 0)
        {
            _groupingsContainer.Children.Add(new Label
            {
                Text = "No groupings yet. Create one to view activities across games.",
                FontSize = 13,
                TextColor = Color.FromArgb("#C0C0FF"),
                Margin = new Thickness(0, 4, 0, 0)
            });
            return;
        }

        foreach (var grouping in groupings)
        {
            _groupingsContainer.Children.Add(BuildGroupingCard(grouping));
        }
    }

    private Frame BuildGroupingCard(ActivityGrouping grouping)
    {
        var frame = new Frame
        {
            Padding = new Thickness(16, 12),
            CornerRadius = 10,
            BackgroundColor = Color.FromArgb("#E8EAF6"),
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        var nameLabel = new Label
        {
            Text = $"{grouping.Name} ({grouping.ActivityCount})",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#283593"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(nameLabel, 0);
        grid.Children.Add(nameLabel);

        // 3-dot menu
        var btnMenu = new Button
        {
            Text = "⋮",
            FontSize = 20,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#666"),
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        int capturedId = grouping.Id;
        string capturedName = grouping.Name;
        btnMenu.Clicked += async (s, e) => await ShowGroupingMenuAsync(capturedId, capturedName);
        Grid.SetColumn(btnMenu, 2);
        grid.Children.Add(btnMenu);

        // Tap to open
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            if (_isNavigating) return;
            _isNavigating = true;
            _loadingOverlay.IsVisible = true;
            await Shell.Current.GoToAsync($"activitygrid?groupingId={capturedId}");
        };
        frame.GestureRecognizers.Add(tap);

        frame.Content = grid;
        return frame;
    }

    private async Task ShowGroupingMenuAsync(int groupingId, string name)
    {
        string action = await DisplayActionSheet(
            name,
            "Cancel",
            null,
            "✏️ Rename",
            "🗑️ Delete");

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        if (action == "✏️ Rename")
        {
            string? newName = await DisplayPromptAsync(
                "Rename Grouping",
                "Enter new name:",
                "Rename",
                "Cancel",
                initialValue: name,
                maxLength: 100);

            if (!string.IsNullOrWhiteSpace(newName))
            {
                await _groupingService.RenameGroupingAsync(groupingId, newName);
                await LoadGroupingsAsync();
            }
        }
        else if (action == "🗑️ Delete")
        {
            bool confirm = await DisplayAlert("Delete Grouping",
                $"Delete '{name}'?\n\nThis won't delete the activities, just the grouping.",
                "Delete", "Cancel");

            if (confirm)
            {
                await _groupingService.DeleteGroupingAsync(groupingId);
                await LoadGroupingsAsync();
            }
        }
    }

    private async void OnAddGroupingClicked(object? sender, EventArgs e)
    {
        string? name = await DisplayPromptAsync(
            "New Grouping",
            "Enter a name:",
            "Create",
            "Cancel",
            placeholder: "e.g., Morning Routine, Most Important",
            maxLength: 100);

        if (!string.IsNullOrWhiteSpace(name))
        {
            await _groupingService.CreateGroupingAsync(_auth.CurrentUsername, name);
            await LoadGroupingsAsync();
        }
    }
}

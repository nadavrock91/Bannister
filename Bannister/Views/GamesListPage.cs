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
    private readonly ExpService _exp;
    private readonly DatabaseService _db;
    private bool _isNavigating = false;
    
    private FlexLayout _gamesGrid;
    private Grid _loadingOverlay;
    private Button _restoreBtn;

    public GamesListPage(AuthService auth, GameService games, ExpService exp, DatabaseService db)
    {
        _auth = auth;
        _games = games;
        _exp = exp;
        _db = db;
        
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
            await Shell.Current.GoToAsync($"activitygrid?gameId={vm.Game.GameId}");
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
}

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
    public Color BackgroundColor => VisitedToday ? Color.FromArgb("#E8F5E9") : Colors.White;
    public Color BorderColor => VisitedToday ? Color.FromArgb("#4CAF50") : Colors.Transparent;
}

public class GamesListPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly ExpService _exp;
    private readonly DatabaseService _db;
    private bool _isNavigating = false;
    
    private VerticalStackLayout _gamesStackLayout;
    private Grid _loadingOverlay;

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

        // Games list container
        _gamesStackLayout = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 16, 0, 0)
        };
        mainStack.Children.Add(_gamesStackLayout);

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
        _gamesStackLayout.Children.Clear();
        
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);

        if (games.Count == 0)
        {
            await _games.CreateGameAsync(_auth.CurrentUsername, "Conversation Practice");
            await _games.CreateGameAsync(_auth.CurrentUsername, "Diet");
            games = await _games.GetGamesAsync(_auth.CurrentUsername);
        }

        var gamesVisitedToday = await GetGamesVisitedTodayAsync();

        foreach (var game in games)
        {
            var progress = await _exp.GetProgressAsync(_auth.CurrentUsername, game.GameId);
            var viewModel = new GameListViewModel
            {
                Game = game,
                VisitedToday = gamesVisitedToday.Contains(game.GameId),
                Level = progress.level
            };

            var frame = CreateGameFrame(viewModel);
            _gamesStackLayout.Children.Add(frame);
        }
    }

    private Frame CreateGameFrame(GameListViewModel vm)
    {
        var frame = new Frame
        {
            BackgroundColor = vm.BackgroundColor,
            Padding = 16,
            CornerRadius = 8,
            BorderColor = vm.BorderColor,
            HasShadow = true
        };

        var stack = new VerticalStackLayout { Spacing = 4 };

        stack.Children.Add(new Label
        {
            Text = vm.DisplayName,
            TextColor = Color.FromArgb("#5B63EE"),
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center
        });

        stack.Children.Add(new Label
        {
            Text = vm.LevelDisplay,
            TextColor = Color.FromArgb("#666"),
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center
        });

        frame.Content = stack;

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await OnGameTapped(vm);
        frame.GestureRecognizers.Add(tapGesture);

        return frame;
    }

    private async Task<HashSet<string>> GetGamesVisitedTodayAsync()
    {
        var visited = new HashSet<string>();
        
        try
        {
            var today = DateTime.Now.Date;
            var tomorrow = today.AddDays(1);
            
            var games = await _games.GetGamesAsync(_auth.CurrentUsername);
            
            foreach (var game in games)
            {
                var logs = await _db.GetExpLogsForDateRangeAsync(
                    _auth.CurrentUsername, 
                    game.GameId, 
                    today, 
                    tomorrow);
                
                if (logs.Count > 0)
                {
                    visited.Add(game.GameId);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking visited games: {ex.Message}");
        }
        
        return visited;
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

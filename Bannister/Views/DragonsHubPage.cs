using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Hub page for all dragon-related pages
/// </summary>
public class DragonsHubPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DragonService _dragons;
    private readonly AttemptService _attempts;

    private Label _activeCountLabel;
    private Label _slainCountLabel;
    private Label _irrelevantCountLabel;

    public DragonsHubPage(AuthService auth, DragonService dragons, AttemptService attempts)
    {
        _auth = auth;
        _dragons = dragons;
        _attempts = attempts;

        Title = "Dragons";
        BackgroundColor = Color.FromArgb("#F5F7FC");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCountsAsync();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 20
        };

        // Header
        var headerFrame = new Frame
        {
            Padding = 20,
            CornerRadius = 16,
            BackgroundColor = Color.FromArgb("#5B63EE"),
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 8 };
        headerStack.Children.Add(new Label
        {
            Text = "🐉 Dragons",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        headerStack.Children.Add(new Label
        {
            Text = "Track your quests to slay dragons (reach Level 100)",
            FontSize = 14,
            TextColor = Color.FromArgb("#FFFFFFCC")
        });
        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Active Dragons button
        var activeFrame = CreateMenuButton(
            "⚔️ Active Dragons",
            "Dragons you're currently battling",
            Color.FromArgb("#4CAF50"),
            async () => await NavigateToActiveDragonsAsync()
        );
        _activeCountLabel = new Label
        {
            Text = "Loading...",
            FontSize = 14,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };
        ((Grid)((Frame)activeFrame).Content).Children.Add(_activeCountLabel);
        Grid.SetColumn(_activeCountLabel, 1);
        mainStack.Children.Add(activeFrame);

        // Slain Dragons button
        var slainFrame = CreateMenuButton(
            "🏆 Slain Dragons",
            "Dragons you've defeated (reached Level 100)",
            Color.FromArgb("#FF9800"),
            async () => await NavigateToSlainDragonsAsync()
        );
        _slainCountLabel = new Label
        {
            Text = "Loading...",
            FontSize = 14,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };
        ((Grid)((Frame)slainFrame).Content).Children.Add(_slainCountLabel);
        Grid.SetColumn(_slainCountLabel, 1);
        mainStack.Children.Add(slainFrame);

        // Irrelevant Dragons button
        var irrelevantFrame = CreateMenuButton(
            "💤 Irrelevant Dragons",
            "Dragons you've decided not to pursue",
            Color.FromArgb("#9E9E9E"),
            async () => await NavigateToIrrelevantDragonsAsync()
        );
        _irrelevantCountLabel = new Label
        {
            Text = "Loading...",
            FontSize = 14,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };
        ((Grid)((Frame)irrelevantFrame).Content).Children.Add(_irrelevantCountLabel);
        Grid.SetColumn(_irrelevantCountLabel, 1);
        mainStack.Children.Add(irrelevantFrame);

        // Add Dragon button
        var addDragonBtn = new Button
        {
            Text = "➕ Add New Dragon",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 12,
            HeightRequest = 50,
            Margin = new Thickness(0, 20, 0, 0)
        };
        addDragonBtn.Clicked += OnAddDragonClicked;
        mainStack.Children.Add(addDragonBtn);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private Frame CreateMenuButton(string title, string subtitle, Color bgColor, Func<Task> onTap)
    {
        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = bgColor,
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        var textStack = new VerticalStackLayout { Spacing = 4 };
        textStack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });
        textStack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 12,
            TextColor = Color.FromArgb("#FFFFFFAA")
        });
        Grid.SetColumn(textStack, 0);
        grid.Children.Add(textStack);

        frame.Content = grid;

        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await onTap();
        frame.GestureRecognizers.Add(tapGesture);

        return frame;
    }

    private async Task LoadCountsAsync()
    {
        try
        {
            var activeDragons = await _dragons.GetActiveDragonsAsync(_auth.CurrentUsername);
            var slainDragons = await _dragons.GetSlainDragonsAsync(_auth.CurrentUsername);
            var irrelevantDragons = await _dragons.GetIrrelevantDragonsAsync(_auth.CurrentUsername);

            _activeCountLabel.Text = $"{activeDragons.Count} →";
            _slainCountLabel.Text = $"{slainDragons.Count} →";
            _irrelevantCountLabel.Text = $"{irrelevantDragons.Count} →";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading dragon counts: {ex.Message}");
            _activeCountLabel.Text = "→";
            _slainCountLabel.Text = "→";
            _irrelevantCountLabel.Text = "→";
        }
    }

    private async Task NavigateToActiveDragonsAsync()
    {
        var page = new ActiveDragonsPage(_auth, _dragons, _attempts);
        await Navigation.PushAsync(page);
    }

    private async Task NavigateToSlainDragonsAsync()
    {
        var page = new SlainDragonsPage(_auth, _dragons, _attempts);
        await Navigation.PushAsync(page);
    }

    private async Task NavigateToIrrelevantDragonsAsync()
    {
        var page = new IrrelevantDragonsPage(_auth, _dragons, _attempts);
        await Navigation.PushAsync(page);
    }

    private async void OnAddDragonClicked(object? sender, EventArgs e)
    {
        // Prompt for dragon title
        string? title = await DisplayPromptAsync(
            "Add New Dragon",
            "Enter a name for your dragon (the goal you want to achieve):",
            placeholder: "e.g., Master Spanish, Run a Marathon");

        if (string.IsNullOrWhiteSpace(title))
            return;

        // Ask if they want to assign to a game or keep it standalone
        string choice = await DisplayActionSheet(
            "Assign to Game?",
            "Cancel",
            null,
            "🚫 No Game (Standalone Dragon)",
            "📁 Select a Game");

        if (choice == "Cancel" || string.IsNullOrEmpty(choice))
            return;

        string gameId;

        if (choice.StartsWith("🚫"))
        {
            gameId = "_standalone_";
        }
        else
        {
            var games = await GetAllGamesAsync();
            
            if (games.Count == 0)
            {
                await DisplayAlert("No Games", "You don't have any games yet. Create a game first, or choose 'No Game' for a standalone dragon.", "OK");
                return;
            }

            var gameNames = games.Select(g => g.DisplayName).ToArray();
            string? selectedGame = await DisplayActionSheet("Select Game", "Cancel", null, gameNames);
            
            if (selectedGame == "Cancel" || string.IsNullOrEmpty(selectedGame))
                return;

            var game = games.FirstOrDefault(g => g.DisplayName == selectedGame);
            if (game == null)
                return;

            gameId = game.GameId;
        }

        // Ask for optional image
        string? imagePath = null;
        bool pickImage = await DisplayAlert(
            "Dragon Image",
            "Would you like to pick an image for this dragon?",
            "Pick Image",
            "Skip (Use Default)");

        if (pickImage)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select dragon image",
                    FileTypes = FilePickerFileType.Images
                });

                if (result != null)
                {
                    string destFolder = Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
                    Directory.CreateDirectory(destFolder);

                    string filename = $"dragon_{DateTime.Now.Ticks}{Path.GetExtension(result.FileName)}";
                    string fullPath = Path.Combine(destFolder, filename);

                    using var sourceStream = await result.OpenReadAsync();
                    using var destStream = File.Create(fullPath);
                    await sourceStream.CopyToAsync(destStream);

                    imagePath = filename;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error picking image: {ex.Message}");
            }
        }

        // Create the dragon
        var dragon = new Dragon
        {
            Username = _auth.CurrentUsername,
            Game = gameId,
            Title = title.Trim(),
            ImagePath = imagePath ?? "",
            SlainAt = null,
            CreatedAt = DateTime.UtcNow
        };

        var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
        if (dbService != null)
        {
            var conn = await dbService.GetConnectionAsync();
            await conn.InsertAsync(dragon);
        }

        await DisplayAlert("Dragon Created!", $"'{title}' has been added to your dragons.\n\nGo to Active Dragons to start an attempt!", "OK");
        await LoadCountsAsync();
    }

    private async Task<List<Game>> GetAllGamesAsync()
    {
        var dbService = Application.Current?.Handler?.MauiContext?.Services.GetService<DatabaseService>();
        if (dbService != null)
        {
            var conn = await dbService.GetConnectionAsync();
            return await conn.Table<Game>()
                .Where(g => g.Username == _auth.CurrentUsername)
                .ToListAsync();
        }
        return new List<Game>();
    }
}

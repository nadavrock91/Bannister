using Bannister.Services;
using Bannister.Models;
using Bannister.ViewModels;
using Bannister.Helpers;
using Microsoft.Maui.Controls;
using ConversationPractice.Views;
using ConversationPractice.Services;

namespace Bannister.Views;

/// <summary>
/// Main game page for displaying and managing activities in a gamified interface
/// </summary>
[QueryProperty(nameof(GameId), "gameId")]
public partial class ActivityGamePage : ContentPage
{
    // Services
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly ActivityService _activities;
    private readonly ExpService _exp;
    private readonly DragonService _dragons;
    private readonly AttemptService _attempts;
    private readonly DatabaseService _db;
    private readonly StreakService _streaks;

    // State
    private string _gameId = "";
    private Game? _game;
    private int _currentLevel = 1;
    private List<ActivityGameViewModel> _allActivities = new();
    private List<string> _categories = new();
    private int _currentCategoryIndex = 0;
    private string _currentMetaFilter = "All Activities";
    private bool _isInitialLoad = true; // Track if page has been loaded already
    private bool _showAllActivities = false; // When true, bypass display day filtering
    private Button btnShowAll;

    // Chart data and views
    private GraphicsView? _expChartView;
    private GraphicsView? _levelChartView;
    private List<ChartDataPoint> _expChartData = new();
    private List<ChartDataPoint> _levelChartData = new();

    // UI Controls
    private Label lblGameTitle;
    private Label lblExpTotal;
    private Label lblCurrentLevel;
    private Label lblExpToNext;
    private ProgressBar expProgressBar;
    private Image imgDragon;
    private Label lblDragonTitle;
    private Label lblDragonSubtitle;
    private Label lblDragonDesc;
    private Button btnDefineDragon;
    private Button btnCalculateExp;
    private Button btnPrevPage;
    private Button btnNextPage;
    private Label lblPageInfo;
    private Picker categoryPicker;
    private Picker metaFilterPicker;
    private Picker sortPicker;
    private ScrollView activitiesCollection;

    public string GameId
    {
        get => _gameId;
        set
        {
            System.Diagnostics.Debug.WriteLine($">>> GameId SETTER called with value: '{value}'");
            _gameId = value;
            OnPropertyChanged();
        }
    }

    public ActivityGamePage(AuthService auth, GameService games, ActivityService activities,
        ExpService exp, DragonService dragons, AttemptService attempts, DatabaseService db, StreakService streaks)
    {
        _auth = auth;
        _games = games;
        _activities = activities;
        _exp = exp;
        _dragons = dragons;
        _attempts = attempts;
        _db = db;
        _streaks = streaks;

        Title = "Game";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        try
        {
            BuildUI();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR building UI: {ex.Message}");
            throw;
        }
    }

    private void BuildUI()
    {
        bool isMobile = DeviceInfo.Current.Idiom == DeviceIdiom.Phone || 
                        DeviceInfo.Current.Idiom == DeviceIdiom.Tablet;

        if (isMobile)
        {
            BuildMobileUI();
        }
        else
        {
            BuildDesktopUI();
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        try
        {
            // Only do full load on first appearance
            // When returning from modal pages (like SetMultiplier), skip reload
            if (_isInitialLoad)
            {
                await LoadGameAsync();
                InjectConversationButtonIfNeeded();
                _isInitialLoad = false; // Mark as loaded
            }
            else
            {
                // Check if categories have changed (e.g., streak container created/removed)
                var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _game?.GameId ?? "");
                var currentCategoryCount = allActivities
                    .Select(a => a.Category ?? "Misc")
                    .Where(c => !c.Equals("Expired", StringComparison.OrdinalIgnoreCase) 
                             && !c.Equals("Stale", StringComparison.OrdinalIgnoreCase))
                    .Distinct()
                    .Count();
                
                // If category count changed, reload categories while preserving position
                if (currentCategoryCount != _categories.Count)
                {
                    string? currentCategory = _currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count 
                        ? _categories[_currentCategoryIndex] 
                        : null;
                    
                    await LoadCategoriesAsync();
                    
                    // Try to stay on the same category
                    if (!string.IsNullOrEmpty(currentCategory) && _categories.Contains(currentCategory))
                    {
                        _currentCategoryIndex = _categories.IndexOf(currentCategory);
                        UpdateCategoryDisplay();
                    }
                }
                
                // Refresh EXP and activities
                await RefreshExpAsync();
                await RefreshActivitiesAsync();
                await RefreshLevelCapsPanelAsync(); // Refresh level caps panel after edit
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in OnAppearing: {ex.Message}");
            await DisplayAlert("Error", $"Failed to load game: {ex.Message}", "OK");
            await Shell.Current.GoToAsync("..");
        }
    }

    private void InjectConversationButtonIfNeeded()
    {
        if (GameId != "conversation_practice")
            return;
        
        if (Content != null)
        {
            var buttonStack = FindButtonStack(Content);
            
            if (buttonStack != null)
            {
                bool alreadyExists = buttonStack.Children.OfType<Button>()
                    .Any(b => b.Text?.Contains("Conversation Practice") == true);
                
                if (!alreadyExists)
                {
                    var btnConversation = new Button
                    {
                        Text = "💬 Conversation Practice",
                        BackgroundColor = Color.FromArgb("#9C27B0"),
                        TextColor = Colors.White,
                        CornerRadius = 8,
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold
                    };
                    btnConversation.Clicked += OnConversationPracticeClicked;
                    buttonStack.Children.Insert(0, btnConversation);
                }
            }
        }
    }

    private VerticalStackLayout? FindButtonStack(Element element)
    {
        if (element is VerticalStackLayout vstack)
        {
            // Look for the button panel by finding "Clear Selection" button
            // (Calculate EXP was moved to dragon card, so we can't use that anymore)
            var hasClearButton = vstack.Children.OfType<Button>()
                .Any(b => b.Text?.Contains("Clear Selection") == true);
            
            if (hasClearButton)
                return vstack;
        }

        if (element is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                if (child is Element childElement)
                {
                    var result = FindButtonStack(childElement);
                    if (result != null)
                        return result;
                }
            }
        }
        else if (element is ScrollView scrollView && scrollView.Content is Element scrollContent)
        {
            return FindButtonStack(scrollContent);
        }
        else if (element is ContentView contentView && contentView.Content is Element content)
        {
            return FindButtonStack(content);
        }
        else if (element is Frame frame && frame.Content is Element frameContent)
        {
            return FindButtonStack(frameContent);
        }

        return null;
    }
}

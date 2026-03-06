using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

[QueryProperty(nameof(GameId), "gameId")]
public partial class ExpLogPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DatabaseService _db;
    private string _gameId = string.Empty;

    public string GameId
    {
        get => _gameId;
        set
        {
            _gameId = value;
            OnPropertyChanged();
            System.Diagnostics.Debug.WriteLine($"ExpLogPage: GameId set to '{_gameId}'");
        }
    }

    public ExpLogPage(AuthService auth, DatabaseService db)
    {
        InitializeComponent();
        _auth = auth;
        _db = db;
        
        System.Diagnostics.Debug.WriteLine("ExpLogPage: Constructor called");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        System.Diagnostics.Debug.WriteLine($"ExpLogPage: OnAppearing - GameId = '{_gameId}'");
        await LoadLogAsync();
    }

    private async Task LoadLogAsync()
    {
        if (string.IsNullOrEmpty(_gameId))
        {
            System.Diagnostics.Debug.WriteLine("ExpLogPage: GameId is empty, cannot load logs");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"ExpLogPage: Loading logs for user '{_auth.CurrentUsername}', game '{_gameId}'");

        // Get stats
        var totalExp = await _db.GetTotalExpForGameAsync(_auth.CurrentUsername, _gameId);
        var todayExp = await _db.GetExpEarnedTodayAsync(_auth.CurrentUsername, _gameId);
        var weekExp = await _db.GetExpEarnedThisWeekAsync(_auth.CurrentUsername, _gameId);

        System.Diagnostics.Debug.WriteLine($"ExpLogPage: Stats - Total: {totalExp}, Today: {todayExp}, Week: {weekExp}");

        lblTotalExp.Text = totalExp.ToString("N0");
        lblTodayExp.Text = todayExp.ToString("N0");
        lblWeekExp.Text = weekExp.ToString("N0");

        // Get logs
        var logs = await _db.GetExpLogsForGameAsync(_auth.CurrentUsername, _gameId);
        System.Diagnostics.Debug.WriteLine($"ExpLogPage: Retrieved {logs.Count} log entries");

        if (logs.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"ExpLogPage: First log entry: {logs[0].ActivityName}, {logs[0].DeltaExp} EXP");
        }
        
        // Create view models
        var viewModels = logs.Select(log => new ExpLogViewModel
        {
            ActivityName = log.ActivityName,
            ExpEarned = log.DeltaExp,
            ExecutedAt = log.LoggedAt,
            LevelBefore = log.LevelBefore,
            LevelAfter = log.LevelAfter
        }).ToList();

        System.Diagnostics.Debug.WriteLine($"ExpLogPage: Created {viewModels.Count} view models");

        logList.ItemsSource = viewModels;
        
        System.Diagnostics.Debug.WriteLine("ExpLogPage: Logs loaded successfully");
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}

public class ExpLogViewModel
{
    public string ActivityName { get; set; } = string.Empty;
    public int ExpEarned { get; set; }
    public DateTime ExecutedAt { get; set; }
    public int LevelBefore { get; set; }
    public int LevelAfter { get; set; }

    public bool LeveledUp => LevelAfter > LevelBefore;
    
    public string LevelChangeText => LeveledUp 
        ? $"Level {LevelBefore} → {LevelAfter} 🎉" 
        : $"Level {LevelAfter}";
}

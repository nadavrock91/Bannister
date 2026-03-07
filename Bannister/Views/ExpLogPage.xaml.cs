using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

[QueryProperty(nameof(GameId), "gameId")]
public partial class ExpLogPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DatabaseService _db;
    private string _gameId = string.Empty;
    private List<ExpLogViewModel> _viewModels = new();

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

        await RefreshStatsAsync();

        // Get logs
        var logs = await _db.GetExpLogsForGameAsync(_auth.CurrentUsername, _gameId);
        System.Diagnostics.Debug.WriteLine($"ExpLogPage: Retrieved {logs.Count} log entries");

        if (logs.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"ExpLogPage: First log entry: {logs[0].ActivityName}, {logs[0].DeltaExp} EXP");
        }
        
        // Create view models with Id for deletion
        _viewModels = logs.Select(log => new ExpLogViewModel
        {
            Id = log.Id,
            ActivityName = log.ActivityName,
            ExpEarned = log.DeltaExp,
            ExecutedAt = log.LoggedAt,
            LevelBefore = log.LevelBefore,
            LevelAfter = log.LevelAfter
        }).ToList();

        System.Diagnostics.Debug.WriteLine($"ExpLogPage: Created {_viewModels.Count} view models");

        logList.ItemsSource = _viewModels;
        
        System.Diagnostics.Debug.WriteLine("ExpLogPage: Logs loaded successfully");
    }

    private async Task RefreshStatsAsync()
    {
        var totalExp = await _db.GetTotalExpForGameAsync(_auth.CurrentUsername, _gameId);
        var todayExp = await _db.GetExpEarnedTodayAsync(_auth.CurrentUsername, _gameId);
        var weekExp = await _db.GetExpEarnedThisWeekAsync(_auth.CurrentUsername, _gameId);

        System.Diagnostics.Debug.WriteLine($"ExpLogPage: Stats - Total: {totalExp}, Today: {todayExp}, Week: {weekExp}");

        lblTotalExp.Text = totalExp.ToString("N0");
        lblTodayExp.Text = todayExp.ToString("N0");
        lblWeekExp.Text = weekExp.ToString("N0");
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is ExpLogViewModel vm)
        {
            await DeleteLogEntryAsync(vm);
        }
    }

    private async Task DeleteLogEntryAsync(ExpLogViewModel vm)
    {
        bool confirm = await DisplayAlert(
            "Delete Entry?",
            $"Delete '{vm.ActivityName}' ({vm.ExpEarned:+#;-#;0} EXP)?\n\nThis will remove the EXP from your total.",
            "Delete",
            "Cancel");

        if (!confirm) return;

        try
        {
            var conn = await _db.GetConnectionAsync();
            
            // Get the log entry
            var logEntry = await conn.Table<ExpLog>().FirstOrDefaultAsync(l => l.Id == vm.Id);
            if (logEntry == null)
            {
                await DisplayAlert("Error", "Log entry not found.", "OK");
                return;
            }

            // Get the game's ExpState to reduce total EXP
            var expState = await conn.Table<ExpState>()
                .FirstOrDefaultAsync(e => e.Username == _auth.CurrentUsername && e.Game == _gameId);

            if (expState != null)
            {
                // Subtract the EXP (handle both positive and negative entries)
                expState.TotalExp -= logEntry.DeltaExp;
                expState.UpdatedAt = DateTime.UtcNow;
                
                await conn.UpdateAsync(expState);
                System.Diagnostics.Debug.WriteLine($"[DELETE LOG] Reduced TotalExp by {logEntry.DeltaExp}, new total: {expState.TotalExp}");
            }

            // Delete the log entry
            await conn.DeleteAsync(logEntry);
            System.Diagnostics.Debug.WriteLine($"[DELETE LOG] Deleted log entry Id={vm.Id}");

            // Remove from UI
            _viewModels.Remove(vm);
            logList.ItemsSource = null;
            logList.ItemsSource = _viewModels;

            // Refresh stats
            await RefreshStatsAsync();

            await DisplayAlert("Deleted", $"Removed {vm.ExpEarned:+#;-#;0} EXP from your total.", "OK");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DELETE LOG] Error: {ex.Message}");
            await DisplayAlert("Error", $"Failed to delete: {ex.Message}", "OK");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}

public class ExpLogViewModel
{
    public int Id { get; set; }
    public string ActivityName { get; set; } = string.Empty;
    public int ExpEarned { get; set; }
    public DateTime ExecutedAt { get; set; }
    public int LevelBefore { get; set; }
    public int LevelAfter { get; set; }

    public bool LeveledUp => LevelAfter > LevelBefore;
    
    public string LevelChangeText => LeveledUp 
        ? $"Level {LevelBefore} → {LevelAfter} 🎉" 
        : $"Level {LevelAfter}";
        
    public string ExpText => ExpEarned >= 0 ? $"+{ExpEarned} EXP" : $"{ExpEarned} EXP";
    public string ExpColor => ExpEarned >= 0 ? "#4CAF50" : "#F44336";
}

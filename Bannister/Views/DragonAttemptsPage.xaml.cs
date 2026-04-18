using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

[QueryProperty(nameof(GameId), "game")]
[QueryProperty(nameof(DragonTitle), "dragon")]
public partial class DragonAttemptsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly AttemptService _attempts;
    
    private string _gameId = "";
    private string _dragonTitle = "";

    public string GameId
    {
        get => _gameId;
        set { _gameId = value; OnPropertyChanged(); }
    }

    public string DragonTitle
    {
        get => _dragonTitle;
        set { _dragonTitle = value; OnPropertyChanged(); }
    }

    public DragonAttemptsPage(AuthService auth, AttemptService attempts)
    {
        InitializeComponent();
        _auth = auth;
        _attempts = attempts;
        
        // Wire up button click handler
        btnAction.Clicked += OnActionClicked;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        lblDragonTitle.Text = DragonTitle;
        
        var attempts = await _attempts.GetAttemptsForDragonAsync(
            _auth.CurrentUsername, GameId, DragonTitle);
        
        var activeAttempt = attempts.FirstOrDefault(a => a.IsActive);
        
        if (activeAttempt != null)
        {
            int days = (int)(DateTime.UtcNow - (activeAttempt.StartedAt ?? DateTime.UtcNow)).TotalDays;
            lblCurrentStatus.Text = $"🟢 Attempt {activeAttempt.AttemptNumber} Active\n{days} days and counting!";
            lblCurrentStatus.TextColor = Color.FromArgb("#4CAF50");
            
            btnAction.Text = "❌ Mark as Failed";
            btnAction.BackgroundColor = Color.FromArgb("#F44336");
            btnAction.TextColor = Colors.White;
        }
        else
        {
            int attemptCount = attempts.Count;
            lblCurrentStatus.Text = attemptCount > 0
                ? $"⚪ No active attempt\n{attemptCount} previous attempt(s)"
                : "⚪ No attempts yet\nStart your first attempt!";
            lblCurrentStatus.TextColor = Color.FromArgb("#999");
            
            btnAction.Text = attemptCount > 0 ? "🚀 Start New Attempt" : "🚀 Start First Attempt";
            btnAction.BackgroundColor = Color.FromArgb("#5B63EE");
            btnAction.TextColor = Colors.White;
        }
        
        historyList.ItemsSource = attempts.Select(a => new AttemptHistoryViewModel(a)).ToList();
    }

    private async void OnActionClicked(object? sender, EventArgs e)
    {
        var activeAttempt = await _attempts.GetActiveAttemptAsync(
            _auth.CurrentUsername, GameId, DragonTitle);
        
        if (activeAttempt != null)
        {
            // Mark as failed
            bool confirm = await DisplayAlert(
                "Mark as Failed",
                $"Mark Attempt {activeAttempt.AttemptNumber} as failed?\n\n" +
                $"Duration: {activeAttempt.DurationDisplay}\n\n" +
                "You can start a new attempt after this.",
                "Mark Failed",
                "Cancel");
            
            if (confirm)
            {
                await _attempts.MarkAttemptFailedAsync(_auth.CurrentUsername, GameId, DragonTitle);
                await LoadDataAsync();
            }
        }
        else
        {
            // Start new attempt
            int nextNum = await _attempts.GetNextAttemptNumberAsync(
                _auth.CurrentUsername, GameId, DragonTitle);
            
            bool confirm = await DisplayAlert(
                "Start New Attempt",
                $"Start Attempt {nextNum} for {DragonTitle}?\n\nThe timer will begin now.",
                "Start",
                "Cancel");
            
            if (confirm)
            {
                await _attempts.StartNewAttemptAsync(_auth.CurrentUsername, GameId, DragonTitle);
                await LoadDataAsync();
            }
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}

public class AttemptHistoryViewModel
{
    private readonly Attempt _attempt;

    public AttemptHistoryViewModel(Attempt attempt)
    {
        _attempt = attempt;
    }

    public string AttemptTitle => $"Attempt {_attempt.AttemptNumber}";
    
    public string StatusIcon
    {
        get
        {
            if (_attempt.IsActive) return "🟢";
            if (_attempt.FailedAt.HasValue) return "🔴";
            return "✅";
        }
    }

    public Color BackgroundColor
    {
        get
        {
            if (_attempt.IsActive) return Color.FromArgb("#E8F5E9");
            if (_attempt.FailedAt.HasValue) return Color.FromArgb("#FFEBEE");
            return Color.FromArgb("#F5F5F5");
        }
    }

    public string DurationText
    {
        get
        {
            if (!_attempt.StartedAt.HasValue)
                return "Not started";
            
            if (_attempt.IsActive)
                return $"{_attempt.DurationDisplay} - Still going!";
            
            if (_attempt.FailedAt.HasValue)
                return $"{_attempt.DurationDisplay} - Failed on {_attempt.FailedAt.Value.ToLocalTime():MMM dd, yyyy}";
            
            return $"{_attempt.DurationDisplay} - Completed";
        }
    }
}

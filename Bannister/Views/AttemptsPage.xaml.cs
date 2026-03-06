using Bannister.Models;
using Bannister.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bannister.Views;

public partial class AttemptsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly AttemptService _attempts;
    private readonly DragonService _dragons;

    public AttemptsPage(AuthService auth, AttemptService attempts, DragonService dragons)
    {
        InitializeComponent();
        _auth = auth;
        _attempts = attempts;
        _dragons = dragons;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAttemptsAsync();
    }

    private async Task LoadAttemptsAsync()
    {
        var latestAttempts = await _attempts.GetLatestAttemptPerDragonAsync(_auth.CurrentUsername);
        
        // Also get dragons without any attempts
        var activeDragons = await _dragons.GetActiveDragonsAsync(_auth.CurrentUsername);
        var dragonsWithAttempts = latestAttempts.Select(a => a.DragonTitle).ToHashSet();
        var dragonsWithoutAttempts = activeDragons
            .Where(d => !dragonsWithAttempts.Contains(d.Title))
            .ToList();

        // Create view models
        var viewModels = new List<AttemptListItemViewModel>();

        foreach (var attempt in latestAttempts)
        {
            viewModels.Add(new AttemptListItemViewModel(attempt));
        }

        foreach (var dragon in dragonsWithoutAttempts)
        {
            viewModels.Add(new AttemptListItemViewModel(dragon));
        }

        attemptsList.ItemsSource = viewModels.OrderByDescending(vm => vm.SortDate).ToList();
    }

    private async void OnAttemptTapped(object sender, EventArgs e)
    {
        if (sender is Frame frame && frame.BindingContext is AttemptListItemViewModel vm)
        {
            await Shell.Current.GoToAsync($"dragonattempts?game={vm.Game}&dragon={vm.DragonTitle}");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}

/// <summary>
/// View model for attempt list items - handles both actual attempts and dragons without attempts
/// </summary>
public class AttemptListItemViewModel : INotifyPropertyChanged
{
    private readonly Attempt? _attempt;
    private readonly Dragon? _dragon;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AttemptListItemViewModel(Attempt attempt)
    {
        _attempt = attempt;
    }

    public AttemptListItemViewModel(Dragon dragon)
    {
        _dragon = dragon;
    }

    public string DragonTitle => _attempt?.DragonTitle ?? _dragon?.Title ?? "";
    public string Game => _attempt?.Game ?? _dragon?.Game ?? "";

    public string StatusText
    {
        get
        {
            if (_attempt != null)
            {
                if (_attempt.IsActive)
                    return $"Attempt {_attempt.AttemptNumber} - Active";
                else if (_attempt.FailedAt.HasValue)
                    return $"Attempt {_attempt.AttemptNumber} - Failed";
                else
                    return $"Attempt {_attempt.AttemptNumber}";
            }
            return "No Attempts";
        }
    }

    public Color StatusColor
    {
        get
        {
            if (_attempt != null)
            {
                if (_attempt.IsActive)
                    return Color.FromArgb("#4CAF50"); // Green
                else if (_attempt.FailedAt.HasValue)
                    return Color.FromArgb("#F44336"); // Red
                else
                    return Color.FromArgb("#999"); // Gray
            }
            return Color.FromArgb("#999"); // Gray for no attempts
        }
    }

    public string InfoText
    {
        get
        {
            if (_attempt != null)
            {
                if (_attempt.IsActive && _attempt.StartedAt.HasValue)
                {
                    int days = (int)(DateTime.UtcNow - _attempt.StartedAt.Value).TotalDays;
                    return days == 0 ? "Started today" : days == 1 ? "1 day" : $"{days} days";
                }
                else if (_attempt.FailedAt.HasValue && _attempt.StartedAt.HasValue)
                {
                    int days = (int)(_attempt.FailedAt.Value - _attempt.StartedAt.Value).TotalDays;
                    return $"Failed after {(days == 0 ? "< 1" : days.ToString())} day(s)";
                }
                return "Completed";
            }
            return "No attempts yet - tap to start";
        }
    }

    public DateTime SortDate => _attempt?.StartedAt ?? _dragon?.CreatedAt ?? DateTime.MinValue;

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

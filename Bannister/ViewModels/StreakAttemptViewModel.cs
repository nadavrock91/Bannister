using System.ComponentModel;
using System.Runtime.CompilerServices;
using Bannister.Models;

namespace Bannister.ViewModels;

/// <summary>
/// ViewModel for displaying a streak attempt as a card in the main game view.
/// </summary>
public class StreakAttemptViewModel : INotifyPropertyChanged
{
    private readonly StreakAttempt _attempt;
    private readonly Activity _parentActivity;
    private bool _isSelected;
    private int _currentLevel;

    public StreakAttemptViewModel(StreakAttempt attempt, Activity parentActivity)
    {
        _attempt = attempt;
        _parentActivity = parentActivity;
    }

    public int AttemptId => _attempt.Id;
    public int ActivityId => _parentActivity.Id;
    public int AttemptNumber => _attempt.AttemptNumber;
    public string Name => $"Attempt {_attempt.AttemptNumber}";
    public string ActivityName => _parentActivity.Name;
    public string Category => _parentActivity.Name;

    public int DaysAchieved => _attempt.DaysAchieved;
    public string DaysDisplay => _attempt.DaysAchieved.ToString();
    public bool IsActive => _attempt.IsActive;
    public string Status => _attempt.Status;
    public string DateRange => _attempt.DateRangeDisplay;
    public DateTime? StartedAt => _attempt.StartedAt;
    public DateTime? EndedAt => _attempt.EndedAt;
    public DateTime? LastUsedDate => _attempt.LastUsedDate;

    public int CurrentLevel
    {
        get => _currentLevel;
        set
        {
            if (_currentLevel != value)
            {
                _currentLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpGain));
                OnPropertyChanged(nameof(ExpGainDisplay));
            }
        }
    }
    
    public int ExpGain
    {
        get
        {
            if (_parentActivity.RewardType == "PercentOfLevel")
            {
                int effectiveLevel = Math.Min(_currentLevel, _parentActivity.PercentCutoffLevel);
                return Services.ExpEngine.ExpForPercentOfLevel(effectiveLevel, _parentActivity.PercentOfLevel);
            }
            return _parentActivity.ExpGain;
        }
    }
    
    public string ExpGainDisplay => $"+{ExpGain}";
    public int Multiplier => _parentActivity.Multiplier;
    public bool ShowMultiplier => _parentActivity.Multiplier > 1;
    public string ImagePath => _parentActivity.ImagePath;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    public StreakAttempt GetAttempt() => _attempt;
    public Activity GetActivity() => _parentActivity;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

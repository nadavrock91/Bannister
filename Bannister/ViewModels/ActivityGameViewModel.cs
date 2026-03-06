using Bannister.Models;
using Bannister.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Bannister.ViewModels;

/// <summary>
/// Wrapper class to add UI-specific properties to Activity model
/// </summary>
public class ActivityGameViewModel : INotifyPropertyChanged
{
    private Activity _activity;
    private bool _isSelected;
    private DateTime? _lastUsedDate;
    private string _sectionHeader;
    private int _temporaryMultiplier = 1; // For one-time "applied X times" usage
    private int _currentLevel = 1; // For percent-based EXP calculation
    
    // Streak tracking
    private bool _isStreakTracked;
    private int _streakCount;
    private int _streakAttemptNumber;
    
    // Display day streak tracking
    private int _displayDayStreak;
    private bool _optOutDisplayDayStreak;

    public ActivityGameViewModel(Activity activity)
    {
        _activity = activity;
        _sectionHeader = "";
        _isStreakTracked = activity.IsStreakTracked;
        _displayDayStreak = activity.DisplayDayStreak;
        _optOutDisplayDayStreak = activity.OptOutDisplayDayStreak;
    }

    public Activity Activity => _activity;
    public int Id => _activity.Id;
    public string Name => _activity.Name;
    
    /// <summary>
    /// EXP gain - calculated dynamically for PercentOfLevel type
    /// </summary>
    public int ExpGain
    {
        get
        {
            if (_activity.RewardType == "PercentOfLevel")
            {
                return ExpEngine.ExpForPercentOfLevel(_currentLevel, _activity.PercentOfLevel, _activity.PercentCutoffLevel);
            }
            return _activity.ExpGain;
        }
    }
    
    public string Category => _activity.Category ?? "Misc";
    public string ImagePath => _activity.ImagePath ?? "";
    public int Multiplier => _activity.Multiplier;
    public bool ShowMultiplier => _activity.Multiplier > 1;
    public DateTime? StartDate => _activity.StartDate;
    public string RewardType => _activity.RewardType ?? "Fixed";
    public double PercentOfLevel => _activity.PercentOfLevel;
    public int PercentCutoffLevel => _activity.PercentCutoffLevel;
    public bool IsPossible => _activity.IsPossible;

    #region Streak Properties

    /// <summary>
    /// Whether this activity has streak tracking enabled
    /// </summary>
    public bool IsStreakTracked
    {
        get => _isStreakTracked;
        set
        {
            if (_isStreakTracked != value)
            {
                _isStreakTracked = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowStreakBadge));
                OnPropertyChanged(nameof(StreakDisplay));
            }
        }
    }

    /// <summary>
    /// Current streak count (consecutive days)
    /// </summary>
    public int StreakCount
    {
        get => _streakCount;
        set
        {
            if (_streakCount != value)
            {
                _streakCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StreakDisplay));
            }
        }
    }

    /// <summary>
    /// Which attempt number this streak is on
    /// </summary>
    public int StreakAttemptNumber
    {
        get => _streakAttemptNumber;
        set
        {
            if (_streakAttemptNumber != value)
            {
                _streakAttemptNumber = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StreakDisplay));
            }
        }
    }

    /// <summary>
    /// Whether to show the streak badge
    /// </summary>
    public bool ShowStreakBadge => _isStreakTracked;

    /// <summary>
    /// Display text for streak badge: "🔥 5d (#2)"
    /// </summary>
    public string StreakDisplay
    {
        get
        {
            if (!_isStreakTracked) return "";
            if (_streakAttemptNumber > 0)
            {
                return $"🔥{_streakCount}d (#{_streakAttemptNumber})";
            }
            return $"🔥{_streakCount}d";
        }
    }

    #endregion

    #region Display Day Streak Properties

    /// <summary>
    /// Current display day streak count
    /// </summary>
    public int DisplayDayStreak
    {
        get => _displayDayStreak;
        set
        {
            if (_displayDayStreak != value)
            {
                _displayDayStreak = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayDayStreakDisplay));
                OnPropertyChanged(nameof(ShowDisplayDayStreak));
            }
        }
    }

    /// <summary>
    /// Whether opted out of display day streak tracking
    /// </summary>
    public bool OptOutDisplayDayStreak
    {
        get => _optOutDisplayDayStreak;
        set
        {
            if (_optOutDisplayDayStreak != value)
            {
                _optOutDisplayDayStreak = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ShowDisplayDayStreak));
            }
        }
    }

    /// <summary>
    /// Whether to show the display day streak badge
    /// </summary>
    public bool ShowDisplayDayStreak => !_optOutDisplayDayStreak && _displayDayStreak > 0;

    /// <summary>
    /// Display text for display day streak badge
    /// </summary>
    public string DisplayDayStreakDisplay
    {
        get
        {
            if (_optOutDisplayDayStreak || _displayDayStreak == 0) return "";
            return $"📅{_displayDayStreak}";
        }
    }

    #endregion

    #region Times Completed Properties

    /// <summary>
    /// How many times this activity has been completed
    /// </summary>
    public int TimesCompleted
    {
        get => _activity.TimesCompleted;
        set
        {
            if (_activity.TimesCompleted != value)
            {
                _activity.TimesCompleted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(TimesCompletedDisplay));
                OnPropertyChanged(nameof(ShowTimesCompleted));
            }
        }
    }

    /// <summary>
    /// Whether to show the times completed badge
    /// </summary>
    public bool ShowTimesCompleted => _activity.ShowTimesCompletedBadge && _activity.TimesCompleted > 0;

    /// <summary>
    /// Display text for times completed badge
    /// </summary>
    public string TimesCompletedDisplay
    {
        get
        {
            if (!_activity.ShowTimesCompletedBadge || _activity.TimesCompleted == 0) return "";
            return $"✓{_activity.TimesCompleted}";
        }
    }

    #endregion

    #region Notes Properties

    /// <summary>
    /// Notes/clarifications for this activity
    /// </summary>
    public string Notes
    {
        get => _activity.Notes ?? "";
        set
        {
            if (_activity.Notes != value)
            {
                _activity.Notes = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasNotes));
                OnPropertyChanged(nameof(NotesPreview));
            }
        }
    }

    /// <summary>
    /// Whether this activity has notes
    /// </summary>
    public bool HasNotes => !string.IsNullOrWhiteSpace(_activity.Notes);

    /// <summary>
    /// Preview of notes (first 50 chars)
    /// </summary>
    public string NotesPreview => _activity.NotesPreview;

    #endregion

    /// <summary>
    /// Set the current level for percent-based EXP calculation
    /// </summary>
    public int CurrentLevel
    {
        get => _currentLevel;
        set
        {
            if (_currentLevel != value)
            {
                _currentLevel = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ExpGain)); // Recalculate ExpGain
            }
        }
    }

    /// <summary>
    /// Temporary multiplier for one-time calculation (resets after calculate)
    /// </summary>
    public int TemporaryMultiplier
    {
        get => _temporaryMultiplier;
        set
        {
            if (_temporaryMultiplier != value)
            {
                _temporaryMultiplier = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(EffectiveMultiplier));
            }
        }
    }

    /// <summary>
    /// The multiplier to actually use (temporary if set, otherwise permanent)
    /// </summary>
    public int EffectiveMultiplier => _temporaryMultiplier > 1 ? _temporaryMultiplier : Multiplier;

    public DateTime? LastUsedDate
    {
        get => _lastUsedDate;
        set
        {
            if (_lastUsedDate != value)
            {
                _lastUsedDate = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DaysSinceClick));
                OnPropertyChanged(nameof(DaysSinceClickDisplay));
                OnPropertyChanged(nameof(ShowDaysSinceClick));
            }
        }
    }

    /// <summary>
    /// Number of days since this activity was last clicked
    /// </summary>
    public int? DaysSinceClick
    {
        get
        {
            if (!_lastUsedDate.HasValue) return null;
            return (int)(DateTime.Now.Date - _lastUsedDate.Value.Date).TotalDays;
        }
    }

    /// <summary>
    /// Display text for days since click badge
    /// </summary>
    public string DaysSinceClickDisplay
    {
        get
        {
            if (!_lastUsedDate.HasValue) return "?";
            var days = DaysSinceClick ?? 0;
            if (days == 0) return "today";
            if (days == 1) return "1d";
            return $"{days}d";
        }
    }

    /// <summary>
    /// Whether to show the days since click badge
    /// </summary>
    public bool ShowDaysSinceClick => true; // Always show

    public string SectionHeader
    {
        get => _sectionHeader;
        set
        {
            if (_sectionHeader != value)
            {
                _sectionHeader = value;
                OnPropertyChanged();
            }
        }
    }

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

    public event PropertyChangedEventHandler? PropertyChanged;

    public void UpdateActivity(Activity updated)
    {
        _activity = updated;
        _isStreakTracked = updated.IsStreakTracked;
        _displayDayStreak = updated.DisplayDayStreak;
        _optOutDisplayDayStreak = updated.OptOutDisplayDayStreak;
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ExpGain));
        OnPropertyChanged(nameof(Category));
        OnPropertyChanged(nameof(ImagePath));
        OnPropertyChanged(nameof(Multiplier));
        OnPropertyChanged(nameof(ShowMultiplier));
        OnPropertyChanged(nameof(IsStreakTracked));
        OnPropertyChanged(nameof(ShowStreakBadge));
        OnPropertyChanged(nameof(StreakDisplay));
        OnPropertyChanged(nameof(DisplayDayStreak));
        OnPropertyChanged(nameof(DisplayDayStreakDisplay));
        OnPropertyChanged(nameof(ShowDisplayDayStreak));
        OnPropertyChanged(nameof(Notes));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(NotesPreview));
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}

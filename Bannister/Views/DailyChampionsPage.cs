using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class DailyChampionsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly FeatService _feats;

    private readonly VerticalStackLayout _todayStack;
    private readonly VerticalStackLayout _managementStack;
    private readonly VerticalStackLayout _leaderboardStack;
    private readonly Label _statusLabel;
    private readonly Button _refreshButton;
    private readonly Picker _featPicker;
    private readonly Entry _customFeatEntry;
    private readonly Entry _libraryFeatEntry;
    private readonly Button _toggleManagementButton;
    private bool _isLoading;
    private bool _isManagementExpanded;
    private List<Feat> _activeFeats = new();

    public DailyChampionsPage(AuthService auth, FeatService feats)
    {
        _auth = auth;
        _feats = feats;

        Title = "Dates Competition";
        BackgroundColor = Color.FromArgb("#F5F7FB");

        _todayStack = new VerticalStackLayout { Spacing = 10 };
        _managementStack = new VerticalStackLayout { Spacing = 10, IsVisible = false };
        _leaderboardStack = new VerticalStackLayout { Spacing = 8 };
        _statusLabel = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        };
        _refreshButton = new Button
        {
            Text = "Refresh",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44
        };
        _refreshButton.Clicked += async (_, _) => await LoadAsync();

        _featPicker = new Picker
        {
            Title = "Choose a feat",
            ItemDisplayBinding = new Binding(nameof(Feat.Title)),
            TextColor = Color.FromArgb("#222"),
            BackgroundColor = Colors.White
        };
        _customFeatEntry = new Entry
        {
            Placeholder = "One-off feat",
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#777"),
            BackgroundColor = Colors.White,
            IsVisible = false
        };
        _libraryFeatEntry = new Entry
        {
            Placeholder = "Feat title",
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#777"),
            BackgroundColor = Colors.White
        };
        _toggleManagementButton = new Button
        {
            Text = "Show Feats Management",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        _toggleManagementButton.Clicked += (_, _) => ToggleManagement();

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        var main = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };
        header.Add(new Label
        {
            Text = "Dates Competition",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        header.Add(_refreshButton, 1, 0);
        main.Children.Add(header);
        main.Children.Add(_statusLabel);

        main.Children.Add(CreateSectionFrame(_todayStack));
        main.Children.Add(CreateSectionFrame(new VerticalStackLayout
        {
            Spacing = 12,
            Children = { _toggleManagementButton, _managementStack }
        }));
        main.Children.Add(CreateSectionFrame(_leaderboardStack));

        Content = new ScrollView { Content = main };
    }

    private async Task LoadAsync()
    {
        if (_isLoading) return;

        _isLoading = true;
        _refreshButton.IsEnabled = false;
        _statusLabel.TextColor = Color.FromArgb("#666");
        _statusLabel.Text = "Loading...";

        try
        {
            var username = _auth.CurrentUsername;
            var todayKey = DateKey(DateTime.Today);
            _activeFeats = await _feats.GetFeatsAsync(username);
            var allFeats = await _feats.GetAllFeatsAsync(username);
            var dailyFeats = await _feats.GetDailyFeatsAsync(username, todayKey);
            var todayScore = GetScore(dailyFeats);
            var todayAggregateScore = await _feats.GetDayScoreAsync(username, todayKey);
            var allScores = await _feats.GetAllDayScoresAsync(username);
            var leaders = allScores
                .Select(x => new DailyTotal
                {
                    Date = ParseMonthDayKey(x.Key),
                    TotalExp = x.Value
                })
                .OrderByDescending(x => x.TotalExp)
                .ThenByDescending(x => x.Date)
                .Take(30)
                .ToList();
            var champion = leaders.FirstOrDefault();

            _featPicker.ItemsSource = _activeFeats;
            BuildTodaySection(todayKey, dailyFeats, todayScore, todayAggregateScore, champion);
            BuildManagementSection(allFeats);
            BuildLeaderboard(leaders, DateTime.Today, champion);
            _statusLabel.Text = $"Showing feat scores for {username}.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not load dates competition: {ex.Message}";
            _statusLabel.TextColor = Color.FromArgb("#C62828");
        }
        finally
        {
            _isLoading = false;
            _refreshButton.IsEnabled = true;
        }
    }

    private void BuildTodaySection(
        string todayKey,
        List<DailyFeat> dailyFeats,
        int todayScore,
        int todayAggregateScore,
        DailyTotal? champion)
    {
        _todayStack.Children.Clear();
        _todayStack.Children.Add(new Label
        {
            Text = "Today's Feats",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        _todayStack.Children.Add(new Label
        {
            Text = DateTime.Today.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture),
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE")
        });

        if (dailyFeats.Count == 0)
        {
            _todayStack.Children.Add(CreateMutedLabel("No feats recorded today."));
        }
        else
        {
            foreach (var dailyFeat in dailyFeats)
            {
                _todayStack.Children.Add(CreateDailyFeatRow(dailyFeat));
            }
        }

        _todayStack.Children.Add(new Label
        {
            Text = $"{todayScore:N0} points today",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2E7D32")
        });

        if (champion == null)
        {
            _todayStack.Children.Add(CreateMutedLabel("No champion date yet."));
        }
        else
        {
            var isTodayChampion = IsSameMonthDay(champion.Date, DateTime.Today);
            _todayStack.Children.Add(new Label
            {
                Text = $"Champion: {FormatMonthDay(champion.Date)} - {champion.TotalExp:N0} points",
                FontSize = 15,
                TextColor = Color.FromArgb("#333")
            });
            _todayStack.Children.Add(new Label
            {
                Text = isTodayChampion
                    ? "Today is the champion!"
                    : $"You need {Math.Max(0, champion.TotalExp - todayAggregateScore + 1):N0} more points to beat the champion",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = isTodayChampion ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828")
            });
        }

        _todayStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 8)
        });
        _todayStack.Children.Add(new Label
        {
            Text = "Add Feat",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        _todayStack.Children.Add(_featPicker);

        var newButton = new Button
        {
            Text = "＋ New",
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            TextColor = Color.FromArgb("#F57C00"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        newButton.Clicked += (_, _) =>
        {
            _customFeatEntry.IsVisible = true;
            _featPicker.SelectedItem = null;
            _customFeatEntry.Focus();
        };

        var addButton = new Button
        {
            Text = "Add",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42
        };
        addButton.Clicked += async (_, _) => await AddDailyFeatAsync(todayKey);

        _todayStack.Children.Add(_customFeatEntry);
        var addGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        addGrid.Add(newButton, 0, 0);
        addGrid.Add(addButton, 1, 0);
        _todayStack.Children.Add(addGrid);
    }

    private View CreateDailyFeatRow(DailyFeat dailyFeat)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10,
            Padding = new Thickness(10),
            BackgroundColor = Color.FromArgb("#FAFAFA")
        };

        row.Add(new Label
        {
            Text = GetDailyFeatTitle(dailyFeat),
            FontSize = 14,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var deleteButton = new Button
        {
            Text = "Delete",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            HeightRequest = 34,
            Padding = new Thickness(12, 0)
        };
        deleteButton.Clicked += async (_, _) =>
        {
            await _feats.DeleteDailyFeatAsync(dailyFeat.Id);
            await LoadAsync();
        };
        row.Add(deleteButton, 1, 0);

        return row;
    }

    private void BuildManagementSection(List<Feat> allFeats)
    {
        _managementStack.Children.Clear();
        _managementStack.Children.Add(new Label
        {
            Text = "Feats Management",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });

        var activeFeats = allFeats
            .Where(x => !x.IsArchived)
            .OrderBy(x => x.SortOrder)
            .ToList();

        if (activeFeats.Count == 0)
        {
            _managementStack.Children.Add(CreateMutedLabel("No feats in the library yet."));
        }
        else
        {
            for (var i = 0; i < activeFeats.Count; i++)
            {
                _managementStack.Children.Add(CreateLibraryFeatRow(activeFeats, i));
            }
        }

        var archived = allFeats
            .Where(x => x.IsArchived)
            .OrderBy(x => x.SortOrder)
            .ToList();
        if (archived.Count > 0)
        {
            _managementStack.Children.Add(new Label
            {
                Text = "Archived",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#555"),
                Margin = new Thickness(0, 8, 0, 0)
            });
            foreach (var feat in archived)
                _managementStack.Children.Add(CreateMutedLabel(feat.Title));
        }

        _managementStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 8)
        });
        _managementStack.Children.Add(new Label
        {
            Text = "Add Feat to Library",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        _managementStack.Children.Add(_libraryFeatEntry);

        var saveButton = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42
        };
        saveButton.Clicked += async (_, _) => await SaveLibraryFeatAsync();
        _managementStack.Children.Add(saveButton);
    }

    private View CreateLibraryFeatRow(List<Feat> activeFeats, int index)
    {
        var feat = activeFeats[index];
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            Padding = new Thickness(10),
            BackgroundColor = Color.FromArgb("#FAFAFA")
        };

        row.Add(new Label
        {
            Text = $"{index + 1}. {feat.Title}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        var upButton = SmallButton("Up", index > 0);
        upButton.Clicked += async (_, _) =>
        {
            if (index <= 0) return;
            (activeFeats[index - 1], activeFeats[index]) = (activeFeats[index], activeFeats[index - 1]);
            await _feats.ReorderFeatsAsync(activeFeats);
            await LoadAsync();
        };

        var downButton = SmallButton("Down", index < activeFeats.Count - 1);
        downButton.Clicked += async (_, _) =>
        {
            if (index >= activeFeats.Count - 1) return;
            (activeFeats[index + 1], activeFeats[index]) = (activeFeats[index], activeFeats[index + 1]);
            await _feats.ReorderFeatsAsync(activeFeats);
            await LoadAsync();
        };

        var archiveButton = SmallButton("Archive", true);
        archiveButton.BackgroundColor = Color.FromArgb("#FFEBEE");
        archiveButton.TextColor = Color.FromArgb("#C62828");
        archiveButton.Clicked += async (_, _) =>
        {
            await _feats.ArchiveFeatAsync(feat.Id);
            await LoadAsync();
        };

        row.Add(new HorizontalStackLayout
        {
            Spacing = 6,
            Children = { upButton, downButton, archiveButton }
        }, 1, 0);

        return row;
    }

    private void BuildLeaderboard(List<DailyTotal> leaders, DateTime today, DailyTotal? champion)
    {
        _leaderboardStack.Children.Clear();
        _leaderboardStack.Children.Add(new Label
        {
            Text = "All-Time Leaderboard",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });

        if (leaders.Count == 0)
        {
            _leaderboardStack.Children.Add(CreateMutedLabel("No scored dates yet."));
            return;
        }

        var maxScore = Math.Max(1, leaders.Max(x => Math.Abs(x.TotalExp)));
        for (var i = 0; i < leaders.Count; i++)
        {
            var total = leaders[i];
            var isToday = IsSameMonthDay(total.Date, today);
            var isChampion = champion != null && IsSameMonthDay(total.Date, champion.Date);
            _leaderboardStack.Children.Add(CreateLeaderboardRow(i + 1, total, maxScore, isToday, isChampion));
        }
    }

    private View CreateLeaderboardRow(int rank, DailyTotal total, int maxScore, bool isToday, bool isChampion)
    {
        var barWidth = 24 + (Math.Max(0, total.TotalExp) / (double)maxScore * 220);
        var bg = isToday ? Color.FromArgb("#E8F5E9") : Colors.White;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 44 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = 110 }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 10,
            RowSpacing = 6,
            Padding = new Thickness(10),
            BackgroundColor = bg
        };

        row.Add(new Label
        {
            Text = $"#{rank}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        row.Add(new Label
        {
            Text = $"{(isChampion ? "Champion - " : "")}{FormatMonthDay(total.Date)}{(isToday ? " (today)" : "")}",
            FontSize = 14,
            FontAttributes = isChampion || isToday ? FontAttributes.Bold : FontAttributes.None,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        row.Add(new Label
        {
            Text = $"{total.TotalExp:N0} points",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2E7D32"),
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center
        }, 2, 0);

        var bar = new BoxView
        {
            HeightRequest = 8,
            WidthRequest = barWidth,
            BackgroundColor = isChampion ? Color.FromArgb("#FBC02D") : Color.FromArgb("#5B63EE"),
            HorizontalOptions = LayoutOptions.Start
        };
        row.Add(bar, 1, 1);
        Grid.SetColumnSpan(bar, 2);

        return new Frame
        {
            Padding = 0,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = isToday ? Color.FromArgb("#81C784") : Color.FromArgb("#E0E0E0"),
            BackgroundColor = bg,
            Content = row
        };
    }

    private async Task AddDailyFeatAsync(string todayKey)
    {
        var selectedFeat = _featPicker.SelectedItem as Feat;
        var customTitle = (_customFeatEntry.Text ?? "").Trim();

        if (selectedFeat == null && string.IsNullOrWhiteSpace(customTitle))
        {
            await DisplayAlert("Missing Feat", "Choose a feat or enter a new one-off feat.", "OK");
            return;
        }

        await _feats.SaveDailyFeatAsync(new DailyFeat
        {
            Username = _auth.CurrentUsername,
            FeatId = selectedFeat?.Id ?? 0,
            CustomTitle = selectedFeat == null ? customTitle : "",
            RecordedDate = todayKey,
            CreatedDate = DateTime.UtcNow
        });

        _featPicker.SelectedItem = null;
        _customFeatEntry.Text = "";
        _customFeatEntry.IsVisible = false;
        await LoadAsync();
    }

    private async Task SaveLibraryFeatAsync()
    {
        var title = (_libraryFeatEntry.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            await DisplayAlert("Missing Title", "Enter a feat title first.", "OK");
            return;
        }

        await _feats.SaveFeatAsync(new Feat
        {
            Username = _auth.CurrentUsername,
            Title = title,
            CreatedDate = DateTime.UtcNow
        });
        _libraryFeatEntry.Text = "";
        await LoadAsync();
    }

    private void ToggleManagement()
    {
        _isManagementExpanded = !_isManagementExpanded;
        _managementStack.IsVisible = _isManagementExpanded;
        _toggleManagementButton.Text = _isManagementExpanded
            ? "Hide Feats Management"
            : "Show Feats Management";
    }

    private string GetDailyFeatTitle(DailyFeat dailyFeat)
    {
        if (dailyFeat.FeatId == 0)
            return dailyFeat.CustomTitle;

        return _activeFeats.FirstOrDefault(x => x.Id == dailyFeat.FeatId)?.Title
            ?? $"Archived feat #{dailyFeat.FeatId}";
    }

    private int GetScore(List<DailyFeat> dailyFeats)
    {
        var totalFeats = _activeFeats.Count;
        var orderById = _activeFeats.ToDictionary(x => x.Id, x => x.SortOrder);

        return dailyFeats.Sum(dailyFeat =>
            dailyFeat.FeatId > 0 && orderById.TryGetValue(dailyFeat.FeatId, out var sortOrder)
                ? totalFeats - sortOrder
                : 0);
    }

    private static Button SmallButton(string text, bool enabled) =>
        new()
        {
            Text = text,
            IsEnabled = enabled,
            BackgroundColor = enabled ? Color.FromArgb("#ECEFF1") : Color.FromArgb("#F5F5F5"),
            TextColor = enabled ? Color.FromArgb("#333") : Color.FromArgb("#999"),
            CornerRadius = 8,
            HeightRequest = 34,
            Padding = new Thickness(10, 0)
        };

    private static Label CreateMutedLabel(string text) =>
        new()
        {
            Text = text,
            FontSize = 13,
            TextColor = Color.FromArgb("#777"),
            FontAttributes = FontAttributes.Italic
        };

    private static Frame CreateSectionFrame(View content) =>
        new()
        {
            Padding = 18,
            CornerRadius = 12,
            HasShadow = true,
            BackgroundColor = Colors.White,
            BorderColor = Colors.Transparent,
            Content = content
        };

    private static string DateKey(DateTime date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime ParseMonthDayKey(string date)
    {
        return DateTime.TryParseExact(
            date,
            "MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : DateTime.MinValue;
    }

    private static bool IsSameMonthDay(DateTime left, DateTime right) =>
        left.Month == right.Month && left.Day == right.Day;

    private static string FormatMonthDay(DateTime date) =>
        date.ToString("MMM d", CultureInfo.CurrentCulture);
}

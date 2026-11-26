using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class DailyChampionsPage : Bannister.Views.ModeThemedPage
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


        _todayStack = new VerticalStackLayout { Spacing = 10 };
        _managementStack = new VerticalStackLayout { Spacing = 10, IsVisible = false };
        _leaderboardStack = new VerticalStackLayout { Spacing = 8 };
        _statusLabel = (new Label
        {
            Text = "",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
        _refreshButton = (new Button
        {
            Text = "Refresh",
            CornerRadius = 8,
            HeightRequest = 44
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        _refreshButton.Clicked += async (_, _) => await LoadAsync();

        _featPicker = (new Picker
        {
            Title = "Choose a feat",
            ItemDisplayBinding = new Binding(nameof(Feat.Title))
        }).ThemeColor(Picker.TextColorProperty, ThemeKey.InputText).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _customFeatEntry = (new Entry
        {
            Placeholder = "One-off feat",
            IsVisible = false
        }).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _libraryFeatEntry = (new Entry
        {
            Placeholder = "Feat title"
        }).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _toggleManagementButton = (new Button
        {
            Text = "Show Feats Management",
            CornerRadius = 8,
            HeightRequest = 42
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
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
        header.Add((new Label
        {
            Text = "Dates Competition",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true), 0, 0);
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
        _statusLabel.ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1, classifyPageText: true);
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
            _statusLabel.ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, ThemeKey.PageText1, classifyPageText: true);
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
        _todayStack.Children.Add((new Label
        {
            Text = "Today's Feats",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));
        _todayStack.Children.Add((new Label
        {
            Text = DateTime.Today.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture),
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SecondaryAccentText, ThemeKey.PageText1, classifyPageText: true));

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

        _todayStack.Children.Add((new Label
        {
            Text = $"{todayScore:N0} points today",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText1, classifyPageText: true));

        if (champion == null)
        {
            _todayStack.Children.Add(CreateMutedLabel("No champion date yet."));
        }
        else
        {
            var isTodayChampion = IsSameMonthDay(champion.Date, DateTime.Today);
            _todayStack.Children.Add((new Label
            {
                Text = $"Champion: {FormatMonthDay(champion.Date)} - {champion.TotalExp:N0} points",
                FontSize = 15
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
            _todayStack.Children.Add((new Label
            {
                Text = isTodayChampion
                    ? "Today is the champion!"
                    : $"You need {Math.Max(0, champion.TotalExp - todayAggregateScore + 1):N0} more points to beat the champion",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, isTodayChampion ? ThemeKey.SuccessText : ThemeKey.ErrorText, ThemeKey.PageText1, classifyPageText: true));
        }

        _todayStack.Children.Add((new BoxView
        {
            HeightRequest = 1,
            Margin = new Thickness(0, 8)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator));
        _todayStack.Children.Add((new Label
        {
            Text = "Add Feat",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        _todayStack.Children.Add(_featPicker);

        var newButton = (new Button
        {
            Text = "＋ New",
            CornerRadius = 8,
            HeightRequest = 42
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Button.TextColorProperty, ThemeKey.WarningStrongText);
        newButton.Clicked += (_, _) =>
        {
            _customFeatEntry.IsVisible = true;
            _featPicker.SelectedItem = null;
            _customFeatEntry.Focus();
        };

        var addButton = (new Button
        {
            Text = "Add",
            CornerRadius = 8,
            HeightRequest = 42
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
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
        var row = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10,
            Padding = new Thickness(10)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        row.Add((new Label
        {
            Text = GetDailyFeatTitle(dailyFeat),
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true), 0, 0);

        var deleteButton = (new Button
        {
            Text = "Delete",
            CornerRadius = 8,
            HeightRequest = 34,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
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
        _managementStack.Children.Add((new Label
        {
            Text = "Feats Management",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));

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
            _managementStack.Children.Add((new Label
            {
                Text = "Archived",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 8, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1, classifyPageText: true));
            foreach (var feat in archived)
                _managementStack.Children.Add(CreateMutedLabel(feat.Title));
        }

        _managementStack.Children.Add((new BoxView
        {
            HeightRequest = 1,
            Margin = new Thickness(0, 8)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator));
        _managementStack.Children.Add((new Label
        {
            Text = "Add Feat to Library",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        _managementStack.Children.Add(_libraryFeatEntry);

        var saveButton = (new Button
        {
            Text = "Save",
            CornerRadius = 8,
            HeightRequest = 42
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessDarkButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        saveButton.Clicked += async (_, _) => await SaveLibraryFeatAsync();
        _managementStack.Children.Add(saveButton);
    }

    private View CreateLibraryFeatRow(List<Feat> activeFeats, int index)
    {
        var feat = activeFeats[index];
        var row = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            Padding = new Thickness(10)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        row.Add((new Label
        {
            Text = $"{index + 1}. {feat.Title}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true), 0, 0);

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
        archiveButton.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground);
        archiveButton.ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
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
        _leaderboardStack.Children.Add((new Label
        {
            Text = "All-Time Leaderboard",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));

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
        var bg = isToday ? ThemeKey.SuccessBackground : ThemeKey.CardBackground;

        var row = (new Grid
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
            Padding = new Thickness(10)
        }).ThemeColor(VisualElement.BackgroundColorProperty, bg);

        row.Add((new Label
        {
            Text = $"#{rank}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1, classifyPageText: true), 0, 0);

        row.Add((new Label
        {
            Text = $"{(isChampion ? "Champion - " : "")}{FormatMonthDay(total.Date)}{(isToday ? " (today)" : "")}",
            FontSize = 14,
            FontAttributes = isChampion || isToday ? FontAttributes.Bold : FontAttributes.None,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true), 1, 0);

        row.Add((new Label
        {
            Text = $"{total.TotalExp:N0} points",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText1, classifyPageText: true), 2, 0);

        var bar = (new BoxView
        {
            HeightRequest = 8,
            WidthRequest = barWidth,
            HorizontalOptions = LayoutOptions.Start
        }).ThemeColor(VisualElement.BackgroundColorProperty, isChampion ? ThemeKey.HighlightGoldButton : ThemeKey.SecondaryButtonBackground);
        row.Add(bar, 1, 1);
        Grid.SetColumnSpan(bar, 2);

        return (new Frame
        {
            Padding = 0,
            CornerRadius = 8,
            HasShadow = false,
            Content = row
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, isToday ? ThemeKey.SuccessSelectedBackground : ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, bg);
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
        (new Button(){
            Text = text,
            IsEnabled = enabled,
            CornerRadius = 8,
            HeightRequest = 34,
            Padding = new Thickness(10, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, enabled ? ThemeKey.NeutralButtonBackground : ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, enabled ? ThemeKey.CardText1 : ThemeKey.CardText3);

    private static Label CreateMutedLabel(string text) =>
        (new Label(){
            Text = text,
            FontSize = 13,
            FontAttributes = FontAttributes.Italic
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);

    private static Frame CreateSectionFrame(View content) =>
        (new Frame(){
            Padding = 18,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            Content = content
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

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

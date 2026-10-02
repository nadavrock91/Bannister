using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class DailyChampionsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DailyChampionService _champions;

    private readonly VerticalStackLayout _todayStack;
    private readonly VerticalStackLayout _historyStack;
    private readonly VerticalStackLayout _leaderboardStack;
    private readonly Label _statusLabel;
    private readonly Button _refreshButton;
    private bool _isLoading;

    public DailyChampionsPage(AuthService auth, DailyChampionService champions)
    {
        _auth = auth;
        _champions = champions;

        Title = "Daily Champions";
        BackgroundColor = Color.FromArgb("#F5F7FB");

        _todayStack = new VerticalStackLayout { Spacing = 10 };
        _historyStack = new VerticalStackLayout { Spacing = 6 };
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
            Text = "Daily Champions",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        header.Add(_refreshButton, 1, 0);
        main.Children.Add(header);
        main.Children.Add(_statusLabel);

        main.Children.Add(CreateSectionFrame(_todayStack));
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
            var today = DateTime.Today;
            var allTotals = await _champions.GetDailyTotalsAsync(username);
            var todayTotal = await _champions.GetTodayTotalAsync(username);
            var champion = await _champions.GetChampionAsync(username);
            var sameDayHistory = await _champions.GetSameDayHistoryAsync(username, today);

            BuildTodaySection(today, todayTotal, champion, sameDayHistory);
            BuildLeaderboard(allTotals.Take(30).ToList(), today, champion);
            _statusLabel.Text = $"Showing EXP history for {username}.";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Could not load champions: {ex.Message}";
            _statusLabel.TextColor = Color.FromArgb("#C62828");
        }
        finally
        {
            _isLoading = false;
            _refreshButton.IsEnabled = true;
        }
    }

    private void BuildTodaySection(
        DateTime today,
        int todayTotal,
        DailyTotal? champion,
        List<DailyTotal> sameDayHistory)
    {
        _todayStack.Children.Clear();
        _todayStack.Children.Add(new Label
        {
            Text = "Today's Status",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        _todayStack.Children.Add(new Label
        {
            Text = today.ToString("dddd, MMMM d, yyyy", CultureInfo.CurrentCulture),
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE")
        });
        _todayStack.Children.Add(new Label
        {
            Text = $"{todayTotal:N0} EXP today",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2E7D32")
        });

        if (champion == null)
        {
            _todayStack.Children.Add(CreateMutedLabel("No EXP history yet."));
        }
        else
        {
            var isTodayChampion = champion.Date.Date == today.Date;
            _todayStack.Children.Add(new Label
            {
                Text = $"Champion: {FormatDate(champion.Date)} - {champion.TotalExp:N0} EXP",
                FontSize = 15,
                TextColor = Color.FromArgb("#333")
            });

            _todayStack.Children.Add(new Label
            {
                Text = isTodayChampion
                    ? "Today is the champion!"
                    : $"You need {Math.Max(0, champion.TotalExp - todayTotal + 1):N0} more EXP to beat the champion",
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
            Text = "This date in previous years:",
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        _historyStack.Children.Clear();
        if (sameDayHistory.Count == 0)
        {
            _historyStack.Children.Add(CreateMutedLabel("No previous-year scores for this date."));
        }
        else
        {
            foreach (var total in sameDayHistory)
            {
                _historyStack.Children.Add(CreateHistoryRow(total));
            }
        }

        _todayStack.Children.Add(_historyStack);
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
            _leaderboardStack.Children.Add(CreateMutedLabel("No EXP logs yet."));
            return;
        }

        var maxExp = Math.Max(1, leaders.Max(x => Math.Abs(x.TotalExp)));
        for (var i = 0; i < leaders.Count; i++)
        {
            var total = leaders[i];
            var isToday = total.Date.Date == today.Date;
            var isChampion = champion != null && total.Date.Date == champion.Date.Date;
            _leaderboardStack.Children.Add(CreateLeaderboardRow(i + 1, total, maxExp, isToday, isChampion));
        }
    }

    private View CreateLeaderboardRow(int rank, DailyTotal total, int maxExp, bool isToday, bool isChampion)
    {
        var barWidth = 24 + (Math.Max(0, total.TotalExp) / (double)maxExp * 220);
        var bg = isToday ? Color.FromArgb("#E8F5E9") : Colors.White;

        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 44 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = 96 }
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
            Text = $"{(isChampion ? "Champion - " : "")}{FormatDate(total.Date)}{(isToday ? " (today)" : "")}",
            FontSize = 14,
            FontAttributes = isChampion || isToday ? FontAttributes.Bold : FontAttributes.None,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);

        row.Add(new Label
        {
            Text = $"{total.TotalExp:N0} EXP",
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

    private static View CreateHistoryRow(DailyTotal total)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Padding = new Thickness(0, 2)
        };

        row.Add(new Label
        {
            Text = FormatDate(total.Date),
            FontSize = 13,
            TextColor = Color.FromArgb("#555")
        }, 0, 0);

        row.Add(new Label
        {
            Text = $"{total.TotalExp:N0} EXP",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalOptions = LayoutOptions.End
        }, 1, 0);

        return row;
    }

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

    private static string FormatDate(DateTime date) =>
        date.ToString("MMM d, yyyy", CultureInfo.CurrentCulture);
}

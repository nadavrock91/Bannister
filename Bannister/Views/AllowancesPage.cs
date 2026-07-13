using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class AllowancesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly AllowanceService _allowances;
    private readonly VerticalStackLayout _listStack = new() { Spacing = 10 };

    public AllowancesPage(AuthService auth, AllowanceService allowances)
    {
        _auth = auth;
        _allowances = allowances;
        Title = "Allowances";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void BuildUI()
    {
        var root = new VerticalStackLayout { Padding = 16, Spacing = 12 };

        root.Children.Add(new Label
        {
            Text = "Allowances",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#00695C")
        });

        root.Children.Add(new Label
        {
            Text = "Adaptive caps. Report success or failure each attempt. Three successes in a row raises the cap. One failure lowers it.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        var add = SmallButton("+ Add Allowance", "#00796B");
        add.HeightRequest = 42;
        add.HorizontalOptions = LayoutOptions.Start;
        add.Clicked += async (_, _) => await AddAllowanceAsync();
        root.Children.Add(add);

        root.Children.Add(_listStack);
        Content = new ScrollView { Content = root };
    }

    private async Task RefreshAsync()
    {
        var allowances = await _allowances.GetAllowancesAsync(_auth.CurrentUsername);
        _listStack.Children.Clear();

        if (allowances.Count == 0)
        {
            _listStack.Children.Add(new Label
            {
                Text = "No allowances yet.",
                FontSize = 13,
                TextColor = Color.FromArgb("#888"),
                Margin = new Thickness(4, 10)
            });
            return;
        }

        foreach (var allowance in allowances)
            _listStack.Children.Add(BuildAllowanceCard(allowance));
    }

    private View BuildAllowanceCard(Allowance allowance)
    {
        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#80CBC4"),
            CornerRadius = 8,
            HasShadow = false,
            Padding = 12
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        var title = new Label
        {
            Text = allowance.Title,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await RenameAsync(allowance);
        title.GestureRecognizers.Add(tap);
        stack.Children.Add(title);

        var metrics = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        metrics.Add(new Label
        {
            Text = $"Cap: {allowance.Total}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238")
        }, 0, 0);
        metrics.Add(new Label
        {
            Text = $"Streak: {allowance.SuccessStreak}/3",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            HorizontalTextAlignment = TextAlignment.End
        }, 1, 0);
        stack.Children.Add(metrics);

        var historyRow = new HorizontalStackLayout { Spacing = 6, HeightRequest = 18 };
        foreach (var entry in ParseHistory(allowance.RecentHistory).TakeLast(10))
        {
            historyRow.Children.Add(new BoxView
            {
                WidthRequest = 10,
                HeightRequest = 10,
                CornerRadius = 5,
                Color = entry.success ? Color.FromArgb("#2E7D32") : Color.FromArgb("#C62828"),
                VerticalOptions = LayoutOptions.Center
            });
        }
        stack.Children.Add(historyRow);

        var actions = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };

        var success = SmallButton("✓ Success", "#2E7D32");
        success.FontAttributes = FontAttributes.Bold;
        success.HeightRequest = 42;
        success.Clicked += async (_, _) => await RecordOutcomeAsync(allowance, true);
        actions.Add(success, 0, 0);

        var failure = SmallButton("✗ Failure", "#C62828");
        failure.FontAttributes = FontAttributes.Bold;
        failure.HeightRequest = 42;
        failure.Clicked += async (_, _) => await RecordOutcomeAsync(allowance, false);
        actions.Add(failure, 1, 0);

        var edit = SmallButton("Edit", "#00695C");
        edit.Clicked += async (_, _) => await ShowEditMenuAsync(allowance);
        actions.Add(edit, 2, 0);

        stack.Children.Add(actions);
        frame.Content = stack;
        return frame;
    }

    private async Task AddAllowanceAsync()
    {
        string? title = await DisplayPromptAsync("Add Allowance", "Allowance title:", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(title))
            return;

        string? totalText = await DisplayPromptAsync("Starting Cap", "Starting cap:", "Add", "Cancel", keyboard: Keyboard.Numeric, initialValue: "1");
        if (!int.TryParse(totalText, out int total))
            return;

        try
        {
            await _allowances.AddAllowanceAsync(_auth.CurrentUsername, title.Trim(), Math.Max(1, total));
            await RefreshAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task RecordOutcomeAsync(Allowance allowance, bool success)
    {
        int previousCap = allowance.Total;

        try
        {
            bool recorded = await _allowances.RecordOutcomeAsync(allowance.Id, success);
            if (!recorded)
            {
                await RefreshAsync();
                return;
            }

            var refreshed = (await _allowances.GetAllowancesAsync(_auth.CurrentUsername)).FirstOrDefault(a => a.Id == allowance.Id);
            await RefreshAsync();

            if (refreshed == null || refreshed.Total == previousCap)
                return;

            if (refreshed.Total > previousCap)
                await DisplayAlert("Allowance Promoted", $"Cap raised to {refreshed.Total}.", "OK");
            else
                await DisplayAlert("Allowance Demoted", $"Cap lowered to {refreshed.Total}.", "OK");
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task ShowEditMenuAsync(Allowance allowance)
    {
        string? action = await DisplayActionSheet(
            allowance.Title,
            "Cancel",
            null,
            "Rename",
            "Change Cap",
            "Change Streak",
            $"Daily Prompt ({(allowance.PromptDailyOnHome ? "On" : "Off")})",
            "Delete");

        if (action == "Rename")
        {
            await RenameAsync(allowance);
        }
        else if (action == "Change Cap")
        {
            await SetTotalAsync(allowance);
        }
        else if (action == "Change Streak")
        {
            await SetStreakAsync(allowance);
        }
        else if (action?.StartsWith("Daily Prompt", StringComparison.Ordinal) == true)
        {
            await ToggleDailyPromptAsync(allowance);
        }
        else if (action == "Delete")
        {
            await DeleteAsync(allowance);
        }
    }

    private async Task RenameAsync(Allowance allowance)
    {
        string? title = await DisplayPromptAsync("Rename Allowance", "Title:", "Save", "Cancel", initialValue: allowance.Title);
        if (string.IsNullOrWhiteSpace(title))
            return;

        try
        {
            await _allowances.UpdateTitleAsync(allowance.Id, title.Trim());
            await RefreshAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task SetTotalAsync(Allowance allowance)
    {
        string? value = await DisplayPromptAsync("Change Cap", "Cap:", "Save", "Cancel", keyboard: Keyboard.Numeric, initialValue: allowance.Total.ToString());
        if (!int.TryParse(value, out int total))
            return;

        try
        {
            await _allowances.SetTotalAsync(allowance.Id, Math.Max(1, total));
            await RefreshAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task SetStreakAsync(Allowance allowance)
    {
        string? value = await DisplayPromptAsync(
            "Change Streak",
            "Streak (0 to 3):",
            "Save",
            "Cancel",
            keyboard: Keyboard.Numeric,
            initialValue: allowance.SuccessStreak.ToString());

        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!int.TryParse(value, out int newStreak))
            return;

        var success = await _allowances.SetSuccessStreakAsync(allowance.Id, newStreak);
        if (success)
        {
            allowance.SuccessStreak = Math.Clamp(newStreak, 0, 3);
            await RefreshAsync();
        }
        else
        {
            await DisplayAlert("Save failed", "Could not update streak. Read-only device?", "OK");
        }
    }

    private async Task ToggleDailyPromptAsync(Allowance allowance)
    {
        try
        {
            await _allowances.SetPromptDailyOnHomeAsync(allowance.Id, !allowance.PromptDailyOnHome);
            await RefreshAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task DeleteAsync(Allowance allowance)
    {
        if (!await DisplayAlert("Delete Allowance?", $"Delete \"{allowance.Title}\"?", "Delete", "Cancel"))
            return;

        try
        {
            await _allowances.DeleteAllowanceAsync(allowance.Id);
            await RefreshAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task ShowReadOnlyAlertAsync()
    {
        await DisplayAlert("Read-only", "Read-only on this device. Sync from master to modify allowances.", "OK");
    }

    private static IEnumerable<(DateTime date, bool success)> ParseHistory(string? history)
    {
        foreach (var token in (history ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = token.Split(':', 2);
            if (parts.Length != 2)
                continue;

            if (!DateTime.TryParse(parts[0], out var date))
                continue;

            if (parts[1] == "S")
                yield return (date, true);
            else if (parts[1] == "F")
                yield return (date, false);
        }
    }

    private static Button SmallButton(string text, string color)
    {
        return new Button
        {
            Text = text,
            FontSize = 13,
            HeightRequest = 36,
            BackgroundColor = Color.FromArgb(color),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(12, 0)
        };
    }
}

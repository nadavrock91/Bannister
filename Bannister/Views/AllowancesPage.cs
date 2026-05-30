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
            Text = "Track any allowance: budget, consumption, behavior, anything with a cap.",
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
        bool atMin = allowance.Current <= 0;
        bool atMax = allowance.Current >= allowance.Total;
        double progress = allowance.Total <= 0 ? 0 : (double)allowance.Current / allowance.Total;

        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = atMax ? Color.FromArgb("#EF9A9A") : Color.FromArgb("#80CBC4"),
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

        stack.Children.Add(new Label
        {
            Text = $"{allowance.Current} / {allowance.Total}",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = atMax ? Color.FromArgb("#C62828") : Color.FromArgb("#2E7D32")
        });

        stack.Children.Add(new ProgressBar
        {
            Progress = progress,
            ProgressColor = atMax ? Color.FromArgb("#C62828") : Color.FromArgb("#2E7D32"),
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            HeightRequest = 8
        });

        var actions = new HorizontalStackLayout { Spacing = 8 };

        var decrement = SmallButton("-", atMin ? "#BDBDBD" : "#1565C0");
        decrement.WidthRequest = 48;
        decrement.IsEnabled = !atMin;
        decrement.Clicked += async (_, _) =>
        {
            await _allowances.DecrementAsync(allowance.Id);
            await RefreshAsync();
        };
        actions.Children.Add(decrement);

        var increment = SmallButton("+", atMax ? "#BDBDBD" : "#2E7D32");
        increment.WidthRequest = 48;
        increment.IsEnabled = !atMax;
        increment.Clicked += async (_, _) =>
        {
            await _allowances.IncrementAsync(allowance.Id);
            await RefreshAsync();
        };
        actions.Children.Add(increment);

        var edit = SmallButton("Edit", "#00695C");
        edit.Clicked += async (_, _) => await ShowEditMenuAsync(allowance);
        actions.Children.Add(edit);

        stack.Children.Add(actions);
        frame.Content = stack;
        return frame;
    }

    private async Task AddAllowanceAsync()
    {
        string? title = await DisplayPromptAsync("Add Allowance", "Allowance title:", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(title))
            return;

        string? totalText = await DisplayPromptAsync("Total Allowance", "Total cap:", "Add", "Cancel", keyboard: Keyboard.Numeric, initialValue: "1");
        if (!int.TryParse(totalText, out int total))
            return;

        await _allowances.AddAllowanceAsync(_auth.CurrentUsername, title.Trim(), total);
        await RefreshAsync();
    }

    private async Task ShowEditMenuAsync(Allowance allowance)
    {
        string? action = await DisplayActionSheet(
            allowance.Title,
            "Cancel",
            null,
            "Rename",
            "Set Current Value",
            "Change Total Allowance",
            "Reset to 0",
            $"Daily Prompt (current: {(allowance.PromptDailyOnHome ? "On" : "Off")})",
            "Delete");

        if (action == "Rename")
        {
            await RenameAsync(allowance);
        }
        else if (action == "Set Current Value")
        {
            await SetCurrentAsync(allowance);
        }
        else if (action == "Change Total Allowance")
        {
            await SetTotalAsync(allowance);
        }
        else if (action == "Reset to 0")
        {
            await ResetAsync(allowance);
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

        await _allowances.UpdateTitleAsync(allowance.Id, title.Trim());
        await RefreshAsync();
    }

    private async Task SetCurrentAsync(Allowance allowance)
    {
        string? value = await DisplayPromptAsync("Set Current Value", "Current count:", "Save", "Cancel", keyboard: Keyboard.Numeric, initialValue: allowance.Current.ToString());
        if (!int.TryParse(value, out int current))
            return;

        await _allowances.SetCurrentAsync(allowance.Id, current);
        await RefreshAsync();
    }

    private async Task SetTotalAsync(Allowance allowance)
    {
        string? value = await DisplayPromptAsync("Change Total Allowance", "Total cap:", "Save", "Cancel", keyboard: Keyboard.Numeric, initialValue: allowance.Total.ToString());
        if (!int.TryParse(value, out int total))
            return;

        await _allowances.SetTotalAsync(allowance.Id, total);
        await RefreshAsync();
    }

    private async Task ResetAsync(Allowance allowance)
    {
        if (!await DisplayAlert("Reset Allowance?", $"Reset \"{allowance.Title}\" to 0?", "Reset", "Cancel"))
            return;

        await _allowances.ResetAsync(allowance.Id);
        await RefreshAsync();
    }

    private async Task ToggleDailyPromptAsync(Allowance allowance)
    {
        await _allowances.SetPromptDailyOnHomeAsync(allowance.Id, !allowance.PromptDailyOnHome);
        await RefreshAsync();
    }

    private async Task DeleteAsync(Allowance allowance)
    {
        if (!await DisplayAlert("Delete Allowance?", $"Delete \"{allowance.Title}\"?", "Delete", "Cancel"))
            return;

        await _allowances.DeleteAllowanceAsync(allowance.Id);
        await RefreshAsync();
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

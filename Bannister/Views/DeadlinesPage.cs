using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DeadlinesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DeadlineService _deadlines;
    private readonly int _bucket;

    private readonly Label _allowanceLabel = new();
    private readonly VerticalStackLayout _listStack = new() { Spacing = 12 };

    public DeadlinesPage(AuthService auth, DeadlineService deadlines, int bucket)
    {
        _auth = auth;
        _deadlines = deadlines;
        _bucket = bucket;
        Title = $"{DeadlineService.BucketName(bucket)} Deadlines";
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
            Text = Title,
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#6A1B9A")
        });

        var allowanceRow = new HorizontalStackLayout { Spacing = 10 };
        _allowanceLabel.FontSize = 15;
        _allowanceLabel.FontAttributes = FontAttributes.Bold;
        _allowanceLabel.VerticalOptions = LayoutOptions.Center;
        allowanceRow.Children.Add(_allowanceLabel);

        var gear = SmallButton("Allowance", "#6A1B9A");
        gear.Clicked += async (_, _) => await EditAllowanceAsync();
        allowanceRow.Children.Add(gear);
        root.Children.Add(allowanceRow);

        var add = SmallButton("+ Add Deadline", "#2E7D32");
        add.HeightRequest = 42;
        add.HorizontalOptions = LayoutOptions.Start;
        add.Clicked += async (_, _) => await AddDeadlineAsync();
        root.Children.Add(add);

        root.Children.Add(_listStack);
        Content = new ScrollView { Content = root };
    }

    private async Task RefreshAsync()
    {
        int activeCount = await _deadlines.GetActiveCountAsync(_auth.CurrentUsername, _bucket);
        int allowance = _deadlines.GetAllowance(_auth.CurrentUsername, _bucket);
        _allowanceLabel.Text = $"Active: {activeCount} / {allowance}";
        _allowanceLabel.TextColor = activeCount >= allowance ? Color.FromArgb("#C62828") : Color.FromArgb("#2E7D32");

        var deadlines = await _deadlines.GetDeadlinesAsync(_auth.CurrentUsername, _bucket);
        _listStack.Children.Clear();
        AddSection("ACTIVE", deadlines.Where(d => d.State == DeadlineService.StateActive));
        AddSection("POSSIBLE", deadlines.Where(d => d.State == DeadlineService.StatePossible));
        AddSection("FAILED", deadlines.Where(d => d.State == DeadlineService.StateFailed));
        AddSection("ARCHIVED", deadlines.Where(d => d.State == DeadlineService.StateArchived));
    }

    private void AddSection(string header, IEnumerable<Deadline> rows)
    {
        var items = rows.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase).ToList();
        _listStack.Children.Add(new Label
        {
            Text = header,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4A148C"),
            Margin = new Thickness(0, 8, 0, 0)
        });

        if (items.Count == 0)
        {
            _listStack.Children.Add(new Label { Text = "None.", FontSize = 13, TextColor = Color.FromArgb("#888"), Margin = new Thickness(4, 0) });
            return;
        }

        foreach (var deadline in items)
            _listStack.Children.Add(BuildDeadlineRow(deadline));
    }

    private View BuildDeadlineRow(Deadline deadline)
    {
        var frame = new Frame
        {
            BackgroundColor = RowColor(deadline.State),
            BorderColor = Color.FromArgb("#D1C4E9"),
            CornerRadius = 8,
            HasShadow = false,
            Padding = 12
        };

        var stack = new VerticalStackLayout { Spacing = 8 };
        var title = new Label
        {
            Text = deadline.Title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await EditDeadlineAsync(deadline);
        title.GestureRecognizers.Add(tap);
        stack.Children.Add(title);
        stack.Children.Add(new Label
        {
            Text = $"Due: {DeadlineService.ComputePeriodEnd(deadline.Bucket, deadline.CreatedAt):MMM d, yyyy h:mm tt}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        var actions = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center
        };
        AddStateActions(actions, deadline);
        stack.Children.Add(actions);

        frame.Content = stack;
        return frame;
    }

    private void AddStateActions(FlexLayout actions, Deadline deadline)
    {
        if (deadline.State == DeadlineService.StateActive)
        {
            AddAction(actions, "Complete", "#2E7D32", async () => await SetStateAndRefreshAsync(deadline.Id, DeadlineService.StateArchived));
            AddAction(actions, "Failed", "#C62828", async () => await SetStateAndRefreshAsync(deadline.Id, DeadlineService.StateFailed));
            AddAction(actions, "Move to Possible", "#6A1B9A", async () => await SetStateAndRefreshAsync(deadline.Id, DeadlineService.StatePossible));
        }
        else if (deadline.State == DeadlineService.StatePossible)
        {
            AddAction(actions, "Promote to Active", "#2E7D32", async () => await PromoteAsync(deadline));
            AddAction(actions, "Archive", "#607D8B", async () => await SetStateAndRefreshAsync(deadline.Id, DeadlineService.StateArchived));
        }
        else if (deadline.State == DeadlineService.StateFailed)
        {
            AddAction(actions, "Move to Possible", "#6A1B9A", async () => await SetStateAndRefreshAsync(deadline.Id, DeadlineService.StatePossible));
            AddAction(actions, "Archive", "#607D8B", async () => await SetStateAndRefreshAsync(deadline.Id, DeadlineService.StateArchived));
        }
        else
        {
            AddAction(actions, "Move to Possible", "#6A1B9A", async () => await SetStateAndRefreshAsync(deadline.Id, DeadlineService.StatePossible));
        }

        AddAction(actions, "Edit", "#1565C0", async () => await EditDeadlineAsync(deadline));
        AddAction(actions, "Delete", "#C62828", async () => await DeleteAsync(deadline));
    }

    private void AddAction(FlexLayout actions, string text, string color, Func<Task> action)
    {
        var button = SmallButton(text, color);
        button.Clicked += async (_, _) => await action();
        actions.Children.Add(button);
    }

    private async Task AddDeadlineAsync()
    {
        string? title = await DisplayPromptAsync("Add Deadline", "Deadline title:", "Add", "Cancel");
        if (string.IsNullOrWhiteSpace(title))
            return;

        int active = await _deadlines.GetActiveCountAsync(_auth.CurrentUsername, _bucket);
        int allowance = _deadlines.GetAllowance(_auth.CurrentUsername, _bucket);
        int state = active < allowance ? DeadlineService.StateActive : DeadlineService.StatePossible;
        await _deadlines.AddDeadlineAsync(_auth.CurrentUsername, title.Trim(), _bucket, state);
        if (state == DeadlineService.StatePossible)
            await DisplayAlert("Allowance Full", "Active allowance is full, so this deadline was added as Possible.", "OK");
        await RefreshAsync();
    }

    private async Task PromoteAsync(Deadline deadline)
    {
        int active = await _deadlines.GetActiveCountAsync(_auth.CurrentUsername, deadline.Bucket);
        int allowance = _deadlines.GetAllowance(_auth.CurrentUsername, deadline.Bucket);
        if (active >= allowance)
        {
            await DisplayAlert("Allowance Full", $"Active allowance is already {active}/{allowance}. Increase the allowance first or move another deadline out of Active.", "OK");
            return;
        }

        await SetStateAndRefreshAsync(deadline.Id, DeadlineService.StateActive);
    }

    private async Task SetStateAndRefreshAsync(int id, int state)
    {
        await _deadlines.SetStateAsync(id, state);
        await RefreshAsync();
    }

    private async Task EditAllowanceAsync()
    {
        int current = _deadlines.GetAllowance(_auth.CurrentUsername, _bucket);
        string? value = await DisplayPromptAsync("Edit Allowance", "Maximum Active deadlines:", "Save", "Cancel", keyboard: Keyboard.Numeric, initialValue: current.ToString());
        if (!int.TryParse(value, out int allowance) || allowance < 1)
            return;

        _deadlines.SetAllowance(_auth.CurrentUsername, _bucket, allowance);
        await RefreshAsync();
    }

    private async Task EditDeadlineAsync(Deadline deadline)
    {
        var options = new List<string> { "Rename" };
        for (int bucket = DeadlineService.BucketDaily; bucket <= DeadlineService.BucketMonthly; bucket++)
        {
            if (bucket != deadline.Bucket)
                options.Add($"Move to {DeadlineService.BucketName(bucket)}");
        }

        string? action = await DisplayActionSheet("Edit Deadline", "Cancel", null, options.ToArray());
        if (action == "Rename")
        {
            string? title = await DisplayPromptAsync("Rename Deadline", "Title:", "Save", "Cancel", initialValue: deadline.Title);
            if (!string.IsNullOrWhiteSpace(title))
                await _deadlines.UpdateTitleAsync(deadline.Id, title.Trim());
        }
        else if (action?.StartsWith("Move to ", StringComparison.OrdinalIgnoreCase) == true)
        {
            int target = action.Contains("Weekly", StringComparison.OrdinalIgnoreCase)
                ? DeadlineService.BucketWeekly
                : action.Contains("Monthly", StringComparison.OrdinalIgnoreCase)
                    ? DeadlineService.BucketMonthly
                    : DeadlineService.BucketDaily;
            await _deadlines.MoveBucketAsync(deadline.Id, target);
        }

        await RefreshAsync();
    }

    private async Task DeleteAsync(Deadline deadline)
    {
        if (!await DisplayAlert("Delete Deadline?", $"Delete \"{deadline.Title}\"?", "Delete", "Cancel"))
            return;

        await _deadlines.DeleteDeadlineAsync(deadline.Id);
        await RefreshAsync();
    }

    private static Color RowColor(int state) => state switch
    {
        DeadlineService.StateActive => Color.FromArgb("#E8F5E9"),
        DeadlineService.StatePossible => Color.FromArgb("#E3F2FD"),
        DeadlineService.StateFailed => Color.FromArgb("#FFEBEE"),
        _ => Color.FromArgb("#ECEFF1")
    };

    private static Button SmallButton(string text, string color)
    {
        return new Button
        {
            Text = text,
            FontSize = 12,
            HeightRequest = 34,
            BackgroundColor = Color.FromArgb(color),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(10, 0),
            Margin = new Thickness(4, 2)
        };
    }
}

using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class PostponedTasksManagementPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly PostponedTaskService _postponedTasks;

    private Grid _root = null!;
    private VerticalStackLayout _activeStack = null!;
    private VerticalStackLayout _completedStack = null!;
    private VerticalStackLayout _disabledStack = null!;
    private Button _completedToggle = null!;
    private Button _disabledToggle = null!;
    private bool _showCompleted;
    private bool _showDisabled;

    public PostponedTasksManagementPage(AuthService auth, PostponedTaskService postponedTasks)
    {
        _auth = auth;
        _postponedTasks = postponedTasks;
        Title = "Postponed Tasks";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        _root = new Grid { BackgroundColor = Color.FromArgb("#F5F5F5") };

        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };

        stack.Children.Add(new Label
        {
            Text = "Postponed Tasks Management",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#EF6C00")
        });

        var newButton = new Button
        {
            Text = "+ New Postponed Task",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };
        newButton.Clicked += async (_, _) => await CreatePostponedTaskAsync();
        stack.Children.Add(newButton);

        _activeStack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(BuildSectionFrame("Active Postponed Tasks", _activeStack));

        _completedToggle = new Button
        {
            Text = "Show completed",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };
        _completedToggle.Clicked += async (_, _) =>
        {
            _showCompleted = !_showCompleted;
            await LoadAsync();
        };
        stack.Children.Add(_completedToggle);

        _completedStack = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        stack.Children.Add(BuildSectionFrame("Completed", _completedStack));

        _disabledToggle = new Button
        {
            Text = "Show disabled",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };
        _disabledToggle.Clicked += async (_, _) =>
        {
            _showDisabled = !_showDisabled;
            await LoadAsync();
        };
        stack.Children.Add(_disabledToggle);

        _disabledStack = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        stack.Children.Add(BuildSectionFrame("Disabled", _disabledStack));

        _root.Children.Add(new ScrollView { Content = stack });
        Content = _root;
    }

    private static Frame BuildSectionFrame(string title, VerticalStackLayout content)
    {
        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 10,
            Padding = 12,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = title,
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333")
                    },
                    content
                }
            }
        };
    }

    private async Task LoadAsync()
    {
        var username = _auth.CurrentUsername;
        var active = await _postponedTasks.GetActiveAsync(username);
        var completed = await _postponedTasks.GetCompletedAsync(username);
        var disabled = await _postponedTasks.GetDisabledAsync(username);

        _activeStack.Children.Clear();
        if (active.Count == 0)
        {
            _activeStack.Children.Add(EmptyLabel("No active postponed tasks."));
        }
        else
        {
            foreach (var task in active)
                _activeStack.Children.Add(BuildActiveCard(task));
        }

        _completedToggle.Text = _showCompleted ? $"Hide {completed.Count} completed" : $"Show {completed.Count} completed";
        _completedStack.IsVisible = _showCompleted;
        _completedStack.Children.Clear();
        if (_showCompleted)
        {
            if (completed.Count == 0)
                _completedStack.Children.Add(EmptyLabel("No completed postponed tasks."));
            else
                foreach (var task in completed)
                    _completedStack.Children.Add(BuildReadOnlyCard(task, "Completed", task.CompletedAt));
        }

        _disabledToggle.Text = _showDisabled ? $"Hide {disabled.Count} disabled" : $"Show {disabled.Count} disabled";
        _disabledStack.IsVisible = _showDisabled;
        _disabledStack.Children.Clear();
        if (_showDisabled)
        {
            if (disabled.Count == 0)
                _disabledStack.Children.Add(EmptyLabel("No disabled postponed tasks."));
            else
                foreach (var task in disabled)
                    _disabledStack.Children.Add(BuildDisabledCard(task));
        }
    }

    private static Label EmptyLabel(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontAttributes = FontAttributes.Italic,
        TextColor = Color.FromArgb("#777"),
        Margin = new Thickness(4, 4)
    };

    private View BuildActiveCard(PostponedTask task)
    {
        var buttons = new HorizontalStackLayout { Spacing = 8 };

        var postpone = SmallButton("Postpone", "#FFF3E0", "#EF6C00");
        postpone.Clicked += async (_, _) => await PostponeTaskAsync(task);
        buttons.Children.Add(postpone);

        var done = SmallButton("Mark Done", "#E8F5E9", "#2E7D32");
        done.Clicked += async (_, _) => await MarkDoneAsync(task);
        buttons.Children.Add(done);

        var disable = SmallButton("Disable", "#FFEBEE", "#C62828");
        disable.Clicked += async (_, _) => await DisableAsync(task);
        buttons.Children.Add(disable);

        return BuildTaskFrame(task, $"Current: {task.CurrentDate:ddd, MMM d, yyyy}", buttons);
    }

    private View BuildDisabledCard(PostponedTask task)
    {
        var buttons = new HorizontalStackLayout { Spacing = 8 };
        var reactivate = SmallButton("Reactivate", "#E3F2FD", "#1565C0");
        reactivate.Clicked += async (_, _) => await ReactivateAsync(task);
        buttons.Children.Add(reactivate);

        return BuildTaskFrame(task, $"Disabled: {(task.DisabledAt?.ToLocalTime().ToString("MMM d, yyyy") ?? "unknown")}", buttons);
    }

    private View BuildReadOnlyCard(PostponedTask task, string label, DateTime? date)
    {
        return BuildTaskFrame(task, $"{label}: {(date?.ToLocalTime().ToString("MMM d, yyyy") ?? "unknown")}", null);
    }

    private static Frame BuildTaskFrame(PostponedTask task, string dateLine, View? actions)
    {
        var stack = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(new Label
        {
            Text = task.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        stack.Children.Add(new Label
        {
            Text = $"{dateLine} · Postponed {task.TimesPostponed}x",
            FontSize = 11,
            TextColor = Color.FromArgb("#EF6C00")
        });
        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            stack.Children.Add(new Label
            {
                Text = task.Description,
                FontSize = 12,
                TextColor = Color.FromArgb("#666"),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }
        if (actions != null)
            stack.Children.Add(actions);

        return new Frame
        {
            BackgroundColor = Color.FromArgb("#FFF8E1"),
            BorderColor = Color.FromArgb("#FFB74D"),
            CornerRadius = 8,
            Padding = 10,
            HasShadow = false,
            Content = stack
        };
    }

    private static Button SmallButton(string text, string bg, string fg) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb(bg),
        TextColor = Color.FromArgb(fg),
        CornerRadius = 6,
        FontSize = 11,
        HeightRequest = 32,
        Padding = new Thickness(10, 0)
    };

    private async Task CreatePostponedTaskAsync()
    {
        if (_postponedTasks.IsReadOnly)
        {
            await DisplayAlert("Read-only", "Postponed tasks cannot be edited on this device.", "OK");
            return;
        }

        string? title = await DisplayPromptAsync("New Postponed Task", "Task title:", "Next", "Cancel", placeholder: "Write blog post");
        if (string.IsNullOrWhiteSpace(title)) return;

        string? description = await DisplayPromptAsync("Description", "Optional description:", "Next", "Skip", initialValue: "");
        var date = await ShowDatePickerModalAsync("Scheduled date", DateTime.Today);
        if (date == null) return;

        await _postponedTasks.CreateAsync(_auth.CurrentUsername, title.Trim(), description ?? "", date.Value);
        await LoadAsync();
    }

    private async Task PostponeTaskAsync(PostponedTask task)
    {
        var date = await ChooseScheduledDateAsync("Postpone task", task.CurrentDate.Date);
        if (date == null) return;

        await _postponedTasks.PostponeAsync(task.Id, date.Value);
        await LoadAsync();
    }

    private async Task MarkDoneAsync(PostponedTask task)
    {
        if (!await DisplayAlert("Mark done?", $"Mark \"{task.Title}\" done?", "Mark Done", "Cancel"))
            return;

        await _postponedTasks.MarkDoneAsync(task.Id);
        await LoadAsync();
    }

    private async Task DisableAsync(PostponedTask task)
    {
        if (!await DisplayAlert("Disable task?", $"Disable \"{task.Title}\" and remove it from the calendar?", "Disable", "Cancel"))
            return;

        await _postponedTasks.DisableAsync(task.Id);
        await LoadAsync();
    }

    private async Task ReactivateAsync(PostponedTask task)
    {
        var date = await ChooseScheduledDateAsync("Reactivate task", DateTime.Today);
        if (date == null) return;

        await _postponedTasks.ReactivateAsync(task.Id, date.Value);
        await LoadAsync();
    }

    private async Task<DateTime?> ChooseScheduledDateAsync(string title, DateTime baseDate)
    {
        var action = await DisplayActionSheet(
            title,
            "Cancel",
            null,
            "7 days",
            "30 days",
            "90 days",
            "180 days",
            "365 days",
            "Pick a date...");
        return action switch
        {
            "7 days" => baseDate.AddDays(7),
            "30 days" => baseDate.AddDays(30),
            "90 days" => baseDate.AddDays(90),
            "180 days" => baseDate.AddDays(180),
            "365 days" => baseDate.AddDays(365),
            "Pick a date..." => await ShowDatePickerModalAsync(title, baseDate),
            _ => null
        };
    }

    private async Task<DateTime?> ShowDatePickerModalAsync(string title, DateTime initialDate)
    {
        var tcs = new TaskCompletionSource<DateTime?>();
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var picker = new DatePicker
        {
            Date = initialDate.Date,
            BackgroundColor = Color.FromArgb("#FFF3E0")
        };

        var save = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#EF6C00"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 38
        };
        var cancel = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 38
        };

        var row = new HorizontalStackLayout { Spacing = 10, Children = { cancel, save } };
        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 420,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#EF6C00") },
                    picker,
                    row
                }
            }
        };

        overlay.Children.Add(card);
        _root.Children.Add(overlay);

        void Close(DateTime? result)
        {
            _root.Children.Remove(overlay);
            tcs.TrySetResult(result);
        }

        save.Clicked += (_, _) => Close(picker.Date);
        cancel.Clicked += (_, _) => Close(null);

        return await tcs.Task;
    }
}

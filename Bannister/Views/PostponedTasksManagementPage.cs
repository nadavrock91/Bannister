using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class PostponedTasksManagementPage : Bannister.Views.ModeThemedPage
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

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        _root = (new Grid { }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };

        stack.Children.Add((new Label
        {
            Text = "Postponed Tasks Management",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningAmber, ThemeKey.PageText1, classifyPageText: true));

        var newButton = (new Button
        {
            Text = "+ New Postponed Task",
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessDarkButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        newButton.Clicked += async (_, _) => await CreatePostponedTaskAsync();
        stack.Children.Add(newButton);

        _activeStack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(BuildSectionFrame("Active Postponed Tasks", _activeStack));

        _completedToggle = (new Button
        {
            Text = "Show completed",
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        _completedToggle.Clicked += async (_, _) =>
        {
            _showCompleted = !_showCompleted;
            await LoadAsync();
        };
        stack.Children.Add(_completedToggle);

        _completedStack = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        stack.Children.Add(BuildSectionFrame("Completed", _completedStack));

        _disabledToggle = (new Button
        {
            Text = "Show disabled",
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
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
        return (new Frame
        {
            CornerRadius = 10,
            Padding = 12,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    (new Label
                    {
                        Text = title,
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                    content
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
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

    private static Label EmptyLabel(string text) => (new Label(){
        Text = text,
        FontSize = 13,
        FontAttributes = FontAttributes.Italic,
        Margin = new Thickness(4, 4)
    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);

    private View BuildActiveCard(PostponedTask task)
    {
        var buttons = new HorizontalStackLayout { Spacing = 8 };

        var postpone = SmallButton("Postpone", ThemeKey.WarningBackground, ThemeKey.WarningAmber);
        postpone.Clicked += async (_, _) => await PostponeTaskAsync(task);
        buttons.Children.Add(postpone);

        var done = SmallButton("Mark Done", ThemeKey.SuccessBackground, ThemeKey.SuccessText);
        done.Clicked += async (_, _) => await MarkDoneAsync(task);
        buttons.Children.Add(done);

        var disable = SmallButton("Disable", ThemeKey.ErrorBackground, ThemeKey.ErrorText);
        disable.Clicked += async (_, _) => await DisableAsync(task);
        buttons.Children.Add(disable);

        return BuildTaskFrame(task, $"Current: {task.CurrentDate:ddd, MMM d, yyyy}", buttons);
    }

    private View BuildDisabledCard(PostponedTask task)
    {
        var buttons = new HorizontalStackLayout { Spacing = 8 };
        var reactivate = SmallButton("Reactivate", ThemeKey.RowSelected, ThemeKey.InfoText);
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
        stack.Children.Add((new Label
        {
            Text = task.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = $"{dateLine} · Postponed {task.TimesPostponed}x",
            FontSize = 11
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningAmber, ThemeKey.PageText3, classifyPageText: true));
        if (!string.IsNullOrWhiteSpace(task.Description))
        {
            stack.Children.Add((new Label
            {
                Text = task.Description,
                FontSize = 12,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        }
        if (actions != null)
            stack.Children.Add(actions);

        return (new Frame
        {
            CornerRadius = 8,
            Padding = 10,
            HasShadow = false,
            Content = stack
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningLightButtonBackground);
    }

    private static Button SmallButton(string text, ThemeKey bg, ThemeKey fg) => (new Button(){
        Text = text,
        CornerRadius = 6,
        FontSize = 11,
        HeightRequest = 32,
        Padding = new Thickness(10, 0)
    }).ThemeColor(VisualElement.BackgroundColorProperty, bg).ThemeColor(Button.TextColorProperty, fg);

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
        var overlay = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var picker = (new DatePicker
        {
            Date = initialDate.Date
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground);

        var save = (new Button
        {
            Text = "Save",
            CornerRadius = 8,
            HeightRequest = 38
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningAmber).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        var cancel = (new Button
        {
            Text = "Cancel",
            CornerRadius = 8,
            HeightRequest = 38
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);

        var row = new HorizontalStackLayout { Spacing = 10, Children = { cancel, save } };
        var card = (new Frame
        {
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
                    (new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningAmber, ThemeKey.PageText1, classifyPageText: true),
                    picker,
                    row
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

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

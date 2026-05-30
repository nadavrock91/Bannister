using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class RoutinesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly RoutineService _routines;

    private VerticalStackLayout _activeStack = new();
    private VerticalStackLayout _inactiveStack = new();

    public RoutinesPage(AuthService auth, RoutineService routines)
    {
        _auth = auth;
        _routines = routines;
        Title = "Routines";
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
        var root = new VerticalStackLayout { Padding = 16, Spacing = 12 };

        root.Children.Add(new Label
        {
            Text = "Routines",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });
        root.Children.Add(new Label
        {
            Text = "Recurring tasks that show on your calendar.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        var addButton = new Button
        {
            Text = "+ Add Routine",
            FontSize = 14,
            HeightRequest = 42,
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(16, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        addButton.Clicked += async (_, _) => await AddRoutineAsync();
        root.Children.Add(addButton);

        root.Children.Add(new Label
        {
            Text = "Active Routines",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2E7D32"),
            Margin = new Thickness(0, 8, 0, 0)
        });
        _activeStack = new VerticalStackLayout { Spacing = 8 };
        root.Children.Add(_activeStack);

        root.Children.Add(new Label
        {
            Text = "Inactive Routines",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#795548"),
            Margin = new Thickness(0, 10, 0, 0)
        });
        _inactiveStack = new VerticalStackLayout { Spacing = 8 };
        root.Children.Add(_inactiveStack);

        Content = new ScrollView { Content = root };
    }

    private async Task LoadAsync()
    {
        var routines = await _routines.GetRoutinesAsync(_auth.CurrentUsername);
        _activeStack.Children.Clear();
        _inactiveStack.Children.Clear();

        var active = routines.Where(r => r.IsActive).ToList();
        var inactive = routines.Where(r => !r.IsActive).ToList();

        if (active.Count == 0)
            _activeStack.Children.Add(EmptyLabel("No active routines."));
        else
            foreach (var routine in active)
                _activeStack.Children.Add(BuildRoutineRow(routine));

        if (inactive.Count == 0)
            _inactiveStack.Children.Add(EmptyLabel("No inactive routines."));
        else
            foreach (var routine in inactive)
                _inactiveStack.Children.Add(BuildRoutineRow(routine));
    }

    private View BuildRoutineRow(Routine routine)
    {
        var frame = new Frame
        {
            BackgroundColor = routine.IsActive ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#EFEBE9"),
            BorderColor = routine.IsActive ? Color.FromArgb("#81C784") : Color.FromArgb("#BCAAA4"),
            CornerRadius = 8,
            Padding = 12,
            HasShadow = false
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        var text = new VerticalStackLayout { Spacing = 3 };
        text.Children.Add(new Label
        {
            Text = routine.Name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        text.Children.Add(new Label
        {
            Text = $"{RoutineService.FormatRoutineFrequency(routine)} · started {routine.StartDate:MMM d, yyyy}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });
        grid.Add(text, 0, 0);

        var actions = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.End,
            AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center
        };

        var edit = SmallButton("Edit", "#1565C0");
        edit.Clicked += async (_, _) => await EditRoutineAsync(routine);
        actions.Children.Add(edit);

        if (routine.IsActive)
        {
            var inactive = SmallButton("Make Inactive", "#795548");
            inactive.Clicked += async (_, _) =>
            {
                await _routines.SetRoutineActiveAsync(routine.Id, false);
                await LoadAsync();
            };
            actions.Children.Add(inactive);
        }
        else
        {
            var resume = SmallButton("Resume", "#2E7D32");
            resume.Clicked += async (_, _) => await ResumeRoutineAsync(routine);
            actions.Children.Add(resume);
        }

        var delete = SmallButton("Delete", "#C62828");
        delete.Clicked += async (_, _) =>
        {
            if (!await DisplayAlert("Delete Routine?", $"Delete \"{routine.Name}\" and all its calendar instances?", "Delete", "Cancel"))
                return;

            await _routines.DeleteRoutineAsync(routine.Id);
            await LoadAsync();
        };
        actions.Children.Add(delete);

        grid.Add(actions, 1, 0);
        frame.Content = grid;
        return frame;
    }

    private async Task AddRoutineAsync()
    {
        string? name = await DisplayPromptAsync("New Routine", "Routine name:", "Next", "Cancel", placeholder: "shave");
        if (string.IsNullOrWhiteSpace(name))
            return;

        var frequency = await PickFrequencyAsync();
        if (frequency == null)
            return;

        DateTime? startDate = await PickStartDateAsync("Start Date", DateTime.Today);
        if (startDate == null)
            return;

        await _routines.AddRoutineAsync(
            _auth.CurrentUsername,
            name.Trim(),
            frequency.FrequencyDays,
            startDate.Value,
            frequency.FrequencyType,
            frequency.DayOfMonth,
            frequency.WeekOrdinal,
            frequency.DayOfWeek);
        await LoadAsync();
    }

    private async Task EditRoutineAsync(Routine routine)
    {
        string? name = await DisplayPromptAsync("Edit Routine", "Routine name:", "Save", "Cancel", initialValue: routine.Name);
        if (string.IsNullOrWhiteSpace(name))
            return;

        var frequency = await PickFrequencyAsync(routine);
        if (frequency == null)
            return;

        routine.Name = name.Trim();
        routine.FrequencyDays = frequency.FrequencyDays;
        routine.FrequencyType = frequency.FrequencyType;
        routine.DayOfMonth = frequency.DayOfMonth;
        routine.WeekOrdinal = frequency.WeekOrdinal;
        routine.DayOfWeek = frequency.DayOfWeek;
        await _routines.UpdateRoutineAsync(routine);
        await LoadAsync();
    }

    private async Task ResumeRoutineAsync(Routine routine)
    {
        DateTime? startDate = await PickStartDateAsync("Resume Routine", DateTime.Today);
        if (startDate == null)
            return;

        await _routines.SetRoutineActiveAsync(routine.Id, true, startDate.Value);
        await LoadAsync();
    }

    private async Task<RoutineFrequencySelection?> PickFrequencyAsync(Routine? current = null)
    {
        string? type = await DisplayActionSheet(
            current == null ? "Frequency Type" : $"Frequency Type (current: {RoutineService.FormatRoutineFrequency(current)})",
            "Cancel",
            null,
            "Every N days",
            "Day of month",
            "Nth weekday of month");

        return type switch
        {
            "Every N days" => await PickEveryNDaysFrequencyAsync(current?.FrequencyDays),
            "Day of month" => await PickDayOfMonthFrequencyAsync(current?.DayOfMonth),
            "Nth weekday of month" => await PickNthWeekdayFrequencyAsync(current?.WeekOrdinal, current?.DayOfWeek),
            _ => null
        };
    }

    private async Task<RoutineFrequencySelection?> PickEveryNDaysFrequencyAsync(int? current = null)
    {
        string? choice = await DisplayActionSheet(
            current.HasValue ? $"Frequency (current: {current.Value} days)" : "Frequency",
            "Cancel",
            null,
            "1 day",
            "7 days (weekly)",
            "30 days (monthly)",
            "180 days",
            "365 days (yearly)",
            "Manual entry");

        int? days = choice switch
        {
            "1 day" => 1,
            "7 days (weekly)" => 7,
            "30 days (monthly)" => 30,
            "180 days" => 180,
            "365 days (yearly)" => 365,
            "Manual entry" => await PromptManualFrequencyAsync(),
            _ => null
        };

        return days.HasValue
            ? new RoutineFrequencySelection { FrequencyType = 0, FrequencyDays = days.Value }
            : null;
    }

    private async Task<RoutineFrequencySelection?> PickDayOfMonthFrequencyAsync(int? current = null)
    {
        string? value = await DisplayPromptAsync(
            "Day of Month",
            "Day of month (1-31):",
            "OK",
            "Cancel",
            keyboard: Keyboard.Numeric,
            initialValue: current is >= 1 and <= 31 ? current.Value.ToString() : "");

        if (!int.TryParse(value, out int day) || day < 1 || day > 31)
            return null;

        return new RoutineFrequencySelection { FrequencyType = 1, DayOfMonth = day };
    }

    private async Task<RoutineFrequencySelection?> PickNthWeekdayFrequencyAsync(int? currentOrdinal = null, int? currentDayOfWeek = null)
    {
        string? ordinalChoice = await DisplayActionSheet(
            currentOrdinal.HasValue ? $"Which occurrence? (current: {OrdinalWord(currentOrdinal.Value)})" : "Which occurrence?",
            "Cancel",
            null,
            "First",
            "Second",
            "Third",
            "Fourth",
            "Last");

        int? ordinal = ordinalChoice switch
        {
            "First" => 1,
            "Second" => 2,
            "Third" => 3,
            "Fourth" => 4,
            "Last" => 5,
            _ => null
        };
        if (ordinal == null)
            return null;

        string? weekdayChoice = await DisplayActionSheet(
            currentDayOfWeek.HasValue ? $"Which weekday? (current: {WeekdayName(currentDayOfWeek.Value)})" : "Which weekday?",
            "Cancel",
            null,
            "Sunday",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday");

        int? dayOfWeek = weekdayChoice switch
        {
            "Sunday" => 0,
            "Monday" => 1,
            "Tuesday" => 2,
            "Wednesday" => 3,
            "Thursday" => 4,
            "Friday" => 5,
            "Saturday" => 6,
            _ => null
        };
        if (dayOfWeek == null)
            return null;

        return new RoutineFrequencySelection { FrequencyType = 2, WeekOrdinal = ordinal.Value, DayOfWeek = dayOfWeek.Value };
    }

    private async Task<int?> PromptManualFrequencyAsync()
    {
        string? value = await DisplayPromptAsync("Manual Frequency", "Every how many days?", "OK", "Cancel", keyboard: Keyboard.Numeric);
        return int.TryParse(value, out int days) && days > 0 ? days : null;
    }

    private async Task<DateTime?> PickStartDateAsync(string title, DateTime initialDate)
    {
        string? choice = await DisplayActionSheet(title, "Cancel", null, "Today", "Tomorrow", "Pick a date");
        return choice switch
        {
            "Today" => DateTime.Today,
            "Tomorrow" => DateTime.Today.AddDays(1),
            "Pick a date" => await ShowDatePickerModalAsync(title, initialDate),
            _ => null
        };
    }

    private async Task<DateTime?> ShowDatePickerModalAsync(string title, DateTime initialDate)
    {
        var tcs = new TaskCompletionSource<DateTime?>();
        var picker = new DatePicker
        {
            Date = initialDate.Date,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            FontSize = 14
        };

        ContentPage? page = null;
        bool closing = false;

        async Task CloseAsync(DateTime? result)
        {
            if (closing) return;
            closing = true;
            if (page != null)
                await Navigation.PopModalAsync(false);
            tcs.TrySetResult(result);
        }

        var save = SmallButton("Save", "#2E7D32");
        save.Clicked += async (_, _) => await CloseAsync(picker.Date);
        var cancel = SmallButton("Cancel", "#9E9E9E");
        cancel.Clicked += async (_, _) => await CloseAsync(null);

        page = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Content = new Grid
            {
                Padding = 24,
                Children =
                {
                    new Frame
                    {
                        BackgroundColor = Colors.White,
                        CornerRadius = 12,
                        Padding = 18,
                        HasShadow = true,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        WidthRequest = 420,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") },
                                picker,
                                new HorizontalStackLayout { Spacing = 10, Children = { save, cancel } }
                            }
                        }
                    }
                }
            }
        };

        await Navigation.PushModalAsync(page, false);
        return await tcs.Task;
    }

    private static Label EmptyLabel(string text) => new()
    {
        Text = text,
        FontSize = 13,
        TextColor = Color.FromArgb("#888"),
        Margin = new Thickness(4, 4)
    };

    private static Button SmallButton(string text, string color)
    {
        var button = new Button
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
        return button;
    }

    private static string OrdinalWord(int ordinal) => ordinal switch
    {
        1 => "first",
        2 => "second",
        3 => "third",
        4 => "fourth",
        5 => "last",
        _ => "unknown"
    };

    private static string WeekdayName(int dayOfWeek)
    {
        return ((System.DayOfWeek)Math.Clamp(dayOfWeek, 0, 6)).ToString();
    }

    private sealed class RoutineFrequencySelection
    {
        public int FrequencyType { get; set; }
        public int FrequencyDays { get; set; }
        public int DayOfMonth { get; set; }
        public int WeekOrdinal { get; set; }
        public int DayOfWeek { get; set; }
    }
}

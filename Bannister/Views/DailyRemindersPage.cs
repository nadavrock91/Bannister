using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DailyRemindersPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DailyReminderService _reminderService;
    private readonly TaskService _taskService;
    private VerticalStackLayout _contentContainer = null!;

    public DailyRemindersPage(
        AuthService auth,
        DailyReminderService reminderService,
        TaskService taskService)
    {
        _auth = auth;
        _reminderService = reminderService;
        _taskService = taskService;
        Title = "Daily Reminders";
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
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 12
        };

        stack.Children.Add(new Label
        {
            Text = " Daily Reminders",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Tap \"Send to Tomorrow\" to postpone a " +
                   "reminder as a calendar task for tomorrow.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var addBtn = new Button
        {
            Text = "+ Add Reminder",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(16, 0)
        };
        addBtn.Clicked += async (_, _) =>
            await AddReminderAsync();
        stack.Children.Add(addBtn);

        _contentContainer = new VerticalStackLayout
        {
            Spacing = 6
        };
        stack.Children.Add(_contentContainer);

        Content = new ScrollView { Content = stack };
    }

    private async Task LoadAsync()
    {
        _contentContainer.Children.Clear();

        var reminders = await _reminderService
            .GetAllAsync(_auth.CurrentUsername);

        if (reminders.Count == 0)
        {
            _contentContainer.Children.Add(new Label
            {
                Text = "No reminders yet. " +
                       "Tap + Add Reminder to create one.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        foreach (var reminder in reminders)
            _contentContainer.Children.Add(
                BuildReminderCard(reminder));
    }

    private View BuildReminderCard(DailyReminder reminder)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(120)),
                new ColumnDefinition(new GridLength(36))
            },
            ColumnSpacing = 6,
            Padding = new Thickness(12, 10),
            BackgroundColor = Colors.White
        };

        var infoStack = new VerticalStackLayout { Spacing = 2 };
        infoStack.Children.Add(new Label
        {
            Text = reminder.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.TailTruncation
        });
        if (!string.IsNullOrWhiteSpace(reminder.Notes))
            infoStack.Children.Add(new Label
            {
                Text = reminder.Notes,
                FontSize = 11,
                TextColor = Color.FromArgb("#777"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
        row.Add(infoStack, 0, 0);

        var sendBtn = new Button
        {
            Text = " Tomorrow",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 11,
            HeightRequest = 36,
            Padding = new Thickness(6, 0),
            VerticalOptions = LayoutOptions.Center
        };
        sendBtn.Clicked += async (_, _) =>
        {
            await _taskService.CreateTaskAsync(
                _auth.CurrentUsername,
                reminder.Title,
                category: "Reminders",
                priority: 1,
                dueDate: DateTime.Today.AddDays(1),
                notes: reminder.Notes);

            sendBtn.Text = "✓ Sent!";
            sendBtn.BackgroundColor =
                Color.FromArgb("#9E9E9E");
            sendBtn.IsEnabled = false;

            await Task.Delay(1500);
            sendBtn.Text = " Tomorrow";
            sendBtn.BackgroundColor =
                Color.FromArgb("#2E7D32");
            sendBtn.IsEnabled = true;
        };
        row.Add(sendBtn, 1, 0);

        var deleteBtn = new Button
        {
            Text = "✕",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 6,
            FontSize = 13,
            HeightRequest = 36,
            WidthRequest = 36,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        deleteBtn.Clicked += async (_, _) =>
        {
            bool confirm = await DisplayAlert(
                "Delete Reminder",
                $"Delete \"{reminder.Title}\"?",
                "Delete", "Cancel");
            if (!confirm) return;
            await _reminderService.DeleteAsync(reminder.Id);
            await LoadAsync();
        };
        row.Add(deleteBtn, 2, 0);

        return new Frame
        {
            Content = row,
            Padding = 0,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White,
            Margin = new Thickness(0, 1)
        };
    }

    private async Task AddReminderAsync()
    {
        string? title = await DisplayPromptAsync(
            "New Reminder",
            "Reminder title:",
            "Add", "Cancel",
            placeholder: "e.g. Check emails");
        if (string.IsNullOrWhiteSpace(title)) return;

        string? notes = await DisplayPromptAsync(
            "Notes (optional)",
            "Add notes:",
            "Save", "Skip",
            initialValue: "");

        await _reminderService.CreateAsync(
            _auth.CurrentUsername,
            title.Trim(),
            notes ?? "");

        await LoadAsync();
    }
}

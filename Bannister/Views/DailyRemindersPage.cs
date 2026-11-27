using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DailyRemindersPage : Bannister.Views.ModeThemedPage
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

        stack.Children.Add((new Label
        {
            Text = " Daily Reminders",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = "Tap \"Send to Tomorrow\" to postpone a " +
                   "reminder as a calendar task for tomorrow.",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        var addBtn = (new Button
        {
            Text = "+ Add Reminder",
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(16, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
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
            _contentContainer.Children.Add((new Label
            {
                Text = "No reminders yet. " +
                       "Tap + Add Reminder to create one.",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        foreach (var reminder in reminders)
            _contentContainer.Children.Add(
                BuildReminderCard(reminder));
    }

    private View BuildReminderCard(DailyReminder reminder)
    {
        var row = (new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(120)),
                new ColumnDefinition(new GridLength(36))
            },
            ColumnSpacing = 6,
            Padding = new Thickness(12, 10)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var infoStack = new VerticalStackLayout { Spacing = 2 };
        infoStack.Children.Add((new Label
        {
            Text = reminder.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));
        if (!string.IsNullOrWhiteSpace(reminder.Notes))
            infoStack.Children.Add((new Label
            {
                Text = reminder.Notes,
                FontSize = 11,
                LineBreakMode = LineBreakMode.TailTruncation
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        row.Add(infoStack, 0, 0);

        var sendBtn = (new Button
        {
            Text = " Tomorrow",
            CornerRadius = 6,
            FontSize = 11,
            HeightRequest = 36,
            Padding = new Thickness(6, 0),
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessDarkButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
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
            sendBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground);
            sendBtn.IsEnabled = false;

            await Task.Delay(1500);
            sendBtn.Text = " Tomorrow";
            sendBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessDarkButtonBackground);
            sendBtn.IsEnabled = true;
        };
        row.Add(sendBtn, 1, 0);

        var deleteBtn = (new Button
        {
            Text = "✕",
            CornerRadius = 6,
            FontSize = 13,
            HeightRequest = 36,
            WidthRequest = 36,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
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

        return (new Frame
        {
            Content = row,
            Padding = 0,
            CornerRadius = 8,
            HasShadow = false,
            Margin = new Thickness(0, 1)
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
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

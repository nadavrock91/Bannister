using Bannister.Models;
using Bannister.Services;

namespace BannisterTools;

public class TimedPopupsPage : ContentPage
{
    private readonly TimedPopupService _service;
    private readonly string _username;
    private readonly VerticalStackLayout _popupList = new() { Spacing = 8 };
    private readonly VerticalStackLayout _addForm = new() { Spacing = 8, IsVisible = false };
    private readonly Entry _labelEntry = new() { Placeholder = "Popup text" };
    private readonly Picker _modePicker = new()
    {
        Title = "Time mode",
        ItemsSource = new[] { "Absolute", "Relative to sleep" },
        SelectedIndex = 0
    };
    private readonly TimePicker _absoluteTimePicker = new()
    {
        Format = "HH:mm",
        Time = TimeSpan.FromHours(9)
    };
    private readonly Entry _offsetEntry = new()
    {
        Placeholder = "e.g. -30 or 15",
        Keyboard = Keyboard.Text,
        Text = "0"
    };
    private readonly Label _offsetHelp = new()
    {
        Text = "Use a negative number for minutes before sleep, or positive for after.",
        FontSize = 12,
        TextColor = Color.FromArgb("#666")
    };

    public TimedPopupsPage(TimedPopupService service, string username)
    {
        _service = service;
        _username = username;
        Title = "Timed Popups";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var closeButton = new Button
        {
            Text = "Close",
            HorizontalOptions = LayoutOptions.End,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333")
        };
        closeButton.Clicked += async (_, _) => await Navigation.PopModalAsync();

        var addButton = new Button
        {
            Text = "Add Popup",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White
        };
        addButton.Clicked += (_, _) => _addForm.IsVisible = !_addForm.IsVisible;

        var saveButton = new Button
        {
            Text = "Save Popup",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White
        };
        saveButton.Clicked += async (_, _) => await AddPopupAsync();

        _modePicker.SelectedIndexChanged += (_, _) => UpdateModeVisibility();
        _addForm.Children.Add(_labelEntry);
        _addForm.Children.Add(_modePicker);
        _addForm.Children.Add(_absoluteTimePicker);
        _addForm.Children.Add(_offsetEntry);
        _addForm.Children.Add(_offsetHelp);
        _addForm.Children.Add(saveButton);
        UpdateModeVisibility();

        var content = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 12,
            Children =
            {
                closeButton,
                new Label
                {
                    Text = "Timed Popups",
                    FontSize = 24,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                addButton,
                _addForm,
                _popupList
            }
        };

        Content = new ScrollView { Content = content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void UpdateModeVisibility()
    {
        bool absolute = _modePicker.SelectedIndex != 1;
        _absoluteTimePicker.IsVisible = absolute;
        _offsetEntry.IsVisible = !absolute;
        _offsetHelp.IsVisible = !absolute;
    }

    private async Task AddPopupAsync()
    {
        string label = _labelEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(label))
        {
            await DisplayAlert("Missing text", "Enter the popup text first.", "OK");
            return;
        }

        bool relative = _modePicker.SelectedIndex == 1;
        if (relative && !int.TryParse(_offsetEntry.Text, out _))
        {
            await DisplayAlert("Invalid offset", "Enter minutes as a whole number, such as -30 or 15.", "OK");
            return;
        }

        int.TryParse(_offsetEntry.Text, out int offsetMinutes);
        await _service.AddPopupAsync(new TimedPopup
        {
            Username = _username,
            Label = label,
            TimeMode = relative ? "relative" : "absolute",
            AbsoluteTime = _absoluteTimePicker.Time.ToString(@"hh\:mm"),
            OffsetMinutes = relative ? offsetMinutes : 0
        });

        _labelEntry.Text = "";
        _offsetEntry.Text = "0";
        _addForm.IsVisible = false;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _popupList.Children.Clear();
        var popups = await _service.GetPopupsAsync(_username);
        if (popups.Count == 0)
        {
            _popupList.Children.Add(new Label
            {
                Text = "No timed popups",
                TextColor = Color.FromArgb("#666")
            });
            return;
        }

        foreach (var popup in popups)
        {
            var text = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label
                    {
                        Text = popup.Label,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222")
                    },
                    new Label
                    {
                        Text = FormatTime(popup),
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666")
                    }
                }
            };
            var deleteButton = new Button
            {
                Text = "Delete",
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#C62828"),
                VerticalOptions = LayoutOptions.Center
            };
            deleteButton.Clicked += async (_, _) =>
            {
                await _service.DeletePopupAsync(popup.Id);
                await LoadAsync();
            };

            var row = new Grid
            {
                Padding = 10,
                BackgroundColor = Colors.White,
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            row.Add(text, 0, 0);
            row.Add(deleteButton, 1, 0);
            _popupList.Children.Add(row);
        }
    }

    private static string FormatTime(TimedPopup popup)
    {
        if (!string.Equals(popup.TimeMode, "relative", StringComparison.OrdinalIgnoreCase))
            return $"At {popup.AbsoluteTime}";
        if (popup.OffsetMinutes == 0)
            return "At sleep time";
        return popup.OffsetMinutes < 0
            ? $"{Math.Abs(popup.OffsetMinutes)} minutes before sleep time"
            : $"{popup.OffsetMinutes} minutes after sleep time";
    }
}

using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class CustomGameEditPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CustomGameService _customGames;
    private readonly int? _gameId;
    private readonly Entry _nameEntry = new() { Placeholder = "Game name" };
    private readonly Picker _endTypePicker = new() { Title = "End condition" };
    private readonly Entry _secondsEntry = new() { Placeholder = "Seconds", Keyboard = Keyboard.Numeric };
    private readonly Entry _amountEntry = new() { Placeholder = "Target score", Keyboard = Keyboard.Numeric };
    private readonly DatePicker _datePicker = new();
    private readonly TimePicker _timePicker = new();
    private readonly Switch _higherIsBetterSwitch = new() { IsToggled = true };
    private readonly VerticalStackLayout _buttonRows = new() { Spacing = 8 };
    private readonly List<CustomGameButton> _buttons = new();
    private CustomGame? _game;

    public CustomGameEditPage(AuthService auth, CustomGameService customGames, int? gameId = null)
    {
        _auth = auth;
        _customGames = customGames;
        _gameId = gameId;
        Title = gameId.HasValue ? "Edit Custom Game" : "New Custom Game";
        BackgroundColor = Color.FromArgb("#FAFAFA");

        _endTypePicker.ItemsSource = new List<string> { "Timer (Seconds)", "Date", "Amount" };
        _endTypePicker.SelectedIndex = 0;
        _endTypePicker.SelectedIndexChanged += (_, _) => UpdateEndFields();

        var addButton = new Button
        {
            Text = "+ Add Button",
            BackgroundColor = Color.FromArgb("#EDE7F6"),
            TextColor = Color.FromArgb("#512DA8"),
            CornerRadius = 8
        };
        addButton.Clicked += async (_, _) => await AddButtonAsync();

        var saveButton = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 48,
            Margin = new Thickness(0, 12, 0, 0)
        };
        saveButton.Clicked += async (_, _) => await SaveAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    new Label { Text = Title, FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222222") },
                    _nameEntry,
                    _endTypePicker,
                    _secondsEntry,
                    new HorizontalStackLayout { Spacing = 8, Children = { _datePicker, _timePicker } },
                    _amountEntry,
                    new HorizontalStackLayout
                    {
                        Spacing = 10,
                        Children =
                        {
                            _higherIsBetterSwitch,
                            new Label { Text = "Higher is better", VerticalOptions = LayoutOptions.Center }
                        }
                    },
                    new Label { Text = "Buttons", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222222"), Margin = new Thickness(0, 8, 0, 0) },
                    _buttonRows,
                    addButton,
                    saveButton
                }
            }
        };

        UpdateEndFields();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_gameId.HasValue && _game == null)
        {
            await LoadAsync();
        }
        RefreshButtonRows();
    }

    private async Task LoadAsync()
    {
        _game = await _customGames.GetGameAsync(_gameId!.Value);
        if (_game == null)
        {
            await DisplayAlert("Not Found", "Custom game was not found.", "OK");
            await Navigation.PopAsync();
            return;
        }

        _nameEntry.Text = _game.Name;
        _endTypePicker.SelectedIndex = Math.Clamp(_game.EndType, 0, 2);
        _secondsEntry.Text = _game.EndValueSeconds?.ToString() ?? "";
        _amountEntry.Text = _game.EndValueAmount?.ToString() ?? "";
        if (_game.EndValueDate.HasValue)
        {
            var local = _game.EndValueDate.Value.ToLocalTime();
            _datePicker.Date = local.Date;
            _timePicker.Time = local.TimeOfDay;
        }
        _higherIsBetterSwitch.IsToggled = _game.HigherIsBetter;

        _buttons.Clear();
        _buttons.AddRange(await _customGames.GetButtonsAsync(_game.Id));
        UpdateEndFields();
    }

    private void UpdateEndFields()
    {
        _secondsEntry.IsVisible = _endTypePicker.SelectedIndex == 0;
        _datePicker.IsVisible = _endTypePicker.SelectedIndex == 1;
        _timePicker.IsVisible = _endTypePicker.SelectedIndex == 1;
        _amountEntry.IsVisible = _endTypePicker.SelectedIndex == 2;
    }

    private async Task AddButtonAsync()
    {
        string label = await DisplayPromptAsync("Button Label", "Enter label:", "Next", "Cancel", placeholder: "+1 Made");
        if (string.IsNullOrWhiteSpace(label)) return;

        string valueText = await DisplayPromptAsync("Point Value", "Enter point value:", "Next", "Cancel", keyboard: Keyboard.Numeric, placeholder: "1");
        if (!int.TryParse(valueText, out int pointValue))
        {
            await DisplayAlert("Invalid", "Point value must be a whole number.", "OK");
            return;
        }

        string color = await DisplayPromptAsync("Button Color", "Optional hex color:", "Save", "Skip", initialValue: pointValue >= 0 ? "#2E7D32" : "#C62828");
        _buttons.Add(new CustomGameButton
        {
            Label = label.Trim(),
            PointValue = pointValue,
            Color = string.IsNullOrWhiteSpace(color) ? "" : color.Trim(),
            SortOrder = _buttons.Count
        });
        RefreshButtonRows();
    }

    private void RefreshButtonRows()
    {
        _buttonRows.Children.Clear();
        if (_buttons.Count == 0)
        {
            _buttonRows.Children.Add(new Label
            {
                Text = "No buttons yet. Add at least one point button.",
                FontSize = 13,
                TextColor = Color.FromArgb("#666666")
            });
            return;
        }

        for (int i = 0; i < _buttons.Count; i++)
        {
            int index = i;
            var button = _buttons[index];
            var row = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            row.Add(new Label
            {
                Text = $"{button.Label} ({button.PointValue:+#;-#;0}) {button.Color}",
                VerticalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#222222")
            }, 0, 0);
            row.Add(MiniButton("Edit", async () => await EditButtonAsync(index)), 1, 0);
            row.Add(MiniButton("↑", () => MoveButton(index, -1)), 2, 0);
            row.Add(MiniButton("↓", () => MoveButton(index, 1)), 3, 0);
            row.Add(MiniButton("Delete", () => { _buttons.RemoveAt(index); RefreshButtonRows(); }), 4, 0);
            _buttonRows.Children.Add(row);
        }
    }

    private Button MiniButton(string text, Action action)
    {
        var button = new Button
        {
            Text = text,
            FontSize = 12,
            Padding = new Thickness(8, 0),
            HeightRequest = 34,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333333"),
            CornerRadius = 6
        };
        button.Clicked += (_, _) => action();
        return button;
    }

    private async Task EditButtonAsync(int index)
    {
        var button = _buttons[index];
        string label = await DisplayPromptAsync("Button Label", "Enter label:", "Next", "Cancel", initialValue: button.Label);
        if (string.IsNullOrWhiteSpace(label)) return;
        string valueText = await DisplayPromptAsync("Point Value", "Enter point value:", "Next", "Cancel", initialValue: button.PointValue.ToString(), keyboard: Keyboard.Numeric);
        if (!int.TryParse(valueText, out int pointValue))
        {
            await DisplayAlert("Invalid", "Point value must be a whole number.", "OK");
            return;
        }
        string color = await DisplayPromptAsync("Button Color", "Optional hex color:", "Save", "Skip", initialValue: button.Color);

        button.Label = label.Trim();
        button.PointValue = pointValue;
        button.Color = string.IsNullOrWhiteSpace(color) ? "" : color.Trim();
        RefreshButtonRows();
    }

    private void MoveButton(int index, int delta)
    {
        int next = index + delta;
        if (next < 0 || next >= _buttons.Count) return;
        (_buttons[index], _buttons[next]) = (_buttons[next], _buttons[index]);
        RefreshButtonRows();
    }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(_nameEntry.Text))
        {
            await DisplayAlert("Validation", "Enter a game name.", "OK");
            return;
        }
        if (_buttons.Count == 0)
        {
            await DisplayAlert("Validation", "Add at least one button.", "OK");
            return;
        }

        int endType = _endTypePicker.SelectedIndex;
        int? seconds = null;
        DateTime? endDateUtc = null;
        int? amount = null;
        if (endType == 0 && (!int.TryParse(_secondsEntry.Text, out int secondsValue) || secondsValue <= 0))
        {
            await DisplayAlert("Validation", "Enter timer seconds greater than zero.", "OK");
            return;
        }
        else if (endType == 0)
        {
            seconds = int.Parse(_secondsEntry.Text);
        }
        else if (endType == 1)
        {
            endDateUtc = DateTime.SpecifyKind(_datePicker.Date.Add(_timePicker.Time), DateTimeKind.Local).ToUniversalTime();
        }
        else if (endType == 2 && !int.TryParse(_amountEntry.Text, out int amountValue))
        {
            await DisplayAlert("Validation", "Enter a target score.", "OK");
            return;
        }
        else if (endType == 2)
        {
            amount = int.Parse(_amountEntry.Text);
        }

        if (_game == null)
        {
            _game = await _customGames.AddGameAsync(_auth.CurrentUsername, _nameEntry.Text.Trim(), endType, seconds, endDateUtc, amount, _higherIsBetterSwitch.IsToggled);
        }
        else
        {
            _game.Name = _nameEntry.Text.Trim();
            _game.EndType = endType;
            _game.EndValueSeconds = seconds;
            _game.EndValueDate = endDateUtc;
            _game.EndValueAmount = amount;
            _game.HigherIsBetter = _higherIsBetterSwitch.IsToggled;
            await _customGames.UpdateGameAsync(_game);
        }

        var existing = await _customGames.GetButtonsAsync(_game.Id);
        foreach (var existingButton in existing)
        {
            await _customGames.DeleteButtonAsync(existingButton.Id);
        }
        for (int i = 0; i < _buttons.Count; i++)
        {
            var button = _buttons[i];
            await _customGames.AddButtonAsync(_game.Id, button.Label, button.PointValue, button.Color, i);
        }

        await Navigation.PopAsync();
    }
}

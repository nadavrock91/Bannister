using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class CommandsCasinoPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CommandsCasinoService _casino;

    private readonly List<int> _deadlineOptions = new() { 15, 30, 60, 120, 300 };
    private readonly List<Button> _deadlineButtons = new();

    private VerticalStackLayout _chipsStack = new();
    private VerticalStackLayout _presetsStack = new();
    private VerticalStackLayout _lostChipsStack = new();
    private Label _selectedChipLabel = new();
    private Label _clockLabel = new();
    private Label _startHintLabel = new();
    private Label _sessionCountdownLabel = new();
    private Label _sessionCommandLabel = new();
    private Editor _commandEditor = new();
    private Button _startButton = new();
    private Grid _sessionPanel = new();
    private VerticalStackLayout _setupStack = new();

    private List<CasinoChip> _chips = new();
    private List<CasinoPreset> _presets = new();
    private List<CasinoLostChip> _lostChips = new();
    private CasinoChip? _selectedChip;
    private int _selectedDeadlineSeconds = 30;
    private bool _sessionActive;
    private CancellationTokenSource? _sessionCts;

    public CommandsCasinoPage(AuthService auth, CommandsCasinoService casino)
    {
        _auth = auth;
        _casino = casino;

        Title = "Commands Casino";
        BackgroundColor = Color.FromArgb("#F6F2EA");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CancelSession();
    }

    private void BuildUI()
    {
        var root = new ScrollView();
        var main = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        header.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label
                {
                    Text = "Commands Casino",
                    FontSize = 30,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#3D2B1F")
                },
                new Label
                {
                    Text = "Stake something. Write a command. Beat the clock.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#6D5A4A")
                }
            }
        }, 0, 0);

        var settingsButton = new Button
        {
            Text = "⚙",
            WidthRequest = 44,
            HeightRequest = 44,
            CornerRadius = 22,
            BackgroundColor = Color.FromArgb("#EFE1CF"),
            TextColor = Color.FromArgb("#3D2B1F"),
            FontSize = 18
        };
        settingsButton.Clicked += OnSettingsClicked;
        header.Add(settingsButton, 1, 0);
        main.Children.Add(header);

        _setupStack = new VerticalStackLayout { Spacing = 16 };
        _setupStack.Children.Add(BuildChipsSection());
        _setupStack.Children.Add(BuildClockSection());
        _setupStack.Children.Add(BuildCommandSection());
        _setupStack.Children.Add(BuildStartSection());
        main.Children.Add(_setupStack);

        _sessionPanel = BuildSessionPanel();
        _sessionPanel.IsVisible = false;
        main.Children.Add(_sessionPanel);

        main.Children.Add(BuildLostChipsSection());
        root.Content = main;
        Content = root;
    }

    private View BuildChipsSection()
    {
        _selectedChipLabel = new Label
        {
            Text = "No chip selected",
            FontSize = 15,
            TextColor = Color.FromArgb("#6D5A4A")
        };

        _chipsStack = new VerticalStackLayout { Spacing = 8 };

        var addButton = SmallButton("+ Add Chip", Color.FromArgb("#2E7D32"), Colors.White);
        addButton.Clicked += OnAddChipClicked;

        var manageButton = SmallButton("Manage", Color.FromArgb("#795548"), Colors.White);
        manageButton.Clicked += OnManageChipsClicked;

        return Section("Stake your chip", new View[]
        {
            _selectedChipLabel,
            _chipsStack,
            new HorizontalStackLayout { Spacing = 8, Children = { addButton, manageButton } }
        });
    }

    private View BuildClockSection()
    {
        _clockLabel = new Label
        {
            TextColor = Color.FromArgb("#3D2B1F"),
            FontSize = 15,
            FontAttributes = FontAttributes.Bold
        };

        var row = new HorizontalStackLayout
        {
            Spacing = 8
        };

        foreach (int seconds in _deadlineOptions)
        {
            var button = SmallButton(FormatDuration(seconds), Color.FromArgb("#DDD0C0"), Color.FromArgb("#3D2B1F"));
            int value = seconds;
            button.Clicked += (_, _) =>
            {
                _selectedDeadlineSeconds = value;
                RefreshDeadlineButtons();
                UpdateStartState();
            };
            _deadlineButtons.Add(button);
            row.Children.Add(button);
        }

        return Section("Set the clock", new View[] { _clockLabel, row });
    }

    private View BuildCommandSection()
    {
        _presetsStack = new VerticalStackLayout { Spacing = 8 };

        var addButton = SmallButton("+ Add Preset", Color.FromArgb("#1565C0"), Colors.White);
        addButton.Clicked += OnAddPresetClicked;

        var manageButton = SmallButton("Manage", Color.FromArgb("#795548"), Colors.White);
        manageButton.Clicked += OnManagePresetsClicked;

        _commandEditor = new Editor
        {
            Placeholder = "Type full command, or add specifics to preset...",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 110,
            BackgroundColor = Colors.White,
            FontSize = 15
        };
        _commandEditor.TextChanged += (_, _) => UpdateStartState();

        return Section("Choose or write command", new View[]
        {
            _presetsStack,
            new HorizontalStackLayout { Spacing = 8, Children = { addButton, manageButton } },
            _commandEditor
        });
    }

    private View BuildStartSection()
    {
        _startButton = new Button
        {
            Text = GetStartButtonLabel(),
            BackgroundColor = Color.FromArgb("#C62828"),
            TextColor = Colors.White,
            CornerRadius = 10,
            HeightRequest = 58,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        };
        _startButton.Clicked += OnStartClicked;

        _startHintLabel = new Label
        {
            TextColor = Color.FromArgb("#8D6E63"),
            FontSize = 13
        };

        return new VerticalStackLayout
        {
            Spacing = 6,
            Children = { _startButton, _startHintLabel }
        };
    }

    private Grid BuildSessionPanel()
    {
        _sessionCountdownLabel = new Label
        {
            FontSize = 34,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        _sessionCommandLabel = new Label
        {
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3D2B1F"),
            LineBreakMode = LineBreakMode.WordWrap,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var cancelButton = SmallButton("Cancel", Color.FromArgb("#9E9E9E"), Colors.White);
        cancelButton.Clicked += async (_, _) =>
        {
            CancelSession();
            await DisplayAlert("Session cancelled", "Chip kept. No forfeit recorded.", "OK");
        };

        return new Grid
        {
            Children =
            {
                new Frame
                {
                    BackgroundColor = Color.FromArgb("#FFF8E1"),
                    BorderColor = Color.FromArgb("#D7CCC8"),
                    CornerRadius = 12,
                    Padding = 18,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 14,
                        Children =
                        {
                            new Label
                            {
                                Text = "In session",
                                FontSize = 18,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Color.FromArgb("#6D4C41")
                            },
                            _sessionCommandLabel,
                            _sessionCountdownLabel,
                            cancelButton
                        }
                    }
                }
            }
        };
    }

    private View BuildLostChipsSection()
    {
        _lostChipsStack = new VerticalStackLayout { Spacing = 8 };

        var clearButton = SmallButton("Clear All", Color.FromArgb("#C62828"), Colors.White);
        clearButton.Clicked += OnClearLostChipsClicked;

        return Section("☠ LOST CHIPS ☠", new View[]
        {
            _lostChipsStack,
            clearButton
        });
    }

    private Frame Section(string title, IEnumerable<View> children)
    {
        var stack = new VerticalStackLayout { Spacing = 10 };
        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#3D2B1F")
        });

        foreach (var child in children)
            stack.Children.Add(child);

        return new Frame
        {
            BackgroundColor = Color.FromArgb("#FFFDF8"),
            BorderColor = Color.FromArgb("#E0D2C0"),
            CornerRadius = 10,
            Padding = 14,
            HasShadow = false,
            Content = stack
        };
    }

    private Button SmallButton(string text, Color background, Color textColor)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = background,
            TextColor = textColor,
            CornerRadius = 8,
            Padding = new Thickness(12, 0),
            HeightRequest = 38,
            FontSize = 13
        };
    }

    private async Task LoadAsync()
    {
        if (_sessionActive) return;

        string username = _auth.CurrentUsername;
        _chips = await _casino.GetChipsAsync(username);
        _presets = await _casino.GetPresetsAsync(username);
        _lostChips = await _casino.GetLostChipsAsync(username);

        if (_selectedChip != null)
            _selectedChip = _chips.FirstOrDefault(c => c.Id == _selectedChip.Id);

        RefreshChips();
        RefreshPresets();
        RefreshLostChips();
        RefreshDeadlineButtons();
        UpdateStartState();
    }

    private void RefreshChips()
    {
        _chipsStack.Children.Clear();
        _selectedChipLabel.Text = _selectedChip == null ? "No chip selected" : $"Selected chip: {_selectedChip.Name}";

        if (_chips.Count == 0)
        {
            _chipsStack.Children.Add(new Label
            {
                Text = "No chips yet. Add one to stake.",
                TextColor = Color.FromArgb("#8D6E63")
            });
            return;
        }

        var row = new HorizontalStackLayout
        {
            Spacing = 8
        };

        foreach (var chip in _chips)
        {
            bool selected = _selectedChip?.Id == chip.Id;
            var button = SmallButton(chip.Name, selected ? Color.FromArgb("#F9A825") : Color.FromArgb("#EFE1CF"), Color.FromArgb("#3D2B1F"));
            button.Clicked += (_, _) =>
            {
                _selectedChip = chip;
                RefreshChips();
                UpdateStartState();
            };
            row.Children.Add(button);
        }

        _chipsStack.Children.Add(row);
    }

    private void RefreshPresets()
    {
        _presetsStack.Children.Clear();
        if (_presets.Count == 0)
        {
            _presetsStack.Children.Add(new Label
            {
                Text = "No presets saved yet.",
                TextColor = Color.FromArgb("#8D6E63")
            });
            return;
        }

        foreach (var preset in _presets)
        {
            var button = SmallButton(Truncate(preset.Text, 80), Color.FromArgb("#E3F2FD"), Color.FromArgb("#0D47A1"));
            button.HorizontalOptions = LayoutOptions.Fill;
            button.Clicked += (_, _) =>
            {
                _commandEditor.Text = preset.Text;
                UpdateStartState();
            };
            _presetsStack.Children.Add(button);
        }
    }

    private void RefreshLostChips()
    {
        _lostChipsStack.Children.Clear();
        if (_lostChips.Count == 0)
        {
            _lostChipsStack.Children.Add(new Label
            {
                Text = "No lost chips yet. Keep it that way.",
                TextColor = Color.FromArgb("#2E7D32")
            });
            return;
        }

        foreach (var lost in _lostChips)
        {
            _lostChipsStack.Children.Add(new Label
            {
                Text = $"{lost.ChipName} - {Truncate(lost.CommandText, 90)}\nLost {lost.LostAt.ToLocalTime():g} after {FormatDuration(lost.DeadlineSeconds)}",
                FontSize = 13,
                TextColor = Color.FromArgb("#4E342E"),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }
    }

    private void RefreshDeadlineButtons()
    {
        _clockLabel.Text = $"HALF: {FormatDuration(_selectedDeadlineSeconds / 2)}    DEADLINE: {FormatDuration(_selectedDeadlineSeconds)}";
        for (int i = 0; i < _deadlineButtons.Count; i++)
        {
            bool selected = _deadlineOptions[i] == _selectedDeadlineSeconds;
            _deadlineButtons[i].BackgroundColor = selected ? Color.FromArgb("#F9A825") : Color.FromArgb("#DDD0C0");
        }
    }

    private void UpdateStartState()
    {
        _startButton.Text = GetStartButtonLabel();
        if (_sessionActive)
        {
            _startButton.IsEnabled = false;
            _startHintLabel.Text = "Session in progress.";
            return;
        }

        if (_selectedChip == null)
        {
            _startButton.IsEnabled = false;
            _startHintLabel.Text = "Select a chip before starting.";
            return;
        }

        if (string.IsNullOrWhiteSpace(_commandEditor.Text))
        {
            _startButton.IsEnabled = false;
            _startHintLabel.Text = "Write a command before starting.";
            return;
        }

        _startButton.IsEnabled = true;
        _startHintLabel.Text = "";
    }

    private async void OnAddChipClicked(object? sender, EventArgs e)
    {
        string? name = await DisplayPromptAsync("Add Chip", "Name the chip you are staking:");
        if (string.IsNullOrWhiteSpace(name)) return;

        await _casino.AddChipAsync(_auth.CurrentUsername, name);
        await LoadAsync();
    }

    private async void OnAddPresetClicked(object? sender, EventArgs e)
    {
        string? text = await DisplayPromptAsync("Add Preset", "Enter a reusable command preset:");
        if (string.IsNullOrWhiteSpace(text)) return;

        await _casino.AddPresetAsync(_auth.CurrentUsername, text);
        await LoadAsync();
    }

    private async void OnManageChipsClicked(object? sender, EventArgs e)
    {
        await ShowManagementOverlayAsync("Manage Chips", _chips.Select(c => (c.Id, c.Name)).ToList(), async id =>
        {
            await _casino.DeleteChipAsync(id);
            if (_selectedChip?.Id == id) _selectedChip = null;
            await LoadAsync();
        });
    }

    private async void OnManagePresetsClicked(object? sender, EventArgs e)
    {
        await ShowManagementOverlayAsync("Manage Presets", _presets.Select(p => (p.Id, p.Text)).ToList(), async id =>
        {
            await _casino.DeletePresetAsync(id);
            await LoadAsync();
        });
    }

    private async void OnClearLostChipsClicked(object? sender, EventArgs e)
    {
        if (_lostChips.Count == 0) return;
        bool confirm = await DisplayAlert("Clear Lost Chips", "Clear all lost chip records?", "Clear", "Cancel");
        if (!confirm) return;

        await _casino.ClearLostChipsAsync(_auth.CurrentUsername);
        await LoadAsync();
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        string? label = await DisplayPromptAsync(
            "Start Button Label",
            "Set the default start button label:",
            initialValue: GetStartButtonLabel());

        if (string.IsNullOrWhiteSpace(label)) return;
        Preferences.Set(GetStartLabelKey(), label.Trim());
        UpdateStartState();
    }

    private async void OnStartClicked(object? sender, EventArgs e)
    {
        if (_selectedChip == null || string.IsNullOrWhiteSpace(_commandEditor.Text) || _sessionActive)
            return;

        var chip = _selectedChip;
        string command = _commandEditor.Text.Trim();
        int deadline = _selectedDeadlineSeconds;

        _sessionActive = true;
        _sessionCts = new CancellationTokenSource();
        _setupStack.IsVisible = false;
        _sessionPanel.IsVisible = true;
        _sessionCommandLabel.Text = command;
        UpdateStartState();

        try
        {
            await RunSessionAsync(chip, command, deadline, _sessionCts.Token);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _sessionCts?.Dispose();
            _sessionCts = null;
            _sessionActive = false;
            _sessionPanel.IsVisible = false;
            _setupStack.IsVisible = true;
            await LoadAsync();
        }
    }

    private async Task RunSessionAsync(CasinoChip chip, string command, int deadlineSeconds, CancellationToken token)
    {
        var startedAt = DateTime.UtcNow;
        int halfAt = deadlineSeconds / 2;
        bool halfShown = false;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            int elapsed = (int)Math.Floor((DateTime.UtcNow - startedAt).TotalSeconds);
            int remaining = Math.Max(0, deadlineSeconds - elapsed);
            _sessionCountdownLabel.Text = $"{remaining}s remaining";

            if (!halfShown && elapsed >= halfAt)
            {
                halfShown = true;
                MainThread.BeginInvokeOnMainThread(async () =>
                    await DisplayAlert("Half time", "Halfway there - keep going!", "OK"));
            }

            if (elapsed >= deadlineSeconds)
                break;

            await Task.Delay(250, token);
        }

        bool? completed = await DeadlinePromptPage.ShowAsync(Navigation, command);
        if (completed == true)
        {
            await DisplayAlert("Chip kept", $"{chip.Name} stays in your stack.", "OK");
            return;
        }

        if (completed == false)
        {
            await _casino.RecordLostChipAsync(_auth.CurrentUsername, chip.Name, command, deadlineSeconds);
            await _casino.RemoveChipAsync(chip.Id);
            if (_selectedChip?.Id == chip.Id) _selectedChip = null;
            await DisplayAlert("Chip lost", $"{chip.Name} moved to Lost Chips.", "OK");
        }
    }

    private void CancelSession()
    {
        if (!_sessionActive) return;
        _sessionCts?.Cancel();
    }

    private async Task ShowManagementOverlayAsync(string title, List<(int id, string text)> rows, Func<int, Task> deleteAsync)
    {
        var tcs = new TaskCompletionSource<bool>();
        bool closing = false;

        var list = new VerticalStackLayout { Spacing = 8 };
        if (rows.Count == 0)
        {
            list.Children.Add(new Label
            {
                Text = "Nothing to manage yet.",
                TextColor = Color.FromArgb("#8D6E63")
            });
        }
        else
        {
            foreach (var rowData in rows)
            {
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Auto }
                    },
                    ColumnSpacing = 8,
                    Padding = new Thickness(0, 4)
                };

                row.Add(new Label
                {
                    Text = rowData.text,
                    LineBreakMode = LineBreakMode.WordWrap,
                    TextColor = Color.FromArgb("#3D2B1F"),
                    VerticalOptions = LayoutOptions.Center
                }, 0, 0);

                var deleteButton = SmallButton("Delete", Color.FromArgb("#C62828"), Colors.White);
                deleteButton.Clicked += async (_, _) => await deleteAsync(rowData.id);
                row.Add(deleteButton, 1, 0);
                list.Children.Add(row);
            }
        }

        var closeButton = SmallButton("Done", Color.FromArgb("#5D4037"), Colors.White);
        ContentPage? page = null;

        async Task CloseAsync()
        {
            if (closing) return;
            closing = true;
            if (page != null)
                await Navigation.PopModalAsync(false);
            tcs.TrySetResult(true);
        }

        closeButton.Clicked += async (_, _) => await CloseAsync();

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
                        WidthRequest = 560,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Label
                                {
                                    Text = title,
                                    FontSize = 20,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#333")
                                },
                                new ScrollView
                                {
                                    HeightRequest = 360,
                                    Content = list
                                },
                                closeButton
                            }
                        }
                    }
                }
            }
        };

        await Navigation.PushModalAsync(page, false);
        await tcs.Task;
    }

    private string GetStartButtonLabel()
    {
        return Preferences.Get(GetStartLabelKey(), "go");
    }

    private string GetStartLabelKey() => $"commands_casino_start_label_{_auth.CurrentUsername}";

    private static string FormatDuration(int seconds)
    {
        if (seconds < 60) return $"{seconds}s";
        int minutes = seconds / 60;
        int remainder = seconds % 60;
        return remainder == 0 ? $"{minutes}m" : $"{minutes}m {remainder}s";
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        return text.Length <= max ? text : text[..Math.Max(0, max - 3)] + "...";
    }

    private sealed class DeadlinePromptPage : ContentPage
    {
        private readonly TaskCompletionSource<bool?> _completion = new();
        private bool _closing;

        private DeadlinePromptPage(string command)
        {
            Title = "Time's up";
            BackgroundColor = Color.FromArgb("#80000000");

            var yesButton = new Button
            {
                Text = "Yes",
                BackgroundColor = Color.FromArgb("#2E7D32"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44
            };
            yesButton.Clicked += (_, _) => _ = CloseAsync(true);

            var noButton = new Button
            {
                Text = "No",
                BackgroundColor = Color.FromArgb("#C62828"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44
            };
            noButton.Clicked += (_, _) => _ = CloseAsync(false);

            var buttons = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 10
            };
            buttons.Add(noButton, 0, 0);
            buttons.Add(yesButton, 1, 0);

            Content = new Grid
            {
                Padding = 24,
                Children =
                {
                    new Frame
                    {
                        BackgroundColor = Colors.White,
                        CornerRadius = 12,
                        Padding = 20,
                        HasShadow = true,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        WidthRequest = 520,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 14,
                            Children =
                            {
                                new Label
                                {
                                    Text = "Time's up",
                                    FontSize = 24,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#333")
                                },
                                new Label
                                {
                                    Text = "Did you do it?",
                                    FontSize = 18,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#5D4037")
                                },
                                new Label
                                {
                                    Text = command,
                                    FontSize = 15,
                                    TextColor = Color.FromArgb("#333"),
                                    LineBreakMode = LineBreakMode.WordWrap
                                },
                                buttons
                            }
                        }
                    }
                }
            };
        }

        public static async Task<bool?> ShowAsync(INavigation navigation, string command)
        {
            var page = new DeadlinePromptPage(command);
            await navigation.PushModalAsync(page, false);
            return await page._completion.Task;
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(bool? result)
        {
            if (_closing) return;
            _closing = true;
            await Navigation.PopModalAsync(false);
            _completion.TrySetResult(result);
        }
    }
}

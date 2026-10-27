using Bannister.Models;
using Bannister.Services;
using CommunityToolkit.Maui.Storage;

namespace Bannister.Views;

public class RclonePresetsPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly AuthService _auth;
    private List<RclonePreset> _presets = new();
    private List<BackupLocation> _backupLocations = new();
    private VerticalStackLayout _mainStack = null!;

    public RclonePresetsPage(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
        Title = "Rclone Presets";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildShell();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
        Render();
    }

    private void BuildShell()
    {
        _mainStack = new VerticalStackLayout { Padding = new Thickness(16), Spacing = 12 };
        Content = new ScrollView { Content = _mainStack };
    }

    private async Task<string?> PickFolderAsync()
    {
        try
        {
            var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
            if (result.IsSuccessful)
                return result.Folder.Path;
        }
        catch { }
        return null;
    }

    private async Task LoadAsync()
    {
        var conn = await _db.GetConnectionAsync();
        _presets = await conn.Table<RclonePreset>()
            .Where(p => p.Username == _auth.CurrentUsername)
            .OrderBy(p => p.Title)
            .ToListAsync();
        var allLocations = await conn.Table<BackupLocation>()
            .Where(l => l.Username == _auth.CurrentUsername)
            .OrderBy(l => l.Name)
            .ToListAsync();
        _backupLocations = allLocations;
    }

    private async Task<string?> PickDestinationAsync(string currentValue = "")
    {
        var locationsWithRemote = _backupLocations
            .Where(l => !string.IsNullOrWhiteSpace(l.RcloneRemote))
            .ToList();

        var options = locationsWithRemote.Select(l => $"{l.Name} ({l.RcloneRemote}:)").ToList();
        options.Add("✏️ Custom");

        string? choice = await DisplayActionSheet("Destination", "Cancel", null, options.ToArray());
        if (choice == null || choice == "Cancel") return null;

        if (choice == "✏️ Custom")
        {
            return await DisplayPromptAsync("Destination", "Enter destination (remote:bucket/path):", initialValue: currentValue);
        }

        var selectedLoc = locationsWithRemote[options.IndexOf(choice)];
        string? path = await DisplayPromptAsync(
            $"{selectedLoc.Name} path",
            $"Bucket/path after '{selectedLoc.RcloneRemote}:' (e.g. my-bucket/folder):",
            initialValue: "");
        if (path == null) return null;
        return $"{selectedLoc.RcloneRemote}:{path.Trim()}";
    }

    private void Render()
    {
        _mainStack.Children.Clear();

        _mainStack.Children.Add(new Label
        {
            Text = "Rclone Presets",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238")
        });
        _mainStack.Children.Add(new Label
        {
            Text = "Save rclone copy commands with fixed or input fields. Run a preset to build your command.",
            FontSize = 13,
            TextColor = Color.FromArgb("#546E7A")
        });

        var addBtn = new Button
        {
            Text = "+ Add Preset",
            BackgroundColor = Color.FromArgb("#6A1B9A"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13,
            Padding = new Thickness(12, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        addBtn.Clicked += async (s, e) => await OnAddPresetAsync();
        _mainStack.Children.Add(addBtn);

        if (_presets.Count == 0)
        {
            _mainStack.Children.Add(new Label
            {
                Text = "No presets yet. Add one to get started.",
                FontSize = 13,
                TextColor = Color.FromArgb("#90A4AE"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 24)
            });
            return;
        }

        foreach (var preset in _presets)
            _mainStack.Children.Add(BuildPresetCard(preset));
    }

    private Frame BuildPresetCard(RclonePreset preset)
    {
        var frame = new Frame
        {
            Padding = new Thickness(12, 10),
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false
        };

        var stack = new VerticalStackLayout { Spacing = 6 };

        // Title row
        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 6
        };

        var titleLabel = new Label
        {
            Text = preset.Title,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(titleLabel, 0);
        titleRow.Children.Add(titleLabel);

        var runBtn = new Button
        {
            Text = "▶️",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            FontSize = 12,
            CornerRadius = 6,
            WidthRequest = 34,
            HeightRequest = 30,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        runBtn.Clicked += async (s, e) => await OnRunPresetAsync(preset);
        Grid.SetColumn(runBtn, 1);
        titleRow.Children.Add(runBtn);

        var editBtn = new Button
        {
            Text = "✏️",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            FontSize = 12,
            CornerRadius = 6,
            WidthRequest = 34,
            HeightRequest = 30,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        editBtn.Clicked += async (s, e) => await OnEditPresetAsync(preset);
        Grid.SetColumn(editBtn, 2);
        titleRow.Children.Add(editBtn);

        var deleteBtn = new Button
        {
            Text = "️",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 12,
            CornerRadius = 6,
            WidthRequest = 34,
            HeightRequest = 30,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        deleteBtn.Clicked += async (s, e) => await OnDeletePresetAsync(preset);
        Grid.SetColumn(deleteBtn, 3);
        titleRow.Children.Add(deleteBtn);

        stack.Children.Add(titleRow);

        // Summary of fields
        var sourceDesc = preset.SourceIsInput ? "[ask at run time]" : preset.Source;
        var destDesc = preset.DestinationIsInput ? "[ask at run time]" : preset.Destination;
        stack.Children.Add(new Label
        {
            Text = $"Source: {sourceDesc}",
            FontSize = 12,
            TextColor = Color.FromArgb("#546E7A")
        });
        stack.Children.Add(new Label
        {
            Text = $"Destination: {destDesc}",
            FontSize = 12,
            TextColor = Color.FromArgb("#546E7A")
        });
        stack.Children.Add(new Label
        {
            Text = $"--transfers={preset.Transfers}  --checkers={preset.Checkers}",
            FontSize = 12,
            TextColor = Color.FromArgb("#90A4AE")
        });

        frame.Content = stack;
        return frame;
    }

    private async Task OnAddPresetAsync()
    {
        string? title = await DisplayPromptAsync("New Preset", "Preset title:");
        if (string.IsNullOrWhiteSpace(title)) return;

        // Source
        string sourceInputChoice = await DisplayActionSheet("Source path", "Cancel", null, "Fixed value", "Ask at run time") ?? "Ask at run time";
        bool sourceIsInput = sourceInputChoice != "Fixed value";
        string source = "";
        if (!sourceIsInput)
        {
            string? picked = await PickFolderAsync();
            source = picked ?? await DisplayPromptAsync("Source", "Fixed source path:", initialValue: @"C:\Users\") ?? "";
        }

        // Destination
        string destInputChoice = await DisplayActionSheet("Destination", "Cancel", null, "Fixed value", "Ask at run time") ?? "Fixed value";
        bool destIsInput = destInputChoice != "Fixed value";
        string destination = "";
        if (!destIsInput)
        {
            destination = await PickDestinationAsync() ?? "";
        }

        string? transfersStr = await DisplayPromptAsync("Transfers", "--transfers value:", initialValue: "8", keyboard: Keyboard.Numeric);
        string? checkersStr = await DisplayPromptAsync("Checkers", "--checkers value:", initialValue: "16", keyboard: Keyboard.Numeric);

        int.TryParse(transfersStr, out int transfers); if (transfers <= 0) transfers = 8;
        int.TryParse(checkersStr, out int checkers); if (checkers <= 0) checkers = 16;

        var preset = new RclonePreset
        {
            Username = _auth.CurrentUsername,
            Title = title.Trim(),
            Source = source.Trim(),
            Destination = destination.Trim(),
            Transfers = transfers,
            Checkers = checkers,
            SourceIsInput = sourceIsInput,
            DestinationIsInput = destIsInput
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(preset);
        await LoadAsync();
        Render();
    }

    private async Task OnRunPresetAsync(RclonePreset preset)
    {
        string source = preset.Source;
        if (preset.SourceIsInput)
        {
            string? picked = await PickFolderAsync();
            source = picked ?? await DisplayPromptAsync("Run Preset", "Source path:", initialValue: @"C:\Users\") ?? "";
            if (string.IsNullOrWhiteSpace(source)) return;
        }

        string destination = preset.Destination;
        if (preset.DestinationIsInput)
        {
            destination = await PickDestinationAsync() ?? "";
            if (string.IsNullOrWhiteSpace(destination)) return;
        }

        string command = $"rclone copy \"{source.Trim()}\" {destination.Trim()} --transfers={preset.Transfers} --checkers={preset.Checkers} --progress";
        await DisplayAlert("Rclone Command", command, "OK");
    }

    private async Task OnEditPresetAsync(RclonePreset preset)
    {
        string? title = await DisplayPromptAsync("Edit Preset", "Title:", initialValue: preset.Title);
        if (title == null) return;

        string sourceInputChoice = await DisplayActionSheet("Source path", "Cancel", null, "Fixed value", "Ask at run time") ?? (preset.SourceIsInput ? "Ask at run time" : "Fixed value");
        bool sourceIsInput = sourceInputChoice != "Fixed value";
        string source = preset.Source;
        if (!sourceIsInput)
        {
            string? picked = await PickFolderAsync();
            source = picked ?? await DisplayPromptAsync("Source", "Fixed source path:", initialValue: preset.Source) ?? preset.Source;
        }

        string destInputChoice = await DisplayActionSheet("Destination", "Cancel", null, "Fixed value", "Ask at run time") ?? (preset.DestinationIsInput ? "Ask at run time" : "Fixed value");
        bool destIsInput = destInputChoice != "Fixed value";
        string destination = preset.Destination;
        if (!destIsInput)
        {
            destination = await PickDestinationAsync(preset.Destination) ?? preset.Destination;
        }

        string? transfersStr = await DisplayPromptAsync("Transfers", "--transfers:", initialValue: preset.Transfers.ToString(), keyboard: Keyboard.Numeric);
        string? checkersStr = await DisplayPromptAsync("Checkers", "--checkers:", initialValue: preset.Checkers.ToString(), keyboard: Keyboard.Numeric);

        int.TryParse(transfersStr, out int transfers); if (transfers <= 0) transfers = preset.Transfers;
        int.TryParse(checkersStr, out int checkers); if (checkers <= 0) checkers = preset.Checkers;

        preset.Title = title.Trim();
        preset.Source = source.Trim();
        preset.Destination = destination.Trim();
        preset.Transfers = transfers;
        preset.Checkers = checkers;
        preset.SourceIsInput = sourceIsInput;
        preset.DestinationIsInput = destIsInput;

        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(preset);
        await LoadAsync();
        Render();
    }

    private async Task OnDeletePresetAsync(RclonePreset preset)
    {
        bool confirm = await DisplayAlert("Delete", $"Remove preset '{preset.Title}'?", "Delete", "Cancel");
        if (!confirm) return;
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(preset);
        await LoadAsync();
        Render();
    }
}

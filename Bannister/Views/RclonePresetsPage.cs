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

    private async Task<string?> GetOrPromptRclonePathAsync()
    {
        var conn = await _db.GetConnectionAsync();
        var setting = await conn.Table<UserSetting>()
            .Where(s => s.Username == _auth.CurrentUsername && s.Key == "rclone_path")
            .FirstOrDefaultAsync();
        if (setting != null && !string.IsNullOrWhiteSpace(setting.Value))
            return setting.Value;

        string? path = await DisplayPromptAsync(
            "Rclone Path",
            "Enter path to rclone executable folder (e.g. C:\\Program Files (x86)\\rclone\\rclone-v1.73.4-windows-amd64):\n\nThis will be saved and reused for all presets.");
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = path.Trim();

        if (setting == null)
            await conn.InsertAsync(new UserSetting { Username = _auth.CurrentUsername, Key = "rclone_path", Value = path });
        else
        {
            setting.Value = path;
            await conn.UpdateAsync(setting);
        }
        return path;
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

        string? presetType = await DisplayActionSheet("Preset type", "Cancel", null, "Generic", "Remote Wizard");
        if (presetType == null || presetType == "Cancel") return;

        if (presetType == "Remote Wizard")
        {
            await OnAddRemoteWizardPresetAsync(title.Trim());
            return;
        }

        // Generic flow
        string sourceInputChoice = await DisplayActionSheet("Source path", "Cancel", null, "Fixed value", "Ask at run time") ?? "Ask at run time";
        bool sourceIsInput = sourceInputChoice != "Fixed value";
        string source = "";
        if (!sourceIsInput)
        {
            string? picked = await PickFolderAsync();
            source = picked ?? await DisplayPromptAsync("Source", "Fixed source path:", initialValue: @"C:\Users\") ?? "";
        }

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

    private async Task OnAddRemoteWizardPresetAsync(string title)
    {
        // Pick Backblaze location (must have RcloneRemote set)
        var bbLocations = _backupLocations
            .Where(l => !string.IsNullOrWhiteSpace(l.RcloneRemote))
            .ToList();

        if (bbLocations.Count == 0)
        {
            await DisplayAlert("No Remotes", "No backup locations with an rclone remote are configured. Add one via Manage Locations (set the Rclone remote name, e.g. gdrive, b3ai, onedrive).", "OK");
            return;
        }

        string? remoteChoice = await DisplayActionSheet("Remote", "Cancel", null,
            bbLocations.Select(l => $"{l.Name} ({l.RcloneRemote}:)").ToArray());
        if (remoteChoice == null || remoteChoice == "Cancel") return;

        var selectedLoc = bbLocations[bbLocations.Select(l => $"{l.Name} ({l.RcloneRemote}:)").ToList().IndexOf(remoteChoice)];

        // Direction: upload or download
        string? direction = await DisplayActionSheet("Direction", "Cancel", null, "⬆️ Upload to Remote", "⬇️ Download from Remote");
        if (direction == null || direction == "Cancel") return;
        bool isUpload = direction == "⬆️ Upload to Remote";

        // Bucket/path on Backblaze — fixed or input
        string? bbPathChoice = await DisplayActionSheet(
            $"Remote path ({selectedLoc.RcloneRemote}:...)",
            "Cancel", null, "Fixed value", "Ask at run time");
        if (bbPathChoice == null || bbPathChoice == "Cancel") return;
        bool bbPathIsInput = bbPathChoice == "Ask at run time";
        string bbPath = "";
        if (!bbPathIsInput)
        {
            string? entered = await DisplayPromptAsync(
                "Remote path",
                $"Folder path after '{selectedLoc.RcloneRemote}:' (e.g. MyDrive/folder or bucket/subfolder):",
                initialValue: "");
            if (entered == null) return;
            bbPath = $"{selectedLoc.RcloneRemote}:{entered.Trim()}";
        }

        // Local folder — fixed or input
        string? localChoice = await DisplayActionSheet("Local folder", "Cancel", null, "Fixed value", "Ask at run time");
        if (localChoice == null || localChoice == "Cancel") return;
        bool localIsInput = localChoice == "Ask at run time";
        string localPath = "";
        if (!localIsInput)
        {
            string? picked = await PickFolderAsync();
            localPath = picked ?? await DisplayPromptAsync("Local folder", "Fixed local folder path:", initialValue: @"C:\Users\") ?? "";
        }

        string? transfersStr = await DisplayPromptAsync("Transfers", "--transfers value:", initialValue: "8", keyboard: Keyboard.Numeric);
        string? checkersStr = await DisplayPromptAsync("Checkers", "--checkers value:", initialValue: "16", keyboard: Keyboard.Numeric);

        int.TryParse(transfersStr, out int transfers); if (transfers <= 0) transfers = 8;
        int.TryParse(checkersStr, out int checkers); if (checkers <= 0) checkers = 16;

        // Upload: source=local, dest=BB. Download: source=BB, dest=local
        var preset = new RclonePreset
        {
            Username = _auth.CurrentUsername,
            Title = title,
            Source = isUpload ? localPath : bbPath,
            Destination = isUpload ? bbPath : localPath,
            Transfers = transfers,
            Checkers = checkers,
            SourceIsInput = isUpload ? localIsInput : bbPathIsInput,
            DestinationIsInput = isUpload ? bbPathIsInput : localIsInput
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

        // Show inline dialog overlay
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#99000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var commandLabel = new Label
        {
            Text = command,
            FontSize = 12,
            TextColor = Color.FromArgb("#263238"),
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            Padding = new Thickness(10),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var copyBtn = new Button
        {
            Text = " Copy",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        };

        var launchBtn = new Button
        {
            Text = "⌨️ Launch CMD",
            BackgroundColor = Color.FromArgb("#1B5E20"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        };

        var closeBtn = new Button
        {
            Text = "Close",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        };

        var btnRow = new VerticalStackLayout { Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        btnRow.Children.Add(copyBtn);
        btnRow.Children.Add(launchBtn);
        btnRow.Children.Add(closeBtn);

        var card = new Frame
        {
            Padding = new Thickness(20),
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            HasShadow = true,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(24)
        };
        card.Content = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Rclone Command", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#263238") },
                commandLabel,
                btnRow
            }
        };

        overlay.Children.Add(card);

        var originalContent = Content;
        Content = null;
        var rootGrid = new Grid();
        rootGrid.Children.Add(originalContent);
        rootGrid.Children.Add(overlay);
        Content = rootGrid;

        copyBtn.Clicked += async (s, e) =>
        {
            await Clipboard.Default.SetTextAsync(command);
            copyBtn.Text = "✅ Copied!";
            await Task.Delay(1500);
            copyBtn.Text = " Copy";
        };

        launchBtn.Clicked += async (s, e) =>
        {
            try
            {
                string? rcloneFolderRaw = await GetOrPromptRclonePathAsync();
                if (string.IsNullOrWhiteSpace(rcloneFolderRaw)) return;
                string rcloneFolder = rcloneFolderRaw.TrimEnd('\\');
                // Replace "rclone" at the start of command with the full exe path
                string fullCommand = command.StartsWith("rclone ")
                    ? $"\"{rcloneFolder}\\rclone.exe\" " + command.Substring("rclone ".Length)
                    : command;
                string cmdArgs = $"/K \"{fullCommand}\"";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdArgs,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                    await DisplayAlert("Error", $"Could not launch cmd: {ex.Message}", "OK"));
            }
        };

        closeBtn.Clicked += (s, e) =>
        {
            rootGrid.Children.Remove(originalContent);
            Content = originalContent;
        };
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

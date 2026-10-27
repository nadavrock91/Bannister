using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class BackupLocationsPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly AuthService _auth;

    private List<BackupLocation> _locations = new();
    private List<BackupFile> _files = new();
    private List<BackupFileItem> _items = new();
    private VerticalStackLayout _mainStack = null!;

    public BackupLocationsPage(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
        Title = "Backup Locations";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildShell();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
        RenderPage();
    }

    private void BuildShell()
    {
        _mainStack = new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 16
        };
        Content = new ScrollView { Content = _mainStack };
    }

    private async Task LoadDataAsync()
    {
        var username = _auth.CurrentUsername;
        var conn = await _db.GetConnectionAsync();
        _locations = await conn.Table<BackupLocation>()
            .Where(l => l.Username == username)
            .OrderBy(l => l.Name)
            .ToListAsync();
        _files = await conn.Table<BackupFile>()
            .Where(f => f.Username == username)
            .OrderBy(f => f.Category)
            .ThenBy(f => f.Name)
            .ToListAsync();
        _items = await conn.Table<BackupFileItem>()
            .Where(i => i.Username == username)
            .OrderBy(i => i.Name)
            .ToListAsync();
    }

    private void RenderPage()
    {
        _mainStack.Children.Clear();

        // Header
        _mainStack.Children.Add(new Label
        {
            Text = "File & Backup Locations",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238")
        });
        _mainStack.Children.Add(new Label
        {
            Text = "Track which files are stored in which locations. Files are organised by category.",
            FontSize = 13,
            TextColor = Color.FromArgb("#546E7A")
        });

        // Action buttons row
        var actionRow = new HorizontalStackLayout { Spacing = 10 };

        var manageLocationsBtn = new Button
        {
            Text = "⚙️ Manage Locations",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13,
            Padding = new Thickness(12, 0)
        };
        manageLocationsBtn.Clicked += async (s, e) => await OnManageLocationsAsync();
        actionRow.Children.Add(manageLocationsBtn);

        var addFileBtn = new Button
        {
            Text = "+ Add File",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13,
            Padding = new Thickness(12, 0)
        };
        addFileBtn.Clicked += async (s, e) => await OnAddFileAsync();
        actionRow.Children.Add(addFileBtn);

        var rcloneBtn = new Button
        {
            Text = " Rclone",
            BackgroundColor = Color.FromArgb("#F3E5F5"),
            TextColor = Color.FromArgb("#6A1B9A"),
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13,
            Padding = new Thickness(12, 0)
        };
        rcloneBtn.Clicked += async (s, e) => await OnRcloneBuilderAsync();
        actionRow.Children.Add(rcloneBtn);

        _mainStack.Children.Add(actionRow);

        if (_locations.Count == 0)
        {
            _mainStack.Children.Add(new Label
            {
                Text = "⚙️ Start by adding your storage locations (e.g. Backblaze, PC, OneDrive) using 'Manage Locations'.",
                FontSize = 13,
                TextColor = Color.FromArgb("#FF9800"),
                Margin = new Thickness(0, 8)
            });
        }

        if (_files.Count == 0)
        {
            _mainStack.Children.Add(new Label
            {
                Text = "No files added yet.",
                FontSize = 13,
                TextColor = Color.FromArgb("#90A4AE"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 24)
            });
            return;
        }

        foreach (var file in _files)
        {
            var card = BuildFileCard(file);
            _mainStack.Children.Add(card);
        }
    }

    private Frame BuildFileCard(BackupFile file)
    {
        var fileLocationIds = string.IsNullOrWhiteSpace(file.LocationIds)
            ? new HashSet<string>()
            : file.LocationIds.Split(',').Select(s => s.Trim()).ToHashSet();

        var subItems = _items.Where(i => i.ParentFileId == file.Id).ToList();

        // Alert if any sub-item's locationIds differ from parent's
        bool hasSubMismatch = subItems.Any(i => i.LocationIds != file.LocationIds);
        bool isInconsistent = (_locations.Count > 0 && fileLocationIds.Count < _locations.Count) || hasSubMismatch;

        var frame = new Frame
        {
            Padding = new Thickness(12, 8),
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = isInconsistent ? Color.FromArgb("#FFB74D") : Color.FromArgb("#E0E0E0"),
            HasShadow = false,
            Margin = new Thickness(0, 2)
        };

        var cardStack = new VerticalStackLayout { Spacing = 6 };

        // File name row with edit/delete/add-sub
        var nameRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 6
        };

        var nameLabel = new Label
        {
            Text = (isInconsistent ? "⚠️ " : "") + file.Name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(nameLabel, 0);
        nameRow.Children.Add(nameLabel);

        var addSubBtn = new Button
        {
            Text = "+",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            FontSize = 12,
            CornerRadius = 6,
            WidthRequest = 34,
            HeightRequest = 30,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        addSubBtn.Clicked += async (s, e) => await OnAddSubItemAsync(file);
        Grid.SetColumn(addSubBtn, 1);
        nameRow.Children.Add(addSubBtn);

        var scanBtn = new Button
        {
            Text = "",
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            TextColor = Color.FromArgb("#E65100"),
            FontSize = 12,
            CornerRadius = 6,
            WidthRequest = 34,
            HeightRequest = 30,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        scanBtn.Clicked += async (s, e) => await OnScanSubfoldersAsync(file);
        Grid.SetColumn(scanBtn, 2);
        nameRow.Children.Add(scanBtn);

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
        editBtn.Clicked += async (s, e) => await OnEditFileAsync(file);
        Grid.SetColumn(editBtn, 3);
        nameRow.Children.Add(editBtn);

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
        deleteBtn.Clicked += async (s, e) => await OnDeleteFileAsync(file);
        Grid.SetColumn(deleteBtn, 4);
        nameRow.Children.Add(deleteBtn);

        cardStack.Children.Add(nameRow);

        // Main item location checkboxes
        cardStack.Children.Add(BuildLocationCheckboxRow(file.LocationIds,
            async newIds => {
                file.LocationIds = newIds;
                var conn = await _db.GetConnectionAsync();
                await conn.UpdateAsync(file);
                await LoadDataAsync();
                RenderPage();
            }));

        if (!string.IsNullOrWhiteSpace(file.Notes))
            cardStack.Children.Add(new Label
            {
                Text = file.Notes,
                FontSize = 11,
                TextColor = Color.FromArgb("#78909C"),
                FontAttributes = FontAttributes.Italic
            });

        // Sub-items
        foreach (var item in subItems)
        {
            cardStack.Children.Add(BuildSubItemCard(item, file.LocationIds));
        }

        frame.Content = cardStack;
        return frame;
    }

    private View BuildLocationCheckboxRow(string currentLocationIds, Func<string, Task> onChanged)
    {
        var locationIds = string.IsNullOrWhiteSpace(currentLocationIds)
            ? new HashSet<string>()
            : currentLocationIds.Split(',').Select(s => s.Trim()).ToHashSet();

        var locRow = new HorizontalStackLayout { Spacing = 16 };
        foreach (var loc in _locations)
        {
            var locId = loc.Id.ToString();
            bool hasIt = locationIds.Contains(locId);

            var locStack = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center
            };

            var chk = new CheckBox
            {
                IsChecked = hasIt,
                Color = hasIt ? Color.FromArgb("#1565C0") : Color.FromArgb("#BDBDBD")
            };

            var capturedLoc = loc;
            chk.CheckedChanged += async (s, e) =>
            {
                if (e.Value) locationIds.Add(capturedLoc.Id.ToString());
                else locationIds.Remove(capturedLoc.Id.ToString());
                await onChanged(string.Join(",", locationIds.OrderBy(x => x)));
            };

            locStack.Children.Add(chk);
            locStack.Children.Add(new Label
            {
                Text = string.IsNullOrWhiteSpace(loc.Abbreviation) ? loc.Name : loc.Abbreviation,
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#263238"),
                HorizontalOptions = LayoutOptions.Center
            });
            locRow.Children.Add(locStack);
        }
        return locRow;
    }

    private Frame BuildSubItemCard(BackupFileItem item, string parentLocationIds)
    {
        bool mismatch = item.LocationIds != parentLocationIds;

        var frame = new Frame
        {
            Padding = new Thickness(10, 6),
            CornerRadius = 6,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            BorderColor = mismatch ? Color.FromArgb("#FFB74D") : Color.FromArgb("#EEEEEE"),
            HasShadow = false,
            Margin = new Thickness(16, 2, 0, 2)
        };

        var stack = new VerticalStackLayout { Spacing = 4 };

        var nameRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 6
        };

        var nameLabel = new Label
        {
            Text = (mismatch ? "⚠️ " : "↳ ") + item.Name,
            FontSize = 13,
            TextColor = Color.FromArgb("#263238"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(nameLabel, 0);
        nameRow.Children.Add(nameLabel);

        var editBtn = new Button
        {
            Text = "✏️",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            FontSize = 11,
            CornerRadius = 6,
            WidthRequest = 30,
            HeightRequest = 26,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        editBtn.Clicked += async (s, e) => await OnEditSubItemAsync(item);
        Grid.SetColumn(editBtn, 1);
        nameRow.Children.Add(editBtn);

        var deleteBtn = new Button
        {
            Text = "️",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 11,
            CornerRadius = 6,
            WidthRequest = 30,
            HeightRequest = 26,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        deleteBtn.Clicked += async (s, e) => await OnDeleteSubItemAsync(item);
        Grid.SetColumn(deleteBtn, 2);
        nameRow.Children.Add(deleteBtn);

        stack.Children.Add(nameRow);

        stack.Children.Add(BuildLocationCheckboxRow(item.LocationIds,
            async newIds => {
                item.LocationIds = newIds;
                var conn = await _db.GetConnectionAsync();
                await conn.UpdateAsync(item);
                await LoadDataAsync();
                RenderPage();
            }));

        if (!string.IsNullOrWhiteSpace(item.Notes))
            stack.Children.Add(new Label
            {
                Text = item.Notes,
                FontSize = 11,
                TextColor = Color.FromArgb("#78909C"),
                FontAttributes = FontAttributes.Italic
            });

        frame.Content = stack;
        return frame;
    }

    private async Task OnAddSubItemAsync(BackupFile parent)
    {
        string? name = await DisplayPromptAsync("Add Sub-item", $"Sub-item name under '{parent.Name}':");
        if (string.IsNullOrWhiteSpace(name)) return;
        string? notes = await DisplayPromptAsync("Add Sub-item", "Notes (optional):");

        var item = new BackupFileItem
        {
            Username = _auth.CurrentUsername,
            ParentFileId = parent.Id,
            Name = name.Trim(),
            LocationIds = parent.LocationIds,
            Notes = notes?.Trim() ?? ""
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(item);
        await LoadDataAsync();
        RenderPage();
    }

    private async Task OnScanSubfoldersAsync(BackupFile file)
    {
        string folderPath = file.Path?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            string? picked = await DisplayPromptAsync("Scan Subfolders", "No path set for this file. Enter folder path to scan:");
            if (string.IsNullOrWhiteSpace(picked)) return;
            folderPath = picked.Trim();
            file.Path = folderPath;
            var conn2 = await _db.GetConnectionAsync();
            await conn2.UpdateAsync(file);
        }

        string? depthChoice = await DisplayActionSheet("Scan depth", "Cancel", null, "1 level down", "All levels");
        if (depthChoice == null || depthChoice == "Cancel") return;
        bool allLevels = depthChoice == "All levels";

        IEnumerable<string> subfolders;
        try
        {
            if (allLevels)
                subfolders = Directory.EnumerateDirectories(folderPath, "*", SearchOption.AllDirectories);
            else
                subfolders = Directory.EnumerateDirectories(folderPath, "*", SearchOption.TopDirectoryOnly);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not scan folder: {ex.Message}", "OK");
            return;
        }

        var folders = subfolders.ToList();
        if (folders.Count == 0)
        {
            await DisplayAlert("No Subfolders", "No subfolders found in that path.", "OK");
            return;
        }

        bool confirm = await DisplayAlert("Add Subfolders", $"Add {folders.Count} subfolder(s) as sub-items under '{file.Name}'?", "Add", "Cancel");
        if (!confirm) return;

        var conn = await _db.GetConnectionAsync();
        var existingItems = await conn.Table<BackupFileItem>()
            .Where(i => i.ParentFileId == file.Id)
            .ToListAsync();
        var existingNames = existingItems.Select(i => i.Name).ToHashSet();

        int added = 0;
        foreach (var folder in folders)
        {
            string name = System.IO.Path.GetFileName(folder);
            if (existingNames.Contains(name)) continue;
            await conn.InsertAsync(new BackupFileItem
            {
                Username = _auth.CurrentUsername,
                ParentFileId = file.Id,
                Name = name,
                LocationIds = file.LocationIds,
                Notes = ""
            });
            added++;
        }

        await DisplayAlert("Done", $"Added {added} new sub-item(s).", "OK");
        await LoadDataAsync();
        RenderPage();
    }

    private async Task OnEditSubItemAsync(BackupFileItem item)
    {
        string? name = await DisplayPromptAsync("Edit Sub-item", "Name:", initialValue: item.Name);
        if (name == null) return;
        string? notes = await DisplayPromptAsync("Edit Sub-item", "Notes:", initialValue: item.Notes);
        if (notes == null) return;

        item.Name = name.Trim();
        item.Notes = notes.Trim();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(item);
        await LoadDataAsync();
        RenderPage();
    }

    private async Task OnDeleteSubItemAsync(BackupFileItem item)
    {
        bool confirm = await DisplayAlert("Delete", $"Remove sub-item '{item.Name}'?", "Delete", "Cancel");
        if (!confirm) return;
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(item);
        await LoadDataAsync();
        RenderPage();
    }

    private async Task OnManageLocationsAsync()
    {
        var action = await DisplayActionSheet("Manage Locations", "Cancel", null, "➕ Add Location", "✏️ Rename a Location", "️ Delete a Location");
        if (action == "➕ Add Location")
        {
            string? name = await DisplayPromptAsync("Add Location", "Name (e.g. Backblaze, OneDrive, PC):");
            if (string.IsNullOrWhiteSpace(name)) return;
            string? abbr = await DisplayPromptAsync("Add Location", "Short abbreviation (e.g. BB, OD, PC):", initialValue: name.Length <= 4 ? name : name[..2].ToUpper());
            string? remote = await DisplayPromptAsync("Add Location", "Rclone remote name (optional, e.g. b3ai, onedrive):");
            var conn = await _db.GetConnectionAsync();
            await conn.InsertAsync(new BackupLocation
            {
                Username = _auth.CurrentUsername,
                Name = name.Trim(),
                Abbreviation = abbr?.Trim() ?? name.Trim(),
                RcloneRemote = remote?.Trim() ?? ""
            });
            await LoadDataAsync();
            RenderPage();
        }
        else if (action == "✏️ Rename a Location")
        {
            if (_locations.Count == 0)
            {
                await DisplayAlert("No Locations", "There are no locations to rename.", "OK");
                return;
            }
            var options = _locations.Select(l => l.Name).ToArray();
            string? chosen = await DisplayActionSheet("Rename which location?", "Cancel", null, options);
            if (chosen == null || chosen == "Cancel") return;
            var toRename = _locations.FirstOrDefault(l => l.Name == chosen);
            if (toRename == null) return;
            string? newName = await DisplayPromptAsync("Rename Location", "New name:", initialValue: toRename.Name);
            if (string.IsNullOrWhiteSpace(newName)) return;
            string? newAbbr = await DisplayPromptAsync("Rename Location", "New abbreviation:", initialValue: toRename.Abbreviation);
            toRename.Name = newName.Trim();
            toRename.Abbreviation = newAbbr?.Trim() ?? newName.Trim();
            var conn = await _db.GetConnectionAsync();
            await conn.UpdateAsync(toRename);
            await LoadDataAsync();
            RenderPage();
        }
        else if (action == "️ Delete a Location")
        {
            if (_locations.Count == 0)
            {
                await DisplayAlert("No Locations", "There are no locations to delete.", "OK");
                return;
            }
            var options = _locations.Select(l => l.Name).ToArray();
            string? chosen = await DisplayActionSheet("Delete which location?", "Cancel", null, options);
            if (chosen == null || chosen == "Cancel") return;
            var toDelete = _locations.FirstOrDefault(l => l.Name == chosen);
            if (toDelete == null) return;
            bool confirm = await DisplayAlert("Delete", $"Remove location '{toDelete.Name}'? This won't delete files, just the location tag.", "Delete", "Cancel");
            if (!confirm) return;
            var conn = await _db.GetConnectionAsync();
            await conn.DeleteAsync(toDelete);
            // Remove this location from all files
            var idStr = toDelete.Id.ToString();
            foreach (var file in _files.Where(f => (f.LocationIds ?? "").Split(',').Contains(idStr)))
            {
                var ids = file.LocationIds.Split(',').Where(x => x.Trim() != idStr).ToList();
                file.LocationIds = string.Join(",", ids);
                await conn.UpdateAsync(file);
            }
            foreach (var subItem in _items.Where(i => (i.LocationIds ?? "").Split(',').Contains(idStr)))
            {
                var ids = subItem.LocationIds.Split(',').Where(x => x.Trim() != idStr).ToList();
                subItem.LocationIds = string.Join(",", ids);
                await conn.UpdateAsync(subItem);
            }
            await LoadDataAsync();
            RenderPage();
        }
    }

    private async Task OnAddFileAsync()
    {
        if (_locations.Count == 0)
        {
            await DisplayAlert("No Locations Yet", "Add at least one location first using '⚙️ Manage Locations'.", "OK");
            return;
        }
        string? name = await DisplayPromptAsync("Add File", "File name (e.g. Brand Kit v3.psd):");
        if (string.IsNullOrWhiteSpace(name)) return;
        string? path = await DisplayPromptAsync("Add File", "Folder path (optional, used for subfolder scan):", initialValue: "");

        var file = new BackupFile
        {
            Username = _auth.CurrentUsername,
            Name = name.Trim(),
            Path = path?.Trim() ?? "",
            Category = "",
            LocationIds = ""
        };
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(file);
        await LoadDataAsync();
        RenderPage();
    }

    private async Task OnRcloneBuilderAsync()
    {
        await Navigation.PushAsync(new RclonePresetsPage(_db, _auth));
    }

    private async Task OnEditFileAsync(BackupFile file)
    {
        string? name = await DisplayPromptAsync("Edit File", "File name:", initialValue: file.Name);
        if (name == null) return;
        string? path = await DisplayPromptAsync("Edit File", "Folder path:", initialValue: file.Path ?? "");
        if (path == null) return;
        string? category = await DisplayPromptAsync("Edit File", "Category:", initialValue: file.Category);
        if (category == null) return;
        string? notes = await DisplayPromptAsync("Edit File", "Notes:", initialValue: file.Notes);
        if (notes == null) return;

        file.Name = name.Trim();
        file.Path = path.Trim();
        file.Category = category.Trim();
        file.Notes = notes.Trim();
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(file);
        await LoadDataAsync();
        RenderPage();
    }

    private async Task OnDeleteFileAsync(BackupFile file)
    {
        bool confirm = await DisplayAlert("Delete", $"Remove '{file.Name}'?", "Delete", "Cancel");
        if (!confirm) return;
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync(file);
        await conn.ExecuteAsync("DELETE FROM backup_file_items WHERE ParentFileId = ?", file.Id);
        await LoadDataAsync();
        RenderPage();
    }
}

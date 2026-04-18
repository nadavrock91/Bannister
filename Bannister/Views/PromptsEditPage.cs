using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for viewing, filtering, and editing all prompts.
/// Provides full CRUD operations with group/pack filtering.
/// </summary>
public class PromptsEditPage : ContentPage
{
    private readonly PromptService _prompts;
    
    // UI Controls
    private Picker _packPicker = null!;
    private Picker _groupPicker = null!;
    private Entry _searchEntry = null!;
    private VerticalStackLayout _promptsStack = null!;
    private Label _countLabel = null!;
    
    // State
    private List<string> _packNames = new();
    private List<string> _groupNames = new();
    private List<PromptItem> _allPrompts = new();
    private List<PromptItem> _filteredPrompts = new();
    private string _selectedPack = "";
    private string _selectedGroup = "All";
    private string _searchText = "";

    public PromptsEditPage(PromptService prompts)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: Constructor started");
            _prompts = prompts ?? throw new ArgumentNullException(nameof(prompts));
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: PromptService assigned");
            
            Title = "Edit Prompts";
            BackgroundColor = Color.FromArgb("#F5F5F5");
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: Basic properties set");
            
            BuildUI();
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: BuildUI completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage Constructor ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage Stack: {ex.StackTrace}");
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: OnAppearing started");
            base.OnAppearing();
            await LoadDataAsync();
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: OnAppearing completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage OnAppearing ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage Stack: {ex.StackTrace}");
        }
    }

    private void BuildUI()
    {
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: BuildUI started");
        
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 12
        };
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: mainStack created");

        // Header
        var headerLabel = new Label
        {
            Text = "📝 Edit Prompts",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };
        mainStack.Children.Add(headerLabel);
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: header added");

        // Filters Frame
        var filtersFrame = new Frame
        {
            Padding = 12,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: filtersFrame created");

        var filtersStack = new VerticalStackLayout { Spacing = 10 };

        // Pack and Group row
        var pickersRow = new HorizontalStackLayout { Spacing = 12 };

        // Pack picker
        var packStack = new VerticalStackLayout { Spacing = 4 };
        packStack.Children.Add(new Label { Text = "Pack", FontSize = 11, TextColor = Color.FromArgb("#666") });
        _packPicker = new Picker
        {
            WidthRequest = 140,
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };
        _packPicker.SelectedIndexChanged += OnPackChanged;
        packStack.Children.Add(_packPicker);
        pickersRow.Children.Add(packStack);
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: pack picker created");

        // Group picker
        var groupStack = new VerticalStackLayout { Spacing = 4 };
        groupStack.Children.Add(new Label { Text = "Group", FontSize = 11, TextColor = Color.FromArgb("#666") });
        _groupPicker = new Picker
        {
            WidthRequest = 140,
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };
        _groupPicker.SelectedIndexChanged += OnGroupChanged;
        groupStack.Children.Add(_groupPicker);
        pickersRow.Children.Add(groupStack);
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: group picker created");

        filtersStack.Children.Add(pickersRow);

        // Search row
        var searchRow = new HorizontalStackLayout { Spacing = 8 };
        
        _searchEntry = new Entry
        {
            Placeholder = "🔍 Search prompts...",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            HorizontalOptions = LayoutOptions.Fill,
            WidthRequest = 250
        };
        _searchEntry.TextChanged += OnSearchChanged;
        searchRow.Children.Add(_searchEntry);
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: search entry created");

        _countLabel = new Label
        {
            Text = "0 prompts",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        searchRow.Children.Add(_countLabel);

        filtersStack.Children.Add(searchRow);

        filtersFrame.Content = filtersStack;
        mainStack.Children.Add(filtersFrame);
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: filters added to main");

        // Prompts list
        var listFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 10,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            VerticalOptions = LayoutOptions.FillAndExpand
        };

        var scrollView = new ScrollView
        {
            VerticalOptions = LayoutOptions.FillAndExpand
        };

        _promptsStack = new VerticalStackLayout
        {
            Spacing = 1,
            Padding = 8
        };
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: promptsStack created");

        scrollView.Content = _promptsStack;
        listFrame.Content = scrollView;
        mainStack.Children.Add(listFrame);
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: list added to main");

        Content = mainStack;
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: Content set, BuildUI complete");
    }

    private async Task LoadDataAsync()
    {
        System.Diagnostics.Debug.WriteLine("PromptsEditPage: LoadDataAsync started");
        try
        {
            // Load pack names
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: Getting pack names...");
            _packNames = await _prompts.GetPackNamesAsync();
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage: Got {_packNames.Count} packs");
            
            _packPicker.Items.Clear();
            foreach (var pack in _packNames)
            {
                _packPicker.Items.Add(pack);
            }
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: Packs added to picker");

            if (_packNames.Count > 0)
            {
                int defaultIndex = _packNames.IndexOf("Writing");
                if (defaultIndex < 0) defaultIndex = 0;
                System.Diagnostics.Debug.WriteLine($"PromptsEditPage: Setting picker index to {defaultIndex}");
                _packPicker.SelectedIndex = defaultIndex;
                System.Diagnostics.Debug.WriteLine("PromptsEditPage: Picker index set");
            }
            else
            {
                // No packs, show empty state
                _promptsStack.Children.Clear();
                _promptsStack.Children.Add(new Label
                {
                    Text = "No prompt packs found.\nAdd prompts from the main Prompts page.",
                    TextColor = Color.FromArgb("#999"),
                    HorizontalOptions = LayoutOptions.Center,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 40)
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading data: {ex.Message}");
            await DisplayAlert("Error", $"Failed to load prompts: {ex.Message}", "OK");
        }
    }

    private async void OnPackChanged(object? sender, EventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage: OnPackChanged, index={_packPicker.SelectedIndex}");
            if (_packPicker.SelectedIndex < 0 || _packPicker.SelectedIndex >= _packNames.Count)
                return;

            _selectedPack = _packNames[_packPicker.SelectedIndex];
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage: Selected pack: {_selectedPack}");
            
            // Load prompts for this pack
            _allPrompts = await _prompts.GetPromptsAsync(_selectedPack);
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage: Loaded {_allPrompts.Count} prompts");
            
            // Update group picker
            _groupNames = _allPrompts.Select(p => p.GroupName).Distinct().OrderBy(g => g).ToList();
            _groupPicker.Items.Clear();
            _groupPicker.Items.Add("All");
            foreach (var group in _groupNames)
            {
                _groupPicker.Items.Add(group);
            }
            _groupPicker.SelectedIndex = 0;
            _selectedGroup = "All";
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage: Group picker updated with {_groupNames.Count} groups");

            ApplyFilters();
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: OnPackChanged complete");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage OnPackChanged ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    private void OnGroupChanged(object? sender, EventArgs e)
    {
        if (_groupPicker.SelectedIndex < 0) return;
        
        _selectedGroup = _groupPicker.SelectedIndex == 0 ? "All" : _groupNames[_groupPicker.SelectedIndex - 1];
        ApplyFilters();
    }

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue?.Trim() ?? "";
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: ApplyFilters started");
            _filteredPrompts = _allPrompts;

            // Filter by group
            if (_selectedGroup != "All")
            {
                _filteredPrompts = _filteredPrompts.Where(p => p.GroupName == _selectedGroup).ToList();
            }

            // Filter by search text
            if (!string.IsNullOrEmpty(_searchText))
            {
                string search = _searchText.ToLower();
                _filteredPrompts = _filteredPrompts
                    .Where(p => p.Text.ToLower().Contains(search) || p.GroupName.ToLower().Contains(search))
                    .ToList();
            }

            // Sort by priority then group then text
            _filteredPrompts = _filteredPrompts
                .OrderBy(p => p.Priority == 0 ? int.MaxValue : p.Priority)
                .ThenBy(p => p.GroupName)
                .ThenBy(p => p.Text)
                .ToList();

            System.Diagnostics.Debug.WriteLine($"PromptsEditPage: Filtered to {_filteredPrompts.Count} prompts");
            _countLabel.Text = $"{_filteredPrompts.Count} prompts";
            DisplayPrompts();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage ApplyFilters ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    private void DisplayPrompts()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: DisplayPrompts started");
            _promptsStack.Children.Clear();

        if (_filteredPrompts.Count == 0)
        {
            _promptsStack.Children.Add(new Label
            {
                Text = "No prompts found",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 20)
            });
            System.Diagnostics.Debug.WriteLine("PromptsEditPage: No prompts to display");
            return;
        }

        int count = 0;
        foreach (var prompt in _filteredPrompts)
        {
            var row = BuildPromptRow(prompt);
            _promptsStack.Children.Add(row);
            count++;
        }
        System.Diagnostics.Debug.WriteLine($"PromptsEditPage: Displayed {count} prompt rows");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PromptsEditPage DisplayPrompts ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
        }
    }

    private Frame BuildPromptRow(PromptItem prompt)
    {
        // Color based on group
        var groupColors = new[] { "#E3F2FD", "#E8F5E9", "#FFF3E0", "#F3E5F5", "#E0F7FA", "#FCE4EC", "#FBE9E7", "#E8EAF6" };
        int colorIndex = Math.Abs(prompt.GroupName.GetHashCode()) % groupColors.Length;

        var frame = new Frame
        {
            Padding = 10,
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb(groupColors[colorIndex]),
            HasShadow = false,
            BorderColor = prompt.Priority > 0 ? Color.FromArgb("#FF9800") : Colors.Transparent,
            Margin = new Thickness(0, 2)
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        // Left side: prompt info
        var infoStack = new VerticalStackLayout { Spacing = 4 };

        // Group and priority badges
        var badgeRow = new HorizontalStackLayout { Spacing = 6 };
        
        var groupBadge = new Frame
        {
            Padding = new Thickness(6, 2),
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#5B63EE"),
            HasShadow = false
        };
        groupBadge.Content = new Label
        {
            Text = prompt.GroupName,
            FontSize = 10,
            TextColor = Colors.White
        };
        badgeRow.Children.Add(groupBadge);

        if (prompt.Priority > 0)
        {
            var priorityBadge = new Frame
            {
                Padding = new Thickness(6, 2),
                CornerRadius = 4,
                BackgroundColor = Color.FromArgb("#FF9800"),
                HasShadow = false
            };
            priorityBadge.Content = new Label
            {
                Text = $"P{prompt.Priority}",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            };
            badgeRow.Children.Add(priorityBadge);
        }

        if (!prompt.IsActive)
        {
            var inactiveBadge = new Frame
            {
                Padding = new Thickness(6, 2),
                CornerRadius = 4,
                BackgroundColor = Color.FromArgb("#9E9E9E"),
                HasShadow = false
            };
            inactiveBadge.Content = new Label
            {
                Text = "Inactive",
                FontSize = 10,
                TextColor = Colors.White
            };
            badgeRow.Children.Add(inactiveBadge);
        }

        // Rating stars
        var ratingLabel = new Label
        {
            Text = new string('★', prompt.Rating) + new string('☆', 5 - prompt.Rating),
            FontSize = 10,
            TextColor = Color.FromArgb("#FFC107"),
            VerticalOptions = LayoutOptions.Center
        };
        badgeRow.Children.Add(ratingLabel);

        infoStack.Children.Add(badgeRow);

        // Prompt text
        var textLabel = new Label
        {
            Text = prompt.Text,
            FontSize = 13,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 3
        };
        infoStack.Children.Add(textLabel);

        // Stats
        var statsLabel = new Label
        {
            Text = $"Used {prompt.UsageCount}x | Prob: {prompt.Probability:P0}",
            FontSize = 10,
            TextColor = Color.FromArgb("#999")
        };
        infoStack.Children.Add(statsLabel);

        Grid.SetColumn(infoStack, 0);
        grid.Children.Add(infoStack);

        // Right side: action buttons
        var actionsStack = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center
        };

        // Menu button
        var menuBtn = new Button
        {
            Text = "⋮",
            FontSize = 18,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333"),
            WidthRequest = 36,
            HeightRequest = 36,
            CornerRadius = 18,
            Padding = 0
        };
        menuBtn.Clicked += async (s, e) => await ShowPromptMenuAsync(prompt);
        actionsStack.Children.Add(menuBtn);

        Grid.SetColumn(actionsStack, 1);
        grid.Children.Add(actionsStack);

        frame.Content = grid;
        return frame;
    }

    private async Task ShowPromptMenuAsync(PromptItem prompt)
    {
        string activeText = prompt.IsActive ? "🚫 Deactivate" : "✅ Activate";
        
        var options = new[]
        {
            "✏️ Edit Text",
            "🔢 Set Priority",
            "⭐ Set Rating",
            "📁 Change Group",
            "🎲 Set Probability",
            activeText,
            "🗑️ Delete"
        };

        var result = await DisplayActionSheet($"Prompt Options", "Cancel", null, options);

        if (result == null || result == "Cancel") return;

        if (result == "✏️ Edit Text")
        {
            await EditTextAsync(prompt);
        }
        else if (result == "🔢 Set Priority")
        {
            await EditPriorityAsync(prompt);
        }
        else if (result == "⭐ Set Rating")
        {
            await EditRatingAsync(prompt);
        }
        else if (result == "📁 Change Group")
        {
            await EditGroupAsync(prompt);
        }
        else if (result == "🎲 Set Probability")
        {
            await EditProbabilityAsync(prompt);
        }
        else if (result == activeText)
        {
            await ToggleActiveAsync(prompt);
        }
        else if (result == "🗑️ Delete")
        {
            await DeletePromptAsync(prompt);
        }
    }

    private async Task EditTextAsync(PromptItem prompt)
    {
        string? newText = await DisplayPromptAsync(
            "Edit Text",
            "Enter the new prompt text:",
            initialValue: prompt.Text);

        if (string.IsNullOrWhiteSpace(newText)) return;

        prompt.Text = newText.Trim();
        await _prompts.UpdatePromptAsync(prompt);
        ApplyFilters();
    }

    private async Task EditPriorityAsync(PromptItem prompt)
    {
        string? priorityStr = await DisplayPromptAsync(
            "Set Priority",
            "Enter priority (1 = highest, 0 = none):",
            initialValue: prompt.Priority.ToString(),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrEmpty(priorityStr)) return;

        if (int.TryParse(priorityStr, out int newPriority) && newPriority >= 0)
        {
            prompt.Priority = newPriority;
            await _prompts.UpdatePromptAsync(prompt);
            ApplyFilters();
        }
        else
        {
            await DisplayAlert("Invalid", "Enter 0 or a positive number.", "OK");
        }
    }

    private async Task EditRatingAsync(PromptItem prompt)
    {
        var options = new[] { "★☆☆☆☆ (1)", "★★☆☆☆ (2)", "★★★☆☆ (3)", "★★★★☆ (4)", "★★★★★ (5)" };
        var result = await DisplayActionSheet("Set Rating", "Cancel", null, options);

        if (result == null || result == "Cancel") return;

        int rating = Array.IndexOf(options, result) + 1;
        prompt.Rating = rating;
        await _prompts.UpdatePromptAsync(prompt);
        ApplyFilters();
    }

    private async Task EditGroupAsync(PromptItem prompt)
    {
        var groupOptions = _groupNames.Concat(new[] { "➕ New Group" }).ToArray();
        var result = await DisplayActionSheet("Change Group", "Cancel", null, groupOptions);

        if (result == null || result == "Cancel") return;

        string newGroup;
        if (result == "➕ New Group")
        {
            newGroup = await DisplayPromptAsync("New Group", "Enter group name:");
            if (string.IsNullOrWhiteSpace(newGroup)) return;
            newGroup = newGroup.Trim();
        }
        else
        {
            newGroup = result;
        }

        prompt.GroupName = newGroup;
        await _prompts.UpdatePromptAsync(prompt);
        
        // Refresh group list
        _allPrompts = await _prompts.GetPromptsAsync(_selectedPack);
        _groupNames = _allPrompts.Select(p => p.GroupName).Distinct().OrderBy(g => g).ToList();
        
        // Update picker
        int currentGroupIndex = _groupPicker.SelectedIndex;
        _groupPicker.Items.Clear();
        _groupPicker.Items.Add("All");
        foreach (var group in _groupNames)
        {
            _groupPicker.Items.Add(group);
        }
        _groupPicker.SelectedIndex = Math.Min(currentGroupIndex, _groupPicker.Items.Count - 1);
        
        ApplyFilters();
    }

    private async Task EditProbabilityAsync(PromptItem prompt)
    {
        string? probStr = await DisplayPromptAsync(
            "Set Probability",
            "Enter probability (0.0 to 1.0):\n\n1.0 = always eligible\n0.5 = 50% chance\n0.0 = never selected",
            initialValue: prompt.Probability.ToString("F2"),
            keyboard: Keyboard.Numeric);

        if (string.IsNullOrEmpty(probStr)) return;

        if (double.TryParse(probStr, out double newProb) && newProb >= 0 && newProb <= 1)
        {
            prompt.Probability = newProb;
            await _prompts.UpdatePromptAsync(prompt);
            ApplyFilters();
        }
        else
        {
            await DisplayAlert("Invalid", "Enter a number between 0.0 and 1.0.", "OK");
        }
    }

    private async Task ToggleActiveAsync(PromptItem prompt)
    {
        prompt.IsActive = !prompt.IsActive;
        await _prompts.UpdatePromptAsync(prompt);
        ApplyFilters();
    }

    private async Task DeletePromptAsync(PromptItem prompt)
    {
        bool confirm = await DisplayAlert(
            "Delete Prompt?",
            $"Delete this prompt?\n\n\"{prompt.Text.Substring(0, Math.Min(100, prompt.Text.Length))}...\"",
            "Delete",
            "Cancel");

        if (confirm)
        {
            await _prompts.DeletePromptAsync(prompt.Id);
            _allPrompts.Remove(prompt);
            ApplyFilters();
        }
    }
}

using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Browse prompts with compact data grid view and detail panel.
/// </summary>
public class PromptsBrowsePage : ContentPage
{
    private readonly PromptService _prompts;
    
    // UI
    private Label _headerLabel;
    private Picker _packPicker;
    private Picker _categoryPicker;
    private Entry _searchEntry;
    private Grid _dataGrid;
    private Frame _detailPanel;
    private Label _detailText;
    private Label _detailMeta;
    private VerticalStackLayout _examplesStack;
    private Button _showArchivedBtn;
    private List<Button> _probButtons;
    private List<Button> _prioButtons;
    private Entry _probEntry;
    private Entry _prioEntry;
    
    // State
    private List<string> _packNames = new();
    private List<string> _categoryNames = new();
    private string _selectedPack = "";
    private string _selectedCategory = "All";
    private bool _showingArchived = false;
    private PromptItem? _selectedPrompt = null;
    private List<PromptItem> _currentPrompts = new();
    private string _searchText = "";

    // Grid config
    private const int ColumnCount = 4;
    private const double CellWidth = 200;
    private const double CellHeight = 55;

    public PromptsBrowsePage(PromptService prompts)
    {
        _prompts = prompts;
        
        Title = "Browse Prompts";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPacksAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            Padding = 12,
            RowSpacing = 8,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),  // Header + Controls
                new RowDefinition(GridLength.Star)   // Content
            }
        };

        // Header row
        var headerRow = new HorizontalStackLayout { Spacing = 10 };

        headerRow.Children.Add(new Label
        {
            Text = "✨",
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center
        });

        _headerLabel = new Label
        {
            Text = "0 prompts",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Children.Add(_headerLabel);

        // Pack picker
        _packPicker = new Picker
        {
            Title = "Pack",
            BackgroundColor = Colors.White,
            WidthRequest = 120
        };
        _packPicker.SelectedIndexChanged += OnPackChanged;
        headerRow.Children.Add(_packPicker);

        // Category picker
        _categoryPicker = new Picker
        {
            Title = "Category",
            BackgroundColor = Colors.White,
            WidthRequest = 120
        };
        _categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        headerRow.Children.Add(_categoryPicker);

        // Search
        _searchEntry = new Entry
        {
            Placeholder = "🔍 Search...",
            BackgroundColor = Colors.White,
            WidthRequest = 140
        };
        _searchEntry.TextChanged += OnSearchChanged;
        headerRow.Children.Add(_searchEntry);

        // Add button
        var addBtn = new Button
        {
            Text = "+ New",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 6,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        };
        addBtn.Clicked += OnAddPromptClicked;
        headerRow.Children.Add(addBtn);

        // Export button
        var exportBtn = new Button
        {
            Text = "📤",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            CornerRadius = 6,
            WidthRequest = 36,
            HeightRequest = 36
        };
        exportBtn.Clicked += OnExportClicked;
        headerRow.Children.Add(exportBtn);

        // Archived toggle
        _showArchivedBtn = new Button
        {
            Text = "📦",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 6,
            WidthRequest = 36,
            HeightRequest = 36
        };
        _showArchivedBtn.Clicked += OnShowArchivedClicked;
        headerRow.Children.Add(_showArchivedBtn);

        Grid.SetRow(headerRow, 0);
        mainGrid.Children.Add(headerRow);

        // Content area
        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(320))
            },
            ColumnSpacing = 12
        };

        // Data grid
        var gridScroll = new ScrollView { Orientation = ScrollOrientation.Both };
        _dataGrid = new Grid
        {
            ColumnSpacing = 1,
            RowSpacing = 1,
            BackgroundColor = Color.FromArgb("#DDD"),
            Padding = 1
        };
        gridScroll.Content = _dataGrid;
        Grid.SetColumn(gridScroll, 0);
        contentGrid.Children.Add(gridScroll);

        // Detail panel
        _detailPanel = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#7B1FA2"),
            HasShadow = true,
            IsVisible = false
        };

        var detailStack = new VerticalStackLayout { Spacing = 10 };

        var closeRow = new HorizontalStackLayout();
        closeRow.Children.Add(new Label
        {
            Text = "📄 Prompt Details",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#7B1FA2"),
            HorizontalOptions = LayoutOptions.StartAndExpand,
            VerticalOptions = LayoutOptions.Center
        });
        var closeBtn = new Button
        {
            Text = "✕",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#999"),
            WidthRequest = 30,
            HeightRequest = 30,
            Padding = 0
        };
        closeBtn.Clicked += (s, e) => { _detailPanel.IsVisible = false; _selectedPrompt = null; };
        closeRow.Children.Add(closeBtn);
        detailStack.Children.Add(closeRow);

        // Prompt text (read-only, wrapped)
        _detailText = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        var textScroll = new ScrollView { HeightRequest = 100 };
        textScroll.Content = _detailText;
        detailStack.Children.Add(textScroll);

        // Meta info
        _detailMeta = new Label
        {
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        detailStack.Children.Add(_detailMeta);

        // Examples section
        detailStack.Children.Add(new Label
        {
            Text = "📝 Examples:",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#666")
        });
        
        _examplesStack = new VerticalStackLayout { Spacing = 4 };
        var examplesScroll = new ScrollView { HeightRequest = 100 };
        examplesScroll.Content = _examplesStack;
        detailStack.Children.Add(examplesScroll);
        
        var addExampleBtn = new Button
        {
            Text = "+ Add Example",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 4,
            HeightRequest = 28,
            FontSize = 11,
            Padding = new Thickness(8, 0)
        };
        addExampleBtn.Clicked += OnAddExampleClicked;
        detailStack.Children.Add(addExampleBtn);

        // Probability row
        var probRow = new HorizontalStackLayout { Spacing = 6 };
        probRow.Children.Add(new Label
        {
            Text = "Prob:",
            FontSize = 11,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 35
        });
        
        _probButtons = new List<Button>();
        var probValues = new[] { ("25%", 0.25), ("50%", 0.50), ("75%", 0.75), ("100%", 1.0) };
        foreach (var (label, value) in probValues)
        {
            var btn = new Button
            {
                Text = label,
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                TextColor = Color.FromArgb("#333"),
                CornerRadius = 4,
                WidthRequest = 42,
                HeightRequest = 28,
                Padding = 0,
                FontSize = 10
            };
            var probValue = value;
            btn.Clicked += async (s, e) => await SetProbabilityAsync(probValue);
            _probButtons.Add(btn);
            probRow.Children.Add(btn);
        }
        
        _probEntry = new Entry
        {
            Placeholder = "Custom",
            Keyboard = Keyboard.Numeric,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            WidthRequest = 55,
            HeightRequest = 28,
            FontSize = 10
        };
        _probEntry.Completed += async (s, e) =>
        {
            if (_selectedPrompt == null) return;
            var text = _probEntry.Text?.Replace("%", "").Trim();
            if (double.TryParse(text, out double val))
            {
                if (val > 1) val = val / 100.0; // Convert 75 to 0.75
                val = Math.Clamp(val, 0.0, 1.0);
                await SetProbabilityAsync(val);
            }
            _probEntry.Text = "";
            _probEntry.Unfocus();
        };
        probRow.Children.Add(_probEntry);
        detailStack.Children.Add(probRow);

        // Priority row
        var prioRow = new HorizontalStackLayout { Spacing = 6 };
        prioRow.Children.Add(new Label
        {
            Text = "Prio:",
            FontSize = 11,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 35
        });
        
        _prioButtons = new List<Button>();
        var prioValues = new[] { ("None", 0), ("1", 1), ("2", 2), ("3", 3) };
        foreach (var (label, value) in prioValues)
        {
            var btn = new Button
            {
                Text = label,
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                TextColor = Color.FromArgb("#333"),
                CornerRadius = 4,
                WidthRequest = 42,
                HeightRequest = 28,
                Padding = 0,
                FontSize = 10
            };
            var prioValue = value;
            btn.Clicked += async (s, e) => await SetPriorityAsync(prioValue);
            _prioButtons.Add(btn);
            prioRow.Children.Add(btn);
        }
        
        _prioEntry = new Entry
        {
            Placeholder = "Custom",
            Keyboard = Keyboard.Numeric,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            WidthRequest = 55,
            HeightRequest = 28,
            FontSize = 10
        };
        _prioEntry.Completed += async (s, e) =>
        {
            if (_selectedPrompt == null) return;
            if (int.TryParse(_prioEntry.Text, out int val))
            {
                val = Math.Max(0, val);
                await SetPriorityAsync(val);
            }
            _prioEntry.Text = "";
            _prioEntry.Unfocus();
        };
        prioRow.Children.Add(_prioEntry);
        detailStack.Children.Add(prioRow);

        // Action buttons
        var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        var btnEdit = MiniBtn("✏️", "#2196F3"); btnEdit.Clicked += OnEditClicked;
        var btnCopy = MiniBtn("📋", "#FF9800"); btnCopy.Clicked += OnCopyClicked;
        var btnMove = MiniBtn("📂", "#9C27B0"); btnMove.Clicked += OnMoveClicked;
        var btnToggle = MiniBtn("⏸️", "#607D8B"); btnToggle.Clicked += OnToggleClicked;
        var btnArchive = MiniBtn("🗄️", "#757575"); btnArchive.Clicked += OnArchiveClicked;
        actions.Children.Add(btnEdit);
        actions.Children.Add(btnCopy);
        actions.Children.Add(btnMove);
        actions.Children.Add(btnToggle);
        actions.Children.Add(btnArchive);
        detailStack.Children.Add(actions);

        _detailPanel.Content = detailStack;
        Grid.SetColumn(_detailPanel, 1);
        contentGrid.Children.Add(_detailPanel);

        Grid.SetRow(contentGrid, 1);
        mainGrid.Children.Add(contentGrid);

        Content = mainGrid;
    }

    private Button MiniBtn(string text, string color) => new Button
    {
        Text = text,
        BackgroundColor = Color.FromArgb(color),
        TextColor = Colors.White,
        CornerRadius = 4,
        WidthRequest = 36,
        HeightRequest = 32,
        Padding = 0,
        Margin = new Thickness(0, 0, 4, 4)
    };

    #region Data Loading

    private async Task LoadPacksAsync()
    {
        _packNames = await _prompts.GetPackNamesAsync();
        
        _packPicker.Items.Clear();
        foreach (var pack in _packNames)
            _packPicker.Items.Add(pack);

        if (_packNames.Count > 0)
        {
            int idx = _packNames.IndexOf("Writing");
            _packPicker.SelectedIndex = idx >= 0 ? idx : 0;
            _selectedPack = _packNames[_packPicker.SelectedIndex];
            await LoadCategoriesAsync();
        }
    }

    private async Task LoadCategoriesAsync()
    {
        if (string.IsNullOrEmpty(_selectedPack)) return;

        _categoryNames = await _prompts.GetGroupNamesAsync(_selectedPack);
        
        _categoryPicker.Items.Clear();
        _categoryPicker.Items.Add("All");
        foreach (var cat in _categoryNames)
            _categoryPicker.Items.Add(cat);

        _categoryPicker.SelectedIndex = 0;
        _selectedCategory = "All";
        await RefreshPromptsAsync();
    }

    private async void OnPackChanged(object? sender, EventArgs e)
    {
        if (_packPicker.SelectedIndex < 0) return;
        _selectedPack = _packNames[_packPicker.SelectedIndex];
        await LoadCategoriesAsync();
    }

    private async void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (_categoryPicker.SelectedIndex < 0) return;
        _selectedCategory = _categoryPicker.Items[_categoryPicker.SelectedIndex];
        await RefreshPromptsAsync();
    }

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue?.ToLower() ?? "";
        await RefreshPromptsAsync();
    }

    private async void OnShowArchivedClicked(object? sender, EventArgs e)
    {
        _showingArchived = !_showingArchived;
        _showArchivedBtn.BackgroundColor = _showingArchived ? Color.FromArgb("#F57C00") : Color.FromArgb("#9E9E9E");
        await RefreshPromptsAsync();
    }

    private async Task RefreshPromptsAsync()
    {
        List<PromptItem> prompts;

        if (_showingArchived)
        {
            prompts = await _prompts.GetArchivedPromptsAsync();
            prompts = prompts.Where(p => p.PackName == _selectedPack).ToList();
        }
        else
        {
            prompts = await _prompts.GetPackPromptsAsync(_selectedPack);
            if (_selectedCategory != "All")
                prompts = prompts.Where(p => p.GroupName == _selectedCategory).ToList();
        }

        // Search filter
        if (!string.IsNullOrEmpty(_searchText))
        {
            prompts = prompts.Where(p =>
                p.Text.ToLower().Contains(_searchText) ||
                p.GroupName.ToLower().Contains(_searchText) ||
                (p.Examples?.ToLower().Contains(_searchText) ?? false)
            ).ToList();
        }

        _currentPrompts = prompts;

        // Update header
        int activeCount = prompts.Count(p => p.IsActive);
        _headerLabel.Text = _showingArchived
            ? $"{prompts.Count} archived"
            : $"{prompts.Count} prompts ({activeCount} active)";

        BuildDataGrid(prompts);
    }

    private void BuildDataGrid(List<PromptItem> prompts)
    {
        _dataGrid.Children.Clear();
        _dataGrid.RowDefinitions.Clear();
        _dataGrid.ColumnDefinitions.Clear();

        if (prompts.Count == 0)
        {
            _dataGrid.BackgroundColor = Colors.Transparent;
            _dataGrid.Children.Add(new Label
            {
                Text = "No prompts. Click + New to add one.",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20)
            });
            return;
        }

        _dataGrid.BackgroundColor = Color.FromArgb("#DDD");

        int cols = ColumnCount;
        int rows = (int)Math.Ceiling((double)prompts.Count / cols);

        for (int c = 0; c < cols; c++)
            _dataGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));
        for (int r = 0; r < rows; r++)
            _dataGrid.RowDefinitions.Add(new RowDefinition(new GridLength(CellHeight)));

        for (int i = 0; i < prompts.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            var cell = BuildCell(prompts[i]);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            _dataGrid.Children.Add(cell);
        }
    }

    private Frame BuildCell(PromptItem prompt)
    {
        Color bg = !prompt.IsActive ? Color.FromArgb("#F5F5F5")
            : prompt.Priority > 0 ? Color.FromArgb("#FFF3E0")
            : Colors.White;

        var frame = new Frame
        {
            Padding = 4,
            CornerRadius = 0,
            BackgroundColor = bg,
            BorderColor = Colors.Transparent,
            HasShadow = false
        };

        var stack = new VerticalStackLayout { Spacing = 1 };

        // Icons row
        var icons = new HorizontalStackLayout { Spacing = 2 };
        if (prompt.Priority > 0) icons.Children.Add(new Label { Text = $"P{prompt.Priority}", FontSize = 7, TextColor = Color.FromArgb("#FF9800"), FontAttributes = FontAttributes.Bold });
        if (!prompt.IsActive) icons.Children.Add(new Label { Text = "⏸", FontSize = 7 });
        if (!string.IsNullOrWhiteSpace(prompt.Examples))
        {
            var count = prompt.Examples.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
            icons.Children.Add(new Label { Text = $"📝{count}", FontSize = 7, TextColor = Color.FromArgb("#2196F3") });
        }
        icons.Children.Add(new Label 
        { 
            Text = new string('★', prompt.Rating), 
            FontSize = 7, 
            TextColor = Color.FromArgb("#FFC107") 
        });
        if (icons.Children.Count > 0) stack.Children.Add(icons);

        // Text (truncated)
        var text = prompt.Text.Length > 45 ? prompt.Text.Substring(0, 45) + "…" : prompt.Text;
        stack.Children.Add(new Label
        {
            Text = text,
            FontSize = 10,
            TextColor = prompt.IsActive ? Color.FromArgb("#333") : Color.FromArgb("#999"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        });

        // Category
        stack.Children.Add(new Label
        {
            Text = prompt.GroupName,
            FontSize = 8,
            TextColor = Color.FromArgb("#999")
        });

        frame.Content = stack;

        var tap = new TapGestureRecognizer();
        var captured = prompt;
        tap.Tapped += (s, e) => ShowDetail(captured);
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private void ShowDetail(PromptItem prompt)
    {
        _selectedPrompt = prompt;
        _detailPanel.IsVisible = true;

        _detailText.Text = prompt.Text;
        
        // Populate examples list
        RefreshExamplesList(prompt);

        var meta = $"📁 {prompt.PackName} / {prompt.GroupName}";
        meta += $"\n{new string('★', prompt.Rating)}{new string('☆', 5 - prompt.Rating)}";
        meta += prompt.IsActive ? " • Active" : " • Inactive";
        if (prompt.Priority > 0) meta += $" • Priority {prompt.Priority}";
        meta += $"\nProb: {prompt.Probability:P0}";
        _detailMeta.Text = meta;

        // Highlight probability buttons
        UpdateProbButtonHighlight(prompt.Probability);
        
        // Highlight priority buttons
        UpdatePrioButtonHighlight(prompt.Priority);
    }
    
    private void RefreshExamplesList(PromptItem prompt)
    {
        _examplesStack.Children.Clear();
        
        if (string.IsNullOrWhiteSpace(prompt.Examples))
        {
            _examplesStack.Children.Add(new Label
            {
                Text = "No examples yet",
                FontSize = 10,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }
        
        var examples = prompt.Examples.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < examples.Length; i++)
        {
            var example = examples[i].Trim();
            if (string.IsNullOrEmpty(example)) continue;
            
            int index = i;
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto)
                }
            };
            
            var label = new Label
            {
                Text = $"• {example}",
                FontSize = 10,
                TextColor = Color.FromArgb("#333"),
                LineBreakMode = LineBreakMode.WordWrap,
                VerticalOptions = LayoutOptions.Center
            };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);
            
            var deleteBtn = new Button
            {
                Text = "✕",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#999"),
                WidthRequest = 24,
                HeightRequest = 24,
                Padding = 0,
                FontSize = 10
            };
            deleteBtn.Clicked += async (s, e) => await DeleteExampleAsync(index);
            Grid.SetColumn(deleteBtn, 1);
            row.Children.Add(deleteBtn);
            
            _examplesStack.Children.Add(row);
        }
    }
    
    private async void OnAddExampleClicked(object? sender, EventArgs e)
    {
        if (_selectedPrompt == null) return;
        
        string? example = await DisplayPromptAsync("Add Example", "Enter example text:");
        if (string.IsNullOrWhiteSpace(example)) return;
        
        var currentExamples = _selectedPrompt.Examples ?? "";
        if (!string.IsNullOrEmpty(currentExamples) && !currentExamples.EndsWith("\n"))
            currentExamples += "\n";
        currentExamples += example.Trim();
        
        await _prompts.UpdatePromptAsync(_selectedPrompt.Id, p => p.Examples = currentExamples);
        _selectedPrompt.Examples = currentExamples;
        
        RefreshExamplesList(_selectedPrompt);
        await RefreshPromptsAsync();
    }
    
    private async Task DeleteExampleAsync(int index)
    {
        if (_selectedPrompt == null) return;
        
        var examples = (_selectedPrompt.Examples ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (index < 0 || index >= examples.Count) return;
        
        bool confirm = await DisplayAlert("Delete Example", $"Delete this example?\n\n\"{examples[index]}\"", "Delete", "Cancel");
        if (!confirm) return;
        
        examples.RemoveAt(index);
        var newExamples = string.Join("\n", examples);
        
        await _prompts.UpdatePromptAsync(_selectedPrompt.Id, p => p.Examples = newExamples);
        _selectedPrompt.Examples = newExamples;
        
        RefreshExamplesList(_selectedPrompt);
        await RefreshPromptsAsync();
    }

    private void UpdateProbButtonHighlight(double probability)
    {
        var probValues = new[] { 0.25, 0.50, 0.75, 1.0 };
        for (int i = 0; i < _probButtons.Count && i < probValues.Length; i++)
        {
            bool isSelected = Math.Abs(probability - probValues[i]) < 0.01;
            _probButtons[i].BackgroundColor = isSelected ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0");
            _probButtons[i].TextColor = isSelected ? Colors.White : Color.FromArgb("#333");
        }
    }

    private void UpdatePrioButtonHighlight(int priority)
    {
        var prioValues = new[] { 0, 1, 2, 3 };
        for (int i = 0; i < _prioButtons.Count && i < prioValues.Length; i++)
        {
            bool isSelected = priority == prioValues[i];
            _prioButtons[i].BackgroundColor = isSelected ? Color.FromArgb("#FF9800") : Color.FromArgb("#E0E0E0");
            _prioButtons[i].TextColor = isSelected ? Colors.White : Color.FromArgb("#333");
        }
    }

    #endregion

    #region Actions

    private async void OnAddPromptClicked(object? sender, EventArgs e)
    {
        string? text = await DisplayPromptAsync("New Prompt", "Enter prompt text:", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(text)) return;

        // Ask for category
        var catOptions = _categoryNames.Concat(new[] { "+ New Category" }).ToArray();
        string? category = await DisplayActionSheet("Category", "Cancel", null, catOptions);
        if (string.IsNullOrEmpty(category) || category == "Cancel") return;

        if (category == "+ New Category")
        {
            category = await DisplayPromptAsync("New Category", "Enter category name:");
            if (string.IsNullOrWhiteSpace(category)) return;
        }

        // Ask for probability
        var probOptions = new[] { "100% (default)", "75%", "50%", "25%", "Custom..." };
        var probChoice = await DisplayActionSheet("Probability", "Cancel", null, probOptions);
        if (probChoice == null || probChoice == "Cancel") return;

        double probability = 1.0;
        if (probChoice == "Custom...")
        {
            var customProb = await DisplayPromptAsync("Custom Probability", "Enter probability (0-100):", "Set", "Cancel", initialValue: "100", keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(customProb)) return;
            if (double.TryParse(customProb.Replace("%", ""), out double val))
            {
                probability = val > 1 ? val / 100.0 : val;
                probability = Math.Clamp(probability, 0.0, 1.0);
            }
        }
        else
        {
            probability = double.Parse(probChoice.Split('%')[0]) / 100.0;
        }

        // Ask for priority
        var prioOptions = new[] { "None (default)", "P1 (highest)", "P2", "P3", "Custom..." };
        var prioChoice = await DisplayActionSheet("Priority", "Cancel", null, prioOptions);
        if (prioChoice == null || prioChoice == "Cancel") return;

        int priority = 0;
        if (prioChoice == "Custom...")
        {
            var customPrio = await DisplayPromptAsync("Custom Priority", "Enter priority (0 = none, 1 = highest):", "Set", "Cancel", initialValue: "0", keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(customPrio)) return;
            if (int.TryParse(customPrio, out int val))
            {
                priority = Math.Max(0, val);
            }
        }
        else if (prioChoice.StartsWith("P"))
        {
            priority = int.Parse(prioChoice.Substring(1, 1));
        }

        var prompt = new PromptItem
        {
            PackName = _selectedPack,
            GroupName = category.Trim(),
            Text = text.Trim(),
            Rating = 3,
            Probability = probability,
            Priority = priority,
            IsActive = true
        };
        await _prompts.AddPromptAsync(prompt);
        await LoadCategoriesAsync();
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_selectedPrompt == null) return;
        string? t = await DisplayPromptAsync("Edit", "Update prompt:", "Save", "Cancel", initialValue: _selectedPrompt.Text);
        if (!string.IsNullOrWhiteSpace(t) && t != _selectedPrompt.Text)
        {
            await _prompts.UpdatePromptAsync(_selectedPrompt.Id, p => p.Text = t.Trim());
            _detailText.Text = t.Trim();
            await RefreshPromptsAsync();
        }
    }

    private async void OnCopyClicked(object? sender, EventArgs e)
    {
        if (_selectedPrompt == null) return;

        // Pick target pack
        var packOptions = _packNames.Concat(new[] { "+ New Pack" }).ToArray();
        var targetPack = await DisplayActionSheet("Copy to Pack", "Cancel", null, packOptions);
        if (string.IsNullOrEmpty(targetPack) || targetPack == "Cancel") return;

        if (targetPack == "+ New Pack")
        {
            targetPack = await DisplayPromptAsync("New Pack", "Enter pack name:");
            if (string.IsNullOrWhiteSpace(targetPack)) return;
        }

        // Pick target category
        var targetGroups = await _prompts.GetGroupNamesAsync(targetPack);
        var groupOptions = targetGroups.Concat(new[] { "+ New Category" }).ToArray();
        var targetGroup = await DisplayActionSheet("Category", "Cancel", null, groupOptions);
        if (string.IsNullOrEmpty(targetGroup) || targetGroup == "Cancel") return;

        if (targetGroup == "+ New Category")
        {
            targetGroup = await DisplayPromptAsync("New Category", "Enter category name:");
            if (string.IsNullOrWhiteSpace(targetGroup)) return;
        }

        await _prompts.CopyToPackAsync(_selectedPrompt.Id, targetPack.Trim(), targetGroup.Trim());
        await DisplayAlert("Copied", $"Copied to {targetPack} / {targetGroup}", "OK");
    }

    private async void OnMoveClicked(object? sender, EventArgs e)
    {
        if (_selectedPrompt == null) return;

        var options = _categoryNames.Where(c => c != _selectedPrompt.GroupName).Concat(new[] { "+ New" }).ToArray();
        var target = await DisplayActionSheet("Move to Category", "Cancel", null, options);
        if (string.IsNullOrEmpty(target) || target == "Cancel") return;

        if (target == "+ New")
        {
            target = await DisplayPromptAsync("New Category", "Enter category name:");
            if (string.IsNullOrWhiteSpace(target)) return;
        }

        await _prompts.MoveToGroupAsync(_selectedPrompt.Id, target.Trim());
        ShowDetail(_selectedPrompt);
        await LoadCategoriesAsync();
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (_selectedPrompt == null) return;
        
        await _prompts.UpdatePromptAsync(_selectedPrompt.Id, p => p.IsActive = !p.IsActive);
        _selectedPrompt.IsActive = !_selectedPrompt.IsActive;
        ShowDetail(_selectedPrompt);
        await RefreshPromptsAsync();
    }

    private async void OnArchiveClicked(object? sender, EventArgs e)
    {
        if (_selectedPrompt == null) return;

        if (_selectedPrompt.IsArchived)
        {
            await _prompts.RestorePromptAsync(_selectedPrompt.Id);
        }
        else
        {
            // Confirm before archiving
            bool confirm = await DisplayAlert(
                "Archive Prompt?",
                $"Archive this prompt?\n\n\"{(_selectedPrompt.Text.Length > 80 ? _selectedPrompt.Text.Substring(0, 80) + "..." : _selectedPrompt.Text)}\"",
                "Archive",
                "Cancel");
            
            if (!confirm) return;
            
            await _prompts.ArchivePromptAsync(_selectedPrompt.Id);
        }

        _detailPanel.IsVisible = false;
        _selectedPrompt = null;
        await RefreshPromptsAsync();
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedPack))
        {
            await DisplayAlert("No Pack", "Select a pack first.", "OK");
            return;
        }

        // Build options: current category or all
        var options = new List<string>();
        
        if (_selectedCategory != "All" && !string.IsNullOrEmpty(_selectedCategory))
        {
            options.Add($"Export '{_selectedCategory}' only");
        }
        options.Add("Export all categories");
        
        var choice = await DisplayActionSheet($"Export: {_selectedPack}", "Cancel", null, options.ToArray());
        
        if (choice == null || choice == "Cancel") return;

        string exportText;
        
        if (choice.StartsWith("Export all"))
        {
            // Get all prompts for pack, grouped by category
            var allPrompts = await _prompts.GetPackPromptsAsync(_selectedPack);
            var grouped = allPrompts
                .Where(p => !p.IsArchived)
                .GroupBy(p => p.GroupName)
                .OrderBy(g => g.Key);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"# {_selectedPack}");
            sb.AppendLine();

            foreach (var group in grouped)
            {
                sb.AppendLine($"## {group.Key}");
                sb.AppendLine();
                foreach (var prompt in group.OrderByDescending(p => p.Rating))
                {
                    sb.AppendLine($"- {prompt.Text}");
                }
                sb.AppendLine();
            }

            exportText = sb.ToString().TrimEnd();
        }
        else
        {
            // Export single category
            var prompts = _currentPrompts.Where(p => !p.IsArchived).ToList();
            
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"## {_selectedCategory}");
            sb.AppendLine();
            foreach (var prompt in prompts.OrderByDescending(p => p.Rating))
            {
                sb.AppendLine($"- {prompt.Text}");
            }

            exportText = sb.ToString().TrimEnd();
        }

        await Clipboard.SetTextAsync(exportText);
        
        int count = exportText.Split('\n').Count(l => l.StartsWith("- "));
        await DisplayAlert("Copied!", $"{count} prompts copied to clipboard.", "OK");
    }

    private async Task SetProbabilityAsync(double probability)
    {
        if (_selectedPrompt == null) return;
        
        await _prompts.UpdatePromptAsync(_selectedPrompt.Id, p => p.Probability = probability);
        _selectedPrompt.Probability = probability;
        UpdateProbButtonHighlight(probability);
        
        // Update meta display
        var meta = $"📁 {_selectedPrompt.PackName} / {_selectedPrompt.GroupName}";
        meta += $"\n{new string('★', _selectedPrompt.Rating)}{new string('☆', 5 - _selectedPrompt.Rating)}";
        meta += _selectedPrompt.IsActive ? " • Active" : " • Inactive";
        if (_selectedPrompt.Priority > 0) meta += $" • Priority {_selectedPrompt.Priority}";
        meta += $"\nProb: {_selectedPrompt.Probability:P0}";
        _detailMeta.Text = meta;
        
        await RefreshPromptsAsync();
    }

    private async Task SetPriorityAsync(int priority)
    {
        if (_selectedPrompt == null) return;
        
        await _prompts.UpdatePromptAsync(_selectedPrompt.Id, p => p.Priority = priority);
        _selectedPrompt.Priority = priority;
        UpdatePrioButtonHighlight(priority);
        
        // Update meta display
        var meta = $"📁 {_selectedPrompt.PackName} / {_selectedPrompt.GroupName}";
        meta += $"\n{new string('★', _selectedPrompt.Rating)}{new string('☆', 5 - _selectedPrompt.Rating)}";
        meta += _selectedPrompt.IsActive ? " • Active" : " • Inactive";
        if (_selectedPrompt.Priority > 0) meta += $" • Priority {_selectedPrompt.Priority}";
        meta += $"\nProb: {_selectedPrompt.Probability:P0}";
        _detailMeta.Text = meta;
        
        await RefreshPromptsAsync();
    }

    #endregion
}

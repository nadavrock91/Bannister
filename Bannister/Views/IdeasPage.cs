using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for managing ideas - compact data grid view for thousands of ideas.
/// Click on any cell to view/edit full content in detail panel.
/// </summary>
public class IdeasPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly IdeasService _ideas;
    private readonly IdeaLoggerService _ideaLogger;
    
    // UI
    private Label _headerLabel;
    private Picker _categoryPicker;
    private Entry _searchEntry;
    private Grid _dataGrid;
    private ScrollView _gridScrollView;
    private Frame _detailPanel;
    private Label _detailTitle;
    private Editor _detailContent;
    private Label _detailMeta;
    private Button _showArchivedBtn;
    private FlexLayout _ratingButtonsContainer;
    private List<Button> _ratingButtons = new();
    private Entry _customRatingEntry;
    private Label _ratingLabel;
    
    // State
    private List<string> _categories = new();
    private string _selectedCategory = "All";
    private bool _showingArchived = false;
    private IdeaItem? _selectedIdea = null;
    private List<IdeaItem> _currentIdeas = new();
    private string _searchText = "";
    
    // Sorting
    private string _sortColumn = "Date";
    private bool _sortDescending = true;
    private Button _sortDateBtn;
    private Button _sortRatingBtn;

    // Grid config
    private const int ColumnCount = 4;
    private const double CellWidth = 180;
    private const double CellHeight = 50;

    public IdeasPage(AuthService auth, IdeasService ideas, IdeaLoggerService ideaLogger)
    {
        _auth = auth;
        _ideas = ideas;
        _ideaLogger = ideaLogger;
        
        Title = "Ideas";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCategoriesAsync();
        await RefreshIdeasAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            Padding = 12,
            RowSpacing = 8,
            ColumnSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        // Header row with controls
        var headerRow = new HorizontalStackLayout { Spacing = 12 };

        // Title
        headerRow.Children.Add(new Label
        {
            Text = "💡",
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center
        });

        _headerLabel = new Label
        {
            Text = "0 ideas",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Children.Add(_headerLabel);

        // Category picker
        _categoryPicker = new Picker
        {
            Title = "Category",
            BackgroundColor = Colors.White,
            WidthRequest = 130
        };
        _categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        headerRow.Children.Add(_categoryPicker);

        // Search
        _searchEntry = new Entry
        {
            Placeholder = "🔍 Search...",
            BackgroundColor = Colors.White,
            WidthRequest = 150
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
        addBtn.Clicked += OnAddIdeaClicked;
        headerRow.Children.Add(addBtn);

        // Sort buttons
        headerRow.Children.Add(new Label
        {
            Text = "Sort:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(10, 0, 0, 0)
        });

        _sortDateBtn = new Button
        {
            Text = "📅 Date ↓",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            CornerRadius = 6,
            HeightRequest = 36,
            Padding = new Thickness(8, 0)
        };
        _sortDateBtn.Clicked += OnSortDateClicked;
        headerRow.Children.Add(_sortDateBtn);

        _sortRatingBtn = new Button
        {
            Text = "⭐ Rating",
            BackgroundColor = Color.FromArgb("#757575"),
            TextColor = Colors.White,
            CornerRadius = 6,
            HeightRequest = 36,
            Padding = new Thickness(8, 0)
        };
        _sortRatingBtn.Clicked += OnSortRatingClicked;
        headerRow.Children.Add(_sortRatingBtn);

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

        // Content area - Grid + Detail panel
        var contentGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(300))
            },
            ColumnSpacing = 12
        };

        // Data grid in scrollview
        _gridScrollView = new ScrollView
        {
            Orientation = ScrollOrientation.Both
        };

        _dataGrid = new Grid
        {
            ColumnSpacing = 1,
            RowSpacing = 1,
            BackgroundColor = Color.FromArgb("#DDD"),
            Padding = 1
        };

        _gridScrollView.Content = _dataGrid;
        Grid.SetColumn(_gridScrollView, 0);
        contentGrid.Children.Add(_gridScrollView);

        // Detail panel (right side)
        _detailPanel = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#FF9800"),
            HasShadow = true,
            IsVisible = false
        };

        var detailStack = new VerticalStackLayout { Spacing = 10 };

        // Close button
        var closeRow = new HorizontalStackLayout();
        closeRow.Children.Add(new Label
        {
            Text = "📄 Details",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FF9800"),
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
        closeBtn.Clicked += (s, e) => { _detailPanel.IsVisible = false; _selectedIdea = null; };
        closeRow.Children.Add(closeBtn);
        detailStack.Children.Add(closeRow);

        // Title
        _detailTitle = new Label
        {
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        detailStack.Children.Add(_detailTitle);

        // Meta
        _detailMeta = new Label
        {
            FontSize = 10,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        detailStack.Children.Add(_detailMeta);

        // Notes editor
        _detailContent = new Editor
        {
            Placeholder = "Notes...",
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            HeightRequest = 150,
            FontSize = 12
        };
        _detailContent.Unfocused += OnDetailContentSave;
        detailStack.Children.Add(_detailContent);

        // Rating section with buttons
        var ratingSection = new VerticalStackLayout { Spacing = 6 };
        
        var ratingHeader = new HorizontalStackLayout { Spacing = 8 };
        ratingHeader.Children.Add(new Label
        {
            Text = "Rating:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });
        _ratingLabel = new Label
        {
            Text = "50",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FF9800"),
            VerticalOptions = LayoutOptions.Center
        };
        ratingHeader.Children.Add(_ratingLabel);
        ratingSection.Children.Add(ratingHeader);

        // Rating buttons row
        _ratingButtonsContainer = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start
        };

        var ratingValues = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        foreach (var rating in ratingValues)
        {
            var btn = new Button
            {
                Text = rating.ToString(),
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                TextColor = Color.FromArgb("#333"),
                CornerRadius = 4,
                WidthRequest = 38,
                HeightRequest = 30,
                Padding = 0,
                Margin = new Thickness(0, 0, 4, 4),
                FontSize = 11
            };
            var capturedRating = rating;
            btn.Clicked += async (s, e) => await SetRatingAsync(capturedRating);
            _ratingButtons.Add(btn);
            _ratingButtonsContainer.Children.Add(btn);
        }

        // Custom rating entry
        _customRatingEntry = new Entry
        {
            Placeholder = "Custom",
            Keyboard = Keyboard.Numeric,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            WidthRequest = 55,
            HeightRequest = 30,
            FontSize = 11
        };
        _customRatingEntry.Completed += async (s, e) =>
        {
            if (int.TryParse(_customRatingEntry.Text, out int custom) && custom >= 0 && custom <= 100)
            {
                await SetRatingAsync(custom);
            }
            _customRatingEntry.Text = "";
            _customRatingEntry.Unfocus();
        };
        _ratingButtonsContainer.Children.Add(_customRatingEntry);

        ratingSection.Children.Add(_ratingButtonsContainer);
        detailStack.Children.Add(ratingSection);

        // Action buttons (compact)
        var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        
        var btnEdit = MiniBtn("✏️", "#2196F3"); btnEdit.Clicked += OnEditClicked;
        var btnStar = MiniBtn("⭐", "#FFC107"); btnStar.Clicked += OnStarClicked;
        var btnMove = MiniBtn("📂", "#9C27B0"); btnMove.Clicked += OnMoveClicked;
        var btnDone = MiniBtn("✅", "#4CAF50"); btnDone.Clicked += OnDoneClicked;
        var btnArchive = MiniBtn("🗄️", "#757575"); btnArchive.Clicked += OnArchiveClicked;
        var btnDelete = MiniBtn("🗑️", "#F44336"); btnDelete.Clicked += OnDeleteClicked;
        
        actions.Children.Add(btnEdit);
        actions.Children.Add(btnStar);
        actions.Children.Add(btnMove);
        actions.Children.Add(btnDone);
        actions.Children.Add(btnArchive);
        actions.Children.Add(btnDelete);
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

    private async Task LoadCategoriesAsync()
    {
        _categories = await _ideas.GetCategoriesAsync(_auth.CurrentUsername);
        
        _categoryPicker.Items.Clear();
        _categoryPicker.Items.Add("All");
        _categoryPicker.Items.Add("⭐ Starred");
        foreach (var cat in _categories)
            _categoryPicker.Items.Add(cat);
        
        _categoryPicker.SelectedIndex = 0;
    }

    private async void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (_categoryPicker.SelectedIndex < 0) return;
        _selectedCategory = _categoryPicker.Items[_categoryPicker.SelectedIndex] as string ?? "All";
        _showingArchived = false;
        _showArchivedBtn.BackgroundColor = Color.FromArgb("#9E9E9E");
        await RefreshIdeasAsync();
    }

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue?.ToLower() ?? "";
        await RefreshIdeasAsync();
    }

    private async void OnShowArchivedClicked(object? sender, EventArgs e)
    {
        _showingArchived = !_showingArchived;
        _showArchivedBtn.BackgroundColor = _showingArchived ? Color.FromArgb("#F57C00") : Color.FromArgb("#9E9E9E");
        await RefreshIdeasAsync();
    }

    private async Task RefreshIdeasAsync()
    {
        // Update header with stats regardless of selection
        var (total, starred, inProgress, done) = await _ideas.GetStatsAsync(_auth.CurrentUsername);
        
        // If "All" is selected, show empty state prompting category selection
        if (_selectedCategory == "All" && !_showingArchived)
        {
            _headerLabel.Text = $"{total} ideas ({starred}⭐ {done}✓)";
            _currentIdeas = new List<IdeaItem>();
            ShowCategoryPrompt();
            return;
        }
        
        List<IdeaItem> ideas;
        
        if (_showingArchived)
            ideas = await _ideas.GetArchivedIdeasAsync(_auth.CurrentUsername);
        else if (_selectedCategory == "⭐ Starred")
            ideas = await _ideas.GetStarredIdeasAsync(_auth.CurrentUsername);
        else
            ideas = await _ideas.GetIdeasByCategoryAsync(_auth.CurrentUsername, _selectedCategory);

        // Search filter
        if (!string.IsNullOrEmpty(_searchText))
        {
            ideas = ideas.Where(i =>
                i.Title.ToLower().Contains(_searchText) ||
                (i.Notes?.ToLower().Contains(_searchText) ?? false) ||
                i.Category.ToLower().Contains(_searchText)
            ).ToList();
        }

        // Apply sorting
        ideas = _sortColumn switch
        {
            "Rating" => _sortDescending 
                ? ideas.OrderByDescending(i => i.Rating).ThenByDescending(i => i.CreatedAt).ToList()
                : ideas.OrderBy(i => i.Rating).ThenByDescending(i => i.CreatedAt).ToList(),
            _ => _sortDescending
                ? ideas.OrderByDescending(i => i.CreatedAt).ToList()
                : ideas.OrderBy(i => i.CreatedAt).ToList()
        };

        _currentIdeas = ideas;

        _headerLabel.Text = _showingArchived
            ? $"{ideas.Count} archived"
            : $"{ideas.Count} in {_selectedCategory} ({total} total)";

        BuildDataGrid(ideas);
    }

    private void ShowCategoryPrompt()
    {
        _dataGrid.Children.Clear();
        _dataGrid.RowDefinitions.Clear();
        _dataGrid.ColumnDefinitions.Clear();
        _dataGrid.BackgroundColor = Colors.Transparent;

        var promptStack = new VerticalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(40)
        };

        promptStack.Children.Add(new Label
        {
            Text = "📁",
            FontSize = 48,
            HorizontalOptions = LayoutOptions.Center
        });

        promptStack.Children.Add(new Label
        {
            Text = "Select a category to view ideas",
            FontSize = 16,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center
        });

        if (_categories.Count > 0)
        {
            var categoryGrid = new FlexLayout
            {
                Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Center,
                HorizontalOptions = LayoutOptions.Center
            };

            foreach (var cat in _categories)
            {
                var catBtn = new Button
                {
                    Text = cat,
                    BackgroundColor = Color.FromArgb("#2196F3"),
                    TextColor = Colors.White,
                    CornerRadius = 6,
                    Margin = new Thickness(4),
                    Padding = new Thickness(12, 8)
                };
                var capturedCat = cat;
                catBtn.Clicked += (s, e) =>
                {
                    for (int i = 0; i < _categoryPicker.Items.Count; i++)
                    {
                        if (_categoryPicker.Items[i] as string == capturedCat)
                        {
                            _categoryPicker.SelectedIndex = i;
                            break;
                        }
                    }
                };
                categoryGrid.Children.Add(catBtn);
            }

            var starredBtn = new Button
            {
                Text = "⭐ Starred",
                BackgroundColor = Color.FromArgb("#FFC107"),
                TextColor = Colors.White,
                CornerRadius = 6,
                Margin = new Thickness(4),
                Padding = new Thickness(12, 8)
            };
            starredBtn.Clicked += (s, e) => { _categoryPicker.SelectedIndex = 1; };
            categoryGrid.Children.Add(starredBtn);

            promptStack.Children.Add(categoryGrid);
        }
        else
        {
            promptStack.Children.Add(new Label
            {
                Text = "No categories yet. Click + New to create your first idea.",
                FontSize = 12,
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center
            });
        }

        _dataGrid.Children.Add(promptStack);
    }

    private void BuildDataGrid(List<IdeaItem> ideas)
    {
        _dataGrid.Children.Clear();
        _dataGrid.RowDefinitions.Clear();
        _dataGrid.ColumnDefinitions.Clear();

        if (ideas.Count == 0)
        {
            _dataGrid.BackgroundColor = Colors.Transparent;
            _dataGrid.Children.Add(new Label
            {
                Text = "No ideas in this category.",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20)
            });
            return;
        }

        _dataGrid.BackgroundColor = Color.FromArgb("#DDD");

        int cols = ColumnCount;
        int rows = (int)Math.Ceiling((double)ideas.Count / cols);

        for (int c = 0; c < cols; c++)
            _dataGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(CellWidth)));

        for (int r = 0; r < rows; r++)
            _dataGrid.RowDefinitions.Add(new RowDefinition(new GridLength(CellHeight)));

        for (int i = 0; i < ideas.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;
            var cell = BuildCell(ideas[i]);
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, col);
            _dataGrid.Children.Add(cell);
        }
    }

    private Frame BuildCell(IdeaItem idea)
    {
        Color bg = idea.Status == 2 ? Color.FromArgb("#E8F5E9")
            : idea.IsStarred ? Color.FromArgb("#FFF8E1")
            : idea.Priority == 3 ? Color.FromArgb("#FFEBEE")
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

        // Top row: rating badge + icons
        var topRow = new HorizontalStackLayout { Spacing = 4 };
        
        // Rating badge (prominent colored box)
        var ratingBadge = new Frame
        {
            Padding = new Thickness(4, 1),
            CornerRadius = 3,
            BackgroundColor = idea.Rating >= 70 ? Color.FromArgb("#4CAF50")
                            : idea.Rating >= 40 ? Color.FromArgb("#FF9800")
                            : Color.FromArgb("#BDBDBD"),
            BorderColor = Colors.Transparent,
            HasShadow = false,
            Content = new Label
            {
                Text = idea.Rating.ToString(),
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White
            }
        };
        topRow.Children.Add(ratingBadge);
        
        // Other icons
        if (idea.IsStarred) topRow.Children.Add(new Label { Text = "⭐", FontSize = 9 });
        if (idea.Priority == 3) topRow.Children.Add(new Label { Text = "!", FontSize = 9, TextColor = Colors.Red, FontAttributes = FontAttributes.Bold });
        if (idea.Status == 2) topRow.Children.Add(new Label { Text = "✓", FontSize = 9, TextColor = Color.FromArgb("#4CAF50") });
        
        stack.Children.Add(topRow);

        // Title (truncated)
        var title = idea.Title.Length > 30 ? idea.Title.Substring(0, 30) + "…" : idea.Title;
        stack.Children.Add(new Label
        {
            Text = title,
            FontSize = 10,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        });

        // Bottom row: Category + Date
        var bottomRow = new HorizontalStackLayout { Spacing = 4 };
        bottomRow.Children.Add(new Label
        {
            Text = idea.Category,
            FontSize = 8,
            TextColor = Color.FromArgb("#999")
        });
        bottomRow.Children.Add(new Label
        {
            Text = idea.CreatedAt.ToString("MMM d"),
            FontSize = 8,
            TextColor = Color.FromArgb("#AAA")
        });
        stack.Children.Add(bottomRow);

        frame.Content = stack;

        // Click to show detail
        var tap = new TapGestureRecognizer();
        var captured = idea;
        tap.Tapped += (s, e) => ShowDetail(captured);
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private void ShowDetail(IdeaItem idea)
    {
        _selectedIdea = idea;
        _detailPanel.IsVisible = true;

        _detailTitle.Text = idea.Title;
        _detailContent.Text = idea.Notes ?? "";
        
        // Update rating buttons
        UpdateRatingButtonSelection(idea.Rating);
        _ratingLabel.Text = idea.Rating.ToString();
        UpdateRatingLabelColor(idea.Rating);

        var meta = $"📁 {idea.Category}";
        if (idea.IsStarred) meta += " • ⭐";
        if (idea.Priority > 0) meta += $" • P{idea.Priority}";
        meta += $" • {idea.StatusText}";
        meta += $"\nCreated: {idea.CreatedAt:MMM d, yyyy}";
        _detailMeta.Text = meta;
    }

    private void UpdateRatingButtonSelection(int rating)
    {
        var ratingValues = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        for (int i = 0; i < _ratingButtons.Count && i < ratingValues.Length; i++)
        {
            var btn = _ratingButtons[i];
            var btnRating = ratingValues[i];
            bool isSelected = btnRating == rating;
            
            if (isSelected)
            {
                btn.BackgroundColor = GetRatingColor(rating);
                btn.TextColor = Colors.White;
            }
            else
            {
                btn.BackgroundColor = Color.FromArgb("#E0E0E0");
                btn.TextColor = Color.FromArgb("#333");
            }
        }
    }

    private Color GetRatingColor(int rating)
    {
        return rating >= 70 ? Color.FromArgb("#4CAF50")
             : rating >= 40 ? Color.FromArgb("#FF9800")
             : Color.FromArgb("#9E9E9E");
    }

    private void UpdateRatingLabelColor(int rating)
    {
        _ratingLabel.TextColor = rating >= 70 ? Color.FromArgb("#4CAF50")
                               : rating >= 40 ? Color.FromArgb("#FF9800")
                               : Color.FromArgb("#999");
    }

    private async Task SetRatingAsync(int rating)
    {
        if (_selectedIdea == null) return;
        
        _selectedIdea.Rating = rating;
        _ratingLabel.Text = rating.ToString();
        UpdateRatingLabelColor(rating);
        UpdateRatingButtonSelection(rating);
        
        await _ideas.UpdateIdeaAsync(_selectedIdea);
        await RefreshIdeasAsync();
    }

    private async void OnDetailContentSave(object? sender, FocusEventArgs e)
    {
        if (_selectedIdea == null) return;
        _selectedIdea.Notes = _detailContent.Text;
        await _ideas.UpdateIdeaAsync(_selectedIdea);
    }

    #region Actions

    private async void OnAddIdeaClicked(object? sender, EventArgs e)
    {
        // Use the reusable idea logger
        string? suggestedCategory = (_selectedCategory == "All" || _selectedCategory == "⭐ Starred") 
            ? null 
            : _selectedCategory;

        var idea = await _ideaLogger.LogIdeaAsync(this, _auth.CurrentUsername, null, suggestedCategory);

        if (idea != null)
        {
            await LoadCategoriesAsync();
            await RefreshIdeasAsync();
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        string? t = await DisplayPromptAsync("Edit", "Update idea:", "Save", "Cancel", initialValue: _selectedIdea.Title);
        if (!string.IsNullOrWhiteSpace(t) && t != _selectedIdea.Title)
        {
            _selectedIdea.Title = t.Trim();
            await _ideas.UpdateIdeaAsync(_selectedIdea);
            _detailTitle.Text = _selectedIdea.Title;
            await RefreshIdeasAsync();
        }
    }

    private async void OnStarClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        await _ideas.ToggleStarAsync(_selectedIdea.Id);
        _selectedIdea.IsStarred = !_selectedIdea.IsStarred;
        ShowDetail(_selectedIdea);
        await RefreshIdeasAsync();
    }

    private async void OnMoveClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        var opts = _categories.Where(c => c != _selectedIdea.Category).Concat(new[] { "+ New" }).ToArray();
        var cat = await DisplayActionSheet("Move to", "Cancel", null, opts);
        if (cat == "+ New") cat = await DisplayPromptAsync("New Category", "Name:");
        if (!string.IsNullOrWhiteSpace(cat) && cat != "Cancel")
        {
            _selectedIdea.Category = cat.Trim();
            await _ideas.UpdateIdeaAsync(_selectedIdea);
            ShowDetail(_selectedIdea);
            await LoadCategoriesAsync();
            await RefreshIdeasAsync();
        }
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        if (_selectedIdea.Status == 2)
        {
            _selectedIdea.Status = 0;
            _selectedIdea.CompletedAt = null;
        }
        else
        {
            _selectedIdea.Status = 2;
            _selectedIdea.CompletedAt = DateTime.Now;
        }
        await _ideas.UpdateIdeaAsync(_selectedIdea);
        ShowDetail(_selectedIdea);
        await RefreshIdeasAsync();
    }

    private async void OnArchiveClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        if (_selectedIdea.Status == 3)
            await _ideas.RestoreIdeaAsync(_selectedIdea.Id);
        else
            await _ideas.ArchiveIdeaAsync(_selectedIdea.Id);
        _detailPanel.IsVisible = false;
        _selectedIdea = null;
        await RefreshIdeasAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        if (await DisplayAlert("Delete?", "Permanently delete?", "Delete", "Cancel"))
        {
            await _ideas.DeleteIdeaAsync(_selectedIdea.Id);
            _detailPanel.IsVisible = false;
            _selectedIdea = null;
            await RefreshIdeasAsync();
        }
    }

    private async void OnSortDateClicked(object? sender, EventArgs e)
    {
        if (_sortColumn == "Date")
            _sortDescending = !_sortDescending;
        else
        {
            _sortColumn = "Date";
            _sortDescending = true;
        }
        UpdateSortButtons();
        await RefreshIdeasAsync();
    }

    private async void OnSortRatingClicked(object? sender, EventArgs e)
    {
        if (_sortColumn == "Rating")
            _sortDescending = !_sortDescending;
        else
        {
            _sortColumn = "Rating";
            _sortDescending = true;
        }
        UpdateSortButtons();
        await RefreshIdeasAsync();
    }

    private void UpdateSortButtons()
    {
        string arrow = _sortDescending ? "↓" : "↑";
        
        if (_sortColumn == "Date")
        {
            _sortDateBtn.Text = $"📅 Date {arrow}";
            _sortDateBtn.BackgroundColor = Color.FromArgb("#2196F3");
            _sortRatingBtn.Text = "⭐ Rating";
            _sortRatingBtn.BackgroundColor = Color.FromArgb("#757575");
        }
        else
        {
            _sortDateBtn.Text = "📅 Date";
            _sortDateBtn.BackgroundColor = Color.FromArgb("#757575");
            _sortRatingBtn.Text = $"⭐ Rating {arrow}";
            _sortRatingBtn.BackgroundColor = Color.FromArgb("#FF9800");
        }
    }

    #endregion
}

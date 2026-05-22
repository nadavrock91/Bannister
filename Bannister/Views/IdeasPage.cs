using Bannister.Models;
using Bannister.Services;
using CommunityToolkit.Maui.Behaviors;
using SQLite;
using System.Globalization;

namespace Bannister.Views;

/// <summary>
/// Ideas page using DataGridView for tabular display with built-in pagination.
/// </summary>
public class IdeasPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly IdeasService _ideas;
    private readonly IdeaLoggerService _ideaLogger;
    private readonly DatabaseService _db;
    private readonly OperationQueueService _queue;
    private readonly SyncService _sync;

    // Header UI
    private Label _headerLabel;
    private Picker _categoryPicker;
    private Entry _searchEntry;
    private Button _showArchivedBtn;
    private Button _sortDateBtn;
    private Button _sortRatingBtn;
    private Label _pendingSyncLabel;
    private Button _syncQueuedIdeasBtn;

    // Grid area
    private VerticalStackLayout _toolbarContainer;
    private VerticalStackLayout _gridContainer;

    // Detail panel
    private Frame _detailPanel;
    private Label _detailTitle;
    private Editor _detailFullIdea;
    private Editor _detailContent;
    private Label _detailMeta;
    private FlexLayout _ratingButtonsContainer;
    private List<Button> _ratingButtons = new();
    private Entry _customRatingEntry;
    private Label _ratingLabel;

    // State
    private List<string> _categories = new();
    private string _selectedCategory = "";
    private bool _showingArchived = false;
    private IdeaItem? _selectedIdea = null;
    private List<IdeaItem> _currentIdeas = new();
    private string _searchText = "";
    private string _sortColumn = "Date";
    private bool _sortDescending = true;
    private DataGridView? _currentDataGrid;
    private bool _isPhoneLayout;
    private CollectionView? _phoneIdeasView;
    private Label? _phoneEmptyLabel;
    private bool _loadingCategories;

    public IdeasPage(AuthService auth, IdeasService ideas, IdeaLoggerService ideaLogger, DatabaseService db, OperationQueueService queue, SyncService sync)
    {
        _auth = auth;
        _ideas = ideas;
        _ideaLogger = ideaLogger;
        _db = db;
        _queue = queue;
        _sync = sync;
        Title = "Ideas";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        _isPhoneLayout = DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density < 600;
        if (_isPhoneLayout) BuildPhoneUI();
        else BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCategoriesAsync();
        await RefreshIdeasAsync();
        await RefreshPendingSyncCountAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            Padding = 12, RowSpacing = 8, ColumnSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),   // header
                new RowDefinition(GridLength.Auto),   // toolbar
                new RowDefinition(GridLength.Star)    // content
            }
        };

        // ====== ROW 0: Header ======
        var headerRow = new HorizontalStackLayout { Spacing = 12 };
        headerRow.Children.Add(new Label { Text = "💡", FontSize = 20, VerticalOptions = LayoutOptions.Center });
        _headerLabel = new Label { Text = "0 ideas", FontSize = 14, TextColor = Color.FromArgb("#666"), VerticalOptions = LayoutOptions.Center };
        headerRow.Children.Add(_headerLabel);

        _pendingSyncLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#F57C00"),
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        headerRow.Children.Add(_pendingSyncLabel);

        _syncQueuedIdeasBtn = new Button
        {
            Text = "Sync queued ideas",
            BackgroundColor = Color.FromArgb("#F57C00"),
            TextColor = Colors.White,
            CornerRadius = 6,
            HeightRequest = 36,
            Padding = new Thickness(10, 0),
            IsVisible = false
        };
        _syncQueuedIdeasBtn.Clicked += OnSyncQueuedIdeasClicked;
        headerRow.Children.Add(_syncQueuedIdeasBtn);

        _categoryPicker = new Picker { Title = "Category", BackgroundColor = Colors.White, WidthRequest = 130 };
        _categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        headerRow.Children.Add(_categoryPicker);

        _searchEntry = new Entry { Placeholder = "🔍 Search...", BackgroundColor = Colors.White, WidthRequest = 150 };
        _searchEntry.TextChanged += OnSearchChanged;
        headerRow.Children.Add(_searchEntry);

        var addBtn = new Button { Text = "+ New", BackgroundColor = Color.FromArgb("#4CAF50"), TextColor = Colors.White, CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(12, 0) };
        addBtn.Clicked += OnAddIdeaClicked;
        headerRow.Children.Add(addBtn);

        headerRow.Children.Add(new Label { Text = "Sort:", FontSize = 12, TextColor = Color.FromArgb("#666"), VerticalOptions = LayoutOptions.Center, Margin = new Thickness(10, 0, 0, 0) });

        _sortDateBtn = new Button { Text = "📅 Date ↓", BackgroundColor = Color.FromArgb("#2196F3"), TextColor = Colors.White, CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(8, 0) };
        _sortDateBtn.Clicked += OnSortDateClicked;
        headerRow.Children.Add(_sortDateBtn);

        _sortRatingBtn = new Button { Text = "⭐ Rating", BackgroundColor = Color.FromArgb("#757575"), TextColor = Colors.White, CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(8, 0) };
        _sortRatingBtn.Clicked += OnSortRatingClicked;
        headerRow.Children.Add(_sortRatingBtn);

        _showArchivedBtn = new Button { Text = "📦", BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, WidthRequest = 36, HeightRequest = 36 };
        _showArchivedBtn.Clicked += OnShowArchivedClicked;
        headerRow.Children.Add(_showArchivedBtn);

        var importBtn = new Button { Text = "📥 Import", BackgroundColor = Color.FromArgb("#795548"), TextColor = Colors.White, CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(8, 0) };
        importBtn.Clicked += OnImportClicked;
        headerRow.Children.Add(importBtn);

        mainGrid.Add(headerRow, 0, 0);

        // ====== ROW 1: Toolbar (fixed) ======
        _toolbarContainer = new VerticalStackLayout { Padding = new Thickness(0, 2, 0, 4) };
        mainGrid.Add(_toolbarContainer, 0, 1);

        // ====== ROW 2: Content ======
        var contentGrid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(300)) },
            ColumnSpacing = 12
        };

        var scrollView = new ScrollView { Orientation = ScrollOrientation.Both };
        _gridContainer = new VerticalStackLayout { Spacing = 4 };
        scrollView.Content = _gridContainer;
        contentGrid.Add(scrollView, 0, 0);

        BuildDetailPanel();
        contentGrid.Add(_detailPanel, 1, 0);

        mainGrid.Add(contentGrid, 0, 2);
        Content = mainGrid;
    }

    private void BuildPhoneUI()
    {
        var root = new Grid
        {
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };

        var header = new VerticalStackLayout
        {
            Padding = new Thickness(14, 12, 14, 8),
            Spacing = 10,
            BackgroundColor = Colors.White
        };

        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        _headerLabel = new Label
        {
            Text = "0 ideas",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        };
        titleRow.Add(_headerLabel, 0, 0);

        var addBtn = new Button
        {
            Text = "+ New",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42,
            Padding = new Thickness(16, 0),
            FontAttributes = FontAttributes.Bold
        };
        addBtn.Clicked += OnAddIdeaClicked;
        titleRow.Add(addBtn, 1, 0);
        header.Children.Add(titleRow);

        _pendingSyncLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#F57C00"),
            IsVisible = false
        };
        header.Children.Add(_pendingSyncLabel);

        _syncQueuedIdeasBtn = new Button
        {
            Text = "Sync queued ideas",
            BackgroundColor = Color.FromArgb("#F57C00"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42,
            FontAttributes = FontAttributes.Bold,
            IsVisible = false
        };
        _syncQueuedIdeasBtn.Clicked += OnSyncQueuedIdeasClicked;
        header.Children.Add(_syncQueuedIdeasBtn);

        _categoryPicker = new Picker
        {
            Title = "Category",
            TextColor = Color.FromArgb("#222"),
            TitleColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White
        };
        _categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        header.Children.Add(_categoryPicker);

        _searchEntry = new Entry
        {
            Placeholder = "Search ideas...",
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White,
            FontSize = 14
        };
        _searchEntry.TextChanged += OnSearchChanged;
        header.Children.Add(_searchEntry);

        var controlsRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        _sortDateBtn = new Button
        {
            Text = "Date",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        };
        _sortDateBtn.Clicked += OnSortDateClicked;
        controlsRow.Add(_sortDateBtn, 0, 0);

        _sortRatingBtn = new Button
        {
            Text = "Rating",
            BackgroundColor = Color.FromArgb("#757575"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        };
        _sortRatingBtn.Clicked += OnSortRatingClicked;
        controlsRow.Add(_sortRatingBtn, 1, 0);

        _showArchivedBtn = new Button
        {
            Text = "Archive",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            Padding = new Thickness(12, 0),
            FontSize = 13
        };
        _showArchivedBtn.Clicked += OnShowArchivedClicked;
        controlsRow.Add(_showArchivedBtn, 2, 0);
        header.Children.Add(controlsRow);

        var importBtn = new Button
        {
            Text = "Import",
            BackgroundColor = Color.FromArgb("#795548"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        };
        importBtn.Clicked += OnImportClicked;
        header.Children.Add(importBtn);

        _phoneEmptyLabel = new Label
        {
            Text = "Select a category to view ideas.",
            TextColor = Color.FromArgb("#777"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(20)
        };

        _phoneIdeasView = new CollectionView
        {
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(CreatePhoneIdeaCard),
            EmptyView = _phoneEmptyLabel
        };
        _phoneIdeasView.SelectionChanged += OnPhoneIdeaSelected;

        root.Add(header, 0, 0);
        root.Add(_phoneIdeasView, 0, 1);
        Content = root;
    }

    private View CreatePhoneIdeaCard()
    {
        var card = new Frame
        {
            Margin = new Thickness(12, 8),
            Padding = 14,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        var title = new Label
        {
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };
        title.SetBinding(Label.TextProperty, nameof(IdeaItem.Title));
        stack.Children.Add(title);

        var meta = new Label
        {
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        meta.SetBinding(Label.TextProperty, new Binding(".", converter: new PhoneIdeaMetaConverter()));
        stack.Children.Add(meta);

        var detail = new Label
        {
            FontSize = 12,
            TextColor = Color.FromArgb("#888"),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        detail.SetBinding(Label.TextProperty, new Binding(".", converter: new PhoneIdeaDetailConverter()));
        stack.Children.Add(detail);

        var touch = new TouchBehavior
        {
            LongPressDuration = 500,
            LongPressCommand = new Command<IdeaItem>(async idea => await ShowPhoneActionMenuAsync(idea))
        };
        touch.SetBinding(TouchBehavior.LongPressCommandParameterProperty, ".");
        card.Behaviors.Add(touch);

        card.Content = stack;
        return card;
    }

    private void BuildDetailPanel()
    {
        _detailPanel = new Frame
        {
            Padding = 12, CornerRadius = 8, BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#FF9800"), HasShadow = true, IsVisible = false
        };

        var detailStack = new VerticalStackLayout { Spacing = 10 };

        var closeRow = new HorizontalStackLayout();
        closeRow.Children.Add(new Label { Text = "📄 Details", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#FF9800"), HorizontalOptions = LayoutOptions.StartAndExpand, VerticalOptions = LayoutOptions.Center });
        var closeBtn = new Button { Text = "✕", BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#999"), WidthRequest = 30, HeightRequest = 30, Padding = 0 };
        closeBtn.Clicked += (s, e) => { _detailPanel.IsVisible = false; _selectedIdea = null; };
        closeRow.Children.Add(closeBtn);
        detailStack.Children.Add(closeRow);

        _detailTitle = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333"), LineBreakMode = LineBreakMode.WordWrap };
        detailStack.Children.Add(_detailTitle);

        _detailMeta = new Label { FontSize = 10, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.WordWrap };
        detailStack.Children.Add(_detailMeta);

        detailStack.Children.Add(new Label { Text = "Full Idea", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#666") });
        _detailFullIdea = new Editor { Placeholder = "Full idea...", BackgroundColor = Color.FromArgb("#FAFAFA"), HeightRequest = 150, FontSize = 12, IsReadOnly = true };
        detailStack.Children.Add(_detailFullIdea);

        detailStack.Children.Add(new Label { Text = "Notes", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#666") });
        _detailContent = new Editor { Placeholder = "Notes...", BackgroundColor = Color.FromArgb("#FAFAFA"), HeightRequest = 150, FontSize = 12 };
        _detailContent.Unfocused += OnDetailContentSave;
        detailStack.Children.Add(_detailContent);

        // Rating
        var ratingSection = new VerticalStackLayout { Spacing = 6 };
        var ratingHeader = new HorizontalStackLayout { Spacing = 8 };
        ratingHeader.Children.Add(new Label { Text = "Rating:", FontSize = 12, TextColor = Color.FromArgb("#666"), VerticalOptions = LayoutOptions.Center });
        _ratingLabel = new Label { Text = "50", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#FF9800"), VerticalOptions = LayoutOptions.Center };
        ratingHeader.Children.Add(_ratingLabel);
        ratingSection.Children.Add(ratingHeader);

        _ratingButtonsContainer = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start };
        foreach (var rating in new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })
        {
            var btn = new Button { Text = rating.ToString(), BackgroundColor = Color.FromArgb("#E0E0E0"), TextColor = Color.FromArgb("#333"), CornerRadius = 4, WidthRequest = 38, HeightRequest = 30, Padding = 0, Margin = new Thickness(0, 0, 4, 4), FontSize = 11 };
            var r = rating; btn.Clicked += async (s, e) => await SetRatingAsync(r);
            _ratingButtons.Add(btn); _ratingButtonsContainer.Children.Add(btn);
        }

        _customRatingEntry = new Entry { Placeholder = "Custom", Keyboard = Keyboard.Numeric, BackgroundColor = Color.FromArgb("#F5F5F5"), WidthRequest = 55, HeightRequest = 30, FontSize = 11 };
        _customRatingEntry.Completed += async (s, e) =>
        {
            if (int.TryParse(_customRatingEntry.Text, out int custom) && custom >= 0 && custom <= 100) await SetRatingAsync(custom);
            _customRatingEntry.Text = ""; _customRatingEntry.Unfocus();
        };
        _ratingButtonsContainer.Children.Add(_customRatingEntry);
        ratingSection.Children.Add(_ratingButtonsContainer);
        detailStack.Children.Add(ratingSection);

        // Actions
        var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap };
        var btnEdit = MiniBtn("✏️", "#2196F3"); btnEdit.Clicked += OnEditClicked;
        var btnStar = MiniBtn("⭐", "#FFC107"); btnStar.Clicked += OnStarClicked;
        var btnMove = MiniBtn("📂", "#9C27B0"); btnMove.Clicked += OnMoveClicked;
        var btnDone = MiniBtn("✅", "#4CAF50"); btnDone.Clicked += OnDoneClicked;
        var btnArchive = MiniBtn("🗄️", "#757575"); btnArchive.Clicked += OnArchiveClicked;
        var btnDelete = MiniBtn("🗑️", "#F44336"); btnDelete.Clicked += OnDeleteClicked;
        actions.Children.Add(btnEdit); actions.Children.Add(btnStar); actions.Children.Add(btnMove);
        actions.Children.Add(btnDone); actions.Children.Add(btnArchive); actions.Children.Add(btnDelete);
        detailStack.Children.Add(actions);

        _detailPanel.Content = detailStack;
    }

    private Button MiniBtn(string text, string color) => new Button
    {
        Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White,
        CornerRadius = 4, WidthRequest = 36, HeightRequest = 32, Padding = 0, Margin = new Thickness(0, 0, 4, 4)
    };

    // ===================== DATA =====================

    private async Task LoadCategoriesAsync()
    {
        _loadingCategories = true;
        var previousCategory = _selectedCategory;
        _categories = await _ideas.GetCategoriesAsync(_auth.CurrentUsername);
        _categoryPicker.Items.Clear();
        _categoryPicker.Items.Add("All");
        _categoryPicker.Items.Add("⭐ Starred");
        foreach (var cat in _categories) _categoryPicker.Items.Add(cat);
        if (!string.IsNullOrEmpty(previousCategory))
        {
            var selectedIndex = -1;
            for (var i = 0; i < _categoryPicker.Items.Count; i++)
            {
                if (string.Equals(_categoryPicker.Items[i], previousCategory, StringComparison.OrdinalIgnoreCase))
                {
                    selectedIndex = i;
                    break;
                }
            }

            _categoryPicker.SelectedIndex = selectedIndex;
            _selectedCategory = selectedIndex >= 0 ? previousCategory : "";
        }
        else
        {
            _categoryPicker.SelectedIndex = -1;
            _selectedCategory = "";
        }

        _loadingCategories = false;
    }

    private async Task RefreshIdeasAsync()
    {
        var (total, starred, inProgress, done) = await _ideas.GetStatsAsync(_auth.CurrentUsername);

        if (string.IsNullOrEmpty(_selectedCategory) && !_showingArchived)
        {
            _headerLabel.Text = $"{total} ideas ({starred}⭐ {done}✓)";
            _currentIdeas = new List<IdeaItem>();
            ShowCategoryPrompt();
            return;
        }

        List<IdeaItem> ideas;
        if (_selectedCategory == "All" && !_showingArchived)
        { _headerLabel.Text = $"{total} ideas ({starred}⭐ {done}✓)"; ideas = await _ideas.GetIdeasAsync(_auth.CurrentUsername); }
        else if (_showingArchived) ideas = await _ideas.GetArchivedIdeasAsync(_auth.CurrentUsername);
        else if (_selectedCategory == "⭐ Starred") ideas = await _ideas.GetStarredIdeasAsync(_auth.CurrentUsername);
        else ideas = await _ideas.GetIdeasByCategoryAsync(_auth.CurrentUsername, _selectedCategory);

        if (!string.IsNullOrEmpty(_searchText))
            ideas = ideas.Where(i => i.Title.ToLower().Contains(_searchText) || (i.FullIdea ?? "").ToLower().Contains(_searchText) || (i.Notes?.ToLower().Contains(_searchText) ?? false) || i.Category.ToLower().Contains(_searchText)).ToList();

        ideas = _sortColumn switch
        {
            "Rating" => _sortDescending ? ideas.OrderByDescending(i => i.Rating).ThenByDescending(i => i.CreatedAt).ToList() : ideas.OrderBy(i => i.Rating).ThenByDescending(i => i.CreatedAt).ToList(),
            _ => _sortDescending ? ideas.OrderByDescending(i => i.CreatedAt).ToList() : ideas.OrderBy(i => i.CreatedAt).ToList()
        };

        _currentIdeas = ideas;
        if (_selectedCategory != "All")
            _headerLabel.Text = _showingArchived ? $"{ideas.Count} archived" : $"{ideas.Count} in {_selectedCategory} ({total} total)";

        if (_isPhoneLayout)
        {
            BuildPhoneIdeasList(ideas);
            return;
        }

        BuildDataGrid(ideas);
    }

    private void ShowCategoryPrompt()
    {
        if (_isPhoneLayout)
        {
            if (_phoneEmptyLabel != null)
                _phoneEmptyLabel.Text = "Select a category to view ideas.";
            _phoneIdeasView!.ItemsSource = Array.Empty<IdeaItem>();
            return;
        }

        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();

        var promptStack = new VerticalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(40) };
        promptStack.Children.Add(new Label { Text = "📁", FontSize = 48, HorizontalOptions = LayoutOptions.Center });
        promptStack.Children.Add(new Label { Text = "Select a category from the dropdown above", FontSize = 14, TextColor = Color.FromArgb("#666"), HorizontalOptions = LayoutOptions.Center });
        _gridContainer.Children.Add(promptStack);
    }

    private void BuildPhoneIdeasList(List<IdeaItem> ideas)
    {
        if (_phoneIdeasView == null) return;
        if (_phoneEmptyLabel != null)
            _phoneEmptyLabel.Text = "No ideas in this category.";
        _phoneIdeasView.ItemsSource = ideas;
        _phoneIdeasView.SelectedItem = null;
    }

    private async Task RefreshPendingSyncCountAsync()
    {
        var count = await _queue.GetPendingCountAsync();
        _pendingSyncLabel.Text = $"{count} {(count == 1 ? "idea" : "ideas")} pending sync";
        _pendingSyncLabel.IsVisible = count > 0;
        _syncQueuedIdeasBtn.IsVisible = _db.IsReadOnly && count > 0;
        _syncQueuedIdeasBtn.IsEnabled = _syncQueuedIdeasBtn.IsVisible;
    }

    private async void OnSyncQueuedIdeasClicked(object? sender, EventArgs e)
    {
        _syncQueuedIdeasBtn.IsEnabled = false;
        _pendingSyncLabel.Text = "Syncing...";
        _pendingSyncLabel.IsVisible = true;

        try
        {
            var result = await _sync.UploadQueueAsync();
            await DisplayAlert("Sync", result.Message, "OK");
        }
        finally
        {
            await RefreshPendingSyncCountAsync();
        }
    }

    private void BuildDataGrid(List<IdeaItem> ideas)
    {
        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();

        if (ideas.Count == 0)
        {
            _gridContainer.Children.Add(new Label { Text = "No ideas in this category.", TextColor = Color.FromArgb("#999"), HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(20) });
            return;
        }

        var headers = new List<string> { "Id", "Rating", "Title", "Full Idea", "Category", "Subcategory", "Status", "Starred", "Created" };
        var displayRows = new List<List<string>>();
        var fullRows = new List<List<string>>();

        foreach (var idea in ideas)
        {
            displayRows.Add(new List<string> { idea.Id.ToString(), idea.Rating.ToString(), idea.Title.Length > 50 ? idea.Title.Substring(0, 47) + "..." : idea.Title, idea.Category, idea.Subcategory ?? "", idea.StatusText, idea.IsStarred ? "⭐" : "", idea.CreatedAt.ToString("MMM d, yyyy") });
            fullRows.Add(new List<string> { idea.Id.ToString(), idea.Rating.ToString(), idea.Title, idea.Category, idea.Subcategory ?? "", idea.StatusText, idea.IsStarred ? "⭐" : "", idea.CreatedAt.ToString("MMM d, yyyy HH:mm") });
        }

        for (int i = 0; i < ideas.Count; i++)
        {
            string fullIdea = ideas[i].FullIdea ?? "";
            displayRows[i].Insert(3, fullIdea.Length > 80 ? fullIdea.Substring(0, 77) + "..." : fullIdea);
            fullRows[i].Insert(3, fullIdea);
        }

        var dataGrid = DataGridView.Create(headers, displayRows)
            .WithHeaderStyle(Color.FromArgb("#FF9800"), Colors.White)
            .WithAlternateRowColor(Color.FromArgb("#FFF8E1"))
            .WithColumnWidths(40, 250)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithFullRows(fullRows)
            .WithIdColumn("Id")
            .WithPageSize(100)
            .WithUpdateCallback(async (idValue, columnName, newValue) => await UpdateIdeaFieldAsync(idValue, columnName, newValue))
            .OnCellTapped(OnGridCellTapped)
            .Build();

        _currentDataGrid = dataGrid;
        _toolbarContainer.Children.Add(dataGrid.ToolbarView);
        _gridContainer.Children.Add(dataGrid.GridView);
    }

    // ===================== GRID EVENTS =====================

    private void OnGridCellTapped(object? sender, CellTappedEventArgs e)
    {
        if (e.RowIndex < 0 || e.RowIndex >= _currentIdeas.Count) return;
        ShowDetail(_currentIdeas[e.RowIndex]);
    }

    private void OnPhoneIdeaSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not IdeaItem idea) return;
        if (_phoneIdeasView != null) _phoneIdeasView.SelectedItem = null;
        ShowDetail(idea);
    }

    private async Task<bool> UpdateIdeaFieldAsync(string idValue, string columnName, string newValue)
    {
        if (!int.TryParse(idValue, out int id)) return false;
        var idea = _currentIdeas.FirstOrDefault(i => i.Id == id);
        if (idea == null) return false;

        switch (columnName)
        {
            case "Title": idea.Title = newValue; break;
            case "Full Idea": idea.FullIdea = newValue; break;
            case "Category": idea.Category = newValue; break;
            case "Subcategory": idea.Subcategory = string.IsNullOrWhiteSpace(newValue) ? null : newValue; break;
            case "Rating":
                if (int.TryParse(newValue, out int rating)) idea.Rating = Math.Clamp(rating, 0, 100);
                else return false;
                break;
            case "Starred": idea.IsStarred = newValue == "⭐"; break;
            default: return false;
        }

        await _ideas.UpdateIdeaAsync(idea);
        if (_selectedIdea?.Id == idea.Id) ShowDetail(idea);
        return true;
    }

    // ===================== DETAIL PANEL =====================

    private void ShowDetail(IdeaItem idea)
    {
        if (_isPhoneLayout)
        {
            _ = ShowPhoneDetailModalAsync(idea);
            return;
        }

        _selectedIdea = idea;
        _detailPanel.IsVisible = true;
        _detailTitle.Text = idea.Title;
        _detailFullIdea.Text = idea.FullIdea ?? "";
        _detailContent.Text = idea.Notes ?? "";
        UpdateRatingButtonSelection(idea.Rating);
        _ratingLabel.Text = idea.Rating.ToString();
        UpdateRatingLabelColor(idea.Rating);

        var meta = $"📁 {idea.Category}";
        if (idea.IsStarred) meta += " • ⭐";
        if (idea.Priority > 0) meta += $" • P{idea.Priority}";
        meta += $" • {idea.StatusText}\nCreated: {idea.CreatedAt:MMM d, yyyy}";
        _detailMeta.Text = meta;
    }

    private async Task ShowPhoneDetailModalAsync(IdeaItem idea)
    {
        _selectedIdea = idea;

        var notes = new Editor
        {
            Text = idea.Notes ?? "",
            Placeholder = "Notes...",
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            BackgroundColor = Colors.White,
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 160
        };

        var page = new ContentPage
        {
            Title = "Idea Details",
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };

        var stack = new VerticalStackLayout
        {
            Padding = 18,
            Spacing = 14
        };

        stack.Children.Add(new Label
        {
            Text = idea.Title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        stack.Children.Add(new Label
        {
            Text = $"{idea.Category}  |  Rating {idea.Rating}  |  {idea.CreatedAt:MMM d, yyyy}",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        if (!string.IsNullOrWhiteSpace(idea.FullIdea))
        {
            stack.Children.Add(new Label
            {
                Text = "Full Idea",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#666")
            });

            stack.Children.Add(new Editor
            {
                Text = idea.FullIdea,
                TextColor = Color.FromArgb("#222"),
                BackgroundColor = Colors.White,
                IsReadOnly = true,
                AutoSize = EditorAutoSizeOption.TextChanges,
                MinimumHeightRequest = 160
            });
        }

        stack.Children.Add(new Label
        {
            Text = "Notes",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#666")
        });

        stack.Children.Add(notes);

        var actionGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        var editBtn = PhoneActionButton("Edit", "#2196F3");
        editBtn.Clicked += async (_, _) =>
        {
            string? t = await DisplayPromptAsync("Edit", "Update idea:", "Save", "Cancel", initialValue: idea.Title);
            if (!string.IsNullOrWhiteSpace(t) && t != idea.Title)
            {
                idea.Title = t.Trim();
                await _ideas.UpdateIdeaAsync(idea);
                await RefreshIdeasAsync();
                await page.Navigation.PopModalAsync();
            }
        };
        actionGrid.Add(editBtn, 0, 0);

        var starBtn = PhoneActionButton(idea.IsStarred ? "Unstar" : "Star", "#FFC107");
        starBtn.Clicked += async (_, _) =>
        {
            await _ideas.ToggleStarAsync(idea.Id);
            idea.IsStarred = !idea.IsStarred;
            await RefreshIdeasAsync();
            await page.Navigation.PopModalAsync();
        };
        actionGrid.Add(starBtn, 1, 0);

        var moveBtn = PhoneActionButton("Move", "#9C27B0");
        moveBtn.Clicked += async (_, _) =>
        {
            var opts = _categories.Where(c => c != idea.Category).Concat(new[] { "+ New" }).ToArray();
            var cat = await DisplayActionSheet("Move to", "Cancel", null, opts);
            if (cat == "+ New") cat = await DisplayPromptAsync("New Category", "Name:");
            if (!string.IsNullOrWhiteSpace(cat) && cat != "Cancel")
            {
                idea.Category = cat.Trim();
                await _ideas.UpdateIdeaAsync(idea);
                await LoadCategoriesAsync();
                await RefreshIdeasAsync();
                await page.Navigation.PopModalAsync();
            }
        };
        actionGrid.Add(moveBtn, 0, 1);

        var doneBtn = PhoneActionButton(idea.Status == 2 ? "Reopen" : "Done", "#4CAF50");
        doneBtn.Clicked += async (_, _) =>
        {
            if (idea.Status == 2) { idea.Status = 0; idea.CompletedAt = null; }
            else { idea.Status = 2; idea.CompletedAt = DateTime.Now; }
            await _ideas.UpdateIdeaAsync(idea);
            await RefreshIdeasAsync();
            await page.Navigation.PopModalAsync();
        };
        actionGrid.Add(doneBtn, 1, 1);

        var archiveBtn = PhoneActionButton(idea.Status == 3 ? "Restore" : "Archive", "#757575");
        archiveBtn.Clicked += async (_, _) =>
        {
            if (idea.Status == 3) await _ideas.RestoreIdeaAsync(idea.Id);
            else await _ideas.ArchiveIdeaAsync(idea.Id);
            await RefreshIdeasAsync();
            await page.Navigation.PopModalAsync();
        };
        actionGrid.Add(archiveBtn, 0, 2);

        var deleteBtn = PhoneActionButton("Delete", "#F44336");
        deleteBtn.Clicked += async (_, _) =>
        {
            if (await DisplayAlert("Delete?", "Permanently delete?", "Delete", "Cancel"))
            {
                await _ideas.DeleteIdeaAsync(idea.Id);
                await RefreshIdeasAsync();
                await page.Navigation.PopModalAsync();
            }
        };
        actionGrid.Add(deleteBtn, 1, 2);

        stack.Children.Add(actionGrid);

        var saveCloseBtn = new Button
        {
            Text = "Save & Close",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        saveCloseBtn.Clicked += async (_, _) =>
        {
            if (_selectedIdea != null)
            {
                _selectedIdea.Notes = notes.Text;
                await _ideas.UpdateIdeaAsync(_selectedIdea);
                await RefreshIdeasAsync();
            }
            await page.Navigation.PopModalAsync();
        };
        stack.Children.Add(saveCloseBtn);

        var closeBtn = new Button
        {
            Text = "Close",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#666")
        };
        closeBtn.Clicked += async (_, _) => await page.Navigation.PopModalAsync();
        stack.Children.Add(closeBtn);

        page.Content = new ScrollView { Content = stack };
        await Navigation.PushModalAsync(page);
    }

    private Button PhoneActionButton(string text, string color) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb(color),
        TextColor = Colors.White,
        CornerRadius = 8,
        HeightRequest = 42,
        FontSize = 13
    };

    private async Task ShowPhoneActionMenuAsync(IdeaItem? idea)
    {
        if (idea == null) return;
        _selectedIdea = idea;

        var choice = await DisplayActionSheet("Idea Actions", "Cancel", null, "Edit", idea.IsStarred ? "Unstar" : "Star", "Delete");
        switch (choice)
        {
            case "Edit":
                string? t = await DisplayPromptAsync("Edit", "Update idea:", "Save", "Cancel", initialValue: idea.Title);
                if (!string.IsNullOrWhiteSpace(t) && t != idea.Title)
                {
                    idea.Title = t.Trim();
                    await _ideas.UpdateIdeaAsync(idea);
                    await RefreshIdeasAsync();
                }
                break;
            case "Star":
            case "Unstar":
                await _ideas.ToggleStarAsync(idea.Id);
                idea.IsStarred = !idea.IsStarred;
                await RefreshIdeasAsync();
                break;
            case "Delete":
                if (await DisplayAlert("Delete?", "Permanently delete?", "Delete", "Cancel"))
                {
                    await _ideas.DeleteIdeaAsync(idea.Id);
                    await RefreshIdeasAsync();
                }
                break;
        }
    }

    private void UpdateRatingButtonSelection(int rating)
    {
        var vals = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        for (int i = 0; i < _ratingButtons.Count && i < vals.Length; i++)
        {
            bool sel = vals[i] == rating;
            _ratingButtons[i].BackgroundColor = sel ? GetRatingColor(rating) : Color.FromArgb("#E0E0E0");
            _ratingButtons[i].TextColor = sel ? Colors.White : Color.FromArgb("#333");
        }
    }

    private Color GetRatingColor(int r) => r >= 70 ? Color.FromArgb("#4CAF50") : r >= 40 ? Color.FromArgb("#FF9800") : Color.FromArgb("#9E9E9E");
    private void UpdateRatingLabelColor(int r) => _ratingLabel.TextColor = r >= 70 ? Color.FromArgb("#4CAF50") : r >= 40 ? Color.FromArgb("#FF9800") : Color.FromArgb("#999");

    private async Task SetRatingAsync(int rating)
    {
        if (_selectedIdea == null) return;
        _selectedIdea.Rating = rating;
        _ratingLabel.Text = rating.ToString();
        UpdateRatingLabelColor(rating); UpdateRatingButtonSelection(rating);
        await _ideas.UpdateIdeaAsync(_selectedIdea);
        await RefreshIdeasAsync();
    }

    private async void OnDetailContentSave(object? sender, FocusEventArgs e)
    {
        if (_selectedIdea == null) return;
        _selectedIdea.Notes = _detailContent.Text;
        await _ideas.UpdateIdeaAsync(_selectedIdea);
    }

    // ===================== HEADER EVENTS =====================

    private async void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (_loadingCategories) return;
        if (_categoryPicker.SelectedIndex < 0) return;
        _selectedCategory = _categoryPicker.Items[_categoryPicker.SelectedIndex] as string ?? "All";
        _showingArchived = false; _showArchivedBtn.BackgroundColor = Color.FromArgb("#9E9E9E");
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

    #region Actions

    private async void OnAddIdeaClicked(object? sender, EventArgs e)
    {
        string? cat = (_selectedCategory == "All" || _selectedCategory == "⭐ Starred" || string.IsNullOrEmpty(_selectedCategory)) ? null : _selectedCategory;
        var idea = await _ideaLogger.LogIdeaAsync(this, _auth.CurrentUsername, null, cat);
        if (idea != null) { await LoadCategoriesAsync(); await RefreshIdeasAsync(); await RefreshPendingSyncCountAsync(); }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        string? t = await DisplayPromptAsync("Edit", "Update idea:", "Save", "Cancel", initialValue: _selectedIdea.Title);
        if (!string.IsNullOrWhiteSpace(t) && t != _selectedIdea.Title)
        { _selectedIdea.Title = t.Trim(); await _ideas.UpdateIdeaAsync(_selectedIdea); _detailTitle.Text = _selectedIdea.Title; await RefreshIdeasAsync(); }
    }

    private async void OnStarClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        await _ideas.ToggleStarAsync(_selectedIdea.Id); _selectedIdea.IsStarred = !_selectedIdea.IsStarred;
        ShowDetail(_selectedIdea); await RefreshIdeasAsync();
    }

    private async void OnMoveClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        var opts = _categories.Where(c => c != _selectedIdea.Category).Concat(new[] { "+ New" }).ToArray();
        var cat = await DisplayActionSheet("Move to", "Cancel", null, opts);
        if (cat == "+ New") cat = await DisplayPromptAsync("New Category", "Name:");
        if (!string.IsNullOrWhiteSpace(cat) && cat != "Cancel")
        { _selectedIdea.Category = cat.Trim(); await _ideas.UpdateIdeaAsync(_selectedIdea); ShowDetail(_selectedIdea); await LoadCategoriesAsync(); await RefreshIdeasAsync(); }
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        if (_selectedIdea.Status == 2) { _selectedIdea.Status = 0; _selectedIdea.CompletedAt = null; }
        else { _selectedIdea.Status = 2; _selectedIdea.CompletedAt = DateTime.Now; }
        await _ideas.UpdateIdeaAsync(_selectedIdea); ShowDetail(_selectedIdea); await RefreshIdeasAsync();
    }

    private async void OnArchiveClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        if (_selectedIdea.Status == 3) await _ideas.RestoreIdeaAsync(_selectedIdea.Id);
        else await _ideas.ArchiveIdeaAsync(_selectedIdea.Id);
        _detailPanel.IsVisible = false; _selectedIdea = null; await RefreshIdeasAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        if (await DisplayAlert("Delete?", "Permanently delete?", "Delete", "Cancel"))
        { await _ideas.DeleteIdeaAsync(_selectedIdea.Id); _detailPanel.IsVisible = false; _selectedIdea = null; await RefreshIdeasAsync(); }
    }

    private async void OnSortDateClicked(object? sender, EventArgs e)
    {
        if (_sortColumn == "Date") _sortDescending = !_sortDescending; else { _sortColumn = "Date"; _sortDescending = true; }
        UpdateSortButtons(); await RefreshIdeasAsync();
    }

    private async void OnSortRatingClicked(object? sender, EventArgs e)
    {
        if (_sortColumn == "Rating") _sortDescending = !_sortDescending; else { _sortColumn = "Rating"; _sortDescending = true; }
        UpdateSortButtons(); await RefreshIdeasAsync();
    }

    private void UpdateSortButtons()
    {
        string a = _sortDescending ? "↓" : "↑";
        if (_sortColumn == "Date") { _sortDateBtn.Text = $"📅 Date {a}"; _sortDateBtn.BackgroundColor = Color.FromArgb("#2196F3"); _sortRatingBtn.Text = "⭐ Rating"; _sortRatingBtn.BackgroundColor = Color.FromArgb("#757575"); }
        else { _sortDateBtn.Text = "📅 Date"; _sortDateBtn.BackgroundColor = Color.FromArgb("#757575"); _sortRatingBtn.Text = $"⭐ Rating {a}"; _sortRatingBtn.BackgroundColor = Color.FromArgb("#FF9800"); }
    }

    // ===================== IMPORT =====================

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        SQLiteAsyncConnection? importConn = null;

        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a .db database file",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".db", ".sqlite", ".sqlite3" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream", "application/x-sqlite3" } },
                    { DevicePlatform.iOS, new[] { "public.database" } }
                })
            });
            if (result == null) return;

            string filePath = result.FullPath;
            if (!System.IO.File.Exists(filePath)) { await DisplayAlert("Error", "File not found.", "OK"); return; }

            importConn = new SQLiteAsyncConnection(filePath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, storeDateTimeAsTicks: false);

            var tables = await importConn.QueryAsync<ImportTableInfo>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
            if (tables.Count == 0) { await DisplayAlert("Empty", "No tables found.", "OK"); return; }

            var selectedTable = await DisplayActionSheet("Select table", "Cancel", null, tables.Select(t => t.Name).ToArray());
            if (string.IsNullOrEmpty(selectedTable) || selectedTable == "Cancel") return;

            var columns = await importConn.QueryAsync<ImportColumnInfo>($"PRAGMA table_info([{selectedTable}])");
            var colNames = columns.Select(c => c.Name).ToArray();
            if (colNames.Length == 0) { await DisplayAlert("Error", "No columns found.", "OK"); return; }

            var textColumn = await DisplayActionSheet("Which column holds the idea text?", "Cancel", null, colNames);
            if (string.IsNullOrEmpty(textColumn) || textColumn == "Cancel") return;

            var catOptions = _categories.Concat(new[] { "+ New Category" }).ToArray();
            var targetCategory = await DisplayActionSheet("Import into which category?", "Cancel", null, catOptions);
            if (string.IsNullOrEmpty(targetCategory) || targetCategory == "Cancel") return;
            if (targetCategory == "+ New Category")
            { targetCategory = await DisplayPromptAsync("New Category", "Enter category name:"); if (string.IsNullOrWhiteSpace(targetCategory)) return; targetCategory = targetCategory.Trim(); }

            var dateOptions = new[] { "(No date column)" }.Concat(colNames).ToArray();
            var dateColumn = await DisplayActionSheet("Date column? (optional)", "Cancel", null, dateOptions);
            if (dateColumn == "Cancel") return;
            if (dateColumn == "(No date column)") dateColumn = null;

            var ratingOptions = new[] { "(No rating column)" }.Concat(colNames).ToArray();
            var ratingColumn = await DisplayActionSheet("Rating column? (optional)", "Cancel", null, ratingOptions);
            if (ratingColumn == "Cancel") return;
            if (ratingColumn == "(No rating column)") ratingColumn = null;

            // Subcategory column (optional)
            var subOptions = new[] { "(No subcategory column)" }.Concat(colNames).ToArray();
            var subColumn = await DisplayActionSheet("Subcategory column? (optional)", "Cancel", null, subOptions);
            if (subColumn == "Cancel") return;
            if (subColumn == "(No subcategory column)") subColumn = null;

            var dupChoice = await DisplayActionSheet("Handle duplicates?", "Cancel", null, "Skip duplicates (recommended)", "Import all including duplicates");
            if (string.IsNullOrEmpty(dupChoice) || dupChoice == "Cancel") return;
            bool skipDuplicates = dupChoice.StartsWith("Skip");

            var rowCount = await importConn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [{selectedTable}]");
            if (rowCount == 0) { await DisplayAlert("Empty", "No rows found.", "OK"); return; }

            HashSet<string> existingTitles = new(StringComparer.OrdinalIgnoreCase);
            if (skipDuplicates) foreach (var ex in await _ideas.GetIdeasAsync(_auth.CurrentUsername)) existingTitles.Add(ex.Title.Trim());

            string summary = $"Import {rowCount} rows from [{selectedTable}]\nText: {textColumn}\nCategory: {targetCategory}\nDate: {dateColumn ?? "(none)"}\nRating: {ratingColumn ?? "(none)"}\nSubcategory: {subColumn ?? "(none)"}\nDuplicates: {(skipDuplicates ? "skip" : "allow")}";
            if (!await DisplayAlert("Confirm Import", summary, "Import", "Cancel")) return;

            var selectCols = new List<string> { $"[{textColumn}]" };
            if (dateColumn != null) selectCols.Add($"[{dateColumn}]");
            if (ratingColumn != null) selectCols.Add($"[{ratingColumn}]");
            if (subColumn != null) selectCols.Add($"[{subColumn}]");

            const string SEP = "║";
            var castCols = selectCols.Select(c => $"COALESCE(CAST({c} AS TEXT), '')").ToList();
            var rows = await importConn.QueryAsync<ImportRowResult>($"SELECT {string.Join($" || '{SEP}' || ", castCols)} AS RowData FROM [{selectedTable}]");

            // Phase 1: Parse all rows in memory (fast)
            var itemsToInsert = new List<(string text, DateTime createdAt, int rating, string? subcategory)>();
            int skipped = 0, duplicates = 0;
            string username = _auth.CurrentUsername;

            foreach (var row in rows)
            {
                if (row?.RowData == null) { skipped++; continue; }
                var parts = row.RowData.Split(SEP);
                string text = parts.Length > 0 ? parts[0].Trim() : "";
                if (string.IsNullOrWhiteSpace(text)) { skipped++; continue; }
                if (skipDuplicates && existingTitles.Contains(text)) { duplicates++; continue; }

                DateTime createdAt = DateTime.Now;
                if (dateColumn != null && parts.Length > 1 && !string.IsNullOrWhiteSpace(parts[1]))
                {
                    string[] formats = new[] { "MM/dd/yyyy", "M/d/yyyy", "MM-dd-yyyy", "M-d-yyyy", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy HH:mm", "MM/dd/yyyy HH:mm:ss", "dd/MM/yyyy HH:mm", "dd/MM/yyyy HH:mm:ss", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-ddTHH:mm:ss", "M/d/yyyy h:mm:ss tt", "dd MMM yyyy", "d MMM yyyy", "MMM d, yyyy" };
                    if (DateTime.TryParseExact(parts[1].Trim(), formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime parsed)) createdAt = parsed;
                    else if (DateTime.TryParse(parts[1].Trim(), out DateTime fallback)) createdAt = fallback;
                }

                int rating = 50;
                if (ratingColumn != null) { int ri = dateColumn != null ? 2 : 1; if (parts.Length > ri && int.TryParse(parts[ri], out int pr)) rating = Math.Clamp(pr, 0, 100); }

                string? subcategory = null;
                if (subColumn != null)
                {
                    int si = 1;
                    if (dateColumn != null) si++;
                    if (ratingColumn != null) si++;
                    if (parts.Length > si && !string.IsNullOrWhiteSpace(parts[si]))
                        subcategory = parts[si].Trim();
                }

                itemsToInsert.Add((text, createdAt, rating, subcategory));
                if (skipDuplicates) existingTitles.Add(text);
            }

            if (itemsToInsert.Count == 0)
            {
                await DisplayAlert("Nothing to Import", $"Skipped: {skipped}\nDuplicates: {duplicates}", "OK");
                return;
            }

            // Phase 2: Batch insert with progress
            var originalContent = this.Content;
            var progressWrapper = new Grid();
            this.Content = progressWrapper;
            progressWrapper.Children.Add(originalContent);

            var overlay = new Grid { BackgroundColor = Color.FromArgb("#CC000000"), HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill };
            var pStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Spacing = 12 };
            pStack.Children.Add(new ActivityIndicator { IsRunning = true, Color = Colors.White, HeightRequest = 40, WidthRequest = 40 });
            var pLabel = new Label { Text = "Importing ideas...", FontSize = 16, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center };
            var pDetail = new Label { Text = $"0 / {itemsToInsert.Count}", FontSize = 13, TextColor = Color.FromArgb("#BBDEFB"), HorizontalTextAlignment = TextAlignment.Center };
            pStack.Children.Add(pLabel);
            pStack.Children.Add(pDetail);
            overlay.Children.Add(pStack);
            progressWrapper.Children.Add(overlay);

            int imported = 0;
            int batchSize = 50;
            var conn = await _db.GetConnectionAsync();

            for (int i = 0; i < itemsToInsert.Count; i += batchSize)
            {
                var batch = itemsToInsert.Skip(i).Take(batchSize).ToList();
                await conn.RunInTransactionAsync(db =>
                {
                    foreach (var (text, createdAt, rating, subcategory) in batch)
                    {
                        var idea = new IdeaItem
                        {
                            Username = username,
                            Title = text,
                            Category = targetCategory,
                            Rating = rating,
                            CreatedAt = createdAt,
                            Subcategory = subcategory
                        };
                        db.Insert(idea);
                    }
                });
                imported += batch.Count;
                pDetail.Text = $"{imported} / {itemsToInsert.Count}";
                await Task.Yield();
            }

            // Remove overlay
            progressWrapper.Children.Remove(overlay);
            progressWrapper.Children.Remove(originalContent);
            this.Content = originalContent;

            await DisplayAlert("Import Complete", $"Imported: {imported}\nSkipped (empty): {skipped}\nDuplicates skipped: {duplicates}\nCategory: {targetCategory}", "OK");
            await LoadCategoriesAsync();
            for (int i = 0; i < _categoryPicker.Items.Count; i++)
                if (_categoryPicker.Items[i] as string == targetCategory) { _categoryPicker.SelectedIndex = i; break; }
        }
        catch (Exception ex) { await DisplayAlert("Import Error", ex.Message, "OK"); }
        finally { if (importConn != null) try { await importConn.CloseAsync(); } catch { } }
    }

    private class ImportTableInfo { public string Name { get; set; } = ""; }
    private class ImportColumnInfo { public string Name { get; set; } = ""; public string Type { get; set; } = ""; }
    private class ImportRowResult { public string RowData { get; set; } = ""; }

    private class PhoneIdeaMetaConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not IdeaItem idea) return "";
            return $"{idea.Category}  |  Rating {idea.Rating}  |  {idea.CreatedAt:MMM d, yyyy}";
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    private class PhoneIdeaDetailConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not IdeaItem idea) return "";
            var parts = new List<string>();
            if (idea.IsStarred) parts.Add("Starred");
            if (!string.IsNullOrWhiteSpace(idea.StatusText)) parts.Add(idea.StatusText);
            if (!string.IsNullOrWhiteSpace(idea.Subcategory)) parts.Add(idea.Subcategory);
            return string.Join("  |  ", parts);
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    #endregion
}

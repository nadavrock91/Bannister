using Bannister.Models;
using Bannister.Services;
using CommunityToolkit.Maui.Behaviors;
using SQLite;
using System.Globalization;

namespace Bannister.Views;

/// <summary>
/// Ideas page using DataGridView for tabular display with built-in pagination.
/// </summary>
public class IdeasPage : Bannister.Views.ModeThemedPage
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
    private Picker _normalCategoryPicker = null!;
    private Picker _llmCategoryPicker = null!;
    private Button _allButton = null!;
    private Button _showArchivedBtn;
    private Button _sortDateBtn;
    private Button _sortRatingBtn;
    private Label _pendingSyncLabel;
    private Button _syncQueuedIdeasBtn;
    private ActivityIndicator _loadingIndicator = null!;
    private Label _loadingLabel = null!;
    private Label _emptyStateLabel = null!;
    private View? _loadingStateView;

    // Grid area
    private VerticalStackLayout _toolbarContainer;
    private VerticalStackLayout _gridContainer;
    private View? _ideasContentView;

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
    private bool _isLoadingFilters;
    private bool _hasLoadedIdeas;
    private (int total, int starred, int inProgress, int done)? _cachedStats;
    private bool _statsCacheDirty = true;

    public IdeasPage(AuthService auth, IdeasService ideas, IdeaLoggerService ideaLogger, DatabaseService db, OperationQueueService queue, SyncService sync)
    {
        _auth = auth;
        _ideas = ideas;
        _ideaLogger = ideaLogger;
        _db = db;
        _queue = queue;
        _sync = sync;
        Title = "Ideas";

        _isPhoneLayout = DeviceInfo.Idiom == DeviceIdiom.Phone || DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density < 600;
        if (_isPhoneLayout) BuildPhoneUI();
        else BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadCategoriesAsync();
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
                new RowDefinition(GridLength.Auto),   // category tabs
                new RowDefinition(GridLength.Auto),   // toolbar
                new RowDefinition(GridLength.Star)    // content
            }
        };

        // ====== ROW 0: Header ======
        var headerRow = new HorizontalStackLayout { Spacing = 12 };
        headerRow.Children.Add((new Label { Text = "💡", FontSize = 20, VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1));
        _headerLabel = (new Label
        {
            Text = "0 ideas",
            FontSize = 14,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 230,
            LineBreakMode = LineBreakMode.TailTruncation,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2);
        headerRow.Children.Add(_headerLabel);

        _pendingSyncLabel = (new Label
        {
            Text = "",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3);
        headerRow.Children.Add(_pendingSyncLabel);

        _syncQueuedIdeasBtn = (new Button
        {
            Text = "Sync queued ideas",
            CornerRadius = 6,
            HeightRequest = 36,
            Padding = new Thickness(10, 0),
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _syncQueuedIdeasBtn.Clicked += OnSyncQueuedIdeasClicked;
        headerRow.Children.Add(_syncQueuedIdeasBtn);

        _searchEntry = (new Entry { Placeholder = "🔍 Search...", WidthRequest = 150 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _searchEntry.TextChanged += OnSearchChanged;
        headerRow.Children.Add(_searchEntry);

        _categoryPicker = new Picker { IsVisible = false };
        _categoryPicker.SelectedIndexChanged += OnCategoryChanged;

        var addBtn = (new Button { Text = "+ New", CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        addBtn.Clicked += OnAddIdeaClicked;
        headerRow.Children.Add(addBtn);

        headerRow.Children.Add((new Label { Text = "Sort:", FontSize = 12, VerticalOptions = LayoutOptions.Center, Margin = new Thickness(10, 0, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        _sortDateBtn = (new Button { Text = "📅 Date ↓", CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(8, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _sortDateBtn.Clicked += OnSortDateClicked;
        headerRow.Children.Add(_sortDateBtn);

        _sortRatingBtn = (new Button { Text = "⭐ Rating", CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(8, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _sortRatingBtn.Clicked += OnSortRatingClicked;
        headerRow.Children.Add(_sortRatingBtn);

        _showArchivedBtn = (new Button { Text = "📦", CornerRadius = 6, WidthRequest = 36, HeightRequest = 36 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _showArchivedBtn.Clicked += OnShowArchivedClicked;
        headerRow.Children.Add(_showArchivedBtn);

        var importBtn = (new Button { Text = "📥 Import", CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(8, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        importBtn.Clicked += OnImportClicked;
        headerRow.Children.Add(importBtn);

        mainGrid.Add(headerRow, 0, 0);

        // ====== ROW 1: Category filters ======
        mainGrid.Add(BuildCategoryFilterSection(), 0, 1);

        // ====== ROW 2: Toolbar (fixed) ======
        _toolbarContainer = new VerticalStackLayout { Padding = new Thickness(0, 2, 0, 4), IsVisible = false };
        mainGrid.Add(_toolbarContainer, 0, 2);

        // ====== ROW 3: Content ======
        var contentGrid = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(300)) },
            ColumnSpacing = 12
        };

        var scrollView = new ScrollView { Orientation = ScrollOrientation.Both, IsVisible = false };
        _gridContainer = new VerticalStackLayout { Spacing = 4 };
        scrollView.Content = _gridContainer;
        _gridContainer.IsVisible = false;
        _ideasContentView = scrollView;

        var ideasArea = new Grid();
        ideasArea.Add(scrollView);
        ideasArea.Add(BuildLoadingAndEmptyState());
        contentGrid.Add(ideasArea, 0, 0);

        BuildDetailPanel();
        contentGrid.Add(_detailPanel, 1, 0);

        mainGrid.Add(contentGrid, 0, 3);
        Content = mainGrid;
    }

    private void BuildPhoneUI()
    {
        var root = (new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        var header = (new VerticalStackLayout
        {
            Padding = new Thickness(14, 12, 14, 8),
            Spacing = 10
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };

        _headerLabel = (new Label
        {
            Text = "0 ideas",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1);
        titleRow.Add(_headerLabel, 0, 0);

        var addBtn = (new Button
        {
            Text = "+ New",
            CornerRadius = 8,
            HeightRequest = 42,
            Padding = new Thickness(16, 0),
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        addBtn.Clicked += OnAddIdeaClicked;
        titleRow.Add(addBtn, 1, 0);
        header.Children.Add(titleRow);

        _pendingSyncLabel = (new Label
        {
            Text = "",
            FontSize = 12,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3);
        header.Children.Add(_pendingSyncLabel);

        _syncQueuedIdeasBtn = (new Button
        {
            Text = "Sync queued ideas",
            CornerRadius = 8,
            HeightRequest = 42,
            FontAttributes = FontAttributes.Bold,
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _syncQueuedIdeasBtn.Clicked += OnSyncQueuedIdeasClicked;
        header.Children.Add(_syncQueuedIdeasBtn);

        _searchEntry = (new Entry
        {
            Placeholder = "Search ideas...",
            FontSize = 14
        }).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _searchEntry.TextChanged += OnSearchChanged;
        header.Children.Add(_searchEntry);

        _categoryPicker = new Picker { IsVisible = false };
        _categoryPicker.SelectedIndexChanged += OnCategoryChanged;

        header.Children.Add(BuildCategoryFilterSection());

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

        _sortDateBtn = (new Button
        {
            Text = "Date",
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _sortDateBtn.Clicked += OnSortDateClicked;
        controlsRow.Add(_sortDateBtn, 0, 0);

        _sortRatingBtn = (new Button
        {
            Text = "Rating",
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _sortRatingBtn.Clicked += OnSortRatingClicked;
        controlsRow.Add(_sortRatingBtn, 1, 0);

        _showArchivedBtn = (new Button
        {
            Text = "Archive",
            CornerRadius = 8,
            HeightRequest = 40,
            Padding = new Thickness(12, 0),
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _showArchivedBtn.Clicked += OnShowArchivedClicked;
        controlsRow.Add(_showArchivedBtn, 2, 0);
        header.Children.Add(controlsRow);

        var importBtn = (new Button
        {
            Text = "Import",
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        importBtn.Clicked += OnImportClicked;
        header.Children.Add(importBtn);

        _phoneEmptyLabel = (new Label
        {
            Text = "Select a category to view ideas.",
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(20)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2);

        _phoneIdeasView = (new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(CreatePhoneIdeaCard),
            EmptyView = _phoneEmptyLabel,
            IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);
        _phoneIdeasView.SelectionChanged += OnPhoneIdeaSelected;
        _ideasContentView = _phoneIdeasView;

        root.Add(header, 0, 0);
        var phoneIdeasArea = new Grid();
        phoneIdeasArea.Add(_phoneIdeasView);
        phoneIdeasArea.Add(BuildLoadingAndEmptyState());
        root.Add(phoneIdeasArea, 0, 1);
        Content = root;
    }

    private View BuildLoadingAndEmptyState()
    {
        _emptyStateLabel = (new Label
        {
            Text = "Pick a category from the dropdowns above to load ideas.",
            FontSize = 15,
            FontAttributes = FontAttributes.Italic,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 40)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2);

        _loadingIndicator = (new ActivityIndicator
        {
            WidthRequest = 48,
            HeightRequest = 48,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false,
            IsRunning = false
        }).ThemeColor(ActivityIndicator.ColorProperty, ThemeKey.InfoText);

        _loadingLabel = (new Label
        {
            Text = "Loading ideas...",
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false,
            Margin = new Thickness(0, 8, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2);

        _loadingStateView = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                _emptyStateLabel,
                new VerticalStackLayout
                {
                    Spacing = 0,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        _loadingIndicator,
                        _loadingLabel
                    }
                }
            }
        };
        return _loadingStateView;
    }

    private View CreatePhoneIdeaCard()
    {
        var card = (new Frame
        {
            Margin = new Thickness(12, 8),
            Padding = 14,
            CornerRadius = 8,
            HasShadow = false
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var stack = new VerticalStackLayout { Spacing = 8 };

        var title = (new Label
        {
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1);
        title.SetBinding(Label.TextProperty, nameof(IdeaItem.Title));
        stack.Children.Add(title);

        var meta = (new Label
        {
            FontSize = 12,
            LineBreakMode = LineBreakMode.TailTruncation
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3);
        meta.SetBinding(Label.TextProperty, new Binding(".", converter: new PhoneIdeaMetaConverter()));
        stack.Children.Add(meta);

        var detail = (new Label
        {
            FontSize = 12,
            LineBreakMode = LineBreakMode.TailTruncation
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3);
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
        _detailPanel = (new Frame
        {
            Padding = 12, CornerRadius = 8, HasShadow = true, IsVisible = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningText);

        var detailStack = new VerticalStackLayout { Spacing = 10 };

        var closeRow = new HorizontalStackLayout();
        closeRow.Children.Add((new Label { Text = "📄 Details", FontSize = 14, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.StartAndExpand, VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1));
        var closeBtn = (new Button { Text = "✕", BackgroundColor = Colors.Transparent, WidthRequest = 30, HeightRequest = 30, Padding = 0 }).ThemeColor(Button.TextColorProperty, ThemeKey.CardText3);
        closeBtn.Clicked += (s, e) => { _detailPanel.IsVisible = false; _selectedIdea = null; };
        closeRow.Children.Add(closeBtn);
        detailStack.Children.Add(closeRow);

        _detailTitle = (new Label { FontSize = 14, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1);
        detailStack.Children.Add(_detailTitle);

        _detailMeta = (new Label { FontSize = 10, LineBreakMode = LineBreakMode.WordWrap }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3);
        detailStack.Children.Add(_detailMeta);

        detailStack.Children.Add((new Label { Text = "Full Idea", FontSize = 11, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        _detailFullIdea = (new Editor { Placeholder = "Full idea...", HeightRequest = 150, FontSize = 12, IsReadOnly = true }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        detailStack.Children.Add(_detailFullIdea);

        detailStack.Children.Add((new Label { Text = "Notes", FontSize = 11, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        _detailContent = (new Editor { Placeholder = "Notes...", HeightRequest = 150, FontSize = 12 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _detailContent.Unfocused += OnDetailContentSave;
        detailStack.Children.Add(_detailContent);

        // Rating
        var ratingSection = new VerticalStackLayout { Spacing = 6 };
        var ratingHeader = new HorizontalStackLayout { Spacing = 8 };
        ratingHeader.Children.Add((new Label { Text = "Rating:", FontSize = 12, VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        _ratingLabel = (new Label { Text = "50", FontSize = 14, FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1);
        ratingHeader.Children.Add(_ratingLabel);
        ratingSection.Children.Add(ratingHeader);

        _ratingButtonsContainer = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start };
        foreach (var rating in new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })
        {
            var btn = (new Button { Text = rating.ToString(), CornerRadius = 4, WidthRequest = 38, HeightRequest = 30, Padding = 0, Margin = new Thickness(0, 0, 4, 4), FontSize = 11 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
            var r = rating; btn.Clicked += async (s, e) => await SetRatingAsync(r);
            _ratingButtons.Add(btn); _ratingButtonsContainer.Children.Add(btn);
        }

        _customRatingEntry = (new Entry { Placeholder = "Custom", Keyboard = Keyboard.Numeric, WidthRequest = 55, HeightRequest = 30, FontSize = 11 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
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
        var btnEdit = MiniBtn("✏️", ThemeKey.InfoButtonBackground); btnEdit.Clicked += OnEditClicked;
        var btnStar = MiniBtn("⭐", ThemeKey.WarningButtonBackground); btnStar.Clicked += OnStarClicked;
        var btnMove = MiniBtn("📂", ThemeKey.HighlightVioletText); btnMove.Clicked += OnMoveClicked;
        var btnDone = MiniBtn("✅", ThemeKey.SuccessButtonBackground); btnDone.Clicked += OnDoneClicked;
        var btnArchive = MiniBtn("🗄️", ThemeKey.NeutralDarkButton); btnArchive.Clicked += OnArchiveClicked;
        var btnDelete = MiniBtn("🗑️", ThemeKey.ErrorButtonBackground); btnDelete.Clicked += OnDeleteClicked;
        actions.Children.Add(btnEdit); actions.Children.Add(btnStar); actions.Children.Add(btnMove);
        actions.Children.Add(btnDone); actions.Children.Add(btnArchive); actions.Children.Add(btnDelete);
        detailStack.Children.Add(actions);

        _detailPanel.Content = detailStack;
    }

    private Button MiniBtn(string text, ThemeKey color) => (new Button
    {
        Text = text,         CornerRadius = 4, WidthRequest = 36, HeightRequest = 32, Padding = 0, Margin = new Thickness(0, 0, 4, 4)
    }).ThemeColor(VisualElement.BackgroundColorProperty, color).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

    private View BuildCategoryFilterSection()
    {
        var filterRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            Padding = new Thickness(0, 8)
        };

        _allButton = (new Button
        {
            Text = "All",
            CornerRadius = 8,
            HeightRequest = 40,
            WidthRequest = 60,
            FontSize = 12
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _allButton.Clicked += OnAllButtonClicked;

        _normalCategoryPicker = (new Picker
        {
            Title = "Normal category",
            HeightRequest = 40
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Picker.TextColorProperty, ThemeKey.InfoText).ThemeColor(Picker.TitleColorProperty, ThemeKey.InfoText);
        _normalCategoryPicker.SelectedIndexChanged += OnNormalCategoryPickerChanged;

        _llmCategoryPicker = (new Picker
        {
            Title = "LLM category",
            HeightRequest = 40
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Picker.TextColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Picker.TitleColorProperty, ThemeKey.HighlightVioletText);
        _llmCategoryPicker.SelectedIndexChanged += OnLlmCategoryPickerChanged;

        filterRow.Add(_allButton, 0, 0);
        filterRow.Add(_normalCategoryPicker, 1, 0);
        filterRow.Add(_llmCategoryPicker, 2, 0);

        return filterRow;
    }

    private void PopulateCategoryPickerItems(Dictionary<string, string> classificationMap)
    {
        if (_normalCategoryPicker == null || _llmCategoryPicker == null)
            return;

        var normalCategories = _categories
            .Where(c => !classificationMap.TryGetValue(c, out var classification) ||
                        !string.Equals(classification, "llm", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var llmCategories = _categories
            .Where(c => classificationMap.TryGetValue(c, out var classification) &&
                        string.Equals(classification, "llm", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _isLoadingFilters = true;
        try
        {
            _normalCategoryPicker.SelectedIndex = -1;
            _llmCategoryPicker.SelectedIndex = -1;
            _normalCategoryPicker.ItemsSource = normalCategories;
            _llmCategoryPicker.ItemsSource = llmCategories;

            if (!_showingArchived && !string.IsNullOrWhiteSpace(_selectedCategory) && _selectedCategory != "All" && _selectedCategory != "â­ Starred")
            {
                var normalIndex = normalCategories.FindIndex(c => string.Equals(c, _selectedCategory, StringComparison.OrdinalIgnoreCase));
                if (normalIndex >= 0)
                {
                    _normalCategoryPicker.SelectedIndex = normalIndex;
                    return;
                }

                var llmIndex = llmCategories.FindIndex(c => string.Equals(c, _selectedCategory, StringComparison.OrdinalIgnoreCase));
                if (llmIndex >= 0)
                    _llmCategoryPicker.SelectedIndex = llmIndex;
            }
        }
        finally
        {
            _isLoadingFilters = false;
        }
    }

    // ===================== DATA =====================

    private async Task LoadCategoriesAsync()
    {
        _loadingCategories = true;
        var previousCategory = _selectedCategory;
        await _ideas.BackfillMissingClassificationsAsync(_auth.CurrentUsername);
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

        var classifications = await _ideas.GetClassificationsAsync(_auth.CurrentUsername);
        var classificationMap = classifications
            .GroupBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Classification, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(_selectedCategory))
            _selectedCategory = "All";

        PopulateCategoryPickerItems(classificationMap);
        _loadingCategories = false;
    }

    private async Task RefreshIdeasAsync()
    {
        var (total, starred, inProgress, done) = await GetCachedStatsAsync();

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

    private async Task<(int total, int starred, int inProgress, int done)> GetCachedStatsAsync()
    {
        if (_cachedStats == null || _statsCacheDirty)
        {
            _cachedStats = await _ideas.GetStatsAsync(_auth.CurrentUsername);
            _statsCacheDirty = false;
        }

        return _cachedStats.Value;
    }

    private void InvalidateStatsCache()
    {
        _cachedStats = null;
        _statsCacheDirty = true;
    }

    private async Task FetchAndRenderIdeasAsync()
    {
        _hasLoadedIdeas = true;

        _emptyStateLabel.IsVisible = false;
        _headerLabel.IsVisible = false;
        if (_ideasContentView != null)
            _ideasContentView.IsVisible = false;
        if (!_isPhoneLayout)
        {
            _toolbarContainer.IsVisible = false;
            _gridContainer.IsVisible = false;
        }

        _loadingIndicator.IsRunning = true;
        _loadingIndicator.IsVisible = true;
        if (_loadingStateView != null)
            _loadingStateView.InputTransparent = false;
        _loadingLabel.IsVisible = true;

        try
        {
            await RefreshIdeasAsync();
        }
        finally
        {
            _loadingIndicator.IsRunning = false;
            _loadingIndicator.IsVisible = false;
            if (_loadingStateView != null)
                _loadingStateView.InputTransparent = true;
            _loadingLabel.IsVisible = false;
            _headerLabel.IsVisible = true;
            if (_ideasContentView != null)
                _ideasContentView.IsVisible = true;
            if (!_isPhoneLayout)
            {
                _toolbarContainer.IsVisible = true;
                _gridContainer.IsVisible = true;
            }
        }
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
        promptStack.Children.Add((new Label { Text = "Select a category from the dropdown above", FontSize = 14, HorizontalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2));
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
            _gridContainer.Children.Add((new Label { Text = "No ideas in this category.", HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(20) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2));
            return;
        }

        var headers = new List<string> { "Id", "Rating", "Title", "Full Idea", "Notes", "Category", "Subcategory", "Status", "Starred", "Created" };
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
            string notes = ideas[i].Notes ?? "";
            displayRows[i].Insert(3, fullIdea.Length > 80 ? fullIdea.Substring(0, 77) + "..." : fullIdea);
            fullRows[i].Insert(3, fullIdea);
            displayRows[i].Insert(4, notes.Length > 80 ? notes.Substring(0, 77) + "..." : notes);
            fullRows[i].Insert(4, notes);
        }

        var dataGrid = DataGridView.Create(headers, displayRows)
            .WithHeaderStyle(AppColors.Get(ThemeKey.WarningText), AppColors.Get(ThemeKey.ButtonPrimaryText))
            .WithAlternateRowColor(AppColors.Get(ThemeKey.HighlightYellow))
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
            case "Notes": idea.Notes = newValue; break;
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
        if (columnName == "Starred")
            InvalidateStatsCache();
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

        var notes = (new Editor
        {
            Text = idea.Notes ?? "",
            Placeholder = "Notes...",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 160
        }).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

        var page = (new ContentPage
        {
            Title = "Idea Details"
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        var stack = new VerticalStackLayout
        {
            Padding = 18,
            Spacing = 14
        };

        stack.Children.Add((new Label
        {
            Text = idea.Title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1));

        stack.Children.Add((new Label
        {
            Text = $"{idea.Category}  |  Rating {idea.Rating}  |  {idea.CreatedAt:MMM d, yyyy}",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        if (!string.IsNullOrWhiteSpace(idea.FullIdea))
        {
            stack.Children.Add((new Label
            {
                Text = "Full Idea",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1));

            stack.Children.Add((new Editor
            {
                Text = idea.FullIdea,
                IsReadOnly = true,
                AutoSize = EditorAutoSizeOption.TextChanges,
                MinimumHeightRequest = 160
            }).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground));
        }

        stack.Children.Add((new Label
        {
            Text = "Notes",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1));

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

        var editBtn = PhoneActionButton("Edit", ThemeKey.InfoButtonBackground);
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

        var starBtn = PhoneActionButton(idea.IsStarred ? "Unstar" : "Star", ThemeKey.WarningButtonBackground);
        starBtn.Clicked += async (_, _) =>
        {
            await _ideas.ToggleStarAsync(idea.Id);
            InvalidateStatsCache();
            idea.IsStarred = !idea.IsStarred;
            await RefreshIdeasAsync();
            await page.Navigation.PopModalAsync();
        };
        actionGrid.Add(starBtn, 1, 0);

        var moveBtn = PhoneActionButton("Move", ThemeKey.HighlightVioletText);
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

        var doneBtn = PhoneActionButton(idea.Status == 2 ? "Reopen" : "Done", ThemeKey.SuccessButtonBackground);
        doneBtn.Clicked += async (_, _) =>
        {
            if (idea.Status == 2) { idea.Status = 0; idea.CompletedAt = null; }
            else { idea.Status = 2; idea.CompletedAt = DateTime.Now; }
            await _ideas.UpdateIdeaAsync(idea);
            InvalidateStatsCache();
            await RefreshIdeasAsync();
            await page.Navigation.PopModalAsync();
        };
        actionGrid.Add(doneBtn, 1, 1);

        var archiveBtn = PhoneActionButton(idea.Status == 3 ? "Restore" : "Archive", ThemeKey.NeutralDarkButton);
        archiveBtn.Clicked += async (_, _) =>
        {
            if (idea.Status == 3) await _ideas.RestoreIdeaAsync(idea.Id);
            else await _ideas.ArchiveIdeaAsync(idea.Id);
            InvalidateStatsCache();
            await RefreshIdeasAsync();
            await page.Navigation.PopModalAsync();
        };
        actionGrid.Add(archiveBtn, 0, 2);

        var deleteBtn = PhoneActionButton("Delete", ThemeKey.ErrorButtonBackground);
        deleteBtn.Clicked += async (_, _) =>
        {
            if (await DisplayAlert("Delete?", "Permanently delete?", "Delete", "Cancel"))
            {
                await _ideas.DeleteIdeaAsync(idea.Id);
                InvalidateStatsCache();
                await RefreshIdeasAsync();
                await page.Navigation.PopModalAsync();
            }
        };
        actionGrid.Add(deleteBtn, 1, 2);

        stack.Children.Add(actionGrid);

        var saveCloseBtn = (new Button
        {
            Text = "Save & Close",
            CornerRadius = 8,
            HeightRequest = 48
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
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

        var closeBtn = (new Button
        {
            Text = "Close",
            BackgroundColor = Colors.Transparent
        }).ThemeColor(Button.TextColorProperty, ThemeKey.CardText2);
        closeBtn.Clicked += async (_, _) => await page.Navigation.PopModalAsync();
        stack.Children.Add(closeBtn);

        page.Content = new ScrollView { Content = stack };
        await Navigation.PushModalAsync(page);
    }

    private Button PhoneActionButton(string text, ThemeKey color) => (new Button(){
        Text = text,
        CornerRadius = 8,
        HeightRequest = 42,
        FontSize = 13
    }).ThemeColor(VisualElement.BackgroundColorProperty, color).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

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
                InvalidateStatsCache();
                idea.IsStarred = !idea.IsStarred;
                await RefreshIdeasAsync();
                break;
            case "Delete":
                if (await DisplayAlert("Delete?", "Permanently delete?", "Delete", "Cancel"))
                {
                    await _ideas.DeleteIdeaAsync(idea.Id);
                    InvalidateStatsCache();
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
            _ratingButtons[i].ThemeColor(VisualElement.BackgroundColorProperty, sel ? GetRatingColor(rating) : ThemeKey.Separator);
            _ratingButtons[i].ThemeColor(Button.TextColorProperty, sel ? ThemeKey.ButtonPrimaryText : ThemeKey.CardText1);
        }
    }

    private ThemeKey GetRatingColor(int r) => r >= 70 ? ThemeKey.SuccessButtonBackground : r >= 40 ? ThemeKey.WarningText : ThemeKey.DisabledButtonBackground;
    private void UpdateRatingLabelColor(int r) => _ratingLabel.ThemeColor(Label.TextColorProperty,
        r >= 70 ? ThemeKey.SuccessButtonBackground : r >= 40 ? ThemeKey.WarningText : ThemeKey.CardText3);

    private async Task SetRatingAsync(int rating)
    {
        if (_selectedIdea == null) return;
        _selectedIdea.Rating = rating;
        _ratingLabel.Text = rating.ToString();
        UpdateRatingLabelColor(rating); UpdateRatingButtonSelection(rating);
        await _ideas.UpdateIdeaAsync(_selectedIdea);
        await FetchAndRenderIdeasAsync();
    }

    private async void OnDetailContentSave(object? sender, FocusEventArgs e)
    {
        if (_selectedIdea == null) return;
        _selectedIdea.Notes = _detailContent.Text;
        await _ideas.UpdateIdeaAsync(_selectedIdea);
    }

    // ===================== HEADER EVENTS =====================

    private async void OnAllButtonClicked(object? sender, EventArgs e)
    {
        _selectedCategory = "All";
        _showingArchived = false;
        _showArchivedBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground);

        _isLoadingFilters = true;
        try
        {
            _normalCategoryPicker.SelectedIndex = -1;
            _llmCategoryPicker.SelectedIndex = -1;
        }
        finally
        {
            _isLoadingFilters = false;
        }

        await FetchAndRenderIdeasAsync();
    }

    private async void OnNormalCategoryPickerChanged(object? sender, EventArgs e)
    {
        if (_isLoadingFilters) return;
        if (_normalCategoryPicker.SelectedIndex < 0) return;

        var selected = _normalCategoryPicker.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selected)) return;

        _selectedCategory = selected;
        _showingArchived = false;
        _showArchivedBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground);

        _isLoadingFilters = true;
        try
        {
            _llmCategoryPicker.SelectedIndex = -1;
        }
        finally
        {
            _isLoadingFilters = false;
        }

        await FetchAndRenderIdeasAsync();
    }

    private async void OnLlmCategoryPickerChanged(object? sender, EventArgs e)
    {
        if (_isLoadingFilters) return;
        if (_llmCategoryPicker.SelectedIndex < 0) return;

        var selected = _llmCategoryPicker.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selected)) return;

        _selectedCategory = selected;
        _showingArchived = false;
        _showArchivedBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground);

        _isLoadingFilters = true;
        try
        {
            _normalCategoryPicker.SelectedIndex = -1;
        }
        finally
        {
            _isLoadingFilters = false;
        }

        await FetchAndRenderIdeasAsync();
    }

    private async void OnCategoryChanged(object? sender, EventArgs e)
    {
        if (_loadingCategories) return;
        if (_categoryPicker.SelectedIndex < 0) return;
        _selectedCategory = _categoryPicker.Items[_categoryPicker.SelectedIndex] as string ?? "All";
        _showingArchived = false; _showArchivedBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground);
        await FetchAndRenderIdeasAsync();
    }

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        _searchText = e.NewTextValue?.ToLower() ?? "";
        if (_hasLoadedIdeas)
            await FetchAndRenderIdeasAsync();
    }

    private async void OnShowArchivedClicked(object? sender, EventArgs e)
    {
        if (!_hasLoadedIdeas) return;
        _showingArchived = !_showingArchived;
        _showArchivedBtn.ThemeColor(VisualElement.BackgroundColorProperty, _showingArchived ? ThemeKey.WarningButtonBackground : ThemeKey.DisabledButtonBackground);
        await FetchAndRenderIdeasAsync();
    }

    #region Actions

    private async void OnAddIdeaClicked(object? sender, EventArgs e)
    {
        string? cat = (_selectedCategory == "All" || _selectedCategory == "⭐ Starred" || string.IsNullOrEmpty(_selectedCategory)) ? null : _selectedCategory;
        var ideaId = await _ideaLogger.LogIdeaAsync(
            new IdeaLogRequest { Username = _auth.CurrentUsername, Category = cat },
            IdeaLogMode.Interactive,
            this);
        if (ideaId != null)
        {
            InvalidateStatsCache();
            await LoadCategoriesAsync();
            if (_hasLoadedIdeas)
                await FetchAndRenderIdeasAsync();
            await RefreshPendingSyncCountAsync();
        }
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
        InvalidateStatsCache();
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
        await _ideas.UpdateIdeaAsync(_selectedIdea); InvalidateStatsCache(); ShowDetail(_selectedIdea); await RefreshIdeasAsync();
    }

    private async void OnArchiveClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        if (_selectedIdea.Status == 3) await _ideas.RestoreIdeaAsync(_selectedIdea.Id);
        else await _ideas.ArchiveIdeaAsync(_selectedIdea.Id);
        InvalidateStatsCache();
        _detailPanel.IsVisible = false; _selectedIdea = null; await RefreshIdeasAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (_selectedIdea == null) return;
        if (await DisplayAlert("Delete?", "Permanently delete?", "Delete", "Cancel"))
        { await _ideas.DeleteIdeaAsync(_selectedIdea.Id); InvalidateStatsCache(); _detailPanel.IsVisible = false; _selectedIdea = null; await RefreshIdeasAsync(); }
    }

    private async void OnSortDateClicked(object? sender, EventArgs e)
    {
        if (_sortColumn == "Date") _sortDescending = !_sortDescending; else { _sortColumn = "Date"; _sortDescending = true; }
        UpdateSortButtons();
        if (_hasLoadedIdeas)
            await FetchAndRenderIdeasAsync();
    }

    private async void OnSortRatingClicked(object? sender, EventArgs e)
    {
        if (_sortColumn == "Rating") _sortDescending = !_sortDescending; else { _sortColumn = "Rating"; _sortDescending = true; }
        UpdateSortButtons();
        if (_hasLoadedIdeas)
            await FetchAndRenderIdeasAsync();
    }

    private void UpdateSortButtons()
    {
        string a = _sortDescending ? "↓" : "↑";
        if (_sortColumn == "Date") { _sortDateBtn.Text = $"📅 Date {a}"; _sortDateBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoButtonBackground); _sortRatingBtn.Text = "⭐ Rating"; _sortRatingBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton); }
        else { _sortDateBtn.Text = "📅 Date"; _sortDateBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton); _sortRatingBtn.Text = $"⭐ Rating {a}"; _sortRatingBtn.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground); }
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

            var overlay = (new Grid { HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.Fill }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);
            var pStack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center, Spacing = 12 };
            pStack.Children.Add((new ActivityIndicator { IsRunning = true, HeightRequest = 40, WidthRequest = 40 }).ThemeColor(ActivityIndicator.ColorProperty, ThemeKey.ButtonPrimaryText));
            var pLabel = (new Label { Text = "Importing ideas...", FontSize = 16, HorizontalTextAlignment = TextAlignment.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1);
            var pDetail = (new Label { Text = $"0 / {itemsToInsert.Count}", FontSize = 13, HorizontalTextAlignment = TextAlignment.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigo, ThemeKey.PageText1);
            pStack.Children.Add(pLabel);
            pStack.Children.Add(pDetail);
            overlay.Children.Add(pStack);
            progressWrapper.Children.Add(overlay);

            var requests = itemsToInsert.Select(x => new IdeaLogRequest
            {
                Username = username,
                Title = x.text,
                Body = x.text,
                Category = targetCategory,
                Rating = x.rating,
                CreatedAt = x.createdAt,
                Subcategory = x.subcategory
            });
            var batchResult = await _ideaLogger.LogBatchSilentAsync(requests);
            int imported = batchResult.SavedCount;
            pDetail.Text = $"{imported} / {itemsToInsert.Count}";
            if (batchResult.Failures.Count > 0)
                System.Diagnostics.Debug.WriteLine($"Ideas import logging failures: {batchResult.Failures.Count}");
            await Task.Yield();

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

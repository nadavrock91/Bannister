using Bannister.Models;
using Bannister.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bannister.Views;

/// <summary>
/// Focus settings for categorical learning discipline
/// </summary>
public class FocusSettings
{
    public bool IsActive { get; set; } = false;
    public string Category { get; set; } = "";
    public int FocusedRequired { get; set; } = 2;  // e.g., 2 in 3
    public int TotalWindow { get; set; } = 3;      // e.g., 2 in 3
    public int GoalCount { get; set; } = 100;      // total to complete
    public int CompletedInCategory { get; set; } = 0;
    public int FocusedInWindow { get; set; } = 0;  // current window progress
    public int TotalInWindow { get; set; } = 0;    // current window progress
    public int StreakDays { get; set; } = 0;
    public DateTime? LastCompletedDate { get; set; }
    public DateTime? StreakStartDate { get; set; }
    public int CreatorMonthlyLimit { get; set; } = 10;  // max videos per creator per month
}

/// <summary>
/// Learning hub page with Books and Videos sections
/// </summary>
public class LearningPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly LearningService _learning;
    private readonly HttpClient _httpClient;
    
    // UI Controls
    private VerticalStackLayout _booksStack;
    private VerticalStackLayout _videosStack;
    private Button _btnBooks;
    private Button _btnVideos;
    private Frame _booksFrame;
    private Frame _videosFrame;
    private string _currentTab = "Books";
    
    // Filters
    private Picker _categoryPicker;
    private Picker _channelPicker;
    private Picker _statusPicker;
    private string _selectedCategory = "All";
    private string _selectedChannel = "All";
    private string _selectedStatus = "To Watch";
    private bool _isUpdatingPicker = false;
    
    // Focus system UI
    private Frame _videoFocusFrame;
    private Frame _bookFocusFrame;
    private Label _videoFocusStatusLabel;
    private Label _bookFocusStatusLabel;
    
    // Services for learning game activities
    private readonly ActivityService _activities;
    private readonly GameService _games;

    public LearningPage(AuthService auth, LearningService learning, ActivityService activities, GameService games)
    {
        _auth = auth;
        _learning = learning;
        _activities = activities;
        _games = games;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        
        Title = "Learning";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Ensure Learning game exists (but don't auto-create activities)
        await EnsureLearningGameExistsAsync();
        
        // Set default category filter to focus category if one is active
        var focus = GetVideoFocusSettings();
        if (focus.IsActive && !string.IsNullOrEmpty(focus.Category))
        {
            _selectedCategory = focus.Category;
        }
        
        await RefreshDataAsync();
    }
    
    /// <summary>
    /// Ensure Learning game exists (activities are created on-demand when focus is completed)
    /// </summary>
    private async Task EnsureLearningGameExistsAsync()
    {
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);
        var learningGame = games.FirstOrDefault(g => g.DisplayName == "Learning");
        
        // Create Learning game if it doesn't exist
        if (learningGame == null)
        {
            await _games.CreateGameAsync(_auth.CurrentUsername, "Learning");
        }
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16
        };

        // Header
        var headerLabel = new Label
        {
            Text = "📚 Learning Center",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalOptions = LayoutOptions.Center
        };
        mainStack.Children.Add(headerLabel);

        // Tab buttons
        var tabGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12
        };

        _btnBooks = new Button
        {
            Text = "📖 Books",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 50
        };
        _btnBooks.Clicked += (s, e) => SwitchTab("Books");
        Grid.SetColumn(_btnBooks, 0);
        tabGrid.Children.Add(_btnBooks);

        _btnVideos = new Button
        {
            Text = "🎬 Videos",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#666"),
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 50
        };
        _btnVideos.Clicked += (s, e) => SwitchTab("Videos");
        Grid.SetColumn(_btnVideos, 1);
        tabGrid.Children.Add(_btnVideos);

        mainStack.Children.Add(tabGrid);

        // Books Section
        _booksFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            IsVisible = true
        };

        var booksContainer = new VerticalStackLayout { Spacing = 0 };
        
        // Button row: Add Book + Settings
        var bookButtonRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8,
            Margin = new Thickness(12, 12, 12, 8)
        };
        
        var addBookBtn = new Button
        {
            Text = "+ Add Book",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        addBookBtn.Clicked += OnAddBookClicked;
        Grid.SetColumn(addBookBtn, 0);
        bookButtonRow.Children.Add(addBookBtn);
        
        var bookSettingsBtn = new Button
        {
            Text = "⚙️",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            WidthRequest = 50,
            CornerRadius = 8
        };
        bookSettingsBtn.Clicked += OnBookSettingsClicked;
        Grid.SetColumn(bookSettingsBtn, 1);
        bookButtonRow.Children.Add(bookSettingsBtn);
        
        booksContainer.Children.Add(bookButtonRow);

        // Book Focus Panel
        _bookFocusFrame = new Frame
        {
            Padding = 10,
            Margin = new Thickness(12, 0, 12, 8),
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            BorderColor = Color.FromArgb("#4CAF50"),
            HasShadow = false,
            IsVisible = false
        };
        
        var bookFocusStack = new VerticalStackLayout { Spacing = 6 };
        
        _bookFocusStatusLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#388E3C")
        };
        bookFocusStack.Children.Add(_bookFocusStatusLabel);
        
        var bookFocusBtnRow = new HorizontalStackLayout { Spacing = 8 };
        
        var editBookFocusBtn = new Button
        {
            Text = "✏️ Edit",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = new Thickness(8, 0),
            FontSize = 11
        };
        editBookFocusBtn.Clicked += async (s, e) => await SetupBookFocusAsync();
        bookFocusBtnRow.Children.Add(editBookFocusBtn);
        
        var editBookNumbersBtn = new Button
        {
            Text = "🔢 Numbers",
            BackgroundColor = Color.FromArgb("#9C27B0"),
            TextColor = Colors.White,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = new Thickness(8, 0),
            FontSize = 11
        };
        editBookNumbersBtn.Clicked += async (s, e) => await EditBookFocusNumbersAsync();
        bookFocusBtnRow.Children.Add(editBookNumbersBtn);
        
        var resetBookStreakBtn = new Button
        {
            Text = "🔄 Reset Streak",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = new Thickness(8, 0),
            FontSize = 11
        };
        resetBookStreakBtn.Clicked += async (s, e) => await ResetBookFocusStreakAsync();
        bookFocusBtnRow.Children.Add(resetBookStreakBtn);
        
        var clearBookFocusBtn = new Button
        {
            Text = "❌ Clear Focus",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = new Thickness(8, 0),
            FontSize = 11
        };
        clearBookFocusBtn.Clicked += async (s, e) => await ClearBookFocusAsync();
        bookFocusBtnRow.Children.Add(clearBookFocusBtn);
        
        bookFocusStack.Children.Add(bookFocusBtnRow);
        _bookFocusFrame.Content = bookFocusStack;
        booksContainer.Children.Add(_bookFocusFrame);

        var booksScroll = new ScrollView { HeightRequest = 400 };
        _booksStack = new VerticalStackLayout { Spacing = 8, Padding = 12 };
        booksScroll.Content = _booksStack;
        booksContainer.Children.Add(booksScroll);

        _booksFrame.Content = booksContainer;
        mainStack.Children.Add(_booksFrame);

        // Videos Section
        _videosFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            IsVisible = false
        };

        var videosContainer = new VerticalStackLayout { Spacing = 0 };
        
        // Button row: Add + Import
        var buttonRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            Margin = new Thickness(12, 12, 12, 8)
        };

        var addVideoBtn = new Button
        {
            Text = "+ Add Video",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        addVideoBtn.Clicked += OnAddVideoClicked;
        Grid.SetColumn(addVideoBtn, 0);
        buttonRow.Children.Add(addVideoBtn);

        var importBtn = new Button
        {
            Text = "📋 Import Links",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        importBtn.Clicked += OnImportLinksClicked;
        Grid.SetColumn(importBtn, 1);
        buttonRow.Children.Add(importBtn);

        videosContainer.Children.Add(buttonRow);

        // Filter row 1: Category and Channel
        var filterRow1 = new HorizontalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(12, 0, 12, 4)
        };

        // Category filter
        filterRow1.Children.Add(new Label
        {
            Text = "Category:",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#666"),
            FontSize = 12
        });

        _categoryPicker = new Picker
        {
            Title = "All",
            WidthRequest = 120,
            BackgroundColor = Colors.White
        };
        _categoryPicker.SelectedIndexChanged += async (s, e) =>
        {
            if (_isUpdatingPicker) return;
            if (_categoryPicker.SelectedIndex >= 0)
            {
                _selectedCategory = _categoryPicker.Items[_categoryPicker.SelectedIndex];
                await RefreshVideosAsync();
            }
        };
        filterRow1.Children.Add(_categoryPicker);

        // Channel filter
        filterRow1.Children.Add(new Label
        {
            Text = "Channel:",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#666"),
            FontSize = 12
        });

        _channelPicker = new Picker
        {
            Title = "All",
            WidthRequest = 150,
            BackgroundColor = Colors.White
        };
        _channelPicker.SelectedIndexChanged += async (s, e) =>
        {
            if (_isUpdatingPicker) return;
            if (_channelPicker.SelectedIndex >= 0)
            {
                _selectedChannel = _channelPicker.Items[_channelPicker.SelectedIndex];
                await RefreshVideosAsync();
            }
        };
        filterRow1.Children.Add(_channelPicker);

        videosContainer.Children.Add(filterRow1);

        // Filter row 2: Status and Settings
        var filterRow2 = new HorizontalStackLayout
        {
            Spacing = 12,
            Padding = new Thickness(12, 0, 12, 8)
        };

        // Status filter
        filterRow2.Children.Add(new Label
        {
            Text = "Status:",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#666"),
            FontSize = 12
        });

        _statusPicker = new Picker
        {
            Title = "To Watch",
            WidthRequest = 120,
            BackgroundColor = Colors.White
        };
        _statusPicker.Items.Add("All");
        _statusPicker.Items.Add("To Watch");
        _statusPicker.Items.Add("Locked");
        _statusPicker.Items.Add("Watching");
        _statusPicker.Items.Add("Pending");
        _statusPicker.Items.Add("Read Summary");
        _statusPicker.Items.Add("Watched");
        _statusPicker.SelectedIndex = 1; // Default to "To Watch"
        _statusPicker.SelectedIndexChanged += async (s, e) =>
        {
            if (_isUpdatingPicker) return;
            if (_statusPicker.SelectedIndex >= 0)
            {
                _selectedStatus = _statusPicker.Items[_statusPicker.SelectedIndex];
                await RefreshVideosAsync();
            }
        };
        filterRow2.Children.Add(_statusPicker);

        // Settings button
        var settingsBtn = new Button
        {
            Text = "⚙️ Settings",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            HeightRequest = 35,
            CornerRadius = 6,
            Padding = new Thickness(10, 0),
            FontSize = 12
        };
        settingsBtn.Clicked += OnVideoSettingsClicked;
        filterRow2.Children.Add(settingsBtn);

        videosContainer.Children.Add(filterRow2);

        // Video Focus Panel
        _videoFocusFrame = new Frame
        {
            Padding = 10,
            Margin = new Thickness(12, 0, 12, 8),
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            BorderColor = Color.FromArgb("#2196F3"),
            HasShadow = false,
            IsVisible = false
        };
        
        var focusStack = new VerticalStackLayout { Spacing = 6 };
        
        _videoFocusStatusLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#1976D2")
        };
        focusStack.Children.Add(_videoFocusStatusLabel);
        
        var focusBtnRow = new HorizontalStackLayout { Spacing = 8 };
        
        var editFocusBtn = new Button
        {
            Text = "✏️ Edit",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = new Thickness(8, 0),
            FontSize = 11
        };
        editFocusBtn.Clicked += async (s, e) => await SetupVideoFocusAsync();
        focusBtnRow.Children.Add(editFocusBtn);
        
        var editNumbersBtn = new Button
        {
            Text = "🔢 Numbers",
            BackgroundColor = Color.FromArgb("#9C27B0"),
            TextColor = Colors.White,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = new Thickness(8, 0),
            FontSize = 11
        };
        editNumbersBtn.Clicked += async (s, e) => await EditVideoFocusNumbersAsync();
        focusBtnRow.Children.Add(editNumbersBtn);
        
        var resetStreakBtn = new Button
        {
            Text = "🔄 Reset Streak",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = new Thickness(8, 0),
            FontSize = 11
        };
        resetStreakBtn.Clicked += async (s, e) => await ResetVideoFocusStreakAsync();
        focusBtnRow.Children.Add(resetStreakBtn);
        
        var clearFocusBtn = new Button
        {
            Text = "❌ Clear Focus",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            HeightRequest = 30,
            CornerRadius = 4,
            Padding = new Thickness(8, 0),
            FontSize = 11
        };
        clearFocusBtn.Clicked += async (s, e) => await ClearVideoFocusAsync();
        focusBtnRow.Children.Add(clearFocusBtn);
        
        focusStack.Children.Add(focusBtnRow);
        _videoFocusFrame.Content = focusStack;
        videosContainer.Children.Add(_videoFocusFrame);

        var videosScroll = new ScrollView { HeightRequest = 400 };
        _videosStack = new VerticalStackLayout { Spacing = 8, Padding = 12 };
        videosScroll.Content = _videosStack;
        videosContainer.Children.Add(videosScroll);

        _videosFrame.Content = videosContainer;
        mainStack.Children.Add(_videosFrame);

        var scrollView = new ScrollView { Content = mainStack };
        Content = scrollView;
    }

    private void SwitchTab(string tab)
    {
        _currentTab = tab;
        
        if (tab == "Books")
        {
            _btnBooks.BackgroundColor = Color.FromArgb("#5B63EE");
            _btnBooks.TextColor = Colors.White;
            _btnVideos.BackgroundColor = Color.FromArgb("#E0E0E0");
            _btnVideos.TextColor = Color.FromArgb("#666");
            _booksFrame.IsVisible = true;
            _videosFrame.IsVisible = false;
        }
        else
        {
            _btnVideos.BackgroundColor = Color.FromArgb("#5B63EE");
            _btnVideos.TextColor = Colors.White;
            _btnBooks.BackgroundColor = Color.FromArgb("#E0E0E0");
            _btnBooks.TextColor = Color.FromArgb("#666");
            _booksFrame.IsVisible = false;
            _videosFrame.IsVisible = true;
        }
    }

    private async Task RefreshDataAsync()
    {
        await RefreshBooksAsync();
        await RefreshVideosAsync();
    }

    private async Task RefreshBooksAsync()
    {
        _booksStack.Children.Clear();
        
        // Update focus panel
        UpdateBookFocusPanel();
        
        var books = await _learning.GetBooksAsync(_auth.CurrentUsername);
        
        if (books.Count == 0)
        {
            _booksStack.Children.Add(new Label
            {
                Text = "No books added yet.\nTap '+ Add Book' to start your reading list!",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            });
            return;
        }

        int order = 1;
        foreach (var book in books)
        {
            var card = BuildBookCard(book, order++);
            _booksStack.Children.Add(card);
        }
    }

    private Frame BuildBookCard(LearningBook book, int order)
    {
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = book.Status == "Completed" 
                ? Color.FromArgb("#E8F5E9") 
                : book.Status == "InProgress" 
                    ? Color.FromArgb("#FFF3E0") 
                    : Colors.White,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E0E0E0")
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 40 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        // Order number
        var orderLabel = new Label
        {
            Text = $"#{order}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(orderLabel, 0);
        grid.Children.Add(orderLabel);

        // Book info
        var infoStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        
        var titleLabel = new Label
        {
            Text = book.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        infoStack.Children.Add(titleLabel);

        if (!string.IsNullOrEmpty(book.Author))
        {
            var authorLabel = new Label
            {
                Text = $"by {book.Author}",
                FontSize = 12,
                TextColor = Color.FromArgb("#666")
            };
            infoStack.Children.Add(authorLabel);
        }

        var statusLabel = new Label
        {
            Text = book.Status == "Completed" ? "✅ Completed" 
                 : book.Status == "InProgress" ? "📖 Reading" 
                 : "📚 To Read",
            FontSize = 11,
            TextColor = book.Status == "Completed" ? Color.FromArgb("#4CAF50")
                      : book.Status == "InProgress" ? Color.FromArgb("#FF9800")
                      : Color.FromArgb("#999")
        };
        infoStack.Children.Add(statusLabel);

        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        // Action buttons
        var buttonStack = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        var menuBtn = new Button
        {
            Text = "⋮",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#666"),
            FontSize = 18,
            WidthRequest = 36,
            HeightRequest = 36,
            Padding = 0
        };
        menuBtn.Clicked += async (s, e) => await ShowBookMenuAsync(book);
        buttonStack.Children.Add(menuBtn);

        Grid.SetColumn(buttonStack, 2);
        grid.Children.Add(buttonStack);

        frame.Content = grid;
        return frame;
    }

    private async Task ShowBookMenuAsync(LearningBook book)
    {
        var options = new List<string>();
        
        if (book.Status != "InProgress")
            options.Add("📖 Mark as Reading");
        if (book.Status != "Completed")
            options.Add("✅ Mark as Completed");
        if (book.Status != "NotStarted")
            options.Add("📚 Mark as To Read");
        
        options.Add("⬆️ Move Up");
        options.Add("⬇️ Move Down");
        options.Add("✏️ Edit");
        options.Add("🗑️ Delete");

        var result = await DisplayActionSheet($"📖 {book.Title}", "Cancel", null, options.ToArray());

        if (result == null || result == "Cancel") return;

        if (result == "📖 Mark as Reading")
        {
            book.Status = "InProgress";
            await _learning.UpdateBookAsync(book);
        }
        else if (result == "✅ Mark as Completed")
        {
            await _learning.CompleteBookAsync(book.Id);
            await RecordBookFocusCompletionAsync(book.Category ?? "Unsorted");
        }
        else if (result == "📚 Mark as To Read")
        {
            book.Status = "NotStarted";
            book.CompletedAt = null;
            await _learning.UpdateBookAsync(book);
        }
        else if (result == "⬆️ Move Up")
        {
            await _learning.MoveBookUpAsync(_auth.CurrentUsername, book.Id);
        }
        else if (result == "⬇️ Move Down")
        {
            await _learning.MoveBookDownAsync(_auth.CurrentUsername, book.Id);
        }
        else if (result == "✏️ Edit")
        {
            await EditBookAsync(book);
        }
        else if (result == "🗑️ Delete")
        {
            bool confirm = await DisplayAlert("Delete Book", $"Delete '{book.Title}' from your list?", "Delete", "Cancel");
            if (confirm)
            {
                await _learning.DeleteBookAsync(book.Id);
            }
        }

        await RefreshBooksAsync();
    }

    private async void OnBookSettingsClicked(object sender, EventArgs e)
    {
        var focus = GetBookFocusSettings();
        string focusOption = focus.IsActive 
            ? $"🎯 Focus Mode: {focus.Category} ({focus.CompletedInCategory}/{focus.GoalCount})"
            : "🎯 Set Focus Mode";
        
        var result = await DisplayActionSheet(
            "⚙️ Book Settings",
            "Done",
            null,
            focusOption);

        if (result == null || result == "Done") return;

        if (result == focusOption)
        {
            await SetupBookFocusAsync();
        }
    }

    private async void OnAddBookClicked(object sender, EventArgs e)
    {
        string title = await DisplayPromptAsync("Add Book", "Enter book title:");
        if (string.IsNullOrWhiteSpace(title)) return;

        string author = await DisplayPromptAsync("Add Book", "Enter author (optional):", initialValue: "");
        
        var book = new LearningBook
        {
            Username = _auth.CurrentUsername,
            Title = title.Trim(),
            Author = author?.Trim() ?? ""
        };

        await _learning.AddBookAsync(book);
        await RefreshBooksAsync();
    }

    private async Task EditBookAsync(LearningBook book)
    {
        string title = await DisplayPromptAsync("Edit Book", "Title:", initialValue: book.Title);
        if (string.IsNullOrWhiteSpace(title)) return;

        string author = await DisplayPromptAsync("Edit Book", "Author:", initialValue: book.Author ?? "");

        book.Title = title.Trim();
        book.Author = author?.Trim() ?? "";
        await _learning.UpdateBookAsync(book);
    }

    private async Task RefreshVideosAsync()
    {
        _videosStack.Children.Clear();
        
        // Update focus panel
        UpdateVideoFocusPanel();
        
        var allVideos = await _learning.GetVideosAsync(_auth.CurrentUsername);
        var focus = GetVideoFocusSettings();
        
        // Update all pickers
        await UpdateFilterPickersAsync(allVideos);
        
        // Get locked creators (those at monthly limit in focus category)
        var lockedCreators = await GetLockedCreatorsAsync();
        
        // Calculate creator monthly watch counts for display (in focus category only)
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var creatorMonthlyCounts = new Dictionary<string, int>();
        
        if (focus.IsActive)
        {
            creatorMonthlyCounts = allVideos
                .Where(v => v.Status == "Completed" && 
                           v.CompletedAt >= monthStart &&
                           (v.Category ?? "Unsorted") == focus.Category)
                .GroupBy(v => v.Creator ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Count());
        }
        
        // Apply filters
        var videos = allVideos.AsEnumerable();
        
        // Category filter
        if (_selectedCategory != "All")
            videos = videos.Where(v => (v.Category ?? "Unsorted") == _selectedCategory);
        
        // Channel filter
        if (_selectedChannel != "All")
            videos = videos.Where(v => (v.Creator ?? "Unknown") == _selectedChannel);
        
        // Status filter - special handling for Locked
        if (_selectedStatus == "Locked")
        {
            // Show only unwatched videos from locked creators in focus category
            if (focus.IsActive && _selectedCategory == focus.Category)
            {
                videos = videos.Where(v => 
                    v.Status == "NotStarted" && 
                    lockedCreators.Contains(v.Creator ?? "Unknown"));
            }
            else
            {
                // No locked videos outside focus category
                videos = Enumerable.Empty<LearningVideo>();
            }
        }
        else if (_selectedStatus == "To Watch")
        {
            // Exclude locked creators when showing "To Watch" in focus category
            if (focus.IsActive && _selectedCategory == focus.Category)
            {
                videos = videos.Where(v => 
                    v.Status == "NotStarted" && 
                    !lockedCreators.Contains(v.Creator ?? "Unknown"));
            }
            else
            {
                videos = videos.Where(v => v.Status == "NotStarted");
            }
        }
        else if (_selectedStatus != "All")
        {
            videos = _selectedStatus switch
            {
                "Watching" => videos.Where(v => v.Status == "InProgress"),
                "Pending" => videos.Where(v => v.Status == "PendingWatched"),
                "Read Summary" => videos.Where(v => v.Status == "ReadSummary"),
                "Watched" => videos.Where(v => v.Status == "Completed"),
                _ => videos
            };
        }
        
        var filteredVideos = videos.ToList();
        
        if (filteredVideos.Count == 0)
        {
            string message = allVideos.Count == 0
                ? "No videos added yet.\nTap '+ Add Video' or '📋 Import Links' to start!"
                : _selectedStatus == "Locked" 
                    ? "No locked creators.\nCreators get locked when you watch " + focus.CreatorMonthlyLimit + " videos from them this month."
                    : "No videos match the current filters.";
                
            _videosStack.Children.Add(new Label
            {
                Text = message,
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            });
            return;
        }

        // Show locked count header if viewing focus category
        if (focus.IsActive && _selectedCategory == focus.Category && lockedCreators.Count > 0 && _selectedStatus != "Locked")
        {
            var lockedHeader = new Frame
            {
                Padding = 8,
                CornerRadius = 6,
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                BorderColor = Color.FromArgb("#EF5350"),
                HasShadow = false,
                Margin = new Thickness(0, 0, 0, 8),
                Content = new Label
                {
                    Text = $"🔒 {lockedCreators.Count} creator(s) locked this month (select 'Locked' filter to view)",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#C62828")
                }
            };
            _videosStack.Children.Add(lockedHeader);
        }

        foreach (var video in filteredVideos)
        {
            int creatorCount = creatorMonthlyCounts.GetValueOrDefault(video.Creator ?? "Unknown", 0);
            bool isLocked = lockedCreators.Contains(video.Creator ?? "Unknown");
            var card = BuildVideoCard(video, creatorCount, focus.CreatorMonthlyLimit, isLocked);
            _videosStack.Children.Add(card);
        }
    }

    private async Task UpdateFilterPickersAsync(List<LearningVideo> videos)
    {
        _isUpdatingPicker = true;
        
        try
        {
            // Reset selections before clearing
            _categoryPicker.SelectedIndex = -1;
            _channelPicker.SelectedIndex = -1;
            
            // Update category picker
            var categories = videos
                .Select(v => v.Category ?? "Unsorted")
                .Distinct()
                .OrderBy(c => c == "Unsorted" ? "ZZZ" : c)
                .ToList();
            
            _categoryPicker.Items.Clear();
            _categoryPicker.Items.Add("All");
            foreach (var cat in categories)
                _categoryPicker.Items.Add(cat);
            
            int catIndex = _categoryPicker.Items.IndexOf(_selectedCategory);
            _categoryPicker.SelectedIndex = catIndex >= 0 ? catIndex : 0;
            
            // Update channel picker
            var channels = videos
                .Select(v => v.Creator ?? "Unknown")
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
            
            _channelPicker.Items.Clear();
            _channelPicker.Items.Add("All");
            foreach (var channel in channels)
                _channelPicker.Items.Add(channel);
            
            int channelIndex = _channelPicker.Items.IndexOf(_selectedChannel);
            _channelPicker.SelectedIndex = channelIndex >= 0 ? channelIndex : 0;
        }
        finally
        {
            _isUpdatingPicker = false;
        }
    }

    private Frame BuildVideoCard(LearningVideo video, int creatorMonthlyCount = 0, int creatorMonthlyLimit = 10, bool isCreatorLocked = false)
    {
        var frame = new Frame
        {
            Padding = 0,
            CornerRadius = 10,
            BackgroundColor = isCreatorLocked && video.Status == "NotStarted"
                ? Color.FromArgb("#FFEBEE")  // Red tint - locked
                : video.Status == "Completed" 
                    ? Color.FromArgb("#E8F5E9")  // Green - watched
                    : video.Status == "ReadSummary"
                        ? Color.FromArgb("#E3F2FD")  // Blue - read summary
                        : video.Status == "PendingWatched"
                            ? Color.FromArgb("#F3E5F5")  // Purple - pending watched
                            : Colors.White,
            HasShadow = true,
            BorderColor = isCreatorLocked && video.Status == "NotStarted" 
                ? Color.FromArgb("#EF5350") 
                : Colors.Transparent
        };

        var mainGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 120 },  // Thumbnail
                new ColumnDefinition { Width = GridLength.Star },  // Info
                new ColumnDefinition { Width = GridLength.Auto }  // Buttons
            }
        };

        // Thumbnail - tappable to watch
        var thumbnailFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 10,
            IsClippedToBounds = true,
            HasShadow = false,
            BorderColor = Colors.Transparent
        };

        var thumbnailTap = new TapGestureRecognizer();
        thumbnailTap.Tapped += async (s, e) => await OpenVideoAsync(video);
        thumbnailFrame.GestureRecognizers.Add(thumbnailTap);

        string thumbnailUrl = !string.IsNullOrEmpty(video.VideoId)
            ? $"https://img.youtube.com/vi/{video.VideoId}/mqdefault.jpg"
            : "";

        if (!string.IsNullOrEmpty(thumbnailUrl))
        {
            var thumbnailGrid = new Grid();
            
            var thumbnail = new Image
            {
                Source = thumbnailUrl,
                Aspect = Aspect.AspectFill,
                HeightRequest = 68,
                WidthRequest = 120
            };
            thumbnailGrid.Children.Add(thumbnail);
            
            // Play overlay
            var playOverlay = new Label
            {
                Text = "▶",
                FontSize = 24,
                TextColor = Colors.White,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Shadow = new Shadow
                {
                    Brush = Colors.Black,
                    Offset = new Point(1, 1),
                    Radius = 3
                }
            };
            thumbnailGrid.Children.Add(playOverlay);
            
            thumbnailFrame.Content = thumbnailGrid;
        }
        else
        {
            // Placeholder for non-YouTube videos
            thumbnailFrame.BackgroundColor = Color.FromArgb("#E0E0E0");
            thumbnailFrame.Content = new Label
            {
                Text = "🎬",
                FontSize = 24,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            };
        }

        Grid.SetColumn(thumbnailFrame, 0);
        mainGrid.Children.Add(thumbnailFrame);

        // Video info - tappable to watch
        var infoStack = new VerticalStackLayout 
        { 
            Spacing = 4, 
            Padding = new Thickness(12, 8),
            VerticalOptions = LayoutOptions.Center 
        };
        
        var infoTap = new TapGestureRecognizer();
        infoTap.Tapped += async (s, e) => await OpenVideoAsync(video);
        infoStack.GestureRecognizers.Add(infoTap);
        
        // Title
        var titleLabel = new Label
        {
            Text = video.Title,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };
        infoStack.Children.Add(titleLabel);

        // Channel name with monthly watch count
        if (!string.IsNullOrEmpty(video.Creator))
        {
            var creatorRow = new HorizontalStackLayout { Spacing = 6 };
            
            // Locked icon if creator is locked
            if (isCreatorLocked && video.Status == "NotStarted")
            {
                creatorRow.Children.Add(new Label
                {
                    Text = "🔒",
                    FontSize = 11,
                    VerticalOptions = LayoutOptions.Center
                });
            }
            
            var creatorLabel = new Label
            {
                Text = video.Creator,
                FontSize = 11,
                TextColor = isCreatorLocked ? Color.FromArgb("#C62828") : Color.FromArgb("#666"),
                LineBreakMode = LineBreakMode.TailTruncation
            };
            creatorRow.Children.Add(creatorLabel);
            
            // Show creator monthly count badge (only in focus category context)
            if (creatorMonthlyCount > 0)
            {
                bool atLimit = creatorMonthlyCount >= creatorMonthlyLimit;
                bool nearLimit = creatorMonthlyCount >= creatorMonthlyLimit - 2;
                
                var countBadge = new Frame
                {
                    Padding = new Thickness(4, 1),
                    CornerRadius = 8,
                    BackgroundColor = atLimit ? Color.FromArgb("#FFCDD2") 
                                    : nearLimit ? Color.FromArgb("#FFF9C4")
                                    : Color.FromArgb("#E0E0E0"),
                    BorderColor = Colors.Transparent,
                    HasShadow = false,
                    Content = new Label
                    {
                        Text = $"{creatorMonthlyCount}/{creatorMonthlyLimit}",
                        FontSize = 9,
                        TextColor = atLimit ? Color.FromArgb("#C62828")
                                  : nearLimit ? Color.FromArgb("#F9A825")
                                  : Color.FromArgb("#666")
                    }
                };
                creatorRow.Children.Add(countBadge);
            }
            
            infoStack.Children.Add(creatorRow);
        }

        // Status row
        var statusRow = new HorizontalStackLayout { Spacing = 8 };
        
        var statusLabel = new Label
        {
            Text = video.Status == "Completed" ? "✅ Watched" 
                 : video.Status == "ReadSummary" ? "📖 Read Summary"
                 : video.Status == "PendingWatched" ? "👀 Pending"
                 : video.Status == "InProgress" ? "▶️ Watching" 
                 : "🎬 To Watch",
            FontSize = 10,
            TextColor = video.Status == "Completed" ? Color.FromArgb("#4CAF50")
                      : video.Status == "ReadSummary" ? Color.FromArgb("#2196F3")
                      : video.Status == "PendingWatched" ? Color.FromArgb("#9C27B0")
                      : video.Status == "InProgress" ? Color.FromArgb("#FF9800")
                      : Color.FromArgb("#999")
        };
        statusRow.Children.Add(statusLabel);

        // Category badge
        var categoryText = video.Category ?? "Unsorted";
        var categoryLabel = new Label
        {
            Text = $"📁 {categoryText}",
            FontSize = 9,
            TextColor = Color.FromArgb("#666"),
            BackgroundColor = Color.FromArgb("#F0F0F0"),
            Padding = new Thickness(4, 2),
        };
        statusRow.Children.Add(categoryLabel);

        infoStack.Children.Add(statusRow);

        Grid.SetColumn(infoStack, 1);
        mainGrid.Children.Add(infoStack);

        // Action buttons column
        var buttonStack = new VerticalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(8, 4),
            VerticalOptions = LayoutOptions.Center
        };

        // Row 1: Watch + Transcript + Watched buttons
        var row1 = new HorizontalStackLayout { Spacing = 4 };
        
        // Watch button
        var watchBtn = CreateHoverButton("▶", "#2196F3", "#1976D2", 12);
        watchBtn.Clicked += async (s, e) => await OpenVideoAsync(video);
        row1.Children.Add(watchBtn);

        // Transcript button (only for YouTube videos)
        if (!string.IsNullOrEmpty(video.VideoId))
        {
            var transcriptBtn = CreateHoverButton("📝", "#FF9800", "#F57C00", 11);
            transcriptBtn.Clicked += async (s, e) => await HandleTranscriptAsync(video);
            row1.Children.Add(transcriptBtn);
        }

        // Mark as watched button (only if not already watched)
        if (video.Status != "Completed")
        {
            var watchedBtn = CreateHoverButton("✓", "#4CAF50", "#388E3C", 14);
            watchedBtn.Clicked += async (s, e) =>
            {
                await _learning.CompleteVideoAsync(video.Id);
                await RecordVideoFocusCompletionAsync(video.Category ?? "Unsorted");
                await RefreshVideosAsync();
            };
            row1.Children.Add(watchedBtn);
        }

        buttonStack.Children.Add(row1);

        // Row 2: Category + Read Summary + Delete + Menu buttons
        var row2 = new HorizontalStackLayout { Spacing = 4 };

        // Category button
        var categoryBtn = CreateHoverButton("📁", "#607D8B", "#455A64", 11);
        categoryBtn.Clicked += async (s, e) => await ChangeCategoryAsync(video);
        row2.Children.Add(categoryBtn);

        // Read Summary button (only if not already read summary)
        if (video.Status != "ReadSummary" && video.Status != "Completed")
        {
            var readSummaryBtn = CreateHoverButton("📖", "#2196F3", "#1976D2", 11);
            readSummaryBtn.Clicked += async (s, e) =>
            {
                video.Status = "ReadSummary";
                await _learning.UpdateVideoAsync(video);
                await RefreshVideosAsync();
            };
            row2.Children.Add(readSummaryBtn);
        }

        // Delete button
        var deleteBtn = CreateHoverButton("🗑", "#F44336", "#D32F2F", 11);
        deleteBtn.Clicked += async (s, e) =>
        {
            bool confirm = await DisplayAlert("Delete Video", $"Delete '{video.Title}'?", "Delete", "Cancel");
            if (confirm)
            {
                await _learning.DeleteVideoAsync(video.Id);
                await RefreshVideosAsync();
            }
        };
        row2.Children.Add(deleteBtn);

        // More menu button
        var menuBtn = CreateHoverButton("⋮", "#9E9E9E", "#757575", 14);
        menuBtn.Clicked += async (s, e) => await ShowVideoMenuAsync(video);
        row2.Children.Add(menuBtn);

        buttonStack.Children.Add(row2);

        Grid.SetColumn(buttonStack, 2);
        mainGrid.Children.Add(buttonStack);

        frame.Content = mainGrid;
        return frame;
    }

    /// <summary>
    /// Create a button with hover effect for desktop
    /// </summary>
    private Button CreateHoverButton(string text, string normalColor, string hoverColor, int fontSize)
    {
        var normalBg = Color.FromArgb(normalColor);
        var hoverBg = Color.FromArgb(hoverColor);
        
        var btn = new Button
        {
            Text = text,
            BackgroundColor = normalBg,
            TextColor = Colors.White,
            FontSize = fontSize,
            WidthRequest = 36,
            HeightRequest = 32,
            Padding = 0,
            CornerRadius = 6
        };

        // Add hover effect using PointerGestureRecognizer (works on desktop)
        var pointerGesture = new PointerGestureRecognizer();
        pointerGesture.PointerEntered += (s, e) => btn.BackgroundColor = hoverBg;
        pointerGesture.PointerExited += (s, e) => btn.BackgroundColor = normalBg;
        btn.GestureRecognizers.Add(pointerGesture);

        return btn;
    }

    private async Task OpenVideoAsync(LearningVideo video)
    {
        // Check focus mode restrictions
        if (!await CheckVideoFocusAllowedAsync(video.Category ?? "Unsorted"))
            return;
        
        // Check creator monthly limit (only in focus category)
        var focus = GetVideoFocusSettings();
        if (focus.IsActive && (video.Category ?? "Unsorted") == focus.Category)
        {
            int count = await GetCreatorMonthlyWatchCountAsync(video.Creator, focus.Category);
            if (count >= focus.CreatorMonthlyLimit)
            {
                await DisplayAlert(
                    "🔒 Creator Locked",
                    $"You've watched {count} videos from '{video.Creator}' in '{focus.Category}' this month.\n\n" +
                    $"Monthly limit: {focus.CreatorMonthlyLimit}\n\n" +
                    "This creator is locked until next month.",
                    "OK");
                return;
            }
        }
        
        if (string.IsNullOrEmpty(video.Url) && string.IsNullOrEmpty(video.VideoId))
        {
            await DisplayAlert("No Link", "This video doesn't have a URL.", "OK");
            return;
        }

        string url = !string.IsNullOrEmpty(video.VideoId)
            ? $"https://www.youtube.com/watch?v={video.VideoId}"
            : video.Url;

        try
        {
            // Check if user wants speed script copied
            bool copySpeedScript = Preferences.Get("CopySpeedScript", false);
            
            if (copySpeedScript)
            {
                double speed = Preferences.Get("VideoPlaybackSpeed", 2.3);
                string script = $"setInterval(() => {{ const video = document.querySelector('video'); if (video && video.playbackRate !== {speed}) {{ video.playbackRate = {speed}; }} }}, 500);";
                await Clipboard.SetTextAsync(script);
            }

            await Launcher.OpenAsync(new Uri(url));
            
            // Move to PendingWatched if not already watched
            if (video.Status != "Completed" && video.Status != "PendingWatched" && video.Status != "ReadSummary")
            {
                video.Status = "PendingWatched";
                await _learning.UpdateVideoAsync(video);
                await RefreshVideosAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not open video: {ex.Message}", "OK");
        }
    }

    private async Task ShowVideoMenuAsync(LearningVideo video)
    {
        var options = new List<string>();
        
        if (!string.IsNullOrEmpty(video.Url) || !string.IsNullOrEmpty(video.VideoId))
            options.Add("▶️ Watch Video");
        
        // Transcript option for YouTube videos
        if (!string.IsNullOrEmpty(video.VideoId))
            options.Add("📝 Get Transcript & Summarize");
        
        if (video.Status != "Completed")
            options.Add("✅ Mark as Watched");
        if (video.Status != "ReadSummary")
            options.Add("📖 Mark as Read Summary");
        if (video.Status != "PendingWatched" && video.Status != "Completed" && video.Status != "ReadSummary")
            options.Add("👀 Mark as Pending Watched");
        if (video.Status != "InProgress")
            options.Add("🔄 Mark as Watching");
        if (video.Status != "NotStarted")
            options.Add("🎬 Mark as To Watch");
        
        options.Add("📁 Change Category");
        options.Add("⬆️ Move Up");
        options.Add("⬇️ Move Down");
        options.Add("✏️ Edit");
        options.Add("🗑️ Delete");

        var result = await DisplayActionSheet($"🎬 {video.Title}", "Cancel", null, options.ToArray());

        if (result == null || result == "Cancel") return;

        if (result == "▶️ Watch Video")
        {
            await OpenVideoAsync(video);
        }
        else if (result == "📝 Get Transcript & Summarize")
        {
            await HandleTranscriptAsync(video);
        }
        else if (result == "✅ Mark as Watched")
        {
            await _learning.CompleteVideoAsync(video.Id);
            await RecordVideoFocusCompletionAsync(video.Category ?? "Unsorted");
        }
        else if (result == "📖 Mark as Read Summary")
        {
            video.Status = "ReadSummary";
            await _learning.UpdateVideoAsync(video);
        }
        else if (result == "👀 Mark as Pending Watched")
        {
            video.Status = "PendingWatched";
            await _learning.UpdateVideoAsync(video);
        }
        else if (result == "🔄 Mark as Watching")
        {
            video.Status = "InProgress";
            await _learning.UpdateVideoAsync(video);
        }
        else if (result == "🎬 Mark as To Watch")
        {
            video.Status = "NotStarted";
            video.CompletedAt = null;
            await _learning.UpdateVideoAsync(video);
        }
        else if (result == "📁 Change Category")
        {
            await ChangeCategoryAsync(video);
        }
        else if (result == "⬆️ Move Up")
        {
            await _learning.MoveVideoUpAsync(_auth.CurrentUsername, video.Id);
        }
        else if (result == "⬇️ Move Down")
        {
            await _learning.MoveVideoDownAsync(_auth.CurrentUsername, video.Id);
        }
        else if (result == "✏️ Edit")
        {
            await EditVideoAsync(video);
        }
        else if (result == "🗑️ Delete")
        {
            bool confirm = await DisplayAlert("Delete Video", $"Delete '{video.Title}' from your list?", "Delete", "Cancel");
            if (confirm)
            {
                await _learning.DeleteVideoAsync(video.Id);
            }
        }

        await RefreshVideosAsync();
    }

    private async void OnAddVideoClicked(object sender, EventArgs e)
    {
        // First, ask for YouTube URL
        string url = await DisplayPromptAsync(
            "Add YouTube Video", 
            "Paste YouTube video URL:",
            placeholder: "https://youtube.com/watch?v=...",
            initialValue: "");

        if (string.IsNullOrWhiteSpace(url)) return;

        // Extract video ID
        string? videoId = ExtractYouTubeVideoId(url.Trim());
        
        if (string.IsNullOrEmpty(videoId))
        {
            await DisplayAlert("Invalid URL", "Could not extract video ID from the URL. Please paste a valid YouTube link.", "OK");
            return;
        }

        // Show loading indicator
        var loadingLabel = new Label
        {
            Text = "⏳ Fetching video info...",
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 20)
        };
        _videosStack.Children.Add(loadingLabel);

        // Fetch video info from YouTube
        var (title, channelName) = await FetchYouTubeVideoInfoAsync(videoId);
        
        _videosStack.Children.Remove(loadingLabel);

        // Show confirmation with fetched info
        bool confirm = await DisplayAlert(
            "Add Video?",
            $"📺 {title}\n👤 {channelName}",
            "Add",
            "Cancel");

        if (!confirm) return;

        // Check for channel mapping
        string category = GetCategoryForChannel(channelName);
        
        // Create the video
        var video = new LearningVideo
        {
            Username = _auth.CurrentUsername,
            Title = title,
            Url = url.Trim(),
            VideoId = videoId,
            Creator = channelName,
            Category = category
        };

        await _learning.AddVideoAsync(video);
        
        if (category != "Unsorted")
        {
            await DisplayAlert("Added", $"Video added to '{category}' (auto-categorized by channel)", "OK");
        }
        
        await RefreshVideosAsync();
    }

    private async Task EditVideoAsync(LearningVideo video)
    {
        string title = await DisplayPromptAsync("Edit Video", "Title:", initialValue: video.Title);
        if (string.IsNullOrWhiteSpace(title)) return;

        string creator = await DisplayPromptAsync("Edit Video", "Channel/Creator:", initialValue: video.Creator ?? "");

        video.Title = title.Trim();
        video.Creator = creator?.Trim() ?? "";
        await _learning.UpdateVideoAsync(video);
    }

    #region YouTube Helpers

    /// <summary>
    /// Extract video ID from various YouTube URL formats
    /// </summary>
    private string? ExtractYouTubeVideoId(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;

        try
        {
            url = url.Trim();

            // youtu.be format
            if (url.Contains("youtu.be/"))
            {
                var parts = url.Split("youtu.be/");
                if (parts.Length > 1)
                {
                    var id = parts[1].Split('?')[0].Split('&')[0].Split('/')[0];
                    if (id.Length == 11) return id;
                }
            }

            // youtube.com/watch?v= format
            if (url.Contains("v="))
            {
                var uri = new Uri(url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                var id = query["v"];
                if (!string.IsNullOrEmpty(id) && id.Length == 11) return id;
            }

            // youtube.com/embed/ format
            if (url.Contains("/embed/"))
            {
                var parts = url.Split("/embed/");
                if (parts.Length > 1)
                {
                    var id = parts[1].Split('?')[0].Split('&')[0].Split('/')[0];
                    if (id.Length == 11) return id;
                }
            }

            // youtube.com/shorts/ format
            if (url.Contains("/shorts/"))
            {
                var parts = url.Split("/shorts/");
                if (parts.Length > 1)
                {
                    var id = parts[1].Split('?')[0].Split('&')[0].Split('/')[0];
                    if (id.Length == 11) return id;
                }
            }

            // If it's just the video ID itself (11 characters)
            if (url.Length == 11 && !url.Contains("/") && !url.Contains("."))
            {
                return url;
            }
        }
        catch
        {
            // Failed to parse
        }

        return null;
    }

    /// <summary>
    /// Fetch video title and channel name from YouTube using oEmbed API (no API key required)
    /// </summary>
    private async Task<(string title, string channelName)> FetchYouTubeVideoInfoAsync(string videoId)
    {
        try
        {
            string videoUrl = $"https://www.youtube.com/watch?v={videoId}";
            string oEmbedUrl = $"https://www.youtube.com/oembed?url={Uri.EscapeDataString(videoUrl)}&format=json";

            System.Diagnostics.Debug.WriteLine($"[YOUTUBE] Fetching: {oEmbedUrl}");

            var response = await _httpClient.GetAsync(oEmbedUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[YOUTUBE] Response: {json}");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string title = root.TryGetProperty("title", out var titleProp) 
                    ? titleProp.GetString() ?? "Unknown Title" 
                    : "Unknown Title";
                    
                string channelName = root.TryGetProperty("author_name", out var authorProp) 
                    ? authorProp.GetString() ?? "Unknown Channel" 
                    : "Unknown Channel";

                return (title, channelName);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[YOUTUBE] Failed: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[YOUTUBE] Error: {ex.Message}");
        }

        return ("Video", "YouTube");
    }

    #endregion

    #region Transcript Helpers

    /// <summary>
    /// Handle transcript workflow: try to fetch, copy prompt, open AI tool
    /// </summary>
    private async Task HandleTranscriptAsync(LearningVideo video)
    {
        if (string.IsNullOrEmpty(video.VideoId))
        {
            await DisplayAlert("Error", "No video ID available for this video.", "OK");
            return;
        }

        // First, ask user what they want to do
        var action = await DisplayActionSheet(
            "📝 Transcript",
            "Cancel",
            null,
            "🔍 Auto-fetch transcript",
            "🌐 Open transcript website",
            "📋 Create summary prompt");

        if (action == null || action == "Cancel") return;

        if (action == "🔍 Auto-fetch transcript")
        {
            await TryFetchTranscriptAsync(video);
        }
        else if (action == "🌐 Open transcript website")
        {
            await OpenTranscriptToolAsync(video);
        }
        else if (action == "📋 Create summary prompt")
        {
            await CreateSummaryPromptAsync(video);
        }
    }

    /// <summary>
    /// Try to fetch transcript using a free API (tactiq)
    /// </summary>
    private async Task TryFetchTranscriptAsync(LearningVideo video)
    {
        try
        {
            // Show loading
            await DisplayAlert("Fetching...", "Trying to get transcript. This may take a moment...", "OK");

            // Try using a transcript API
            string transcriptUrl = $"https://tactiq-apps-prod.tactiq.io/transcript?videoId={video.VideoId}";
            
            var response = await _httpClient.GetAsync(transcriptUrl);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("captions", out var captions) && captions.GetArrayLength() > 0)
                {
                    // Extract transcript text
                    var transcriptBuilder = new System.Text.StringBuilder();
                    foreach (var caption in captions.EnumerateArray())
                    {
                        if (caption.TryGetProperty("text", out var textProp))
                        {
                            transcriptBuilder.AppendLine(textProp.GetString());
                        }
                    }

                    string transcript = transcriptBuilder.ToString().Trim();
                    
                    if (!string.IsNullOrEmpty(transcript))
                    {
                        // Create and copy the summary prompt
                        string prompt = $"Summarize this YouTube video transcript:\n\nVideo: {video.Title}\nChannel: {video.Creator}\n\n---TRANSCRIPT---\n{transcript}\n---END TRANSCRIPT---\n\nPlease provide:\n1. A brief summary (2-3 sentences)\n2. Key points (bullet list)\n3. Main takeaways";

                        await Clipboard.SetTextAsync(prompt);
                        
                        bool openAI = await DisplayAlert(
                            "✅ Transcript Copied!",
                            $"Summary prompt copied to clipboard!\n\nTranscript length: {transcript.Length} characters\n\nOpen an AI assistant to paste?",
                            "Open AI Tool",
                            "Done");

                        if (openAI)
                        {
                            await OpenAIToolAsync();
                        }
                        return;
                    }
                }
            }

            // If we get here, automatic fetch failed
            bool openTool = await DisplayAlert(
                "❌ Transcript Not Available",
                "Could not fetch transcript automatically. The video may not have captions enabled.\n\nWould you like to open a transcript tool website instead?",
                "Open Tool",
                "Cancel");

            if (openTool)
            {
                await OpenTranscriptToolAsync(video);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TRANSCRIPT] Error: {ex.Message}");
            
            bool openTool = await DisplayAlert(
                "❌ Error",
                $"Failed to fetch transcript: {ex.Message}\n\nWould you like to open a transcript tool website instead?",
                "Open Tool",
                "Cancel");

            if (openTool)
            {
                await OpenTranscriptToolAsync(video);
            }
        }
    }

    /// <summary>
    /// Open a transcript tool website
    /// </summary>
    private async Task OpenTranscriptToolAsync(LearningVideo video)
    {
        // Get configured transcript services
        var services = GetTranscriptServices();
        
        if (services.Count == 0)
        {
            await DisplayAlert("No Services", "No transcript services configured. Add one in settings.", "OK");
            return;
        }

        var options = services.Select(s => $"📄 {s.Name}").ToList();
        options.Add("⚙️ Manage Services");

        var tool = await DisplayActionSheet(
            "Choose Transcript Tool",
            "Cancel",
            null,
            options.ToArray());

        if (tool == null || tool == "Cancel") return;

        if (tool == "⚙️ Manage Services")
        {
            await ManageTranscriptServicesAsync();
            return;
        }

        // Find selected service
        string selectedName = tool.Replace("📄 ", "");
        var service = services.FirstOrDefault(s => s.Name == selectedName);
        
        if (service == null) return;

        string videoUrl = $"https://www.youtube.com/watch?v={video.VideoId}";
        
        // Copy video URL to clipboard for easy pasting
        await Clipboard.SetTextAsync(videoUrl);
        
        try
        {
            await Launcher.OpenAsync(new Uri(service.Url));
            
            // Ask if they want to create summary prompt after getting transcript
            bool createPrompt = await DisplayAlert(
                "Link Copied!",
                "Video URL copied to clipboard. Paste it on the website.\n\nAfter copying the transcript, create a summary prompt?",
                "Create Prompt",
                "Done");

            if (createPrompt)
            {
                await CreateSummaryPromptAsync(video);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not open browser: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Get list of transcript services from preferences
    /// </summary>
    private List<TranscriptService> GetTranscriptServices()
    {
        string json = Preferences.Get("TranscriptServices", "");
        
        if (string.IsNullOrEmpty(json))
        {
            // Default service
            var defaults = new List<TranscriptService>
            {
                new TranscriptService { Name = "Downsub", Url = "https://downsub.com/" }
            };
            SaveTranscriptServices(defaults);
            return defaults;
        }

        try
        {
            return JsonSerializer.Deserialize<List<TranscriptService>>(json) ?? new List<TranscriptService>();
        }
        catch
        {
            return new List<TranscriptService>();
        }
    }

    /// <summary>
    /// Save transcript services to preferences
    /// </summary>
    private void SaveTranscriptServices(List<TranscriptService> services)
    {
        string json = JsonSerializer.Serialize(services);
        Preferences.Set("TranscriptServices", json);
    }

    /// <summary>
    /// Manage transcript services (add/remove)
    /// </summary>
    private async Task ManageTranscriptServicesAsync()
    {
        var services = GetTranscriptServices();
        
        var options = new List<string>();
        foreach (var service in services)
        {
            options.Add($"❌ Remove: {service.Name}");
        }
        options.Add("➕ Add New Service");

        var result = await DisplayActionSheet("Manage Transcript Services", "Done", null, options.ToArray());

        if (result == null || result == "Done") return;

        if (result == "➕ Add New Service")
        {
            string name = await DisplayPromptAsync("Add Service", "Service name:", placeholder: "e.g., Tactiq");
            if (string.IsNullOrWhiteSpace(name)) return;

            string url = await DisplayPromptAsync("Add Service", "Service URL:", placeholder: "https://...", initialValue: "https://");
            if (string.IsNullOrWhiteSpace(url)) return;

            services.Add(new TranscriptService { Name = name.Trim(), Url = url.Trim() });
            SaveTranscriptServices(services);
            
            await DisplayAlert("Added", $"'{name}' has been added.", "OK");
        }
        else if (result.StartsWith("❌ Remove:"))
        {
            string nameToRemove = result.Replace("❌ Remove: ", "");
            services.RemoveAll(s => s.Name == nameToRemove);
            SaveTranscriptServices(services);
            
            await DisplayAlert("Removed", $"'{nameToRemove}' has been removed.", "OK");
        }

        // Show menu again
        await ManageTranscriptServicesAsync();
    }

    /// <summary>
    /// Simple class for transcript service
    /// </summary>
    private class TranscriptService
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
    }

    /// <summary>
    /// Create a summary prompt for pasting into AI tools
    /// </summary>
    private async Task CreateSummaryPromptAsync(LearningVideo video)
    {
        // Get transcript from clipboard or prompt user to paste it
        string? clipboardContent = await Clipboard.GetTextAsync();
        
        bool useClipboard = false;
        if (!string.IsNullOrEmpty(clipboardContent) && clipboardContent.Length > 100)
        {
            useClipboard = await DisplayAlert(
                "Use Clipboard?",
                $"Found {clipboardContent.Length} characters in clipboard. Use this as the transcript?",
                "Yes, Use It",
                "No, I'll Paste Later");
        }

        string prompt;
        if (useClipboard && !string.IsNullOrEmpty(clipboardContent))
        {
            prompt = $"Summarize this YouTube video transcript:\n\nVideo: {video.Title}\nChannel: {video.Creator}\n\n---TRANSCRIPT---\n{clipboardContent}\n---END TRANSCRIPT---\n\nPlease provide:\n1. A brief summary (2-3 sentences)\n2. Key points (bullet list)\n3. Main takeaways";
        }
        else
        {
            prompt = $"Summarize this YouTube video transcript:\n\nVideo: {video.Title}\nChannel: {video.Creator}\n\n---TRANSCRIPT---\n[PASTE YOUR TRANSCRIPT HERE]\n---END TRANSCRIPT---\n\nPlease provide:\n1. A brief summary (2-3 sentences)\n2. Key points (bullet list)\n3. Main takeaways";
        }

        await Clipboard.SetTextAsync(prompt);

        bool openAI = await DisplayAlert(
            "📋 Prompt Copied!",
            "Summary prompt has been copied to clipboard.\n\nOpen an AI assistant to paste?",
            "Open AI Tool",
            "Done");

        if (openAI)
        {
            await OpenAIToolAsync();
        }
    }

    /// <summary>
    /// Open an AI tool for summarization
    /// </summary>
    private async Task OpenAIToolAsync()
    {
        var tool = await DisplayActionSheet(
            "Choose AI Tool",
            "Cancel",
            null,
            "🤖 Claude (claude.ai)",
            "🤖 ChatGPT (chat.openai.com)",
            "🤖 Gemini (gemini.google.com)",
            "🤖 Perplexity (perplexity.ai)");

        if (tool == null || tool == "Cancel") return;

        string toolUrl = tool switch
        {
            "🤖 Claude (claude.ai)" => "https://claude.ai",
            "🤖 ChatGPT (chat.openai.com)" => "https://chat.openai.com",
            "🤖 Gemini (gemini.google.com)" => "https://gemini.google.com",
            "🤖 Perplexity (perplexity.ai)" => "https://perplexity.ai",
            _ => ""
        };

        if (!string.IsNullOrEmpty(toolUrl))
        {
            try
            {
                await Launcher.OpenAsync(new Uri(toolUrl));
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"Could not open browser: {ex.Message}", "OK");
            }
        }
    }

    #endregion

    #region Category Management

    /// <summary>
    /// Change category for a video
    /// </summary>
    private async Task ChangeCategoryAsync(LearningVideo video)
    {
        var categories = GetCategories()
            .OrderBy(c => c == "Unsorted" ? "ZZZ" : c) // Unsorted at end
            .ToList();
        
        var options = new List<string>();
        foreach (var cat in categories)
        {
            string prefix = (video.Category ?? "Unsorted") == cat ? "✓ " : "";
            options.Add($"{prefix}{cat}");
        }
        options.Add("➕ New Category");

        var result = await DisplayActionSheet("Select Category", "Cancel", null, options.ToArray());

        if (result == null || result == "Cancel") return;

        if (result == "➕ New Category")
        {
            string newCat = await DisplayPromptAsync("New Category", "Enter category name:");
            if (string.IsNullOrWhiteSpace(newCat)) return;

            newCat = newCat.Trim();
            AddCategory(newCat);
            video.Category = newCat;
        }
        else
        {
            video.Category = result.Replace("✓ ", "");
        }

        await _learning.UpdateVideoAsync(video);
        await RefreshVideosAsync();
    }

    /// <summary>
    /// Get list of categories from preferences
    /// </summary>
    private List<string> GetCategories()
    {
        string json = Preferences.Get("VideoCategories", "");
        
        if (string.IsNullOrEmpty(json))
        {
            var defaults = new List<string> { "Unsorted" };
            SaveCategories(defaults);
            return defaults;
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string> { "Unsorted" };
        }
        catch
        {
            return new List<string> { "Unsorted" };
        }
    }

    /// <summary>
    /// Save categories to preferences
    /// </summary>
    private void SaveCategories(List<string> categories)
    {
        string json = JsonSerializer.Serialize(categories);
        Preferences.Set("VideoCategories", json);
    }

    /// <summary>
    /// Add a new category
    /// </summary>
    private void AddCategory(string category)
    {
        var categories = GetCategories();
        if (!categories.Contains(category))
        {
            categories.Add(category);
            SaveCategories(categories);
        }
    }

    /// <summary>
    /// Manage categories (add/remove)
    /// </summary>
    private async void OnManageCategoriesClicked(object sender, EventArgs e)
    {
        var categories = GetCategories();
        
        var options = new List<string>();
        foreach (var cat in categories)
        {
            if (cat != "Unsorted") // Can't remove Unsorted
                options.Add($"❌ Remove: {cat}");
        }
        options.Add("➕ Add New Category");

        var result = await DisplayActionSheet("Manage Categories", "Done", null, options.ToArray());

        if (result == null || result == "Done") return;

        if (result == "➕ Add New Category")
        {
            string name = await DisplayPromptAsync("Add Category", "Category name:");
            if (!string.IsNullOrWhiteSpace(name))
            {
                AddCategory(name.Trim());
                await DisplayAlert("Added", $"'{name}' has been added.", "OK");
            }
        }
        else if (result.StartsWith("❌ Remove:"))
        {
            string catToRemove = result.Replace("❌ Remove: ", "");
            
            // Move videos in this category to Unsorted
            var videos = await _learning.GetVideosAsync(_auth.CurrentUsername);
            foreach (var video in videos.Where(v => v.Category == catToRemove))
            {
                video.Category = "Unsorted";
                await _learning.UpdateVideoAsync(video);
            }
            
            categories.Remove(catToRemove);
            SaveCategories(categories);
            
            await DisplayAlert("Removed", $"'{catToRemove}' removed. Videos moved to Unsorted.", "OK");
            await RefreshVideosAsync();
        }

        // Show menu again
        OnManageCategoriesClicked(sender, e);
    }

    /// <summary>
    /// Video settings menu
    /// </summary>
    private async void OnVideoSettingsClicked(object sender, EventArgs e)
    {
        var focus = GetVideoFocusSettings();
        string focusOption = focus.IsActive 
            ? $"🎯 Focus Mode: {focus.Category} ({focus.CompletedInCategory}/{focus.GoalCount})"
            : "🎯 Set Focus Mode";
        
        string creatorLimitOption = $"👤 Creator Monthly Limit: {focus.CreatorMonthlyLimit}";
        
        var result = await DisplayActionSheet(
            "⚙️ Video Settings",
            "Done",
            null,
            focusOption,
            "📊 Creator Stats This Month",
            creatorLimitOption,
            "📁 Manage Categories",
            "🔗 Channel → Category Mappings",
            "🔄 Reset Filters");

        if (result == null || result == "Done") return;

        if (result == focusOption)
        {
            await SetupVideoFocusAsync();
        }
        else if (result == "📊 Creator Stats This Month")
        {
            await ShowCreatorMonthlyStatsAsync();
        }
        else if (result == creatorLimitOption)
        {
            await SetCreatorMonthlyLimitAsync();
        }
        else if (result == "📁 Manage Categories")
        {
            await ManageCategoriesAsync();
        }
        else if (result == "🔗 Channel → Category Mappings")
        {
            await ManageChannelMappingsAsync();
        }
        else if (result == "🔄 Reset Filters")
        {
            _selectedCategory = "All";
            _selectedChannel = "All";
            _selectedStatus = "All";
            _statusPicker.SelectedIndex = 0;
            await RefreshVideosAsync();
        }
    }

    /// <summary>
    /// Manage categories
    /// </summary>
    private async Task ManageCategoriesAsync()
    {
        var categories = GetCategories();
        
        var options = new List<string>();
        foreach (var cat in categories)
        {
            if (cat != "Unsorted")
                options.Add($"❌ Remove: {cat}");
        }
        options.Add("➕ Add New Category");

        var result = await DisplayActionSheet("Manage Categories", "Done", null, options.ToArray());

        if (result == null || result == "Done") return;

        if (result == "➕ Add New Category")
        {
            string name = await DisplayPromptAsync("Add Category", "Category name:");
            if (!string.IsNullOrWhiteSpace(name))
            {
                AddCategory(name.Trim());
                await DisplayAlert("Added", $"'{name}' has been added.", "OK");
            }
        }
        else if (result.StartsWith("❌ Remove:"))
        {
            string catToRemove = result.Replace("❌ Remove: ", "");
            
            var videos = await _learning.GetVideosAsync(_auth.CurrentUsername);
            foreach (var video in videos.Where(v => v.Category == catToRemove))
            {
                video.Category = "Unsorted";
                await _learning.UpdateVideoAsync(video);
            }
            
            categories.Remove(catToRemove);
            SaveCategories(categories);
            
            await DisplayAlert("Removed", $"'{catToRemove}' removed. Videos moved to Unsorted.", "OK");
            await RefreshVideosAsync();
        }

        await ManageCategoriesAsync();
    }

    /// <summary>
    /// Manage channel to category mappings
    /// </summary>
    private async Task ManageChannelMappingsAsync()
    {
        var mappings = GetChannelMappings();
        var categories = GetCategories()
            .OrderBy(c => c == "Unsorted" ? "ZZZ" : c)
            .ToList();
        
        var options = new List<string>();
        options.Add("➕ Add Channel Mapping");
        
        foreach (var mapping in mappings.OrderBy(m => m.Key))
        {
            options.Add($"❌ {mapping.Key} → {mapping.Value}");
        }

        var result = await DisplayActionSheet("Channel → Category Mappings", "Done", null, options.ToArray());

        if (result == null || result == "Done") return;

        if (result == "➕ Add Channel Mapping")
        {
            // Get list of channels from existing videos
            var videos = await _learning.GetVideosAsync(_auth.CurrentUsername);
            var channels = videos
                .Select(v => v.Creator)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            if (channels.Count == 0)
            {
                await DisplayAlert("No Channels", "Add some videos first to see available channels.", "OK");
                return;
            }

            // Select channel
            var channelOptions = channels.Select(c => c!).ToArray();
            var selectedChannel = await DisplayActionSheet("Select Channel", "Cancel", null, channelOptions);
            
            if (selectedChannel == null || selectedChannel == "Cancel") return;

            // Select category (already sorted alphabetically)
            var categoryOptions = categories.ToArray();
            var selectedCategory = await DisplayActionSheet($"Category for '{selectedChannel}'", "Cancel", null, categoryOptions);
            
            if (selectedCategory == null || selectedCategory == "Cancel") return;

            // Save mapping
            mappings[selectedChannel] = selectedCategory;
            SaveChannelMappings(mappings);
            
            // Ask to apply to existing videos
            bool applyExisting = await DisplayAlert(
                "Apply to Existing?",
                $"Move all existing videos from '{selectedChannel}' to '{selectedCategory}'?",
                "Yes",
                "No");

            if (applyExisting)
            {
                foreach (var video in videos.Where(v => v.Creator == selectedChannel))
                {
                    video.Category = selectedCategory;
                    await _learning.UpdateVideoAsync(video);
                }
                await RefreshVideosAsync();
            }

            await DisplayAlert("Saved", $"'{selectedChannel}' will be auto-categorized as '{selectedCategory}'", "OK");
        }
        else if (result.StartsWith("❌"))
        {
            // Remove mapping
            var parts = result.Substring(2).Split(" → ");
            if (parts.Length == 2)
            {
                mappings.Remove(parts[0].Trim());
                SaveChannelMappings(mappings);
                await DisplayAlert("Removed", "Channel mapping removed.", "OK");
            }
        }

        await ManageChannelMappingsAsync();
    }

    /// <summary>
    /// Get channel to category mappings
    /// </summary>
    private Dictionary<string, string> GetChannelMappings()
    {
        string json = Preferences.Get("ChannelCategoryMappings", "");
        
        if (string.IsNullOrEmpty(json))
            return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Save channel to category mappings
    /// </summary>
    private void SaveChannelMappings(Dictionary<string, string> mappings)
    {
        string json = JsonSerializer.Serialize(mappings);
        Preferences.Set("ChannelCategoryMappings", json);
    }

    /// <summary>
    /// Get category for a channel based on mappings
    /// </summary>
    private string GetCategoryForChannel(string? channel)
    {
        if (string.IsNullOrEmpty(channel))
            return "Unsorted";

        var mappings = GetChannelMappings();
        return mappings.TryGetValue(channel, out var category) ? category : "Unsorted";
    }

    /// <summary>
    /// Show creator monthly stats - how many videos watched per creator this month in focus category
    /// </summary>
    private async Task ShowCreatorMonthlyStatsAsync()
    {
        var focus = GetVideoFocusSettings();
        
        if (!focus.IsActive)
        {
            await DisplayAlert("No Focus", "Set a focus category first to track creator limits.", "OK");
            return;
        }
        
        var videos = await _learning.GetVideosAsync(_auth.CurrentUsername);
        
        // Get start of current month
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        
        // Count completed videos per creator this month IN FOCUS CATEGORY
        var creatorCounts = videos
            .Where(v => v.Status == "Completed" && 
                       v.CompletedAt >= monthStart &&
                       (v.Category ?? "Unsorted") == focus.Category)
            .GroupBy(v => v.Creator ?? "Unknown")
            .Select(g => new { Creator = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Creator)
            .ToList();
        
        if (creatorCounts.Count == 0)
        {
            await DisplayAlert("Creator Stats", $"No videos watched in '{focus.Category}' this month yet.", "OK");
            return;
        }
        
        // Build display with limit indicators
        var tcs = new TaskCompletionSource<bool>();
        
        var contentStack = new VerticalStackLayout { Spacing = 8, Padding = 16 };
        
        contentStack.Children.Add(new Label
        {
            Text = $"📊 Creator Stats - {DateTime.Now:MMMM yyyy}",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        });
        
        contentStack.Children.Add(new Label
        {
            Text = $"Focus: {focus.Category} | Limit: {focus.CreatorMonthlyLimit}/creator",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center
        });
        
        contentStack.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#DDD") });
        
        var scroll = new ScrollView { HeightRequest = 400 };
        var statsStack = new VerticalStackLayout { Spacing = 6 };
        
        foreach (var item in creatorCounts)
        {
            bool atLimit = item.Count >= focus.CreatorMonthlyLimit;
            bool nearLimit = item.Count >= focus.CreatorMonthlyLimit - 2;
            
            var row = new HorizontalStackLayout { Spacing = 8 };
            
            row.Children.Add(new Label
            {
                Text = atLimit ? "🔒" : nearLimit ? "🟡" : "🟢",
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            });
            
            row.Children.Add(new Label
            {
                Text = item.Creator,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center,
                TextColor = atLimit ? Color.FromArgb("#D32F2F") : Colors.Black
            });
            
            row.Children.Add(new Label
            {
                Text = $"({item.Count}/{focus.CreatorMonthlyLimit})",
                FontSize = 12,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#666")
            });
            
            if (atLimit)
            {
                row.Children.Add(new Label
                {
                    Text = "LOCKED",
                    FontSize = 10,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#D32F2F"),
                    VerticalOptions = LayoutOptions.Center
                });
            }
            
            statsStack.Children.Add(row);
        }
        
        scroll.Content = statsStack;
        contentStack.Children.Add(scroll);
        
        var closeBtn = new Button
        {
            Text = "Close",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Margin = new Thickness(0, 12, 0, 0)
        };
        closeBtn.Clicked += (s, e) => tcs.TrySetResult(true);
        contentStack.Children.Add(closeBtn);
        
        var overlayPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Content = contentStack
        };
        
        await Navigation.PushModalAsync(overlayPage, false);
        await tcs.Task;
        await Navigation.PopModalAsync(false);
    }

    /// <summary>
    /// Set the monthly limit for watching videos from the same creator
    /// </summary>
    private async Task SetCreatorMonthlyLimitAsync()
    {
        var focus = GetVideoFocusSettings();
        
        string? result = await DisplayPromptAsync(
            "Creator Monthly Limit",
            "Max videos to watch per creator per month:",
            initialValue: focus.CreatorMonthlyLimit.ToString(),
            keyboard: Keyboard.Numeric);
        
        if (string.IsNullOrEmpty(result)) return;
        
        if (int.TryParse(result, out int limit) && limit > 0)
        {
            focus.CreatorMonthlyLimit = limit;
            SaveVideoFocusSettings(focus);
            await DisplayAlert("Saved", $"Creator limit set to {limit} videos per month.", "OK");
        }
        else
        {
            await DisplayAlert("Invalid", "Enter a positive number.", "OK");
        }
    }

    /// <summary>
    /// Get watch count for a creator this month within a specific category
    /// </summary>
    private async Task<int> GetCreatorMonthlyWatchCountAsync(string? creator, string? category = null)
    {
        if (string.IsNullOrEmpty(creator)) return 0;
        
        var videos = await _learning.GetVideosAsync(_auth.CurrentUsername);
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        
        var query = videos.Where(v => 
            v.Creator == creator && 
            v.Status == "Completed" && 
            v.CompletedAt >= monthStart);
        
        // If category specified, filter by it
        if (!string.IsNullOrEmpty(category))
        {
            query = query.Where(v => (v.Category ?? "Unsorted") == category);
        }
        
        return query.Count();
    }

    /// <summary>
    /// Check if creator has reached monthly limit in focus category
    /// </summary>
    private async Task<bool> CheckCreatorLimitAsync(string? creator, string? category = null)
    {
        if (string.IsNullOrEmpty(creator)) return false;
        
        var focus = GetVideoFocusSettings();
        int count = await GetCreatorMonthlyWatchCountAsync(creator, category);
        
        return count >= focus.CreatorMonthlyLimit;
    }
    
    /// <summary>
    /// Get set of locked creators (at monthly limit) for the focus category
    /// </summary>
    private async Task<HashSet<string>> GetLockedCreatorsAsync()
    {
        var focus = GetVideoFocusSettings();
        if (!focus.IsActive) return new HashSet<string>();
        
        var videos = await _learning.GetVideosAsync(_auth.CurrentUsername);
        var monthStart = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        
        // Count videos per creator in focus category this month
        var creatorCounts = videos
            .Where(v => v.Status == "Completed" && 
                       v.CompletedAt >= monthStart &&
                       (v.Category ?? "Unsorted") == focus.Category)
            .GroupBy(v => v.Creator ?? "Unknown")
            .Where(g => g.Count() >= focus.CreatorMonthlyLimit)
            .Select(g => g.Key)
            .ToHashSet();
        
        return creatorCounts;
    }

    #endregion

    #region Import Links

    /// <summary>
    /// Import videos by extracting YouTube links from any text
    /// </summary>
    private async void OnImportLinksClicked(object sender, EventArgs e)
    {
        // Create custom popup with multi-line Editor
        var tcs = new TaskCompletionSource<string?>();
        
        var editor = new Editor
        {
            Placeholder = "Paste text with YouTube URLs...",
            HeightRequest = 200,
            BackgroundColor = Colors.White,
            FontSize = 12
        };
        
        // Remove line breaks as user types/pastes
        editor.TextChanged += (s, args) =>
        {
            if (args.NewTextValue != null && (args.NewTextValue.Contains('\n') || args.NewTextValue.Contains('\r')))
            {
                editor.Text = args.NewTextValue.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
            }
        };

        var importBtn = new Button
        {
            Text = "Import",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            WidthRequest = 100
        };
        
        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 8,
            WidthRequest = 100
        };

        var popup = new Frame
        {
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            Padding = 20,
            HasShadow = true,
            WidthRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label
                    {
                        Text = "Import YouTube Links",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333")
                    },
                    new Label
                    {
                        Text = "Paste any text containing YouTube links:",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#666")
                    },
                    new Frame
                    {
                        Padding = 2,
                        BorderColor = Color.FromArgb("#DDD"),
                        CornerRadius = 6,
                        HasShadow = false,
                        Content = editor
                    },
                    new HorizontalStackLayout
                    {
                        Spacing = 12,
                        HorizontalOptions = LayoutOptions.End,
                        Children = { cancelBtn, importBtn }
                    }
                }
            }
        };

        // Create overlay with tap to dismiss background
        var backgroundTap = new TapGestureRecognizer();
        backgroundTap.Tapped += (s, args) => tcs.TrySetResult(null);
        
        var overlay = new ContentView
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Content = popup
        };
        overlay.GestureRecognizers.Add(backgroundTap);

        // Prevent popup tap from dismissing
        var popupTap = new TapGestureRecognizer();
        popupTap.Tapped += (s, args) => { }; // Do nothing, just consume the tap
        popup.GestureRecognizers.Add(popupTap);

        importBtn.Clicked += (s, args) => tcs.TrySetResult(editor.Text);
        cancelBtn.Clicked += (s, args) => tcs.TrySetResult(null);

        // Show overlay using Navigation
        var overlayPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Content = popup
        };

        await Navigation.PushModalAsync(overlayPage, false);
        
        string? input = await tcs.Task;
        
        await Navigation.PopModalAsync(false);

        if (string.IsNullOrWhiteSpace(input)) return;

        await ProcessImportAsync(input);
    }

    /// <summary>
    /// Process the import after getting input
    /// </summary>
    private async Task ProcessImportAsync(string input)
    {

        // Extract all YouTube URLs from the text
        var urlPattern = new Regex(
            @"(https?://)?(www\.)?(youtube\.com/watch\?v=|youtu\.be/|youtube\.com/shorts/)([a-zA-Z0-9_-]{11})",
            RegexOptions.IgnoreCase);

        var matches = urlPattern.Matches(input);
        var videoIds = new HashSet<string>(); // Use HashSet to avoid duplicates
        
        foreach (Match match in matches)
        {
            string videoId = match.Groups[4].Value;
            if (videoId.Length == 11)
            {
                videoIds.Add(videoId);
            }
        }

        if (videoIds.Count == 0)
        {
            await DisplayAlert("No Videos Found", "Could not find any YouTube links in the pasted text.", "OK");
            return;
        }

        bool confirm = await DisplayAlert(
            "Import Videos",
            $"Found {videoIds.Count} YouTube video(s).\n\nImport and categorize them?",
            "Import",
            "Cancel");

        if (!confirm) return;

        // Show loading
        var loadingLabel = new Label
        {
            Text = $"⏳ Importing {videoIds.Count} videos...",
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 20)
        };
        _videosStack.Children.Add(loadingLabel);

        // Import all videos
        var importedVideos = new List<LearningVideo>();
        int count = 0;
        int autoMapped = 0;
        
        foreach (var videoId in videoIds)
        {
            count++;
            loadingLabel.Text = $"⏳ Fetching video {count}/{videoIds.Count}...";
            
            // Fetch video info
            var (title, channelName) = await FetchYouTubeVideoInfoAsync(videoId);
            
            // Check for channel mapping
            string category = GetCategoryForChannel(channelName);
            if (category != "Unsorted") autoMapped++;
            
            var video = new LearningVideo
            {
                Username = _auth.CurrentUsername,
                Title = title,
                Url = $"https://www.youtube.com/watch?v={videoId}",
                VideoId = videoId,
                Creator = channelName,
                Category = category
            };

            await _learning.AddVideoAsync(video);
            importedVideos.Add(video);
        }

        _videosStack.Children.Remove(loadingLabel);

        string message = autoMapped > 0
            ? $"Imported {importedVideos.Count} videos!\n({autoMapped} auto-categorized by channel)"
            : $"Successfully imported {importedVideos.Count} videos!";
        await DisplayAlert("Imported", message, "OK");

        // Only ask to categorize if there are unsorted videos
        var unsortedVideos = importedVideos.Where(v => v.Category == "Unsorted").ToList();
        if (unsortedVideos.Count > 0)
        {
            bool categorize = await DisplayAlert(
                "Categorize?",
                $"{unsortedVideos.Count} videos are unsorted. Categorize now?",
                "Yes",
                "Later");

            if (categorize)
            {
                await CategorizeVideosSequentiallyAsync(unsortedVideos);
            }
        }
        
        await RefreshVideosAsync();
    }

    /// <summary>
    /// Categorize imported videos one by one
    /// </summary>
    private async Task CategorizeVideosSequentiallyAsync(List<LearningVideo> videos)
    {
        var categories = GetCategories()
            .OrderBy(c => c == "Unsorted" ? "ZZZ" : c)
            .ToList();
        int current = 0;
        int total = videos.Count;

        foreach (var video in videos)
        {
            current++;
            
            var options = new List<string>();
            foreach (var cat in categories)
            {
                options.Add(cat);
            }
            options.Add("➕ New Category");
            options.Add("⏭️ Skip");
            options.Add("⏹️ Stop");

            var result = await DisplayActionSheet(
                $"[{current}/{total}] {video.Title}",
                null,
                null,
                options.ToArray());

            if (result == "⏹️ Stop")
            {
                break;
            }
            else if (result == "⏭️ Skip" || result == null)
            {
                continue;
            }
            else if (result == "➕ New Category")
            {
                string newCat = await DisplayPromptAsync("New Category", "Enter category name:");
                if (!string.IsNullOrWhiteSpace(newCat))
                {
                    newCat = newCat.Trim();
                    AddCategory(newCat);
                    categories = GetCategories()
                        .OrderBy(c => c == "Unsorted" ? "ZZZ" : c)
                        .ToList();
                    video.Category = newCat;
                    await _learning.UpdateVideoAsync(video);
                }
            }
            else
            {
                video.Category = result;
                await _learning.UpdateVideoAsync(video);
            }
        }
    }

    #endregion

    #region Focus System

    /// <summary>
    /// Get video focus settings
    /// </summary>
    private FocusSettings GetVideoFocusSettings()
    {
        string json = Preferences.Get("VideoFocusSettings", "");
        if (string.IsNullOrEmpty(json))
            return new FocusSettings();
        
        try
        {
            return JsonSerializer.Deserialize<FocusSettings>(json) ?? new FocusSettings();
        }
        catch
        {
            return new FocusSettings();
        }
    }

    /// <summary>
    /// Save video focus settings
    /// </summary>
    private void SaveVideoFocusSettings(FocusSettings settings)
    {
        string json = JsonSerializer.Serialize(settings);
        Preferences.Set("VideoFocusSettings", json);
    }

    /// <summary>
    /// Get book focus settings
    /// </summary>
    private FocusSettings GetBookFocusSettings()
    {
        string json = Preferences.Get("BookFocusSettings", "");
        if (string.IsNullOrEmpty(json))
            return new FocusSettings();
        
        try
        {
            return JsonSerializer.Deserialize<FocusSettings>(json) ?? new FocusSettings();
        }
        catch
        {
            return new FocusSettings();
        }
    }

    /// <summary>
    /// Save book focus settings
    /// </summary>
    private void SaveBookFocusSettings(FocusSettings settings)
    {
        string json = JsonSerializer.Serialize(settings);
        Preferences.Set("BookFocusSettings", json);
    }

    /// <summary>
    /// Update video focus panel display
    /// </summary>
    private void UpdateVideoFocusPanel()
    {
        var focus = GetVideoFocusSettings();
        
        if (!focus.IsActive)
        {
            _videoFocusFrame.IsVisible = false;
            return;
        }
        
        _videoFocusFrame.IsVisible = true;
        
        // Calculate if user can watch non-focus video
        bool canWatchOther = CanWatchNonFocusVideo(focus);
        string statusIcon = canWatchOther ? "✅" : "🔒";
        
        // Update streak
        UpdateFocusStreak(focus, "video");
        
        _videoFocusStatusLabel.Text = 
            $"🎯 Focus: {focus.Category} | " +
            $"📊 {focus.CompletedInCategory}/{focus.GoalCount} | " +
            $"⚖️ {focus.FocusedInWindow}/{focus.FocusedRequired} in {focus.TotalInWindow}/{focus.TotalWindow} {statusIcon} | " +
            $"🔥 {focus.StreakDays} day streak";
    }

    /// <summary>
    /// Update book focus panel display
    /// </summary>
    private void UpdateBookFocusPanel()
    {
        var focus = GetBookFocusSettings();
        
        if (!focus.IsActive)
        {
            _bookFocusFrame.IsVisible = false;
            return;
        }
        
        _bookFocusFrame.IsVisible = true;
        
        bool canReadOther = CanWatchNonFocusVideo(focus);
        string statusIcon = canReadOther ? "✅" : "🔒";
        
        UpdateFocusStreak(focus, "book");
        
        _bookFocusStatusLabel.Text = 
            $"🎯 Focus: {focus.Category} | " +
            $"📊 {focus.CompletedInCategory}/{focus.GoalCount} | " +
            $"⚖️ {focus.FocusedInWindow}/{focus.FocusedRequired} in {focus.TotalInWindow}/{focus.TotalWindow} {statusIcon} | " +
            $"🔥 {focus.StreakDays} day streak";
    }

    /// <summary>
    /// Check if user can watch/read non-focus content
    /// </summary>
    private bool CanWatchNonFocusVideo(FocusSettings focus)
    {
        if (!focus.IsActive) return true;
        
        // Check if goal is complete
        if (focus.CompletedInCategory >= focus.GoalCount) return true;
        
        // Check ratio: if we've watched enough focused content in the window
        // e.g., 2 in 3: after watching 2 focused, can watch 1 non-focused
        // The window resets after TotalWindow items
        
        if (focus.TotalInWindow >= focus.TotalWindow)
        {
            // Window complete, check if ratio was met
            return focus.FocusedInWindow >= focus.FocusedRequired;
        }
        
        // Mid-window: can watch non-focus if we've met the requirement already
        return focus.FocusedInWindow >= focus.FocusedRequired;
    }

    /// <summary>
    /// Update streak based on last activity date
    /// </summary>
    private void UpdateFocusStreak(FocusSettings focus, string type)
    {
        if (focus.LastCompletedDate == null) return;
        
        var today = DateTime.Today;
        var lastDate = focus.LastCompletedDate.Value.Date;
        
        // If last activity was today, streak is current
        if (lastDate == today) return;
        
        // If last activity was yesterday, streak continues (will be updated on next completion)
        if (lastDate == today.AddDays(-1)) return;
        
        // If more than 1 day gap, streak should have been reset
        // (This would be caught when they violated)
    }

    /// <summary>
    /// Record a video completion for focus tracking
    /// </summary>
    private async Task RecordVideoFocusCompletionAsync(string category)
    {
        var focus = GetVideoFocusSettings();
        if (!focus.IsActive) return;
        
        // Check if "Videos by Focus" activity exists, if not prompt to create
        await EnsureVideosFocusActivityExistsAsync();
        
        bool isFocusCategory = category == focus.Category;
        
        focus.TotalInWindow++;
        if (isFocusCategory)
        {
            focus.FocusedInWindow++;
            focus.CompletedInCategory++;
        }
        
        // Reset window if complete
        if (focus.TotalInWindow >= focus.TotalWindow)
        {
            // Check if ratio was violated
            if (focus.FocusedInWindow < focus.FocusedRequired)
            {
                // Violated! Reset streak
                focus.StreakDays = 0;
                focus.StreakStartDate = null;
            }
            
            // Reset window
            focus.TotalInWindow = 0;
            focus.FocusedInWindow = 0;
        }
        
        // Update streak
        var today = DateTime.Today;
        if (focus.LastCompletedDate?.Date != today)
        {
            if (focus.LastCompletedDate?.Date == today.AddDays(-1))
            {
                focus.StreakDays++;
            }
            else if (focus.StreakStartDate == null)
            {
                focus.StreakDays = 1;
                focus.StreakStartDate = today;
            }
        }
        focus.LastCompletedDate = DateTime.Now;
        
        SaveVideoFocusSettings(focus);
        UpdateVideoFocusPanel();
    }
    
    /// <summary>
    /// Ensure Videos by Focus activity exists, prompt to create if not
    /// </summary>
    private async Task EnsureVideosFocusActivityExistsAsync()
    {
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);
        var learningGame = games.FirstOrDefault(g => g.DisplayName == "Learning");
        if (learningGame == null) return;
        
        var activities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, learningGame.GameId);
        bool hasVideosActivity = activities.Any(a => a.Name == "Videos by Focus");
        
        if (!hasVideosActivity)
        {
            bool create = await DisplayAlert(
                "Create Activity",
                "The 'Videos by Focus' activity doesn't exist yet. Create it now to track EXP?",
                "Create",
                "Skip");
            
            if (create)
            {
                await ActivityCreationPage.CreateActivityModalAsync(
                    Navigation,
                    _auth,
                    _activities,
                    _games,
                    learningGame.GameId,
                    prefillName: "Videos by Focus");
            }
        }
    }

    /// <summary>
    /// Record a book completion for focus tracking
    /// </summary>
    private async Task RecordBookFocusCompletionAsync(string category)
    {
        var focus = GetBookFocusSettings();
        if (!focus.IsActive) return;
        
        // Check if "Books by Focus" activity exists, if not prompt to create
        await EnsureBooksFocusActivityExistsAsync();
        
        bool isFocusCategory = category == focus.Category;
        
        focus.TotalInWindow++;
        if (isFocusCategory)
        {
            focus.FocusedInWindow++;
            focus.CompletedInCategory++;
        }
        
        if (focus.TotalInWindow >= focus.TotalWindow)
        {
            if (focus.FocusedInWindow < focus.FocusedRequired)
            {
                focus.StreakDays = 0;
                focus.StreakStartDate = null;
            }
            focus.TotalInWindow = 0;
            focus.FocusedInWindow = 0;
        }
        
        var today = DateTime.Today;
        if (focus.LastCompletedDate?.Date != today)
        {
            if (focus.LastCompletedDate?.Date == today.AddDays(-1))
            {
                focus.StreakDays++;
            }
            else if (focus.StreakStartDate == null)
            {
                focus.StreakDays = 1;
                focus.StreakStartDate = today;
            }
        }
        focus.LastCompletedDate = DateTime.Now;
        
        SaveBookFocusSettings(focus);
        UpdateBookFocusPanel();
    }
    
    /// <summary>
    /// Ensure Books by Focus activity exists, prompt to create if not
    /// </summary>
    private async Task EnsureBooksFocusActivityExistsAsync()
    {
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);
        var learningGame = games.FirstOrDefault(g => g.DisplayName == "Learning");
        if (learningGame == null) return;
        
        var activities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, learningGame.GameId);
        bool hasBooksActivity = activities.Any(a => a.Name == "Books by Focus");
        
        if (!hasBooksActivity)
        {
            bool create = await DisplayAlert(
                "Create Activity",
                "The 'Books by Focus' activity doesn't exist yet. Create it now to track EXP?",
                "Create",
                "Skip");
            
            if (create)
            {
                await ActivityCreationPage.CreateActivityModalAsync(
                    Navigation,
                    _auth,
                    _activities,
                    _games,
                    learningGame.GameId,
                    prefillName: "Books by Focus");
            }
        }
    }

    /// <summary>
    /// Check if user can open a video (blocking if focus violated)
    /// </summary>
    private async Task<bool> CheckVideoFocusAllowedAsync(string category)
    {
        var focus = GetVideoFocusSettings();
        if (!focus.IsActive) return true;
        if (focus.CompletedInCategory >= focus.GoalCount) return true;
        if (category == focus.Category) return true;
        
        if (!CanWatchNonFocusVideo(focus))
        {
            int needed = focus.FocusedRequired - focus.FocusedInWindow;
            await DisplayAlert(
                "🔒 Focus Mode Active",
                $"Watch {needed} more '{focus.Category}' video(s) before watching other categories.\n\n" +
                $"Current: {focus.FocusedInWindow}/{focus.FocusedRequired} in window of {focus.TotalInWindow}/{focus.TotalWindow}",
                "OK");
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Check if user can open a book (blocking if focus violated)
    /// </summary>
    private async Task<bool> CheckBookFocusAllowedAsync(string category)
    {
        var focus = GetBookFocusSettings();
        if (!focus.IsActive) return true;
        if (focus.CompletedInCategory >= focus.GoalCount) return true;
        if (category == focus.Category) return true;
        
        if (!CanWatchNonFocusVideo(focus))
        {
            int needed = focus.FocusedRequired - focus.FocusedInWindow;
            await DisplayAlert(
                "🔒 Focus Mode Active",
                $"Complete {needed} more '{focus.Category}' book(s) before reading other categories.\n\n" +
                $"Current: {focus.FocusedInWindow}/{focus.FocusedRequired} in window of {focus.TotalInWindow}/{focus.TotalWindow}",
                "OK");
            return false;
        }
        
        return true;
    }

    /// <summary>
    /// Setup video focus mode
    /// </summary>
    private async Task SetupVideoFocusAsync()
    {
        var categories = GetCategories()
            .Where(c => c != "Unsorted")
            .OrderBy(c => c)
            .ToList();
        
        if (categories.Count == 0)
        {
            await DisplayAlert("No Categories", "Create some categories first before setting up focus mode.", "OK");
            return;
        }
        
        // Select category
        var categoryOptions = categories.ToArray();
        var selectedCategory = await DisplayActionSheet("Select Focus Category", "Cancel", null, categoryOptions);
        if (selectedCategory == null || selectedCategory == "Cancel") return;
        
        // Select ratio
        var ratioOptions = new[] { "1 in 2", "2 in 3", "3 in 4", "4 in 5", "5 in 6", "1 in 3", "1 in 4", "2 in 5", "3 in 5" };
        var selectedRatio = await DisplayActionSheet($"Ratio for '{selectedCategory}'", "Cancel", null, ratioOptions);
        if (selectedRatio == null || selectedRatio == "Cancel") return;
        
        // Parse ratio
        var parts = selectedRatio.Split(" in ");
        int focused = int.Parse(parts[0]);
        int total = int.Parse(parts[1]);
        
        // Select goal
        string goalStr = await DisplayPromptAsync(
            "Focus Goal",
            $"How many '{selectedCategory}' videos to complete?",
            initialValue: "100",
            keyboard: Keyboard.Numeric);
        if (string.IsNullOrEmpty(goalStr)) return;
        if (!int.TryParse(goalStr, out int goal) || goal <= 0)
        {
            await DisplayAlert("Invalid", "Enter a positive number.", "OK");
            return;
        }
        
        var focus = GetVideoFocusSettings();
        focus.IsActive = true;
        focus.Category = selectedCategory;
        focus.FocusedRequired = focused;
        focus.TotalWindow = total;
        focus.GoalCount = goal;
        
        // Reset progress if category changed
        if (focus.Category != selectedCategory)
        {
            focus.CompletedInCategory = 0;
            focus.FocusedInWindow = 0;
            focus.TotalInWindow = 0;
            focus.StreakDays = 0;
            focus.StreakStartDate = null;
        }
        
        SaveVideoFocusSettings(focus);
        UpdateVideoFocusPanel();
        
        await DisplayAlert("Focus Set", 
            $"Focus mode active!\n\n" +
            $"Category: {selectedCategory}\n" +
            $"Ratio: {focused} in {total}\n" +
            $"Goal: {goal} videos", 
            "OK");
    }

    /// <summary>
    /// Setup book focus mode
    /// </summary>
    private async Task SetupBookFocusAsync()
    {
        // For books, we'll use a simple text prompt for category since books don't have categories yet
        string category = await DisplayPromptAsync(
            "Focus Category",
            "Enter the category/genre to focus on:",
            placeholder: "e.g., Business, Self-Help, Psychology");
        if (string.IsNullOrWhiteSpace(category)) return;
        
        var ratioOptions = new[] { "1 in 2", "2 in 3", "3 in 4", "4 in 5", "5 in 6", "1 in 3", "1 in 4", "2 in 5", "3 in 5" };
        var selectedRatio = await DisplayActionSheet($"Ratio for '{category}'", "Cancel", null, ratioOptions);
        if (selectedRatio == null || selectedRatio == "Cancel") return;
        
        var parts = selectedRatio.Split(" in ");
        int focused = int.Parse(parts[0]);
        int total = int.Parse(parts[1]);
        
        string goalStr = await DisplayPromptAsync(
            "Focus Goal",
            $"How many '{category}' books to complete?",
            initialValue: "20",
            keyboard: Keyboard.Numeric);
        if (string.IsNullOrEmpty(goalStr)) return;
        if (!int.TryParse(goalStr, out int goal) || goal <= 0)
        {
            await DisplayAlert("Invalid", "Enter a positive number.", "OK");
            return;
        }
        
        var focus = new FocusSettings
        {
            IsActive = true,
            Category = category.Trim(),
            FocusedRequired = focused,
            TotalWindow = total,
            GoalCount = goal
        };
        
        SaveBookFocusSettings(focus);
        UpdateBookFocusPanel();
        
        await DisplayAlert("Focus Set", 
            $"Focus mode active!\n\n" +
            $"Category: {category}\n" +
            $"Ratio: {focused} in {total}\n" +
            $"Goal: {goal} books", 
            "OK");
    }

    /// <summary>
    /// Reset video focus streak
    /// </summary>
    private async Task ResetVideoFocusStreakAsync()
    {
        bool confirm = await DisplayAlert(
            "Reset Streak",
            "Reset your focus streak to 0 days?\n\nDo this if you violated the focus rule.",
            "Reset",
            "Cancel");
        
        if (!confirm) return;
        
        var focus = GetVideoFocusSettings();
        focus.StreakDays = 0;
        focus.StreakStartDate = null;
        focus.FocusedInWindow = 0;
        focus.TotalInWindow = 0;
        SaveVideoFocusSettings(focus);
        UpdateVideoFocusPanel();
    }

    /// <summary>
    /// Reset book focus streak
    /// </summary>
    private async Task ResetBookFocusStreakAsync()
    {
        bool confirm = await DisplayAlert(
            "Reset Streak",
            "Reset your focus streak to 0 days?\n\nDo this if you violated the focus rule.",
            "Reset",
            "Cancel");
        
        if (!confirm) return;
        
        var focus = GetBookFocusSettings();
        focus.StreakDays = 0;
        focus.StreakStartDate = null;
        focus.FocusedInWindow = 0;
        focus.TotalInWindow = 0;
        SaveBookFocusSettings(focus);
        UpdateBookFocusPanel();
    }

    /// <summary>
    /// Clear video focus mode
    /// </summary>
    private async Task ClearVideoFocusAsync()
    {
        bool confirm = await DisplayAlert(
            "Clear Focus",
            "Disable focus mode? Your progress will be saved.",
            "Clear",
            "Cancel");
        
        if (!confirm) return;
        
        var focus = GetVideoFocusSettings();
        focus.IsActive = false;
        SaveVideoFocusSettings(focus);
        UpdateVideoFocusPanel();
    }

    /// <summary>
    /// Clear book focus mode
    /// </summary>
    private async Task ClearBookFocusAsync()
    {
        bool confirm = await DisplayAlert(
            "Clear Focus",
            "Disable focus mode? Your progress will be saved.",
            "Clear",
            "Cancel");
        
        if (!confirm) return;
        
        var focus = GetBookFocusSettings();
        focus.IsActive = false;
        SaveBookFocusSettings(focus);
        UpdateBookFocusPanel();
    }

    /// <summary>
    /// Edit video focus numbers manually
    /// </summary>
    private async Task EditVideoFocusNumbersAsync()
    {
        var focus = GetVideoFocusSettings();
        if (!focus.IsActive) return;
        
        var options = new[]
        {
            $"📊 Completed in Category: {focus.CompletedInCategory}",
            $"🎯 Goal Count: {focus.GoalCount}",
            $"⚖️ Focused in Window: {focus.FocusedInWindow}",
            $"📦 Total in Window: {focus.TotalInWindow}",
            $"🔥 Streak Days: {focus.StreakDays}"
        };
        
        var result = await DisplayActionSheet("Edit Focus Numbers", "Done", null, options);
        
        if (result == null || result == "Done") return;
        
        string? newValue = null;
        
        if (result.StartsWith("📊"))
        {
            newValue = await DisplayPromptAsync("Completed in Category", 
                $"Videos completed in '{focus.Category}':", 
                initialValue: focus.CompletedInCategory.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val >= 0)
                focus.CompletedInCategory = val;
        }
        else if (result.StartsWith("🎯"))
        {
            newValue = await DisplayPromptAsync("Goal Count", 
                "Total goal:", 
                initialValue: focus.GoalCount.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val > 0)
                focus.GoalCount = val;
        }
        else if (result.StartsWith("⚖️"))
        {
            newValue = await DisplayPromptAsync("Focused in Window", 
                $"Focused videos in current window (max {focus.FocusedRequired}):", 
                initialValue: focus.FocusedInWindow.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val >= 0)
                focus.FocusedInWindow = Math.Min(val, focus.TotalWindow);
        }
        else if (result.StartsWith("📦"))
        {
            newValue = await DisplayPromptAsync("Total in Window", 
                $"Total videos in current window (max {focus.TotalWindow}):", 
                initialValue: focus.TotalInWindow.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val >= 0)
                focus.TotalInWindow = Math.Min(val, focus.TotalWindow);
        }
        else if (result.StartsWith("🔥"))
        {
            newValue = await DisplayPromptAsync("Streak Days", 
                "Days without violating focus:", 
                initialValue: focus.StreakDays.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val >= 0)
                focus.StreakDays = val;
        }
        
        SaveVideoFocusSettings(focus);
        UpdateVideoFocusPanel();
        
        // Show menu again
        await EditVideoFocusNumbersAsync();
    }

    /// <summary>
    /// Edit book focus numbers manually
    /// </summary>
    private async Task EditBookFocusNumbersAsync()
    {
        var focus = GetBookFocusSettings();
        if (!focus.IsActive) return;
        
        var options = new[]
        {
            $"📊 Completed in Category: {focus.CompletedInCategory}",
            $"🎯 Goal Count: {focus.GoalCount}",
            $"⚖️ Focused in Window: {focus.FocusedInWindow}",
            $"📦 Total in Window: {focus.TotalInWindow}",
            $"🔥 Streak Days: {focus.StreakDays}"
        };
        
        var result = await DisplayActionSheet("Edit Focus Numbers", "Done", null, options);
        
        if (result == null || result == "Done") return;
        
        string? newValue = null;
        
        if (result.StartsWith("📊"))
        {
            newValue = await DisplayPromptAsync("Completed in Category", 
                $"Books completed in '{focus.Category}':", 
                initialValue: focus.CompletedInCategory.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val >= 0)
                focus.CompletedInCategory = val;
        }
        else if (result.StartsWith("🎯"))
        {
            newValue = await DisplayPromptAsync("Goal Count", 
                "Total goal:", 
                initialValue: focus.GoalCount.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val > 0)
                focus.GoalCount = val;
        }
        else if (result.StartsWith("⚖️"))
        {
            newValue = await DisplayPromptAsync("Focused in Window", 
                $"Focused books in current window (max {focus.FocusedRequired}):", 
                initialValue: focus.FocusedInWindow.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val >= 0)
                focus.FocusedInWindow = Math.Min(val, focus.TotalWindow);
        }
        else if (result.StartsWith("📦"))
        {
            newValue = await DisplayPromptAsync("Total in Window", 
                $"Total books in current window (max {focus.TotalWindow}):", 
                initialValue: focus.TotalInWindow.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val >= 0)
                focus.TotalInWindow = Math.Min(val, focus.TotalWindow);
        }
        else if (result.StartsWith("🔥"))
        {
            newValue = await DisplayPromptAsync("Streak Days", 
                "Days without violating focus:", 
                initialValue: focus.StreakDays.ToString(),
                keyboard: Keyboard.Numeric);
            if (int.TryParse(newValue, out int val) && val >= 0)
                focus.StreakDays = val;
        }
        
        SaveBookFocusSettings(focus);
        UpdateBookFocusPanel();
        
        // Show menu again
        await EditBookFocusNumbersAsync();
    }

    #endregion
}

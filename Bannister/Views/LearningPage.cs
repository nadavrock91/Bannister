using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Learning hub page with Books and Videos sections
/// </summary>
public class LearningPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly LearningService _learning;
    
    // UI Controls
    private VerticalStackLayout _booksStack;
    private VerticalStackLayout _videosStack;
    private Button _btnBooks;
    private Button _btnVideos;
    private Frame _booksFrame;
    private Frame _videosFrame;
    private string _currentTab = "Books";

    public LearningPage(AuthService auth, LearningService learning)
    {
        _auth = auth;
        _learning = learning;
        
        Title = "Learning";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshDataAsync();
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
        
        // Add book button
        var addBookBtn = new Button
        {
            Text = "+ Add Book",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Margin = new Thickness(12, 12, 12, 8)
        };
        addBookBtn.Clicked += OnAddBookClicked;
        booksContainer.Children.Add(addBookBtn);

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
        
        // Add video button
        var addVideoBtn = new Button
        {
            Text = "+ Add Video",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Margin = new Thickness(12, 12, 12, 8)
        };
        addVideoBtn.Clicked += OnAddVideoClicked;
        videosContainer.Children.Add(addVideoBtn);

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
        
        var videos = await _learning.GetVideosAsync(_auth.CurrentUsername);
        
        if (videos.Count == 0)
        {
            _videosStack.Children.Add(new Label
            {
                Text = "No videos added yet.\nTap '+ Add Video' to start your watch list!",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            });
            return;
        }

        int order = 1;
        foreach (var video in videos)
        {
            var card = BuildVideoCard(video, order++);
            _videosStack.Children.Add(card);
        }
    }

    private Frame BuildVideoCard(LearningVideo video, int order)
    {
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = video.Status == "Completed" 
                ? Color.FromArgb("#E8F5E9") 
                : video.Status == "InProgress" 
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
            TextColor = Color.FromArgb("#FF5722"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(orderLabel, 0);
        grid.Children.Add(orderLabel);

        // Video info
        var infoStack = new VerticalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.Center };
        
        var titleLabel = new Label
        {
            Text = video.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.TailTruncation
        };
        infoStack.Children.Add(titleLabel);

        if (!string.IsNullOrEmpty(video.Creator))
        {
            var creatorLabel = new Label
            {
                Text = video.Creator,
                FontSize = 12,
                TextColor = Color.FromArgb("#666")
            };
            infoStack.Children.Add(creatorLabel);
        }

        var statusRow = new HorizontalStackLayout { Spacing = 8 };
        
        var statusLabel = new Label
        {
            Text = video.Status == "Completed" ? "✅ Watched" 
                 : video.Status == "InProgress" ? "▶️ Watching" 
                 : "🎬 To Watch",
            FontSize = 11,
            TextColor = video.Status == "Completed" ? Color.FromArgb("#4CAF50")
                      : video.Status == "InProgress" ? Color.FromArgb("#FF9800")
                      : Color.FromArgb("#999")
        };
        statusRow.Children.Add(statusLabel);

        if (video.DurationMinutes.HasValue)
        {
            var durationLabel = new Label
            {
                Text = $"⏱ {video.DurationMinutes}min",
                FontSize = 10,
                TextColor = Color.FromArgb("#999")
            };
            statusRow.Children.Add(durationLabel);
        }

        infoStack.Children.Add(statusRow);

        Grid.SetColumn(infoStack, 1);
        grid.Children.Add(infoStack);

        // Action buttons
        var buttonStack = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };

        // Open link button
        if (!string.IsNullOrEmpty(video.Url))
        {
            var linkBtn = new Button
            {
                Text = "🔗",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#2196F3"),
                FontSize = 16,
                WidthRequest = 36,
                HeightRequest = 36,
                Padding = 0
            };
            linkBtn.Clicked += async (s, e) =>
            {
                try
                {
                    await Launcher.OpenAsync(new Uri(video.Url));
                }
                catch
                {
                    await DisplayAlert("Error", "Could not open the video link.", "OK");
                }
            };
            buttonStack.Children.Add(linkBtn);
        }

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
        menuBtn.Clicked += async (s, e) => await ShowVideoMenuAsync(video);
        buttonStack.Children.Add(menuBtn);

        Grid.SetColumn(buttonStack, 2);
        grid.Children.Add(buttonStack);

        frame.Content = grid;
        return frame;
    }

    private async Task ShowVideoMenuAsync(LearningVideo video)
    {
        var options = new List<string>();
        
        if (!string.IsNullOrEmpty(video.Url))
            options.Add("🔗 Open Video");
        
        if (video.Status != "InProgress")
            options.Add("▶️ Mark as Watching");
        if (video.Status != "Completed")
            options.Add("✅ Mark as Watched");
        if (video.Status != "NotStarted")
            options.Add("🎬 Mark as To Watch");
        
        options.Add("⬆️ Move Up");
        options.Add("⬇️ Move Down");
        options.Add("✏️ Edit");
        options.Add("🗑️ Delete");

        var result = await DisplayActionSheet($"🎬 {video.Title}", "Cancel", null, options.ToArray());

        if (result == null || result == "Cancel") return;

        if (result == "🔗 Open Video")
        {
            try
            {
                await Launcher.OpenAsync(new Uri(video.Url));
            }
            catch
            {
                await DisplayAlert("Error", "Could not open the video link.", "OK");
            }
        }
        else if (result == "▶️ Mark as Watching")
        {
            video.Status = "InProgress";
            await _learning.UpdateVideoAsync(video);
        }
        else if (result == "✅ Mark as Watched")
        {
            await _learning.CompleteVideoAsync(video.Id);
        }
        else if (result == "🎬 Mark as To Watch")
        {
            video.Status = "NotStarted";
            video.CompletedAt = null;
            await _learning.UpdateVideoAsync(video);
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
        string title = await DisplayPromptAsync("Add Video", "Enter video title:");
        if (string.IsNullOrWhiteSpace(title)) return;

        string url = await DisplayPromptAsync("Add Video", "Enter video URL:", initialValue: "https://");
        
        string creator = await DisplayPromptAsync("Add Video", "Enter channel/creator (optional):", initialValue: "");

        var video = new LearningVideo
        {
            Username = _auth.CurrentUsername,
            Title = title.Trim(),
            Url = url?.Trim() ?? "",
            Creator = creator?.Trim() ?? ""
        };

        await _learning.AddVideoAsync(video);
        await RefreshVideosAsync();
    }

    private async Task EditVideoAsync(LearningVideo video)
    {
        string title = await DisplayPromptAsync("Edit Video", "Title:", initialValue: video.Title);
        if (string.IsNullOrWhiteSpace(title)) return;

        string url = await DisplayPromptAsync("Edit Video", "URL:", initialValue: video.Url ?? "");
        string creator = await DisplayPromptAsync("Edit Video", "Creator:", initialValue: video.Creator ?? "");

        video.Title = title.Trim();
        video.Url = url?.Trim() ?? "";
        video.Creator = creator?.Trim() ?? "";
        await _learning.UpdateVideoAsync(video);
    }
}

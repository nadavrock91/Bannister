using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ClipsExperimentPage : ContentPage
{
    private readonly ClipsExperimentService _service;
    private VerticalStackLayout _content = null!;
    private Entry _nameEntry = null!;
    private Entry _totalPostsEntry = null!;
    private Entry _titleEntry = null!;
    private Entry _conceptEntry = null!;
    private DatePicker _datePicker = null!;
    private int? _expandedPostId;
    private bool _busy;

    public ClipsExperimentPage(ClipsExperimentService service)
    {
        _service = service;
        Title = "AI Clips Experiment";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void BuildUI()
    {
        _content = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 12
        };
        _content.Children.Add(new Label
        {
            Text = "🎬 AI Clips Experiment",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });
        _content.Children.Add(new Label
        {
            Text = "Test short-video hook concepts and compare viewer retention.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        Content = new ScrollView { Content = _content };
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        await _service.InitAsync();
        var experiment = await _service.GetActiveExperimentAsync();
        _content.Children.Clear();
        _content.Children.Add(new Label
        {
            Text = "🎬 AI Clips Experiment",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });

        if (experiment == null)
        {
            BuildCreateExperimentSection();
            return;
        }

        var posts = await _service.GetPostsAsync(experiment.Id);
        BuildHeader(experiment, posts);
        BuildNewPostSection(experiment);
        BuildPostsSection(experiment, posts);
        await BuildAveragesSectionAsync(experiment, posts);
    }

    private void BuildCreateExperimentSection()
    {
        _content.Children.Add(new Label
        {
            Text = "No active experiment",
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 12, 0, 0)
        });
        _nameEntry = MakeEntry("Experiment name, e.g. Hook Concepts Q4");
        _totalPostsEntry = MakeEntry("Target posts", true);
        _content.Children.Add(_nameEntry);
        _content.Children.Add(_totalPostsEntry);
        var create = MakeButton("Create Experiment", "#1565C0", Colors.White);
        create.Clicked += async (_, _) =>
        {
            if (_busy || string.IsNullOrWhiteSpace(_nameEntry.Text)) return;
            int total = int.TryParse(_totalPostsEntry.Text, out var parsed) && parsed > 0 ? parsed : 10;
            _busy = true;
            try
            {
                await _service.SaveExperimentAsync(new ClipsExperiment
                {
                    Name = _nameEntry.Text.Trim(),
                    TotalPosts = total,
                    CreatedDate = DateTime.UtcNow
                });
            }
            finally { _busy = false; }
            await RefreshAsync();
        };
        _content.Children.Add(create);
    }

    private void BuildHeader(ClipsExperiment experiment, List<ClipsPost> posts)
    {
        int completed = posts.Count(IsComplete);
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        header.Add(new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label { Text = experiment.Name, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") },
                new Label { Text = $"{completed} of {experiment.TotalPosts} posts completed", FontSize = 13, TextColor = Color.FromArgb("#666") }
            }
        }, 0, 0);
        if (completed >= experiment.TotalPosts && experiment.TotalPosts > 0)
        {
            var archive = MakeButton("Archive", "#E0E0E0", Color.FromArgb("#555"));
            archive.Clicked += async (_, _) =>
            {
                if (!await DisplayAlert("Archive Experiment", "Archive this clips experiment?", "Archive", "Cancel")) return;
                experiment.IsArchived = true;
                await _service.SaveExperimentAsync(experiment);
                await RefreshAsync();
            };
            header.Add(archive, 1, 0);
        }
        _content.Children.Add(header);
    }

    private void BuildNewPostSection(ClipsExperiment experiment)
    {
        _content.Children.Add(new Label
        {
            Text = "New Post",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 12, 0, 0)
        });
        _titleEntry = MakeEntry("Post title");
        _conceptEntry = MakeEntry("Hook concept");
        _datePicker = new DatePicker { Date = DateTime.Today, Format = "dd MMM yyyy", HorizontalOptions = LayoutOptions.Start };
        _content.Children.Add(_titleEntry);
        _content.Children.Add(_conceptEntry);
        _content.Children.Add(_datePicker);
        var save = MakeButton("Save Post", "#2E7D32", Colors.White);
        save.Clicked += async (_, _) =>
        {
            if (_busy || string.IsNullOrWhiteSpace(_titleEntry.Text) || string.IsNullOrWhiteSpace(_conceptEntry.Text)) return;
            _busy = true;
            try
            {
                await _service.SavePostAsync(new ClipsPost
                {
                    ExperimentId = experiment.Id,
                    Title = _titleEntry.Text.Trim(),
                    Concept = _conceptEntry.Text.Trim(),
                    PostedDate = _datePicker.Date
                });
            }
            finally { _busy = false; }
            await RefreshAsync();
        };
        _content.Children.Add(save);
    }

    private void BuildPostsSection(ClipsExperiment experiment, List<ClipsPost> posts)
    {
        _content.Children.Add(new Label
        {
            Text = $"Posts ({posts.Count})",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 12, 0, 0)
        });
        if (posts.Count == 0)
        {
            _content.Children.Add(new Label { Text = "No posts recorded yet.", FontSize = 13, TextColor = Color.FromArgb("#999") });
            return;
        }
        foreach (var post in posts)
            _content.Children.Add(BuildPostCard(post));
    }

    private View BuildPostCard(ClipsPost post)
    {
        bool complete = IsComplete(post);
        var stack = new VerticalStackLayout { Spacing = 4 };
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        var title = new Label
        {
            Text = $"{post.Title}\n{post.Concept}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = complete ? Color.FromArgb("#777") : Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        header.Add(title, 0, 0);
        header.Add(new Label
        {
            Text = complete ? "✓ Complete" : post.PostedDate.ToString("dd MMM yyyy"),
            FontSize = 11,
            TextColor = complete ? Color.FromArgb("#2E7D32") : Color.FromArgb("#888"),
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            _expandedPostId = _expandedPostId == post.Id ? null : post.Id;
            _ = RefreshAsync();
        };
        header.GestureRecognizers.Add(tap);
        stack.Children.Add(header);
        var retentionParts = new List<string>();
        if (post.Retention10s.HasValue)
            retentionParts.Add($"10s: {post.Retention10s.Value:F0}%");
        if (post.Retention20s.HasValue)
            retentionParts.Add($"20s: {post.Retention20s.Value:F0}%");
        if (post.Retention30s.HasValue)
            retentionParts.Add($"30s: {post.Retention30s.Value:F0}%");
        if (retentionParts.Count > 0)
            stack.Children.Add(new Label
            {
                Text = string.Join(" · ", retentionParts),
                FontSize = 11,
                TextColor = Color.FromArgb("#666")
            });
        if (_expandedPostId == post.Id)
            AddPostEditor(stack, post);
        return new Frame
        {
            Content = stack,
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = complete ? Color.FromArgb("#FAFAFA") : Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false
        };
    }

    private void AddPostEditor(VerticalStackLayout stack, ClipsPost post)
    {
        var r10 = MakeEntry("10s retention (0-100)", true, post.Retention10s);
        var r20 = MakeEntry("20s retention (0-100)", true, post.Retention20s);
        var r30 = MakeEntry("30s retention (0-100)", true, post.Retention30s);
        var notes = MakeEntry("Notes");
        notes.Text = post.Notes;
        stack.Children.Add(r10);
        stack.Children.Add(r20);
        stack.Children.Add(r30);
        stack.Children.Add(notes);
        var save = MakeButton("Save Retention", "#1565C0", Colors.White);
        save.Clicked += async (_, _) =>
        {
            if (_busy) return;
            _busy = true;
            try
            {
                post.Retention10s = ParseScore(r10.Text);
                post.Retention20s = ParseScore(r20.Text);
                post.Retention30s = ParseScore(r30.Text);
                post.Notes = notes.Text?.Trim() ?? "";
                await _service.SavePostAsync(post);
                _expandedPostId = null;
            }
            finally { _busy = false; }
            await RefreshAsync();
        };
        stack.Children.Add(save);
    }

    private async Task BuildAveragesSectionAsync(ClipsExperiment experiment, List<ClipsPost> posts)
    {
        var averages = await _service.GetConceptAveragesAsync(experiment.Id);
        _content.Children.Add(new Label
        {
            Text = "Concept Averages",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 12, 0, 0)
        });
        if (averages.Count == 0)
        {
            _content.Children.Add(new Label { Text = "Averages appear after posts are added.", FontSize = 13, TextColor = Color.FromArgb("#999") });
            return;
        }
        double best10 = averages.Values.Max(v => v.avg10);
        double best20 = averages.Values.Max(v => v.avg20);
        double best30 = averages.Values.Max(v => v.avg30);
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            RowSpacing = 5,
            ColumnSpacing = 8
        };
        AddCell(grid, "Concept", 0, 0, true);
        AddCell(grid, "10s", 1, 0, true);
        AddCell(grid, "20s", 2, 0, true);
        AddCell(grid, "30s", 3, 0, true);
        AddCell(grid, "Posts", 4, 0, true);
        int row = 1;
        foreach (var item in averages.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
        {
            var v = item.Value;
            AddCell(grid, item.Key, 0, row, false);
            AddCell(grid, FormatAverage(v.avg10, best10), 1, row, v.avg10 == best10);
            AddCell(grid, FormatAverage(v.avg20, best20), 2, row, v.avg20 == best20);
            AddCell(grid, FormatAverage(v.avg30, best30), 3, row, v.avg30 == best30);
            AddCell(grid, v.count.ToString(), 4, row, false);
            row++;
        }
        _content.Children.Add(new Frame { Content = grid, Padding = 10, BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = false });
    }

    private static void AddCell(Grid grid, string text, int column, int row, bool highlight)
    {
        var label = new Label
        {
            Text = text,
            FontSize = 11,
            FontAttributes = highlight ? FontAttributes.Bold : FontAttributes.None,
            TextColor = highlight ? Color.FromArgb("#2E7D32") : Color.FromArgb("#444"),
            VerticalOptions = LayoutOptions.Center
        };
        grid.Add(label, column, row);
    }

    private static string FormatAverage(double value, double best)
        => value == best && best > 0 ? $"{value:F1}% ★" : $"{value:F1}%";

    private static Entry MakeEntry(string placeholder, bool numeric = false, double? value = null)
        => new()
        {
            Placeholder = placeholder,
            Text = value.HasValue ? value.Value.ToString("0.##") : "",
            Keyboard = numeric ? Keyboard.Numeric : Keyboard.Default,
            FontSize = 13,
            BackgroundColor = Colors.White,
            HeightRequest = 38
        };

    private static Button MakeButton(string text, string background, Color foreground)
        => new()
        {
            Text = text,
            BackgroundColor = Color.FromArgb(background),
            TextColor = foreground,
            CornerRadius = 7,
            HeightRequest = 38,
            FontSize = 12,
            Padding = new Thickness(12, 0)
        };

    private static double? ParseScore(string? text)
    {
        if (!double.TryParse(text, out var value)) return null;
        return Math.Clamp(value, 0, 100);
    }

    private static bool IsComplete(ClipsPost post)
        => post.Retention10s.HasValue &&
           post.Retention20s.HasValue &&
           post.Retention30s.HasValue;
}

using Bannister.Models;
using Bannister.Services;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Bannister.Views;

public class ClipsExperimentPage : ContentPage
{
    private readonly ClipsExperimentService _service;
    private readonly AuthService _auth;
    private Grid _root = null!;
    private VerticalStackLayout _content = null!;
    private HorizontalStackLayout _tabs = null!;
    private Entry _nameEntry = null!;
    private Entry _totalPostsEntry = null!;
    private Entry _titleEntry = null!;
    private Entry _conceptEntry = null!;
    private DatePicker _datePicker = null!;
    private List<ClipsExperimentPlatform> _platforms = new();
    private readonly HashSet<string> _expandedPostGroups = new();
    private readonly HashSet<string> _expandedExperimentSections = new();
    private readonly HashSet<int> _selectedPostIds = new();
    private Button? _assignSelectedPostsButton;
    private bool _postSelectionMode;
    private string _activeTab = "Posts";
    private bool _busy;

    public ClipsExperimentPage(ClipsExperimentService service, AuthService auth)
    {
        _service = service;
        _auth = auth;
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
        _root = new Grid
        {
            Padding = 20,
            RowSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        _root.Add(new Label
        {
            Text = "🎬 AI Clips Experiment",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        }, 0, 0);
        _root.Add(new Label
        {
            Text = "Test short-video hook concepts and compare viewer retention.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        }, 0, 1);
        _tabs = new HorizontalStackLayout { Spacing = 6 };
        BuildTabs();
        _root.Add(_tabs, 0, 2);
        _content = new VerticalStackLayout { Spacing = 12 };
        _root.Add(new ScrollView
        {
            Content = _content
        }, 0, 3);
        Content = _root;
    }

    private void BuildTabs()
    {
        _tabs.Children.Clear();
        foreach (var tab in new[] { "Posts", "Data Points", "Experiments" })
        {
            var selectedTab = tab;
            var button = MakeButton(
                tab,
                tab == _activeTab ? "#1565C0" : "#E0E0E0",
                tab == _activeTab ? Colors.White : Color.FromArgb("#555"));
            button.Clicked += async (_, _) =>
            {
                _activeTab = selectedTab;
                BuildTabs();
                await RefreshAsync();
            };
            _tabs.Children.Add(button);
        }
    }

    private async Task RefreshAsync()
    {
        if (_busy) return;
        await _service.InitAsync();
        var experiment = await _service.GetActiveExperimentAsync();
        _platforms = experiment != null ? await _service.GetPlatformsAsync(experiment.Id) : new();
        _content.Children.Clear();

        if (experiment == null)
        {
            BuildCreateExperimentSection();
            return;
        }

        var posts = await _service.GetPostsAsync(experiment.Id);
        BuildHeader(experiment, posts);
        if (_activeTab == "Data Points")
        {
            await BuildDataPointsTabAsync(experiment);
        }
        else if (_activeTab == "Experiments")
        {
            await BuildExperimentsTabAsync(experiment, posts);
        }
        else
        {
            var experimentItems = await _service.GetExperimentItemsAsync(_auth.CurrentUsername, experiment.Id);
            var allPlatformData = await _service.GetPlatformDataForExperimentAsync(experiment.Id);
            var platformDataByPost = allPlatformData
                .GroupBy(data => data.PostId)
                .ToDictionary(group => group.Key, group => group.ToList());
            BuildNewPostSection(experiment);
            BuildPostsSection(experiment, posts, experimentItems, platformDataByPost);
            await BuildAveragesSectionAsync(experiment, posts);
        }
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

        var platformRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        foreach (var platform in _platforms)
        {
            var currentPlatform = platform;
            var chip = MakeButton(currentPlatform.Name, "#E3F2FD", Color.FromArgb("#1565C0"));
            chip.FontSize = 11;
            chip.HeightRequest = 30;
            chip.Clicked += async (_, _) =>
            {
                if (!await DisplayAlert(
                        "Remove Platform",
                        $"Remove '{currentPlatform.Name}' and all its data?",
                        "Remove",
                        "Cancel")) return;
                await _service.DeletePlatformAsync(currentPlatform.Id);
                await RefreshAsync();
            };
            platformRow.Children.Add(chip);
        }
        var addPlatformButton = MakeButton("+ Platform", "#F5F5F5", Color.FromArgb("#555"));
        addPlatformButton.FontSize = 11;
        addPlatformButton.HeightRequest = 30;
        addPlatformButton.Clicked += async (_, _) =>
        {
            var name = await DisplayPromptAsync(
                "Add Platform",
                "Platform name (e.g. YouTube, TikTok):",
                initialValue: "YouTube");
            if (string.IsNullOrWhiteSpace(name)) return;
            await _service.SavePlatformAsync(new ClipsExperimentPlatform
            {
                ExperimentId = experiment.Id,
                Name = name.Trim(),
                SortOrder = _platforms.Count,
                CreatedDate = DateTime.UtcNow
            });
            await RefreshAsync();
        };
        platformRow.Children.Add(addPlatformButton);
        _content.Children.Add(platformRow);
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

    private void BuildPostsSection(
        ClipsExperiment experiment,
        List<ClipsPost> posts,
        List<ClipsExperimentItem> experimentItems,
        Dictionary<int, List<ClipsPostPlatformData>> platformDataByPost)
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

        var toggleSelection = MakeButton(
            _postSelectionMode ? "Exit Selection" : "Select Posts",
            _postSelectionMode ? "#EEEEEE" : "#E3F2FD",
            _postSelectionMode ? Color.FromArgb("#555") : Color.FromArgb("#1565C0"));
        toggleSelection.HorizontalOptions = LayoutOptions.Start;
        toggleSelection.Clicked += async (_, _) =>
        {
            _postSelectionMode = !_postSelectionMode;
            _selectedPostIds.Clear();
            await RefreshAsync();
        };
        _content.Children.Add(toggleSelection);

        if (_postSelectionMode)
        {
            var selectAll = MakeButton("Select All", "#EEEEEE", Color.FromArgb("#555"));
            selectAll.HorizontalOptions = LayoutOptions.Start;
            selectAll.Clicked += async (_, _) =>
            {
                foreach (var post in posts)
                    _selectedPostIds.Add(post.Id);
                await RefreshAsync();
            };
            _content.Children.Add(selectAll);

            _assignSelectedPostsButton = MakeButton("Assign 0 Posts to Experiment", "#1565C0", Colors.White);
            _assignSelectedPostsButton.IsEnabled = false;
            _assignSelectedPostsButton.Clicked += async (_, _) =>
            {
                var selectedExperiment = await ChooseExperimentAsync(experimentItems);
                if (selectedExperiment == null) return;

                var selectedPosts = posts.Where(post => _selectedPostIds.Contains(post.Id)).ToList();
                foreach (var selectedPost in selectedPosts)
                {
                    selectedPost.ExperimentItemId = selectedExperiment.Id;
                    await _service.SavePostAsync(selectedPost);
                }

                _selectedPostIds.Clear();
                _postSelectionMode = false;
                await RefreshAsync();
            };
            _content.Children.Add(_assignSelectedPostsButton);
            UpdateAssignSelectedPostsButton();
        }
        else
        {
            _assignSelectedPostsButton = null;
        }

        var knownExperimentIds = experimentItems.Select(item => item.Id).ToHashSet();
        var orderedItems = experimentItems
            .OrderByDescending(item => item.StartDate ?? DateTime.MinValue)
            .ToList();
        foreach (var experimentItem in orderedItems)
        {
            var assignedPosts = posts
                .Where(post => post.ExperimentItemId == experimentItem.Id)
                .OrderByDescending(post => post.PostedDate)
                .ToList();
            if (assignedPosts.Count > 0)
            {
                // Compute stats for this experiment's posts across all platforms
                var groupPosts = posts
                    .Where(post => post.ExperimentItemId == experimentItem.Id)
                    .ToList();
                var groupPostIds = groupPosts.Select(post => post.Id).ToHashSet();
                var groupData = platformDataByPost
                    .Where(item => groupPostIds.Contains(item.Key))
                    .SelectMany(item => item.Value)
                    .ToList();

                var statParts = new List<string>();
                foreach (var platform in _platforms)
                {
                    var platformData = groupData
                        .Where(data => data.PlatformId == platform.Id)
                        .ToList();
                    if (platformData.Count == 0) continue;
                    var avg10 = platformData
                        .Where(data => data.Retention10s.HasValue)
                        .Select(data => data.Retention10s!.Value)
                        .DefaultIfEmpty(0)
                        .Average();
                    var totalViews = platformData
                        .Where(data => data.TotalViews.HasValue)
                        .Sum(data => data.TotalViews!.Value);
                    var parts = new List<string>();
                    if (avg10 > 0) parts.Add($"10s:{avg10:F0}%");
                    if (totalViews > 0) parts.Add($"Views:{totalViews}");
                    if (parts.Count > 0)
                        statParts.Add($"{platform.Name} {string.Join(" ", parts)}");
                }

                var dateRow = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 12,
                    Margin = new Thickness(0, 10, 0, 0)
                };
                dateRow.Add(new Label
                {
                    Text = FormatDateRange(experimentItem),
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#444")
                }, 0, 0);
                if (statParts.Count > 0)
                {
                    dateRow.Add(new Label
                    {
                        Text = string.Join("  ·  ", statParts),
                        FontSize = 12,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333"),
                        VerticalOptions = LayoutOptions.Center
                    }, 1, 0);
                }
                _content.Children.Add(dateRow);
                AddPostGroup($"experiment:{experimentItem.Id}", experimentItem.Title, assignedPosts, experimentItems, platformDataByPost);
            }
        }

        var unassignedPosts = posts
            .Where(post => !post.ExperimentItemId.HasValue || !knownExperimentIds.Contains(post.ExperimentItemId.Value))
            .OrderByDescending(post => post.PostedDate)
            .ToList();
        if (unassignedPosts.Count > 0)
            AddPostGroup("unassigned", "Unassigned", unassignedPosts, experimentItems, platformDataByPost);
    }

    private void AddPostGroup(
        string groupKey,
        string title,
        List<ClipsPost> posts,
        List<ClipsExperimentItem> experimentItems,
        Dictionary<int, List<ClipsPostPlatformData>> platformDataByPost)
    {
        var isExpanded = _expandedPostGroups.Contains(groupKey);
        var header = MakeButton(
            $"{(isExpanded ? "▼" : "▶")} {title} ({posts.Count})",
            "#ECEFF1",
            Color.FromArgb("#333"));
        header.HorizontalOptions = LayoutOptions.Fill;
        header.Clicked += (_, _) =>
        {
            if (isExpanded)
                _expandedPostGroups.Remove(groupKey);
            else
                _expandedPostGroups.Add(groupKey);
            _ = RefreshAsync();
        };
        _content.Children.Add(header);

        if (!isExpanded) return;
        foreach (var post in posts)
            _content.Children.Add(BuildPostCard(post, experimentItems, platformDataByPost));
    }

    private View BuildPostCard(
        ClipsPost post,
        List<ClipsExperimentItem> experimentItems,
        Dictionary<int, List<ClipsPostPlatformData>> platformDataByPost)
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
        stack.Children.Add(header);
        var matchingExperiment = FindExperimentForPost(post, experimentItems);
        if (matchingExperiment != null)
        {
            stack.Children.Add(new Label
            {
                Text = matchingExperiment.Title,
                FontSize = 11,
                TextColor = Color.FromArgb("#1565C0"),
                FontAttributes = FontAttributes.Italic
            });
        }
        var assignExperiment = MakeButton("Assign Experiment", "#E3F2FD", Color.FromArgb("#1565C0"));
        assignExperiment.FontSize = 10;
        assignExperiment.HeightRequest = 32;
        assignExperiment.HorizontalOptions = LayoutOptions.Start;
        assignExperiment.Clicked += async (_, _) =>
        {
            var selectedExperiment = await ChooseExperimentAsync(experimentItems);
            if (selectedExperiment == null) return;
            post.ExperimentItemId = selectedExperiment.Id;
            await _service.SavePostAsync(post);
            await RefreshAsync();
        };
        if (!_postSelectionMode)
            stack.Children.Add(assignExperiment);

        var postData = platformDataByPost.TryGetValue(post.Id, out var loadedData)
            ? loadedData
            : new List<ClipsPostPlatformData>();
        var dataByPlatformId = postData.ToDictionary(data => data.PlatformId);

        foreach (var platform in _platforms)
        {
            dataByPlatformId.TryGetValue(platform.Id, out var existing);
            var r10 = MakeEntry("10s%", true, existing?.Retention10s);
            var r20 = MakeEntry("20s%", true, existing?.Retention20s);
            var r30 = MakeEntry("30s%", true, existing?.Retention30s);
            var views = MakeEntry(
                "Views",
                true,
                existing?.TotalViews.HasValue == true ? existing.TotalViews.Value : null);

            var pid = platform.Id;
            var existingId = existing?.Id ?? 0;
            CancellationTokenSource? autoSaveDebounce = null;

            async Task AutoSaveAsync()
            {
                autoSaveDebounce?.Cancel();
                autoSaveDebounce?.Dispose();
                autoSaveDebounce = new CancellationTokenSource();
                var cancellationToken = autoSaveDebounce.Token;
                try
                {
                    await Task.Delay(300, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return;
                }

                if (_busy) return;
                _busy = true;
                try
                {
                    var data = new ClipsPostPlatformData
                    {
                        Id = existingId,
                        PostId = post.Id,
                        PlatformId = pid,
                        Retention10s = ParseScore(r10.Text),
                        Retention20s = ParseScore(r20.Text),
                        Retention30s = ParseScore(r30.Text),
                        TotalViews = int.TryParse(views.Text, out var v) ? v : null,
                        UpdatedDate = DateTime.UtcNow
                    };
                    await _service.SavePostPlatformDataAsync(data);
                    existingId = data.Id;
                }
                finally { _busy = false; }
            }

            r10.TextChanged += async (_, _) => await AutoSaveAsync();
            r20.TextChanged += async (_, _) => await AutoSaveAsync();
            r30.TextChanged += async (_, _) => await AutoSaveAsync();
            views.TextChanged += async (_, _) => await AutoSaveAsync();

            var platformLabel = new Label
            {
                Text = platform.Name,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0"),
                Margin = new Thickness(0, 6, 0, 2)
            };
            var fieldsRow = new HorizontalStackLayout
            {
                Spacing = 6,
                Children = { r10, r20, r30, views }
            };
            stack.Children.Add(platformLabel);
            stack.Children.Add(fieldsRow);
        }

        var deletePost = MakeButton("Delete Post", "#FFEBEE", Color.FromArgb("#C62828"));
        deletePost.FontSize = 10;
        deletePost.HeightRequest = 30;
        deletePost.HorizontalOptions = LayoutOptions.Start;
        deletePost.Clicked += async (_, _) =>
        {
            if (!await DisplayAlert("Delete Post", $"Delete '{post.Title}'?", "Delete", "Cancel")) return;
            await _service.DeletePostAsync(post.Id);
            await RefreshAsync();
        };
        stack.Children.Add(deletePost);

        View cardContent = stack;
        if (_postSelectionMode)
        {
            var checkBox = new CheckBox
            {
                IsChecked = _selectedPostIds.Contains(post.Id),
                VerticalOptions = LayoutOptions.Start
            };
            checkBox.CheckedChanged += (_, args) =>
            {
                if (args.Value)
                    _selectedPostIds.Add(post.Id);
                else
                    _selectedPostIds.Remove(post.Id);
                UpdateAssignSelectedPostsButton();
            };
            var selectionGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };
            selectionGrid.Add(checkBox, 0, 0);
            selectionGrid.Add(stack, 1, 0);
            cardContent = selectionGrid;
        }
        return new Frame
        {
            Content = cardContent,
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = complete ? Color.FromArgb("#FAFAFA") : Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false
        };
    }

    private async Task<ClipsExperimentItem?> ChooseExperimentAsync(List<ClipsExperimentItem> experimentItems)
    {
        if (experimentItems.Count == 0)
        {
            await DisplayAlert("Assign Experiment", "No experiment items are available.", "OK");
            return null;
        }

        var completion = new TaskCompletionSource<ClipsExperimentItem?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Margin = new Thickness(-20),
            ZIndex = 100
        };
        Grid.SetRow(overlay, 0);
        Grid.SetRowSpan(overlay, 4);

        void Complete(ClipsExperimentItem? selectedItem)
        {
            _root.Children.Remove(overlay);
            completion.TrySetResult(selectedItem);
        }

        var items = new VerticalStackLayout
        {
            Padding = new Thickness(12, 8),
            Spacing = 8
        };
        foreach (var experimentItem in experimentItems)
        {
            var item = experimentItem;
            var button = new Button
            {
                Text = item.Title,
                FontSize = 14,
                TextColor = Color.FromArgb("#222"),
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                CornerRadius = 7,
                Padding = new Thickness(12, 10),
                HorizontalOptions = LayoutOptions.Fill,
                LineBreakMode = LineBreakMode.WordWrap
            };
            button.Clicked += (_, _) => Complete(item);
            items.Children.Add(button);
        }

        var cancel = MakeButton("Cancel", "#EEEEEE", Color.FromArgb("#555"));
        cancel.Clicked += (_, _) => Complete(null);
        var cardContent = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label
                {
                    Text = "Assign Experiment",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                new ScrollView
                {
                    MaximumHeightRequest = 420,
                    Content = items
                },
                cancel
            }
        };
        overlay.Children.Add(new Frame
        {
            Content = cardContent,
            Padding = 18,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            MaximumWidthRequest = 520,
            MaximumHeightRequest = 600,
            Margin = new Thickness(20),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = true
        });
        _root.Children.Add(overlay);
        return await completion.Task;
    }

    private void UpdateAssignSelectedPostsButton()
    {
        if (_assignSelectedPostsButton == null) return;
        var count = _selectedPostIds.Count;
        _assignSelectedPostsButton.Text = $"Assign {count} Posts to Experiment";
        _assignSelectedPostsButton.IsEnabled = count > 0;
    }

    private async Task BuildDataPointsTabAsync(ClipsExperiment experiment)
    {
        _content.Children.Add(new Label
        {
            Text = "Add Data Point",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 12, 0, 0)
        });
        var observationEditor = MakeEditor("Write an observation about clip performance...");
        _content.Children.Add(observationEditor);
        var save = MakeButton("Save Data Point", "#2E7D32", Colors.White);
        save.Clicked += async (_, _) =>
        {
            if (_busy || string.IsNullOrWhiteSpace(observationEditor.Text)) return;
            _busy = true;
            try
            {
                await _service.SaveDataPointAsync(new ClipsDataPoint
                {
                    Username = _auth.CurrentUsername,
                    ExperimentId = experiment.Id,
                    Text = observationEditor.Text.Trim(),
                    CreatedDate = DateTime.UtcNow
                });
            }
            finally { _busy = false; }
            await RefreshAsync();
        };
        _content.Children.Add(save);

        var dataPoints = await _service.GetDataPointsAsync(_auth.CurrentUsername, experiment.Id);
        _content.Children.Add(new Label
        {
            Text = $"Data Points ({dataPoints.Count})",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 12, 0, 0)
        });
        if (dataPoints.Count == 0)
        {
            _content.Children.Add(new Label { Text = "No data points recorded yet.", FontSize = 13, TextColor = Color.FromArgb("#999") });
            return;
        }

        foreach (var dataPoint in dataPoints)
        {
            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Children.Add(new Label
            {
                Text = dataPoint.Text,
                FontSize = 13,
                TextColor = Color.FromArgb("#333"),
                LineBreakMode = LineBreakMode.WordWrap
            });
            stack.Children.Add(new Label
            {
                Text = dataPoint.CreatedDate.ToLocalTime().ToString("dd MMM yyyy HH:mm"),
                FontSize = 11,
                TextColor = Color.FromArgb("#888")
            });
            var delete = MakeButton("Delete", "#FFEBEE", Color.FromArgb("#C62828"));
            delete.HorizontalOptions = LayoutOptions.Start;
            delete.Clicked += async (_, _) =>
            {
                if (!await DisplayAlert("Delete Data Point", "Delete this observation?", "Delete", "Cancel")) return;
                await _service.DeleteDataPointAsync(dataPoint.Id);
                await RefreshAsync();
            };
            stack.Children.Add(delete);
            _content.Children.Add(MakeCard(stack));
        }
    }

    private async Task BuildExperimentsTabAsync(ClipsExperiment experiment, List<ClipsPost> posts)
    {
        var dataPoints = await _service.GetDataPointsAsync(_auth.CurrentUsername, experiment.Id);
        var allItems = await _service.GetExperimentItemsAsync(_auth.CurrentUsername, experiment.Id);
        var unrunItems = await _service.GetUnrunExperimentItemsAsync(_auth.CurrentUsername, experiment.Id);
        var activeItem = await _service.GetActiveExperimentItemAsync(_auth.CurrentUsername, experiment.Id);

        _content.Children.Add(new Label
        {
            Text = "Currently Running:",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 12, 0, 0)
        });
        var activeStack = new VerticalStackLayout { Spacing = 4 };
        activeStack.Children.Add(new Label
        {
            Text = activeItem?.Title ?? "No active experiment",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = activeItem == null ? Color.FromArgb("#777") : Color.FromArgb("#1565C0")
        });
        if (activeItem != null)
        {
            activeStack.Children.Add(new Label
            {
                Text = FormatDateRange(activeItem),
                FontSize = 12,
                TextColor = Color.FromArgb("#666")
            });
            var activeButtons = new HorizontalStackLayout { Spacing = 8 };
            var moveToNotRun = MakeButton("Move to Not Yet Run", "#FFF3E0", Color.FromArgb("#E65100"));
            moveToNotRun.Clicked += async (_, _) =>
            {
                await _service.UnmarkExperimentItemRunAsync(activeItem.Id);
                activeItem.IsRun = false;
                activeItem.StartDate = null;
                activeItem.EndDate = null;
                await _service.SaveExperimentItemAsync(activeItem);
                await RefreshAsync();
            };
            activeButtons.Children.Add(moveToNotRun);
            var moveToCompleted = MakeButton("Move to Completed", "#2E7D32", Colors.White);
            moveToCompleted.Clicked += async (_, _) =>
            {
                await _service.MarkExperimentItemRunAsync(activeItem.Id);
                await RefreshAsync();
            };
            activeButtons.Children.Add(moveToCompleted);
            activeStack.Children.Add(activeButtons);
        }
        _content.Children.Add(MakeCard(activeStack));

        _content.Children.Add(new Label
        {
            Text = "Add Experiment",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            Margin = new Thickness(0, 12, 0, 0)
        });
        var titleEntry = MakeEntry("Experiment title");
        var descriptionEditor = MakeEditor("What should be tested, and why?");
        var startDatePicker = new DatePicker
        {
            Date = DateTime.Today,
            Format = "dd MMM yyyy",
            HorizontalOptions = LayoutOptions.Start
        };
        var endDatePicker = new DatePicker
        {
            Date = DateTime.Today.AddDays(6),
            Format = "dd MMM yyyy",
            HorizontalOptions = LayoutOptions.Start
        };
        _content.Children.Add(titleEntry);
        _content.Children.Add(descriptionEditor);
        _content.Children.Add(new Label { Text = "Start date", FontSize = 12, TextColor = Color.FromArgb("#666") });
        _content.Children.Add(startDatePicker);
        _content.Children.Add(new Label { Text = "End date", FontSize = 12, TextColor = Color.FromArgb("#666") });
        _content.Children.Add(endDatePicker);
        var save = MakeButton("Save Experiment", "#2E7D32", Colors.White);
        save.Clicked += async (_, _) =>
        {
            if (_busy || string.IsNullOrWhiteSpace(titleEntry.Text)) return;
            if (endDatePicker.Date < startDatePicker.Date)
            {
                await DisplayAlert("Invalid Dates", "End date must be on or after the start date.", "OK");
                return;
            }
            _busy = true;
            try
            {
                await _service.SaveExperimentItemAsync(new ClipsExperimentItem
                {
                    Username = _auth.CurrentUsername,
                    ExperimentId = experiment.Id,
                    Title = titleEntry.Text.Trim(),
                    Description = descriptionEditor.Text?.Trim() ?? "",
                    StartDate = startDatePicker.Date.Date,
                    EndDate = endDatePicker.Date.Date,
                    CreatedDate = DateTime.UtcNow
                });
            }
            finally { _busy = false; }
            await RefreshAsync();
        };
        _content.Children.Add(save);

        var promptButtons = new VerticalStackLayout { Spacing = 8 };
        var ideas = MakeButton("Get LLM Experiment Ideas", "#6A1B9A", Colors.White);
        ideas.Clicked += async (_, _) =>
        {
            await Clipboard.SetTextAsync(BuildExperimentIdeasPrompt(posts, dataPoints, allItems));
            await DisplayAlert("Copied", "The experiment ideas prompt was copied to the clipboard.", "OK");
        };
        var importIdeas = MakeButton("Import Experiment Ideas", "#F3E5F5", Color.FromArgb("#6A1B9A"));
        importIdeas.Clicked += async (_, _) =>
        {
            var response = await ShowLlmPasteOverlayAsync("Import Experiment Ideas");
            if (response == null) return;
            var parsedIdeas = ParseExperimentIdeas(response);
            if (parsedIdeas.Count == 0)
            {
                await DisplayAlert("No Ideas Found", "No parseable newExperiment entries were found.", "OK");
                return;
            }
            await ShowExperimentIdeaApprovalsAsync(experiment, parsedIdeas, allItems);
            await RefreshAsync();
        };
        var ranking = MakeButton("Export for LLM Priority Ranking", "#1565C0", Colors.White);
        ranking.Clicked += async (_, _) =>
        {
            await Clipboard.SetTextAsync(BuildPriorityRankingPrompt(posts, dataPoints, allItems, unrunItems));
            await DisplayAlert("Copied", "The priority ranking prompt was copied to the clipboard.", "OK");
        };
        var importRanking = MakeButton("Import Priority Ranking", "#E3F2FD", Color.FromArgb("#1565C0"));
        importRanking.Clicked += async (_, _) =>
        {
            var response = await ShowLlmPasteOverlayAsync("Import Priority Ranking");
            if (response == null) return;
            var parsedRanking = ParsePriorityRanking(response);
            if (parsedRanking.Count == 0)
            {
                await DisplayAlert("No Ranking Found", "No parseable priority entries were found.", "OK");
                return;
            }
            if (!await ShowPriorityApprovalAsync(parsedRanking)) return;
            await ApplyPriorityRankingAsync(parsedRanking, unrunItems);
            await RefreshAsync();
        };

        var exportDataPoints = MakeButton("Export Stats for Data Points", "#F3E5F5", Color.FromArgb("#6A1B9A"));
        exportDataPoints.Clicked += async (_, _) =>
        {
            var allPlatformData = await _service.GetPlatformDataForExperimentAsync(experiment.Id);
            var platforms = await _service.GetPlatformsAsync(experiment.Id);
            var platformMap = platforms.ToDictionary(platform => platform.Id, platform => platform.Name);
            var dataByPost = allPlatformData.GroupBy(data => data.PostId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var builder = new StringBuilder();
            builder.AppendLine("Here is my short-video clip experiment data. Based on the retention and views statistics, generate 5–10 insightful data point observations I can add to my experiment log.");
            builder.AppendLine();
            builder.AppendLine($"Experiment: {experiment.Name}");
            builder.AppendLine();
            builder.AppendLine("Posts and platform stats:");
            foreach (var post in posts)
            {
                builder.AppendLine($"- \"{post.Title}\" | Hook: {post.Concept} | Date: {post.PostedDate:dd MMM yyyy}");
                if (dataByPost.TryGetValue(post.Id, out var platformDataItems))
                {
                    foreach (var platformData in platformDataItems)
                    {
                        if (!platformMap.TryGetValue(platformData.PlatformId, out var platformName)) continue;
                        var parts = new List<string>();
                        if (platformData.Retention10s.HasValue) parts.Add($"10s:{platformData.Retention10s.Value:F0}%");
                        if (platformData.Retention20s.HasValue) parts.Add($"20s:{platformData.Retention20s.Value:F0}%");
                        if (platformData.Retention30s.HasValue) parts.Add($"30s:{platformData.Retention30s.Value:F0}%");
                        if (platformData.TotalViews.HasValue) parts.Add($"Views:{platformData.TotalViews.Value}");
                        if (parts.Count > 0)
                            builder.AppendLine($"    {platformName}: {string.Join(" · ", parts)}");
                    }
                }
            }
            builder.AppendLine();
            builder.AppendLine("Existing data points (do not repeat these):");
            foreach (var dataPoint in dataPoints)
                builder.AppendLine($"- [{dataPoint.CreatedDate.ToLocalTime():yyyy-MM-dd}] {dataPoint.Text}");
            builder.AppendLine();
            builder.AppendLine("Return new observations only, each as a single sentence. Return them in this exact format:");
            builder.AppendLine("dataPoint[1] = \"Observation here\";");
            builder.AppendLine("dataPoint[2] = \"Observation here\";");
            builder.AppendLine("(continue for all observations)");

            await Clipboard.SetTextAsync(builder.ToString());
            await DisplayAlert("Copied", "Data points from stats prompt copied to clipboard.", "OK");
        };

        var importDataPoints = MakeButton("Import Data Points from LLM", "#F3E5F5", Color.FromArgb("#6A1B9A"));
        importDataPoints.Clicked += async (_, _) =>
        {
            var response = await ShowLlmPasteOverlayAsync("Import Data Points from LLM");
            if (response == null) return;

            var parsedDataPoints = ParseDataPoints(response);
            if (parsedDataPoints.Count == 0)
            {
                await DisplayAlert("No Data Points Found", "No parseable dataPoint entries were found.", "OK");
                return;
            }

            var existingIndices = Enumerable.Range(1, dataPoints.Count).ToHashSet();
            var importedCount = 0;
            foreach (var parsedDataPoint in parsedDataPoints)
            {
                if (existingIndices.Contains(parsedDataPoint.Index)) continue;

                await _service.SaveDataPointAsync(new ClipsDataPoint
                {
                    Username = _auth.CurrentUsername,
                    ExperimentId = experiment.Id,
                    Text = parsedDataPoint.Text,
                    CreatedDate = DateTime.UtcNow
                });
                existingIndices.Add(parsedDataPoint.Index);
                importedCount++;
            }

            await DisplayAlert(
                "Import Complete",
                importedCount == 0
                    ? "All parsed data point indices already exist."
                    : $"Imported {importedCount} data point(s).",
                "OK");
            await RefreshAsync();
        };

        var exportAllStats = MakeButton("Export All Stats & Data Points", "#ECEFF1", Color.FromArgb("#333"));
        exportAllStats.Clicked += async (_, _) =>
        {
            var allPlatformData = await _service.GetPlatformDataForExperimentAsync(experiment.Id);
            var platforms = await _service.GetPlatformsAsync(experiment.Id);
            var platformMap = platforms.ToDictionary(platform => platform.Id, platform => platform.Name);
            var dataByPost = allPlatformData.GroupBy(data => data.PostId)
                .ToDictionary(group => group.Key, group => group.ToList());

            var export = new StringBuilder();
            export.AppendLine($"=== {experiment.Name} ===");
            export.AppendLine();
            export.AppendLine("POSTS & STATS:");
            foreach (var post in posts)
            {
                export.AppendLine($"- \"{post.Title}\" | Hook: {post.Concept} | {post.PostedDate:dd MMM yyyy}");
                if (dataByPost.TryGetValue(post.Id, out var platformDataItems))
                {
                    foreach (var platformData in platformDataItems)
                    {
                        if (!platformMap.TryGetValue(platformData.PlatformId, out var platformName)) continue;
                        var parts = new List<string>();
                        if (platformData.Retention10s.HasValue) parts.Add($"10s:{platformData.Retention10s.Value:F0}%");
                        if (platformData.Retention20s.HasValue) parts.Add($"20s:{platformData.Retention20s.Value:F0}%");
                        if (platformData.Retention30s.HasValue) parts.Add($"30s:{platformData.Retention30s.Value:F0}%");
                        if (platformData.TotalViews.HasValue) parts.Add($"Views:{platformData.TotalViews.Value}");
                        if (parts.Count > 0)
                            export.AppendLine($"    {platformName}: {string.Join(" · ", parts)}");
                    }
                }
            }
            export.AppendLine();
            export.AppendLine("DATA POINTS:");
            foreach (var dataPoint in dataPoints)
                export.AppendLine($"- [{dataPoint.CreatedDate.ToLocalTime():yyyy-MM-dd}] {dataPoint.Text}");

            await Clipboard.SetTextAsync(export.ToString());
            await DisplayAlert("Copied", "All stats and data points copied to clipboard.", "OK");
        };

        promptButtons.Children.Add(ideas);
        promptButtons.Children.Add(importIdeas);
        promptButtons.Children.Add(ranking);
        promptButtons.Children.Add(importRanking);
        promptButtons.Children.Add(exportDataPoints);
        promptButtons.Children.Add(importDataPoints);
        promptButtons.Children.Add(exportAllStats);
        _content.Children.Add(promptButtons);

        var editPriority = MakeButton("✏️ Edit Priority Order", "#FFF8E1", Color.FromArgb("#F57F17"));
        editPriority.Clicked += async (_, _) =>
        {
            if (unrunItems.Count == 0)
            {
                await DisplayAlert("Edit Priority Order", "There are no Not Yet Run experiments to reorder.", "OK");
                return;
            }
            var currentRanking = unrunItems.Select((item, index) => new ParsedPriorityItem
            {
                Index = index + 1,
                Title = item.Title,
                Reason = string.IsNullOrWhiteSpace(item.Description) ? "No description." : item.Description
            }).ToList();
            if (!await ShowPriorityApprovalAsync(currentRanking)) return;
            await ApplyPriorityRankingAsync(currentRanking, unrunItems);
            await RefreshAsync();
        };
        _content.Children.Add(editPriority);

        BuildExperimentItemsSection("Not Yet Run", unrunItems, true);
        BuildExperimentItemsSection("Completed", allItems.Where(item => item.IsRun).ToList(), false);
    }

    private void BuildExperimentItemsSection(string heading, List<ClipsExperimentItem> items, bool canMarkRun)
    {
        var isExpanded = _expandedExperimentSections.Contains(heading);
        var sectionToggle = MakeButton(
            $"{(isExpanded ? "▼" : "▶")} {heading} ({items.Count})",
            "#ECEFF1",
            Color.FromArgb("#333"));
        sectionToggle.HorizontalOptions = LayoutOptions.Fill;
        sectionToggle.Margin = new Thickness(0, 12, 0, 0);
        sectionToggle.Clicked += (_, _) =>
        {
            if (isExpanded)
                _expandedExperimentSections.Remove(heading);
            else
                _expandedExperimentSections.Add(heading);
            _ = RefreshAsync();
        };
        _content.Children.Add(sectionToggle);
        if (!isExpanded) return;

        if (items.Count == 0)
        {
            _content.Children.Add(new Label { Text = "None.", FontSize = 13, TextColor = Color.FromArgb("#999") });
            return;
        }

        foreach (var item in items)
        {
            var stack = new VerticalStackLayout { Spacing = 6 };
            stack.Children.Add(new Label
            {
                Text = item.Title,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#222")
            });
            stack.Children.Add(new Label
            {
                Text = FormatDateRange(item),
                FontSize = 11,
                TextColor = Color.FromArgb("#1565C0")
            });
            stack.Children.Add(new Label
            {
                Text = item.Description,
                FontSize = 13,
                TextColor = Color.FromArgb("#555"),
                LineBreakMode = LineBreakMode.WordWrap
            });
            stack.Children.Add(new Label
            {
                Text = item.CreatedDate.ToLocalTime().ToString("dd MMM yyyy"),
                FontSize = 11,
                TextColor = Color.FromArgb("#888")
            });
            var buttons = new HorizontalStackLayout { Spacing = 8 };
            if (canMarkRun)
            {
                var setActive = MakeButton("Set as Active", "#E3F2FD", Color.FromArgb("#1565C0"));
                setActive.Clicked += async (_, _) => await EditExperimentDatesAsync(item);
                buttons.Children.Add(setActive);
                var markRun = MakeButton("Mark as Run", "#2E7D32", Colors.White);
                markRun.Clicked += async (_, _) =>
                {
                    await _service.MarkExperimentItemRunAsync(item.Id);
                    await RefreshAsync();
                };
                buttons.Children.Add(markRun);
            }
            else
            {
                var moveToNotRun = MakeButton("Move to Not Run", "#FFF3E0", Color.FromArgb("#E65100"));
                moveToNotRun.Clicked += async (_, _) =>
                {
                    await _service.UnmarkExperimentItemRunAsync(item.Id);
                    await RefreshAsync();
                };
                buttons.Children.Add(moveToNotRun);
                var editDates = MakeButton("Edit Dates", "#E3F2FD", Color.FromArgb("#1565C0"));
                editDates.Clicked += async (_, _) => await EditExperimentDatesAsync(item);
                buttons.Children.Add(editDates);
            }
            var delete = MakeButton("Delete", "#FFEBEE", Color.FromArgb("#C62828"));
            delete.Clicked += async (_, _) =>
            {
                if (!await DisplayAlert("Delete Experiment", $"Delete '{item.Title}'?", "Delete", "Cancel")) return;
                await _service.DeleteExperimentItemAsync(item.Id);
                await RefreshAsync();
            };
            buttons.Children.Add(delete);
            stack.Children.Add(buttons);

            var describeExperiment = MakeButton(
                "Describe This Experiment",
                "#E8F5E9",
                Color.FromArgb("#2E7D32"));
            describeExperiment.HorizontalOptions = LayoutOptions.Start;
            describeExperiment.Clicked += async (_, _) =>
            {
                var describePrompt = new StringBuilder();
                describePrompt.AppendLine($"Experiment title: {item.Title}");
                describePrompt.AppendLine($"Current description: {item.Description}");
                describePrompt.AppendLine();
                describePrompt.AppendLine("I am running a short-video clip experiment. Please interview me by asking questions one at a time to understand what this experiment is testing, what my hypothesis is, and what success looks like. Once you understand it fully, write a concise 2-3 sentence description I can save as the experiment description.");
                await Clipboard.SetTextAsync(describePrompt.ToString());
                await DisplayAlert("Copied", "Describe prompt copied to clipboard.", "OK");
            };
            stack.Children.Add(describeExperiment);
            _content.Children.Add(MakeCard(stack));
        }
    }

    private async Task EditExperimentDatesAsync(ClipsExperimentItem item)
    {
        var startText = await DisplayPromptAsync(
            "Set Start Date",
            "Enter the start date (MM/dd/yyyy).",
            initialValue: (item.StartDate ?? DateTime.Today).ToString("MM/dd/yyyy"));
        if (startText == null) return;

        var endText = await DisplayPromptAsync(
            "Set End Date",
            "Enter the end date (MM/dd/yyyy).",
            initialValue: (item.EndDate ?? DateTime.Today.AddDays(6)).ToString("MM/dd/yyyy"));
        if (endText == null) return;

        if (!DateTime.TryParseExact(startText.Trim(), "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDate) ||
            !DateTime.TryParseExact(endText.Trim(), "MM/dd/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDate))
        {
            await DisplayAlert("Invalid Dates", "Enter both dates in MM/dd/yyyy format.", "OK");
            return;
        }
        if (endDate < startDate)
        {
            await DisplayAlert("Invalid Dates", "End date must be on or after the start date.", "OK");
            return;
        }

        item.StartDate = startDate.Date;
        item.EndDate = endDate.Date;
        await _service.SaveExperimentItemAsync(item);
        await RefreshAsync();
    }

    private async Task<string?> ShowLlmPasteOverlayAsync(string title)
    {
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlay = CreateDimmedOverlay();
        var editor = new Editor
        {
            Placeholder = "Paste the LLM response here...",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 260,
            FontSize = 13,
            BackgroundColor = Color.FromArgb("#FAFAFA")
        };
        void Complete(string? result)
        {
            _root.Children.Remove(overlay);
            completion.TrySetResult(result);
        }

        var import = MakeButton("Import", "#1565C0", Colors.White);
        import.Clicked += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(editor.Text))
                Complete(editor.Text);
        };
        var cancel = MakeButton("Cancel", "#EEEEEE", Color.FromArgb("#555"));
        cancel.Clicked += (_, _) => Complete(null);
        var buttons = new HorizontalStackLayout { Spacing = 8, Children = { import, cancel } };
        var content = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Label { Text = title, FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") },
                editor,
                buttons
            }
        };
        overlay.Children.Add(CreateOverlayCard(content, 680, 620));
        _root.Children.Add(overlay);
        return await completion.Task;
    }

    private async Task ShowExperimentIdeaApprovalsAsync(
        ClipsExperiment experiment,
        List<ParsedExperimentIdea> ideas,
        List<ClipsExperimentItem> existingItems)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlay = CreateDimmedOverlay();
        var cards = new VerticalStackLayout { Padding = new Thickness(4), Spacing = 10 };
        var remaining = ideas.Count;
        var nextSortOrder = existingItems.Count == 0 ? 0 : existingItems.Max(item => item.SortOrder) + 1;

        foreach (var idea in ideas)
        {
            var approvalCard = new Frame
            {
                Padding = 12,
                CornerRadius = 8,
                BackgroundColor = Color.FromArgb("#FAFAFA"),
                BorderColor = Color.FromArgb("#E0E0E0"),
                HasShadow = false
            };
            var add = MakeButton("Add", "#2E7D32", Colors.White);
            var skip = MakeButton("Skip", "#EEEEEE", Color.FromArgb("#555"));
            var handled = false;
            async Task ResolveAsync(bool shouldAdd)
            {
                if (handled) return;
                handled = true;
                add.IsEnabled = false;
                skip.IsEnabled = false;
                if (shouldAdd)
                {
                    await _service.SaveExperimentItemAsync(new ClipsExperimentItem
                    {
                        Username = _auth.CurrentUsername,
                        ExperimentId = experiment.Id,
                        Title = idea.Title,
                        Description = idea.Description,
                        SortOrder = nextSortOrder++,
                        CreatedDate = DateTime.UtcNow
                    });
                }
                cards.Children.Remove(approvalCard);
                remaining--;
                if (remaining == 0)
                {
                    _root.Children.Remove(overlay);
                    completion.TrySetResult();
                }
            }
            add.Clicked += async (_, _) => await ResolveAsync(true);
            skip.Clicked += async (_, _) => await ResolveAsync(false);
            approvalCard.Content = new VerticalStackLayout
            {
                Spacing = 7,
                Children =
                {
                    new Label { Text = idea.Title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222"), LineBreakMode = LineBreakMode.WordWrap },
                    new Label { Text = idea.Description, FontSize = 13, TextColor = Color.FromArgb("#555"), LineBreakMode = LineBreakMode.WordWrap },
                    new HorizontalStackLayout { Spacing = 8, Children = { add, skip } }
                }
            };
            cards.Children.Add(approvalCard);
        }

        var content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Approve Experiment Ideas", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") },
                new ScrollView { MaximumHeightRequest = 560, Content = cards }
            }
        };
        overlay.Children.Add(CreateOverlayCard(content, 720, 680));
        _root.Children.Add(overlay);
        await completion.Task;
    }

    private async Task<bool> ShowPriorityApprovalAsync(List<ParsedPriorityItem> ranking)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var overlay = CreateDimmedOverlay();
        var list = new VerticalStackLayout { Padding = new Thickness(4), Spacing = 10 };
        void RenderRanking()
        {
            list.Children.Clear();
            for (var index = 0; index < ranking.Count; index++)
            {
                var currentIndex = index;
                var item = ranking[index];
                var details = new VerticalStackLayout
                {
                    Spacing = 4,
                    Children =
                    {
                        new Label { Text = $"{index + 1}. {item.Title}", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222"), LineBreakMode = LineBreakMode.WordWrap },
                        new Label { Text = item.Reason, FontSize = 12, TextColor = Color.FromArgb("#555"), LineBreakMode = LineBreakMode.WordWrap }
                    }
                };
                var up = MakeButton("↑", "#EEEEEE", Color.FromArgb("#333"));
                up.WidthRequest = 42;
                up.IsEnabled = index > 0;
                up.Clicked += (_, _) =>
                {
                    if (currentIndex <= 0) return;
                    (ranking[currentIndex - 1], ranking[currentIndex]) =
                        (ranking[currentIndex], ranking[currentIndex - 1]);
                    RenderRanking();
                };
                var down = MakeButton("↓", "#EEEEEE", Color.FromArgb("#333"));
                down.WidthRequest = 42;
                down.IsEnabled = index < ranking.Count - 1;
                down.Clicked += (_, _) =>
                {
                    if (currentIndex >= ranking.Count - 1) return;
                    (ranking[currentIndex + 1], ranking[currentIndex]) =
                        (ranking[currentIndex], ranking[currentIndex + 1]);
                    RenderRanking();
                };
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 6
                };
                row.Add(details, 0, 0);
                row.Add(up, 1, 0);
                row.Add(down, 2, 0);
                list.Children.Add(MakeCard(row));
            }
        }
        RenderRanking();

        void Complete(bool approved)
        {
            _root.Children.Remove(overlay);
            completion.TrySetResult(approved);
        }
        var approve = MakeButton("Approve New Order", "#2E7D32", Colors.White);
        approve.Clicked += (_, _) => Complete(true);
        var cancel = MakeButton("Cancel", "#EEEEEE", Color.FromArgb("#555"));
        cancel.Clicked += (_, _) => Complete(false);
        var content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Proposed Priority Order", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") },
                new ScrollView { MaximumHeightRequest = 500, Content = list },
                new HorizontalStackLayout { Spacing = 8, Children = { approve, cancel } }
            }
        };
        overlay.Children.Add(CreateOverlayCard(content, 720, 680));
        _root.Children.Add(overlay);
        return await completion.Task;
    }

    private async Task ApplyPriorityRankingAsync(
        List<ParsedPriorityItem> ranking,
        List<ClipsExperimentItem> unrunItems)
    {
        var rankByTitle = ranking
            .Select((item, index) => new { item.Title, Rank = index + 1 })
            .GroupBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Rank, StringComparer.OrdinalIgnoreCase);
        var nextUnrankedPosition = ranking.Count + 1;
        foreach (var item in unrunItems)
        {
            item.SortOrder = rankByTitle.TryGetValue(item.Title, out var rank)
                ? rank
                : nextUnrankedPosition++;
            await _service.SaveExperimentItemAsync(item);
        }
    }

    private static List<ParsedExperimentIdea> ParseExperimentIdeas(string response)
    {
        var normalized = response.Replace("\"\"", "\"");
        var parsed = new Dictionary<int, ParsedExperimentIdea>();
        foreach (Match match in Regex.Matches(
                     normalized,
                     "newExperiment\\[(\\d+)\\]\\.(title|description)\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;",
                     RegexOptions.IgnoreCase))
        {
            var index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!parsed.TryGetValue(index, out var idea))
            {
                idea = new ParsedExperimentIdea { Index = index };
                parsed[index] = idea;
            }
            var value = DecodeLlmValue(match.Groups[3].Value);
            if (match.Groups[2].Value.Equals("title", StringComparison.OrdinalIgnoreCase))
                idea.Title = value;
            else
                idea.Description = value;
        }
        return parsed.Values
            .Where(idea => !string.IsNullOrWhiteSpace(idea.Title) && !string.IsNullOrWhiteSpace(idea.Description))
            .OrderBy(idea => idea.Index)
            .ToList();
    }

    private static List<ParsedPriorityItem> ParsePriorityRanking(string response)
    {
        var normalized = response.Replace("\"\"", "\"");
        var parsed = new Dictionary<int, ParsedPriorityItem>();
        foreach (Match match in Regex.Matches(
                     normalized,
                     "priority\\[(\\d+)\\]\\.(title|reason)\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;",
                     RegexOptions.IgnoreCase))
        {
            var index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (!parsed.TryGetValue(index, out var item))
            {
                item = new ParsedPriorityItem { Index = index };
                parsed[index] = item;
            }
            var value = DecodeLlmValue(match.Groups[3].Value);
            if (match.Groups[2].Value.Equals("title", StringComparison.OrdinalIgnoreCase))
                item.Title = value;
            else
                item.Reason = value;
        }
        return parsed.Values
            .Where(item => !string.IsNullOrWhiteSpace(item.Title) && !string.IsNullOrWhiteSpace(item.Reason))
            .OrderBy(item => item.Index)
            .ToList();
    }

    private static List<ParsedDataPoint> ParseDataPoints(string response)
    {
        var normalized = response.Replace("\"\"", "\"");
        var parsed = new Dictionary<int, ParsedDataPoint>();
        foreach (Match match in Regex.Matches(
                     normalized,
                     "dataPoint\\[(\\d+)\\]\\s*=\\s*\"((?:\\\\.|[^\"\\\\])*)\"\\s*;",
                     RegexOptions.IgnoreCase))
        {
            var index = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var text = DecodeLlmValue(match.Groups[2].Value);
            if (index > 0 && !string.IsNullOrWhiteSpace(text))
                parsed[index] = new ParsedDataPoint { Index = index, Text = text };
        }

        return parsed.Values
            .OrderBy(item => item.Index)
            .ToList();
    }

    private static string DecodeLlmValue(string value)
        => value.Replace("\\\"", "\"").Replace("\\\\", "\\").Trim();

    private Grid CreateDimmedOverlay()
    {
        var overlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Margin = new Thickness(-20),
            ZIndex = 100
        };
        Grid.SetRow(overlay, 0);
        Grid.SetRowSpan(overlay, 4);
        return overlay;
    }

    private static Frame CreateOverlayCard(View content, double maximumWidth, double maximumHeight)
        => new()
        {
            Content = content,
            Padding = 18,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            MaximumWidthRequest = maximumWidth,
            MaximumHeightRequest = maximumHeight,
            Margin = new Thickness(20),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = true
        };

    private static string BuildExperimentIdeasPrompt(
        List<ClipsPost> posts,
        List<ClipsDataPoint> dataPoints,
        List<ClipsExperimentItem> experimentItems)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Here is my AI clip experiment data. Based on this, suggest 10 new experiments I haven't tried yet, ordered by expected impact.");
        prompt.AppendLine();
        prompt.AppendLine("Posts with retention scores:");
        AppendPosts(prompt, posts, experimentItems);
        prompt.AppendLine();
        prompt.AppendLine("Data points (observations):");
        AppendDataPoints(prompt, dataPoints);
        prompt.AppendLine();
        prompt.AppendLine("Experiments already run or planned:");
        if (experimentItems.Count == 0)
            prompt.AppendLine("None.");
        else
            foreach (var item in experimentItems)
                prompt.AppendLine($"- {item.Title}: {item.Description} ({(item.IsRun ? "run" : "planned")})");
        prompt.AppendLine();
        prompt.AppendLine("Return each suggestion as:");
        prompt.AppendLine("Title: [short name]");
        prompt.AppendLine("Description: [what to test and why it might work based on the data]");
        prompt.AppendLine();
        prompt.AppendLine("Return your suggestions in this exact C# parseable format:");
        prompt.AppendLine("newExperiment[1].title = \"Title here\";");
        prompt.AppendLine("newExperiment[1].description = \"Description here\";");
        prompt.AppendLine("newExperiment[2].title = \"Title here\";");
        prompt.AppendLine("newExperiment[2].description = \"Description here\";");
        prompt.Append("(continue for all suggestions)");
        return prompt.ToString();
    }

    private static string BuildPriorityRankingPrompt(
        List<ClipsPost> posts,
        List<ClipsDataPoint> dataPoints,
        List<ClipsExperimentItem> experimentItems,
        List<ClipsExperimentItem> unrunItems)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("Here is my AI clip experiment data. Rank the following unrun experiments by priority based on my performance data.");
        prompt.AppendLine();
        prompt.AppendLine("Posts with retention scores:");
        AppendPosts(prompt, posts, experimentItems);
        prompt.AppendLine();
        prompt.AppendLine("Data points:");
        AppendDataPoints(prompt, dataPoints);
        prompt.AppendLine();
        prompt.AppendLine("Unrun experiments to rank:");
        if (unrunItems.Count == 0)
            prompt.AppendLine("None.");
        else
            for (var index = 0; index < unrunItems.Count; index++)
                prompt.AppendLine($"{index + 1}. {unrunItems[index].Title}: {unrunItems[index].Description}");
        prompt.AppendLine();
        prompt.AppendLine("Return the experiments ranked 1 to N with a one-sentence reason for each ranking.");
        prompt.AppendLine();
        prompt.AppendLine("Return the re-ranked list in this exact C# parseable format:");
        prompt.AppendLine("priority[1].title = \"Exact experiment title\";");
        prompt.AppendLine("priority[1].reason = \"One sentence reason\";");
        prompt.AppendLine("priority[2].title = \"Exact experiment title\";");
        prompt.AppendLine("priority[2].reason = \"One sentence reason\";");
        prompt.Append("(continue for all unrun experiments in priority order)");
        return prompt.ToString();
    }

    private static void AppendPosts(
        StringBuilder prompt,
        List<ClipsPost> posts,
        List<ClipsExperimentItem> experimentItems)
    {
        if (posts.Count == 0)
        {
            prompt.AppendLine("None.");
            return;
        }

        var groupedPosts = posts
            .Select(post => new
            {
                Post = post,
                Experiment = FindExperimentForPost(post, experimentItems)
            })
            .GroupBy(item => item.Experiment?.Id)
            .OrderByDescending(group => group.First().Experiment?.StartDate)
            .ToList();

        foreach (var group in groupedPosts)
        {
            var experiment = group.First().Experiment;
            prompt.AppendLine(experiment == null
                ? "No matching experiment:"
                : $"{experiment.Title} ({FormatDateRange(experiment)}):");
            foreach (var item in group)
            {
                var post = item.Post;
                prompt.AppendLine($"- {post.Title} | Hook: {post.Concept}");
            }
        }
    }

    private static ClipsExperimentItem? FindExperimentForPost(
        ClipsPost post,
        IEnumerable<ClipsExperimentItem> experimentItems)
    {
        if (!post.ExperimentItemId.HasValue) return null;
        return experimentItems.FirstOrDefault(item => item.Id == post.ExperimentItemId.Value);
    }

    private static string FormatDateRange(ClipsExperimentItem item)
    {
        if (!item.StartDate.HasValue && !item.EndDate.HasValue)
            return "Dates not set";
        if (!item.StartDate.HasValue)
            return $"Through {item.EndDate!.Value:MMM d, yyyy}";
        if (!item.EndDate.HasValue)
            return $"From {item.StartDate.Value:MMM d, yyyy}";

        var start = item.StartDate.Value;
        var end = item.EndDate.Value;
        return start.Year == end.Year
            ? $"{start:MMM d} – {end:MMM d, yyyy}"
            : $"{start:MMM d, yyyy} – {end:MMM d, yyyy}";
    }

    private static void AppendDataPoints(StringBuilder prompt, List<ClipsDataPoint> dataPoints)
    {
        if (dataPoints.Count == 0)
        {
            prompt.AppendLine("None.");
            return;
        }

        foreach (var dataPoint in dataPoints)
            prompt.AppendLine($"- {dataPoint.CreatedDate.ToLocalTime():yyyy-MM-dd}: {dataPoint.Text}");
    }

    private async Task BuildAveragesSectionAsync(ClipsExperiment experiment, List<ClipsPost> posts)
    {
        var averages = await _service.GetConceptAveragesByPlatformAsync(experiment.Id);
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

        var allPlatforms = averages.Values
            .SelectMany(byPlatform => byPlatform.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(platform => platform)
            .ToList();

        foreach (var platformName in allPlatforms)
        {
            _content.Children.Add(new Label
            {
                Text = platformName,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0"),
                Margin = new Thickness(0, 8, 0, 0)
            });

            var platformStats = averages
                .Where(item => item.Value.ContainsKey(platformName))
                .ToDictionary(item => item.Key, item => item.Value[platformName]);

            if (platformStats.Count == 0) continue;

            double best10 = platformStats.Values.Max(value => value.avg10);
            double best20 = platformStats.Values.Max(value => value.avg20);
            double best30 = platformStats.Values.Max(value => value.avg30);
            int bestViews = platformStats.Values.Max(value => value.totalViews);

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
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
            AddCell(grid, "Views", 4, 0, true);
            AddCell(grid, "Posts", 5, 0, true);

            int row = 1;
            foreach (var item in platformStats.OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase))
            {
                var value = item.Value;
                AddCell(grid, item.Key, 0, row, false);
                AddCell(grid, FormatAverage(value.avg10, best10), 1, row, value.avg10 == best10 && best10 > 0);
                AddCell(grid, FormatAverage(value.avg20, best20), 2, row, value.avg20 == best20 && best20 > 0);
                AddCell(grid, FormatAverage(value.avg30, best30), 3, row, value.avg30 == best30 && best30 > 0);
                AddCell(grid, value.totalViews > 0 ? value.totalViews.ToString() : "-", 4, row, value.totalViews == bestViews && bestViews > 0);
                AddCell(grid, value.count.ToString(), 5, row, false);
                row++;
            }
            _content.Children.Add(new Frame { Content = grid, Padding = 10, BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = false });
        }
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

    private static Editor MakeEditor(string placeholder)
        => new()
        {
            Placeholder = placeholder,
            FontSize = 13,
            BackgroundColor = Colors.White,
            MinimumHeightRequest = 90,
            AutoSize = EditorAutoSizeOption.TextChanges
        };

    private static Frame MakeCard(View content)
        => new()
        {
            Content = content,
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false
        };

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

    private static bool IsComplete(ClipsPost post) => false;

    private sealed class ParsedExperimentIdea
    {
        public int Index { get; set; }
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
    }

    private sealed class ParsedPriorityItem
    {
        public int Index { get; set; }
        public string Title { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    private sealed class ParsedDataPoint
    {
        public int Index { get; set; }
        public string Text { get; set; } = "";
    }
}

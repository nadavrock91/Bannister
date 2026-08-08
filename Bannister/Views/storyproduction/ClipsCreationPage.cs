using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ClipsCreationPage : ContentPage
{
    private readonly StoryProductionService _storyService;
    private readonly StoryProjectSelector _selector;

    private Grid _alignedGrid = null!;
    private Label _clipsHeaderLabel = null!;
    private Label _linesHeaderLabel = null!;

    private Frame _navigatorFrame = null!;
    private Editor _navigatorEditor = null!;
    private Label _navigatorTitleLabel = null!;
    private Label _navigatorCountLabel = null!;
    private Button _navigatorPrevBtn = null!;
    private Button _navigatorNextBtn = null!;
    private Button _navigatorCloseBtn = null!;
    private Button _navigatorClearBtn = null!;
    private Button _navigatorClearNextBtn = null!;
    private CheckBox _navigatorDoneCheckbox = null!;
    private Label _navigatorDoneLabel = null!;
    private EventHandler? _navigatorCloseHandler;

    private Frame _promptNavFrame = null!;
    private Label _promptNavTitleLabel = null!;
    private Label _promptNavCountLabel = null!;
    private Label _promptNavScriptLabel = null!;
    private Label _promptNavVisualLabel = null!;
    private Label _promptNavDescLabel = null!;
    private Label _promptNavPromptsLabel = null!;
    private Button _promptNavCopyBtn = null!;
    private Button _promptNavPrevBtn = null!;
    private Button _promptNavNextBtn = null!;
    private Button _promptNavCloseBtn = null!;
    private Button _promptNavMarkDoneBtn = null!;
    private EventHandler? _promptNavPrevHandler;
    private EventHandler? _promptNavNextHandler;
    private EventHandler? _promptNavCopyHandler;
    private EventHandler? _promptNavMarkDoneHandler;
    private EventHandler? _promptNavCloseHandler;

    private readonly List<(StoryLine Line, VisualShot Shot, List<VisualShot> AllShots)> _allClips = new();
    private int _currentClipIndex = -1;
    private int _promptNavIndex = -1;
    private List<int> _promptNavClipIndices = new();

    public ClipsCreationPage(AuthService auth, StoryProductionService storyService)
    {
        _storyService = storyService;
        _selector = new StoryProjectSelector(auth, storyService, this, "ClipsCreation");
        _selector.ProjectSelected += OnProjectSelectedAsync;
        _selector.ShowControls += OnShowControls;
        _selector.HideControls += OnHideControls;

        Title = "Clips Creation";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _selector.LoadProjectsAsync();
    }

    private void BuildUI()
    {
        var topStack = new VerticalStackLayout { Padding = new Thickness(20, 12, 20, 4), Spacing = 6 };
        topStack.Children.Add(new Label
        {
            Text = "\U0001F3AC Clips Creation",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });

        var selectorRow = new HorizontalStackLayout { Spacing = 8 };
        _selector.ProjectSelectFrame.WidthRequest = 200;
        selectorRow.Children.Add(_selector.ProjectSelectFrame);
        _selector.DraftPicker.WidthRequest = 130;
        selectorRow.Children.Add(_selector.DraftPicker);
        selectorRow.Children.Add(_selector.ProjectCategoryBtn);
        selectorRow.Children.Add(_selector.SeriesBtn);
        selectorRow.Children.Add(_selector.WritingProcessBtn);
        topStack.Children.Add(selectorRow);

        var actionsRow = new HorizontalStackLayout { Spacing = 6 };
        _selector.ProjectCategoryPicker.WidthRequest = 120;
        actionsRow.Children.Add(_selector.ProjectCategoryPicker);
        _selector.WritingProcessFilterBtn.HeightRequest = 32;
        _selector.WritingProcessFilterBtn.FontSize = 11;
        actionsRow.Children.Add(_selector.WritingProcessFilterBtn);

        var navigateBtn = new Button
        {
            Text = "\U0001F4DD Navigate",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 6,
            HeightRequest = 32,
            FontSize = 11,
            Padding = new Thickness(10, 0)
        };
        navigateBtn.Clicked += async (_, _) => await OpenClipNavigatorAsync();
        actionsRow.Children.Add(navigateBtn);

        var markEmptyBtn = new Button
        {
            Text = "\u2705 Empty Done",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 6,
            HeightRequest = 32,
            FontSize = 11,
            Padding = new Thickness(10, 0)
        };
        markEmptyBtn.Clicked += async (_, _) => await MarkEmptyClipsDoneAsync();
        actionsRow.Children.Add(markEmptyBtn);

        var chatGptBtn = new Button
        {
            Text = "\U0001F4AC ChatGPT Setup",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 6,
            HeightRequest = 32,
            FontSize = 11,
            Padding = new Thickness(10, 0)
        };
        chatGptBtn.Clicked += async (_, _) => await OpenPromptNavigatorAsync();
        actionsRow.Children.Add(chatGptBtn);
        topStack.Children.Add(actionsRow);

        var metaRow = new HorizontalStackLayout { Spacing = 12 };
        _selector.CurrentDraftLabel.FontSize = 10;
        _selector.ProjectMetaLabel.FontSize = 10;
        metaRow.Children.Add(_selector.CurrentDraftLabel);
        metaRow.Children.Add(_selector.ProjectMetaLabel);
        topStack.Children.Add(metaRow);

        _linesHeaderLabel = new Label { Text = "Lines", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333"), Margin = new Thickness(0, 0, 0, 4) };
        _clipsHeaderLabel = new Label { Text = "Clips", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0"), Margin = new Thickness(0, 0, 0, 4) };

        var headersGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            Padding = new Thickness(20, 0, 20, 0)
        };
        Grid.SetColumn(_linesHeaderLabel, 0);
        headersGrid.Children.Add(_linesHeaderLabel);
        Grid.SetColumn(_clipsHeaderLabel, 1);
        headersGrid.Children.Add(_clipsHeaderLabel);
        topStack.Children.Add(headersGrid);

        _alignedGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            RowSpacing = 8,
            Padding = new Thickness(20, 0, 20, 20)
        };
        var contentScroll = new ScrollView { Content = _alignedGrid, VerticalOptions = LayoutOptions.Fill };

        BuildNavigator();
        BuildPromptNavigator();

        var rootGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        Grid.SetRow(topStack, 0);
        rootGrid.Children.Add(topStack);

        Grid.SetRow(contentScroll, 1);
        rootGrid.Children.Add(contentScroll);

        Content = rootGrid;
    }

    private void BuildNavigator()
    {
        _navigatorTitleLabel = new Label { FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333"), LineBreakMode = LineBreakMode.WordWrap };
        _navigatorCountLabel = new Label { FontSize = 11, TextColor = Color.FromArgb("#888") };
        _navigatorEditor = new Editor
        {
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = 120,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#999"),
            Placeholder = "Type clip description or notes here...",
            FontSize = 13
        };

        _navigatorPrevBtn = MakeNavigatorButton("\u25C0 Back", "#E0E0E0", "#333");
        _navigatorNextBtn = MakeNavigatorButton("Next \u25B6", "#1565C0", "#FFFFFF");
        _navigatorCloseBtn = MakeNavigatorButton("Close", "#9E9E9E", "#FFFFFF");
        _navigatorClearBtn = MakeNavigatorButton("Clear", "#FFEBEE", "#C62828");
        _navigatorClearNextBtn = MakeNavigatorButton("Clear & Next \u25B6", "#FFCDD2", "#C62828");
        _navigatorPrevBtn.Clicked += async (_, _) => await NavigateClipAsync(-1);
        _navigatorNextBtn.Clicked += async (_, _) => await NavigateClipAsync(1);
        _navigatorClearBtn.Clicked += async (_, _) =>
        {
            _navigatorEditor.Text = "";
            await SaveCurrentClipAsync();
        };
        _navigatorClearNextBtn.Clicked += async (_, _) =>
        {
            _navigatorEditor.Text = "";
            await SaveCurrentClipAsync();
            await NavigateClipAsync(1);
        };

        _navigatorDoneCheckbox = new CheckBox
        {
            Color = Color.FromArgb("#4CAF50"),
            VerticalOptions = LayoutOptions.Center
        };
        _navigatorDoneCheckbox.CheckedChanged += async (_, e) =>
        {
            if (_currentClipIndex < 0 || _currentClipIndex >= _allClips.Count) return;
            var (line, shot, allShots) = _allClips[_currentClipIndex];
            shot.Done = e.Value;
            try { await _storyService.SaveShotsAsync(line, allShots); }
            catch { }
            _promptNavIndex = -1;
            _promptNavClipIndices.Clear();
        };
        _navigatorDoneLabel = new Label
        {
            Text = "Clip setup complete",
            FontSize = 12,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        };

        var navBtnRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
        navBtnRow.Children.Add(_navigatorPrevBtn);
        navBtnRow.Children.Add(_navigatorCloseBtn);
        navBtnRow.Children.Add(_navigatorClearBtn);
        navBtnRow.Children.Add(_navigatorNextBtn);
        navBtnRow.Children.Add(_navigatorClearNextBtn);

        var doneRow = new HorizontalStackLayout { Spacing = 6, HorizontalOptions = LayoutOptions.Start };
        doneRow.Children.Add(_navigatorDoneCheckbox);
        doneRow.Children.Add(_navigatorDoneLabel);

        _navigatorFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#1565C0"),
            HasShadow = true,
            IsVisible = false,
            WidthRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children = { _navigatorTitleLabel, _navigatorCountLabel, _navigatorEditor, doneRow, navBtnRow }
            }
        };
    }

    private void BuildPromptNavigator()
    {
        _promptNavTitleLabel = new Label { FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0"), LineBreakMode = LineBreakMode.WordWrap };
        _promptNavCountLabel = new Label { FontSize = 11, TextColor = Color.FromArgb("#888") };
        _promptNavScriptLabel = PromptNavLabel(12, "#333");
        _promptNavVisualLabel = PromptNavLabel(12, "#555");
        _promptNavDescLabel = PromptNavLabel(12, "#666", FontAttributes.Italic);
        _promptNavPromptsLabel = PromptNavLabel(10, "#999");

        _promptNavCopyBtn = MakePromptNavButton("\U0001F4CB Copy Description", "#1565C0", "#FFFFFF", 40, true);
        _promptNavMarkDoneBtn = MakePromptNavButton("\U0001F4AC Has ChatGPT Project & Next", "#1565C0", "#FFFFFF");
        _promptNavPrevBtn = MakePromptNavButton("\u25C0 Back", "#E0E0E0", "#333333");
        _promptNavNextBtn = MakePromptNavButton("Skip \u25B6", "#FF9800", "#FFFFFF");
        _promptNavCloseBtn = MakePromptNavButton("Close", "#9E9E9E", "#FFFFFF");

        var navRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
        navRow.Children.Add(_promptNavPrevBtn);
        navRow.Children.Add(_promptNavCloseBtn);
        navRow.Children.Add(_promptNavNextBtn);

        var actionRow = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.Center };
        actionRow.Children.Add(_promptNavCopyBtn);
        actionRow.Children.Add(_promptNavMarkDoneBtn);

        var promptContent = new VerticalStackLayout { Spacing = 8 };
        promptContent.Children.Add(_promptNavScriptLabel);
        promptContent.Children.Add(_promptNavVisualLabel);
        promptContent.Children.Add(_promptNavDescLabel);
        promptContent.Children.Add(_promptNavPromptsLabel);

        var frameContent = new VerticalStackLayout { Spacing = 10 };
        frameContent.Children.Add(_promptNavTitleLabel);
        frameContent.Children.Add(_promptNavCountLabel);
        frameContent.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#E0E0E0") });
        frameContent.Children.Add(new ScrollView { MaximumHeightRequest = 250, Content = promptContent });
        frameContent.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#E0E0E0") });
        frameContent.Children.Add(actionRow);
        frameContent.Children.Add(navRow);

        _promptNavFrame = new Frame
        {
            Padding = 20,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#1565C0"),
            HasShadow = true,
            IsVisible = false,
            WidthRequest = 560,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = frameContent
        };
    }

    private static Label PromptNavLabel(double size, string color, FontAttributes attributes = FontAttributes.None) => new()
    {
        FontSize = size,
        TextColor = Color.FromArgb(color),
        FontAttributes = attributes,
        LineBreakMode = LineBreakMode.WordWrap,
        IsVisible = false
    };

    private static Button MakePromptNavButton(string text, string background, string foreground, double height = 36, bool bold = false) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb(background),
        TextColor = Color.FromArgb(foreground),
        CornerRadius = 8,
        HeightRequest = height,
        FontSize = bold ? 13 : 12,
        FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
        Padding = new Thickness(bold ? 16 : 12, 0)
    };

    private static Button MakeNavigatorButton(string text, string background, string foreground) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb(background),
        TextColor = Color.FromArgb(foreground),
        CornerRadius = 8,
        HeightRequest = 36,
        FontSize = 12,
        Padding = new Thickness(12, 0)
    };

    private async Task OpenClipNavigatorAsync()
    {
        if (_allClips.Count == 0)
        {
            await DisplayAlert("No Clips", "No clips to navigate. Make sure the draft has lines with shots.", "OK");
            return;
        }

        _currentClipIndex = 0;
        DisplayCurrentClip();
        _navigatorFrame.IsVisible = true;

        if (Content is not Grid rootGrid || rootGrid.Children.Contains(_navigatorFrame)) return;

        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
        overlay.Children.Add(_navigatorFrame);
        var tapToDismiss = new TapGestureRecognizer();
        tapToDismiss.Tapped += async (_, _) => await CloseNavigatorAsync(rootGrid, overlay);
        overlay.GestureRecognizers.Add(tapToDismiss);

        var blockTap = new TapGestureRecognizer();
        blockTap.Tapped += (_, _) => { };
        _navigatorFrame.GestureRecognizers.Clear();
        _navigatorFrame.GestureRecognizers.Add(blockTap);
        rootGrid.Children.Add(overlay);

        if (_navigatorCloseHandler != null)
            _navigatorCloseBtn.Clicked -= _navigatorCloseHandler;
        _navigatorCloseHandler = async (_, _) => await CloseNavigatorAsync(rootGrid, overlay);
        _navigatorCloseBtn.Clicked += _navigatorCloseHandler;
    }

    private async Task CloseNavigatorAsync(Grid rootGrid, Grid overlay)
    {
        await SaveCurrentClipAsync();
        if (rootGrid.Children.Contains(overlay))
            rootGrid.Children.Remove(overlay);
        _navigatorFrame.IsVisible = false;
        await LoadClipsAsync();
    }

    private void DisplayCurrentClip()
    {
        if (_currentClipIndex < 0 || _currentClipIndex >= _allClips.Count) return;
        var (line, shot, _) = _allClips[_currentClipIndex];
        string linePreview = string.IsNullOrWhiteSpace(line.LineText) ? "[VISUAL]" : line.LineText.Length > 80 ? line.LineText[..80] + "..." : line.LineText;
        _navigatorTitleLabel.Text = $"Line {line.LineOrder} — Clip {shot.Index}\n{linePreview}";
        _navigatorCountLabel.Text = $"Clip {_currentClipIndex + 1} of {_allClips.Count}";
        _navigatorEditor.Text = shot.Description ?? "";
        _navigatorPrevBtn.IsEnabled = _currentClipIndex > 0;
        _navigatorNextBtn.IsEnabled = _currentClipIndex < _allClips.Count - 1;
        _navigatorDoneCheckbox.IsChecked = shot.Done;
        _navigatorClearNextBtn.IsEnabled = _currentClipIndex < _allClips.Count - 1;
    }

    private async Task NavigateClipAsync(int direction)
    {
        await SaveCurrentClipAsync();
        int newIndex = _currentClipIndex + direction;
        if (newIndex < 0 || newIndex >= _allClips.Count) return;
        _currentClipIndex = newIndex;
        DisplayCurrentClip();
        HighlightCurrentClipCard();
    }

    private async Task SaveCurrentClipAsync()
    {
        if (_currentClipIndex < 0 || _currentClipIndex >= _allClips.Count) return;
        var (line, shot, allShots) = _allClips[_currentClipIndex];
        var newDescription = (_navigatorEditor.Text ?? "").Trim();
        if (newDescription == (shot.Description ?? "").Trim()) return;
        shot.Description = newDescription;
        try { await _storyService.SaveShotsAsync(line, allShots); }
        catch { }
    }

    private async Task MarkEmptyClipsDoneAsync()
    {
        if (_allClips.Count == 0)
        {
            await DisplayAlert("No Clips", "No clips available.", "OK");
            return;
        }

        int marked = 0;
        var processedLines = new HashSet<int>();
        foreach (var (line, shot, _) in _allClips)
        {
            if (!shot.Done && string.IsNullOrWhiteSpace(shot.Description))
            {
                shot.Done = true;
                marked++;
                processedLines.Add(line.Id);
            }
        }

        if (marked == 0)
        {
            await DisplayAlert("Nothing to Mark", "No empty undone clips found.", "OK");
            return;
        }

        var savedLines = new HashSet<int>();
        foreach (var (line, _, allShots) in _allClips)
        {
            if (processedLines.Contains(line.Id) && savedLines.Add(line.Id))
            {
                try { await _storyService.SaveShotsAsync(line, allShots); }
                catch { }
            }
        }

        await DisplayAlert("Done", $"Marked {marked} empty clip(s) as done.", "OK");
        _promptNavIndex = -1;
        _promptNavClipIndices.Clear();
        await LoadClipsAsync();
    }

    private async Task OpenPromptNavigatorAsync()
    {
        if (_allClips.Count == 0)
        {
            await DisplayAlert("No Clips", "Select a project with clips first.", "OK");
            return;
        }

        _promptNavClipIndices = Enumerable.Range(0, _allClips.Count).Where(i => !_allClips[i].Shot.HasChatGptProject).ToList();
        if (_promptNavClipIndices.Count == 0)
        {
            await DisplayAlert("All Done", "All clips already have ChatGPT projects.", "OK");
            return;
        }

        _promptNavIndex = 0;
        DisplayPromptNavClip();
        if (Content is not Grid rootGrid) return;

        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
        var blockTap = new TapGestureRecognizer();
        blockTap.Tapped += (_, _) => { };
        _promptNavFrame.GestureRecognizers.Clear();
        _promptNavFrame.GestureRecognizers.Add(blockTap);
        overlay.Children.Add(_promptNavFrame);
        _promptNavFrame.IsVisible = true;

        ReplaceHandler(_promptNavPrevBtn, ref _promptNavPrevHandler, (_, _) =>
        {
            if (_promptNavIndex > 0) { _promptNavIndex--; DisplayPromptNavClip(); }
        });
        ReplaceHandler(_promptNavNextBtn, ref _promptNavNextHandler, (_, _) =>
        {
            if (_promptNavIndex < _promptNavClipIndices.Count - 1) { _promptNavIndex++; DisplayPromptNavClip(); }
        });
        ReplaceHandler(_promptNavCopyBtn, ref _promptNavCopyHandler, async (_, _) => await CopyCurrentPromptToClipboardAsync());
        ReplaceHandler(_promptNavMarkDoneBtn, ref _promptNavMarkDoneHandler, async (_, _) => await MarkCurrentPromptDoneAndAdvanceAsync(rootGrid, overlay));
        ReplaceHandler(_promptNavCloseBtn, ref _promptNavCloseHandler, async (_, _) =>
        {
            rootGrid.Children.Remove(overlay);
            _promptNavFrame.IsVisible = false;
            await LoadClipsAsync();
        });
        rootGrid.Children.Add(overlay);
    }

    private static void ReplaceHandler(Button button, ref EventHandler? current, EventHandler replacement)
    {
        if (current != null) button.Clicked -= current;
        current = replacement;
        button.Clicked += current;
    }

    private void DisplayPromptNavClip()
    {
        if (_promptNavIndex < 0 || _promptNavIndex >= _promptNavClipIndices.Count) return;
        int clipIndex = _promptNavClipIndices[_promptNavIndex];
        var (line, shot, _) = _allClips[clipIndex];
        string projectName = GetCurrentRootProjectName();

        _promptNavTitleLabel.Text = $"{projectName}\nLine {line.LineOrder} — Clip {shot.Index}";
        _promptNavCountLabel.Text = $"Clip {_promptNavIndex + 1} of {_promptNavClipIndices.Count} not setup";
        SetPromptLabel(_promptNavScriptLabel, line.LineText, "\U0001F4DD Narration:\n");
        SetPromptLabel(_promptNavVisualLabel, line.VisualDescription, "\U0001F3A8 Visual:\n");
        SetPromptLabel(_promptNavDescLabel, shot.Description, "\U0001F3AC Clip: ");

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(shot.ImagePrompt)) parts.Add($"Image: {Preview(shot.ImagePrompt)}");
        if (!string.IsNullOrWhiteSpace(shot.VideoPrompt)) parts.Add($"Video: {Preview(shot.VideoPrompt)}");
        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(line.ImagePrompt)) parts.Add($"Line image: {Preview(line.ImagePrompt)}");
        if (parts.Count == 0 && !string.IsNullOrWhiteSpace(line.VideoPrompt)) parts.Add($"Line video: {Preview(line.VideoPrompt)}");
        _promptNavPromptsLabel.Text = string.Join("\n", parts);
        _promptNavPromptsLabel.IsVisible = parts.Count > 0;

        _promptNavPrevBtn.IsEnabled = _promptNavIndex > 0;
        _promptNavNextBtn.IsEnabled = _promptNavIndex < _promptNavClipIndices.Count - 1;
        _promptNavMarkDoneBtn.IsEnabled = true;
    }

    private static void SetPromptLabel(Label label, string value, string prefix)
    {
        label.IsVisible = !string.IsNullOrWhiteSpace(value);
        if (label.IsVisible) label.Text = prefix + value;
    }

    private static string Preview(string value) => value.Length > 60 ? value[..60] + "..." : value;

    private string GetCurrentRootProjectName()
    {
        var project = _selector.CurrentProject;
        int rootId = project?.ParentProjectId ?? project?.Id ?? 0;
        return _selector.AllOriginalProjects.FirstOrDefault(p => p.Id == rootId)?.Name ?? project?.Name ?? "Project";
    }

    private async Task CopyCurrentPromptToClipboardAsync()
    {
        if (_promptNavIndex < 0 || _promptNavIndex >= _promptNavClipIndices.Count) return;
        int clipIndex = _promptNavClipIndices[_promptNavIndex];
        var (_, shot, _) = _allClips[clipIndex];
        string clipText = shot.Description?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(clipText))
        {
            await DisplayAlert("No Description", "This clip has no description to copy.", "OK");
            return;
        }

        await Clipboard.SetTextAsync(clipText);
        _promptNavCopyBtn.Text = "\u2705 Copied!";
        _promptNavCopyBtn.BackgroundColor = Color.FromArgb("#4CAF50");
        await Task.Delay(1500);
        _promptNavCopyBtn.Text = "\U0001F4CB Copy Description";
        _promptNavCopyBtn.BackgroundColor = Color.FromArgb("#1565C0");
    }

    private async Task MarkCurrentPromptDoneAndAdvanceAsync(Grid rootGrid, Grid overlay)
    {
        if (_promptNavIndex < 0 || _promptNavIndex >= _promptNavClipIndices.Count) return;
        var (line, shot, allShots) = _allClips[_promptNavClipIndices[_promptNavIndex]];
        shot.HasChatGptProject = true;
        try { await _storyService.SaveShotsAsync(line, allShots); }
        catch { }
        _promptNavClipIndices.RemoveAt(_promptNavIndex);

        if (_promptNavClipIndices.Count == 0)
        {
            rootGrid.Children.Remove(overlay);
            _promptNavFrame.IsVisible = false;
            await LoadClipsAsync();
            await DisplayAlert("All Done!", "All clips have ChatGPT projects.", "OK");
            return;
        }

        if (_promptNavIndex >= _promptNavClipIndices.Count) _promptNavIndex = _promptNavClipIndices.Count - 1;
        DisplayPromptNavClip();
    }

    private void HighlightCurrentClipCard()
    {
        foreach (var stack in _alignedGrid.Children.OfType<VerticalStackLayout>())
        {
            foreach (var frame in stack.Children.OfType<Frame>())
            {
                if (frame.BindingContext is int index)
                    frame.BorderColor = index == _currentClipIndex ? Color.FromArgb("#1565C0") : Color.FromArgb("#E0E0E0");
            }
        }
    }

    private async Task OnProjectSelectedAsync(StoryProject project)
    {
        _promptNavIndex = -1;
        _promptNavClipIndices.Clear();
        await LoadClipsAsync();
    }

    private void OnShowControls()
    {
        _selector.ProjectCategoryBtn.IsVisible = true;
        _selector.SeriesBtn.IsVisible = true;
        _selector.WritingProcessBtn.IsVisible = true;
        _selector.ShowDraftControls();
    }

    private void OnHideControls()
    {
        _selector.ProjectCategoryBtn.IsVisible = false;
        _selector.SeriesBtn.IsVisible = false;
        _selector.WritingProcessBtn.IsVisible = false;
        _selector.HideDraftControls();
        _alignedGrid.Children.Clear();
        _alignedGrid.RowDefinitions.Clear();
        _allClips.Clear();
        _promptNavIndex = -1;
        _promptNavClipIndices.Clear();
    }

    private async Task LoadClipsAsync()
    {
        var project = _selector.CurrentProject;
        if (project == null) return;
        var lines = await _storyService.GetLinesAsync(project.Id);
        _alignedGrid.Children.Clear();
        _alignedGrid.RowDefinitions.Clear();
        _allClips.Clear();

        if (lines.Count == 0)
        {
            _alignedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var emptyLabel = new Label { Text = "No lines in this draft yet.", FontSize = 13, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic };
            Grid.SetColumn(emptyLabel, 0);
            Grid.SetColumnSpan(emptyLabel, 2);
            _alignedGrid.Children.Add(emptyLabel);
            return;
        }

        int totalShots = 0, completedShots = 0;
        int row = 0;
        foreach (var line in lines)
        {
            var shots = _storyService.GetShots(line);
            totalShots += shots.Count;
            completedShots += shots.Count(s => s.Done);
            _alignedGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var lineFrame = new Frame { Padding = 10, CornerRadius = 8, BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = false, VerticalOptions = LayoutOptions.Start };
            var lineStack = new VerticalStackLayout { Spacing = 4 };
            string preview = string.IsNullOrWhiteSpace(line.LineText) ? "[VISUAL]" : line.LineText.Length > 80 ? line.LineText[..80] + "..." : line.LineText;
            lineStack.Children.Add(new Label { Text = $"Line {line.LineOrder}", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
            lineStack.Children.Add(new Label { Text = preview, FontSize = 11, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.WordWrap });
            if (shots.Count > 0)
            {
                int lineDone = shots.Count(s => s.Done);
                lineStack.Children.Add(new Label { Text = $"{lineDone}/{shots.Count} clips", FontSize = 10, TextColor = lineDone == shots.Count ? Color.FromArgb("#2E7D32") : Color.FromArgb("#F57C00") });
            }
            lineFrame.Content = lineStack;
            Grid.SetColumn(lineFrame, 0);
            Grid.SetRow(lineFrame, row);
            _alignedGrid.Children.Add(lineFrame);

            var clipsStack = new VerticalStackLayout { Spacing = 6, VerticalOptions = LayoutOptions.Start };

            foreach (var shot in shots)
            {
                int clipIndex = _allClips.Count;
                _allClips.Add((line, shot, shots));
                var clipFrame = new Frame
                {
                    Padding = 10,
                    CornerRadius = 8,
                    BackgroundColor = shot.Done ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFEBEE"),
                    BorderColor = shot.HasChatGptProject ? Color.FromArgb("#1565C0") : Color.FromArgb("#E0E0E0"),
                    HasShadow = false,
                    BindingContext = clipIndex
                };
                var clipStack = new VerticalStackLayout { Spacing = 2 };
                string statusIcon = shot.Done ? "\u2705" : "\U0001F534";
                string description = !string.IsNullOrWhiteSpace(shot.Description) ? (shot.Description.Length > 40 ? shot.Description[..40] + "..." : shot.Description) : "(no description)";
                clipStack.Children.Add(new Label { Text = $"{statusIcon} L{line.LineOrder} C{shot.Index}", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
                clipStack.Children.Add(new Label { Text = description, FontSize = 10, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.TailTruncation });
                string tasks = ((!shot.Task1_ImageGenerated ? "IMG " : "") + (!shot.Task2_VideoGenerated ? "VID" : "")).Trim();
                if (!string.IsNullOrEmpty(tasks))
                    clipStack.Children.Add(new Label { Text = tasks, FontSize = 9, TextColor = Color.FromArgb("#F57C00"), FontAttributes = FontAttributes.Bold });

                var cardChatGptCheckbox = new CheckBox
                {
                    IsChecked = shot.HasChatGptProject,
                    Color = Color.FromArgb("#1565C0"),
                    Scale = 0.7,
                    VerticalOptions = LayoutOptions.Center
                };

                var cardDoneCheckbox = new CheckBox
                {
                    IsChecked = shot.Done,
                    Color = Color.FromArgb("#4CAF50"),
                    Scale = 0.7,
                    VerticalOptions = LayoutOptions.Center
                };
                var capturedShotForDone = shot;
                var capturedLineForDone = line;
                var capturedShotsForDone = shots;
                cardDoneCheckbox.CheckedChanged += async (_, e) =>
                {
                    capturedShotForDone.Done = e.Value;
                    if (e.Value && !capturedShotForDone.HasChatGptProject)
                    {
                        capturedShotForDone.HasChatGptProject = true;
                        cardChatGptCheckbox.IsChecked = true;
                    }
                    try { await _storyService.SaveShotsAsync(capturedLineForDone, capturedShotsForDone); }
                    catch { }
                    clipFrame.BackgroundColor = e.Value ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFEBEE");
                };
                var cardDoneRow = new HorizontalStackLayout { Spacing = 4 };
                cardDoneRow.Children.Add(cardDoneCheckbox);
                cardDoneRow.Children.Add(new Label
                {
                    Text = "Setup done",
                    FontSize = 9,
                    TextColor = Color.FromArgb("#666"),
                    VerticalOptions = LayoutOptions.Center
                });
                clipStack.Children.Add(cardDoneRow);

                var capturedShotForChatGpt = shot;
                var capturedLineForChatGpt = line;
                var capturedShotsForChatGpt = shots;
                cardChatGptCheckbox.CheckedChanged += async (_, e) =>
                {
                    capturedShotForChatGpt.HasChatGptProject = e.Value;
                    try { await _storyService.SaveShotsAsync(capturedLineForChatGpt, capturedShotsForChatGpt); }
                    catch { }
                    clipFrame.BorderColor = e.Value ? Color.FromArgb("#1565C0") : Color.FromArgb("#E0E0E0");
                };

                var cardChatGptRow = new HorizontalStackLayout { Spacing = 4 };
                cardChatGptRow.Children.Add(cardChatGptCheckbox);
                cardChatGptRow.Children.Add(new Label
                {
                    Text = "\U0001F4AC ChatGPT",
                    FontSize = 9,
                    TextColor = Color.FromArgb("#1565C0"),
                    VerticalOptions = LayoutOptions.Center
                });
                clipStack.Children.Add(cardChatGptRow);
                clipFrame.Content = clipStack;

                var capturedLine = line;
                var capturedShot = shot;
                var capturedShots = shots;
                var tap = new TapGestureRecognizer();
                tap.Tapped += async (_, _) =>
                {
                    var clipPage = new ClipSetupPage(_storyService, capturedLine, capturedShot, capturedShots);
                    clipPage.Disappearing += async (_, _) => await LoadClipsAsync();
                    await Navigation.PushAsync(clipPage);
                };
                clipFrame.GestureRecognizers.Add(tap);
                clipsStack.Children.Add(clipFrame);
            }

            if (shots.Count == 0)
            {
                clipsStack.Children.Add(new Label
                {
                    Text = "(no clips)",
                    FontSize = 10,
                    TextColor = Color.FromArgb("#999"),
                    FontAttributes = FontAttributes.Italic
                });
            }

            Grid.SetColumn(clipsStack, 1);
            Grid.SetRow(clipsStack, row);
            _alignedGrid.Children.Add(clipsStack);
            row++;
        }

        int percent = totalShots > 0 ? (int)Math.Round(100.0 * completedShots / totalShots) : 0;
        _linesHeaderLabel.Text = $"Lines ({lines.Count})";
        _clipsHeaderLabel.Text = $"Clips ({completedShots}/{totalShots} done — {percent}%)";
    }
}

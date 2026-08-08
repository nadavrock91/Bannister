using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ClipsCreationPage : ContentPage
{
    private readonly StoryProductionService _storyService;
    private readonly StoryProjectSelector _selector;

    private VerticalStackLayout _linesContainer = null!;
    private VerticalStackLayout _clipsContainer = null!;
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

    private readonly List<(StoryLine Line, VisualShot Shot, List<VisualShot> AllShots)> _allClips = new();
    private int _currentClipIndex = -1;
    private int _copyNextIndex = -1;
    private List<int> _nonSetupClipIndices = new();

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
        var topStack = new VerticalStackLayout { Padding = new Thickness(20, 20, 20, 8), Spacing = 8 };
        topStack.Children.Add(new Label
        {
            Text = "\U0001F3AC Clips Creation",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });
        topStack.Children.Add(new Label
        {
            Text = "Select a project and draft, then manage clip creation tasks in parallel.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, -4, 0, 4)
        });

        var categoryRow = new HorizontalStackLayout { Spacing = 8 };
        categoryRow.Children.Add(_selector.ProjectCategoryPicker);
        categoryRow.Children.Add(_selector.ProjectCategoryBtn);
        categoryRow.Children.Add(_selector.SeriesBtn);
        topStack.Children.Add(categoryRow);

        var processRow = new HorizontalStackLayout { Spacing = 8 };
        processRow.Children.Add(_selector.WritingProcessFilterBtn);
        processRow.Children.Add(_selector.WritingProcessBtn);
        topStack.Children.Add(processRow);

        var projectRow = new HorizontalStackLayout { Spacing = 8 };
        projectRow.Children.Add(_selector.ProjectSelectFrame);
        topStack.Children.Add(projectRow);
        topStack.Children.Add(_selector.DraftLabel);

        var draftRow = new HorizontalStackLayout { Spacing = 8 };
        draftRow.Children.Add(_selector.DraftPicker);
        topStack.Children.Add(draftRow);
        topStack.Children.Add(_selector.CurrentDraftLabel);
        topStack.Children.Add(_selector.ProjectMetaLabel);

        var navigateBtn = new Button
        {
            Text = "\U0001F4DD Navigate Clips",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(16, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        navigateBtn.Clicked += async (_, _) => await OpenClipNavigatorAsync();
        topStack.Children.Add(navigateBtn);

        var markEmptyDoneBtn = new Button
        {
            Text = "\u2705 Mark Empty Done",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13,
            Padding = new Thickness(16, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        markEmptyDoneBtn.Clicked += async (_, _) => await MarkEmptyClipsDoneAsync();
        topStack.Children.Add(markEmptyDoneBtn);

        var copyNextPromptBtn = new Button
        {
            Text = "\U0001F4CB Copy Next Clip Prompt",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(16, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        copyNextPromptBtn.Clicked += async (_, _) => await CopyNextClipPromptAsync();
        topStack.Children.Add(copyNextPromptBtn);

        var columnsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            Padding = new Thickness(20, 0, 20, 20),
            RowDefinitions = { new RowDefinition { Height = GridLength.Star } }
        };

        _linesHeaderLabel = new Label { Text = "Lines", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333"), Margin = new Thickness(0, 0, 0, 4) };
        _linesContainer = new VerticalStackLayout { Spacing = 8 };
        var leftStack = new VerticalStackLayout { Spacing = 4 };
        leftStack.Children.Add(_linesHeaderLabel);
        leftStack.Children.Add(_linesContainer);
        var leftScroll = new ScrollView { Content = leftStack };
        Grid.SetColumn(leftScroll, 0);
        columnsGrid.Children.Add(leftScroll);

        _clipsHeaderLabel = new Label { Text = "Clips", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0"), Margin = new Thickness(0, 0, 0, 4) };
        _clipsContainer = new VerticalStackLayout { Spacing = 8 };
        var rightStack = new VerticalStackLayout { Spacing = 4 };
        rightStack.Children.Add(_clipsHeaderLabel);
        rightStack.Children.Add(_clipsContainer);
        var rightScroll = new ScrollView { Content = rightStack };
        Grid.SetColumn(rightScroll, 1);
        columnsGrid.Children.Add(rightScroll);

        BuildNavigator();

        var rootStack = new VerticalStackLayout { Children = { topStack, columnsGrid } };
        Content = new Grid { Children = { new ScrollView { Content = rootStack, VerticalOptions = LayoutOptions.Fill } } };
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
            _copyNextIndex = -1;
            _nonSetupClipIndices.Clear();
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
        _copyNextIndex = -1;
        _nonSetupClipIndices.Clear();
        await LoadClipsAsync();
    }

    private async Task CopyNextClipPromptAsync()
    {
        var project = _selector.CurrentProject;
        if (project == null || _allClips.Count == 0)
        {
            await DisplayAlert("No Clips", "Select a project with clips first.", "OK");
            return;
        }

        if (_copyNextIndex < 0)
        {
            _nonSetupClipIndices = Enumerable.Range(0, _allClips.Count)
                .Where(i => !_allClips[i].Shot.Done)
                .ToList();
            _copyNextIndex = 0;
        }

        if (_nonSetupClipIndices.Count == 0)
        {
            await DisplayAlert("All Done", "All clips are already marked as setup complete.", "OK");
            return;
        }

        if (_copyNextIndex >= _nonSetupClipIndices.Count)
        {
            await DisplayAlert("Sequence Complete",
                $"All {_nonSetupClipIndices.Count} clip prompts have been copied.\n\nRestart?",
                "OK");
            _copyNextIndex = -1;
            return;
        }

        int clipIndex = _nonSetupClipIndices[_copyNextIndex];
        var line = _allClips[clipIndex].Line;
        var shot = _allClips[clipIndex].Shot;

        int rootId = project.ParentProjectId ?? project.Id;
        var rootProject = _selector.AllOriginalProjects.FirstOrDefault(p => p.Id == rootId);
        string projectName = rootProject?.Name ?? project.Name;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"PROJECT: {projectName}");
        sb.AppendLine($"LINE {line.LineOrder} — CLIP {shot.Index}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(line.LineText))
        {
            sb.AppendLine("NARRATION:");
            sb.AppendLine(line.LineText);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(line.VisualDescription))
        {
            sb.AppendLine("VISUAL DESCRIPTION:");
            sb.AppendLine(line.VisualDescription);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(shot.Description))
        {
            sb.AppendLine("CLIP DESCRIPTION:");
            sb.AppendLine(shot.Description);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(shot.ImagePrompt))
        {
            sb.AppendLine("EXISTING IMAGE PROMPT:");
            sb.AppendLine(shot.ImagePrompt);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(shot.VideoPrompt))
        {
            sb.AppendLine("EXISTING VIDEO PROMPT:");
            sb.AppendLine(shot.VideoPrompt);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(line.ImagePrompt) && string.IsNullOrWhiteSpace(shot.ImagePrompt))
        {
            sb.AppendLine("LINE-LEVEL IMAGE PROMPT (use as reference):");
            sb.AppendLine(line.ImagePrompt);
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(line.VideoPrompt) && string.IsNullOrWhiteSpace(shot.VideoPrompt))
        {
            sb.AppendLine("LINE-LEVEL VIDEO PROMPT (use as reference):");
            sb.AppendLine(line.VideoPrompt);
            sb.AppendLine();
        }

        sb.AppendLine("TASK:");
        sb.AppendLine("Generate the starting frame image for this clip. The image should capture the exact visual described above as a single cinematic still frame suitable for video generation.");
        sb.AppendLine();
        sb.AppendLine("After generating, I will use this image as the starting frame for AI video generation (Luma/Runway) to create the motion clip.");

        await Clipboard.SetTextAsync(sb.ToString());
        int remaining = _nonSetupClipIndices.Count - _copyNextIndex - 1;
        await DisplayAlert(
            "Clip Prompt Copied",
            $"Copied prompt for Line {line.LineOrder}, Clip {shot.Index}.\n\n" +
            $"Clip {_copyNextIndex + 1} of {_nonSetupClipIndices.Count} ({remaining} remaining).\n\n" +
            $"Create a conversation in your ChatGPT project \"{projectName}\" and paste this prompt.\n\n" +
            "Tap Copy Next Clip Prompt again for the next one.",
            "OK");

        _copyNextIndex++;
    }

    private void HighlightCurrentClipCard()
    {
        foreach (var child in _clipsContainer.Children)
        {
            if (child is Frame frame && frame.BindingContext is int index)
                frame.BorderColor = index == _currentClipIndex ? Color.FromArgb("#1565C0") : Color.FromArgb("#E0E0E0");
        }
    }

    private async Task OnProjectSelectedAsync(StoryProject project)
    {
        _copyNextIndex = -1;
        _nonSetupClipIndices.Clear();
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
        _linesContainer.Children.Clear();
        _clipsContainer.Children.Clear();
        _allClips.Clear();
        _copyNextIndex = -1;
        _nonSetupClipIndices.Clear();
    }

    private async Task LoadClipsAsync()
    {
        var project = _selector.CurrentProject;
        if (project == null) return;
        var lines = await _storyService.GetLinesAsync(project.Id);
        _linesContainer.Children.Clear();
        _clipsContainer.Children.Clear();
        _allClips.Clear();

        if (lines.Count == 0)
        {
            _linesContainer.Children.Add(new Label { Text = "No lines in this draft yet.", FontSize = 13, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic });
            return;
        }

        int totalShots = 0, completedShots = 0;
        foreach (var line in lines)
        {
            var shots = _storyService.GetShots(line);
            totalShots += shots.Count;
            completedShots += shots.Count(s => s.Done);

            var lineFrame = new Frame { Padding = 10, CornerRadius = 8, BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = false };
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
            _linesContainer.Children.Add(lineFrame);

            foreach (var shot in shots)
            {
                int clipIndex = _allClips.Count;
                _allClips.Add((line, shot, shots));
                var clipFrame = new Frame
                {
                    Padding = 10,
                    CornerRadius = 8,
                    BackgroundColor = shot.Done ? Color.FromArgb("#E8F5E9") : Color.FromArgb("#FFEBEE"),
                    BorderColor = Color.FromArgb("#E0E0E0"),
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
                    try { await _storyService.SaveShotsAsync(capturedLineForDone, capturedShotsForDone); }
                    catch { }
                    _copyNextIndex = -1;
                    _nonSetupClipIndices.Clear();
                    await LoadClipsAsync();
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
                _clipsContainer.Children.Add(clipFrame);
            }
        }

        int percent = totalShots > 0 ? (int)Math.Round(100.0 * completedShots / totalShots) : 0;
        _linesHeaderLabel.Text = $"Lines ({lines.Count})";
        _clipsHeaderLabel.Text = $"Clips ({completedShots}/{totalShots} done — {percent}%)";
    }
}

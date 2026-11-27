using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page showing all clips/shots for a line with progress tracking.
/// Shows X/Y clips ready at top, then list of clips with checkmarks.
/// </summary>
public class ShotBreakdownPage : Bannister.Views.ModeThemedPage
{
    private readonly StoryProductionService _storyService;
    private readonly StoryLine _line;
    
    private Label _progressLabel;
    private VerticalStackLayout _clipsContainer;
    private List<VisualShot> _shots;

    public ShotBreakdownPage(StoryProductionService storyService, StoryLine line)
    {
        _storyService = storyService;
        _line = line;
        
        Title = $"Shot Breakdown - Line #{line.LineOrder}";

        
        BuildUI();
        LoadClips();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16
        };

        // Header
        var headerFrame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BorderColor = Colors.Transparent,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText);

        var headerStack = new VerticalStackLayout { Spacing = 8 };
        
        headerStack.Children.Add((new Label
        {
            Text = $"📸 Line #{_line.LineOrder} - Clips",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true));

        // Progress indicator
        _progressLabel = (new Label
        {
            Text = "0/0 clips ready",
            FontSize = 16
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightViolet, ThemeKey.PageText1, classifyPageText: true);
        headerStack.Children.Add(_progressLabel);

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Visual description context
        if (!string.IsNullOrEmpty(_line.VisualDescription))
        {
            var contextFrame = (new Frame
            {
                Padding = 12,
                CornerRadius = 8
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningButtonBackground);
            contextFrame.Content = (new Label
            {
                Text = $"🎨 {_line.VisualDescription}",
                FontSize = 13
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
            mainStack.Children.Add(contextFrame);
        }

        // Line text context
        if (!string.IsNullOrEmpty(_line.LineText) && !_line.IsSilent)
        {
            var lineTextFrame = (new Frame
            {
                Padding = 12,
                CornerRadius = 8
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.InfoText);
            lineTextFrame.Content = (new Label
            {
                Text = $"💬 \"{_line.LineText}\"",
                FontSize = 12,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText3, classifyPageText: true);
            mainStack.Children.Add(lineTextFrame);
        }

        // Add clip button
        var addClipBtn = (new Button
        {
            Text = "+ Add Clip",
            FontSize = 14,
            CornerRadius = 8,
            HeightRequest = 44,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(16, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        addClipBtn.Clicked += OnAddClipClicked;
        mainStack.Children.Add(addClipBtn);

        // Clips list
        _clipsContainer = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_clipsContainer);

        var scrollView = new ScrollView { Content = mainStack };
        Content = scrollView;
    }

    private void LoadClips()
    {
        _shots = _storyService.GetShots(_line);
        
        int readyCount = _shots.Count(s => s.Done || s.AllTasksDone);
        int totalCount = _shots.Count;
        
        _progressLabel.Text = $"{readyCount}/{totalCount} clips ready";
        
        RebuildClipsList();
    }

    private void RebuildClipsList()
    {
        _clipsContainer.Children.Clear();
        _shots = _storyService.GetShots(_line);

        if (_shots.Count == 0)
        {
            var emptyFrame = (new Frame
            {
                Padding = 24,
                CornerRadius = 12,
                HasShadow = true
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
            var emptyStack = new VerticalStackLayout
            {
                Spacing = 12,
                HorizontalOptions = LayoutOptions.Center
            };
            emptyStack.Children.Add(new Label
            {
                Text = "🎬",
                FontSize = 48,
                HorizontalOptions = LayoutOptions.Center
            });
            emptyStack.Children.Add((new Label
            {
                Text = "No clips yet",
                FontSize = 18,
                FontAttributes = FontAttributes.Bold,
                HorizontalOptions = LayoutOptions.Center
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1, classifyPageText: true));
            emptyStack.Children.Add((new Label
            {
                Text = "Add clips to break down this visual into separate shots",
                FontSize = 13,
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            emptyFrame.Content = emptyStack;
            _clipsContainer.Children.Add(emptyFrame);
            
            _progressLabel.Text = "0/0 clips ready";
            return;
        }

        int readyCount = _shots.Count(s => s.Done || s.AllTasksDone);
        _progressLabel.Text = $"{readyCount}/{_shots.Count} clips ready";

        foreach (var shot in _shots)
        {
            _clipsContainer.Children.Add(BuildClipCard(shot));
        }
    }

    private async Task ShowReadOnlyAlertAsync()
    {
        await DisplayAlert("Read-only", "Read-only on this device. Sync from master to modify Story Production data.", "OK");
    }

    private Frame BuildClipCard(VisualShot shot)
    {
        bool isReady = shot.Done || shot.AllTasksDone;
        int tasksComplete = (shot.Task1_ImageGenerated ? 1 : 0) + (shot.Task2_VideoGenerated ? 1 : 0);
        
        var frame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, isReady ? ThemeKey.SuccessBackground : ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, isReady ? ThemeKey.SuccessButtonBackground : ThemeKey.Separator);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),   // Checkbox
                new ColumnDefinition(GridLength.Star),   // Content
                new ColumnDefinition(GridLength.Auto),   // Progress
                new ColumnDefinition(GridLength.Auto)    // Arrow
            },
            ColumnSpacing = 12
        };

        // Checkbox
        var checkbox = (new CheckBox
        {
            IsChecked = isReady,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(CheckBox.ColorProperty, ThemeKey.SuccessButtonBackground);
        int shotIdx = shot.Index - 1;
        checkbox.CheckedChanged += async (s, e) =>
        {
            try
            {
                // Toggle all tasks when main checkbox is toggled
                shot.Task1_ImageGenerated = checkbox.IsChecked;
                shot.Task2_VideoGenerated = checkbox.IsChecked;
                shot.Done = checkbox.IsChecked;
                await _storyService.SaveShotsAsync(_line, _shots);
                RebuildClipsList();
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
            }
        };
        Grid.SetColumn(checkbox, 0);
        grid.Children.Add(checkbox);

        // Content
        var contentStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        
        contentStack.Children.Add((new Label
        {
            Text = $"Clip {shot.Index}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        if (!string.IsNullOrEmpty(shot.Description))
        {
            contentStack.Children.Add((new Label
            {
                Text = shot.Description,
                FontSize = 13,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 2
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));
        }

        Grid.SetColumn(contentStack, 1);
        grid.Children.Add(contentStack);

        // Progress badge
        var progressFrame = (new Frame
        {
            Padding = new Thickness(8, 4),
            CornerRadius = 12,
            BorderColor = Colors.Transparent,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, isReady ? ThemeKey.SuccessButtonBackground : ThemeKey.WarningBackground);
        progressFrame.Content = (new Label
        {
            Text = isReady ? "✓ Ready" : $"{tasksComplete}/2 tasks",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, isReady ? ThemeKey.ButtonPrimaryText : ThemeKey.WarningText, ThemeKey.PageText3, classifyPageText: true);
        Grid.SetColumn(progressFrame, 2);
        grid.Children.Add(progressFrame);

        // Arrow for navigation
        var arrowLabel = (new Label
        {
            Text = "›",
            FontSize = 24,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true);
        Grid.SetColumn(arrowLabel, 3);
        grid.Children.Add(arrowLabel);

        frame.Content = grid;

        // Tap to open clip setup
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) =>
        {
            var clipPage = new ClipSetupPage(_storyService, _line, shot, _shots);
            clipPage.Disappearing += (sender, args) => LoadClips();
            await Navigation.PushAsync(clipPage);
        };
        frame.GestureRecognizers.Add(tapGesture);

        return frame;
    }

    private async void OnAddClipClicked(object sender, EventArgs e)
    {
        string description = await DisplayPromptAsync(
            "Add Clip",
            "Enter a description for this clip:",
            "Add",
            "Cancel",
            placeholder: "e.g., Wide shot of the city skyline");

        if (string.IsNullOrWhiteSpace(description)) return;

        try
        {
            await _storyService.AddShotAsync(_line, description);
            LoadClips();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }
}

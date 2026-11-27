using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for setting up a single clip/shot.
/// Shows tasks required to create the clip with collapsible prompt sections.
/// </summary>
public class ClipSetupPage : Bannister.Views.ModeThemedPage
{
    private readonly StoryProductionService _storyService;
    private readonly StoryLine _line;
    private readonly VisualShot _shot;
    private readonly List<VisualShot> _allShots;
    
    private Label _progressLabel;
    private VerticalStackLayout _tasksContainer;

    public ClipSetupPage(StoryProductionService storyService, StoryLine line, VisualShot shot, List<VisualShot> allShots)
    {
        _storyService = storyService;
        _line = line;
        _shot = shot;
        _allShots = allShots;
        
        Title = $"Clip {shot.Index} Setup";

        
        BuildUI();
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
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoButtonBackground);

        var headerStack = new VerticalStackLayout { Spacing = 8 };
        
        headerStack.Children.Add((new Label
        {
            Text = $"🎬 Clip {_shot.Index}",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true));

        // Progress indicator
        int tasksComplete = (_shot.Task1_ImageGenerated ? 1 : 0) + (_shot.Task2_VideoGenerated ? 1 : 0);
        _progressLabel = (new Label
        {
            Text = $"{tasksComplete}/2 tasks complete",
            FontSize = 16
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigo, ThemeKey.PageText1, classifyPageText: true);
        headerStack.Children.Add(_progressLabel);

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Clip description
        if (!string.IsNullOrEmpty(_shot.Description))
        {
            var descFrame = (new Frame
            {
                Padding = 12,
                CornerRadius = 8
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningButtonBackground);
            descFrame.Content = (new Label
            {
                Text = $"📝 {_shot.Description}",
                FontSize = 14
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
            mainStack.Children.Add(descFrame);
        }

        // Edit description button
        var editDescBtn = (new Button
        {
            Text = "✏️ Edit Description",
            FontSize = 13,
            CornerRadius = 8,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
        editDescBtn.Clicked += OnEditDescriptionClicked;
        mainStack.Children.Add(editDescBtn);

        // Tasks container
        _tasksContainer = new VerticalStackLayout { Spacing = 12 };
        BuildTasks();
        mainStack.Children.Add(_tasksContainer);

        // Delete clip button at bottom
        var deleteBtn = (new Button
        {
            Text = "🗑️ Delete This Clip",
            FontSize = 14,
            CornerRadius = 8,
            HeightRequest = 44,
            Margin = new Thickness(0, 24, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        deleteBtn.Clicked += OnDeleteClipClicked;
        mainStack.Children.Add(deleteBtn);

        var scrollView = new ScrollView { Content = mainStack };
        Content = scrollView;
    }

    private void BuildTasks()
    {
        _tasksContainer.Children.Clear();

        // Task 1: Generate Starting Image
        _tasksContainer.Children.Add(BuildTaskCard(
            taskNumber: 1,
            title: "Generate Starting Image",
            subtitle: "Create the first frame using ChatGPT/DALL-E/Midjourney",
            isComplete: _shot.Task1_ImageGenerated,
            promptLabel: "Image Prompt",
            promptText: _shot.ImagePrompt,
            promptPlaceholder: "Cinematic shot of...",
            onToggle: async (isChecked) =>
            {
                _shot.Task1_ImageGenerated = isChecked;
                _shot.Done = _shot.AllTasksDone;
                await SaveAndRefresh();
            },
            onPromptChanged: (text) => _shot.ImagePrompt = text,
            linePrompt: _line.ImagePrompt,
            promptColor: ThemeKey.HighlightYellow,
            accentColor: ThemeKey.WarningButtonBackground
        ));

        // Task 2: Generate Video
        _tasksContainer.Children.Add(BuildTaskCard(
            taskNumber: 2,
            title: "Generate Video",
            subtitle: "Create video from image using Luma/Runway",
            isComplete: _shot.Task2_VideoGenerated,
            promptLabel: "Video Prompt",
            promptText: _shot.VideoPrompt,
            promptPlaceholder: "Camera slowly pans...",
            onToggle: async (isChecked) =>
            {
                _shot.Task2_VideoGenerated = isChecked;
                _shot.Done = _shot.AllTasksDone;
                await SaveAndRefresh();
            },
            onPromptChanged: (text) => _shot.VideoPrompt = text,
            linePrompt: _line.VideoPrompt,
            promptColor: ThemeKey.RowSelected,
            accentColor: ThemeKey.InfoText
        ));

        UpdateProgress();
    }

    private async Task ShowReadOnlyAlertAsync()
    {
        await DisplayAlert("Read-only", "Read-only on this device. Sync from master to modify Story Production data.", "OK");
    }

    private Frame BuildTaskCard(
        int taskNumber,
        string title,
        string subtitle,
        bool isComplete,
        string promptLabel,
        string promptText,
        string promptPlaceholder,
        Func<bool, Task> onToggle,
        Action<string> onPromptChanged,
        string linePrompt,
        ThemeKey promptColor,
        ThemeKey accentColor)
    {
        var frame = (new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, isComplete ? ThemeKey.SuccessBackground : ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, isComplete ? ThemeKey.SuccessButtonBackground : ThemeKey.Separator);

        var stack = new VerticalStackLayout { Spacing = 12 };

        // Task header with checkbox
        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12
        };

        var checkbox = (new CheckBox
        {
            IsChecked = isComplete,
            VerticalOptions = LayoutOptions.Start
        }).ThemeColor(CheckBox.ColorProperty, ThemeKey.SuccessButtonBackground);
        checkbox.CheckedChanged += async (s, e) => await onToggle(checkbox.IsChecked);
        Grid.SetColumn(checkbox, 0);
        headerGrid.Children.Add(checkbox);

        var titleStack = new VerticalStackLayout { Spacing = 2 };
        titleStack.Children.Add((new Label
        {
            Text = $"Task {taskNumber}: {title}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, isComplete ? ThemeKey.SuccessButtonBackground : ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        titleStack.Children.Add((new Label
        {
            Text = subtitle,
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        Grid.SetColumn(titleStack, 1);
        headerGrid.Children.Add(titleStack);

        stack.Children.Add(headerGrid);

        // Collapsible prompt section
        var promptSection = BuildCollapsiblePromptSection(
            promptLabel,
            promptText,
            promptPlaceholder,
            onPromptChanged,
            linePrompt,
            promptColor,
            accentColor
        );
        stack.Children.Add(promptSection);

        frame.Content = stack;
        return frame;
    }

    private View BuildCollapsiblePromptSection(
        string label,
        string promptText,
        string placeholder,
        Action<string> onChanged,
        string linePrompt,
        ThemeKey bgColor,
        ThemeKey accentColor)
    {
        var container = new VerticalStackLayout { Spacing = 8 };

        // Expand/collapse header
        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var expandLabel = (new Label
        {
            Text = "▶",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, accentColor, ThemeKey.PageText3, classifyPageText: true);
        Grid.SetColumn(expandLabel, 0);
        headerRow.Children.Add(expandLabel);

        var promptLabel = (new Label
        {
            Text = $"📋 {label}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1, classifyPageText: true);
        Grid.SetColumn(promptLabel, 1);
        headerRow.Children.Add(promptLabel);

        // Copy button (always visible)
        var copyBtn = (new Button
        {
            Text = "📋 Copy",
            HeightRequest = 30,
            Padding = new Thickness(10, 0),
            CornerRadius = 6,
            FontSize = 12
        }).ThemeColor(VisualElement.BackgroundColorProperty, bgColor).ThemeColor(Button.TextColorProperty, accentColor);
        string currentPrompt = promptText;
        copyBtn.Clicked += async (s, e) =>
        {
            if (!string.IsNullOrEmpty(currentPrompt))
            {
                await Clipboard.SetTextAsync(currentPrompt);
                copyBtn.Text = "✓ Copied";
                await Task.Delay(1500);
                copyBtn.Text = "📋 Copy";
            }
        };
        Grid.SetColumn(copyBtn, 2);
        headerRow.Children.Add(copyBtn);

        container.Children.Add(headerRow);

        // Collapsible content
        var contentFrame = (new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            IsVisible = false  // Collapsed by default
        }).ThemeColor(VisualElement.BackgroundColorProperty, bgColor).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, accentColor);

        var contentStack = new VerticalStackLayout { Spacing = 8 };

        // Fill from line-level prompt button (if available)
        if (!string.IsNullOrEmpty(linePrompt))
        {
            var fillRow = new HorizontalStackLayout { Spacing = 8 };
            fillRow.Children.Add((new Label
            {
                Text = "💡 Line-level prompt available:",
                FontSize = 11,
                VerticalOptions = LayoutOptions.Center
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
            var fillBtn = (new Button
            {
                Text = "⬇️ Use Line Prompt",
                HeightRequest = 28,
                Padding = new Thickness(8, 0),
                CornerRadius = 4,
                FontSize = 11
            }).ThemeColor(VisualElement.BackgroundColorProperty, accentColor).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
            fillBtn.Clicked += async (s, e) =>
            {
                onChanged(linePrompt);
                currentPrompt = linePrompt;
                await SaveShot();
                BuildTasks(); // Rebuild to show updated prompt
            };
            fillRow.Children.Add(fillBtn);
            contentStack.Children.Add(fillRow);
        }

        // Prompt editor
        var editor = (new Editor
        {
            Text = promptText,
            Placeholder = placeholder,
            HeightRequest = 80,
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        editor.TextChanged += (s, e) =>
        {
            onChanged(editor.Text);
            currentPrompt = editor.Text;
        };
        editor.Unfocused += async (s, e) => await SaveShot();
        contentStack.Children.Add(editor);

        contentFrame.Content = contentStack;
        container.Children.Add(contentFrame);

        // Toggle expand/collapse
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += (s, e) =>
        {
            contentFrame.IsVisible = !contentFrame.IsVisible;
            expandLabel.Text = contentFrame.IsVisible ? "▼" : "▶";
        };
        headerRow.GestureRecognizers.Add(tapGesture);

        return container;
    }

    private void UpdateProgress()
    {
        int tasksComplete = (_shot.Task1_ImageGenerated ? 1 : 0) + (_shot.Task2_VideoGenerated ? 1 : 0);
        _progressLabel.Text = $"{tasksComplete}/2 tasks complete";
    }

    private async Task SaveShot()
    {
        try
        {
            await _storyService.SaveShotsAsync(_line, _allShots);
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task SaveAndRefresh()
    {
        await SaveShot();
        UpdateProgress();
        BuildTasks();
    }

    private async void OnEditDescriptionClicked(object sender, EventArgs e)
    {
        string newDesc = await DisplayPromptAsync(
            "Edit Description",
            "Enter clip description:",
            "Save",
            "Cancel",
            initialValue: _shot.Description,
            placeholder: "Describe this clip...");

        if (newDesc == null) return; // Cancelled

        _shot.Description = newDesc;
        await SaveShot();
        
        // Rebuild UI to show new description
        BuildUI();
    }

    private async void OnDeleteClipClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(
            "Delete Clip?",
            $"Delete Clip {_shot.Index}?\n\nThis cannot be undone.",
            "Delete",
            "Cancel");

        if (!confirm) return;

        try
        {
            int shotIdx = _shot.Index - 1;
            await _storyService.DeleteShotAsync(_line, shotIdx);
            await Navigation.PopAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }
}

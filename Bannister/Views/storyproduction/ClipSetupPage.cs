using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for setting up a single clip/shot.
/// Shows tasks required to create the clip with collapsible prompt sections.
/// </summary>
public class ClipSetupPage : ContentPage
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
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
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
        var headerFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#1976D2"),
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var headerStack = new VerticalStackLayout { Spacing = 8 };
        
        headerStack.Children.Add(new Label
        {
            Text = $"🎬 Clip {_shot.Index}",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        // Progress indicator
        int tasksComplete = (_shot.Task1_ImageGenerated ? 1 : 0) + (_shot.Task2_VideoGenerated ? 1 : 0);
        _progressLabel = new Label
        {
            Text = $"{tasksComplete}/2 tasks complete",
            FontSize = 16,
            TextColor = Color.FromArgb("#BBDEFB")
        };
        headerStack.Children.Add(_progressLabel);

        headerFrame.Content = headerStack;
        mainStack.Children.Add(headerFrame);

        // Clip description
        if (!string.IsNullOrEmpty(_shot.Description))
        {
            var descFrame = new Frame
            {
                BackgroundColor = Color.FromArgb("#FFF8E1"),
                Padding = 12,
                CornerRadius = 8,
                BorderColor = Color.FromArgb("#FFB300")
            };
            descFrame.Content = new Label
            {
                Text = $"📝 {_shot.Description}",
                FontSize = 14,
                TextColor = Color.FromArgb("#666")
            };
            mainStack.Children.Add(descFrame);
        }

        // Edit description button
        var editDescBtn = new Button
        {
            Text = "✏️ Edit Description",
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            TextColor = Color.FromArgb("#E65100"),
            FontSize = 13,
            CornerRadius = 8,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(12, 0)
        };
        editDescBtn.Clicked += OnEditDescriptionClicked;
        mainStack.Children.Add(editDescBtn);

        // Tasks container
        _tasksContainer = new VerticalStackLayout { Spacing = 12 };
        BuildTasks();
        mainStack.Children.Add(_tasksContainer);

        // Delete clip button at bottom
        var deleteBtn = new Button
        {
            Text = "🗑️ Delete This Clip",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            FontSize = 14,
            CornerRadius = 8,
            HeightRequest = 44,
            Margin = new Thickness(0, 24, 0, 0)
        };
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
            promptColor: Color.FromArgb("#FFFDE7"),
            accentColor: Color.FromArgb("#FFC107")
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
            promptColor: Color.FromArgb("#E3F2FD"),
            accentColor: Color.FromArgb("#2196F3")
        ));

        UpdateProgress();
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
        Color promptColor,
        Color accentColor)
    {
        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = isComplete ? Color.FromArgb("#E8F5E9") : Colors.White,
            BorderColor = isComplete ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

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

        var checkbox = new CheckBox
        {
            IsChecked = isComplete,
            Color = Color.FromArgb("#4CAF50"),
            VerticalOptions = LayoutOptions.Start
        };
        checkbox.CheckedChanged += async (s, e) => await onToggle(checkbox.IsChecked);
        Grid.SetColumn(checkbox, 0);
        headerGrid.Children.Add(checkbox);

        var titleStack = new VerticalStackLayout { Spacing = 2 };
        titleStack.Children.Add(new Label
        {
            Text = $"Task {taskNumber}: {title}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = isComplete ? Color.FromArgb("#4CAF50") : Color.FromArgb("#333")
        });
        titleStack.Children.Add(new Label
        {
            Text = subtitle,
            FontSize = 12,
            TextColor = Color.FromArgb("#999")
        });
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
        Color bgColor,
        Color accentColor)
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

        var expandLabel = new Label
        {
            Text = "▶",
            FontSize = 12,
            TextColor = accentColor,
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(expandLabel, 0);
        headerRow.Children.Add(expandLabel);

        var promptLabel = new Label
        {
            Text = $"📋 {label}",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(promptLabel, 1);
        headerRow.Children.Add(promptLabel);

        // Copy button (always visible)
        var copyBtn = new Button
        {
            Text = "📋 Copy",
            BackgroundColor = bgColor,
            TextColor = accentColor,
            HeightRequest = 30,
            Padding = new Thickness(10, 0),
            CornerRadius = 6,
            FontSize = 12
        };
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
        var contentFrame = new Frame
        {
            BackgroundColor = bgColor,
            Padding = 12,
            CornerRadius = 8,
            BorderColor = accentColor,
            IsVisible = false  // Collapsed by default
        };

        var contentStack = new VerticalStackLayout { Spacing = 8 };

        // Fill from line-level prompt button (if available)
        if (!string.IsNullOrEmpty(linePrompt))
        {
            var fillRow = new HorizontalStackLayout { Spacing = 8 };
            fillRow.Children.Add(new Label
            {
                Text = "💡 Line-level prompt available:",
                FontSize = 11,
                TextColor = Color.FromArgb("#666"),
                VerticalOptions = LayoutOptions.Center
            });
            var fillBtn = new Button
            {
                Text = "⬇️ Use Line Prompt",
                BackgroundColor = accentColor,
                TextColor = Colors.White,
                HeightRequest = 28,
                Padding = new Thickness(8, 0),
                CornerRadius = 4,
                FontSize = 11
            };
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
        var editor = new Editor
        {
            Text = promptText,
            Placeholder = placeholder,
            HeightRequest = 80,
            FontSize = 13,
            BackgroundColor = Colors.White
        };
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
        await _storyService.SaveShotsAsync(_line, _allShots);
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

        int shotIdx = _shot.Index - 1;
        await _storyService.DeleteShotAsync(_line, shotIdx);
        await Navigation.PopAsync();
    }
}

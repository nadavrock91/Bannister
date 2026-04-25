using Bannister.Models;
using Bannister.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bannister.Views;

/// <summary>
/// Image Production hub — create/continue projects, then enter sub-workflows.
/// Currently supports: Generate Hook First Frame.
/// 
/// Flow:
///   1. Create new project or select existing
///   2. Paste story/video concept into project
///   3. Enter "Hook First Frame" to generate prompts for LLM
/// </summary>
public class ImageProductionPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ImageProductionService _imageService;

    // UI
    private Picker _projectPicker;
    private Label _projectInfoLabel;
    private Editor _storyEditor;
    private Button _saveStoryBtn;
    private VerticalStackLayout _workflowContainer;

    // State
    private List<ImageProject> _projects = new();
    private ImageProject? _currentProject;

    public ImageProductionPage(AuthService auth, ImageProductionService imageService)
    {
        _auth = auth;
        _imageService = imageService;
        Title = "Image Production";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProjectsAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 20, Spacing = 16 };

        // Header
        mainStack.Children.Add(new Label
        {
            Text = "🎨 Image Production",
            FontSize = 26, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828"),
            HorizontalOptions = LayoutOptions.Center
        });

        // ====== PROJECT SELECTION ======
        var projectSection = new Frame
        {
            BackgroundColor = Colors.White, CornerRadius = 10,
            Padding = 16, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = true
        };

        var projectStack = new VerticalStackLayout { Spacing = 10 };

        projectStack.Children.Add(new Label
        {
            Text = "PROJECT", FontSize = 12, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#888"), CharacterSpacing = 1
        });

        var pickerRow = new HorizontalStackLayout { Spacing = 8 };

        _projectPicker = new Picker
        {
            Title = "Select project...", FontSize = 14,
            BackgroundColor = Color.FromArgb("#FAFAFA"), WidthRequest = 280
        };
        _projectPicker.SelectedIndexChanged += OnProjectSelected;
        pickerRow.Children.Add(_projectPicker);

        var newBtn = new Button
        {
            Text = "+ New Project", FontSize = 12, HeightRequest = 36,
            BackgroundColor = Color.FromArgb("#C62828"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(12, 0)
        };
        newBtn.Clicked += OnNewProjectClicked;
        pickerRow.Children.Add(newBtn);

        var deleteBtn = new Button
        {
            Text = "🗑️", FontSize = 14, HeightRequest = 36, WidthRequest = 40,
            BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 6, Padding = 0
        };
        deleteBtn.Clicked += OnDeleteProjectClicked;
        pickerRow.Children.Add(deleteBtn);

        projectStack.Children.Add(pickerRow);

        _projectInfoLabel = new Label
        {
            Text = "No project selected", FontSize = 12,
            TextColor = Color.FromArgb("#999")
        };
        projectStack.Children.Add(_projectInfoLabel);

        projectSection.Content = projectStack;
        mainStack.Children.Add(projectSection);

        // ====== STORY / VIDEO CONCEPT ======
        var storySection = new Frame
        {
            BackgroundColor = Colors.White, CornerRadius = 10,
            Padding = 16, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = true,
            IsVisible = false
        };

        var storyStack = new VerticalStackLayout { Spacing = 10 };

        storyStack.Children.Add(new Label
        {
            Text = "STORY / VIDEO CONCEPT", FontSize = 12, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#888"), CharacterSpacing = 1
        });

        storyStack.Children.Add(new Label
        {
            Text = "Paste what the story or video is about. This feeds into all prompt generation.",
            FontSize = 12, TextColor = Color.FromArgb("#666")
        });

        _storyEditor = new Editor
        {
            Placeholder = "Paste your story concept, video idea, script summary, or narrative here...",
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            HeightRequest = 140, FontSize = 13
        };
        storyStack.Children.Add(_storyEditor);

        _saveStoryBtn = new Button
        {
            Text = "💾 Save Concept", FontSize = 13, HeightRequest = 38,
            BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(16, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        _saveStoryBtn.Clicked += OnSaveStoryClicked;
        storyStack.Children.Add(_saveStoryBtn);

        storySection.Content = storyStack;
        mainStack.Children.Add(storySection);

        // ====== WORKFLOWS ======
        _workflowContainer = new VerticalStackLayout { Spacing = 12, IsVisible = false };

        _workflowContainer.Children.Add(new Label
        {
            Text = "WORKFLOWS", FontSize = 12, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#888"), CharacterSpacing = 1,
            Margin = new Thickness(0, 4, 0, 0)
        });

        // Hook First Frame workflow button
        var hookBtn = CreateWorkflowButton(
            "🎬 Generate Hook First Frame",
            "Get 10 ideas for a compelling opening frame that works as thumbnail and video start",
            Color.FromArgb("#FF6F00"));
        hookBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnHookFirstFrameClicked())
        });
        _workflowContainer.Children.Add(hookBtn);

        // Clip Start Frame workflow button
        var clipBtn = CreateWorkflowButton(
            "🎞️ Generate Clip Start Frame",
            "Get 10 ideas for the opening frame of a specific scene/clip",
            Color.FromArgb("#7B1FA2"));
        clipBtn.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () => await OnClipStartFrameClicked())
        });
        _workflowContainer.Children.Add(clipBtn);

        mainStack.Children.Add(_workflowContainer);

        Content = new ScrollView { Content = mainStack };

        // Store refs for visibility toggling
        _storySection = storySection;
    }

    private Frame _storySection;

    private Frame CreateWorkflowButton(string title, string subtitle, Color accentColor)
    {
        var frame = new Frame
        {
            BackgroundColor = accentColor.WithAlpha(0.08f),
            BorderColor = accentColor.WithAlpha(0.3f),
            CornerRadius = 10, Padding = 16, HasShadow = true
        };

        var stack = new HorizontalStackLayout { Spacing = 12 };

        var textStack = new VerticalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        textStack.Children.Add(new Label
        {
            Text = title, FontSize = 16, FontAttributes = FontAttributes.Bold,
            TextColor = accentColor
        });
        textStack.Children.Add(new Label
        {
            Text = subtitle, FontSize = 12, TextColor = Color.FromArgb("#666")
        });
        stack.Children.Add(textStack);

        stack.Children.Add(new Label
        {
            Text = "›", FontSize = 28, TextColor = accentColor,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.EndAndExpand
        });

        frame.Content = stack;
        return frame;
    }

    // ================================================================
    //  PROJECT MANAGEMENT
    // ================================================================

    private async Task LoadProjectsAsync()
    {
        _projects = await _imageService.GetActiveProjectsAsync(_auth.CurrentUsername);

        _projectPicker.Items.Clear();
        foreach (var p in _projects)
            _projectPicker.Items.Add(p.Name);

        if (_currentProject != null)
        {
            int idx = _projects.FindIndex(p => p.Id == _currentProject.Id);
            if (idx >= 0) _projectPicker.SelectedIndex = idx;
            else SelectProject(null);
        }
        else if (_projects.Count > 0)
        {
            // Auto-select the most recent project
            _projectPicker.SelectedIndex = 0;
        }
    }

    private void OnProjectSelected(object? sender, EventArgs e)
    {
        if (_projectPicker.SelectedIndex < 0 || _projectPicker.SelectedIndex >= _projects.Count)
        {
            SelectProject(null);
            return;
        }
        SelectProject(_projects[_projectPicker.SelectedIndex]);
    }

    private void SelectProject(ImageProject? project)
    {
        _currentProject = project;

        bool hasProject = project != null;
        _storySection.IsVisible = hasProject;
        _workflowContainer.IsVisible = hasProject;

        if (project != null)
        {
            _storyEditor.Text = project.StoryDescription ?? "";
            _projectInfoLabel.Text = $"Created {project.CreatedAt:MMM d, yyyy} • {(string.IsNullOrEmpty(project.StoryDescription) ? "No concept yet" : $"{project.StoryDescription.Length} chars")}";
        }
        else
        {
            _storyEditor.Text = "";
            _projectInfoLabel.Text = "No project selected";
        }
    }

    private async void OnNewProjectClicked(object? sender, EventArgs e)
    {
        string? name = await DisplayPromptAsync("New Project", "Project name:", "Create", "Cancel",
            placeholder: "e.g. The Wizard's Deal, Product Launch Video");
        if (string.IsNullOrWhiteSpace(name)) return;

        var project = await _imageService.CreateProjectAsync(_auth.CurrentUsername, name.Trim());
        await LoadProjectsAsync();

        int idx = _projects.FindIndex(p => p.Id == project.Id);
        if (idx >= 0) _projectPicker.SelectedIndex = idx;
    }

    private async void OnDeleteProjectClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null) return;
        if (!await DisplayAlert("Delete?", $"Delete project \"{_currentProject.Name}\"?", "Delete", "Cancel")) return;

        await _imageService.DeleteProjectAsync(_currentProject.Id);
        _currentProject = null;
        SelectProject(null);
        await LoadProjectsAsync();
    }

    private async void OnSaveStoryClicked(object? sender, EventArgs e)
    {
        if (_currentProject == null) return;

        _currentProject.StoryDescription = _storyEditor.Text?.Trim() ?? "";
        await _imageService.UpdateProjectAsync(_currentProject);

        _saveStoryBtn.Text = "✓ Saved";
        _projectInfoLabel.Text = $"Created {_currentProject.CreatedAt:MMM d, yyyy} • {_currentProject.StoryDescription.Length} chars";
        await Task.Delay(1500);
        _saveStoryBtn.Text = "💾 Save Concept";
    }

    // ================================================================
    //  HOOK FIRST FRAME WORKFLOW
    // ================================================================

    private async Task OnHookFirstFrameClicked()
    {
        if (_currentProject == null) return;
        if (string.IsNullOrWhiteSpace(_currentProject.StoryDescription))
        { await DisplayAlert("No Concept", "Paste your story/video concept first.", "OK"); return; }
        var page = new ImageWorkflowPage(_currentProject, HookFirstFrameConfig.Create(), _imageService);
        await Navigation.PushAsync(page);
    }

    private async Task OnClipStartFrameClicked()
    {
        if (_currentProject == null) return;
        if (string.IsNullOrWhiteSpace(_currentProject.StoryDescription))
        { await DisplayAlert("No Concept", "Paste your story/video concept first.", "OK"); return; }
        var page = new ImageWorkflowPage(_currentProject, ClipStartFrameConfig.Create(), _imageService);
        await Navigation.PushAsync(page);
    }
}

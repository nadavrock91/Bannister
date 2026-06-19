using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace Bannister.Views;

public class WebsiteBuilderPage : ContentPage
{
    private const string IdeasPrompt = @"I'm looking for 100 website ideas to build for the purpose of making money online.

Generate the ideas based on what you know about me from public information and context: I'm Nadav, an independent content creator and software developer. I make AI-generated short films and philosophical video essays — typically 3-minute shorts and 8-minute vlog essays — with a painterly storybook aesthetic and themes around philosophy, AI, consciousness, fear, discipline, and human potential. On the software side I build Bannister, a gamified productivity app using .NET MAUI 8 and C# with SQLite, plus a few side projects involving image processing pipelines, Etsy publishing workflows, and AI image management tools. I have a daily creative discipline of producing one story or video per day. I'm comfortable with C#, .NET, basic web stack, and AI tooling.

Generate 100 distinct website ideas tailored to my background and skills that could realistically generate revenue. For each idea include:
- A short name/title (one line)
- A one-paragraph description of what the site is and how it makes money
- The primary monetization model (e.g. ads, subscription, digital product, affiliate, services, marketplace)
- Rough difficulty to build (low/medium/high) and time to first revenue (weeks/months)

Format the output as a numbered list 1 to 100. Be specific and concrete. Avoid generic ideas like 'a blog' or 'sell a course' — every idea should have a unique angle, niche, or mechanic. Some ideas should leverage my existing creator audience and content, some should leverage my dev skills, some should be standalone web products that don't depend on either.

No preamble. Start directly with idea 1.";

    private readonly AuthService _auth;
    private readonly WebsiteProjectService _service;
    private readonly Picker _projectPicker;
    private readonly Entry _titleEntry;
    private readonly Editor _ideaEditor;
    private readonly Button _deleteButton;
    private List<WebsiteProject> _projectsCache = new();
    private int _currentProjectId;
    private bool _isRefreshingPicker;

    public WebsiteBuilderPage(AuthService auth, WebsiteProjectService service)
    {
        _auth = auth;
        _service = service;
        Title = "Website Builder";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var copyPromptButton = new Button
        {
            Text = "Copy Ideas Prompt to Clipboard",
            BackgroundColor = Color.FromArgb("#01579B"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48,
            FontAttributes = FontAttributes.Bold
        };
        copyPromptButton.Clicked += async (_, _) => await CopyIdeasPromptAsync();

        _projectPicker = new Picker
        {
            Title = "Select project to load...",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        _projectPicker.SelectedIndexChanged += async (_, _) => await OnProjectSelectedAsync();

        var newProjectButton = new Button
        {
            Text = "New Project",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        newProjectButton.Clicked += (_, _) => ClearFields();

        _titleEntry = new Entry
        {
            Placeholder = "Project title",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888")
        };

        _ideaEditor = new Editor
        {
            Placeholder = "Paste the selected website idea or notes here...",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 300,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888")
        };

        var saveButton = new Button
        {
            Text = "Save Project",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48,
            FontAttributes = FontAttributes.Bold
        };
        saveButton.Clicked += async (_, _) => await SaveProjectAsync();

        _deleteButton = new Button
        {
            Text = "Delete Project",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            HeightRequest = 44,
            IsVisible = false
        };
        _deleteButton.Clicked += async (_, _) => await DeleteProjectAsync();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Website Builder",
                        FontSize = 28,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#222")
                    },
                    new Label
                    {
                        Text = "Generate website ideas via LLM, save selected ones as projects.",
                        FontSize = 15,
                        TextColor = Color.FromArgb("#666"),
                        Margin = new Thickness(0, -6, 0, 8)
                    },
                    copyPromptButton,
                    new BoxView
                    {
                        HeightRequest = 1,
                        BackgroundColor = Color.FromArgb("#DDDDDD"),
                        Margin = new Thickness(0, 8)
                    },
                    new Label
                    {
                        Text = "Or load an existing project:",
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333")
                    },
                    _projectPicker,
                    newProjectButton,
                    new Label
                    {
                        Text = "Project Title",
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333")
                    },
                    _titleEntry,
                    new Label
                    {
                        Text = "Idea / Notes",
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333")
                    },
                    _ideaEditor,
                    saveButton,
                    _deleteButton
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshProjectsPickerAsync();
    }

    private async Task CopyIdeasPromptAsync()
    {
        await Clipboard.SetTextAsync(IdeasPrompt);
        await DisplayAlert(
            "Copied",
            "Prompt copied. Paste into Gemini or ChatGPT, then copy a selected idea back into the editor below.",
            "OK");
    }

    private async Task RefreshProjectsPickerAsync(int? selectProjectId = null)
    {
        _isRefreshingPicker = true;
        _projectsCache = await _service.GetAllForUserAsync(_auth.CurrentUsername);
        _projectPicker.ItemsSource = _projectsCache.Select(project => project.Title).ToList();

        var idToSelect = selectProjectId ?? (_currentProjectId > 0 ? _currentProjectId : null);
        if (idToSelect.HasValue)
        {
            var index = _projectsCache.FindIndex(project => project.Id == idToSelect.Value);
            _projectPicker.SelectedIndex = index;
        }
        else
        {
            _projectPicker.SelectedIndex = -1;
        }

        _isRefreshingPicker = false;
    }

    private async Task OnProjectSelectedAsync()
    {
        if (_isRefreshingPicker || _projectPicker.SelectedIndex < 0 || _projectPicker.SelectedIndex >= _projectsCache.Count)
            return;

        var project = _projectsCache[_projectPicker.SelectedIndex];
        _currentProjectId = project.Id;
        _titleEntry.Text = project.Title;
        _ideaEditor.Text = project.IdeaText;
        _deleteButton.IsVisible = true;
        await Task.CompletedTask;
    }

    private async Task SaveProjectAsync()
    {
        var title = _titleEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title))
        {
            await DisplayAlert("Title required", "Enter a project title before saving.", "OK");
            return;
        }

        WebsiteProject project;
        if (_currentProjectId > 0)
        {
            project = await _service.GetByIdAsync(_currentProjectId) ?? new WebsiteProject
            {
                Id = _currentProjectId,
                Username = _auth.CurrentUsername
            };
        }
        else
        {
            project = new WebsiteProject
            {
                Username = _auth.CurrentUsername
            };
        }

        project.Title = title;
        project.IdeaText = _ideaEditor.Text?.Trim() ?? "";
        project.Username = _auth.CurrentUsername;

        _currentProjectId = await _service.SaveAsync(project);
        _deleteButton.IsVisible = true;
        await RefreshProjectsPickerAsync(_currentProjectId);
        await DisplayAlert("Saved", "Project saved.", "OK");
    }

    private async Task DeleteProjectAsync()
    {
        if (_currentProjectId <= 0)
            return;

        var confirm = await DisplayAlert("Delete this project?", "This website project will be permanently deleted.", "Delete", "Cancel");
        if (!confirm)
            return;

        await _service.DeleteAsync(_currentProjectId);
        ClearFields();
        await RefreshProjectsPickerAsync();
    }

    private void ClearFields()
    {
        _currentProjectId = 0;
        _titleEntry.Text = "";
        _ideaEditor.Text = "";
        _deleteButton.IsVisible = false;
        _projectPicker.SelectedIndex = -1;
    }
}

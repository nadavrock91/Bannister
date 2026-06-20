using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.ApplicationModel;
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

    private const string DomainNamesPromptTemplate = @"I have an idea for a website I want to build. Suggest 20 domain name candidates for it.

My idea:
Title: {0}

Description:
{1}

Constraints for the domain suggestions:
- Short (ideally 4 to 14 characters before the TLD)
- Brandable and easy to spell
- Easy to remember and pronounce
- No hyphens, no numbers
- .com preferred; also include some .ai .io .co .app variants
- Mix of literal-descriptive names (clear what the site is about) and made-up brandable names (memorable, distinctive)
- Suggest both single-word and compound-word options
- Avoid trademark conflicts with major brands

Output as a plain numbered list 1 to 20, one domain per line, with the TLD included on each line. No commentary, no explanation, no preamble. Just the numbered list of domain names.";

    private readonly AuthService _auth;
    private readonly WebsiteProjectService _projectService;
    private readonly WebsiteIdeaService _ideaService;
    private readonly Picker _ideaPicker;
    private readonly Picker _projectPicker;
    private readonly Entry _ideaTitleEntry;
    private readonly Editor _ideaEditor;
    private readonly Button _deleteIdeaButton;
    private readonly VerticalStackLayout _ideaSection;
    private readonly VerticalStackLayout _domainSection;
    private readonly Entry _purchasedDomainEntry;
    private readonly VerticalStackLayout _taskCounterSection;
    private readonly Label _projectTitleHeaderLabel;
    private readonly Label _projectIdeaReferenceLabel;
    private readonly Label _taskCountLabel;
    private readonly Button _incrementButton;
    private readonly Button _decrementButton;
    private readonly Button _editCountButton;
    private readonly Button _setTargetButton;
    private readonly Frame _celebrationFrame;
    private readonly Button _setNewTargetButton;
    private readonly Button _deleteProjectButton;

    private List<WebsiteIdea> _ideasCache = new();
    private List<WebsiteProject> _projectsCache = new();
    private int _currentIdeaId;
    private int _currentProjectId;
    private bool _isRefreshingPickers;

    public WebsiteBuilderPage(AuthService auth, WebsiteProjectService projectService, WebsiteIdeaService ideaService)
    {
        _auth = auth;
        _projectService = projectService;
        _ideaService = ideaService;
        Title = "Website Builder";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _ideaPicker = CreatePicker("Load a saved idea...");
        _ideaPicker.SelectedIndexChanged += async (_, _) => await OnIdeaSelectedAsync();

        _projectPicker = CreatePicker("Load a saved project...");
        _projectPicker.SelectedIndexChanged += async (_, _) => await OnProjectSelectedAsync();

        var newIdeaButton = CreateSecondaryButton("New Idea");
        newIdeaButton.Clicked += async (_, _) => await ClearAllAsync();

        var newProjectButton = CreateSecondaryButton("New Project");
        newProjectButton.Clicked += async (_, _) => await ClearProjectStateAsync();

        var copyIdeasButton = CreatePrimaryButton("Copy Ideas Prompt to Clipboard", Color.FromArgb("#01579B"));
        copyIdeasButton.Clicked += async (_, _) => await CopyIdeasPromptAsync();

        _ideaTitleEntry = new Entry
        {
            Placeholder = "Short idea title",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888")
        };

        _ideaEditor = new Editor
        {
            Placeholder = "Paste the selected website idea or notes here...",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 200,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888")
        };

        var saveIdeaButton = CreatePrimaryButton("Save Idea", Color.FromArgb("#2E7D32"));
        saveIdeaButton.Clicked += async (_, _) => await SaveIdeaAsync();

        _deleteIdeaButton = CreateDangerButton("Delete Idea");
        _deleteIdeaButton.IsVisible = false;
        _deleteIdeaButton.Clicked += async (_, _) => await DeleteIdeaAsync();

        var copyDomainPromptButton = CreatePrimaryButton("Copy Domain Names Prompt to Clipboard", Color.FromArgb("#01579B"));
        copyDomainPromptButton.Clicked += async (_, _) => await CopyDomainNamesPromptAsync();

        var goToGoDaddyButton = CreatePrimaryButton("Go to GoDaddy", Color.FromArgb("#01579B"));
        goToGoDaddyButton.TextColor = Colors.White;
        goToGoDaddyButton.FontAttributes = FontAttributes.Bold;
        goToGoDaddyButton.Clicked += async (_, _) => await Launcher.OpenAsync("https://www.godaddy.com/en");

        _purchasedDomainEntry = new Entry
        {
            Placeholder = "e.g. hookbrain.com",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888")
        };

        var saveProjectFromDomainButton = CreatePrimaryButton("Save as Project", Color.FromArgb("#2E7D32"));
        saveProjectFromDomainButton.Clicked += async (_, _) => await SaveProjectFromPurchasedDomainAsync();

        _domainSection = new VerticalStackLayout
        {
            Spacing = 12,
            IsVisible = false,
            Children =
            {
                CreateSectionHeader("Step 2: Pick and Purchase Domain"),
                copyDomainPromptButton,
                goToGoDaddyButton,
                new Label { Text = "Purchased Domain", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") },
                _purchasedDomainEntry,
                saveProjectFromDomainButton
            }
        };

        _ideaSection = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                CreateSectionHeader("Step 1: Ideas for Website"),
                copyIdeasButton,
                new Label { Text = "Idea Title", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") },
                _ideaTitleEntry,
                new Label { Text = "Selected Idea / Notes", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") },
                _ideaEditor,
                saveIdeaButton,
                _deleteIdeaButton
            }
        };

        _projectTitleHeaderLabel = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        };
        _projectIdeaReferenceLabel = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        _taskCountLabel = new Label
        {
            FontSize = 48,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#222")
        };
        _incrementButton = CreatePrimaryButton("+1 Task", Color.FromArgb("#2E7D32"));
        _incrementButton.FontSize = 20;
        _incrementButton.HeightRequest = 58;
        _incrementButton.Clicked += async (_, _) => await OnIncrementClickedAsync();

        _decrementButton = CreateSecondaryButton("-1 Task");
        _decrementButton.Clicked += async (_, _) => await OnDecrementClickedAsync();

        _editCountButton = CreateSecondaryButton("Edit Count");
        _editCountButton.Clicked += async (_, _) => await OnEditCountClickedAsync();

        _setTargetButton = CreateSecondaryButton("Set Target");
        _setTargetButton.Clicked += async (_, _) => await OnSetTargetClickedAsync();

        _setNewTargetButton = CreatePrimaryButton("Set New Target", Color.FromArgb("#2E7D32"));
        _setNewTargetButton.Clicked += async (_, _) => await OnSetNewTargetClickedAsync();

        _celebrationFrame = new Frame
        {
            Padding = 14,
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            BorderColor = Color.FromArgb("#2E7D32"),
            HasShadow = false,
            IsVisible = false,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "🎉 Target reached!",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1B5E20")
                    },
                    _setNewTargetButton
                }
            }
        };

        _deleteProjectButton = CreateDangerButton("Delete Project");
        _deleteProjectButton.Clicked += async (_, _) => await DeleteProjectAsync();

        var counterEditGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        counterEditGrid.Add(_decrementButton, 0, 0);
        counterEditGrid.Add(_editCountButton, 1, 0);

        _taskCounterSection = new VerticalStackLayout
        {
            Spacing = 14,
            IsVisible = false,
            Children =
            {
                _projectTitleHeaderLabel,
                _projectIdeaReferenceLabel,
                _taskCountLabel,
                _incrementButton,
                counterEditGrid,
                _setTargetButton,
                _celebrationFrame,
                _deleteProjectButton
            }
        };

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
                    CreatePickerGrid(newIdeaButton, newProjectButton),
                    _taskCounterSection,
                    _ideaSection,
                    _domainSection
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshPickersAsync();
        RefreshStateVisibility();
    }

    private View CreatePickerGrid(Button newIdeaButton, Button newProjectButton)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        grid.Add(_ideaPicker, 0, 0);
        grid.Add(_projectPicker, 1, 0);
        grid.Add(newIdeaButton, 0, 1);
        grid.Add(newProjectButton, 1, 1);
        return grid;
    }

    private static Picker CreatePicker(string title)
    {
        return new Picker
        {
            Title = title,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
    }

    private static Label CreateSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            Margin = new Thickness(0, 12, 0, 0)
        };
    }

    private static Button CreatePrimaryButton(string text, Color backgroundColor)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = backgroundColor,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 46,
            FontAttributes = FontAttributes.Bold
        };
    }

    private static Button CreateSecondaryButton(string text)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 8,
            HeightRequest = 42
        };
    }

    private static Button CreateDangerButton(string text)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            HeightRequest = 44
        };
    }

    private async Task CopyIdeasPromptAsync()
    {
        await Clipboard.SetTextAsync(IdeasPrompt);
        await DisplayAlert("Copied", "Prompt copied. Paste into Gemini or ChatGPT, then copy a selected idea back into the editor below.", "OK");
    }

    private async Task CopyDomainNamesPromptAsync()
    {
        if (_currentIdeaId <= 0)
        {
            await DisplayAlert("Save idea first", "Save or load an idea before copying the domain prompt.", "OK");
            return;
        }

        var title = _ideaTitleEntry.Text?.Trim() ?? "";
        var ideaText = _ideaEditor.Text?.Trim() ?? "";
        var prompt = string.Format(DomainNamesPromptTemplate, title, ideaText);
        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert("Copied", "Domain names prompt copied with your idea details baked in.", "OK");
    }

    private async Task RefreshPickersAsync(int? selectIdeaId = null, int? selectProjectId = null)
    {
        _isRefreshingPickers = true;
        _ideasCache = await _ideaService.GetAllForUserAsync(_auth.CurrentUsername);
        _projectsCache = await _projectService.GetAllForUserAsync(_auth.CurrentUsername);

        _ideaPicker.ItemsSource = _ideasCache.Select(idea => idea.Title).ToList();
        var projectTitles = _projectsCache.Select(project => project.Title).ToList();
        projectTitles.Add("+ New Project");
        _projectPicker.ItemsSource = projectTitles;

        SetPickerSelection(_ideaPicker, _ideasCache.Select(i => i.Id).ToList(), selectIdeaId ?? (_currentIdeaId > 0 ? _currentIdeaId : null));
        SetPickerSelection(_projectPicker, _projectsCache.Select(p => p.Id).ToList(), selectProjectId ?? (_currentProjectId > 0 ? _currentProjectId : null));
        _isRefreshingPickers = false;
    }

    private static void SetPickerSelection(Picker picker, List<int> ids, int? selectedId)
    {
        if (!selectedId.HasValue)
        {
            picker.SelectedIndex = -1;
            return;
        }

        picker.SelectedIndex = ids.IndexOf(selectedId.Value);
    }

    private async Task OnIdeaSelectedAsync()
    {
        if (_isRefreshingPickers || _ideaPicker.SelectedIndex < 0 || _ideaPicker.SelectedIndex >= _ideasCache.Count)
            return;

        var idea = _ideasCache[_ideaPicker.SelectedIndex];
        LoadIdea(idea);
        await Task.CompletedTask;
    }

    private async Task OnProjectSelectedAsync()
    {
        if (_isRefreshingPickers || _projectPicker.SelectedIndex < 0)
            return;

        if (_projectPicker.SelectedIndex == _projectsCache.Count)
        {
            await ClearProjectStateAsync();
            return;
        }

        if (_projectPicker.SelectedIndex > _projectsCache.Count)
            return;

        var project = _projectsCache[_projectPicker.SelectedIndex];
        LoadProject(project);
        await Task.CompletedTask;
    }

    private void LoadIdea(WebsiteIdea idea)
    {
        _currentIdeaId = idea.Id;
        _currentProjectId = 0;
        _ideaTitleEntry.Text = idea.Title;
        _ideaEditor.Text = idea.IdeaText;
        _purchasedDomainEntry.Text = "";
        _projectPicker.SelectedIndex = -1;
        RefreshStateVisibility();
    }

    private void LoadProject(WebsiteProject project)
    {
        _currentProjectId = project.Id;
        _currentIdeaId = 0;
        UpdateTaskCounterDisplay(project);
        _ideaTitleEntry.Text = "";
        _ideaEditor.Text = "";
        _purchasedDomainEntry.Text = "";
        _ideaPicker.SelectedIndex = -1;
        RefreshStateVisibility();
    }

    private async Task SaveIdeaAsync()
    {
        var title = _ideaTitleEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title))
        {
            await DisplayAlert("Title required", "Enter a short idea title before saving.", "OK");
            return;
        }

        WebsiteIdea idea;
        if (_currentIdeaId > 0)
        {
            idea = await _ideaService.GetByIdAsync(_currentIdeaId) ?? new WebsiteIdea
            {
                Id = _currentIdeaId,
                Username = _auth.CurrentUsername
            };
        }
        else
        {
            idea = new WebsiteIdea { Username = _auth.CurrentUsername };
        }

        idea.Username = _auth.CurrentUsername;
        idea.Title = title;
        idea.IdeaText = _ideaEditor.Text?.Trim() ?? "";
        _currentIdeaId = await _ideaService.SaveAsync(idea);
        _currentProjectId = 0;
        await RefreshPickersAsync(selectIdeaId: _currentIdeaId);
        RefreshStateVisibility();
        await DisplayAlert("Saved", "Idea saved.", "OK");
    }

    private async Task DeleteIdeaAsync()
    {
        if (_currentIdeaId <= 0)
            return;

        var confirm = await DisplayAlert("Delete this idea?", "This website idea will be permanently deleted.", "Delete", "Cancel");
        if (!confirm)
            return;

        await _ideaService.DeleteAsync(_currentIdeaId);
        await ClearAllAsync();
        await DisplayAlert("Deleted", "Idea deleted.", "OK");
    }

    private async Task DeleteProjectAsync()
    {
        if (_currentProjectId <= 0)
            return;

        var confirm = await DisplayAlert("Delete this project?", "This website project will be permanently deleted.", "Delete", "Cancel");
        if (!confirm)
            return;

        await _projectService.DeleteAsync(_currentProjectId);
        await ClearAllAsync();
        await DisplayAlert("Deleted", "Project deleted.", "OK");
    }

    private async Task SaveProjectFromPurchasedDomainAsync()
    {
        var domain = (_purchasedDomainEntry.Text ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(domain))
        {
            await DisplayAlert("Domain required", "Enter a purchased domain first.", "OK");
            return;
        }

        if (domain.Any(char.IsWhiteSpace))
        {
            await DisplayAlert("Invalid domain", "Domain cannot contain spaces.", "OK");
            return;
        }

        if (!domain.Contains('.'))
        {
            await DisplayAlert("Invalid domain", "Enter a domain like hookbrain.com.", "OK");
            return;
        }

        await PromoteIdeaToProjectAsync(domain);
    }


    private async Task PromoteIdeaToProjectAsync(string domain)
    {
        if (_currentIdeaId <= 0)
            return;

        var confirm = await DisplayAlert(
            "Save domain as project?",
            $"Save '{domain}' as the project name? This will create a project using this domain and delete the source idea.",
            "Save Project",
            "Cancel");

        if (!confirm)
            return;

        var ideaTitle = _ideaTitleEntry.Text?.Trim() ?? "";
        var ideaText = _ideaEditor.Text?.Trim() ?? "";
        var project = new WebsiteProject
        {
            Username = _auth.CurrentUsername,
            Title = domain,
            IdeaText = $"{ideaTitle}\n\n{ideaText}".Trim()
        };

        await _projectService.SaveAsync(project);
        await _ideaService.DeleteAsync(_currentIdeaId);
        await ClearAllAsync();
        await RefreshPickersAsync(selectProjectId: project.Id);
        var savedProject = await _projectService.GetByIdAsync(project.Id);
        if (savedProject != null)
            LoadProject(savedProject);
        await DisplayAlert("Project created", $"Project '{domain}' created.", "OK");
    }

    private async Task ClearAllAsync()
    {
        _currentIdeaId = 0;
        _currentProjectId = 0;
        _ideaTitleEntry.Text = "";
        _ideaEditor.Text = "";
        _projectTitleHeaderLabel.Text = "";
        _projectIdeaReferenceLabel.Text = "";
        _taskCountLabel.Text = "";
        _purchasedDomainEntry.Text = "";
        await RefreshPickersAsync();
        RefreshStateVisibility();
    }

    private async Task ClearProjectStateAsync()
    {
        _currentProjectId = 0;
        _projectTitleHeaderLabel.Text = "";
        _projectIdeaReferenceLabel.Text = "";
        _taskCountLabel.Text = "";
        _projectPicker.SelectedIndex = -1;
        RefreshStateVisibility();
        await Task.CompletedTask;
    }

    private void UpdateTaskCounterDisplay(WebsiteProject project)
    {
        _projectTitleHeaderLabel.Text = project.Title;
        _projectIdeaReferenceLabel.Text = project.IdeaText;
        _taskCountLabel.Text = $"{project.TaskCount} / {project.TaskTarget}";
        _decrementButton.IsEnabled = project.TaskCount > 0;
        _celebrationFrame.IsVisible = project.TaskCount >= project.TaskTarget;
    }

    private async Task RefreshCurrentProjectAsync()
    {
        if (_currentProjectId <= 0)
            return;

        var project = await _projectService.GetByIdAsync(_currentProjectId);
        if (project == null)
        {
            await ClearAllAsync();
            return;
        }

        UpdateTaskCounterDisplay(project);
        await RefreshPickersAsync(selectProjectId: project.Id);
        RefreshStateVisibility();
    }

    private async Task OnIncrementClickedAsync()
    {
        if (_currentProjectId <= 0)
            return;

        if (await _projectService.IncrementTaskCountAsync(_currentProjectId))
            await RefreshCurrentProjectAsync();
    }

    private async Task OnDecrementClickedAsync()
    {
        if (_currentProjectId <= 0)
            return;

        if (await _projectService.DecrementTaskCountAsync(_currentProjectId))
            await RefreshCurrentProjectAsync();
    }

    private async Task OnEditCountClickedAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var input = await DisplayPromptAsync(
            "Edit Count",
            "Enter the current completed task count:",
            "Save",
            "Cancel",
            initialValue: project.TaskCount.ToString(),
            keyboard: Keyboard.Numeric);

        if (input == null)
            return;

        if (!int.TryParse(input.Trim(), out var newCount) || newCount < 0)
        {
            await DisplayAlert("Invalid count", "Task count must be 0 or a positive number.", "OK");
            return;
        }

        if (await _projectService.SetTaskCountAsync(project.Id, newCount))
            await RefreshCurrentProjectAsync();
    }

    private async Task OnSetTargetClickedAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        await PromptAndSetTargetAsync(project, project.TaskTarget.ToString());
    }

    private async Task OnSetNewTargetClickedAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        await PromptAndSetTargetAsync(project, Math.Max(1, project.TaskTarget * 2).ToString());
    }

    private async Task PromptAndSetTargetAsync(WebsiteProject project, string initialValue)
    {
        var input = await DisplayPromptAsync(
            "Set Target",
            "Enter the task target:",
            "Save",
            "Cancel",
            initialValue: initialValue,
            keyboard: Keyboard.Numeric);

        if (input == null)
            return;

        if (!int.TryParse(input.Trim(), out var newTarget) || newTarget <= 0)
        {
            await DisplayAlert("Invalid target", "Task target must be a positive number.", "OK");
            return;
        }

        if (await _projectService.SetTaskTargetAsync(project.Id, newTarget))
            await RefreshCurrentProjectAsync();
    }

    private async Task<WebsiteProject?> GetCurrentProjectOrAlertAsync()
    {
        if (_currentProjectId <= 0)
            return null;

        var project = await _projectService.GetByIdAsync(_currentProjectId);
        if (project != null)
            return project;

        await DisplayAlert("Project missing", "This project could not be found.", "OK");
        await ClearAllAsync();
        return null;
    }

    private void RefreshStateVisibility()
    {
        var hasIdea = _currentIdeaId > 0;
        var hasProject = _currentProjectId > 0;
        _deleteIdeaButton.IsVisible = hasIdea;
        _ideaSection.IsVisible = !hasProject;
        _domainSection.IsVisible = hasIdea && !hasProject;
        _taskCounterSection.IsVisible = hasProject;
    }
}

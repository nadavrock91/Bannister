using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Reusable project and draft selection component for Story Production pages.
/// Builds UI elements, handles project picker modal, draft picker, and filtering.
/// </summary>
public class StoryProjectSelector
{
    private readonly AuthService _auth;
    private readonly StoryProductionService _storyService;
    private readonly ContentPage _page;
    private readonly string _prefsPrefix;
    private bool _isLoadingProjectCategories;

    public Label ProjectSelectLabel { get; private set; } = null!;
    public Frame ProjectSelectFrame { get; private set; } = null!;
    public Picker ProjectCategoryPicker { get; private set; } = null!;
    public Button ProjectCategoryBtn { get; private set; } = null!;
    public Button SeriesBtn { get; private set; } = null!;
    public Button WritingProcessFilterBtn { get; private set; } = null!;
    public Button WritingProcessBtn { get; private set; } = null!;
    public Label DraftLabel { get; private set; } = null!;
    public Picker DraftPicker { get; private set; } = null!;
    public Button RenameDraftBtn { get; private set; } = null!;
    public Button SetLatestBtn { get; private set; } = null!;
    public Button DeleteDraftBtn { get; private set; } = null!;
    public Button CompareToBtn { get; private set; } = null!;
    public Label CurrentDraftLabel { get; private set; } = null!;
    public Label ProjectMetaLabel { get; private set; } = null!;

    public List<StoryProject> Projects { get; private set; } = new();
    public List<StoryProject> AllOriginalProjects { get; private set; } = new();
    public List<StoryProject> Drafts { get; private set; } = new();
    public StoryProject? CurrentProject { get; set; }
    public string SelectedProjectCategory { get; set; } = "All";
    public string SelectedWritingProcess { get; set; } = "All";

    public event Func<StoryProject, Task>? ProjectSelected;
    public event Action? ShowControls;
    public event Action? HideControls;

    public StoryProjectSelector(AuthService auth, StoryProductionService storyService, ContentPage page, string prefsPrefix = "StoryProd")
    {
        _auth = auth;
        _storyService = storyService;
        _page = page;
        _prefsPrefix = prefsPrefix;
        BuildUIElements();
    }

    private void BuildUIElements()
    {
        ProjectSelectLabel = new Label
        {
            Text = "Choose a project...", FontSize = 14, TextColor = Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center, HorizontalOptions = LayoutOptions.FillAndExpand,
            Padding = new Thickness(12, 8), LineBreakMode = LineBreakMode.TailTruncation
        };
        ProjectSelectFrame = new Frame
        {
            Padding = 0, CornerRadius = 8, BackgroundColor = Color.FromArgb("#F5F5F5"),
            BorderColor = Color.FromArgb("#DDD"), HorizontalOptions = LayoutOptions.FillAndExpand,
            Content = ProjectSelectLabel
        };
        var projectTap = new TapGestureRecognizer();
        projectTap.Tapped += async (_, _) => await ShowProjectPickerModalAsync();
        ProjectSelectFrame.GestureRecognizers.Add(projectTap);

        ProjectCategoryPicker = new Picker { Title = "Category", WidthRequest = 180, BackgroundColor = Color.FromArgb("#F5F5F5") };
        ProjectCategoryPicker.SelectedIndexChanged += OnProjectCategoryFilterChanged;
        ProjectCategoryBtn = MakeButton("Set Category", "#E0F7FA", "#00838F", false);
        SeriesBtn = MakeButton("Set Series", "#FFF3E0", "#E65100", false);
        WritingProcessFilterBtn = MakeButton("Process: All", "#F3E5F5", "#7B1FA2", true);
        WritingProcessFilterBtn.FontSize = 13;
        WritingProcessFilterBtn.Clicked += OnWritingProcessFilterClicked;
        WritingProcessBtn = MakeButton("Set Process", "#F3E5F5", "#7B1FA2", false);

        DraftLabel = new Label { Text = "Draft Version", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#666"), IsVisible = false };
        DraftPicker = new Picker { Title = "Select draft...", HorizontalOptions = LayoutOptions.Start, WidthRequest = 150, BackgroundColor = Color.FromArgb("#F5F5F5"), IsVisible = false };
        DraftPicker.SelectedIndexChanged += OnDraftSelected;
        RenameDraftBtn = MakeButton("\u270F\uFE0F", "#FFF3E0", "#E65100", false);
        SetLatestBtn = MakeButton("\u2B50 Set Latest", "#FFF8E1", "#F57F17", false);
        DeleteDraftBtn = MakeButton("\U0001F5D1\uFE0F", "#FFEBEE", "#C62828", false);
        CompareToBtn = MakeButton("Compare To...", "#E8EAF6", "#3F51B5", false);
        CurrentDraftLabel = new Label { FontSize = 12, TextColor = Color.FromArgb("#666"), IsVisible = false };
        ProjectMetaLabel = new Label { FontSize = 12, TextColor = Color.FromArgb("#888"), IsVisible = false };
    }

    private static Button MakeButton(string text, string background, string foreground, bool visible) => new()
    {
        Text = text, BackgroundColor = Color.FromArgb(background), TextColor = Color.FromArgb(foreground),
        CornerRadius = 8, Padding = new Thickness(12, 8), FontSize = 12,
        IsVisible = visible, HorizontalOptions = LayoutOptions.Start
    };

    public async Task LoadProjectsAsync()
    {
        try
        {
            var allProjects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
            AllOriginalProjects = allProjects.Where(p => p.ParentProjectId == null)
                .OrderBy(p => string.IsNullOrWhiteSpace(p.ProjectCategory) ? "zzz" : p.ProjectCategory, StringComparer.OrdinalIgnoreCase)
                .ThenByDescending(p => p.CreatedAt).ToList();
            await LoadSelectedProjectCategoryAsync();
            RefreshProjectCategoryPicker();
            await LoadSelectedWritingProcessAsync();
            RefreshWritingProcessPicker();
            Projects = FilterProjectsBySelectedWritingProcess(FilterProjectsBySelectedCategory(AllOriginalProjects));

            int targetId = CurrentProject?.Id ?? Preferences.Get($"{_prefsPrefix}_LastProject_{_auth.CurrentUsername}", -1);
            if (targetId > 0)
            {
                var target = allProjects.FirstOrDefault(p => p.Id == targetId);
                if (target?.ParentProjectId != null) targetId = target.ParentProjectId.Value;
                var found = Projects.FirstOrDefault(p => p.Id == targetId);
                if (found != null) { await SelectProjectAsync(found); return; }
            }

            CurrentProject = null;
            ProjectSelectLabel.Text = "Choose a project...";
            ProjectSelectLabel.TextColor = Color.FromArgb("#999");
            HideControls?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SELECTOR] LoadProjects ERROR: {ex.Message}");
        }
    }

    public async Task SelectProjectAsync(StoryProject project)
    {
        try
        {
            ProjectSelectLabel.Text = project.Name + (project.IsPublished ? " \u2713" : project.Status == "completed" ? " (done)" : "");
            ProjectSelectLabel.TextColor = Color.FromArgb("#333");
            await LoadDraftsAsync(project.Id);
            ShowControls?.Invoke();
            if (CurrentProject != null)
                Preferences.Set($"{_prefsPrefix}_LastProject_{_auth.CurrentUsername}", CurrentProject.Id);
            if (ProjectSelected != null && CurrentProject != null)
                await ProjectSelected.Invoke(CurrentProject);
        }
        catch (Exception ex) { await _page.DisplayAlert("Error", $"Failed to load project: {ex.Message}", "OK"); }
    }

    public async Task LoadDraftsAsync(int projectId, int? selectDraftId = null)
    {
        Drafts = await _storyService.GetProjectDraftsAsync(projectId);
        DraftPicker.Items.Clear();
        foreach (var draft in Drafts)
            DraftPicker.Items.Add(draft.DraftVersion == 1 ? "Original" + (draft.IsLatest ? " \u2B50" : "") : draft.Name + (draft.IsLatest ? " \u2B50" : ""));
        ShowDraftControls();
        int index = selectDraftId.HasValue ? Drafts.FindIndex(d => d.Id == selectDraftId.Value) : Drafts.FindIndex(d => d.IsLatest);
        if (index < 0) index = 0;
        if (Drafts.Count > 0)
        {
            DraftPicker.SelectedIndex = index;
            CurrentProject = Drafts[index];
            UpdateCurrentDraftDisplay();
        }
    }

    public void RefreshProjectCategoryPicker()
    {
        _isLoadingProjectCategories = true;
        var categories = AllOriginalProjects.Select(p => string.IsNullOrWhiteSpace(p.ProjectCategory) ? "Uncategorized" : p.ProjectCategory.Trim())
            .GroupBy(c => c, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        ProjectCategoryPicker.Items.Clear();
        ProjectCategoryPicker.Items.Add("All");
        foreach (var category in categories) ProjectCategoryPicker.Items.Add(category);
        int index = ProjectCategoryPicker.Items.IndexOf(SelectedProjectCategory);
        if (index < 0) { SelectedProjectCategory = "All"; index = 0; }
        ProjectCategoryPicker.SelectedIndex = index;
        _isLoadingProjectCategories = false;
    }

    public List<StoryProject> FilterProjectsBySelectedCategory(List<StoryProject> projects) =>
        SelectedProjectCategory == "All" ? projects.ToList() :
        SelectedProjectCategory == "Uncategorized" ? projects.Where(p => string.IsNullOrWhiteSpace(p.ProjectCategory)).ToList() :
        projects.Where(p => string.Equals(p.ProjectCategory?.Trim(), SelectedProjectCategory, StringComparison.OrdinalIgnoreCase)).ToList();

    public List<StoryProject> FilterProjectsBySelectedWritingProcess(List<StoryProject> projects) =>
        SelectedWritingProcess == "All" ? projects.ToList() :
        SelectedWritingProcess == "No Process" ? projects.Where(p => string.IsNullOrWhiteSpace(p.WritingProcess)).ToList() :
        projects.Where(p => string.Equals(p.WritingProcess?.Trim(), SelectedWritingProcess, StringComparison.OrdinalIgnoreCase)).ToList();

    public void RefreshWritingProcessPicker() => WritingProcessFilterBtn.Text =
        SelectedWritingProcess == "All" ? "Process: All" : $"Process: {SelectedWritingProcess}";

    private async Task LoadSelectedProjectCategoryAsync()
    {
        try { SelectedProjectCategory = await SecureStorage.GetAsync($"story_production_category_filter_{_auth.CurrentUsername}") ?? "All"; }
        catch { SelectedProjectCategory = "All"; }
        if (string.IsNullOrWhiteSpace(SelectedProjectCategory)) SelectedProjectCategory = "All";
    }

    private async Task SaveSelectedProjectCategoryAsync()
    {
        try { await SecureStorage.SetAsync($"story_production_category_filter_{_auth.CurrentUsername}", SelectedProjectCategory); } catch { }
    }

    private async Task LoadSelectedWritingProcessAsync()
    {
        try { SelectedWritingProcess = await SecureStorage.GetAsync($"story_production_writing_process_filter_{_auth.CurrentUsername}") ?? "All"; }
        catch { SelectedWritingProcess = "All"; }
        if (string.IsNullOrWhiteSpace(SelectedWritingProcess)) SelectedWritingProcess = "All";
    }

    private async Task SaveSelectedWritingProcessAsync()
    {
        try { await SecureStorage.SetAsync($"story_production_writing_process_filter_{_auth.CurrentUsername}", SelectedWritingProcess); } catch { }
    }

    private async void OnProjectCategoryFilterChanged(object? sender, EventArgs e)
    {
        if (_isLoadingProjectCategories || ProjectCategoryPicker.SelectedIndex < 0) return;
        SelectedProjectCategory = ProjectCategoryPicker.Items[ProjectCategoryPicker.SelectedIndex];
        await SaveSelectedProjectCategoryAsync();
        ResetSelection();
        await LoadProjectsAsync();
    }

    private async void OnWritingProcessFilterClicked(object? sender, EventArgs e)
    {
        var processes = AllOriginalProjects.Select(p => string.IsNullOrWhiteSpace(p.WritingProcess) ? "No Process" : p.WritingProcess.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
        var options = new List<string> { "All" }; options.AddRange(processes);
        string? selected = await _page.DisplayActionSheet("Filter by Writing Process", "Cancel", null, options.ToArray());
        if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel") return;
        SelectedWritingProcess = selected;
        await SaveSelectedWritingProcessAsync();
        ResetSelection();
        await LoadProjectsAsync();
    }

    private void ResetSelection()
    {
        CurrentProject = null;
        ProjectSelectLabel.Text = "Choose a project...";
        ProjectSelectLabel.TextColor = Color.FromArgb("#999");
        HideControls?.Invoke();
    }

    private async void OnDraftSelected(object? sender, EventArgs e)
    {
        if (DraftPicker.SelectedIndex < 0 || DraftPicker.SelectedIndex >= Drafts.Count) return;
        CurrentProject = Drafts[DraftPicker.SelectedIndex];
        UpdateCurrentDraftDisplay();
        Preferences.Set($"{_prefsPrefix}_LastProject_{_auth.CurrentUsername}", CurrentProject.Id);
        if (ProjectSelected != null) await ProjectSelected.Invoke(CurrentProject);
    }

    private void UpdateCurrentDraftDisplay()
    {
        if (CurrentProject == null) return;
        string info = CurrentProject.DraftVersion == 1 ? "Original" : $"Draft v{CurrentProject.DraftVersion}";
        if (CurrentProject.IsLatest) info += " \u2B50";
        info += $" ({CurrentProject.DraftSource})";
        CurrentDraftLabel.Text = info;
        CurrentDraftLabel.IsVisible = true;
        var root = AllOriginalProjects.FirstOrDefault(p => p.Id == (CurrentProject.ParentProjectId ?? CurrentProject.Id));
        if (root == null) return;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(root.Series)) parts.Add($"\U0001F4DA {root.Series}");
        if (!string.IsNullOrWhiteSpace(root.ProjectCategory)) parts.Add(root.ProjectCategory);
        if (!string.IsNullOrWhiteSpace(root.WritingProcess)) parts.Add($"Process: {root.WritingProcess}");
        ProjectMetaLabel.Text = string.Join("  \u2022  ", parts);
        ProjectMetaLabel.IsVisible = parts.Count > 0;
    }

    private async Task ShowProjectPickerModalAsync()
    {
        var tcs = new TaskCompletionSource<StoryProject?>();
        var source = new List<StoryProject>(Projects);
        var filtered = new List<StoryProject>(source);
        string search = "", category = "All", series = "All", sort = "alpha_asc";
        var categories = source.Select(p => string.IsNullOrWhiteSpace(p.ProjectCategory) ? "Uncategorized" : p.ProjectCategory.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var seriesList = source.Select(p => string.IsNullOrWhiteSpace(p.Series) ? "No Series" : p.Series.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };
        var card = new Frame { CornerRadius = 12, Padding = 0, BackgroundColor = Colors.White, HasShadow = true, WidthRequest = 520, MaximumHeightRequest = 650, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        var stack = new VerticalStackLayout();
        var header = new Frame { Padding = 16, CornerRadius = 0, BackgroundColor = Color.FromArgb("#7B1FA2"), BorderColor = Colors.Transparent, Content = new Label { Text = "Select Project", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.White } };
        stack.Children.Add(header);
        var searchEntry = new Entry { Placeholder = "Search projects...", FontSize = 13, BackgroundColor = Color.FromArgb("#FAFAFA"), TextColor = Color.FromArgb("#333"), PlaceholderColor = Color.FromArgb("#999"), Margin = new Thickness(12, 8, 12, 0), HeightRequest = 38 };
        stack.Children.Add(searchEntry);
        var filterRow = new HorizontalStackLayout { Spacing = 6, Margin = new Thickness(12, 4) };
        var categoryBtn = SmallButton("Category: All", "#E0F7FA", "#00838F");
        var seriesBtn = SmallButton("Series: All", "#FFF3E0", "#E65100");
        var sortBtn = SmallButton("A-Z \u25B2", "#7B1FA2", "#FFFFFF");
        var countLabel = new Label { Text = $"{source.Count} projects", FontSize = 10, TextColor = Color.FromArgb("#999"), VerticalOptions = LayoutOptions.Center };
        filterRow.Children.Add(categoryBtn); filterRow.Children.Add(seriesBtn); filterRow.Children.Add(sortBtn); filterRow.Children.Add(countLabel);
        stack.Children.Add(filterRow);
        var list = new VerticalStackLayout { Padding = 8, Spacing = 4 };
        stack.Children.Add(new ScrollView { MaximumHeightRequest = 420, Content = list });

        void Apply()
        {
            filtered = category == "All" ? new(source) : category == "Uncategorized" ? source.Where(p => string.IsNullOrWhiteSpace(p.ProjectCategory)).ToList() : source.Where(p => string.Equals(p.ProjectCategory?.Trim(), category, StringComparison.OrdinalIgnoreCase)).ToList();
            if (series != "All") filtered = series == "No Series" ? filtered.Where(p => string.IsNullOrWhiteSpace(p.Series)).ToList() : filtered.Where(p => string.Equals(p.Series?.Trim(), series, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(search)) filtered = filtered.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || p.Description.Contains(search, StringComparison.OrdinalIgnoreCase) || p.WritingProcess.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();
            filtered = sort switch { "alpha_desc" => filtered.OrderByDescending(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList(), "date_desc" => filtered.OrderByDescending(p => p.CreatedAt).ToList(), "date_asc" => filtered.OrderBy(p => p.CreatedAt).ToList(), _ => filtered.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList() };
            countLabel.Text = search.Length == 0 && category == "All" && series == "All" ? $"{source.Count} projects" : $"{filtered.Count} of {source.Count} projects";
        }

        Frame Card(StoryProject project)
        {
            int? rootId = CurrentProject?.ParentProjectId ?? CurrentProject?.Id;
            bool selected = rootId == project.Id;
            var frame = new Frame { Padding = 12, CornerRadius = 8, BackgroundColor = selected ? Color.FromArgb("#E8EAF6") : Color.FromArgb("#F5F5F5"), BorderColor = selected ? Color.FromArgb("#7B1FA2") : Colors.Transparent, HasShadow = false };
            var body = new VerticalStackLayout { Spacing = 2 };
            body.Children.Add(new Label { Text = project.Name + (project.IsPublished ? " \u2713" : project.Status == "completed" ? " (done)" : ""), FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") });
            var meta = new List<string>(); if (!string.IsNullOrWhiteSpace(project.Series)) meta.Add($"\U0001F4DA {project.Series}"); if (!string.IsNullOrWhiteSpace(project.WritingProcess)) meta.Add(project.WritingProcess); if (!string.IsNullOrWhiteSpace(project.ProjectCategory)) meta.Add(project.ProjectCategory); meta.Add(project.CreatedAt.ToString("MMM yyyy"));
            body.Children.Add(new Label { Text = string.Join(" \u2022 ", meta), FontSize = 11, TextColor = Color.FromArgb("#888") });
            if (!string.IsNullOrWhiteSpace(project.Description)) body.Children.Add(new Label { Text = project.Description.Length > 80 ? project.Description[..80] + "..." : project.Description, FontSize = 11, TextColor = Color.FromArgb("#AAA"), LineBreakMode = LineBreakMode.TailTruncation });
            frame.Content = body;
            var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => { if (_page.Content is Grid grid) grid.Children.Remove(overlay); tcs.TrySetResult(project); }; frame.GestureRecognizers.Add(tap);
            return frame;
        }

        void Rebuild()
        {
            list.Children.Clear();
            if (filtered.Count == 0) { list.Children.Add(new Label { Text = search.Length == 0 ? "No projects in this category." : "No projects match your search.", TextColor = Color.FromArgb("#999"), HorizontalOptions = LayoutOptions.Center }); return; }
            bool byCategory = category == "All" && series == "All" && categories.Count > 1;
            bool bySeries = series == "All" && seriesList.Count > 1 && !byCategory;
            if (byCategory) AddGroups(filtered.GroupBy(p => string.IsNullOrWhiteSpace(p.ProjectCategory) ? "Uncategorized" : p.ProjectCategory.Trim(), StringComparer.OrdinalIgnoreCase), "#00838F", "");
            else if (bySeries) AddGroups(filtered.GroupBy(p => string.IsNullOrWhiteSpace(p.Series) ? "No Series" : p.Series.Trim(), StringComparer.OrdinalIgnoreCase), "#E65100", "\U0001F4DA ");
            else foreach (var project in filtered) list.Children.Add(Card(project));
        }

        void AddGroups(IEnumerable<IGrouping<string, StoryProject>> groups, string color, string prefix)
        {
            foreach (var group in groups.OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)) { list.Children.Add(new Label { Text = $"{prefix}{group.Key} ({group.Count()})", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(color), Margin = new Thickness(4, 8, 0, 2) }); foreach (var project in group) list.Children.Add(Card(project)); }
        }

        searchEntry.TextChanged += (_, e) => { search = e.NewTextValue ?? ""; Apply(); Rebuild(); };
        categoryBtn.Clicked += async (_, _) => { var options = new List<string> { "All" }; options.AddRange(categories); var value = await _page.DisplayActionSheet("Filter by Category", "Cancel", null, options.ToArray()); if (string.IsNullOrEmpty(value) || value == "Cancel") return; category = value; categoryBtn.Text = $"Category: {category}"; Apply(); Rebuild(); };
        seriesBtn.Clicked += async (_, _) => { var options = new List<string> { "All" }; options.AddRange(seriesList); var value = await _page.DisplayActionSheet("Filter by Series", "Cancel", null, options.ToArray()); if (string.IsNullOrEmpty(value) || value == "Cancel") return; series = value; seriesBtn.Text = $"Series: {series}"; Apply(); Rebuild(); };
        sortBtn.Clicked += (_, _) => { sort = sort switch { "alpha_asc" => "alpha_desc", "alpha_desc" => "date_desc", "date_desc" => "date_asc", _ => "alpha_asc" }; sortBtn.Text = sort switch { "alpha_desc" => "Z-A \u25BC", "date_desc" => "Newest \u25BC", "date_asc" => "Oldest \u25B2", _ => "A-Z \u25B2" }; Apply(); Rebuild(); };
        var cancel = new Button { Text = "Cancel", BackgroundColor = Colors.Transparent, TextColor = Color.FromArgb("#7B1FA2"), Margin = new Thickness(12, 4, 12, 12) };
        cancel.Clicked += (_, _) => { if (_page.Content is Grid grid) grid.Children.Remove(overlay); tcs.TrySetResult(null); }; stack.Children.Add(cancel);
        card.Content = stack; overlay.Children.Add(card); Apply(); Rebuild();
        if (_page.Content is Grid root) root.Children.Add(overlay); else { var old = _page.Content; var rootGrid = new Grid(); rootGrid.Children.Add(old); rootGrid.Children.Add(overlay); _page.Content = rootGrid; }
        var result = await tcs.Task; if (result != null) await SelectProjectAsync(result);
    }

    private static Button SmallButton(string text, string background, string foreground) => new() { Text = text, FontSize = 10, HeightRequest = 28, Padding = new Thickness(8, 0), CornerRadius = 4, BackgroundColor = Color.FromArgb(background), TextColor = Color.FromArgb(foreground) };

    public void ShowDraftControls() { bool show = Drafts.Count > 1; DraftLabel.IsVisible = show; DraftPicker.IsVisible = show; }
    public void HideDraftControls() { DraftLabel.IsVisible = false; DraftPicker.IsVisible = false; RenameDraftBtn.IsVisible = false; SetLatestBtn.IsVisible = false; DeleteDraftBtn.IsVisible = false; CompareToBtn.IsVisible = false; CurrentDraftLabel.IsVisible = false; ProjectMetaLabel.IsVisible = false; }
}

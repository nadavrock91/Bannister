using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ClipsCreationPage : ContentPage
{
    private readonly StoryProductionService _storyService;
    private readonly StoryProjectSelector _selector;
    private VerticalStackLayout _clipsContainer = null!;
    private Label _clipsHeaderLabel = null!;

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
        var mainStack = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        mainStack.Children.Add(new Label { Text = "\U0001F3AC Clips Creation", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        mainStack.Children.Add(new Label { Text = "Select a project and draft, then manage clip creation tasks in parallel.", FontSize = 13, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, -6, 0, 8) });

        var categoryRow = new HorizontalStackLayout { Spacing = 8 };
        categoryRow.Children.Add(_selector.ProjectCategoryPicker);
        categoryRow.Children.Add(_selector.ProjectCategoryBtn);
        categoryRow.Children.Add(_selector.SeriesBtn);
        mainStack.Children.Add(categoryRow);

        var processRow = new HorizontalStackLayout { Spacing = 8 };
        processRow.Children.Add(_selector.WritingProcessFilterBtn);
        processRow.Children.Add(_selector.WritingProcessBtn);
        mainStack.Children.Add(processRow);

        var projectRow = new HorizontalStackLayout { Spacing = 8 };
        projectRow.Children.Add(_selector.ProjectSelectFrame);
        mainStack.Children.Add(projectRow);

        mainStack.Children.Add(_selector.DraftLabel);
        var draftRow = new HorizontalStackLayout { Spacing = 8 };
        draftRow.Children.Add(_selector.DraftPicker);
        mainStack.Children.Add(draftRow);
        mainStack.Children.Add(_selector.CurrentDraftLabel);
        mainStack.Children.Add(_selector.ProjectMetaLabel);

        _clipsHeaderLabel = new Label { Text = "Select a project to see clips.", FontSize = 14, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 20) };
        _clipsContainer = new VerticalStackLayout { Spacing = 12 };
        _clipsContainer.Children.Add(_clipsHeaderLabel);
        mainStack.Children.Add(_clipsContainer);

        Content = new Grid { Children = { new ScrollView { Content = mainStack } } };
    }

    private async Task OnProjectSelectedAsync(StoryProject project) => await LoadClipsAsync();

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
        _clipsContainer.Children.Clear();
        _clipsHeaderLabel.Text = "Select a project to see clips.";
        _clipsContainer.Children.Add(_clipsHeaderLabel);
    }

    private async Task LoadClipsAsync()
    {
        var project = _selector.CurrentProject;
        if (project == null) return;
        var lines = await _storyService.GetLinesAsync(project.Id);
        _clipsContainer.Children.Clear();

        if (lines.Count == 0)
        {
            _clipsContainer.Children.Add(new Label { Text = "No lines in this draft yet. Add lines in the Drafts page first.", FontSize = 14, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 20) });
            return;
        }

        int totalShots = 0, completedShots = 0;
        foreach (var line in lines)
        {
            var shots = _storyService.GetShots(line);
            totalShots += shots.Count;
            completedShots += shots.Count(s => s.AllTasksDone);
        }

        int percent = totalShots > 0 ? (int)Math.Round(100.0 * completedShots / totalShots) : 0;
        _clipsContainer.Children.Add(new Label { Text = $"\U0001F3AC {project.Name} — {completedShots}/{totalShots} clips done ({percent}%)", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });

        foreach (var line in lines)
        {
            var shots = _storyService.GetShots(line);
            if (shots.Count == 0) continue;
            var lineFrame = new Frame { Padding = 12, CornerRadius = 8, BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), HasShadow = false };
            var lineStack = new VerticalStackLayout { Spacing = 6 };
            string preview = string.IsNullOrWhiteSpace(line.LineText) ? "[VISUAL]" : line.LineText.Length > 60 ? line.LineText[..60] + "..." : line.LineText;
            int lineDone = shots.Count(s => s.AllTasksDone);
            lineStack.Children.Add(new Label { Text = $"Line {line.LineOrder}: {preview}", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333"), LineBreakMode = LineBreakMode.WordWrap });
            lineStack.Children.Add(new Label { Text = $"{lineDone}/{shots.Count} clips complete", FontSize = 11, TextColor = lineDone == shots.Count ? Color.FromArgb("#2E7D32") : Color.FromArgb("#F57C00") });

            foreach (var shot in shots)
            {
                var row = new HorizontalStackLayout { Spacing = 8 };
                row.Children.Add(new Label { Text = shot.AllTasksDone ? "\u2705" : shot.Task1_ImageGenerated ? "\U0001F7E1" : "\u26AA", FontSize = 12, VerticalOptions = LayoutOptions.Center });
                string description = !string.IsNullOrWhiteSpace(shot.Description) ? (shot.Description.Length > 50 ? shot.Description[..50] + "..." : shot.Description) : $"Clip {shot.Index}";
                var shotLabel = new Label { Text = description, FontSize = 12, TextColor = shot.AllTasksDone ? Color.FromArgb("#999") : Color.FromArgb("#333"), TextDecorations = shot.AllTasksDone ? TextDecorations.Strikethrough : TextDecorations.None, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.TailTruncation };
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
                shotLabel.GestureRecognizers.Add(tap);
                row.Children.Add(shotLabel);
                string tasks = (!shot.Task1_ImageGenerated ? "IMG " : "") + (!shot.Task2_VideoGenerated ? "VID" : "");
                if (!string.IsNullOrWhiteSpace(tasks)) row.Children.Add(new Label { Text = tasks.Trim(), FontSize = 9, TextColor = Color.FromArgb("#F57C00"), FontAttributes = FontAttributes.Bold, VerticalOptions = LayoutOptions.Center });
                lineStack.Children.Add(row);
            }

            lineFrame.Content = lineStack;
            _clipsContainer.Children.Add(lineFrame);
        }
    }
}

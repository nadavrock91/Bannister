using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class MotivationSourcesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MotivationService _service;
    private readonly VerticalStackLayout _content = new() { Spacing = 10 };
    private Entry _titleEntry = null!;
    private Editor _descriptionEditor = null!;
    private bool _showArchived;

    public MotivationSourcesPage(AuthService auth, MotivationService service)
    {
        _auth = auth;
        _service = service;
        Title = "Motivation Sources";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 16, 16, 32),
                Spacing = 12,
                Children = { _content }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await _service.InitAsync();
        _content.Children.Clear();
        _content.Children.Add(new Label
        {
            Text = "Motivation Sources",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        var export = MakeButton("Export Strength Questionnaire", "#E3F2FD", "#1565C0");
        export.Clicked += async (_, _) => await ExportStrengthQuestionnaireAsync();
        _content.Children.Add(export);
        var import = MakeButton("Import Results", "#E8F5E9", "#2E7D32");
        import.Clicked += async (_, _) => await ImportStrengthResultsAsync();
        _content.Children.Add(import);
        var toggle = new Button
        {
            Text = _showArchived ? "Hide Archived" : "Show Archived",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 6,
            HeightRequest = 38
        };
        toggle.Clicked += async (_, _) => { _showArchived = !_showArchived; await RefreshAsync(); };
        _content.Children.Add(toggle);

        var sources = _showArchived
            ? await _service.GetAllSourcesAsync(_auth.CurrentUsername)
            : await _service.GetActiveSourcesAsync(_auth.CurrentUsername);
        var activeSources = _showArchived
            ? sources.Where(s => !s.IsArchived).ToList()
            : sources;
        if (activeSources.Any(s => s.StrengthScore.HasValue))
            _content.Children.Add(BuildFuelTanks(activeSources));
        foreach (var source in sources)
            _content.Children.Add(BuildSourceCard(source));
        _content.Children.Add(BuildAddForm());
    }

    private View BuildSourceCard(MotivationSource source)
    {
        var body = new VerticalStackLayout { Spacing = 6 };
        body.Children.Add(new Label
        {
            Text = source.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        if (source.StrengthScore.HasValue)
        {
            int score = source.StrengthScore.Value;
            string badgeColor = score <= 20 ? "#D32F2F"
                : score <= 40 ? "#EF6C00"
                : score <= 60 ? "#F9A825"
                : score <= 80 ? "#2E7D32" : "#7B1FA2";
            body.Children.Add(new Border
            {
                Content = new Label
                {
                    Text = $"Strength: {score}/100",
                    TextColor = Colors.White,
                    FontSize = 12,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                BackgroundColor = Color.FromArgb(badgeColor),
                StrokeThickness = 0,
                Padding = new Thickness(8, 4),
                HorizontalOptions = LayoutOptions.Start
            });
        }
        if (!string.IsNullOrWhiteSpace(source.Description))
            body.Children.Add(new Label
            {
                Text = source.Description,
                FontSize = 13,
                TextColor = Color.FromArgb("#777")
            });
        var actions = new HorizontalStackLayout { Spacing = 8 };
        var edit = MakeButton("Edit", "#E8EAF6", "#3949AB");
        edit.Clicked += async (_, _) =>
        {
            var title = await DisplayPromptAsync(
                "Edit Motivation Source",
                "Title:",
                "Save", "Cancel",
                initialValue: source.Title);
            if (string.IsNullOrWhiteSpace(title)) return;
            source.Title = title.Trim();
            await _service.SaveSourceAsync(source);
            await RefreshAsync();
        };
        actions.Children.Add(edit);
        if (source.IsArchived)
        {
            var restore = MakeButton("Restore", "#E8F5E9", "#2E7D32");
            restore.Clicked += async (_, _) =>
            {
                source.IsArchived = false;
                await _service.SaveSourceAsync(source);
                await RefreshAsync();
            };
            actions.Children.Add(restore);
        }
        else
        {
            var archive = MakeButton("Archive", "#FFF3E0", "#E65100");
            archive.Clicked += async (_, _) => { await _service.ArchiveSourceAsync(source.Id); await RefreshAsync(); };
            actions.Children.Add(archive);
        }
        var delete = MakeButton("Delete", "#FFEBEE", "#C62828");
        delete.Clicked += async (_, _) =>
        {
            if (await DisplayAlert("Delete Source", "Delete this motivation source?", "Delete", "Cancel"))
            {
                await _service.DeleteSourceAsync(source.Id);
                await RefreshAsync();
            }
        };
        actions.Children.Add(delete);
        body.Children.Add(actions);
        var card = new Border
        {
            Content = body,
            Stroke = Color.FromArgb("#DDDDDD"),
            StrokeThickness = 1,
            BackgroundColor = source.IsArchived ? Color.FromArgb("#F2F2F2") : Colors.White,
            Padding = 12
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
            await Navigation.PushAsync(
                new MotivationNotesPage(source, _service, _auth));
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private View BuildFuelTanks(List<MotivationSource> sources)
    {
        var bars = new HorizontalStackLayout { Spacing = 12 };
        foreach (var source in sources
            .Where(s => s.StrengthScore.HasValue)
            .OrderByDescending(s => s.StrengthScore))
        {
            int score = Math.Clamp(source.StrengthScore!.Value, 1, 100);
            double fillHeight = score / 100.0 * 180;
            var tankGrid = new Grid
            {
                HeightRequest = 180,
                WidthRequest = 48,
                BackgroundColor = Color.FromArgb("#ECEFF1")
            };
            tankGrid.Children.Add(new BoxView
            {
                HeightRequest = fillHeight,
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.Fill,
                Color = StrengthColor(score)
            });
            var tank = new Border
            {
                Content = tankGrid,
                WidthRequest = 48,
                HeightRequest = 180,
                Stroke = Color.FromArgb("#B0BEC5"),
                StrokeThickness = 1,
                BackgroundColor = Color.FromArgb("#ECEFF1")
            };
            var bar = new VerticalStackLayout
            {
                Spacing = 3,
                WidthRequest = 64,
                HorizontalOptions = LayoutOptions.Center
            };
            bar.Children.Add(tank);
            bar.Children.Add(new Label
            {
                Text = score.ToString(),
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = StrengthColor(score)
            });
            string shortTitle = source.Title.Length > 8
                ? source.Title[..8] + "…"
                : source.Title;
            bar.Children.Add(new Label
            {
                Text = shortTitle,
                FontSize = 10,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = Color.FromArgb("#555")
            });
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
                await DisplayAlert(
                    source.Title,
                    $"Strength: {score}/100",
                    "OK");
            bar.GestureRecognizers.Add(tap);
            bars.Children.Add(bar);
        }
        var section = new VerticalStackLayout { Spacing = 6 };
        section.Children.Add(new Label
        {
            Text = "Motivation Fuel",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        section.Children.Add(new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = bars,
            HeightRequest = 225
        });
        return section;
    }

    private static Color StrengthColor(int score) => score <= 20
        ? Color.FromArgb("#D32F2F")
        : score <= 40 ? Color.FromArgb("#EF6C00")
        : score <= 60 ? Color.FromArgb("#F9A825")
        : score <= 80 ? Color.FromArgb("#2E7D32")
        : Color.FromArgb("#7B1FA2");

    private View BuildAddForm()
    {
        var form = new VerticalStackLayout { Spacing = 8 };
        form.Children.Add(new Label { Text = "Add Source", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        _titleEntry = new Entry { Placeholder = "Title", BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222") };
        _descriptionEditor = new Editor { Placeholder = "Description (optional)", HeightRequest = 110, AutoSize = EditorAutoSizeOption.Disabled, BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222") };
        form.Children.Add(_titleEntry);
        form.Children.Add(_descriptionEditor);
        var save = MakeButton("Save", "#E8F5E9", "#2E7D32");
        save.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_titleEntry.Text)) return;
            await _service.SaveSourceAsync(new MotivationSource
            {
                Username = _auth.CurrentUsername,
                Title = _titleEntry.Text.Trim(),
                Description = _descriptionEditor.Text?.Trim() ?? "",
                CreatedDate = DateTime.Now
            });
            await RefreshAsync();
        };
        form.Children.Add(save);
        return new Border { Content = form, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = Color.FromArgb("#FAFAFA"), Padding = 12 };
    }

    private async Task ExportStrengthQuestionnaireAsync()
    {
        var sources = await _service.GetActiveSourcesAsync(_auth.CurrentUsername);
        var list = string.Join("\n", sources.Select((s, i) =>
            $"{i + 1}. {s.Title}" +
            (string.IsNullOrWhiteSpace(s.Description) ? "" : $" — {s.Description}")));
        var prompt =
            "I am going to describe my motivation sources and you will help me rate their strength from 1-100 by asking me questions based on a cold shower scenario. The cold shower is used as a universal calibration tool — it is uncomfortable, arbitrary, and has no inherent value, making it a clean signal of what truly drives behavior.\n\n" +
            "My motivation sources are:\n" + list + "\n\n" +
            "For each source, ask me the following scenario questions one at a time and remember my answers:\n" +
            "1. Would you take a cold shower right now to avoid losing 1000 EXP points?\n" +
            "2. Would you take a cold shower right now to avoid losing an entire level?\n" +
            "3. Would you add a cold shower as a daily activity and execute it every day if you gained 1000 EXP per session?\n" +
            "4. Would you reset a habit that reached escape velocity after 1 year to avoid a cold shower? After 2 years? After 5 years? After 10 years?\n" +
            "5. If you publicly promised online you would take a cold shower every day, would you start? How long do you think you would last?\n\n" +
            "Based on my answers, rate each motivation source from 1-100 where:\n" +
            "1-20 = weak, easily overridden by discomfort\n" +
            "21-40 = below average, present but inconsistent\n" +
            "41-60 = moderate, reliable under normal conditions\n" +
            "61-80 = strong, persists under significant discomfort\n" +
            "81-100 = critical, near-unbreakable drive\n\n" +
            "Return ONLY a C#-parsable result in exactly this format after all questions are answered:\n" +
            "sourceStrength[1] = {score};\n" +
            "sourceStrength[2] = {score};\n" +
            "(one line per source in the same order as listed above)";
        prompt =
            "I want you to help me rate the strength of my personal motivation sources on a scale of 1-100. These motivation sources are the things that fuel my behavior and keep me on track in a self-designed gamification system I built for myself called Bannister. In this system I have games representing life areas, activities within those games that award EXP points, levels, streaks, and other gamification mechanics. The motivation sources I am about to list are the underlying psychological drivers that cause me to engage with this system and execute in real life.\n\n" +
            "Your job is to rate each motivation source from 1-100 using a cold shower as a calibration tool. The cold shower is deliberately arbitrary and uncomfortable with no inherent value — it is used purely as a signal of what truly drives behavior. The rating scale is:\n" +
            "1-20 = weak, easily overridden by discomfort\n" +
            "21-40 = below average, present but inconsistent  \n" +
            "41-60 = moderate, reliable under normal conditions\n" +
            "61-80 = strong, persists under significant discomfort\n" +
            "81-100 = critical, near-unbreakable drive\n\n" +
            "Before forming your questionnaire, if any motivation source is unclear to you, ask me clarifying questions first. Take as many clarifying rounds as you need until you fully understand each source. Then design a tailored set of scenario questions for each source — do not use a fixed script. The scenarios should probe what the source actually means to this person and how far it would drive them under real discomfort and resistance.\n\n" +
            "My motivation sources are:\n" + list + "\n\n" +
            "Start by asking any clarifying questions you have about these sources. Once you fully understand them, proceed with your tailored questionnaire. After all questions are answered, return ONLY a C#-parsable result in exactly this format:\n" +
            "sourceStrength[1] = {score};\n" +
            "sourceStrength[2] = {score};\n" +
            "(one line per source in the same order as listed above)";
        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert("Prompt copied", "Prompt copied — paste into your LLM, answer its questions, then use Import Results to paste back the ratings.", "OK");
    }

    private async Task ImportStrengthResultsAsync()
    {
        var sources = await _service.GetActiveSourcesAsync(_auth.CurrentUsername);
        if (sources.Count == 0)
        {
            await DisplayAlert("No Sources", "Add at least one active motivation source first.", "OK");
            return;
        }
        var text = await ShowImportEditorAsync();
        if (string.IsNullOrWhiteSpace(text)) return;
        int updated = 0;
        foreach (System.Text.RegularExpressions.Match match in
            System.Text.RegularExpressions.Regex.Matches(
                text, @"sourceStrength\[(\d+)\]\s*=\s*(-?\d+)"))
        {
            if (!int.TryParse(match.Groups[1].Value, out int index) ||
                !int.TryParse(match.Groups[2].Value, out int score) ||
                index < 1 || index > sources.Count || score < 1 || score > 100)
                continue;
            await _service.UpdateStrengthAsync(sources[index - 1].Id, score);
            updated++;
        }
        await DisplayAlert("Import Complete", $"{updated} source(s) updated.", "OK");
        await RefreshAsync();
    }

    private async Task<string?> ShowImportEditorAsync()
    {
        var tcs = new TaskCompletionSource<string?>();
        var editor = new Editor
        {
            Placeholder = "Paste sourceStrength[N] = score; lines here...",
            HeightRequest = 260,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        var save = MakeButton("Import", "#E8F5E9", "#2E7D32");
        var page = new ContentPage
        {
            Title = "Import Strength Results",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Paste the LLM results:", FontSize = 16, TextColor = Color.FromArgb("#222") },
                    editor,
                    save
                }
            }
        };
        save.Clicked += async (_, _) =>
        {
            tcs.TrySetResult(editor.Text ?? "");
            await Navigation.PopModalAsync();
        };
        page.Disappearing += (_, _) => tcs.TrySetResult(null);
        await Navigation.PushModalAsync(page);
        return await tcs.Task;
    }

    private static Button MakeButton(string text, string background, string foreground) => new()
    {
        Text = text, BackgroundColor = Color.FromArgb(background), TextColor = Color.FromArgb(foreground), CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(10, 0), FontSize = 12
    };
}

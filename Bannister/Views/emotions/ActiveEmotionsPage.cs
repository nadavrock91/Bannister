using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ActiveEmotionsPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly EmotionService _emotions;
    private readonly IdeasService? _ideasService;
    private readonly IdeaLoggerService? _ideaLogger;
    private VerticalStackLayout _activeList;
    private VerticalStackLayout _archivedList;
    private Label _activeCountLabel;
    private Label _archivedCountLabel;
    private bool _showArchived = false;
    private static readonly string[] Categories = { "Joy", "Sadness", "Anger", "Fear", "Surprise", "Disgust", "Love", "Anxiety", "Gratitude", "Other" };

    public ActiveEmotionsPage(AuthService auth, EmotionService emotions, IdeasService? ideasService = null)
    {
        _auth = auth;
        _emotions = emotions;
        _ideasService = ideasService;
        _ideaLogger = Application.Current?.Handler?.MauiContext?.Services.GetService<IdeaLoggerService>();
        Title = "Active Emotions";

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _emotions.EnsureInitializedAsync();
        await LoadEmotionsAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 20, Spacing = 12 };

        mainStack.Children.Add((new Label
        {
            Text = "\U0001F525 Active Emotions",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1, classifyPageText: true));

        var addBtn = (new Button
        {
            Text = "+ Add Emotion",
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningDarkButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        addBtn.Clicked += async (_, _) => await AddEmotionAsync();
        mainStack.Children.Add(addBtn);

        _activeCountLabel = (new Label
        {
            Text = "Active (0)",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        mainStack.Children.Add(_activeCountLabel);

        _activeList = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_activeList);

        var archivedToggle = (new Button
        {
            Text = "Show Archived",
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText3);
        archivedToggle.Clicked += async (_, _) =>
        {
            _showArchived = !_showArchived;
            archivedToggle.Text = _showArchived ? "Hide Archived" : "Show Archived";
            await LoadEmotionsAsync();
        };
        mainStack.Children.Add(archivedToggle);

        _archivedCountLabel = (new Label
        {
            Text = "Archived (0)",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            IsVisible = false
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText1, classifyPageText: true);
        mainStack.Children.Add(_archivedCountLabel);

        _archivedList = new VerticalStackLayout { Spacing = 8, IsVisible = false };
        mainStack.Children.Add(_archivedList);

        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadEmotionsAsync()
    {
        var active = await _emotions.GetActiveEmotionsAsync(_auth.CurrentUsername);
        _activeCountLabel.Text = $"Active ({active.Count})";

        _activeList.Children.Clear();
        if (active.Count == 0)
        {
            _activeList.Children.Add((new Label
            {
                Text = "No active emotions tracked. Tap + Add Emotion to start.",
                FontSize = 13,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
        }

        foreach (var emotion in active)
            _activeList.Children.Add(BuildEmotionCard(emotion, false));

        if (_showArchived)
        {
            var archived = await _emotions.GetArchivedEmotionsAsync(_auth.CurrentUsername);
            _archivedCountLabel.Text = $"Archived ({archived.Count})";
            _archivedCountLabel.IsVisible = true;
            _archivedList.IsVisible = true;
            _archivedList.Children.Clear();

            if (archived.Count == 0)
            {
                _archivedList.Children.Add((new Label
                {
                    Text = "No archived emotions.",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Italic
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            }

            foreach (var emotion in archived)
                _archivedList.Children.Add(BuildEmotionCard(emotion, true));
        }
        else
        {
            _archivedCountLabel.IsVisible = false;
            _archivedList.IsVisible = false;
        }
    }

    private Frame BuildEmotionCard(Emotion emotion, bool isArchived)
    {
        string intensityBar = new string('\u2588', emotion.Intensity) + new string('\u2591', 10 - emotion.Intensity);

        ThemeKey cardBg = isArchived ? ThemeKey.SecondarySurface : GetCategoryColor(emotion.Category);

        var frame = (new Frame
        {
            Padding = 14,
            CornerRadius = 10,
            BorderColor = Colors.Transparent,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, cardBg);

        var stack = new VerticalStackLayout { Spacing = 4 };

        var headerRow = new HorizontalStackLayout { Spacing = 8 };
        headerRow.Children.Add((new Label
        {
            Text = $"{GetCategoryEmoji(emotion.Category)} {emotion.Name}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        headerRow.Children.Add((new Label
        {
            Text = emotion.Category,
            FontSize = 11,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        stack.Children.Add(headerRow);

        stack.Children.Add((new Label
        {
            Text = $"Intensity: {intensityBar} {emotion.Intensity}/10",
            FontSize = 11,
            FontFamily = "Consolas"
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

        if (!string.IsNullOrWhiteSpace(emotion.Description))
        {
            stack.Children.Add((new Label
            {
                Text = emotion.Description,
                FontSize = 12,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        }

        if (!string.IsNullOrWhiteSpace(emotion.Notes))
        {
            stack.Children.Add((new Label
            {
                Text = emotion.Notes,
                FontSize = 11,
                FontAttributes = FontAttributes.Italic,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        }

        string dateText = isArchived && emotion.ArchivedAt.HasValue
            ? $"Archived {emotion.ArchivedAt.Value.ToLocalTime():MMM dd}"
            : $"Added {emotion.CreatedAt.ToLocalTime():MMM dd}";
        stack.Children.Add((new Label
        {
            Text = dateText,
            FontSize = 10
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));

        var btnRow = new HorizontalStackLayout { Spacing = 6 };

        if (!isArchived)
        {
            var editBtn = (new Button
            {
                Text = "\u270F\uFE0F",
                CornerRadius = 6,
                WidthRequest = 36,
                HeightRequest = 32,
                Padding = 0,
                FontSize = 12
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
            var capturedEmotion = emotion;
            editBtn.Clicked += async (_, _) => await EditEmotionAsync(capturedEmotion);
            btnRow.Children.Add(editBtn);

            var archiveBtn = (new Button
            {
                Text = "\U0001F4E6",
                CornerRadius = 6,
                WidthRequest = 36,
                HeightRequest = 32,
                Padding = 0,
                FontSize = 12
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator).ThemeColor(Button.TextColorProperty, ThemeKey.CardText2);
            archiveBtn.Clicked += async (_, _) =>
            {
                await _emotions.ArchiveEmotionAsync(capturedEmotion.Id);
                await LoadEmotionsAsync();
            };
            btnRow.Children.Add(archiveBtn);
        }
        else
        {
            var restoreBtn = (new Button
            {
                Text = "\u267B\uFE0F",
                CornerRadius = 6,
                WidthRequest = 36,
                HeightRequest = 32,
                Padding = 0,
                FontSize = 12
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground).ThemeColor(Button.TextColorProperty, ThemeKey.SuccessText);
            var capturedEmotion = emotion;
            restoreBtn.Clicked += async (_, _) =>
            {
                await _emotions.RestoreEmotionAsync(capturedEmotion.Id);
                await LoadEmotionsAsync();
            };
            btnRow.Children.Add(restoreBtn);

            var deleteBtn = (new Button
            {
                Text = "\U0001F5D1\uFE0F",
                CornerRadius = 6,
                WidthRequest = 36,
                HeightRequest = 32,
                Padding = 0,
                FontSize = 12
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
            deleteBtn.Clicked += async (_, _) =>
            {
                bool confirm = await DisplayAlert("Delete?", $"Permanently delete '{capturedEmotion.Name}'?", "Delete", "Cancel");
                if (!confirm) return;
                await _emotions.DeleteEmotionAsync(capturedEmotion.Id);
                await LoadEmotionsAsync();
            };
            btnRow.Children.Add(deleteBtn);
        }

        stack.Children.Add(btnRow);
        frame.Content = stack;
        return frame;
    }

    private async Task AddEmotionAsync()
    {
        string? name = await DisplayPromptAsync("New Emotion", "What are you feeling?", "Next", "Cancel", maxLength: 100);
        if (string.IsNullOrWhiteSpace(name)) return;

        string? category = await DisplayActionSheet("Category", "Cancel", null, Categories);
        if (string.IsNullOrEmpty(category) || category == "Cancel") return;

        string? intensityStr = await DisplayPromptAsync("Intensity", "How intense? (1-10)", "Next", "Cancel", initialValue: "5", maxLength: 2, keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(intensityStr) || !int.TryParse(intensityStr, out int intensity)) return;
        intensity = Math.Clamp(intensity, 1, 10);

        string? description = await DisplayPromptAsync("Description (optional)", "What triggered this?", "Create", "Skip", maxLength: 500);

        await _emotions.CreateEmotionAsync(_auth.CurrentUsername, name.Trim(), category, intensity, description?.Trim() ?? "");
        string ideaText = name.Trim();
        if (_ideaLogger != null)
            await _ideaLogger.LogSilentlyAsync(new IdeaLogRequest
            {
                Username = _auth.CurrentUsername,
                Title = ideaText,
                Body = ideaText,
                Category = "active_emotions"
            });
        await LoadEmotionsAsync();
    }

    private async Task EditEmotionAsync(Emotion emotion)
    {
        string? choice = await DisplayActionSheet("Edit " + emotion.Name, "Cancel", null,
            "Change Name", "Change Category", "Change Intensity", "Edit Description", "Edit Notes");
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        switch (choice)
        {
            case "Change Name":
                string? n = await DisplayPromptAsync("Name", "Emotion name:", "Save", "Cancel", initialValue: emotion.Name, maxLength: 100);
                if (!string.IsNullOrWhiteSpace(n)) { emotion.Name = n.Trim(); await _emotions.UpdateEmotionAsync(emotion); }
                break;
            case "Change Category":
                string? c = await DisplayActionSheet("Category", "Cancel", null, Categories);
                if (!string.IsNullOrEmpty(c) && c != "Cancel") { emotion.Category = c; await _emotions.UpdateEmotionAsync(emotion); }
                break;
            case "Change Intensity":
                string? i = await DisplayPromptAsync("Intensity", "1-10:", "Save", "Cancel", initialValue: emotion.Intensity.ToString(), maxLength: 2, keyboard: Keyboard.Numeric);
                if (!string.IsNullOrWhiteSpace(i) && int.TryParse(i, out int ni)) { emotion.Intensity = Math.Clamp(ni, 1, 10); await _emotions.UpdateEmotionAsync(emotion); }
                break;
            case "Edit Description":
                string? d = await DisplayPromptAsync("Description", "Describe:", "Save", "Cancel", initialValue: emotion.Description, maxLength: 500);
                if (d != null) { emotion.Description = d.Trim(); await _emotions.UpdateEmotionAsync(emotion); }
                break;
            case "Edit Notes":
                string? no = await DisplayPromptAsync("Notes", "Notes:", "Save", "Cancel", initialValue: emotion.Notes, maxLength: 500);
                if (no != null) { emotion.Notes = no.Trim(); await _emotions.UpdateEmotionAsync(emotion); }
                break;
        }
        await LoadEmotionsAsync();
    }

    private static string GetCategoryEmoji(string category) => category switch
    {
        "Joy" => "\U0001F60A",
        "Sadness" => "\U0001F622",
        "Anger" => "\U0001F621",
        "Fear" => "\U0001F628",
        "Surprise" => "\U0001F632",
        "Disgust" => "\U0001F922",
        "Love" => "\u2764\uFE0F",
        "Anxiety" => "\U0001F630",
        "Gratitude" => "\U0001F64F",
        _ => "\U0001F4AD"
    };

    private static ThemeKey GetCategoryColor(string category) => category switch
    {
        "Joy" => ThemeKey.HighlightYellowSoft,        "Sadness" => ThemeKey.RowSelected,        "Anger" => ThemeKey.ErrorBackground,        "Fear" => ThemeKey.AccentPurpleBackground,        "Surprise" => ThemeKey.HighlightTeal,        "Disgust" => ThemeKey.HighlightLime,        "Love" => ThemeKey.HighlightPink,        "Anxiety" => ThemeKey.WarningBackground,        "Gratitude" => ThemeKey.SuccessBackground,        _ => ThemeKey.SecondarySurface    };
}

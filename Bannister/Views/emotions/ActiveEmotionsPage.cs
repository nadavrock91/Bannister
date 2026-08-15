using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ActiveEmotionsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly EmotionService _emotions;
    private readonly IdeasService? _ideasService;
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
        Title = "Active Emotions";
        BackgroundColor = Color.FromArgb("#FFF8E1");
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

        mainStack.Children.Add(new Label
        {
            Text = "\U0001F525 Active Emotions",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E65100")
        });

        var addBtn = new Button
        {
            Text = "+ Add Emotion",
            BackgroundColor = Color.FromArgb("#E65100"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        };
        addBtn.Clicked += async (_, _) => await AddEmotionAsync();
        mainStack.Children.Add(addBtn);

        _activeCountLabel = new Label
        {
            Text = "Active (0)",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };
        mainStack.Children.Add(_activeCountLabel);

        _activeList = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_activeList);

        var archivedToggle = new Button
        {
            Text = "Show Archived",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            TextColor = Color.FromArgb("#888"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };
        archivedToggle.Clicked += async (_, _) =>
        {
            _showArchived = !_showArchived;
            archivedToggle.Text = _showArchived ? "Hide Archived" : "Show Archived";
            await LoadEmotionsAsync();
        };
        mainStack.Children.Add(archivedToggle);

        _archivedCountLabel = new Label
        {
            Text = "Archived (0)",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#999"),
            IsVisible = false
        };
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
            _activeList.Children.Add(new Label
            {
                Text = "No active emotions tracked. Tap + Add Emotion to start.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
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
                _archivedList.Children.Add(new Label
                {
                    Text = "No archived emotions.",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#999"),
                    FontAttributes = FontAttributes.Italic
                });
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

        Color cardBg = isArchived ? Color.FromArgb("#F5F5F5") : GetCategoryColor(emotion.Category);

        var frame = new Frame
        {
            Padding = 14,
            CornerRadius = 10,
            BackgroundColor = cardBg,
            BorderColor = Colors.Transparent,
            HasShadow = false
        };

        var stack = new VerticalStackLayout { Spacing = 4 };

        var headerRow = new HorizontalStackLayout { Spacing = 8 };
        headerRow.Children.Add(new Label
        {
            Text = $"{GetCategoryEmoji(emotion.Category)} {emotion.Name}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        });
        headerRow.Children.Add(new Label
        {
            Text = emotion.Category,
            FontSize = 11,
            TextColor = Color.FromArgb("#888"),
            VerticalOptions = LayoutOptions.Center
        });
        stack.Children.Add(headerRow);

        stack.Children.Add(new Label
        {
            Text = $"Intensity: {intensityBar} {emotion.Intensity}/10",
            FontSize = 11,
            TextColor = Color.FromArgb("#666"),
            FontFamily = "Consolas"
        });

        if (!string.IsNullOrWhiteSpace(emotion.Description))
        {
            stack.Children.Add(new Label
            {
                Text = emotion.Description,
                FontSize = 12,
                TextColor = Color.FromArgb("#555"),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        if (!string.IsNullOrWhiteSpace(emotion.Notes))
        {
            stack.Children.Add(new Label
            {
                Text = emotion.Notes,
                FontSize = 11,
                TextColor = Color.FromArgb("#888"),
                FontAttributes = FontAttributes.Italic,
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        string dateText = isArchived && emotion.ArchivedAt.HasValue
            ? $"Archived {emotion.ArchivedAt.Value.ToLocalTime():MMM dd}"
            : $"Added {emotion.CreatedAt.ToLocalTime():MMM dd}";
        stack.Children.Add(new Label
        {
            Text = dateText,
            FontSize = 10,
            TextColor = Color.FromArgb("#AAA")
        });

        var btnRow = new HorizontalStackLayout { Spacing = 6 };

        if (!isArchived)
        {
            var editBtn = new Button
            {
                Text = "\u270F\uFE0F",
                BackgroundColor = Color.FromArgb("#FFF3E0"),
                TextColor = Color.FromArgb("#E65100"),
                CornerRadius = 6,
                WidthRequest = 36,
                HeightRequest = 32,
                Padding = 0,
                FontSize = 12
            };
            var capturedEmotion = emotion;
            editBtn.Clicked += async (_, _) => await EditEmotionAsync(capturedEmotion);
            btnRow.Children.Add(editBtn);

            var archiveBtn = new Button
            {
                Text = "\U0001F4E6",
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                TextColor = Color.FromArgb("#666"),
                CornerRadius = 6,
                WidthRequest = 36,
                HeightRequest = 32,
                Padding = 0,
                FontSize = 12
            };
            archiveBtn.Clicked += async (_, _) =>
            {
                await _emotions.ArchiveEmotionAsync(capturedEmotion.Id);
                await LoadEmotionsAsync();
            };
            btnRow.Children.Add(archiveBtn);
        }
        else
        {
            var restoreBtn = new Button
            {
                Text = "\u267B\uFE0F",
                BackgroundColor = Color.FromArgb("#E8F5E9"),
                TextColor = Color.FromArgb("#2E7D32"),
                CornerRadius = 6,
                WidthRequest = 36,
                HeightRequest = 32,
                Padding = 0,
                FontSize = 12
            };
            var capturedEmotion = emotion;
            restoreBtn.Clicked += async (_, _) =>
            {
                await _emotions.RestoreEmotionAsync(capturedEmotion.Id);
                await LoadEmotionsAsync();
            };
            btnRow.Children.Add(restoreBtn);

            var deleteBtn = new Button
            {
                Text = "\U0001F5D1\uFE0F",
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#C62828"),
                CornerRadius = 6,
                WidthRequest = 36,
                HeightRequest = 32,
                Padding = 0,
                FontSize = 12
            };
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
        if (_ideasService != null)
        {
            try { await _ideasService.CreateIdeaAsync(_auth.CurrentUsername, name.Trim(), "active_emotions"); }
            catch { }
        }
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

    private static Color GetCategoryColor(string category) => category switch
    {
        "Joy" => Color.FromArgb("#FFFDE7"),
        "Sadness" => Color.FromArgb("#E3F2FD"),
        "Anger" => Color.FromArgb("#FFEBEE"),
        "Fear" => Color.FromArgb("#F3E5F5"),
        "Surprise" => Color.FromArgb("#E0F7FA"),
        "Disgust" => Color.FromArgb("#F1F8E9"),
        "Love" => Color.FromArgb("#FCE4EC"),
        "Anxiety" => Color.FromArgb("#FFF3E0"),
        "Gratitude" => Color.FromArgb("#E8F5E9"),
        _ => Color.FromArgb("#FAFAFA")
    };
}

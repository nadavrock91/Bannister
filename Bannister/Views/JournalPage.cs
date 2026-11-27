using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class JournalPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly JournalService _journalService;
    private readonly IdeaLoggerService _ideaLogger;

    private VerticalStackLayout _entriesContainer = null!;
    private Editor _entryEditor = null!;
    private Button _saveBtn = null!;
    private Label _dateLabel = null!;
    private DateTime _selectedDate = DateTime.Today;
    private bool _isSaving = false;
    private bool _isAiMode = false;
    private Editor _aiEditor = null!;
    private VerticalStackLayout _soloSection = null!;
    private VerticalStackLayout _aiSection = null!;

    public JournalPage(
        AuthService auth,
        JournalService journalService,
        IdeaLoggerService ideaLogger)
    {
        _auth = auth;
        _journalService = journalService;
        _ideaLogger = ideaLogger;
        Title = "Journal";

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadEntriesAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 12
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(36)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(36))
            },
            ColumnSpacing = 8
        };

        var prevBtn = (new Button
        {
            Text = "◀", FontSize = 14, HeightRequest = 36,
            WidthRequest = 36, CornerRadius = 6, Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        prevBtn.Clicked += async (_, _) =>
        {
            _selectedDate = _selectedDate.AddDays(-1);
            UpdateDateLabel();
            await LoadEntriesAsync();
        };
        headerGrid.Add(prevBtn, 0, 0);

        _dateLabel = (new Label
        {
            FontSize = 18, FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true);
        headerGrid.Add(_dateLabel, 1, 0);

        var nextBtn = (new Button
        {
            Text = "▶", FontSize = 14, HeightRequest = 36,
            WidthRequest = 36, CornerRadius = 6, Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        nextBtn.Clicked += async (_, _) =>
        {
            if (_selectedDate.Date >= DateTime.Today) return;
            _selectedDate = _selectedDate.AddDays(1);
            UpdateDateLabel();
            await LoadEntriesAsync();
        };
        headerGrid.Add(nextBtn, 2, 0);

        stack.Children.Add(headerGrid);
        UpdateDateLabel();

        var modeRow = new HorizontalStackLayout { Spacing = 0 };
        var soloTab = (new Button
        {
            Text = "✏️ Solo", FontSize = 13, HeightRequest = 36,
            CornerRadius = 0, Padding = new Thickness(20, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        var aiTab = (new Button
        {
            Text = " AI Conversation", FontSize = 13,
            HeightRequest = 36, CornerRadius = 0,
            Padding = new Thickness(20, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        soloTab.Clicked += (_, _) =>
        {
            _isAiMode = false;
            soloTab.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground);
            soloTab.ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
            aiTab.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground);
            aiTab.ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
            _soloSection.IsVisible = true;
            _aiSection.IsVisible = false;
        };
        aiTab.Clicked += (_, _) =>
        {
            _isAiMode = true;
            aiTab.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.AccentPurpleDarkButtonBackground);
            aiTab.ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
            soloTab.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground);
            soloTab.ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
            _soloSection.IsVisible = false;
            _aiSection.IsVisible = true;
        };
        modeRow.Children.Add(soloTab);
        modeRow.Children.Add(aiTab);
        stack.Children.Add(modeRow);

        var analyzeBtn = (new Button
        {
            Text = " Analyze",
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            HorizontalOptions = LayoutOptions.End,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightIndigo).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightIndigoButton);
        analyzeBtn.Clicked += async (_, _) =>
        {
            var services = Application.Current?.Handler?.MauiContext?.Services;
            var analysis = services?.GetService<JournalAnalysisService>();
            var provider = services?.GetService<IJournalAnalysisProvider>();
            var db = services?.GetService<DatabaseService>();
            if (analysis == null || provider == null || db == null) return;
            await Navigation.PushAsync(new JournalAnalysisPage(
                _auth, analysis, provider, _journalService, db));
        };
        stack.Children.Add(analyzeBtn);

        _soloSection = new VerticalStackLayout { Spacing = 8 };
        _entryEditor = (new Editor
        {
            Placeholder = "Write your thoughts...",
            HeightRequest = 120,
            AutoSize = EditorAutoSizeOption.TextChanges,
            FontSize = 14,
            Margin = new Thickness(0, 4)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText);
        _soloSection.Children.Add(_entryEditor);

        _saveBtn = (new Button
        {
            Text = " Save Entry",
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        _saveBtn.Clicked += async (_, _) => await SaveEntryAsync();
        _soloSection.Children.Add(_saveBtn);
        stack.Children.Add(_soloSection);

        _aiSection = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = false
        };
        _aiSection.Children.Add((new Label
        {
            Text = "Paste your full AI conversation below, " +
                   "then tap Save.",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        _aiEditor = (new Editor
        {
            Placeholder = "Paste AI conversation here...",
            HeightRequest = 180,
            AutoSize = EditorAutoSizeOption.TextChanges,
            FontSize = 13,
            Margin = new Thickness(0, 4)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText);
        _aiSection.Children.Add(_aiEditor);
        var aiSaveBtn = (new Button
        {
            Text = " Save Conversation",
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.AccentPurpleDarkButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        aiSaveBtn.Clicked += async (_, _) => await SaveAiEntryAsync();
        _aiSection.Children.Add(aiSaveBtn);
        stack.Children.Add(_aiSection);

        stack.Children.Add((new BoxView
        {
            HeightRequest = 1,
            Margin = new Thickness(0, 4)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator));
        stack.Children.Add((new Label
        {
            Text = "Entries", FontSize = 15,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        _entriesContainer = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(_entriesContainer);
        Content = new ScrollView { Content = stack };
    }

    private void UpdateDateLabel()
    {
        _dateLabel.Text = _selectedDate.Date == DateTime.Today
            ? "Today"
            : _selectedDate.ToString("dd MMM yyyy");
    }

    private async Task LoadEntriesAsync()
    {
        _entriesContainer.Children.Clear();
        var entries = await _journalService.GetEntriesAsync(
            _auth.CurrentUsername, _selectedDate);
        if (entries.Count == 0)
        {
            _entriesContainer.Children.Add((new Label
            {
                Text = "No entries for this day yet.", FontSize = 13,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }
        foreach (var entry in entries)
            _entriesContainer.Children.Add(BuildEntryCard(entry));
    }

    private View BuildEntryCard(JournalEntry entry)
    {
        var stack = (new VerticalStackLayout
        {
            Padding = new Thickness(12, 10), Spacing = 4
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
        var topRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(36))
            },
            ColumnSpacing = 6
        };
        topRow.Add((new Label
        {
            Text = entry.TimeDisplay, FontSize = 11,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true), 0, 0);
        var deleteBtn = (new Button
        {
            Text = "✕", CornerRadius = 4,
            FontSize = 11, HeightRequest = 28, WidthRequest = 28,
            Padding = 0
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        deleteBtn.Clicked += async (_, _) =>
        {
            bool confirm = await DisplayAlert("Delete Entry",
                "Delete this journal entry?", "Delete", "Cancel");
            if (!confirm) return;
            await _journalService.DeleteAsync(entry.Id);
            await LoadEntriesAsync();
        };
        topRow.Add(deleteBtn, 1, 0);
        stack.Children.Add(topRow);
        stack.Children.Add((new Label
        {
            Text = entry.Text, FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true));
        return (new Frame
        {
            Content = stack, Padding = 0, CornerRadius = 8,
            HasShadow = false, Margin = new Thickness(0, 1)
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
    }

    private async Task SaveEntryAsync()
    {
        if (_isSaving) return;
        var text = _entryEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        _isSaving = true;
        _saveBtn.IsEnabled = false;
        _saveBtn.Text = "Saving...";

        try
        {
            await _journalService.CreateAsync(
                _auth.CurrentUsername, text);

            string title = text.Length > 60
                ? text[..57] + "..."
                : text;
            await _ideaLogger.LogIdeaAsync(
                new IdeaLogRequest
                {
                    Username = _auth.CurrentUsername,
                    Title = title,
                    Body = text,
                    Category = "Journal",
                    Notes = $"Solo journal entry — {DateTime.Now:dd MMM yyyy HH:mm}"
                },
                IdeaLogMode.Silent);

            _entryEditor.Text = "";
            await LoadEntriesAsync();
            _saveBtn.Text = "✓ Saved!";
            await Task.Delay(1000);
        }
        finally
        {
            _saveBtn.Text = " Save Entry";
            _saveBtn.IsEnabled = true;
            _isSaving = false;
        }
    }

    private async Task SaveAiEntryAsync()
    {
        if (_isSaving) return;
        var text = _aiEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text)) return;

        _isSaving = true;
        try
        {
            await _journalService.CreateAsync(
                _auth.CurrentUsername, text);

            string title = text.Length > 60
                ? text[..57] + "..."
                : text;
            await _ideaLogger.LogIdeaAsync(
                new IdeaLogRequest
                {
                    Username = _auth.CurrentUsername,
                    Title = title,
                    Body = text,
                    Category = IdeasService.AiConversationCategory,
                    Notes = $"AI conversation — {DateTime.Now:dd MMM yyyy HH:mm}"
                },
                IdeaLogMode.Silent);

            _aiEditor.Text = "";
            await LoadEntriesAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }
}

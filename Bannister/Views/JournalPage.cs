using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class JournalPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly JournalService _journalService;
    private readonly IdeasService _ideasService;

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
        IdeasService ideasService)
    {
        _auth = auth;
        _journalService = journalService;
        _ideasService = ideasService;
        Title = "Journal";
        BackgroundColor = Color.FromArgb("#F5F5F5");
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

        var prevBtn = new Button
        {
            Text = "◀", FontSize = 14, HeightRequest = 36,
            WidthRequest = 36, CornerRadius = 6, Padding = 0,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0")
        };
        prevBtn.Clicked += async (_, _) =>
        {
            _selectedDate = _selectedDate.AddDays(-1);
            UpdateDateLabel();
            await LoadEntriesAsync();
        };
        headerGrid.Add(prevBtn, 0, 0);

        _dateLabel = new Label
        {
            FontSize = 18, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        headerGrid.Add(_dateLabel, 1, 0);

        var nextBtn = new Button
        {
            Text = "▶", FontSize = 14, HeightRequest = 36,
            WidthRequest = 36, CornerRadius = 6, Padding = 0,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0")
        };
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
        var soloTab = new Button
        {
            Text = "✏️ Solo", FontSize = 13, HeightRequest = 36,
            CornerRadius = 0, Padding = new Thickness(20, 0),
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White
        };
        var aiTab = new Button
        {
            Text = " AI Conversation", FontSize = 13,
            HeightRequest = 36, CornerRadius = 0,
            Padding = new Thickness(20, 0),
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F")
        };
        soloTab.Clicked += (_, _) =>
        {
            _isAiMode = false;
            soloTab.BackgroundColor = Color.FromArgb("#1565C0");
            soloTab.TextColor = Colors.White;
            aiTab.BackgroundColor = Color.FromArgb("#ECEFF1");
            aiTab.TextColor = Color.FromArgb("#37474F");
            _soloSection.IsVisible = true;
            _aiSection.IsVisible = false;
        };
        aiTab.Clicked += (_, _) =>
        {
            _isAiMode = true;
            aiTab.BackgroundColor = Color.FromArgb("#6A1B9A");
            aiTab.TextColor = Colors.White;
            soloTab.BackgroundColor = Color.FromArgb("#ECEFF1");
            soloTab.TextColor = Color.FromArgb("#37474F");
            _soloSection.IsVisible = false;
            _aiSection.IsVisible = true;
        };
        modeRow.Children.Add(soloTab);
        modeRow.Children.Add(aiTab);
        stack.Children.Add(modeRow);

        _soloSection = new VerticalStackLayout { Spacing = 8 };
        _entryEditor = new Editor
        {
            Placeholder = "Write your thoughts...",
            HeightRequest = 120,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            FontSize = 14,
            Margin = new Thickness(0, 4)
        };
        _soloSection.Children.Add(_entryEditor);

        _saveBtn = new Button
        {
            Text = " Save Entry",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };
        _saveBtn.Clicked += async (_, _) => await SaveEntryAsync();
        _soloSection.Children.Add(_saveBtn);
        stack.Children.Add(_soloSection);

        _aiSection = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = false
        };
        _aiSection.Children.Add(new Label
        {
            Text = "Paste your full AI conversation below, " +
                   "then tap Save.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        _aiEditor = new Editor
        {
            Placeholder = "Paste AI conversation here...",
            HeightRequest = 180,
            AutoSize = EditorAutoSizeOption.TextChanges,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            FontSize = 13,
            Margin = new Thickness(0, 4)
        };
        _aiSection.Children.Add(_aiEditor);
        var aiSaveBtn = new Button
        {
            Text = " Save Conversation",
            BackgroundColor = Color.FromArgb("#6A1B9A"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };
        aiSaveBtn.Clicked += async (_, _) => await SaveAiEntryAsync();
        _aiSection.Children.Add(aiSaveBtn);
        stack.Children.Add(_aiSection);

        stack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 4)
        });
        stack.Children.Add(new Label
        {
            Text = "Entries", FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#444")
        });

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
            _entriesContainer.Children.Add(new Label
            {
                Text = "No entries for this day yet.", FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }
        foreach (var entry in entries)
            _entriesContainer.Children.Add(BuildEntryCard(entry));
    }

    private View BuildEntryCard(JournalEntry entry)
    {
        var stack = new VerticalStackLayout
        {
            Padding = new Thickness(12, 10), Spacing = 4,
            BackgroundColor = Colors.White
        };
        var topRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(36))
            },
            ColumnSpacing = 6
        };
        topRow.Add(new Label
        {
            Text = entry.TimeDisplay, FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        var deleteBtn = new Button
        {
            Text = "✕", BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"), CornerRadius = 4,
            FontSize = 11, HeightRequest = 28, WidthRequest = 28,
            Padding = 0
        };
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
        stack.Children.Add(new Label
        {
            Text = entry.Text, FontSize = 14,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        return new Frame
        {
            Content = stack, Padding = 0, CornerRadius = 8,
            HasShadow = false, BorderColor = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White, Margin = new Thickness(0, 1)
        };
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
            await _ideasService.CreateIdeaAsync(
                _auth.CurrentUsername,
                title,
                category: "Journal",
                fullIdea: text,
                notes: $"Solo journal entry — " +
                       $"{DateTime.Now:dd MMM yyyy HH:mm}");

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
            await _ideasService.CreateIdeaAsync(
                _auth.CurrentUsername,
                title,
                category: "Journal - AI",
                fullIdea: text,
                notes: $"AI conversation — " +
                       $"{DateTime.Now:dd MMM yyyy HH:mm}");

            _aiEditor.Text = "";
            await LoadEntriesAsync();
        }
        finally
        {
            _isSaving = false;
        }
    }
}

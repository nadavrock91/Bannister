using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class CompanionsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly CompanionService _service;
    private readonly GameService _games;
    private readonly PrivacyModeService _privacyMode;
    private readonly VerticalStackLayout _content = new() { Padding = 16, Spacing = 10 };
    private Companion? _selected;
    private CompanionInterview? _selectedInterview;
    private bool _showArchived;
    private string? _pointGameFilter;
    private string? _pointCategoryFilter;
    private bool _pointGameFilterIsNoGame;
    private bool _pointCategoryFilterIsNoCategory;
    private ModeThemeColors _modeColors = ModeTheme.Get(ActivityDisplayMode.All);

    public CompanionsPage(AuthService auth, CompanionService service, GameService games, PrivacyModeService privacyMode)
    {
        _auth = auth; _service = service; _games = games; _privacyMode = privacyMode;
        Title = "Companions";
        BackgroundColor = _modeColors.Background;
        Content = new ScrollView { Content = _content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyModeTheme();
        await _service.EnsureMetaAsync(_auth.CurrentUsername);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _content.Children.Clear();
        if (_selected == null) await BuildListAsync(); else await BuildDetailAsync();
    }

    private void ApplyModeTheme()
    {
        _modeColors = ModeTheme.Get(_privacyMode.GetDisplayMode(_auth.CurrentUsername));
        BackgroundColor = _modeColors.Background;
    }

    private async Task BuildListAsync()
    {
        _content.Children.Add(PageHeading("Companions", 24));
        _content.Children.Add(new Label { Text = "LLM companions that learn durable context about you through interviews.", FontSize = 13, TextColor = _modeColors.MutedForeground });
        var displayMode = _privacyMode.GetDisplayMode(_auth.CurrentUsername);
        var add = Button("+ Add Companion", "#5B63EE", Colors.White);
        add.Clicked += async (_, _) =>
        {
            var games = GamesForDisplayMode(await GetAllGamesAsync(), displayMode);
            var item = await CompanionEditPage.ShowAsync(Navigation, null, games, _auth.CurrentUsername);
            if (item == null) return;
            item.SortOrder = (await _service.GetCompanionsAsync(_auth.CurrentUsername)).Count;
            await _service.SaveCompanionAsync(item); await RefreshAsync();
        };
        _content.Children.Add(add);
        var companions = await _service.GetCompanionsAsync(_auth.CurrentUsername);
        var games = await GetAllGamesAsync();
        var gamesById = games.ToDictionary(x => x.GameId, StringComparer.OrdinalIgnoreCase);
        foreach (var companion in companions)
        {
            var points = (await _service.GetPointsAsync(_auth.CurrentUsername, companion.Id))
                .Where(x => CompanionService.IsVisibleInDisplayMode(x, gamesById, displayMode))
                .ToList();
            string scope = ScopeLabel(companion, gamesById);
            var stack = new VerticalStackLayout { Spacing = 4 };
            stack.Children.Add(Heading(companion.Nickname, 17));
            stack.Children.Add(new Label { Text = $"{scope} · {points.Count} active data point{(points.Count == 1 ? "" : "s")}", FontSize = 12, TextColor = Color.FromArgb("#666") });
            var actions = new HorizontalStackLayout { Spacing = 6 };
            var edit = Button("Edit", "#FFF3E0", Color.FromArgb("#E65100"));
            edit.Clicked += async (_, _) =>
            {
                var editGames = GamesForDisplayMode(games, displayMode, companion.GameId);
                var edited = await CompanionEditPage.ShowAsync(Navigation, companion, editGames, _auth.CurrentUsername);
                if (edited == null) return; await _service.SaveCompanionAsync(edited); await RefreshAsync();
            };
            actions.Children.Add(edit);
            if (companion.Scope != "meta")
            {
                var delete = Button("Delete", "#FFEBEE", Color.FromArgb("#C62828"));
                delete.Clicked += async (_, _) =>
                {
                    if (!await DisplayAlert("Delete Companion", $"Delete {companion.Nickname} and all of its data points and interviews?", "Delete", "Cancel")) return;
                    await _service.DeleteCompanionAsync(companion); await RefreshAsync();
                };
                actions.Children.Add(delete);
            }
            stack.Children.Add(actions);
            var card = Card(stack);
            var openTap = new TapGestureRecognizer();
            openTap.Tapped += async (_, _) => await OpenCompanionAsync(companion);
            card.GestureRecognizers.Add(openTap);
            _content.Children.Add(card);
        }
    }

    private async Task OpenCompanionAsync(Companion companion)
    {
        _selected = companion; _selectedInterview = null;
        _pointGameFilter = null; _pointCategoryFilter = null;
        _pointGameFilterIsNoGame = false; _pointCategoryFilterIsNoCategory = false;
        await RefreshAsync();
    }

    private async Task BuildDetailAsync()
    {
        var companion = _selected!;
        var back = Button("← All Companions", "#ECEFF1", Color.FromArgb("#37474F"));
        back.Clicked += async (_, _) => { _selected = null; _selectedInterview = null; await RefreshAsync(); };
        _content.Children.Add(back);
        _content.Children.Add(PageHeading(companion.Nickname, 24));
        var allGames = await GetAllGamesAsync();
        var gamesById = allGames.ToDictionary(x => x.GameId, StringComparer.OrdinalIgnoreCase);
        var displayMode = _privacyMode.GetDisplayMode(_auth.CurrentUsername);
        var games = GamesForDisplayMode(allGames, displayMode);
        string scopeLabel = ScopeLabel(companion, gamesById);
        _content.Children.Add(new Label { Text = scopeLabel, FontSize = 13, TextColor = _modeColors.MutedForeground });

        var interviews = await _service.GetInterviewsAsync(_auth.CurrentUsername, companion.Id);
        if (_selectedInterview != null)
            _selectedInterview = interviews.FirstOrDefault(x => x.Id == _selectedInterview.Id);
        var interviewActions = new HorizontalStackLayout { Spacing = 8 };
        var start = Button("Start Interview", "#5B63EE", Colors.White);
        start.Clicked += async (_, _) => await StartInterviewAsync(companion, allGames, gamesById);
        interviewActions.Children.Add(start);
        var history = Button($"Interview History ({interviews.Count})", "#ECEFF1", Color.FromArgb("#37474F"));
        history.Clicked += async (_, _) => await Navigation.PushAsync(new CompanionInterviewHistoryPage(
            _auth.CurrentUsername,
            companion,
            _service,
            _privacyMode,
            interview => _selectedInterview = interview,
            deletedInterviewId =>
            {
                if (_selectedInterview?.Id == deletedInterviewId) _selectedInterview = null;
            }));
        interviewActions.Children.Add(history);
        if (_selectedInterview != null)
        {
            var resume = Button(CompanionInterviewPresentation.StepText(_selectedInterview), "#E8EAF6", Color.FromArgb("#3949AB"));
            resume.Clicked += async (_, _) => await ContinueInterviewAsync(companion, _selectedInterview, scopeLabel, allGames);
            interviewActions.Children.Add(resume);
            if (!string.IsNullOrWhiteSpace(_selectedInterview.Transcript))
            {
                var replace = Button("Paste Conversation Again", "#FFF3E0", Color.FromArgb("#E65100"));
                replace.Clicked += async (_, _) => await SaveConversationAsync(companion, _selectedInterview, scopeLabel, allGames);
                interviewActions.Children.Add(replace);
            }
        }
        _content.Children.Add(interviewActions);
        if (_selectedInterview != null)
            _content.Children.Add(new Label { Text = $"Current interview: {CompanionInterviewPresentation.StepText(_selectedInterview)}", FontAttributes = FontAttributes.Bold, TextColor = _modeColors.Accent });

        _content.Children.Add(PageHeading(_showArchived ? "Data Points (including archived)" : "Active Data Points", 18));
        var pointActions = new HorizontalStackLayout { Spacing = 8 };
        var addPoint = Button("+ Add Data Point", "#2E7D32", Colors.White);
        var allPoints = await _service.GetPointsAsync(_auth.CurrentUsername, companion.Id, _showArchived);
        var allCategories = await _service.GetPointCategoriesAsync(_auth.CurrentUsername);
        addPoint.Clicked += async (_, _) => await AddPointAsync(companion, scopeLabel, games, allCategories);
        var archived = Button(_showArchived ? "Hide Archived" : "Show Archived", "#ECEFF1", Color.FromArgb("#555"));
        archived.Clicked += async (_, _) => { _showArchived = !_showArchived; await RefreshAsync(); };
        pointActions.Children.Add(addPoint); pointActions.Children.Add(archived); _content.Children.Add(pointActions);
        var modeVisiblePoints = allPoints.Where(x => CompanionService.IsVisibleInDisplayMode(x, gamesById, displayMode)).ToList();
        if (modeVisiblePoints.Count != allPoints.Count)
            _content.Children.Add(new Label
            {
                Text = $"{modeVisiblePoints.Count} / {allPoints.Count} shown",
                FontSize = 13,
                TextColor = _modeColors.Accent,
                FontAttributes = FontAttributes.Bold
            });

        var gameOptions = new List<CompanionPointFilterOption>
        {
            new("All games", null, false),
            new("No game", null, true)
        };
        gameOptions.AddRange(games.Select(x => new CompanionPointFilterOption(x.DisplayName, x.GameId, false)));
        var gameFilter = new Picker
        {
            ItemsSource = gameOptions,
            ItemDisplayBinding = new Binding(nameof(CompanionPointFilterOption.Label)),
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        var selectedGameOption = gameOptions.FirstOrDefault(x => x.IsNone == _pointGameFilterIsNoGame &&
            string.Equals(x.Value, _pointGameFilter, StringComparison.OrdinalIgnoreCase));
        if (selectedGameOption == null)
        {
            _pointGameFilter = null; _pointGameFilterIsNoGame = false;
            selectedGameOption = gameOptions[0];
        }
        gameFilter.SelectedItem = selectedGameOption;
        gameFilter.SelectedIndexChanged += async (_, _) =>
        {
            if (gameFilter.SelectedItem is not CompanionPointFilterOption option) return;
            _pointGameFilter = option.Value; _pointGameFilterIsNoGame = option.IsNone;
            await RefreshAsync();
        };

        var visibleCategories = modeVisiblePoints.Select(x => x.Category).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        var categoryOptions = new List<CompanionPointFilterOption>
        {
            new("All categories", null, false),
            new("No category", null, true)
        };
        categoryOptions.AddRange(visibleCategories.Select(x => new CompanionPointFilterOption(x, x, false)));
        var categoryFilter = new Picker
        {
            ItemsSource = categoryOptions,
            ItemDisplayBinding = new Binding(nameof(CompanionPointFilterOption.Label)),
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        var selectedCategoryOption = categoryOptions.FirstOrDefault(x => x.IsNone == _pointCategoryFilterIsNoCategory &&
            string.Equals(x.Value, _pointCategoryFilter, StringComparison.OrdinalIgnoreCase));
        if (selectedCategoryOption == null)
        {
            _pointCategoryFilter = null; _pointCategoryFilterIsNoCategory = false;
            selectedCategoryOption = categoryOptions[0];
        }
        categoryFilter.SelectedItem = selectedCategoryOption;
        categoryFilter.SelectedIndexChanged += async (_, _) =>
        {
            if (categoryFilter.SelectedItem is not CompanionPointFilterOption option) return;
            _pointCategoryFilter = option.Value; _pointCategoryFilterIsNoCategory = option.IsNone;
            await RefreshAsync();
        };
        var gameFilterStack = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label { Text = "Game", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = _modeColors.Foreground },
                gameFilter
            }
        };
        var categoryFilterStack = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label { Text = "Category", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = _modeColors.Foreground },
                categoryFilter
            }
        };
        _content.Children.Add(new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) },
            ColumnSpacing = 8,
            Children = { gameFilterStack, categoryFilterStack }
        });
        Grid.SetColumn(categoryFilterStack, 1);

        var points = modeVisiblePoints.Where(x =>
                (_pointGameFilterIsNoGame ? string.IsNullOrWhiteSpace(x.GameId) :
                    _pointGameFilter == null || string.Equals(x.GameId, _pointGameFilter, StringComparison.OrdinalIgnoreCase)) &&
                (_pointCategoryFilterIsNoCategory ? string.IsNullOrWhiteSpace(x.Category) :
                    _pointCategoryFilter == null || string.Equals(x.Category, _pointCategoryFilter, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (points.Count == 0) _content.Children.Add(new Label { Text = "No data points yet.", TextColor = _modeColors.MutedForeground });
        foreach (var point in points)
        {
            var pointGames = GamesForDisplayMode(allGames, displayMode, point.GameId);
            _content.Children.Add(BuildPointCard(point, companion, scopeLabel, pointGames, gamesById, allCategories));
        }

    }

    private View BuildPointCard(CompanionDataPoint point, Companion companion, string scopeLabel,
        List<Game> games, IReadOnlyDictionary<string, Game> gamesById, List<string> categories)
    {
        var stack = new VerticalStackLayout { Spacing = 5 };
        stack.Children.Add(new Label { Text = point.Text, FontSize = 14, TextColor = point.Status == "archived" ? Color.FromArgb("#888") : Color.FromArgb("#222") });

        var categoryChoices = CompanionDataPointEditorOptions.BuildClassifications(games, categories, point.Category);
        var categoryPicker = new Picker
        {
            ItemsSource = categoryChoices,
            ItemDisplayBinding = new Binding(nameof(CompanionPointClassificationOption.Label)),
            FontSize = 12,
            TextColor = Color.FromArgb("#222222"),
            TitleColor = Color.FromArgb("#666666"),
            MinimumHeightRequest = 44,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 175
        };
        categoryPicker.SelectedItem = CompanionDataPointEditorOptions.CurrentClassification(
            categoryChoices, point.GameId, point.Category);
        TraceInlinePicker(categoryPicker, point.Id, "category");
        bool changingCategory = false;
        categoryPicker.SelectedIndexChanged += async (_, _) =>
        {
            if (changingCategory || categoryPicker.SelectedItem is not CompanionPointClassificationOption selected) return;
            var previousSelection = CompanionDataPointEditorOptions.CurrentClassification(
                categoryChoices, point.GameId, point.Category);
            string? previousGameId = point.GameId;
            string? previousCategory = point.Category;
            string? newGameId = null;
            string? newCategory = null;
            if (selected.Kind == CompanionPointClassificationKind.NewCategory)
            {
                string? entered = await DisplayPromptAsync(
                    "New Category", "Category name", "Save", "Cancel", initialValue: "");
                if (string.IsNullOrWhiteSpace(entered))
                {
                    changingCategory = true;
                    categoryPicker.SelectedItem = previousSelection;
                    changingCategory = false;
                    return;
                }
                newCategory = entered.Trim();
            }
            else if (selected.Kind == CompanionPointClassificationKind.Game)
            {
                newGameId = selected.Value;
            }
            else if (selected.Kind == CompanionPointClassificationKind.Category)
                newCategory = selected.Value;

            if (string.Equals(previousGameId, newGameId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(previousCategory?.Trim(), newCategory, StringComparison.OrdinalIgnoreCase))
            {
                changingCategory = true;
                categoryPicker.SelectedItem = previousSelection;
                changingCategory = false;
                return;
            }
            point.GameId = newGameId;
            point.Category = newCategory;
            await _service.SavePointAsync(point, companion, scopeLabel);

            var displayMode = _privacyMode.GetDisplayMode(_auth.CurrentUsername);
            bool remainsVisible = CompanionService.IsVisibleInDisplayMode(point, gamesById, displayMode);
            if (!remainsVisible && displayMode != ActivityDisplayMode.All)
                await DisplayAlert("Visibility Changed", $"Hidden in {DisplayModeName(displayMode)} mode.", "OK");
            await RefreshAsync();
        };

        var visibilityPicker = new Picker
        {
            ItemsSource = CompanionDataPointEditorOptions.Visibility,
            FontSize = 12,
            TextColor = Color.FromArgb("#222222"),
            TitleColor = Color.FromArgb("#666666"),
            MinimumHeightRequest = 44,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 165
        };
        visibilityPicker.SelectedItem = CompanionDataPointEditorOptions.Visibility[
            CompanionDataPointEditorOptions.VisibilityIndex(point.VisibilityOverride)];
        TraceInlinePicker(visibilityPicker, point.Id, "visibility");
        var autoResult = new Label
        {
            Text = AutoVisibilityText(point, gamesById),
            FontSize = 11,
            TextColor = Color.FromArgb("#555"),
            VerticalOptions = LayoutOptions.Center,
            IsVisible = !point.VisibilityOverride.HasValue
        };
        visibilityPicker.SelectedIndexChanged += async (_, _) =>
        {
            int? newOverride = CompanionDataPointEditorOptions.VisibilityOverride(visibilityPicker.SelectedIndex);
            if (point.VisibilityOverride == newOverride) return;
            point.VisibilityOverride = newOverride;
            await _service.SavePointAsync(point, companion, scopeLabel);

            var displayMode = _privacyMode.GetDisplayMode(_auth.CurrentUsername);
            bool remainsVisible = CompanionService.IsVisibleInDisplayMode(point, gamesById, displayMode);
            if (!remainsVisible && displayMode != ActivityDisplayMode.All)
                await DisplayAlert("Visibility Changed", $"Hidden in {DisplayModeName(displayMode)} mode.", "OK");
            await RefreshAsync();
        };
        stack.Children.Add(new FlexLayout
        {
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            AlignItems = Microsoft.Maui.Layouts.FlexAlignItems.Center,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start,
            Children = { categoryPicker, visibilityPicker, autoResult }
        });

        string gameName = !string.IsNullOrWhiteSpace(point.GameId) && gamesById.TryGetValue(point.GameId, out var game)
            ? game.DisplayName
            : string.IsNullOrWhiteSpace(point.GameId) ? "No game" : point.GameId;
        string category = string.IsNullOrWhiteSpace(point.Category) ? "No category" : point.Category;
        string visibility = VisibilityName(CompanionService.GetEffectiveVisibility(point, gamesById)) + (point.VisibilityOverride.HasValue ? " (manual)" : "");
        stack.Children.Add(new Label { Text = $"Game: {gameName} · Category: {category} · {visibility}", FontSize = 11, TextColor = Color.FromArgb("#555") });
        stack.Children.Add(new Label { Text = $"Priority {point.Priority} · {point.Source} · {point.Status} · {point.UpdatedAt.ToLocalTime():dd MMM yyyy}", FontSize = 11, TextColor = Color.FromArgb("#777") });
        var actions = new HorizontalStackLayout { Spacing = 5 };
        var edit = Button("Edit", "#FFF3E0", Color.FromArgb("#E65100"));
        edit.Clicked += async (_, _) =>
        {
            var edited = await CompanionDataPointEditPage.ShowAsync(Navigation, point, games, categories);
            if (edited == null) return;
            edited.ApplyTo(point);
            await _service.SavePointAsync(point, companion, scopeLabel);
            await RefreshAsync();
        };
        var archive = Button(point.Status == "active" ? "Archive" : "Restore", "#ECEFF1", Color.FromArgb("#555"));
        archive.Clicked += async (_, _) => { point.Status = point.Status == "active" ? "archived" : "active"; await _service.SavePointAsync(point); await RefreshAsync(); };
        var delete = Button("Delete", "#FFEBEE", Color.FromArgb("#C62828"));
        delete.Clicked += async (_, _) => { if (!await DisplayAlert("Delete Data Point", "Delete this data point?", "Delete", "Cancel")) return; await _service.DeletePointAsync(point); await RefreshAsync(); };
        actions.Children.Add(edit); actions.Children.Add(archive); actions.Children.Add(delete); stack.Children.Add(actions);
        return Card(stack);
    }

    private async Task AddPointAsync(Companion companion, string scopeLabel, List<Game> games, List<string> categories)
    {
        var edited = await CompanionDataPointEditPage.ShowAsync(Navigation, null, games, categories);
        if (edited == null) return;
        await _service.SaveManualPointAsync(_auth.CurrentUsername, companion, scopeLabel, edited.Text, edited.Priority,
            edited.GameId, edited.Category, edited.VisibilityOverride);
        await RefreshAsync();
    }

    private async Task StartInterviewAsync(Companion companion, List<Game> games, Dictionary<string, Game> gamesById)
    {
        var value = await DisplayPromptAsync("Start Interview", "Number of questions", "Copy Prompt", "Cancel", keyboard: Keyboard.Numeric, initialValue: companion.DefaultQuestionCount.ToString());
        if (value == null) return;
        if (!int.TryParse(value, out int questionCount) || questionCount is < 1 or > 50)
        {
            await DisplayAlert("Invalid Number of Questions", "Enter a whole number from 1 to 50.", "OK");
            return;
        }
        var interview = new CompanionInterview { Username = _auth.CurrentUsername, CompanionId = companion.Id, QuestionCount = questionCount, StartedAt = DateTime.UtcNow };
        await _service.SaveInterviewAsync(interview);
        var prompt = await _service.BuildInterviewPromptAsync(
            _auth.CurrentUsername, companion, questionCount, ScopeLabel(companion, gamesById), games,
            _privacyMode.GetDisplayMode(_auth.CurrentUsername));
        await Clipboard.SetTextAsync(prompt.Prompt);
        _selectedInterview = interview;
        await DisplayAlert("Interview Prompt Copied",
            "Paste it into your LLM and conduct the interview. Return here to paste the conversation." + OmittedPointsNote(prompt.OmittedPointCount), "OK");
        await RefreshAsync();
    }

    private async Task ContinueInterviewAsync(Companion companion, CompanionInterview interview, string scopeLabel, List<Game> games)
    {
        if (string.IsNullOrWhiteSpace(interview.Transcript))
        {
            await SaveConversationAsync(companion, interview, scopeLabel, games);
            return;
        }
        try
        {
            var response = await CompanionTextInputPage.ShowAsync(Navigation, "Paste Extraction Result", "", "Paste the structured extraction result...");
            if (string.IsNullOrWhiteSpace(response)) return;
            var extraction = CompanionService.ParseExtraction(response);
            CompanionService.ResolveExtractionGames(extraction, games);
            int recognizedCount = extraction.Adds.Count + extraction.Priorities.Count + extraction.Archives.Count + extraction.Updates.Count;
            if (recognizedCount == 0)
            {
                string details = string.Join("\n", extraction.SkippedLines.Concat(extraction.Warnings));
                string message = "Nothing recognized in the pasted result";
                if (!string.IsNullOrWhiteSpace(details)) message += $"\n\n{details}";
                await DisplayAlert("Nothing Recognized", message, "OK");
                return;
            }

            var applied = await CompanionExtractionReviewPage.ShowAsync(Navigation, extraction, async (adds, priorities, archives, updates) =>
            {
                var result = await _service.ApplyExtractionAsync(_auth.CurrentUsername, companion, interview, scopeLabel, adds, priorities, archives, updates);
                await TrySaveInterviewIdeaAsync(companion, interview, scopeLabel);
                return result;
            });
            if (applied == null) return;

            string loggingNote = applied.IdeaLoggingFailures > 0
                ? $" ({applied.IdeaLoggingFailures} not logged to Ideas)"
                : "";
            await DisplayAlert(
                "Extraction Applied",
                $"Added {applied.Added}{loggingNote}, reprioritized {applied.Reprioritized}, archived {applied.Archived}, updated {applied.Updated}",
                "OK");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Extraction import failed: {ex}");
            await DisplayAlert("Extraction Import Failed", ex.Message, "OK");
        }
    }

    private async Task SaveConversationAsync(Companion companion, CompanionInterview interview, string scopeLabel, List<Game> games)
    {
        var transcript = await CompanionTextInputPage.ShowAsync(Navigation, "Paste Conversation", interview.Transcript, "Paste the complete interview transcript...");
        if (string.IsNullOrWhiteSpace(transcript)) return;
        interview.Transcript = transcript.Trim();
        await _service.SaveInterviewAsync(interview);
        await TrySaveInterviewIdeaAsync(companion, interview, scopeLabel);
        var allPoints = await _service.GetPointsAsync(_auth.CurrentUsername, companion.Id);
        var gamesById = games.ToDictionary(x => x.GameId, StringComparer.OrdinalIgnoreCase);
        var displayMode = _privacyMode.GetDisplayMode(_auth.CurrentUsername);
        var points = allPoints.Where(x => CompanionService.IsVisibleInDisplayMode(x, gamesById, displayMode)).ToList();
        await Clipboard.SetTextAsync(CompanionService.BuildExtractionPrompt(companion, interview, points, games));
        await DisplayAlert("Extraction Prompt Copied",
            "Run the copied prompt in your LLM, then return and paste the extraction result." + OmittedPointsNote(allPoints.Count - points.Count), "OK");
        await RefreshAsync();
    }

    private async Task TrySaveInterviewIdeaAsync(Companion companion, CompanionInterview interview, string scopeLabel)
    {
        try
        {
            await _service.SaveInterviewIdeaAsync(_auth.CurrentUsername, companion, interview, scopeLabel);
        }
        catch
        {
            await DisplayAlert("Idea Logging Warning", "The interview was saved, but it could not be logged on the Ideas page.", "OK");
        }
    }

    private static string ScopeLabel(Companion item, IReadOnlyDictionary<string, Game> games) => item.Scope switch
    {
        "meta" => "Meta · all of Bannister",
        "game" => item.GameId != null && games.TryGetValue(item.GameId, out var game) ? $"Game · {game.DisplayName}" : $"Game · {item.GameId ?? "not selected"}",
        _ => $"Custom · {(string.IsNullOrWhiteSpace(item.Topic) ? "No topic" : item.Topic)}"
    };

    private static string VisibilityName(int visibility) => visibility switch
    {
        0 => "Private",
        1 => "Public",
        _ => "Both"
    };

    private static string AutoVisibilityText(
        CompanionDataPoint point,
        IReadOnlyDictionary<string, Game> gamesById)
    {
        string source = !string.IsNullOrWhiteSpace(point.GameId) && gamesById.TryGetValue(point.GameId, out var game)
            ? $"game: {game.DisplayName}"
            : string.IsNullOrWhiteSpace(point.GameId) ? "no game" : $"game: {point.GameId}";
        return $"Auto \u2192 {VisibilityName(CompanionService.GetEffectiveVisibility(point, gamesById))} ({source})";
    }

    private static void TraceInlinePicker(Picker picker, int pointId, string pickerName)
    {
        void WriteState(string stage)
        {
            var items = picker.ItemsSource?.Cast<object>().ToList() ?? new List<object>();
            object? selected = picker.SelectedItem;
            bool referenceMatch = selected != null && items.Any(x => ReferenceEquals(x, selected));
            bool valueMatch = selected != null && items.Any(x => Equals(x, selected));
            string display = selected switch
            {
                CompanionPointClassificationOption option => option.Label,
                null => "<null>",
                _ => selected.ToString() ?? "<empty>"
            };
#if WINDOWS
            string native = picker.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ComboBox comboBox
                ? $", nativeIndex={comboBox.SelectedIndex}, selectionBox={comboBox.SelectionBoxItem ?? "<null>"}, nativeSize={comboBox.ActualWidth:0.#}x{comboBox.ActualHeight:0.#}"
                : ", native=<not attached>";
#else
            string native = "";
#endif
            System.Diagnostics.Debug.WriteLine(
                $"[COMPANIONS PICKER] point={pointId}, picker={pickerName}, stage={stage}, index={picker.SelectedIndex}, itemCount={items.Count}, referenceMatch={referenceMatch}, valueMatch={valueMatch}, display='{display}', size={picker.Width:0.#}x{picker.Height:0.#}{native}");
        }

        WriteState("built");
        picker.Loaded += (_, _) => WriteState("loaded-after-refresh");
    }

    private static string DisplayModeName(ActivityDisplayMode displayMode) => displayMode switch
    {
        ActivityDisplayMode.PublicOnly => "Public",
        ActivityDisplayMode.PrivateOnly => "Private",
        _ => "All"
    };

    private static string OmittedPointsNote(int omitted) => omitted > 0
        ? $"\n\n{omitted} data point{(omitted == 1 ? " was" : "s were")} left out because of the current privacy mode."
        : "\n\nNo data points were left out by the current privacy mode.";

    private async Task<List<Game>> GetAllGamesAsync() =>
        (await _games.GetGamesAsync(_auth.CurrentUsername))
        .Concat(await _games.GetInactiveGamesAsync(_auth.CurrentUsername))
        .GroupBy(x => x.GameId, StringComparer.OrdinalIgnoreCase)
        .Select(x => x.First())
        .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private static List<Game> GamesForDisplayMode(
        IEnumerable<Game> games,
        ActivityDisplayMode displayMode,
        string? currentGameId = null) => games
        .Where(game =>
            (!string.IsNullOrWhiteSpace(currentGameId) &&
             string.Equals(game.GameId, currentGameId, StringComparison.OrdinalIgnoreCase)) ||
            displayMode switch
            {
                ActivityDisplayMode.PublicOnly => game.GameVisibility is 1 or 2,
                ActivityDisplayMode.PrivateOnly => game.GameVisibility is 0 or 2,
                _ => true
            })
        .OrderBy(game => game.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();

    private Label PageHeading(string text, double size) => new() { Text = text, FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = _modeColors.Foreground };
    private static Label Heading(string text, double size) => new() { Text = text, FontSize = size, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") };
    private static Button Button(string text, string background, Color foreground) => new() { Text = text, BackgroundColor = Color.FromArgb(background), TextColor = foreground, CornerRadius = 7, FontSize = 12, HeightRequest = 38, Padding = new Thickness(10, 0) };
    private static Frame Card(View content) => new() { Content = content, Padding = 12, CornerRadius = 9, HasShadow = false, BorderColor = Color.FromArgb("#DDDDDD"), BackgroundColor = Colors.White };
}

internal static class CompanionDataPointEditorOptions
{
    public const string NoCategory = "No category";
    public const string NewCategory = "New category\u2026";
    public static readonly string[] Visibility = { "Auto (follow game)", "Private", "Public", "Both" };

    public static List<CompanionPointClassificationOption> BuildClassifications(
        IEnumerable<Game> games,
        IEnumerable<string> categories,
        string? currentCategory = null)
    {
        var options = new List<CompanionPointClassificationOption>
        {
            new(NoCategory, CompanionPointClassificationKind.None, null)
        };
        options.AddRange(games
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new CompanionPointClassificationOption(
                $"Game: {x.DisplayName}{(x.IsActive ? "" : " (inactive)")}",
                CompanionPointClassificationKind.Game,
                x.GameId)));
        options.AddRange(categories
            .Append(currentCategory ?? "")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select(x => new CompanionPointClassificationOption(
                x, CompanionPointClassificationKind.Category, x)));
        options.Add(new(NewCategory, CompanionPointClassificationKind.NewCategory, null));
        return options;
    }

    public static CompanionPointClassificationOption CurrentClassification(
        IReadOnlyList<CompanionPointClassificationOption> options,
        string? gameId,
        string? category)
    {
        if (!string.IsNullOrWhiteSpace(gameId))
        {
            var game = options.FirstOrDefault(x => x.Kind == CompanionPointClassificationKind.Game &&
                string.Equals(x.Value, gameId, StringComparison.OrdinalIgnoreCase));
            if (game != null) return game;
        }
        if (!string.IsNullOrWhiteSpace(category))
        {
            var textCategory = options.FirstOrDefault(x => x.Kind == CompanionPointClassificationKind.Category &&
                string.Equals(x.Value, category, StringComparison.OrdinalIgnoreCase));
            if (textCategory != null) return textCategory;
        }
        return options.First(x => x.Kind == CompanionPointClassificationKind.None);
    }

    public static int VisibilityIndex(int? visibilityOverride) => visibilityOverride switch
    {
        0 => 1,
        1 => 2,
        2 => 3,
        _ => 0
    };

    public static int? VisibilityOverride(int selectedIndex) => selectedIndex switch
    {
        1 => 0,
        2 => 1,
        3 => 2,
        _ => null
    };
}

internal enum CompanionPointClassificationKind { None, Game, Category, NewCategory }
internal sealed record CompanionPointClassificationOption(
    string Label,
    CompanionPointClassificationKind Kind,
    string? Value)
{
    public override string ToString() => Label;
}
internal sealed record CompanionPointFilterOption(string Label, string? Value, bool IsNone);

internal sealed record CompanionDataPointEditResult(
    string Text,
    int Priority,
    string? GameId,
    string? Category,
    int? VisibilityOverride)
{
    public void ApplyTo(CompanionDataPoint point)
    {
        point.Text = Text;
        point.Priority = Priority;
        point.GameId = GameId;
        point.Category = string.IsNullOrWhiteSpace(Category) ? null : Category.Trim();
        point.VisibilityOverride = VisibilityOverride;
    }
}

internal sealed class CompanionDataPointEditPage : ContentPage
{
    private readonly TaskCompletionSource<CompanionDataPointEditResult?> _completion = new();

    private CompanionDataPointEditPage(CompanionDataPoint? point, List<Game> games, List<string> categories)
    {
        Title = point == null ? "Add Data Point" : "Edit Data Point";
        var text = new Editor
        {
            Placeholder = "Write a durable fact or observation...",
            Text = point?.Text ?? "",
            HeightRequest = 150,
            AutoSize = EditorAutoSizeOption.TextChanges
        };
        var priority = new Entry
        {
            Placeholder = "Priority (1-5)",
            Text = (point?.Priority ?? 3).ToString(),
            Keyboard = Keyboard.Numeric
        };
        var classificationOptions = CompanionDataPointEditorOptions.BuildClassifications(
            games, categories, point?.Category);
        var classification = new Picker
        {
            Title = "Game or category",
            ItemsSource = classificationOptions,
            ItemDisplayBinding = new Binding(nameof(CompanionPointClassificationOption.Label))
        };
        var lastClassification = CompanionDataPointEditorOptions.CurrentClassification(
            classificationOptions, point?.GameId, point?.Category);
        classification.SelectedItem = lastClassification;
        bool changingClassification = false;
        classification.SelectedIndexChanged += async (_, _) =>
        {
            if (changingClassification ||
                classification.SelectedItem is not CompanionPointClassificationOption selected) return;
            if (selected.Kind != CompanionPointClassificationKind.NewCategory)
            {
                lastClassification = selected;
                return;
            }
            string? entered = await DisplayPromptAsync(
                "New Category", "Category name", "Save", "Cancel", initialValue: "");
            changingClassification = true;
            if (string.IsNullOrWhiteSpace(entered))
            {
                classification.SelectedItem = lastClassification;
            }
            else
            {
                string newCategory = entered.Trim();
                categories = categories.Append(newCategory)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                classificationOptions = CompanionDataPointEditorOptions.BuildClassifications(
                    games, categories, newCategory);
                classification.ItemsSource = classificationOptions;
                lastClassification = classificationOptions.First(x =>
                    x.Kind == CompanionPointClassificationKind.Category &&
                    string.Equals(x.Value, newCategory, StringComparison.OrdinalIgnoreCase));
                classification.SelectedItem = lastClassification;
            }
            changingClassification = false;
        };

        var visibility = new Picker { Title = "Visibility", ItemsSource = CompanionDataPointEditorOptions.Visibility };
        visibility.SelectedItem = CompanionDataPointEditorOptions.Visibility[
            CompanionDataPointEditorOptions.VisibilityIndex(point?.VisibilityOverride)];

        var save = new Button { Text = "Save", BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White };
        save.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(text.Text))
            {
                await DisplayAlert("Text Required", "Enter a data point.", "OK");
                return;
            }
            if (!int.TryParse(priority.Text, out int priorityValue) || priorityValue is < 1 or > 5)
            {
                await DisplayAlert("Invalid Priority", "Enter a whole number from 1 to 5.", "OK");
                return;
            }
            int? visibilityOverride = CompanionDataPointEditorOptions.VisibilityOverride(visibility.SelectedIndex);
            var selectedClassification = classification.SelectedItem as CompanionPointClassificationOption;
            string? gameId = selectedClassification?.Kind == CompanionPointClassificationKind.Game
                ? selectedClassification.Value
                : null;
            string? category = selectedClassification?.Kind == CompanionPointClassificationKind.Category
                ? selectedClassification.Value
                : null;
            await CloseAsync(new CompanionDataPointEditResult(
                text.Text.Trim(), priorityValue, gameId, category, visibilityOverride));
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += async (_, _) => await CloseAsync(null);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 9,
                Children =
                {
                    new Label { Text = Title, FontSize = 22, FontAttributes = FontAttributes.Bold },
                    text, priority, classification, visibility,
                    new HorizontalStackLayout { Spacing = 8, Children = { save, cancel } }
                }
            }
        };
    }

    private async Task CloseAsync(CompanionDataPointEditResult? result)
    {
        await Navigation.PopModalAsync();
        _completion.TrySetResult(result);
    }

    public static async Task<CompanionDataPointEditResult?> ShowAsync(
        INavigation navigation, CompanionDataPoint? point, List<Game> games, List<string> categories)
    {
        var page = new CompanionDataPointEditPage(point, games, categories);
        await navigation.PushModalAsync(page);
        return await page._completion.Task;
    }
}

internal sealed class CompanionEditPage : ContentPage
{
    private readonly TaskCompletionSource<Companion?> _completion = new();
    private CompanionEditPage(Companion? source, List<Game> games, string username)
    {
        Title = source == null ? "Add Companion" : "Edit Companion";
        var item = source == null ? new Companion { Username = username } : source;
        var scope = new Picker { Title = "Scope", ItemsSource = new[] { "game", "custom" }, SelectedItem = item.Scope == "game" ? "game" : "custom", IsEnabled = item.Scope != "meta" };
        var game = new Picker { Title = "Game", ItemsSource = games, ItemDisplayBinding = new Binding(nameof(Game.DisplayName)), SelectedItem = games.FirstOrDefault(x => x.GameId == item.GameId) };
        var topic = new Entry { Placeholder = "Custom topic", Text = item.Topic };
        var nickname = new Entry { Placeholder = "Nickname", Text = item.Nickname };
        var persona = new Editor { Placeholder = "Persona description", Text = item.PersonaDescription, HeightRequest = 90 };
        var stance = new Editor { Placeholder = "Interview stance", Text = item.InterviewStance, HeightRequest = 90 };
        var questionCount = new Entry { Placeholder = "Number of questions", Text = item.DefaultQuestionCount.ToString(), Keyboard = Keyboard.Numeric };
        void UpdateScope() { bool meta = item.Scope == "meta"; string selected = meta ? "meta" : scope.SelectedItem?.ToString() ?? "custom"; game.IsVisible = selected == "game"; topic.IsVisible = selected == "custom"; }
        scope.SelectedIndexChanged += (_, _) => UpdateScope(); UpdateScope();
        var save = new Button { Text = "Save", BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White };
        save.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nickname.Text)) { await DisplayAlert("Nickname Required", "Enter a nickname.", "OK"); return; }
            if (!int.TryParse(questionCount.Text, out int questions) || questions is < 1 or > 50) { await DisplayAlert("Invalid Number of Questions", "Enter a whole number from 1 to 50.", "OK"); return; }
            string selectedScope = item.Scope == "meta" ? "meta" : scope.SelectedItem?.ToString() ?? "custom";
            if (selectedScope == "game" && game.SelectedItem is not Game) { await DisplayAlert("Game Required", "Choose a game.", "OK"); return; }
            item.Nickname = nickname.Text.Trim(); item.Scope = selectedScope; item.GameId = selectedScope == "game" ? ((Game)game.SelectedItem).GameId : null; item.Topic = selectedScope == "custom" ? topic.Text?.Trim() ?? "" : "";
            item.PersonaDescription = persona.Text?.Trim() ?? ""; item.InterviewStance = string.IsNullOrWhiteSpace(stance.Text) ? DefaultStance(item, games) : stance.Text.Trim(); item.DefaultQuestionCount = questions;
            await CloseAsync(item);
        };
        var cancel = new Button { Text = "Cancel" }; cancel.Clicked += async (_, _) => await CloseAsync(null);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 18, Spacing = 9, Children = { new Label { Text = Title, FontSize = 22, FontAttributes = FontAttributes.Bold }, scope, game, topic, nickname, persona, stance, questionCount, new HorizontalStackLayout { Spacing = 8, Children = { save, cancel } } } } };
    }
    private static string DefaultStance(Companion item, List<Game> games) => item.Scope == "game" ? $"get to know the user in the context of {games.FirstOrDefault(x => x.GameId == item.GameId)?.DisplayName ?? "this game"}" : item.Scope == "custom" ? $"get to know the user in the context of {item.Topic}" : "get to know the user";
    private async Task CloseAsync(Companion? result) { await Navigation.PopModalAsync(); _completion.TrySetResult(result); }
    public static async Task<Companion?> ShowAsync(INavigation navigation, Companion? item, List<Game> games, string username) { var page = new CompanionEditPage(item, games, username); await navigation.PushModalAsync(page); return await page._completion.Task; }
}

internal sealed class CompanionTextInputPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _completion = new();
    private CompanionTextInputPage(string title, string initial, string placeholder)
    {
        Title = title;
        var editor = new Editor { Text = initial, Placeholder = placeholder, HeightRequest = 320, AutoSize = EditorAutoSizeOption.Disabled, BackgroundColor = Color.FromArgb("#FAFAFA") };
        var paste = new Button { Text = "Paste", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White };
        paste.Clicked += async (_, _) => { try { if (!Clipboard.HasText) return; var text = await Clipboard.GetTextAsync(); if (string.IsNullOrEmpty(text)) return; string current = editor.Text ?? ""; int start = Math.Clamp(editor.CursorPosition, 0, current.Length); int length = Math.Clamp(editor.SelectionLength, 0, current.Length - start); editor.Text = current.Remove(start, length).Insert(start, text); editor.CursorPosition = start + text.Length; editor.SelectionLength = 0; } finally { editor.Focus(); } };
        var ok = new Button { Text = "OK", BackgroundColor = Color.FromArgb("#5B63EE"), TextColor = Colors.White }; ok.Clicked += async (_, _) => await CloseAsync(editor.Text);
        var cancel = new Button { Text = "Cancel" }; cancel.Clicked += async (_, _) => await CloseAsync(null);
        Content = new Grid { Padding = 16, RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }, Children = { editor } };
        var buttons = new FlexLayout { Direction = Microsoft.Maui.Layouts.FlexDirection.Row, Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start, Children = { paste, ok, cancel } }; Grid.SetRow(buttons, 1); ((Grid)Content).Children.Add(buttons);
    }
    private async Task CloseAsync(string? value) { await Navigation.PopModalAsync(); _completion.TrySetResult(value); }
    public static async Task<string?> ShowAsync(INavigation navigation, string title, string initial, string placeholder) { var page = new CompanionTextInputPage(title, initial, placeholder); await navigation.PushModalAsync(page); return await page._completion.Task; }
}

internal sealed class CompanionExtractionReviewPage : ContentPage
{
    private readonly TaskCompletionSource<CompanionApplyResult?> _completion = new();
    private CompanionExtractionReviewPage(CompanionExtraction result, Func<List<CompanionAddProposal>, List<CompanionPriorityProposal>, List<CompanionArchiveProposal>, List<CompanionUpdateProposal>, Task<CompanionApplyResult>> apply)
    {
        Title = "Review Extraction";
        var stack = new VerticalStackLayout { Padding = 16, Spacing = 8 };
        stack.Children.Add(new Label { Text = "Review Proposed Changes", FontSize = 22, FontAttributes = FontAttributes.Bold });
        var adds = AddRows(stack, "Add points", result.Adds, x =>
            $"P{x.Priority}: {x.Text}\nGame: {(string.IsNullOrWhiteSpace(x.GameName) ? "No game" : x.GameName)} · Category: {(string.IsNullOrWhiteSpace(x.Category) ? "No category" : x.Category)}");
        var priorities = AddRows(stack, "Priority changes", result.Priorities, x => $"Point {x.PointId} → priority {x.Priority}");
        var archives = AddRows(stack, "Archives", result.Archives, x => $"Point {x.PointId}: {x.Reason}");
        var updates = AddRows(stack, "Text updates", result.Updates, x => $"Point {x.PointId}: {x.Text}");
        if (result.Warnings.Count > 0) { stack.Children.Add(new Label { Text = $"Warnings ({result.Warnings.Count})", FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#E65100") }); foreach (var warning in result.Warnings) stack.Children.Add(new Label { Text = warning, FontSize = 11, TextColor = Color.FromArgb("#777") }); }
        if (result.SkippedLines.Count > 0) { stack.Children.Add(new Label { Text = $"Skipped invalid lines ({result.SkippedLines.Count})", FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#E65100") }); foreach (var line in result.SkippedLines) stack.Children.Add(new Label { Text = line, FontSize = 11, TextColor = Color.FromArgb("#777") }); }
        var applyButton = new Button { Text = "Apply Selected", BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White };
        applyButton.Clicked += async (_, _) =>
        {
            try
            {
                applyButton.IsEnabled = false;
                var applied = await apply(Selected(result.Adds, adds), Selected(result.Priorities, priorities), Selected(result.Archives, archives), Selected(result.Updates, updates));
                await CloseAsync(applied);
            }
            catch (Exception ex)
            {
                await CloseWithErrorAsync(ex);
            }
        };
        var cancel = new Button { Text = "Cancel" }; cancel.Clicked += async (_, _) => await CloseAsync(null);
        stack.Children.Add(new HorizontalStackLayout { Spacing = 8, Children = { applyButton, cancel } }); Content = new ScrollView { Content = stack };
    }
    private static List<CheckBox> AddRows<T>(VerticalStackLayout stack, string title, List<T> values, Func<T, string> text) { var checks = new List<CheckBox>(); if (values.Count == 0) return checks; stack.Children.Add(new Label { Text = title, FontSize = 17, FontAttributes = FontAttributes.Bold }); foreach (var value in values) { var check = new CheckBox { IsChecked = true }; checks.Add(check); stack.Children.Add(new HorizontalStackLayout { Spacing = 6, Children = { check, new Label { Text = text(value), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap } } }); } return checks; }
    private static List<T> Selected<T>(List<T> values, List<CheckBox> checks) => values.Where((_, i) => checks[i].IsChecked).ToList();
    private async Task CloseAsync(CompanionApplyResult? result) { await Navigation.PopModalAsync(); _completion.TrySetResult(result); }
    private async Task CloseWithErrorAsync(Exception exception) { await Navigation.PopModalAsync(); _completion.TrySetException(exception); }
    public static async Task<CompanionApplyResult?> ShowAsync(INavigation navigation, CompanionExtraction result, Func<List<CompanionAddProposal>, List<CompanionPriorityProposal>, List<CompanionArchiveProposal>, List<CompanionUpdateProposal>, Task<CompanionApplyResult>> apply) { var page = new CompanionExtractionReviewPage(result, apply); await navigation.PushModalAsync(page); return await page._completion.Task; }
}

using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class CompanionsPage : Bannister.Views.ModeThemedPage
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
    private ExpBarView? _contextExpBar;

    public CompanionsPage(AuthService auth, CompanionService service, GameService games, PrivacyModeService privacyMode)
    {
        _auth = auth; _service = service; _games = games; _privacyMode = privacyMode;
        Title = "The Context Game";

        Content = new ScrollView { Content = _content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var migration = await _service.EnsureSinglePersonaAsync(_auth.CurrentUsername);
        _selected = migration.Persona;
        if (migration.MergedPersonas > 0)
        {
            await DisplayAlert(
                "The Context Game Updated",
                $"Kept {migration.Persona.Nickname} as your persona and merged {migration.MergedPersonas} other persona record{(migration.MergedPersonas == 1 ? "" : "s")}. Reassigned {migration.ReassignedPoints} data point{(migration.ReassignedPoints == 1 ? "" : "s")} and {migration.ReassignedInterviews} interview{(migration.ReassignedInterviews == 1 ? "" : "s")}.",
                "OK");
        }
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        _content.Children.Clear();
        if (_selected != null) await BuildDetailAsync();
    }

    private async Task BuildDetailAsync()
    {
        _contextExpBar = null;
        var companion = _selected!;
        var avatarState = await _service.GetAvatarStateAsync(_auth.CurrentUsername, companion);
        var contextGame = await _service.GetContextGameAsync(_auth.CurrentUsername);
        var stats = await _service.GetStatsAsync(_auth.CurrentUsername, companion.Id);
        _content.Children.Add(PageHeading("The Context Game", 24));
        _content.Children.Add((new Label
        {
            Text = "Build the context AI needs to really know you.",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText2));
        var personaText = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                PageHeading(companion.Nickname, 24),
                (new Label { Text = "All of Bannister", FontSize = 13 })
                    .ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText2)
            }
        };
        var personaHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        var avatarHost = new ContentView
        {
            Content = CompanionAvatarPresentation.Create(
                companion, 72, avatarState.LevelImage, avatarState.ImageFit)
        };
        var fitAvatar = ImageFitCycle.CreateButton(compact: true);
        fitAvatar.IsEnabled = avatarState.LevelImage is { Length: > 0 } ||
            companion.AvatarImage is { Length: > 0 };
        fitAvatar.Clicked += async (_, _) =>
        {
            int nextFit = ImageFitCycle.Next(avatarState.ImageFit);
            if (avatarState.LevelImageId.HasValue)
            {
                bool updated = await _service.UpdateLevelImageFitAsync(
                    _auth.CurrentUsername,
                    companion.Id,
                    avatarState.LevelImageId.Value,
                    nextFit);
                if (!updated) return;
            }
            else
            {
                companion.AvatarImageFit = nextFit;
                await _service.SaveCompanionAsync(companion);
            }
            avatarState = avatarState with { ImageFit = nextFit };
            avatarHost.Content = CompanionAvatarPresentation.Create(
                companion, 72, avatarState.LevelImage, avatarState.ImageFit);
        };
        personaHeader.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children = { avatarHost, fitAvatar }
        }, 0);
        personaHeader.Add(personaText, 1);
        var editPersona = Button("Edit", ThemeKey.WarningBackground, ThemeKey.WarningText);
        editPersona.VerticalOptions = LayoutOptions.Center;
        editPersona.Clicked += async (_, _) =>
        {
            var edited = await CompanionEditPage.ShowAsync(
                Navigation, companion, avatarState.LevelImage, avatarState.ImageFit);
            if (edited == null) return;
            await _service.SaveCompanionAsync(edited);
            _selected = edited;
            await RefreshAsync();
        };
        var levelImages = Button("Level Images", ThemeKey.SecondaryButtonBackground, ThemeKey.ButtonPrimaryText);
        levelImages.Clicked += async (_, _) => await Navigation.PushAsync(
            new CompanionLevelImagesPage(_auth.CurrentUsername, companion, _service));
        personaHeader.Add(new VerticalStackLayout
        {
            Spacing = 6,
            VerticalOptions = LayoutOptions.Center,
            Children = { editPersona, levelImages }
        }, 2);
        _content.Children.Add(personaHeader);
        if (contextGame != null)
        {
            var expBar = new ExpBarView(
                _auth.CurrentUsername, contextGame.GameId, ExpBarPresentation.Page);
            _contextExpBar = expBar;
            expBar.LevelChanged += async _ =>
            {
                avatarState = await _service.GetAvatarStateAsync(_auth.CurrentUsername, companion);
                avatarHost.Content = CompanionAvatarPresentation.Create(
                    companion, 72, avatarState.LevelImage, avatarState.ImageFit);
                fitAvatar.IsEnabled = avatarState.LevelImage is { Length: > 0 } ||
                    companion.AvatarImage is { Length: > 0 };
            };
            _content.Children.Add(expBar);
        }
        _content.Children.Add(BuildStatsRow(stats));
        var allGames = await GetAllGamesAsync();
        var gamesById = allGames.ToDictionary(x => x.GameId, StringComparer.OrdinalIgnoreCase);
        var displayMode = _privacyMode.GetDisplayMode(_auth.CurrentUsername);
        var games = GamesForDisplayMode(allGames, displayMode);
        const string scopeLabel = "All of Bannister";

        var interviews = await _service.GetInterviewsAsync(_auth.CurrentUsername, companion.Id);
        if (_selectedInterview != null)
            _selectedInterview = interviews.FirstOrDefault(x => x.Id == _selectedInterview.Id);
        var interviewActions = new HorizontalStackLayout { Spacing = 8 };
        var start = Button("Start Interview", ThemeKey.SecondaryButtonBackground, ThemeKey.ButtonPrimaryText);
        start.Clicked += async (_, _) => await StartInterviewAsync(companion, allGames);
        interviewActions.Children.Add(start);
        var history = Button($"Interview History ({interviews.Count})", ThemeKey.NeutralButtonBackground, ThemeKey.NeutralButtonText);
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
            var resume = Button(CompanionInterviewPresentation.StepText(_selectedInterview), ThemeKey.HighlightIndigo, ThemeKey.HighlightIndigoText);
            resume.Clicked += async (_, _) => await ContinueInterviewAsync(companion, _selectedInterview, scopeLabel, allGames);
            interviewActions.Children.Add(resume);
            if (!string.IsNullOrWhiteSpace(_selectedInterview.Transcript))
            {
                var replace = Button("Paste Conversation Again", ThemeKey.WarningBackground, ThemeKey.WarningText);
                replace.Clicked += async (_, _) => await SaveConversationAsync(companion, _selectedInterview, scopeLabel, allGames);
                interviewActions.Children.Add(replace);
            }
        }
        _content.Children.Add(interviewActions);
        if (_selectedInterview != null)
            _content.Children.Add((new Label { Text = $"Current interview: {CompanionInterviewPresentation.StepText(_selectedInterview)}", FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1));

        _content.Children.Add(PageHeading(_showArchived ? "Data Points (including archived)" : "Active Data Points", 18));
        var pointActions = new FlexLayout
        {
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap
        };
        var addPoint = Button("+ Add Data Point", ThemeKey.SuccessButtonBackground, ThemeKey.ButtonPrimaryText);
        var allPoints = await _service.GetPointsAsync(_auth.CurrentUsername, companion.Id, _showArchived);
        var allCategories = await _service.GetPointCategoriesAsync(_auth.CurrentUsername);
        addPoint.Clicked += async (_, _) => await AddPointAsync(companion, scopeLabel, games, allCategories);
        var archived = Button(_showArchived ? "Hide Archived" : "Show Archived", ThemeKey.NeutralButtonBackground, ThemeKey.CardText2);
        archived.Clicked += async (_, _) => { _showArchived = !_showArchived; await RefreshAsync(); };
        var rephrase = Button("Rephrase with LLM", ThemeKey.ButtonPrimaryBackground, ThemeKey.ButtonPrimaryText);
        rephrase.Clicked += async (_, _) => await ChooseRephraseScopeAsync(
            companion, scopeLabel, allPoints, gamesById, displayMode);
        foreach (var action in new[] { addPoint, archived, rephrase })
        {
            action.Margin = 3;
            pointActions.Children.Add(action);
        }
        _content.Children.Add(pointActions);
        var modeVisiblePoints = allPoints.Where(x => CompanionService.IsVisibleInDisplayMode(x, gamesById, displayMode)).ToList();
        if (modeVisiblePoints.Count != allPoints.Count)
            _content.Children.Add((new Label
            {
                Text = $"{modeVisiblePoints.Count} / {allPoints.Count} shown",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1));

        var gameOptions = new List<CompanionPointFilterOption>
        {
            new("All games", null, false),
            new("No game", null, true)
        };
        gameOptions.AddRange(games.Select(x => new CompanionPointFilterOption(x.DisplayName, x.GameId, false)));
        var gameFilter = (new Picker
        {
            ItemsSource = gameOptions,
            ItemDisplayBinding = new Binding(nameof(CompanionPointFilterOption.Label))
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(Picker.TextColorProperty, ThemeKey.InputText);
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
        var categoryFilter = (new Picker
        {
            ItemsSource = categoryOptions,
            ItemDisplayBinding = new Binding(nameof(CompanionPointFilterOption.Label))
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(Picker.TextColorProperty, ThemeKey.InputText);
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
                (new Label { Text = "Game", FontSize = 12, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3),
                gameFilter
            }
        };
        var categoryFilterStack = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                (new Label { Text = "Category", FontSize = 12, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3),
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

        var points = FilterCurrentlyDisplayedPoints(modeVisiblePoints);
        if (points.Count == 0) _content.Children.Add((new Label { Text = "No data points yet." }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1));
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
        stack.Children.Add((new Label { Text = point.Text, FontSize = 14 }).ThemeColor(Label.TextColorProperty, point.Status == "archived" ? ThemeKey.CardText3 : ThemeKey.CardText1, ThemeKey.PageText2));

        var categoryChoices = CompanionDataPointEditorOptions.BuildClassifications(games, categories, point.Category);
        var categoryPicker = (new Picker
        {
            ItemsSource = categoryChoices,
            ItemDisplayBinding = new Binding(nameof(CompanionPointClassificationOption.Label)),
            FontSize = 12,
            MinimumHeightRequest = 44,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 175
        }).ThemeColor(Picker.TextColorProperty, ThemeKey.InputText).ThemeColor(Picker.TitleColorProperty, ThemeKey.CardText2);
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
            await _service.SavePointAsync(point);

            var displayMode = _privacyMode.GetDisplayMode(_auth.CurrentUsername);
            bool remainsVisible = CompanionService.IsVisibleInDisplayMode(point, gamesById, displayMode);
            if (!remainsVisible && displayMode != ActivityDisplayMode.All)
                await DisplayAlert("Visibility Changed", $"Hidden in {DisplayModeName(displayMode)} mode.", "OK");
            await RefreshAsync();
        };

        var visibilityPicker = (new Picker
        {
            ItemsSource = CompanionDataPointEditorOptions.Visibility,
            FontSize = 12,
            MinimumHeightRequest = 44,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 165
        }).ThemeColor(Picker.TextColorProperty, ThemeKey.InputText).ThemeColor(Picker.TitleColorProperty, ThemeKey.CardText2);
        visibilityPicker.SelectedItem = CompanionDataPointEditorOptions.Visibility[
            CompanionDataPointEditorOptions.VisibilityIndex(point.VisibilityOverride)];
        TraceInlinePicker(visibilityPicker, point.Id, "visibility");
        var autoResult = (new Label
        {
            Text = AutoVisibilityText(point, gamesById),
            FontSize = 11,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = !point.VisibilityOverride.HasValue
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3);
        visibilityPicker.SelectedIndexChanged += async (_, _) =>
        {
            int? newOverride = CompanionDataPointEditorOptions.VisibilityOverride(visibilityPicker.SelectedIndex);
            if (point.VisibilityOverride == newOverride) return;
            point.VisibilityOverride = newOverride;
            await _service.SavePointAsync(point);

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
        stack.Children.Add((new Label { Text = $"Game: {gameName} · Category: {category} · {visibility}", FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        stack.Children.Add((new Label { Text = $"Priority {point.Priority} · {point.Source} · {point.Status} · {point.UpdatedAt.ToLocalTime():dd MMM yyyy}", FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        if (point.ReplacedByPointId.HasValue)
            stack.Children.Add((new Label { Text = $"Replaced by point {point.ReplacedByPointId.Value}", FontSize = 11 })
                .ThemeColor(Label.TextColorProperty, ThemeKey.CardText2));
        if (point.ReplacesPointId.HasValue)
            stack.Children.Add((new Label { Text = $"Replaces point {point.ReplacesPointId.Value}", FontSize = 11 })
                .ThemeColor(Label.TextColorProperty, ThemeKey.CardText2));
        var actions = new HorizontalStackLayout { Spacing = 5 };
        var edit = Button("Edit", ThemeKey.WarningBackground, ThemeKey.WarningText);
        edit.Clicked += async (_, _) =>
        {
            var edited = await CompanionDataPointEditPage.ShowAsync(Navigation, point, games, categories);
            if (edited == null) return;
            edited.ApplyTo(point);
            await _service.SavePointAsync(point);
            await RefreshAsync();
        };
        var rephrase = Button("Rephrase", ThemeKey.SecondaryButtonBackground, ThemeKey.ButtonPrimaryText);
        rephrase.Clicked += async (_, _) => await RunRephraseFlowAsync(
            companion, scopeLabel, new[] { point }, $"Point {point.Id}");
        var archive = Button(point.Status == "active" ? "Archive" : "Restore", ThemeKey.NeutralButtonBackground, ThemeKey.CardText2);
        archive.Clicked += async (_, _) => { point.Status = point.Status == "active" ? "archived" : "active"; await _service.SavePointAsync(point); await RefreshAsync(); };
        var delete = Button("Delete", ThemeKey.ErrorBackground, ThemeKey.ErrorText);
        delete.Clicked += async (_, _) => { if (!await DisplayAlert("Delete Data Point", "Delete this data point?", "Delete", "Cancel")) return; await _service.DeletePointAsync(point); await RefreshAsync(); };
        actions.Children.Add(edit); actions.Children.Add(rephrase); actions.Children.Add(archive); actions.Children.Add(delete); stack.Children.Add(actions);
        return Card(stack);
    }

    private async Task AddPointAsync(Companion companion, string scopeLabel, List<Game> games, List<string> categories)
    {
        var edited = await CompanionDataPointEditPage.ShowAsync(Navigation, null, games, categories);
        if (edited == null) return;
        var saved = await _service.SaveManualPointAsync(_auth.CurrentUsername, companion, scopeLabel, edited.Text, edited.Priority,
            edited.GameId, edited.Category, edited.VisibilityOverride);
        if (_contextExpBar != null)
            await _contextExpBar.RefreshAnimatedAsync();
        string ideaNote = saved.IdeaLoggingFailures > 0 ? "\nCould not log the data point to Ideas." : "";
        await DisplayAlert("Data Point Added", $"Data point saved.{ideaNote}{AwardMessage([saved.Award])}", "OK");
        await RefreshAsync();
    }

    private async Task StartInterviewAsync(Companion companion, List<Game> games)
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
            _auth.CurrentUsername, companion, questionCount, "All of Bannister", games,
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
            await ReviewAndApplyExtractionAsync(companion, interview, scopeLabel, games, response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Extraction import failed: {ex}");
            await DisplayAlert("Extraction Import Failed", ex.Message, "OK");
        }
    }

    private List<CompanionDataPoint> FilterCurrentlyDisplayedPoints(IEnumerable<CompanionDataPoint> points) =>
        points.Where(x =>
            (_pointGameFilterIsNoGame ? string.IsNullOrWhiteSpace(x.GameId) :
                _pointGameFilter == null || string.Equals(x.GameId, _pointGameFilter, StringComparison.OrdinalIgnoreCase)) &&
            (_pointCategoryFilterIsNoCategory ? string.IsNullOrWhiteSpace(x.Category) :
                _pointCategoryFilter == null || string.Equals(x.Category, _pointCategoryFilter, StringComparison.OrdinalIgnoreCase)))
        .ToList();

    private async Task ChooseRephraseScopeAsync(
        Companion companion,
        string scopeLabel,
        IReadOnlyList<CompanionDataPoint> points,
        IReadOnlyDictionary<string, Game> gamesById,
        ActivityDisplayMode displayMode)
    {
        var visible = points.Where(x => CompanionService.IsVisibleInDisplayMode(x, gamesById, displayMode)).ToList();
        var category = visible.Where(x =>
            _pointCategoryFilterIsNoCategory ? string.IsNullOrWhiteSpace(x.Category) :
            _pointCategoryFilter == null || string.Equals(x.Category, _pointCategoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        var game = visible.Where(x =>
            _pointGameFilterIsNoGame ? string.IsNullOrWhiteSpace(x.GameId) :
            _pointGameFilter == null || string.Equals(x.GameId, _pointGameFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        var displayed = FilterCurrentlyDisplayedPoints(visible);

        string categoryName = _pointCategoryFilterIsNoCategory ? "No category" : _pointCategoryFilter ?? "All categories";
        string gameName = _pointGameFilterIsNoGame ? "No game" :
            _pointGameFilter == null ? "All games" :
            gamesById.TryGetValue(_pointGameFilter, out var selectedGame) ? selectedGame.DisplayName : _pointGameFilter;
        string allOption = $"All points ({visible.Count})";
        string categoryOption = $"Current Category: {categoryName} ({category.Count})";
        string gameOption = $"Current Game: {gameName} ({game.Count})";
        string displayedOption = $"Currently displayed ({displayed.Count})";
        string? selected = await DisplayActionSheet(
            "Rephrase which points?", "Cancel", null,
            allOption, categoryOption, gameOption, displayedOption);

        IReadOnlyList<CompanionDataPoint>? scope = selected switch
        {
            var value when value == allOption => visible,
            var value when value == categoryOption => category,
            var value when value == gameOption => game,
            var value when value == displayedOption => displayed,
            _ => null
        };
        if (scope == null) return;
        await RunRephraseFlowAsync(companion, scopeLabel, scope, selected!);
    }

    private async Task RunRephraseFlowAsync(
        Companion companion,
        string scopeLabel,
        IReadOnlyList<CompanionDataPoint> points,
        string rephraseScope)
    {
        if (points.Count == 0)
        {
            await DisplayAlert("No Points", "There are no points in that scope.", "OK");
            return;
        }

        try
        {
            var instructionPage = new CompanionRephraseInstructionPage(rephraseScope, points.Count);
            await Navigation.PushModalAsync(instructionPage);
            string? instruction = await instructionPage.Result;
            if (instruction == null) return;

            string prompt = CompanionService.BuildRephrasePrompt(companion, points, instruction);
            await Clipboard.SetTextAsync(prompt);
            await DisplayAlert("Rephrase Prompt Copied", "Run the copied prompt in your LLM, then paste its response on the next screen.", "OK");

            var pastePage = new CompanionRephrasePastePage();
            await Navigation.PushModalAsync(pastePage);
            string? response = await pastePage.Result;
            if (string.IsNullOrWhiteSpace(response)) return;

            var currentCompanionPoints = await _service.GetPointsAsync(
                _auth.CurrentUsername, companion.Id, includeArchived: true);
            var exportedIds = points.Select(x => x.Id).ToList();
            var parsed = CompanionService.ParseRephraseResult(response, currentCompanionPoints, exportedIds);
            if (parsed.RecognizedAssignmentCount == 0)
            {
                await DisplayAlert(
                    "Could Not Parse Response",
                    "No point[id] = \"new text\" entries were recognized. Nothing was changed.",
                    "OK");
                return;
            }

            var reviewPage = new CompanionRephraseReviewPage(parsed, currentCompanionPoints);
            await Navigation.PushModalAsync(reviewPage);
            var selected = await reviewPage.Result;
            if (selected == null || selected.Count == 0) return;

            var applied = await _service.ApplyRephraseAsync(
                _auth.CurrentUsername, companion, scopeLabel, exportedIds, selected);
            string skippedNote = applied.Skipped == 0 ? "" : $"\n{applied.Skipped} point(s) were no longer eligible or had already changed.";
            string ideaNote = $"\nIdeas logged: {applied.IdeasLogged}.";
            if (applied.IdeaLoggingFailures > 0)
                ideaNote += $" Failures: {applied.IdeaLoggingFailures}.";
            await DisplayAlert("Rephrase Applied", $"Updated {applied.Updated} point{(applied.Updated == 1 ? "" : "s")}.{skippedNote}{ideaNote}", "OK");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[COMPANIONS] Rephrase import failed: {ex}");
            await DisplayAlert("Rephrase Failed", ex.Message, "OK");
        }
    }

    private async Task ReviewAndApplyExtractionAsync(
        Companion companion,
        CompanionInterview? interview,
        string scopeLabel,
        List<Game> games,
        string response)
    {
        var extraction = CompanionService.ParseExtraction(response.ReplaceLineEndings("\n"));
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

        var reviewPoints = await _service.GetPointsAsync(_auth.CurrentUsername, companion.Id, includeArchived: true);
        var applied = await CompanionExtractionReviewPage.ShowAsync(Navigation, extraction, reviewPoints, async (adds, priorities, archives, updates) =>
        {
            var result = await _service.ApplyExtractionAsync(
                _auth.CurrentUsername, companion, interview, scopeLabel, adds, priorities, archives, updates);
            if (interview != null)
                await TrySaveInterviewIdeaAsync(companion, interview, scopeLabel);
            return result;
        });
        if (applied == null) return;
        if (_contextExpBar != null)
            await _contextExpBar.RefreshAnimatedAsync();

        string loggingNote = applied.IdeaLoggingFailures > 0
            ? $", idea logging failures {applied.IdeaLoggingFailures}"
            : "";
        await DisplayAlert(
            "Extraction Applied",
            $"Added {applied.Added}, reprioritized {applied.Reprioritized}, archived {applied.Archived}, replaced {applied.Updated}{loggingNote}{AwardMessage(applied.Awards)}",
            "OK");
        await RefreshAsync();
    }

    private static string AwardMessage(IReadOnlyList<ExpAwardResult> awards)
    {
        if (awards.Count == 0) return "";
        var messages = new List<string>();
        if (awards.Any(award => award.Succeeded && !award.AlreadyAwarded || award.ExpAwarded != 0))
            messages.Add($"EXP awarded: {awards.Sum(award => award.ExpAwarded):+#;-#;0}");
        messages.AddRange(awards.Select(award => award.FailureReason)
            .OfType<string>().Distinct().Select(reason => $"EXP award failed: {reason}"));
        return messages.Count == 0 ? "" : "\n\n" + string.Join("\n", messages);
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

    private View BuildStatsRow(CompanionStats stats)
    {
        var row = new FlexLayout
        {
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row,
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start
        };
        row.Children.Add(StatCard(stats.ActivePointCount.ToString("N0"), "Active points"));
        row.Children.Add(StatCard(stats.ArchivedPointCount.ToString("N0"), "Archived"));
        row.Children.Add(StatCard(stats.CompletedInterviewCount.ToString("N0"), "Interviews"));
        var gamesCovered = StatCard(stats.GamesCovered.Count.ToString("N0"), "Games covered");
        var showGames = new TapGestureRecognizer();
        showGames.Tapped += async (_, _) =>
        {
            string details = stats.GamesCovered.Count == 0
                ? "No active context points are assigned to a game."
                : string.Join("\n", stats.GamesCovered.Select(x => $"{x.DisplayName}: {x.PointCount:N0}"));
            await DisplayAlert("Games Covered", details, "OK");
        };
        gamesCovered.GestureRecognizers.Add(showGames);
        row.Children.Add(gamesCovered);
        row.Children.Add(StatCard(stats.ExpEarnedToday.ToString("N0"), "EXP today"));
        return row;
    }

    private static Frame StatCard(string value, string label) =>
        (new Frame
        {
            Padding = new Thickness(10, 7),
            Margin = new Thickness(0, 0, 6, 6),
            HasShadow = false,
            MinimumWidthRequest = 105,
            Content = new VerticalStackLayout
            {
                Spacing = 1,
                Children =
                {
                    (new Label { Text = value, FontSize = 16, FontAttributes = FontAttributes.Bold })
                        .ThemeColor(Label.TextColorProperty, ThemeKey.CardText1),
                    (new Label { Text = label, FontSize = 10 })
                        .ThemeColor(Label.TextColorProperty, ThemeKey.CardText2)
                }
            }
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator)
          .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

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

    private Label PageHeading(string text, double size) => (new Label(){ Text = text, FontSize = size, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1);
    private static Label Heading(string text, double size) => (new Label(){ Text = text, FontSize = size, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1);
    private static Button Button(string text, ThemeKey background, ThemeKey foreground) => (new Button(){ Text = text, CornerRadius = 7, FontSize = 12, HeightRequest = 38, Padding = new Thickness(10, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, background).ThemeColor(Microsoft.Maui.Controls.Button.TextColorProperty, foreground);
    private static Frame Card(View content) => (new Frame(){ Content = content, Padding = 12, CornerRadius = 9, HasShadow = false }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
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

internal sealed class CompanionDataPointEditPage : ModeThemedPage
{
    private readonly TaskCompletionSource<CompanionDataPointEditResult?> _completion = new();

    private CompanionDataPointEditPage(CompanionDataPoint? point, List<Game> games, List<string> categories)
    {
        Title = point == null ? "Add Data Point" : "Edit Data Point";
        var titleLabel = (new Label
        {
            Text = Title,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1);
        var textLabel = (new Label
        {
            Text = "Text",
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1);
        var text = (new Editor
        {
            Placeholder = "Write a durable fact or observation...",
            Text = point?.Text ?? "",
            HeightRequest = 150,
            AutoSize = EditorAutoSizeOption.TextChanges
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
            .ThemeColor(InputView.TextColorProperty, ThemeKey.InputText)
            .ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3);
        var priorityLabel = (new Label
        {
            Text = "Priority",
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1);
        var priorityHint = (new Label
        {
            Text = "1 = low, 5 = top priority",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText3);
        var priority = (new Entry
        {
            Text = (point?.Priority ?? 3).ToString(),
            Keyboard = Keyboard.Numeric
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
            .ThemeColor(InputView.TextColorProperty, ThemeKey.InputText)
            .ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3);
        var classificationOptions = CompanionDataPointEditorOptions.BuildClassifications(
            games, categories, point?.Category);
        var classificationLabel = (new Label
        {
            Text = "Game or category",
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1);
        var classification = (new Picker
        {
            ItemsSource = classificationOptions,
            ItemDisplayBinding = new Binding(nameof(CompanionPointClassificationOption.Label))
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
            .ThemeColor(Picker.TextColorProperty, ThemeKey.InputText);
        SemanticProperties.SetDescription(classification, "Game or category");
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

        var visibilityLabel = (new Label
        {
            Text = "Visibility",
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1);
        var visibility = (new Picker { ItemsSource = CompanionDataPointEditorOptions.Visibility })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
            .ThemeColor(Picker.TextColorProperty, ThemeKey.InputText);
        SemanticProperties.SetDescription(visibility, "Visibility");
        visibility.SelectedItem = CompanionDataPointEditorOptions.Visibility[
            CompanionDataPointEditorOptions.VisibilityIndex(point?.VisibilityOverride)];

        var save = (new Button { Text = "Save" }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Microsoft.Maui.Controls.Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
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
        var cancel = (new Button { Text = "Cancel" })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground)
            .ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        cancel.Clicked += async (_, _) => await CloseAsync(null);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 9,
                Children =
                {
                    titleLabel,
                    textLabel, text,
                    priorityLabel, priorityHint, priority,
                    classificationLabel, classification,
                    visibilityLabel, visibility,
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

internal sealed class CompanionEditPage : ModeThemedPage
{
    private readonly TaskCompletionSource<Companion?> _completion = new();
    private CompanionEditPage(Companion item, byte[]? displayedImage, int? displayedImageFit)
    {
        Title = "Edit Context Game Persona";
        byte[]? pendingImage = item.AvatarImage;
        int pendingImageFit = item.AvatarImageFit;
        var nickname = (new Entry { Placeholder = "Name", Text = item.Nickname })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
            .ThemeColor(Entry.TextColorProperty, ThemeKey.InputText)
            .ThemeColor(Entry.PlaceholderColorProperty, ThemeKey.CardText3);
        var persona = (new Editor { Placeholder = "Persona description", Text = item.PersonaDescription, HeightRequest = 90 })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
            .ThemeColor(Editor.TextColorProperty, ThemeKey.InputText)
            .ThemeColor(Editor.PlaceholderColorProperty, ThemeKey.CardText3);
        var stance = (new Editor { Placeholder = "Interview stance", Text = item.InterviewStance, HeightRequest = 90 })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
            .ThemeColor(Editor.TextColorProperty, ThemeKey.InputText)
            .ThemeColor(Editor.PlaceholderColorProperty, ThemeKey.CardText3);
        var questionCount = (new Entry { Placeholder = "Number of questions", Text = item.DefaultQuestionCount.ToString(), Keyboard = Keyboard.Numeric })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
            .ThemeColor(Entry.TextColorProperty, ThemeKey.InputText)
            .ThemeColor(Entry.PlaceholderColorProperty, ThemeKey.CardText3);
        var avatarHost = new ContentView
        {
            Content = CompanionAvatarPresentation.Create(item, 112, displayedImage, displayedImageFit),
            HorizontalOptions = LayoutOptions.Start
        };
        void RefreshAvatar()
        {
            var preview = new Companion
            {
                Nickname = nickname.Text ?? item.Nickname,
                AvatarImage = pendingImage,
                AvatarImageFit = pendingImageFit
            };
            avatarHost.Content = CompanionAvatarPresentation.Create(
                preview, 112, displayedImage, displayedImageFit);
        }
        nickname.TextChanged += (_, _) => { if (pendingImage is not { Length: > 0 }) RefreshAvatar(); };
        var replaceImage = (new Button { Text = pendingImage is { Length: > 0 } ? "Replace Image" : "Choose Image" })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground)
            .ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var removeImage = (new Button { Text = "Remove Image", IsEnabled = pendingImage is { Length: > 0 } })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground)
            .ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        replaceImage.Clicked += async (_, _) =>
        {
            try
            {
                var picked = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Choose persona image",
                    FileTypes = FilePickerFileType.Images
                });
                if (picked == null) return;
                if (!CompanionAvatarImageProcessor.IsSupportedFileName(picked.FileName))
                {
                    await DisplayAlert("Unsupported Image", "Choose a PNG, JPG, or WebP image.", "OK");
                    return;
                }
                await using var stream = await picked.OpenReadAsync();
                pendingImage = CompanionAvatarImageProcessor.LoadAndResizePng(stream);
                pendingImageFit = ImageFitCycle.DefaultMode;
                replaceImage.Text = "Replace Image";
                removeImage.IsEnabled = true;
                RefreshAvatar();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Image Could Not Be Loaded", ex.Message, "OK");
            }
        };
        removeImage.Clicked += (_, _) =>
        {
            pendingImage = null;
            pendingImageFit = ImageFitCycle.DefaultMode;
            replaceImage.Text = "Choose Image";
            removeImage.IsEnabled = false;
            RefreshAvatar();
        };
        var save = (new Button { Text = "Save" }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Microsoft.Maui.Controls.Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        save.Clicked += async (_, _) =>
        {
            string normalizedNickname = (nickname.Text ?? "").ReplaceLineEndings("\n").Trim();
            string normalizedPersona = (persona.Text ?? "").ReplaceLineEndings("\n").Trim();
            string normalizedStance = (stance.Text ?? "").ReplaceLineEndings("\n").Trim();
            string normalizedQuestionCount = (questionCount.Text ?? "").ReplaceLineEndings("\n").Trim();
            if (string.IsNullOrWhiteSpace(normalizedNickname)) { await DisplayAlert("Name Required", "Enter a persona name.", "OK"); return; }
            if (!int.TryParse(normalizedQuestionCount, out int questions) || questions is < 1 or > 50) { await DisplayAlert("Invalid Number of Questions", "Enter a whole number from 1 to 50.", "OK"); return; }
            item.Nickname = normalizedNickname;
            item.Scope = "meta";
            item.GameId = null;
            item.Topic = "";
            item.PersonaDescription = normalizedPersona;
            item.InterviewStance = string.IsNullOrWhiteSpace(normalizedStance) ? "get to know the user across all of Bannister" : normalizedStance;
            item.DefaultQuestionCount = questions;
            item.AvatarImage = pendingImage;
            item.AvatarImageFit = pendingImageFit;
            item.AvatarPath = null;
            await CloseAsync(item);
        };
        var cancel = (new Button { Text = "Cancel" })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground)
            .ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        cancel.Clicked += async (_, _) => await CloseAsync(null);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 18, Spacing = 9, Children =
        {
            (new Label { Text = Title, FontSize = 22, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1),
            avatarHost,
            new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { replaceImage, removeImage } },
            nickname, persona, stance, questionCount,
            new HorizontalStackLayout { Spacing = 8, Children = { save, cancel } }
        } } };
    }
    private async Task CloseAsync(Companion? result) { await Navigation.PopModalAsync(); _completion.TrySetResult(result); }
    public static async Task<Companion?> ShowAsync(
        INavigation navigation,
        Companion item,
        byte[]? displayedImage = null,
        int? displayedImageFit = null)
    {
        var page = new CompanionEditPage(item, displayedImage, displayedImageFit);
        await navigation.PushModalAsync(page);
        return await page._completion.Task;
    }
}

internal sealed class CompanionTextInputPage : ModeThemedPage
{
    private readonly TaskCompletionSource<string?> _completion = new();
    private CompanionTextInputPage(string title, string initial, string placeholder)
    {
        Title = title;
        var editor = (new Editor { Text = initial, Placeholder = placeholder, HeightRequest = 320, AutoSize = EditorAutoSizeOption.Disabled })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
            .ThemeColor(Editor.TextColorProperty, ThemeKey.InputText)
            .ThemeColor(Editor.PlaceholderColorProperty, ThemeKey.CardText3);
        var paste = (new Button { Text = "Paste" }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Microsoft.Maui.Controls.Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        paste.Clicked += async (_, _) => { try { if (!Clipboard.HasText) return; var text = await Clipboard.GetTextAsync(); if (string.IsNullOrEmpty(text)) return; string current = editor.Text ?? ""; int start = Math.Clamp(editor.CursorPosition, 0, current.Length); int length = Math.Clamp(editor.SelectionLength, 0, current.Length - start); editor.Text = current.Remove(start, length).Insert(start, text); editor.CursorPosition = start + text.Length; editor.SelectionLength = 0; } finally { editor.Focus(); } };
        var ok = (new Button { Text = "OK" }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Microsoft.Maui.Controls.Button.TextColorProperty, ThemeKey.ButtonPrimaryText); ok.Clicked += async (_, _) => await CloseAsync(editor.Text);
        var cancel = (new Button { Text = "Cancel" })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground)
            .ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        cancel.Clicked += async (_, _) => await CloseAsync(null);
        Content = new Grid { Padding = 16, RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }, Children = { editor } };
        var buttons = new FlexLayout { Direction = Microsoft.Maui.Layouts.FlexDirection.Row, Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start, Children = { paste, ok, cancel } }; Grid.SetRow(buttons, 1); ((Grid)Content).Children.Add(buttons);
    }
    private async Task CloseAsync(string? value) { await Navigation.PopModalAsync(); _completion.TrySetResult(value); }
    public static async Task<string?> ShowAsync(INavigation navigation, string title, string initial, string placeholder) { var page = new CompanionTextInputPage(title, initial, placeholder); await navigation.PushModalAsync(page); return await page._completion.Task; }
}

internal sealed class CompanionExtractionReviewPage : ModeThemedPage
{
    private readonly TaskCompletionSource<CompanionApplyResult?> _completion = new();
    private CompanionExtractionReviewPage(CompanionExtraction result, IReadOnlyList<CompanionDataPoint> points, Func<List<CompanionAddProposal>, List<CompanionPriorityProposal>, List<CompanionArchiveProposal>, List<CompanionUpdateProposal>, Task<CompanionApplyResult>> apply)
    {
        Title = "Review Extraction";
        var stack = new VerticalStackLayout { Padding = 16, Spacing = 8 };
        stack.Children.Add((new Label { Text = "Review Proposed Changes", FontSize = 22, FontAttributes = FontAttributes.Bold })
            .ThemeColor(Label.TextColorProperty, ThemeKey.PageText1));
        var adds = AddRows(stack, "Add points", result.Adds, x =>
            $"P{x.Priority}: {x.Text}\nGame: {(string.IsNullOrWhiteSpace(x.GameName) ? "No game" : x.GameName)} · Category: {(string.IsNullOrWhiteSpace(x.Category) ? "No category" : x.Category)}");
        var priorities = AddRows(stack, "Priority changes", result.Priorities, x => $"Point {x.PointId} → priority {x.Priority}");
        var archives = AddRows(stack, "Archives", result.Archives, x => $"Point {x.PointId}: {x.Reason}");
        var updates = AddUpdateRows(stack, result.Updates, points.ToDictionary(x => x.Id));
        if (result.Warnings.Count > 0) { stack.Children.Add((new Label { Text = $"Warnings ({result.Warnings.Count})", FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1)); foreach (var warning in result.Warnings) stack.Children.Add((new Label { Text = warning, FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText3)); }
        if (result.SkippedLines.Count > 0) { stack.Children.Add((new Label { Text = $"Skipped invalid lines ({result.SkippedLines.Count})", FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1)); foreach (var line in result.SkippedLines) stack.Children.Add((new Label { Text = line, FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText3)); }
        var applyButton = (new Button { Text = "Apply Selected" }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Microsoft.Maui.Controls.Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
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
        var cancel = (new Button { Text = "Cancel" })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground)
            .ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        cancel.Clicked += async (_, _) => await CloseAsync(null);
        stack.Children.Add(new HorizontalStackLayout { Spacing = 8, Children = { applyButton, cancel } }); Content = new ScrollView { Content = stack };
    }
    private static List<CheckBox> AddRows<T>(VerticalStackLayout stack, string title, List<T> values, Func<T, string> text) { var checks = new List<CheckBox>(); if (values.Count == 0) return checks; stack.Children.Add((new Label { Text = title, FontSize = 17, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1)); foreach (var value in values) { var check = new CheckBox { IsChecked = true }; checks.Add(check); stack.Children.Add(new HorizontalStackLayout { Spacing = 6, Children = { check, (new Label { Text = text(value), VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText2) } }); } return checks; }
    private static List<CheckBox> AddUpdateRows(
        VerticalStackLayout stack,
        List<CompanionUpdateProposal> values,
        IReadOnlyDictionary<int, CompanionDataPoint> points)
    {
        var checks = new List<CheckBox>();
        if (values.Count == 0) return checks;

        stack.Children.Add((new Label
        {
            Text = "Replacements",
            FontSize = 17,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText1));
        foreach (var value in values)
        {
            var check = new CheckBox { IsChecked = true };
            checks.Add(check);
            string oldText = points.TryGetValue(value.PointId, out var point)
                ? point.Text
                : "(point not found)";
            var description = (new Label
            {
                Text = $"Replace point {value.PointId}\nOld: {oldText}\nNew: {value.Text}",
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.PageText2);
            stack.Children.Add(new HorizontalStackLayout
            {
                Spacing = 6,
                Children = { check, description }
            });
        }
        return checks;
    }
    private static List<T> Selected<T>(List<T> values, List<CheckBox> checks) => values.Where((_, i) => checks[i].IsChecked).ToList();
    private async Task CloseAsync(CompanionApplyResult? result) { await Navigation.PopModalAsync(); _completion.TrySetResult(result); }
    private async Task CloseWithErrorAsync(Exception exception) { await Navigation.PopModalAsync(); _completion.TrySetException(exception); }
    public static async Task<CompanionApplyResult?> ShowAsync(INavigation navigation, CompanionExtraction result, IReadOnlyList<CompanionDataPoint> points, Func<List<CompanionAddProposal>, List<CompanionPriorityProposal>, List<CompanionArchiveProposal>, List<CompanionUpdateProposal>, Task<CompanionApplyResult>> apply) { var page = new CompanionExtractionReviewPage(result, points, apply); await navigation.PushModalAsync(page); return await page._completion.Task; }
}

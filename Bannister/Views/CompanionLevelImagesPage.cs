using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

internal sealed record CompanionLevelImageAssignment(int PanelNumber, int Level, byte[] ImageBytes);

internal sealed class CompanionLevelImagesPage : ModeThemedPage
{
    private const string PromptInstructions = "Create the result as a single 9:16 vertical concept sheet containing 20 numbered variations arranged in a 4x5 grid. Every panel shows this same character at a different level, as if in a video game where the character levels up: panel 1 is the weakest beginner form and panel 20 the fully mastered, all-knowing form, with clear progression between them. Each panel must have a different design, outfit, pose, composition, camera angle, environment, background, mood and visual hook. Prioritize variety of ideas over small visual changes, while keeping the character recognizable. Large visible numbers 1\u201320. Cinematic realistic, high detail, easy side-by-side comparison, no text except numbers.";

    private readonly string _username;
    private readonly Companion _companion;
    private readonly CompanionService _service;
    private readonly Editor _description;
    private readonly Button _copyPrompt;
    private readonly Label _currentLevel;
    private readonly VerticalStackLayout _assignedImages = new() { Spacing = 10 };
    private bool _loading;

    public CompanionLevelImagesPage(string username, Companion companion, CompanionService service)
    {
        _username = username;
        _companion = companion;
        _service = service;
        Title = "Level Images";

        _description = (new Editor
        {
            Text = companion.PersonaDescription,
            Placeholder = "Describe the character",
            AutoSize = EditorAutoSizeOption.TextChanges,
            HeightRequest = 120
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
          .ThemeColor(Editor.TextColorProperty, ThemeKey.InputText)
          .ThemeColor(Editor.PlaceholderColorProperty, ThemeKey.CardText3);

        var saveDescription = ActionButton("Save description", ThemeKey.SecondaryButtonBackground, ThemeKey.ButtonPrimaryText);
        saveDescription.Clicked += async (_, _) => await SaveDescriptionAsync(showConfirmation: true);

        _copyPrompt = ActionButton("Copy image prompt", ThemeKey.ButtonPrimaryBackground, ThemeKey.ButtonPrimaryText);
        _copyPrompt.IsEnabled = HasDescription();
        _copyPrompt.Clicked += async (_, _) =>
        {
            string description = NormalizedDescription();
            if (string.IsNullOrWhiteSpace(description)) return;
            await SaveDescriptionAsync(showConfirmation: false);
            await Clipboard.SetTextAsync($"Character: {description}\n{PromptInstructions}");
            await DisplayAlert("Prompt Copied", "The level-image prompt was copied to the clipboard.", "OK");
        };
        _description.TextChanged += (_, _) => _copyPrompt.IsEnabled = HasDescription();

        var importSheet = ActionButton("Import concept sheet", ThemeKey.SuccessButtonBackground, ThemeKey.ButtonPrimaryText);
        importSheet.Clicked += async (_, _) => await ImportConceptSheetAsync();
        var addSingle = ActionButton("Add single image", ThemeKey.SecondaryButtonBackground, ThemeKey.ButtonPrimaryText);
        addSingle.Clicked += async (_, _) => await AddSingleImageAsync();

        _currentLevel = PageLabel("Level 1", ThemeKey.PageText2, 14, bold: true);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 12,
                Children =
                {
                    PageLabel("Level Images", ThemeKey.PageText1, 24, bold: true),
                    _currentLevel,
                    PageLabel("Who is your avatar?", ThemeKey.PageText1, 16, bold: true),
                    _description,
                    new FlexLayout
                    {
                        Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                        Children = { saveDescription, _copyPrompt }
                    },
                    new FlexLayout
                    {
                        Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                        Children = { importSheet, addSingle }
                    },
                    PageLabel("Assigned levels", ThemeKey.PageText1, 19, bold: true),
                    _assignedImages
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private bool HasDescription() => !string.IsNullOrWhiteSpace(NormalizedDescription());

    private string NormalizedDescription() =>
        (_description.Text ?? "").ReplaceLineEndings("\n").Trim();

    private async Task SaveDescriptionAsync(bool showConfirmation)
    {
        _companion.PersonaDescription = NormalizedDescription();
        await _service.SaveCompanionAsync(_companion);
        if (showConfirmation)
            await DisplayAlert("Description Saved", "The avatar description was saved.", "OK");
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            _currentLevel.Text = $"Current Context Game level: {await _service.GetContextGameLevelAsync(_username)}";
            var images = await _service.GetLevelImagesAsync(_username, _companion.Id);
            _assignedImages.Children.Clear();
            if (images.Count == 0)
            {
                _assignedImages.Children.Add(PageLabel("No level images assigned yet.", ThemeKey.PageText2));
                return;
            }
            foreach (var image in images)
                _assignedImages.Children.Add(BuildAssignedImageCard(image));
        }
        finally
        {
            _loading = false;
        }
    }

    private View BuildAssignedImageCard(CompanionLevelImage levelImage)
    {
        var thumbnail = new Image
        {
            Source = BytesSource(levelImage.ImageBytes),
            Aspect = Aspect.AspectFill,
            HeightRequest = 110,
            WidthRequest = 90
        };
        var details = new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                CardLabel($"Level {levelImage.Level}", ThemeKey.CardText1, 17, bold: true),
                CardLabel($"Assigned {levelImage.CreatedAt.ToLocalTime():dd MMM yyyy}", ThemeKey.CardText3, 11)
            }
        };
        var replace = ActionButton("Replace image", ThemeKey.SecondaryButtonBackground, ThemeKey.ButtonPrimaryText);
        replace.Clicked += async (_, _) => await ReplaceImageAsync(levelImage);
        var changeLevel = ActionButton("Change level", ThemeKey.NeutralButtonBackground, ThemeKey.NeutralButtonText);
        changeLevel.Clicked += async (_, _) => await ChangeLevelAsync(levelImage);
        var remove = ActionButton("Remove", ThemeKey.ErrorBackground, ThemeKey.ErrorText);
        remove.Clicked += async (_, _) => await RemoveImageAsync(levelImage);
        details.Children.Add(new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Children = { replace, changeLevel, remove }
        });

        var row = new HorizontalStackLayout { Spacing = 12, Children = { thumbnail, details } };
        return (new Frame { Padding = 12, HasShadow = false, Content = row })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground)
            .ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
    }

    private async Task ImportConceptSheetAsync()
    {
        try
        {
            var picked = await PickImageAsync("Choose the 20-panel concept sheet");
            if (picked == null) return;
            await using var stream = await picked.OpenReadAsync();
            var cells = CompanionAvatarImageProcessor.SplitConceptSheet(stream);
            var assignments = await CompanionConceptSheetPreviewPage.ShowAsync(Navigation, cells);
            if (assignments == null || assignments.Count == 0) return;

            var duplicate = assignments.GroupBy(x => x.Level).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                await DisplayAlert("Duplicate Level", $"More than one selected panel is assigned to level {duplicate.Key}.", "OK");
                return;
            }

            var existing = (await _service.GetLevelImagesAsync(_username, _companion.Id))
                .ToDictionary(x => x.Level);
            int saved = 0;
            foreach (var assignment in assignments)
            {
                bool replace = existing.ContainsKey(assignment.Level);
                if (replace && !await DisplayAlert(
                    "Replace Level Image?",
                    $"Level {assignment.Level} already has an image. Replace it with panel {assignment.PanelNumber}?",
                    "Replace",
                    "Skip"))
                    continue;
                if (await _service.SaveLevelImageAsync(
                    _username, _companion.Id, assignment.Level, assignment.ImageBytes, replace))
                {
                    saved++;
                    existing[assignment.Level] = new CompanionLevelImage { Level = assignment.Level };
                }
            }
            await LoadAsync();
            await DisplayAlert("Concept Sheet Imported", $"Saved {saved} level image{(saved == 1 ? "" : "s")}.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Import Failed", ex.Message, "OK");
        }
    }

    private async Task AddSingleImageAsync()
    {
        try
        {
            int? level = await PromptForLevelAsync("Add Single Image", null);
            if (!level.HasValue) return;
            var picked = await PickImageAsync($"Choose image for level {level.Value}");
            if (picked == null) return;
            await using var stream = await picked.OpenReadAsync();
            byte[] image = CompanionAvatarImageProcessor.LoadAndResizePng(stream);
            bool exists = (await _service.GetLevelImagesAsync(_username, _companion.Id))
                .Any(x => x.Level == level.Value);
            if (exists && !await ConfirmReplaceLevelAsync(level.Value)) return;
            await _service.SaveLevelImageAsync(_username, _companion.Id, level.Value, image, exists);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Image Could Not Be Loaded", ex.Message, "OK");
        }
    }

    private async Task ReplaceImageAsync(CompanionLevelImage levelImage)
    {
        try
        {
            var picked = await PickImageAsync($"Replace level {levelImage.Level} image");
            if (picked == null || !await ConfirmReplaceLevelAsync(levelImage.Level)) return;
            await using var stream = await picked.OpenReadAsync();
            byte[] image = CompanionAvatarImageProcessor.LoadAndResizePng(stream);
            await _service.SaveLevelImageAsync(_username, _companion.Id, levelImage.Level, image, replaceExisting: true);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Image Could Not Be Loaded", ex.Message, "OK");
        }
    }

    private async Task ChangeLevelAsync(CompanionLevelImage levelImage)
    {
        int? newLevel = await PromptForLevelAsync("Change Level", levelImage.Level);
        if (!newLevel.HasValue || newLevel.Value == levelImage.Level) return;
        bool exists = (await _service.GetLevelImagesAsync(_username, _companion.Id))
            .Any(x => x.Id != levelImage.Id && x.Level == newLevel.Value);
        if (exists && !await ConfirmReplaceLevelAsync(newLevel.Value)) return;
        await _service.ChangeLevelImageLevelAsync(
            _username, _companion.Id, levelImage.Id, newLevel.Value, exists);
        await LoadAsync();
    }

    private async Task RemoveImageAsync(CompanionLevelImage levelImage)
    {
        if (!await DisplayAlert(
            "Remove Level Image?",
            $"Remove the image assigned to level {levelImage.Level}?",
            "Remove",
            "Cancel"))
            return;
        await _service.DeleteLevelImageAsync(_username, _companion.Id, levelImage.Id);
        await LoadAsync();
    }

    private async Task<bool> ConfirmReplaceLevelAsync(int level) =>
        await DisplayAlert(
            "Replace Level Image?",
            $"Level {level} already has an image. Replace it?",
            "Replace",
            "Cancel");

    private async Task<int?> PromptForLevelAsync(string title, int? initial)
    {
        string? value = await DisplayPromptAsync(
            title,
            $"Level (1\u2013{ExpEngine.MaxLevel})",
            "Continue",
            "Cancel",
            initialValue: initial?.ToString() ?? "",
            keyboard: Keyboard.Numeric);
        if (value == null) return null;
        string normalized = value.ReplaceLineEndings("\n").Trim();
        if (!int.TryParse(normalized, out int level) || level is < 1 or > ExpEngine.MaxLevel)
        {
            await DisplayAlert("Invalid Level", $"Enter a whole number from 1 to {ExpEngine.MaxLevel}.", "OK");
            return null;
        }
        return level;
    }

    private static async Task<FileResult?> PickImageAsync(string title)
    {
        var picked = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = title,
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, new[] { ".png", ".jpg", ".jpeg", ".webp" } }
            })
        });
        if (picked == null) return null;
        if (!CompanionAvatarImageProcessor.IsSupportedFileName(picked.FileName))
            throw new InvalidOperationException("Choose a PNG, JPG, JPEG, or WebP image.");
        return picked;
    }

    private static ImageSource BytesSource(byte[] bytes) =>
        ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));

    private static Label PageLabel(string text, ThemeKey color, double size = 14, bool bold = false) =>
        (new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, color);

    private static Label CardLabel(string text, ThemeKey color, double size = 14, bool bold = false) =>
        (new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, color);

    private static Button ActionButton(string text, ThemeKey background, ThemeKey foreground) =>
        (new Button { Text = text, Margin = 3 })
            .ThemeColor(VisualElement.BackgroundColorProperty, background)
            .ThemeColor(Button.TextColorProperty, foreground);
}

internal sealed class CompanionConceptSheetPreviewPage : ModeThemedPage
{
    private sealed record PreviewRow(int PanelNumber, byte[] ImageBytes, CheckBox Selected, Entry Level);

    private readonly TaskCompletionSource<IReadOnlyList<CompanionLevelImageAssignment>?> _completion =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<PreviewRow> _rows = [];
    private bool _closing;

    private CompanionConceptSheetPreviewPage(IReadOnlyList<byte[]> cells)
    {
        Title = "Review Concept Sheet";
        var panels = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Direction = Microsoft.Maui.Layouts.FlexDirection.Row
        };
        for (int index = 0; index < cells.Count; index++)
        {
            int panelNumber = index + 1;
            byte[] cellBytes = cells[index];
            int defaultLevel = (int)Math.Ceiling(panelNumber * ExpEngine.MaxLevel / 20d);
            var selected = new CheckBox { IsChecked = true };
            var level = (new Entry
            {
                Text = defaultLevel.ToString(),
                Keyboard = Keyboard.Numeric,
                WidthRequest = 70
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground)
              .ThemeColor(Entry.TextColorProperty, ThemeKey.InputText)
              .ThemeColor(Entry.PlaceholderColorProperty, ThemeKey.CardText3);
            _rows.Add(new PreviewRow(panelNumber, cellBytes, selected, level));

            var selectionRow = new HorizontalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    selected,
                    CardLabel("Use panel", ThemeKey.CardText2, 12)
                }
            };
            var levelRow = new HorizontalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    CardLabel("Level", ThemeKey.CardText2, 12),
                    level
                }
            };
            var card = (new Frame
            {
                WidthRequest = 210,
                Padding = 10,
                HasShadow = false,
                Content = new VerticalStackLayout
                {
                    Spacing = 5,
                    Children =
                    {
                        CardLabel($"Panel {panelNumber}", ThemeKey.CardText1, 15, bold: true),
                        new Image
                        {
                            Source = ImageSource.FromStream(() => new MemoryStream(cellBytes, writable: false)),
                            Aspect = Aspect.AspectFit,
                            HeightRequest = 230
                        },
                        selectionRow,
                        levelRow
                    }
                }
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground)
              .ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
            panels.Children.Add(card);
        }

        var save = ActionButton("Save selected panels", ThemeKey.SuccessButtonBackground, ThemeKey.ButtonPrimaryText);
        save.Clicked += async (_, _) => await SaveAsync();
        var cancel = ActionButton("Cancel", ThemeKey.NeutralButtonBackground, ThemeKey.NeutralButtonText);
        cancel.Clicked += async (_, _) => await CloseAsync(null);

        var layout = new Grid
        {
            Padding = 16,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        layout.Add(PageLabel("Review panel assignments", ThemeKey.PageText1, 22, bold: true), 0, 0);
        layout.Add(new ScrollView { Content = panels }, 0, 1);
        layout.Add(new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Children = { save, cancel }
        }, 0, 2);
        Content = layout;
    }

    protected override void OnDisappearing()
    {
        if (!_closing) _completion.TrySetResult(null);
        base.OnDisappearing();
    }

    private async Task SaveAsync()
    {
        var assignments = new List<CompanionLevelImageAssignment>();
        foreach (var row in _rows.Where(x => x.Selected.IsChecked))
        {
            string value = (row.Level.Text ?? "").ReplaceLineEndings("\n").Trim();
            if (!int.TryParse(value, out int level) || level is < 1 or > ExpEngine.MaxLevel)
            {
                await DisplayAlert(
                    "Invalid Level",
                    $"Panel {row.PanelNumber} needs a whole-number level from 1 to {ExpEngine.MaxLevel}.",
                    "OK");
                return;
            }
            assignments.Add(new CompanionLevelImageAssignment(row.PanelNumber, level, row.ImageBytes));
        }
        var duplicate = assignments.GroupBy(x => x.Level).FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            await DisplayAlert("Duplicate Level", $"Only one selected panel can use level {duplicate.Key}.", "OK");
            return;
        }
        await CloseAsync(assignments);
    }

    private async Task CloseAsync(IReadOnlyList<CompanionLevelImageAssignment>? result)
    {
        if (_closing) return;
        _closing = true;
        await Navigation.PopModalAsync();
        _completion.TrySetResult(result);
    }

    public static async Task<IReadOnlyList<CompanionLevelImageAssignment>?> ShowAsync(
        INavigation navigation,
        IReadOnlyList<byte[]> cells)
    {
        var page = new CompanionConceptSheetPreviewPage(cells);
        await navigation.PushModalAsync(page);
        return await page._completion.Task;
    }

    private static Label PageLabel(string text, ThemeKey color, double size = 14, bool bold = false) =>
        (new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
        }).ThemeColor(Label.TextColorProperty, color);

    private static Label CardLabel(string text, ThemeKey color, double size = 14, bool bold = false) =>
        (new Label
        {
            Text = text,
            FontSize = size,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
        }).ThemeColor(Label.TextColorProperty, color);

    private static Button ActionButton(string text, ThemeKey background, ThemeKey foreground) =>
        (new Button { Text = text, Margin = 3 })
            .ThemeColor(VisualElement.BackgroundColorProperty, background)
            .ThemeColor(Button.TextColorProperty, foreground);
}

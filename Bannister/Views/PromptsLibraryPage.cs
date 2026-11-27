using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class PromptsLibraryPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly PromptLibraryService _libraryService;
    private VerticalStackLayout _categoriesStack = null!;
    private Grid _root = null!;
    private readonly HashSet<int> _expandedCategoryIds = new();
    private readonly HashSet<int> _expandedPromptIds = new();

    public PromptsLibraryPage(AuthService auth, PromptLibraryService libraryService)
    {
        _auth = auth;
        _libraryService = libraryService;

        Title = "Prompts Library";


        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        _root = (new Grid
        {
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        var stack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 14
        };

        stack.Children.Add((new Label
        {
            Text = " Prompts Library",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightTealText, ThemeKey.PageText1, classifyPageText: true));

        stack.Children.Add((new Label
        {
            Text = "Organize prompts into categories for quick reuse.",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        var newCategoryButton = (new Button
        {
            Text = "+ New Category",
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        newCategoryButton.Clicked += async (_, _) => await AddCategoryAsync();
        stack.Children.Add(newCategoryButton);

        _categoriesStack = new VerticalStackLayout
        {
            Spacing = 12
        };
        stack.Children.Add(_categoriesStack);

        _root.Children.Add(new ScrollView { Content = stack });
        Content = _root;
    }

    private async Task LoadAsync()
    {
        _categoriesStack.Children.Clear();

        var username = _auth.CurrentUsername;
        var categories = await _libraryService.GetCategoriesAsync(username);

        if (categories.Count == 0)
        {
            _categoriesStack.Children.Add((new Label
            {
                Text = "No categories yet. Tap + New Category to start.",
                FontSize = 14,
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 40, 0, 0)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
            return;
        }

        foreach (var category in categories)
        {
            var prompts = await _libraryService.GetPromptsForCategoryAsync(category.Id);
            _categoriesStack.Children.Add(BuildCategoryCard(category, prompts, categories));
        }
    }

    private Frame BuildCategoryCard(
        PromptLibraryCategory category,
        List<PromptLibraryPrompt> prompts,
        List<PromptLibraryCategory> allCategories)
    {
        var expanded = _expandedCategoryIds.Contains(category.Id);

        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var titleStack = new VerticalStackLayout { Spacing = 2 };
        titleStack.Children.Add((new Label
        {
            Text = category.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightTealText, ThemeKey.PageText1, classifyPageText: true));
        titleStack.Children.Add((new Label
        {
            Text = $"{prompts.Count} prompt{(prompts.Count == 1 ? "" : "s")}",
            FontSize = 11
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        headerGrid.Add(titleStack, 0, 0);

        var actions = new HorizontalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center
        };
        actions.Children.Add(SmallButton("✏️", async () => await RenameCategoryAsync(category)));
        actions.Children.Add(SmallButton("⬆️", async () => await MoveCategoryAsync(category, up: true)));
        actions.Children.Add(SmallButton("⬇️", async () => await MoveCategoryAsync(category, up: false)));
        actions.Children.Add(SmallButton("🗑️", async () => await DeleteCategoryAsync(category)));
        headerGrid.Add(actions, 1, 0);

        var chevron = (new Label
        {
            Text = expanded ? "▼" : "▶",
            FontSize = 16,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
        headerGrid.Add(chevron, 2, 0);

        var contentStack = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = expanded,
            Margin = new Thickness(0, 10, 0, 0)
        };

        if (prompts.Count == 0)
        {
            contentStack.Children.Add((new Label
            {
                Text = "No prompts in this category yet.",
                FontSize = 12,
                FontAttributes = FontAttributes.Italic
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        }
        else
        {
            foreach (var prompt in prompts)
            {
                contentStack.Children.Add(BuildPromptCard(prompt, allCategories));
            }
        }

        var addPromptButton = (new Button
        {
            Text = "+ Add prompt",
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightTeal).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightTealText);
        addPromptButton.Clicked += async (_, _) => await ShowPromptEditorAsync(category, null);
        contentStack.Children.Add(addPromptButton);

        var outer = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { headerGrid, contentStack }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (_expandedCategoryIds.Contains(category.Id))
                _expandedCategoryIds.Remove(category.Id);
            else
                _expandedCategoryIds.Add(category.Id);
            await LoadAsync();
        };
        headerGrid.GestureRecognizers.Add(tap);

        return (new Frame
        {
            CornerRadius = 12,
            Padding = 14,
            HasShadow = false,
            Content = outer
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.HighlightTeal);
    }

    private Frame BuildPromptCard(PromptLibraryPrompt prompt, List<PromptLibraryCategory> allCategories)
    {
        var isBodyExpanded = _expandedPromptIds.Contains(prompt.Id);

        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add((new Label
        {
            Text = prompt.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        var bodyRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };

        var bodyLabel = (new Label
        {
            Text = prompt.Body,
            FontSize = 13,
            FontAttributes = FontAttributes.Italic,
            LineBreakMode = isBodyExpanded ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation,
            MaxLines = isBodyExpanded ? int.MaxValue : 2
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);

        void ToggleBodyExpansion()
        {
            if (_expandedPromptIds.Contains(prompt.Id))
                _expandedPromptIds.Remove(prompt.Id);
            else
                _expandedPromptIds.Add(prompt.Id);
            _ = LoadAsync();
        }

        var bodyTap = new TapGestureRecognizer();
        bodyTap.Tapped += (_, _) => ToggleBodyExpansion();
        bodyLabel.GestureRecognizers.Add(bodyTap);

        var rowTap = new TapGestureRecognizer();
        rowTap.Tapped += (_, _) => ToggleBodyExpansion();
        bodyRow.GestureRecognizers.Add(rowTap);

        bodyRow.Add(bodyLabel, 0, 0);
        bodyRow.Add((new Label
        {
            Text = isBodyExpanded ? "▲" : "▼",
            FontSize = 12,
            VerticalOptions = LayoutOptions.Start
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true), 1, 0);
        stack.Children.Add(bodyRow);

        var total = prompt.SuccessCount + prompt.FailureCount;
        var percent = total > 0 ? (int)Math.Round(prompt.SuccessCount * 100.0 / total) : 0;
        var statsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            Padding = new Thickness(0, 4)
        };

        statsGrid.Add(BuildStatCell("Success", prompt.SuccessCount.ToString(), ThemeKey.SuccessText), 0, 0);
        statsGrid.Add(BuildStatCell("Failure", prompt.FailureCount.ToString(), ThemeKey.ErrorText), 1, 0);
        statsGrid.Add(BuildStatCell("Total", total.ToString(), ThemeKey.CardText1), 2, 0);
        statsGrid.Add(BuildStatCell("% Success", total > 0 ? $"{percent}%" : "—", ThemeKey.InfoText), 3, 0);
        stack.Children.Add(statsGrid);

        var statsButtons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };

        var successButton = (new Button
        {
            Text = "✓ +1 Success",
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessBackground).ThemeColor(Button.TextColorProperty, ThemeKey.SuccessText);
        successButton.Clicked += async (_, _) =>
        {
            await _libraryService.IncrementSuccessAsync(prompt.Id);
            await LoadAsync();
        };

        var failureButton = (new Button
        {
            Text = "✗ +1 Failure",
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        failureButton.Clicked += async (_, _) =>
        {
            await _libraryService.IncrementFailureAsync(prompt.Id);
            await LoadAsync();
        };

        statsButtons.Add(successButton, 0, 0);
        statsButtons.Add(failureButton, 1, 0);
        stack.Children.Add(statsButtons);

        var actions = new HorizontalStackLayout { Spacing = 6 };
        var copyButton = ActionButton("Copy", ThemeKey.ButtonPrimaryBackground);
        copyButton.Clicked += async (_, _) => await CopyPromptAsync(prompt, copyButton);
        actions.Children.Add(copyButton);

        var editButton = ActionButton("Edit", ThemeKey.HighlightVioletText);
        editButton.Clicked += async (_, _) => await ShowPromptEditorAsync(null, prompt);
        actions.Children.Add(editButton);

        var upButton = ActionButton("⬆️", ThemeKey.NeutralDarkButton);
        upButton.Clicked += async (_, _) => await MovePromptAsync(prompt, up: true);
        actions.Children.Add(upButton);

        var downButton = ActionButton("⬇️", ThemeKey.NeutralDarkButton);
        downButton.Clicked += async (_, _) => await MovePromptAsync(prompt, up: false);
        actions.Children.Add(downButton);

        var moveButton = ActionButton("Move to...", ThemeKey.HighlightTealText);
        moveButton.Clicked += async (_, _) => await MovePromptToCategoryAsync(prompt, allCategories);
        actions.Children.Add(moveButton);

        var statsButton = ActionButton("Edit stats", ThemeKey.NeutralDarkButton);
        statsButton.Clicked += async (_, _) => await ShowStatsEditorAsync(prompt);
        actions.Children.Add(statsButton);

        var deleteButton = ActionButton("Delete", ThemeKey.ErrorButtonBackground);
        deleteButton.Clicked += async (_, _) => await DeletePromptAsync(prompt);
        actions.Children.Add(deleteButton);

        stack.Children.Add(actions);

        return (new Frame
        {
            CornerRadius = 8,
            Padding = 10,
            HasShadow = false,
            Content = stack
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
    }

    private Button SmallButton(string text, Func<Task> onClick)
    {
        var button = (new Button
        {
            Text = text,
            WidthRequest = 34,
            HeightRequest = 30,
            Padding = 0,
            FontSize = 11,
            CornerRadius = 6
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        button.Clicked += async (_, _) => await onClick();
        return button;
    }

    private Button ActionButton(string text, ThemeKey color)
    {
        return (new Button
        {
            Text = text,
            CornerRadius = 6,
            HeightRequest = 32,
            FontSize = 11,
            Padding = new Thickness(10, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, color).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
    }

    private static View BuildStatCell(string caption, string value, ThemeKey valueColor)
    {
        return new VerticalStackLayout
        {
            Spacing = 2,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                (new Label
                {
                    Text = value,
                    FontSize = 15,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center
                }).ThemeColor(Label.TextColorProperty, valueColor, ThemeKey.PageText1, classifyPageText: true),
                (new Label
                {
                    Text = caption,
                    FontSize = 10,
                    HorizontalOptions = LayoutOptions.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true)
            }
        };
    }

    private async Task AddCategoryAsync()
    {
        var name = await DisplayPromptAsync("New Category", "Enter category name:", "Create", "Cancel");
        if (string.IsNullOrWhiteSpace(name)) return;
        await _libraryService.CreateCategoryAsync(_auth.CurrentUsername, name);
        await LoadAsync();
    }

    private async Task RenameCategoryAsync(PromptLibraryCategory category)
    {
        var name = await DisplayPromptAsync("Rename Category", "Enter category name:", "Save", "Cancel", initialValue: category.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        await _libraryService.RenameCategoryAsync(category.Id, name);
        await LoadAsync();
    }

    private async Task MoveCategoryAsync(PromptLibraryCategory category, bool up)
    {
        if (up)
            await _libraryService.MoveCategoryUpAsync(category.Id, _auth.CurrentUsername);
        else
            await _libraryService.MoveCategoryDownAsync(category.Id, _auth.CurrentUsername);
        await LoadAsync();
    }

    private async Task DeleteCategoryAsync(PromptLibraryCategory category)
    {
        var count = await _libraryService.GetPromptCountForCategoryAsync(category.Id);
        if (count == 0)
        {
            var ok = await DisplayAlert("Delete category?", $"Delete category \"{category.Name}\"?", "Delete", "Cancel");
            if (!ok) return;
            await _libraryService.DeleteCategoryAsync(category.Id);
        }
        else
        {
            var choice = await DisplayActionSheet(
                $"Category has {count} prompt{(count == 1 ? "" : "s")}.",
                "Cancel",
                null,
                "Delete category and all prompts");
            if (choice != "Delete category and all prompts") return;
            await _libraryService.DeleteCategoryAndPromptsAsync(category.Id);
        }

        _expandedCategoryIds.Remove(category.Id);
        await LoadAsync();
    }

    private async Task CopyPromptAsync(PromptLibraryPrompt prompt, Button button)
    {
        await Clipboard.SetTextAsync(prompt.Body);
        var original = button.Text;
        button.Text = "Copied!";
        await Task.Delay(800);
        button.Text = original;
    }

    private async Task MovePromptAsync(PromptLibraryPrompt prompt, bool up)
    {
        if (up)
            await _libraryService.MovePromptUpAsync(prompt.Id, prompt.CategoryId);
        else
            await _libraryService.MovePromptDownAsync(prompt.Id, prompt.CategoryId);
        await LoadAsync();
    }

    private async Task MovePromptToCategoryAsync(PromptLibraryPrompt prompt, List<PromptLibraryCategory> allCategories)
    {
        var targets = allCategories
            .Where(c => c.Id != prompt.CategoryId)
            .ToList();
        if (targets.Count == 0)
        {
            await DisplayAlert("No other categories", "Create another category first.", "OK");
            return;
        }

        var options = targets.Select(c => c.Name).ToArray();
        var choice = await DisplayActionSheet("Move prompt to category", "Cancel", null, options);
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

        var target = targets.FirstOrDefault(c => c.Name == choice);
        if (target == null) return;

        await _libraryService.MovePromptToCategoryAsync(prompt.Id, target.Id);
        _expandedCategoryIds.Add(target.Id);
        await LoadAsync();
    }

    private async Task DeletePromptAsync(PromptLibraryPrompt prompt)
    {
        var ok = await DisplayAlert("Delete prompt?", $"Delete \"{prompt.Title}\"?", "Delete", "Cancel");
        if (!ok) return;
        await _libraryService.DeletePromptAsync(prompt.Id);
        await LoadAsync();
    }

    private async Task ShowPromptEditorAsync(PromptLibraryCategory? category, PromptLibraryPrompt? prompt)
    {
        var overlay = (new Grid
        {
            InputTransparent = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var titleEntry = (new Entry
        {
            Placeholder = "Prompt title",
            Text = prompt?.Title ?? ""
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

        var bodyEditor = (new Editor
        {
            Placeholder = "Prompt body",
            Text = prompt?.Body ?? "",
            HeightRequest = 220,
            AutoSize = EditorAutoSizeOption.Disabled
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

        var saveButton = (new Button
        {
            Text = "Save",
            CornerRadius = 8,
            HeightRequest = 40
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        var cancelButton = (new Button
        {
            Text = "Cancel",
            CornerRadius = 8,
            HeightRequest = 40
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        actions.Add(cancelButton, 0, 0);
        actions.Add(saveButton, 1, 0);

        var card = (new Frame
        {
            Padding = 18,
            CornerRadius = 12,
            HasShadow = true,
            WidthRequest = 560,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    (new Label
                    {
                        Text = prompt == null ? "Add Prompt" : "Edit Prompt",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightTealText, ThemeKey.PageText1, classifyPageText: true),
                    titleEntry,
                    bodyEditor,
                    actions
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        overlay.Children.Add(card);
        _root.Children.Add(overlay);

        var tcs = new TaskCompletionSource<bool>();

        cancelButton.Clicked += (_, _) =>
        {
            _root.Children.Remove(overlay);
            tcs.TrySetResult(false);
        };

        saveButton.Clicked += async (_, _) =>
        {
            var title = titleEntry.Text?.Trim() ?? "";
            var body = bodyEditor.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            {
                await DisplayAlert("Missing fields", "Title and body are required.", "OK");
                return;
            }

            if (prompt == null)
            {
                if (category == null) return;
                await _libraryService.CreatePromptAsync(_auth.CurrentUsername, category.Id, title, body);
                _expandedCategoryIds.Add(category.Id);
            }
            else
            {
                await _libraryService.UpdatePromptAsync(prompt.Id, title, body);
            }

            _root.Children.Remove(overlay);
            tcs.TrySetResult(true);
            await LoadAsync();
        };

        await tcs.Task;
    }

    private async Task ShowStatsEditorAsync(PromptLibraryPrompt prompt)
    {
        var overlay = (new Grid
        {
            InputTransparent = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var successEntry = (new Entry
        {
            Placeholder = "Success count",
            Text = prompt.SuccessCount.ToString(),
            Keyboard = Keyboard.Numeric
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

        var failureEntry = (new Entry
        {
            Placeholder = "Failure count",
            Text = prompt.FailureCount.ToString(),
            Keyboard = Keyboard.Numeric
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

        var saveButton = (new Button
        {
            Text = "Save",
            CornerRadius = 8,
            HeightRequest = 40
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        var cancelButton = (new Button
        {
            Text = "Cancel",
            CornerRadius = 8,
            HeightRequest = 40
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        actions.Add(cancelButton, 0, 0);
        actions.Add(saveButton, 1, 0);

        var card = (new Frame
        {
            Padding = 18,
            CornerRadius = 12,
            HasShadow = true,
            WidthRequest = 460,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    (new Label
                    {
                        Text = $"Edit stats for: {prompt.Title}",
                        FontSize = 16,
                        FontAttributes = FontAttributes.Bold,
                        LineBreakMode = LineBreakMode.TailTruncation,
                        MaxLines = 1
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                    (new Label { Text = "Success", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true),
                    successEntry,
                    (new Label { Text = "Failure", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true),
                    failureEntry,
                    actions
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        overlay.Children.Add(card);
        _root.Children.Add(overlay);

        var tcs = new TaskCompletionSource<bool>();

        cancelButton.Clicked += (_, _) =>
        {
            _root.Children.Remove(overlay);
            tcs.TrySetResult(false);
        };

        saveButton.Clicked += async (_, _) =>
        {
            if (!int.TryParse(successEntry.Text, out var successCount) ||
                !int.TryParse(failureEntry.Text, out var failureCount))
            {
                await DisplayAlert("Invalid stats", "Enter whole numbers for both Success and Failure.", "OK");
                return;
            }

            await _libraryService.SetStatsAsync(prompt.Id, Math.Max(0, successCount), Math.Max(0, failureCount));
            _root.Children.Remove(overlay);
            tcs.TrySetResult(true);
            await LoadAsync();
        };

        await tcs.Task;
    }
}

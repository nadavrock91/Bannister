using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public static class TaskCreationHelper
{
    public static async Task<TaskItem?> ShowCreateTaskAsync(
        ContentPage page,
        AuthService auth,
        TaskService tasks,
        IdeasService? ideasService,
        string? fixedCategory = null,
        string? excludedCategory = null,
        bool markAsTopCandidate = false)
    {
        List<string> allCategories;
        if (!string.IsNullOrWhiteSpace(fixedCategory))
        {
            allCategories = new List<string> { fixedCategory };
        }
        else
        {
            var allTasks = await tasks.GetActiveTasksAsync(auth.CurrentUsername);
            allCategories = allTasks
                .Select(t => t.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(excludedCategory))
                allCategories = allCategories
                    .Where(c => !string.Equals(c, excludedCategory, StringComparison.OrdinalIgnoreCase))
                    .ToList();
        }

        var tcs = new TaskCompletionSource<(
            string? text,
            string? category,
            string? impact,
            bool isPrivate)?>();
        var overlay = (new Grid { }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var editor = (new Editor
        {
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = 120,
            Placeholder = "Describe the task...",
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3);

        var impactEntry = (new Entry
        {
            Placeholder = "Impact (public summary)",
            FontSize = 13
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3);

        var isPrivateCheck = new CheckBox { IsChecked = false };
        var isPrivateRow = new HorizontalStackLayout
        {
            Spacing = 6,
            Children =
            {
                isPrivateCheck,
                (new Label
                {
                    Text = "Private (hide title in public mode)",
                    FontSize = 12,
                    VerticalOptions = LayoutOptions.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true)
            }
        };

        string selectedCategory = fixedCategory ?? "";
        var categoryLabel = (new Label
        {
            Text = string.IsNullOrWhiteSpace(fixedCategory)
                ? "Category: (select below or on submit)"
                : $"Category: {fixedCategory}",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true);

        View categorySection;
        Entry? categorySearch = null;
        VerticalStackLayout? categoryDropdown = null;

        if (string.IsNullOrWhiteSpace(fixedCategory))
        {
            categorySearch = (new Entry
            {
                Placeholder = "Search categories...",
                FontSize = 12,
                HeightRequest = 34
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3);

            categoryDropdown = new VerticalStackLayout { Spacing = 2, MaximumHeightRequest = 120 };

            void RebuildCategoryDropdown(string search)
            {
                categoryDropdown.Children.Clear();
                var filtered = string.IsNullOrWhiteSpace(search)
                    ? allCategories
                    : allCategories.Where(c => c.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var cat in filtered)
                {
                    var catBtn = (new Button
                    {
                        Text = cat == selectedCategory ? $"\u2713 {cat}" : cat,
                        CornerRadius = 4,
                        HeightRequest = 30,
                        FontSize = 11,
                        Padding = new Thickness(8, 0),
                        HorizontalOptions = LayoutOptions.Fill
                    }).ThemeColor(VisualElement.BackgroundColorProperty, cat == selectedCategory ? ThemeKey.HighlightIndigo : ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, cat == selectedCategory ? ThemeKey.HighlightVioletText : ThemeKey.CardText1);
                    var capturedCat = cat;
                    catBtn.Clicked += (_, _) =>
                    {
                        selectedCategory = capturedCat;
                        categoryLabel.Text = $"Category: {capturedCat}";
                        RebuildCategoryDropdown(categorySearch?.Text ?? "");
                    };
                    categoryDropdown.Children.Add(catBtn);
                }

                var newCatBtn = (new Button
                {
                    Text = "+ New Category",
                    CornerRadius = 4,
                    HeightRequest = 30,
                    FontSize = 11,
                    Padding = new Thickness(8, 0),
                    HorizontalOptions = LayoutOptions.Fill
                }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
                newCatBtn.Clicked += async (_, _) =>
                {
                    var newName = await page.DisplayPromptAsync("New Category", "Category name:", "Create", "Cancel", maxLength: 100);
                    if (!string.IsNullOrWhiteSpace(newName))
                    {
                        var candidate = newName.Trim();
                        if (!string.IsNullOrWhiteSpace(excludedCategory) &&
                            string.Equals(candidate, excludedCategory, StringComparison.OrdinalIgnoreCase))
                        {
                            await page.DisplayAlert("Focus Category", "This category is excluded here.", "OK");
                            return;
                        }

                        selectedCategory = candidate;
                        categoryLabel.Text = $"Category: {selectedCategory}";
                        if (!allCategories.Contains(selectedCategory, StringComparer.OrdinalIgnoreCase))
                            allCategories.Add(selectedCategory);
                        allCategories = allCategories.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
                        RebuildCategoryDropdown(categorySearch?.Text ?? "");
                    }
                };
                categoryDropdown.Children.Add(newCatBtn);
            }

            categorySearch.TextChanged += (_, e) => RebuildCategoryDropdown(e.NewTextValue ?? "");
            RebuildCategoryDropdown("");

            categorySection = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    categoryLabel,
                    categorySearch,
                    new ScrollView { MaximumHeightRequest = 120, Content = categoryDropdown }
                }
            };
        }
        else
        {
            categorySection = categoryLabel;
        }

        var createBtn = (new Button
        {
            Text = "Create",
            CornerRadius = 8
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var cancelBtn = (new Button
        {
            Text = "Cancel",
            CornerRadius = 8
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        Grid rootGrid;
        View? originalContent = null;
        if (page.Content is Grid existingGrid)
        {
            rootGrid = existingGrid;
        }
        else
        {
            originalContent = page.Content;
            rootGrid = new Grid();
            if (originalContent != null)
            {
                page.Content = null;
                rootGrid.Children.Add(originalContent);
            }
            page.Content = rootGrid;
        }

        void CloseOverlay((
            string? text,
            string? category,
            string? impact,
            bool isPrivate)? result)
        {
            rootGrid.Children.Remove(overlay);
            tcs.TrySetResult(result);
        }

        createBtn.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(selectedCategory))
            {
                var cats = new List<string>(allCategories) { "+ New Category" };
                var picked = await page.DisplayActionSheet("Pick a category", "Cancel", null, cats.ToArray());
                if (string.IsNullOrEmpty(picked) || picked == "Cancel") return;

                if (picked == "+ New Category")
                {
                    var newName = await page.DisplayPromptAsync("New Category", "Category name:", "Create", "Cancel", maxLength: 100);
                    if (string.IsNullOrWhiteSpace(newName)) return;
                    selectedCategory = newName.Trim();
                    if (!string.IsNullOrWhiteSpace(excludedCategory) &&
                        string.Equals(selectedCategory, excludedCategory, StringComparison.OrdinalIgnoreCase))
                    {
                        await page.DisplayAlert("Focus Category", "This category is excluded here.", "OK");
                        selectedCategory = "";
                        return;
                    }
                }
                else
                {
                    selectedCategory = picked;
                }
            }

            CloseOverlay((
                editor.Text,
                selectedCategory,
                impactEntry.Text,
                isPrivateCheck.IsChecked));
        };
        cancelBtn.Clicked += (_, _) => CloseOverlay(null);

        var card = (new Frame
        {
            CornerRadius = 12,
            Padding = 20,
            MaximumWidthRequest = 600,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(16, 0),
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    (new Label
                    {
                        Text = markAsTopCandidate ? "New Top Candidate" : "New Task",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true),
                    editor,
                    (new Label
                    {
                        Text = "Impact (public summary)",
                        FontSize = 12
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true),
                    impactEntry,
                    isPrivateRow,
                    categorySection,
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        HorizontalOptions = LayoutOptions.End,
                        Children = { cancelBtn, createBtn }
                    }
                }
            }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        overlay.Children.Add(card);
        rootGrid.Children.Add(overlay);

        var result = await tcs.Task;
        if (originalContent != null && ReferenceEquals(page.Content, rootGrid))
        {
            rootGrid.Children.Remove(originalContent);
            page.Content = null;
            page.Content = originalContent;
        }

        if (result == null || string.IsNullOrWhiteSpace(result.Value.text)) return null;
        string title = result.Value.text.Trim();
        string category = result.Value.category ?? "General";
        string impact = result.Value.impact?.Trim() ?? "";
        bool isPrivate = result.Value.isPrivate;

        string? priorityInput = await page.DisplayPromptAsync(
            "Priority",
            "Enter priority (higher number = higher priority, 0 = lowest):",
            "Set", "Cancel",
            initialValue: "0",
            keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(priorityInput))
            return null;
        if (!int.TryParse(priorityInput.Trim(), out int priority)
            || priority < 0)
            priority = 0;

        var newTask = await tasks.CreateTaskAsync(
            auth.CurrentUsername,
            title,
            category,
            priority,
            impact: impact,
            isPrivate: isPrivate);
        if (markAsTopCandidate)
        {
            newTask.IsTopCandidate = true;
            await tasks.UpdateTaskAsync(newTask);
        }

        var ideaLogger = Application.Current?.Handler?.MauiContext?.Services.GetService<IdeaLoggerService>();
        if (ideaLogger != null)
            await ideaLogger.LogSilentlyAsync(new IdeaLogRequest
            {
                Username = auth.CurrentUsername,
                Title = title,
                Body = title,
                Category = "all_tasks"
            });

        return newTask;
    }
}

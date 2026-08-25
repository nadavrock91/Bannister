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

        var tcs = new TaskCompletionSource<(string? text, string? category)?>();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };

        var editor = new Editor
        {
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = 120,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888"),
            Placeholder = "Describe the task...",
            FontSize = 13
        };

        string selectedCategory = fixedCategory ?? "";
        var categoryLabel = new Label
        {
            Text = string.IsNullOrWhiteSpace(fixedCategory)
                ? "Category: (select below or on submit)"
                : $"Category: {fixedCategory}",
            FontSize = 12,
            TextColor = Color.FromArgb("#888")
        };

        View categorySection;
        Entry? categorySearch = null;
        VerticalStackLayout? categoryDropdown = null;

        if (string.IsNullOrWhiteSpace(fixedCategory))
        {
            categorySearch = new Entry
            {
                Placeholder = "Search categories...",
                FontSize = 12,
                BackgroundColor = Color.FromArgb("#FAFAFA"),
                TextColor = Color.FromArgb("#333"),
                PlaceholderColor = Color.FromArgb("#999"),
                HeightRequest = 34
            };

            categoryDropdown = new VerticalStackLayout { Spacing = 2, MaximumHeightRequest = 120 };

            void RebuildCategoryDropdown(string search)
            {
                categoryDropdown.Children.Clear();
                var filtered = string.IsNullOrWhiteSpace(search)
                    ? allCategories
                    : allCategories.Where(c => c.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var cat in filtered)
                {
                    var catBtn = new Button
                    {
                        Text = cat == selectedCategory ? $"\u2713 {cat}" : cat,
                        BackgroundColor = cat == selectedCategory ? Color.FromArgb("#E8EAF6") : Color.FromArgb("#F5F5F5"),
                        TextColor = cat == selectedCategory ? Color.FromArgb("#7B1FA2") : Color.FromArgb("#333"),
                        CornerRadius = 4,
                        HeightRequest = 30,
                        FontSize = 11,
                        Padding = new Thickness(8, 0),
                        HorizontalOptions = LayoutOptions.Fill
                    };
                    var capturedCat = cat;
                    catBtn.Clicked += (_, _) =>
                    {
                        selectedCategory = capturedCat;
                        categoryLabel.Text = $"Category: {capturedCat}";
                        RebuildCategoryDropdown(categorySearch?.Text ?? "");
                    };
                    categoryDropdown.Children.Add(catBtn);
                }

                var newCatBtn = new Button
                {
                    Text = "+ New Category",
                    BackgroundColor = Color.FromArgb("#FFF3E0"),
                    TextColor = Color.FromArgb("#E65100"),
                    CornerRadius = 4,
                    HeightRequest = 30,
                    FontSize = 11,
                    Padding = new Thickness(8, 0),
                    HorizontalOptions = LayoutOptions.Fill
                };
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

        var createBtn = new Button
        {
            Text = "Create",
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 8
        };

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

        void CloseOverlay((string? text, string? category)? result)
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

            CloseOverlay((editor.Text, selectedCategory));
        };
        cancelBtn.Clicked += (_, _) => CloseOverlay(null);

        var card = new Frame
        {
            BackgroundColor = Colors.White,
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
                    new Label
                    {
                        Text = markAsTopCandidate ? "New Top Candidate" : "New Task",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#7B1FA2")
                    },
                    editor,
                    categorySection,
                    new HorizontalStackLayout
                    {
                        Spacing = 8,
                        HorizontalOptions = LayoutOptions.End,
                        Children = { cancelBtn, createBtn }
                    }
                }
            }
        };

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

        var priorityChoice = await page.DisplayActionSheet("Priority", "Cancel", null,
            "\U0001F534 High", "\U0001F7E1 Medium", "\U0001F7E2 Low");
        if (priorityChoice == "Cancel" || string.IsNullOrEmpty(priorityChoice)) return null;
        int priority = priorityChoice.Contains("High") ? 1 : priorityChoice.Contains("Low") ? 3 : 2;

        var newTask = await tasks.CreateTaskAsync(auth.CurrentUsername, title, category, priority);
        if (markAsTopCandidate)
        {
            newTask.IsTopCandidate = true;
            await tasks.UpdateTaskAsync(newTask);
        }

        if (ideasService != null)
        {
            try { await ideasService.CreateIdeaAsync(auth.CurrentUsername, title, "all_tasks"); }
            catch { }
        }

        return newTask;
    }
}

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
        bool markAsTopCandidate = false,
        string? excludedCategory = null)
    {
        var tcs = new TaskCompletionSource<string?>();
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

        var createBtn = new Button
        {
            Text = "Next",
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

        void CloseOverlay(string? result)
        {
            rootGrid.Children.Remove(overlay);
            tcs.TrySetResult(result);
        }

        createBtn.Clicked += (_, _) => CloseOverlay(editor.Text);
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
                    new Label
                    {
                        Text = "Describe what needs to be done:",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666")
                    },
                    editor,
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

        var title = await tcs.Task;
        if (originalContent != null && ReferenceEquals(page.Content, rootGrid))
        {
            rootGrid.Children.Remove(originalContent);
            page.Content = null;
            page.Content = originalContent;
        }
        if (string.IsNullOrWhiteSpace(title)) return null;

        string category;
        if (!string.IsNullOrWhiteSpace(fixedCategory))
        {
            category = fixedCategory;
        }
        else
        {
            var allTasks = await tasks.GetActiveTasksAsync(auth.CurrentUsername);
            var categories = allTasks
                .Select(t => t.Category)
                .Where(c => !string.IsNullOrWhiteSpace(c) &&
                    (string.IsNullOrWhiteSpace(excludedCategory) ||
                     !string.Equals(c, excludedCategory, StringComparison.OrdinalIgnoreCase)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            categories.Add("+ New Category");
            var selected = await page.DisplayActionSheet("Category", "Cancel", null, categories.ToArray());
            if (string.IsNullOrEmpty(selected) || selected == "Cancel") return null;

            if (selected == "+ New Category")
            {
                var newCat = await page.DisplayPromptAsync("New Category", "Category name:", "Create", "Cancel", maxLength: 100);
                if (string.IsNullOrWhiteSpace(newCat)) return null;
                category = newCat.Trim();
                if (!string.IsNullOrWhiteSpace(excludedCategory) &&
                    string.Equals(category, excludedCategory, StringComparison.OrdinalIgnoreCase))
                {
                    await page.DisplayAlert("Focus Category", "Free tasks must use a non-focus category.", "OK");
                    return null;
                }
            }
            else
            {
                category = selected;
            }
        }

        var priorityChoice = await page.DisplayActionSheet("Priority", "Cancel", null,
            "\U0001F534 High", "\U0001F7E1 Medium", "\U0001F7E2 Low");
        if (priorityChoice == "Cancel" || string.IsNullOrEmpty(priorityChoice)) return null;
        int priority = priorityChoice.Contains("High") ? 1 : priorityChoice.Contains("Low") ? 3 : 2;

        var newTask = await tasks.CreateTaskAsync(auth.CurrentUsername, title.Trim(), category, priority);
        if (markAsTopCandidate)
        {
            newTask.IsTopCandidate = true;
            await tasks.UpdateTaskAsync(newTask);
        }

        if (ideasService != null)
        {
            try { await ideasService.CreateIdeaAsync(auth.CurrentUsername, title.Trim(), "all_tasks"); }
            catch { }
        }

        return newTask;
    }
}

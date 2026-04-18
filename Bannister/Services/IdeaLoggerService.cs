using Bannister.Models;

namespace Bannister.Services;

/// <summary>
/// Reusable service for logging ideas from anywhere in the app.
/// Provides a consistent popup UI for entering idea details.
/// </summary>
public class IdeaLoggerService
{
    private readonly IdeasService _ideas;

    public IdeaLoggerService(IdeasService ideas)
    {
        _ideas = ideas;
    }

    /// <summary>
    /// Show the idea logging popup and create an idea if confirmed.
    /// </summary>
    /// <param name="page">The page to show the popup on</param>
    /// <param name="username">Current user's username</param>
    /// <param name="prefillText">Optional text to prefill the idea content</param>
    /// <param name="suggestedCategory">Optional suggested category</param>
    /// <returns>The created idea, or null if cancelled</returns>
    public async Task<IdeaItem?> LogIdeaAsync(Page page, string username, string? prefillText = null, string? suggestedCategory = null)
    {
        // Get existing categories
        var existingCategories = await _ideas.GetCategoriesAsync(username);

        // Create the popup
        var result = await ShowIdeaPopupAsync(page, existingCategories, prefillText, suggestedCategory);

        if (result == null || string.IsNullOrWhiteSpace(result.Value.text))
            return null;

        // Create and save the idea
        var idea = await _ideas.CreateIdeaAsync(username, result.Value.text.Trim(), result.Value.category.Trim());
        idea.Rating = result.Value.rating;
        await _ideas.UpdateIdeaAsync(idea);

        return idea;
    }

    /// <summary>
    /// Quick log - just asks for confirmation with prefilled data
    /// </summary>
    public async Task<IdeaItem?> QuickLogAsync(Page page, string username, string text, string category, int rating = 50)
    {
        bool confirm = await page.DisplayAlert(
            "Log to Ideas?",
            $"Save as idea?\n\n\"{(text.Length > 100 ? text.Substring(0, 100) + "..." : text)}\"\n\nCategory: {category}\nRating: {rating}",
            "Save", "Cancel");

        if (!confirm)
            return null;

        var idea = await _ideas.CreateIdeaAsync(username, text.Trim(), category.Trim());
        idea.Rating = rating;
        await _ideas.UpdateIdeaAsync(idea);

        return idea;
    }

    private async Task<(string text, string category, int rating)?> ShowIdeaPopupAsync(
        Page page, List<string> existingCategories, string? prefillText, string? suggestedCategory)
    {
        var tcs = new TaskCompletionSource<(string text, string category, int rating)?>();

        // Create popup overlay
        var popup = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 450,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HasShadow = true
        };

        var mainStack = new VerticalStackLayout { Spacing = 14 };

        // Title
        mainStack.Children.Add(new Label
        {
            Text = "💡 Log Idea",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        // Idea text input (multi-line)
        mainStack.Children.Add(new Label
        {
            Text = "Idea:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        var ideaEditor = new Editor
        {
            Text = prefillText ?? "",
            Placeholder = "What's your idea?",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            HeightRequest = 100,
            FontSize = 14
        };
        mainStack.Children.Add(ideaEditor);

        // Category section
        mainStack.Children.Add(new Label
        {
            Text = "Category:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        // Category buttons (existing categories)
        string selectedCategory = suggestedCategory ?? (existingCategories.Count > 0 ? existingCategories[0] : "General");
        Button? selectedCategoryBtn = null;

        var categoryFlow = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start
        };

        // Create category buttons
        var categoryButtons = new List<Button>();

        void UpdateCategorySelection(Button btn, string cat)
        {
            selectedCategory = cat;
            foreach (var b in categoryButtons)
            {
                b.BackgroundColor = Color.FromArgb("#E0E0E0");
                b.TextColor = Color.FromArgb("#333");
            }
            btn.BackgroundColor = Color.FromArgb("#2196F3");
            btn.TextColor = Colors.White;
            selectedCategoryBtn = btn;
        }

        foreach (var cat in existingCategories)
        {
            var catBtn = new Button
            {
                Text = cat,
                BackgroundColor = cat == selectedCategory ? Color.FromArgb("#2196F3") : Color.FromArgb("#E0E0E0"),
                TextColor = cat == selectedCategory ? Colors.White : Color.FromArgb("#333"),
                CornerRadius = 6,
                Margin = new Thickness(0, 0, 6, 6),
                Padding = new Thickness(10, 6),
                FontSize = 12,
                HeightRequest = 32
            };
            var capturedCat = cat;
            catBtn.Clicked += (s, e) => UpdateCategorySelection(catBtn, capturedCat);
            categoryButtons.Add(catBtn);
            categoryFlow.Children.Add(catBtn);

            if (cat == selectedCategory)
                selectedCategoryBtn = catBtn;
        }

        // "+ New" category button
        var newCatBtn = new Button
        {
            Text = "+ New",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 6,
            Margin = new Thickness(0, 0, 6, 6),
            Padding = new Thickness(10, 6),
            FontSize = 12,
            HeightRequest = 32
        };

        var customCategoryEntry = new Entry
        {
            Placeholder = "New category name",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            IsVisible = false,
            WidthRequest = 200
        };

        newCatBtn.Clicked += (s, e) =>
        {
            customCategoryEntry.IsVisible = !customCategoryEntry.IsVisible;
            if (customCategoryEntry.IsVisible)
            {
                customCategoryEntry.Focus();
                // Deselect other category buttons
                foreach (var b in categoryButtons)
                {
                    b.BackgroundColor = Color.FromArgb("#E0E0E0");
                    b.TextColor = Color.FromArgb("#333");
                }
                selectedCategoryBtn = null;
            }
        };

        customCategoryEntry.TextChanged += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.NewTextValue))
            {
                selectedCategory = e.NewTextValue;
            }
        };

        categoryFlow.Children.Add(newCatBtn);
        mainStack.Children.Add(categoryFlow);
        mainStack.Children.Add(customCategoryEntry);

        // Rating section
        mainStack.Children.Add(new Label
        {
            Text = "Rating:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, 6, 0, 0)
        });

        int selectedRating = 50;
        Button? selectedRatingBtn = null;

        var ratingFlow = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start
        };

        var ratingButtons = new List<Button>();
        var ratingValues = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        Color GetRatingColor(int rating, bool selected)
        {
            if (!selected)
                return Color.FromArgb("#E0E0E0");
            
            return rating >= 70 ? Color.FromArgb("#4CAF50")
                 : rating >= 40 ? Color.FromArgb("#FF9800")
                 : Color.FromArgb("#9E9E9E");
        }

        void UpdateRatingSelection(Button btn, int rating)
        {
            selectedRating = rating;
            foreach (var b in ratingButtons)
            {
                int btnRating = int.Parse(b.Text);
                b.BackgroundColor = Color.FromArgb("#E0E0E0");
                b.TextColor = Color.FromArgb("#333");
            }
            btn.BackgroundColor = GetRatingColor(rating, true);
            btn.TextColor = Colors.White;
            selectedRatingBtn = btn;
        }

        foreach (var rating in ratingValues)
        {
            var ratingBtn = new Button
            {
                Text = rating.ToString(),
                BackgroundColor = rating == selectedRating ? GetRatingColor(rating, true) : Color.FromArgb("#E0E0E0"),
                TextColor = rating == selectedRating ? Colors.White : Color.FromArgb("#333"),
                CornerRadius = 6,
                Margin = new Thickness(0, 0, 4, 4),
                Padding = new Thickness(8, 4),
                FontSize = 12,
                WidthRequest = 40,
                HeightRequest = 32
            };
            var capturedRating = rating;
            ratingBtn.Clicked += (s, e) => UpdateRatingSelection(ratingBtn, capturedRating);
            ratingButtons.Add(ratingBtn);
            ratingFlow.Children.Add(ratingBtn);

            if (rating == selectedRating)
                selectedRatingBtn = ratingBtn;
        }

        // Custom rating entry
        var customRatingEntry = new Entry
        {
            Placeholder = "Custom",
            Keyboard = Keyboard.Numeric,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            WidthRequest = 60,
            HeightRequest = 32
        };
        customRatingEntry.TextChanged += (s, e) =>
        {
            if (int.TryParse(e.NewTextValue, out int custom) && custom >= 0 && custom <= 100)
            {
                selectedRating = custom;
                // Deselect preset buttons
                foreach (var b in ratingButtons)
                {
                    b.BackgroundColor = Color.FromArgb("#E0E0E0");
                    b.TextColor = Color.FromArgb("#333");
                }
                selectedRatingBtn = null;
            }
        };
        ratingFlow.Children.Add(customRatingEntry);

        mainStack.Children.Add(ratingFlow);

        // Action buttons
        var buttonStack = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(20, 10)
        };

        var saveBtn = new Button
        {
            Text = "💾 Save Idea",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 6,
            Padding = new Thickness(20, 10),
            FontAttributes = FontAttributes.Bold
        };

        buttonStack.Children.Add(cancelBtn);
        buttonStack.Children.Add(saveBtn);
        mainStack.Children.Add(buttonStack);

        card.Content = mainStack;
        popup.Children.Add(card);

        // Add popup to page - must be ContentPage
        if (page is not ContentPage contentPage)
        {
            tcs.TrySetResult(null);
            return await tcs.Task;
        }

        if (contentPage.Content is Grid pageGrid)
        {
            // Set row span if grid has multiple rows
            if (pageGrid.RowDefinitions.Count > 0)
                Grid.SetRowSpan(popup, pageGrid.RowDefinitions.Count);

            pageGrid.Children.Add(popup);

            cancelBtn.Clicked += (s, e) =>
            {
                pageGrid.Children.Remove(popup);
                tcs.TrySetResult(null);
            };

            saveBtn.Clicked += (s, e) =>
            {
                pageGrid.Children.Remove(popup);
                tcs.TrySetResult((ideaEditor.Text ?? "", selectedCategory, selectedRating));
            };

            // Focus editor
            ideaEditor.Focus();
        }
        else
        {
            // Fallback: wrap content in grid
            var wrapperGrid = new Grid();
            if (contentPage.Content != null)
                wrapperGrid.Children.Add(contentPage.Content);
            wrapperGrid.Children.Add(popup);
            contentPage.Content = wrapperGrid;

            cancelBtn.Clicked += (s, e) =>
            {
                wrapperGrid.Children.Remove(popup);
                tcs.TrySetResult(null);
            };

            saveBtn.Clicked += (s, e) =>
            {
                wrapperGrid.Children.Remove(popup);
                tcs.TrySetResult((ideaEditor.Text ?? "", selectedCategory, selectedRating));
            };

            ideaEditor.Focus();
        }

        return await tcs.Task;
    }
}

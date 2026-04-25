using Bannister.Models;
using SQLite;

namespace Bannister.Services;

/// <summary>
/// Reusable service for logging ideas from anywhere in the app.
/// 
/// Persisted features (survive app restart):
///   - Favorite categories: manually added via ★ button, shown in a quick-select dropdown
///   - Linked categories: one-directional, when logging to A also prompts to log to B
///   - Last used category: auto-selected on next open if it's a favorite (session-level only)
/// </summary>
public class IdeaLoggerService
{
    private readonly IdeasService _ideas;
    private readonly DatabaseService _db;

    // Persisted state (loaded from DB on first use)
    private List<string> _favoriteCategories = new();
    private Dictionary<string, List<string>> _linkedCategories = new();
    private Dictionary<string, List<string>> _subcategories = new(); // category → list of subcategory names
    private bool _loaded = false;

    // Session-level only
    private string? _lastUsedCategory = null;

    public IdeaLoggerService(IdeasService ideas, DatabaseService db)
    {
        _ideas = ideas;
        _db = db;
    }

    // Legacy constructor for backward compatibility
    public IdeaLoggerService(IdeasService ideas) : this(ideas, null!) { }

    public IReadOnlyList<string> FavoriteCategories => _favoriteCategories.AsReadOnly();

    private async Task EnsureLoadedAsync(string username)
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            if (_db == null) return;
            var conn = await _db.GetConnectionAsync();
            await conn.CreateTableAsync<IdeaLoggerSetting>();

            // Load favorites
            var favRow = await conn.Table<IdeaLoggerSetting>()
                .FirstOrDefaultAsync(s => s.Username == username && s.Key == "favorites");
            if (favRow != null && !string.IsNullOrEmpty(favRow.Value))
                _favoriteCategories = favRow.Value.Split("║", StringSplitOptions.RemoveEmptyEntries).ToList();

            // Load linked categories (stored as "link:{category}" → "cat1║cat2║cat3")
            var linkRows = await conn.Table<IdeaLoggerSetting>()
                .Where(s => s.Username == username && s.Key.StartsWith("link:"))
                .ToListAsync();
            foreach (var row in linkRows)
            {
                string cat = row.Key.Substring(5); // Remove "link:" prefix
                if (!string.IsNullOrEmpty(row.Value))
                    _linkedCategories[cat] = row.Value.Split("║", StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            // Load subcategories (stored as "subcats:{category}" → "sub1║sub2║sub3")
            var subRows = await conn.Table<IdeaLoggerSetting>()
                .Where(s => s.Username == username && s.Key.StartsWith("subcats:"))
                .ToListAsync();
            foreach (var row in subRows)
            {
                string cat = row.Key.Substring(8); // Remove "subcats:" prefix
                if (!string.IsNullOrEmpty(row.Value))
                    _subcategories[cat] = row.Value.Split("║", StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IDEA LOGGER] Failed to load settings: {ex.Message}");
        }
    }

    private async Task SaveFavoritesAsync(string username)
    {
        try
        {
            if (_db == null) return;
            var conn = await _db.GetConnectionAsync();

            var existing = await conn.Table<IdeaLoggerSetting>()
                .FirstOrDefaultAsync(s => s.Username == username && s.Key == "favorites");

            string value = string.Join("║", _favoriteCategories);

            if (existing != null)
            {
                existing.Value = value;
                await conn.UpdateAsync(existing);
            }
            else
            {
                await conn.InsertAsync(new IdeaLoggerSetting { Username = username, Key = "favorites", Value = value });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IDEA LOGGER] Failed to save favorites: {ex.Message}");
        }
    }

    private async Task SaveLinkedCategoryAsync(string username, string category)
    {
        try
        {
            if (_db == null) return;
            var conn = await _db.GetConnectionAsync();
            string key = $"link:{category}";

            var existing = await conn.Table<IdeaLoggerSetting>()
                .FirstOrDefaultAsync(s => s.Username == username && s.Key == key);

            if (_linkedCategories.TryGetValue(category, out var linked) && linked.Count > 0)
            {
                string value = string.Join("║", linked);
                if (existing != null)
                {
                    existing.Value = value;
                    await conn.UpdateAsync(existing);
                }
                else
                {
                    await conn.InsertAsync(new IdeaLoggerSetting { Username = username, Key = key, Value = value });
                }
            }
            else if (existing != null)
            {
                await conn.DeleteAsync(existing);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IDEA LOGGER] Failed to save linked category: {ex.Message}");
        }
    }

    private async Task SaveSubcategoriesAsync(string username, string category)
    {
        try
        {
            if (_db == null) return;
            var conn = await _db.GetConnectionAsync();
            string key = $"subcats:{category}";

            var existing = await conn.Table<IdeaLoggerSetting>()
                .FirstOrDefaultAsync(s => s.Username == username && s.Key == key);

            if (_subcategories.TryGetValue(category, out var subs) && subs.Count > 0)
            {
                string value = string.Join("║", subs);
                if (existing != null) { existing.Value = value; await conn.UpdateAsync(existing); }
                else { await conn.InsertAsync(new IdeaLoggerSetting { Username = username, Key = key, Value = value }); }
            }
            else if (existing != null) { await conn.DeleteAsync(existing); }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IDEA LOGGER] Failed to save subcategories: {ex.Message}");
        }
    }

    public async Task<IdeaItem?> LogIdeaAsync(Page page, string username, string? prefillText = null, string? suggestedCategory = null)
    {
        await EnsureLoadedAsync(username);
        var existingCategories = await _ideas.GetCategoriesAsync(username);
        var result = await ShowIdeaPopupAsync(page, username, existingCategories, prefillText, suggestedCategory);

        if (result == null || string.IsNullOrWhiteSpace(result.Value.text))
            return null;

        string category = result.Value.category.Trim();
        string? subcategory = result.Value.subcategory;
        string text = result.Value.text.Trim();
        int rating = result.Value.rating;
        _lastUsedCategory = category;

        var idea = await _ideas.CreateIdeaAsync(username, text, category);
        idea.Rating = rating;
        idea.Subcategory = subcategory;
        await _ideas.UpdateIdeaAsync(idea);

        // Check for linked categories
        if (_linkedCategories.TryGetValue(category, out var linked) && linked.Count > 0)
        {
            await HandleLinkedCategoriesAsync(page, username, text, rating, linked);
        }

        return idea;
    }

    public async Task<IdeaItem?> QuickLogAsync(Page page, string username, string text, string category, int rating = 50)
    {
        bool confirm = await page.DisplayAlert(
            "Log to Ideas?",
            $"Save as idea?\n\n\"{(text.Length > 100 ? text.Substring(0, 100) + "..." : text)}\"\n\nCategory: {category}\nRating: {rating}",
            "Save", "Cancel");

        if (!confirm) return null;

        _lastUsedCategory = category;
        var idea = await _ideas.CreateIdeaAsync(username, text.Trim(), category.Trim());
        idea.Rating = rating;
        await _ideas.UpdateIdeaAsync(idea);
        return idea;
    }

    private async Task<(string text, string category, string? subcategory, int rating)?> ShowIdeaPopupAsync(
        Page page, string username, List<string> existingCategories, string? prefillText, string? suggestedCategory)
    {
        var tcs = new TaskCompletionSource<(string text, string category, string? subcategory, int rating)?>();

        // Use a string array as a holder so closures can read/write the selected category
        // [0] = selected category
        var holder = new[] { "" };

        // Determine initial category
        string? initialCategory = suggestedCategory;
        if (string.IsNullOrEmpty(initialCategory) && _lastUsedCategory != null && _favoriteCategories.Contains(_lastUsedCategory))
            initialCategory = _lastUsedCategory;

        if (!string.IsNullOrEmpty(initialCategory))
            holder[0] = initialCategory;

        // ====== BUILD POPUP ======
        var popup = new Grid
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White, CornerRadius = 12, Padding = 20,
            WidthRequest = 480, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        };

        var mainStack = new VerticalStackLayout { Spacing = 12 };

        // Title
        mainStack.Children.Add(new Label
        {
            Text = "💡 Log Idea", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333")
        });

        // Idea text
        mainStack.Children.Add(new Label { Text = "Idea:", FontSize = 12, TextColor = Color.FromArgb("#666") });
        var ideaEditor = new Editor
        {
            Text = prefillText ?? "", Placeholder = "What's your idea?",
            BackgroundColor = Color.FromArgb("#F5F5F5"), HeightRequest = 100, FontSize = 14
        };
        mainStack.Children.Add(ideaEditor);

        // ====== FAVORITES DROPDOWN (only if favorites exist) ======
        Picker? favPicker = null;
        if (_favoriteCategories.Count > 0)
        {
            mainStack.Children.Add(new Label { Text = "⭐ Favorites:", FontSize = 12, TextColor = Color.FromArgb("#666") });

            favPicker = new Picker
            {
                Title = "Quick select favorite...", FontSize = 13,
                BackgroundColor = Color.FromArgb("#FFF8E1")
            };

            foreach (var fav in _favoriteCategories)
                favPicker.Items.Add(fav);

            if (!string.IsNullOrEmpty(initialCategory))
            {
                int fi = _favoriteCategories.IndexOf(initialCategory);
                if (fi >= 0) favPicker.SelectedIndex = fi;
            }

            mainStack.Children.Add(favPicker);
        }

        // ====== MAIN CATEGORY DROPDOWN ======
        mainStack.Children.Add(new Label { Text = "Category:", FontSize = 12, TextColor = Color.FromArgb("#666") });

        var catRow = new HorizontalStackLayout { Spacing = 8 };

        var categoryPicker = new Picker
        {
            Title = "Select category...", FontSize = 13,
            BackgroundColor = Color.FromArgb("#F5F5F5"), WidthRequest = 220
        };

        foreach (var cat in existingCategories)
            categoryPicker.Items.Add(cat);

        // Pre-select
        if (!string.IsNullOrEmpty(initialCategory))
        {
            int ci = existingCategories.IndexOf(initialCategory);
            if (ci >= 0) categoryPicker.SelectedIndex = ci;
        }

        catRow.Children.Add(categoryPicker);

        // + New button
        var customEntry = new Entry
        {
            Placeholder = "New category name", BackgroundColor = Color.FromArgb("#F5F5F5"),
            IsVisible = false, WidthRequest = 160
        };

        var newBtn = new Button
        {
            Text = "+ New", BackgroundColor = Color.FromArgb("#4CAF50"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(10, 6), FontSize = 12, HeightRequest = 32
        };
        newBtn.Clicked += (s, e) =>
        {
            customEntry.IsVisible = !customEntry.IsVisible;
            if (customEntry.IsVisible) customEntry.Focus();
        };
        catRow.Children.Add(newBtn);

        // ★ Add to favorites button
        var favBtn = new Button
        {
            Text = "★", BackgroundColor = Color.FromArgb("#FFC107"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(8, 6), FontSize = 14,
            HeightRequest = 32, WidthRequest = 36
        };
        favBtn.Clicked += async (s, e) =>
        {
            string? catToFav = null;
            if (categoryPicker.SelectedIndex >= 0)
                catToFav = categoryPicker.Items[categoryPicker.SelectedIndex];
            if (!string.IsNullOrWhiteSpace(customEntry.Text))
                catToFav = customEntry.Text.Trim();
            if (catToFav == null && favPicker != null && favPicker.SelectedIndex >= 0)
                catToFav = favPicker.Items[favPicker.SelectedIndex];

            if (!string.IsNullOrEmpty(catToFav) && !_favoriteCategories.Contains(catToFav))
            {
                _favoriteCategories.Add(catToFav);
                favBtn.Text = "★✓";
                if (favPicker != null && !favPicker.Items.Contains(catToFav))
                    favPicker.Items.Add(catToFav);
                await SaveFavoritesAsync(username);
            }
        };
        catRow.Children.Add(favBtn);

        // 🔗 Link categories button
        var linkBtn = new Button
        {
            Text = "🔗", BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 6, Padding = new Thickness(8, 6), FontSize = 14,
            HeightRequest = 32, WidthRequest = 36
        };
        linkBtn.Clicked += async (s, e) =>
        {
            // Get current category
            string? catToLink = null;
            if (categoryPicker.SelectedIndex >= 0)
                catToLink = categoryPicker.Items[categoryPicker.SelectedIndex];
            if (!string.IsNullOrWhiteSpace(customEntry.Text))
                catToLink = customEntry.Text.Trim();
            if (catToLink == null && favPicker != null && favPicker.SelectedIndex >= 0)
                catToLink = favPicker.Items[favPicker.SelectedIndex];

            if (string.IsNullOrEmpty(catToLink))
            {
                Page? parentPage = FindParentPage(linkBtn);
                if (parentPage != null)
                    await parentPage.DisplayAlert("No Category", "Select a category first.", "OK");
                return;
            }

            await ShowLinkSetupAsync(linkBtn, catToLink, existingCategories, username);
        };
        catRow.Children.Add(linkBtn);

        mainStack.Children.Add(catRow);
        mainStack.Children.Add(customEntry);

        // ====== SUBCATEGORY (optional, per-category) ======
        var subHolder = new[] { "" };

        var subSection = new VerticalStackLayout { Spacing = 6, IsVisible = false };
        subSection.Children.Add(new Label { Text = "Subcategory (optional):", FontSize = 12, TextColor = Color.FromArgb("#666") });

        var subRow = new HorizontalStackLayout { Spacing = 8 };
        var subPicker = new Picker { Title = "(none)", FontSize = 13, BackgroundColor = Color.FromArgb("#F5F5F5"), WidthRequest = 180 };
        subPicker.SelectedIndexChanged += (s, e) =>
        {
            if (subPicker.SelectedIndex > 0) // index 0 = "(none)"
                subHolder[0] = subPicker.Items[subPicker.SelectedIndex];
            else
                subHolder[0] = "";
        };
        subRow.Children.Add(subPicker);

        var newSubBtn = new Button { Text = "+ Sub", BackgroundColor = Color.FromArgb("#78909C"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(10, 6), FontSize = 12, HeightRequest = 32 };
        subRow.Children.Add(newSubBtn);
        subSection.Children.Add(subRow);

        var newSubEntry = new Entry { Placeholder = "New subcategory name", BackgroundColor = Color.FromArgb("#F5F5F5"), IsVisible = false, WidthRequest = 180 };
        subSection.Children.Add(newSubEntry);

        newSubBtn.Clicked += async (s, e) =>
        {
            newSubEntry.IsVisible = !newSubEntry.IsVisible;
            if (newSubEntry.IsVisible) newSubEntry.Focus();
        };

        newSubEntry.Completed += async (s, e) =>
        {
            string newSub = newSubEntry.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(newSub)) return;

            string currentCat = holder[0];
            if (string.IsNullOrEmpty(currentCat)) return;

            if (!_subcategories.ContainsKey(currentCat))
                _subcategories[currentCat] = new List<string>();

            if (!_subcategories[currentCat].Contains(newSub))
            {
                _subcategories[currentCat].Add(newSub);
                await SaveSubcategoriesAsync(username, currentCat);

                // Refresh picker
                RefreshSubPicker(subPicker, currentCat);
                // Select the new one
                int idx = subPicker.Items.IndexOf(newSub);
                if (idx >= 0) subPicker.SelectedIndex = idx;
            }

            newSubEntry.Text = "";
            newSubEntry.IsVisible = false;
        };

        mainStack.Children.Add(subSection);

        // Helper to refresh subcategory picker for a given category
        void RefreshSubPicker(Picker picker, string cat)
        {
            picker.Items.Clear();
            picker.Items.Add("(none)");
            if (_subcategories.TryGetValue(cat, out var subs))
                foreach (var sub in subs) picker.Items.Add(sub);
            picker.SelectedIndex = 0;
            subHolder[0] = "";
            subSection.IsVisible = true;
        }

        // ====== WIRE UP CROSS-PICKER SYNC ======

        // When main category picker changes → update holder, deselect favorites, refresh subcategories
        categoryPicker.SelectedIndexChanged += (s, e) =>
        {
            if (categoryPicker.SelectedIndex >= 0)
            {
                holder[0] = categoryPicker.Items[categoryPicker.SelectedIndex];
                if (favPicker != null) favPicker.SelectedIndex = -1;
                RefreshSubPicker(subPicker, holder[0]);
            }
        };

        // When favorites picker changes → update holder, sync main picker, refresh subcategories
        if (favPicker != null)
        {
            favPicker.SelectedIndexChanged += (s, e) =>
            {
                if (favPicker.SelectedIndex >= 0)
                {
                    holder[0] = favPicker.Items[favPicker.SelectedIndex];
                    int mainIdx = existingCategories.IndexOf(holder[0]);
                    categoryPicker.SelectedIndex = mainIdx;
                    RefreshSubPicker(subPicker, holder[0]);
                }
            };
        }

        // When typing new category → update holder, deselect both pickers, hide subcategories
        customEntry.TextChanged += (s, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.NewTextValue))
            {
                holder[0] = e.NewTextValue;
                categoryPicker.SelectedIndex = -1;
                if (favPicker != null) favPicker.SelectedIndex = -1;
                subSection.IsVisible = false;
                subHolder[0] = "";
            }
        };

        // Show subcategories if initial category has them
        if (!string.IsNullOrEmpty(initialCategory) && _subcategories.ContainsKey(initialCategory))
            RefreshSubPicker(subPicker, initialCategory);

        // ====== RATING ======
        mainStack.Children.Add(new Label { Text = "Rating:", FontSize = 12, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 4, 0, 0) });

        int selectedRating = 50;
        Button? selectedRatingBtn = null;
        var ratingFlow = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start };
        var ratingButtons = new List<Button>();

        Color GetRatingColor(int r, bool sel) => !sel ? Color.FromArgb("#E0E0E0") : r >= 70 ? Color.FromArgb("#4CAF50") : r >= 40 ? Color.FromArgb("#FF9800") : Color.FromArgb("#9E9E9E");

        void UpdateRatingSelection(Button btn, int r)
        {
            selectedRating = r;
            foreach (var b in ratingButtons) { b.BackgroundColor = Color.FromArgb("#E0E0E0"); b.TextColor = Color.FromArgb("#333"); }
            btn.BackgroundColor = GetRatingColor(r, true); btn.TextColor = Colors.White; selectedRatingBtn = btn;
        }

        foreach (var r in new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })
        {
            var rb = new Button
            {
                Text = r.ToString(),
                BackgroundColor = r == selectedRating ? GetRatingColor(r, true) : Color.FromArgb("#E0E0E0"),
                TextColor = r == selectedRating ? Colors.White : Color.FromArgb("#333"),
                CornerRadius = 6, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4),
                FontSize = 12, WidthRequest = 40, HeightRequest = 32
            };
            var cr = r;
            rb.Clicked += (s, e) => UpdateRatingSelection(rb, cr);
            ratingButtons.Add(rb); ratingFlow.Children.Add(rb);
            if (r == selectedRating) selectedRatingBtn = rb;
        }

        var customRating = new Entry { Placeholder = "Custom", Keyboard = Keyboard.Numeric, BackgroundColor = Color.FromArgb("#F5F5F5"), WidthRequest = 60, HeightRequest = 32 };
        customRating.TextChanged += (s, e) =>
        {
            if (int.TryParse(e.NewTextValue, out int c) && c >= 0 && c <= 100)
            {
                selectedRating = c;
                foreach (var b in ratingButtons) { b.BackgroundColor = Color.FromArgb("#E0E0E0"); b.TextColor = Color.FromArgb("#333"); }
                selectedRatingBtn = null;
            }
        };
        ratingFlow.Children.Add(customRating);
        mainStack.Children.Add(ratingFlow);

        // ====== ACTION BUTTONS ======
        var btnRow = new HorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.End, Margin = new Thickness(0, 8, 0, 0) };

        var cancelBtn = new Button { Text = "Cancel", BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(20, 10) };
        var saveBtn = new Button { Text = "💾 Save Idea", BackgroundColor = Color.FromArgb("#4CAF50"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(20, 10), FontAttributes = FontAttributes.Bold };

        btnRow.Children.Add(cancelBtn);
        btnRow.Children.Add(saveBtn);
        mainStack.Children.Add(btnRow);

        card.Content = mainStack;
        popup.Children.Add(card);

        // ====== ATTACH TO PAGE ======
        if (page is not ContentPage contentPage)
        {
            tcs.TrySetResult(null);
            return await tcs.Task;
        }

        void Cancel(Grid container) { container.Children.Remove(popup); tcs.TrySetResult(null); }
        void Save(Grid container) { container.Children.Remove(popup); tcs.TrySetResult((ideaEditor.Text ?? "", holder[0], string.IsNullOrWhiteSpace(subHolder[0]) ? null : subHolder[0], selectedRating)); }

        if (contentPage.Content is Grid pageGrid)
        {
            if (pageGrid.RowDefinitions.Count > 0)
                Grid.SetRowSpan(popup, pageGrid.RowDefinitions.Count);
            pageGrid.Children.Add(popup);
            cancelBtn.Clicked += (s, e) => Cancel(pageGrid);
            saveBtn.Clicked += (s, e) => Save(pageGrid);
            ideaEditor.Focus();
        }
        else
        {
            var wrapper = new Grid();
            if (contentPage.Content != null) wrapper.Children.Add(contentPage.Content);
            wrapper.Children.Add(popup);
            contentPage.Content = wrapper;
            cancelBtn.Clicked += (s, e) => Cancel(wrapper);
            saveBtn.Clicked += (s, e) => Save(wrapper);
            ideaEditor.Focus();
        }

        return await tcs.Task;
    }

    // ====== LINKED CATEGORIES ======

    private async Task HandleLinkedCategoriesAsync(Page page, string username, string text, int rating, List<string> linkedCategories)
    {
        string linkedList = string.Join(", ", linkedCategories);
        string choice = await page.DisplayActionSheet(
            $"Also log to linked categories?\n({linkedList})",
            "Cancel",
            null,
            "Yes (all linked)",
            "No",
            "Some (choose which)");

        if (string.IsNullOrEmpty(choice) || choice == "Cancel" || choice == "No") return;

        List<string> categoriesToLog;

        if (choice.StartsWith("Yes"))
        {
            categoriesToLog = linkedCategories;
        }
        else // "Some"
        {
            categoriesToLog = new List<string>();
            foreach (var cat in linkedCategories)
            {
                bool include = await page.DisplayAlert("Link", $"Also log to \"{cat}\"?", "Yes", "No");
                if (include) categoriesToLog.Add(cat);
            }
        }

        foreach (var cat in categoriesToLog)
        {
            try
            {
                var linkedIdea = await _ideas.CreateIdeaAsync(username, text, cat);
                linkedIdea.Rating = rating;
                await _ideas.UpdateIdeaAsync(linkedIdea);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[IDEA LOGGER] Failed to log to linked category {cat}: {ex.Message}");
            }
        }
    }

    private async Task ShowLinkSetupAsync(Button linkBtn, string category, List<string> allCategories, string username)
    {
        Page? page = FindParentPage(linkBtn);
        if (page == null) return;

        if (!_linkedCategories.ContainsKey(category))
            _linkedCategories[category] = new List<string>();

        var currentLinks = _linkedCategories[category];

        string choice = await page.DisplayActionSheet(
            $"Links for \"{category}\"",
            "Done",
            null,
            "➕ Add linked category",
            currentLinks.Count > 0 ? $"🗑️ Remove link ({currentLinks.Count} linked)" : "📋 No links yet");

        if (choice == null || choice == "Done") return;

        if (choice.StartsWith("➕"))
        {
            var available = allCategories
                .Where(c => c != category && !currentLinks.Contains(c))
                .ToArray();

            if (available.Length == 0)
            {
                await page.DisplayAlert("No Categories", "All categories are already linked or there are no other categories.", "OK");
                return;
            }

            string? selected = await page.DisplayActionSheet("Link to which category?", "Cancel", null, available);
            if (!string.IsNullOrEmpty(selected) && selected != "Cancel")
            {
                currentLinks.Add(selected);
                linkBtn.Text = "🔗✓";
                await SaveLinkedCategoryAsync(username, category);
                await page.DisplayAlert("Linked", $"\"{category}\" → \"{selected}\"\n\nWhen you log to \"{category}\", you'll be asked to also log to \"{selected}\".", "OK");
            }
        }
        else if (choice.StartsWith("🗑️") && currentLinks.Count > 0)
        {
            string? toRemove = await page.DisplayActionSheet("Remove which link?", "Cancel", null, currentLinks.ToArray());
            if (!string.IsNullOrEmpty(toRemove) && toRemove != "Cancel")
            {
                currentLinks.Remove(toRemove);
                if (currentLinks.Count == 0) linkBtn.Text = "🔗";
                await SaveLinkedCategoryAsync(username, category);
                await page.DisplayAlert("Removed", $"Link from \"{category}\" → \"{toRemove}\" removed.", "OK");
            }
        }
    }

    private static Page? FindParentPage(Element element)
    {
        Element? el = element;
        while (el != null) { if (el is Page p) return p; el = el.Parent; }
        return Application.Current?.MainPage;
    }
}

using Bannister.Models;
using SQLite;

namespace Bannister.Services;

public enum IdeaLogMode
{
    Interactive,
    Silent
}

public sealed record IdeaLogRequest
{
    public required string Username { get; init; }
    public string? Title { get; init; }
    public string? Body { get; init; }
    public string? Category { get; init; }
    public string? Notes { get; init; }
    public string? Subcategory { get; init; }
    public int? Rating { get; init; }
    public bool? IsStarred { get; init; }
    public int? Status { get; init; }
    public DateTime? CreatedAt { get; init; }
    public bool? IsLlmSource { get; init; }
}

public sealed record IdeaLogFailure(IdeaLogRequest Request, string Error);

public sealed record IdeaLogSuccess(IdeaLogRequest Request, int? IdeaId);

public sealed record IdeaLogBatchResult(
    int SavedCount,
    IReadOnlyList<IdeaLogFailure> Failures,
    IReadOnlyList<IdeaLogSuccess> Successes);

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
    private readonly AudioLibraryService? _audioLib;

    // Persisted state (loaded from DB on first use)
    private List<string> _favoriteCategories = new();
    private Dictionary<string, List<string>> _linkedCategories = new();
    private Dictionary<string, string> _audioLinkedCategories = new(); // idea category → audio library category
    private Dictionary<string, List<string>> _subcategories = new(); // category → list of subcategory names
    private bool _loaded = false;

    // Session-level only
    private string? _lastUsedCategory = null;

    public IdeaLoggerService(IdeasService ideas, DatabaseService db, AudioLibraryService? audioLib = null)
    {
        _ideas = ideas;
        _db = db;
        _audioLib = audioLib;
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
            if (!_db.IsReadOnly) await conn.CreateTableAsync<IdeaLoggerSetting>();

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

            // Load audio-linked categories (stored as "audiolink:{category}" → "audio_category_name")
            var audioLinkRows = await conn.Table<IdeaLoggerSetting>()
                .Where(s => s.Username == username && s.Key.StartsWith("audiolink:"))
                .ToListAsync();
            foreach (var row in audioLinkRows)
            {
                string cat = row.Key.Substring(10); // Remove "audiolink:" prefix
                if (!string.IsNullOrEmpty(row.Value))
                    _audioLinkedCategories[cat] = row.Value;
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

    public async Task<int?> LogIdeaAsync(IdeaLogRequest request, IdeaLogMode mode, Page? page = null)
    {
        await EnsureLoadedAsync(request.Username);
        IdeaLogRequest finalRequest = request;
        bool interactive = mode == IdeaLogMode.Interactive;
        if (interactive)
        {
            if (page == null)
                throw new ArgumentException("Interactive idea logging requires a page.", nameof(page));
            var existingCategories = await _ideas.GetCategoriesAsync(request.Username);
            var edited = await ShowIdeaPopupAsync(page, request, existingCategories);
            if (edited == null) return null;
            finalRequest = edited;
        }

        var idea = await SaveIdeaCoreAsync(finalRequest, interactive ? page : null, interactive);
        if (mode == IdeaLogMode.Silent && idea.Id <= 0)
            throw new InvalidOperationException("The idea was queued for sync and does not have a local Id.");
        return idea.Id;
    }

    /// <summary>
    /// Logs an automatic side-effect without UI and never lets logging failure break the caller.
    /// A read-only-device queue result (which has no local Id) counts as success.
    /// </summary>
    public async Task LogSilentlyAsync(IdeaLogRequest request)
    {
        try
        {
            await EnsureLoadedAsync(request.Username);
            await SaveIdeaCoreAsync(request, null, false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IDEA LOGGER] Silent log failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs an import batch through the same validation and persistence core as single ideas.
    /// Silent batches deliberately skip linked-category, audio-link, and confirmation UI.
    /// </summary>
    public async Task<IdeaLogBatchResult> LogBatchSilentAsync(IEnumerable<IdeaLogRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        int savedCount = 0;
        var failures = new List<IdeaLogFailure>();
        var successes = new List<IdeaLogSuccess>();
        foreach (var request in requests)
        {
            try
            {
                await EnsureLoadedAsync(request.Username);
                var idea = await SaveIdeaCoreAsync(request, null, false);
                successes.Add(new IdeaLogSuccess(request, idea.Id > 0 ? idea.Id : null));
                savedCount++;
            }
            catch (Exception ex)
            {
                failures.Add(new IdeaLogFailure(request, ex.Message));
                System.Diagnostics.Debug.WriteLine($"[IDEA LOGGER] Batch log failed for '{request.Title}': {ex.Message}");
            }
        }

        return new IdeaLogBatchResult(savedCount, failures, successes);
    }

    private async Task<IdeaItem> SaveIdeaCoreAsync(IdeaLogRequest request, Page? page, bool interactiveSideEffects)
    {
        try
        {
            string username = request.Username?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username is required.", nameof(request));

            string title = (request.Title ?? "").Trim();
            string body = (request.Body ?? "").Trim();
            string category = (request.Category ?? "").Trim();
            string? notes = request.Notes?.Trim();
            string? subcategory = string.IsNullOrWhiteSpace(request.Subcategory) ? null : request.Subcategory.Trim();
            int rating = Math.Clamp(request.Rating ?? 50, 0, 100);
            bool isStarred = request.IsStarred ?? false;
            int status = request.Status ?? 0;
            DateTime createdAt = request.CreatedAt ?? DateTime.Now;
            bool isLlmSource = request.IsLlmSource ?? false;

            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Idea title is required.", nameof(request));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Idea body is required.", nameof(request));

            var normalized = request with
            {
                Username = username,
                Title = title,
                Body = body,
                Category = category,
                Notes = notes,
                Subcategory = subcategory,
                Rating = rating,
                IsStarred = isStarred,
                Status = status,
                CreatedAt = createdAt,
                IsLlmSource = isLlmSource
            };

            IdeaItem idea = await _ideas.CreateIdeaAsync(
                username,
                title,
                category,
                notes: notes,
                subcategory: subcategory,
                rating: rating,
                isStarred: isStarred,
                status: status,
                createdAt: createdAt,
                fullIdea: body,
                isLlmSource: isLlmSource);

            _lastUsedCategory = category;

            if (interactiveSideEffects && page != null)
            {
                if (_linkedCategories.TryGetValue(category, out var linked) && linked.Count > 0)
                    await HandleLinkedCategoriesAsync(page, normalized, linked);

                if (_audioLinkedCategories.TryGetValue(category, out var audioCategory) && _audioLib != null)
                    await HandleAudioLinkAsync(page, username, GetIdeaContentText(body, title), audioCategory);

                string heading = idea.Id > 0 ? "✅ Idea Logged" : "Idea Queued";
                await page.DisplayAlert(
                    heading,
                    $"{(title.Length > 60 ? title[..57] + "..." : title)}\n\nCategory: {category}{(subcategory != null ? $"\nSubcategory: {subcategory}" : "")}",
                    "OK");
            }

            return idea;
        }
        catch (Exception ex) when (ex is not ArgumentException && ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Idea logging failed: {ex.Message}", ex);
        }
    }

    private async Task<IdeaLogRequest?> ShowIdeaPopupAsync(
        Page page, IdeaLogRequest request, List<string> existingCategories)
    {
        string username = request.Username;
        var tcs = new TaskCompletionSource<IdeaLogRequest?>();
        bool isPhone = DeviceInfo.Idiom == DeviceIdiom.Phone ||
            DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density < 600;

        void StylePhoneEntry(Entry entry)
        {
            if (!isPhone) return;
            entry.ThemeColor(InputView.TextColorProperty, ThemeKey.InputText);
            entry.ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3);
            entry.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
            entry.HorizontalOptions = LayoutOptions.Fill;
        }

        void StylePhoneEditor(Editor editor)
        {
            if (!isPhone) return;
            editor.ThemeColor(InputView.TextColorProperty, ThemeKey.InputText);
            editor.ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3);
            editor.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        }

        void StylePhonePicker(Picker picker)
        {
            if (!isPhone) return;
            picker.ThemeColor(Picker.TextColorProperty, ThemeKey.InputText);
            picker.ThemeColor(Picker.TitleColorProperty, ThemeKey.CardText3);
            picker.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
            picker.HorizontalOptions = LayoutOptions.Fill;
        }

        // Use a string array as a holder so closures can read/write the selected category
        // [0] = selected category
        var holder = new[] { "" };

        // Determine initial category
        string? initialCategory = request.Category;
        if (string.IsNullOrEmpty(initialCategory) && _lastUsedCategory != null && _favoriteCategories.Contains(_lastUsedCategory))
            initialCategory = _lastUsedCategory;

        if (!string.IsNullOrEmpty(initialCategory))
            holder[0] = initialCategory;

        // ====== BUILD POPUP ======
        var popup = (new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

        var card = (new Frame
        {
CornerRadius = 12, Padding = 20,
            WidthRequest = 480, HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center, HasShadow = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
        if (isPhone)
        {
            card.WidthRequest = -1;
            card.Margin = new Thickness(12);
            card.Padding = 16;
            card.HorizontalOptions = LayoutOptions.Fill;
            card.VerticalOptions = LayoutOptions.Center;
        }

        var mainStack = new VerticalStackLayout { Spacing = 12 };

        // Title
        mainStack.Children.Add((new Label
        {
            Text = "💡 Log Idea", FontSize = 20, FontAttributes = FontAttributes.Bold         }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));

        // Idea title and full text
        mainStack.Children.Add((new Label { Text = "Title:", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        var titleEntry = (new Editor
        {
            Text = request.Title ?? request.Body ?? "", Placeholder = "Idea title",
HeightRequest = 100, FontSize = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        StylePhoneEditor(titleEntry);
        mainStack.Children.Add(titleEntry);

        mainStack.Children.Add((new Label { Text = "Full Idea:", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        var fullIdeaEditor = (new Editor
        {
            Text = request.Body ?? request.Title ?? "", Placeholder = "Write the full idea...",
HeightRequest = 100, FontSize = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        StylePhoneEditor(fullIdeaEditor);
        mainStack.Children.Add(fullIdeaEditor);

        // ====== FAVORITES DROPDOWN (only if favorites exist) ======
        Picker? favPicker = null;
        if (_favoriteCategories.Count > 0)
        {
            mainStack.Children.Add((new Label { Text = "⭐ Favorites:", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

            favPicker = (new Picker
            {
                Title = "Quick select favorite...", FontSize = 13
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow);
            StylePhonePicker(favPicker);

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
        mainStack.Children.Add((new Label { Text = "Category:", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

        var categorySection = new VerticalStackLayout { Spacing = 8 };

        View pickerRow;
        if (isPhone)
        {
            pickerRow = new VerticalStackLayout { Spacing = 8 };
        }
        else
        {
            pickerRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };
        }

        var categoryActionsRow = new HorizontalStackLayout { Spacing = 8 };

        var classifications = await _ideas.GetClassificationsAsync(username);
        var classificationMap = classifications
            .GroupBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Classification, StringComparer.OrdinalIgnoreCase);

        var normalCategories = existingCategories
            .Where(c => !classificationMap.TryGetValue(c, out var cls) ||
                        string.Equals(cls, "normal", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var llmCategories = existingCategories
            .Where(c => classificationMap.TryGetValue(c, out var cls) &&
                        string.Equals(cls, "llm", StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalCategoryPicker = (new Picker
        {
            Title = "Normal category",
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Picker.TextColorProperty, ThemeKey.InfoText).ThemeColor(Picker.TitleColorProperty, ThemeKey.InfoText);
        if (isPhone) normalCategoryPicker.WidthRequest = -1;
        StylePhonePicker(normalCategoryPicker);

        var llmCategoryPicker = (new Picker
        {
            Title = "LLM category",
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Picker.TextColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Picker.TitleColorProperty, ThemeKey.HighlightVioletText);
        if (isPhone) llmCategoryPicker.WidthRequest = -1;
        StylePhonePicker(llmCategoryPicker);

        foreach (var cat in normalCategories) normalCategoryPicker.Items.Add(cat);
        foreach (var cat in llmCategories) llmCategoryPicker.Items.Add(cat);

        if (pickerRow is Grid pickerGrid)
        {
            pickerGrid.Add(normalCategoryPicker, 0, 0);
            pickerGrid.Add(llmCategoryPicker, 1, 0);
        }
        else if (pickerRow is VerticalStackLayout pickerStack)
        {
            pickerStack.Children.Add(normalCategoryPicker);
            pickerStack.Children.Add(llmCategoryPicker);
        }

        categorySection.Children.Add(pickerRow);

        // + New button
        var customEntry = (new Entry
        {
            Placeholder = "New category name",             IsVisible = false, WidthRequest = 160
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        if (isPhone) customEntry.WidthRequest = -1;
        StylePhoneEntry(customEntry);

        string? GetCurrentCategory()
        {
            if (customEntry.IsVisible && !string.IsNullOrWhiteSpace(customEntry.Text))
                return customEntry.Text.Trim();

            if (normalCategoryPicker.SelectedIndex >= 0)
                return normalCategoryPicker.Items[normalCategoryPicker.SelectedIndex];

            if (llmCategoryPicker.SelectedIndex >= 0)
                return llmCategoryPicker.Items[llmCategoryPicker.SelectedIndex];

            if (favPicker != null && favPicker.SelectedIndex >= 0)
                return favPicker.Items[favPicker.SelectedIndex];

            return string.IsNullOrWhiteSpace(holder[0]) ? null : holder[0];
        }

        bool IsLlmCategory(string category)
        {
            return classificationMap.TryGetValue(category, out var cls) &&
                   string.Equals(cls, "llm", StringComparison.OrdinalIgnoreCase);
        }

        bool suppressPickerSync = false;

        var newBtn = (new Button
        {
            Text = "+ New",             CornerRadius = 6, Padding = new Thickness(10, 6), FontSize = 12, HeightRequest = 32
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        newBtn.Clicked += (s, e) =>
        {
            customEntry.IsVisible = !customEntry.IsVisible;
            if (customEntry.IsVisible)
            {
                normalCategoryPicker.SelectedIndex = -1;
                llmCategoryPicker.SelectedIndex = -1;
                if (favPicker != null) favPicker.SelectedIndex = -1;
                customEntry.Focus();
            }
        };
        categoryActionsRow.Children.Add(newBtn);

        // ★ Add to favorites button
        var favBtn = (new Button
        {
            Text = "★",             CornerRadius = 6, Padding = new Thickness(8, 6), FontSize = 14,
            HeightRequest = 32, WidthRequest = 36
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        favBtn.Clicked += async (s, e) =>
        {
            string? catToFav = GetCurrentCategory();

            if (!string.IsNullOrEmpty(catToFav) && !_favoriteCategories.Contains(catToFav))
            {
                _favoriteCategories.Add(catToFav);
                favBtn.Text = "★✓";
                if (favPicker != null && !favPicker.Items.Contains(catToFav))
                    favPicker.Items.Add(catToFav);
                await SaveFavoritesAsync(username);
            }
        };
        categoryActionsRow.Children.Add(favBtn);

        // 🔗 Link categories button
        var linkBtn = (new Button
        {
            Text = "🔗",             CornerRadius = 6, Padding = new Thickness(8, 6), FontSize = 14,
            HeightRequest = 32, WidthRequest = 36
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        linkBtn.Clicked += async (s, e) =>
        {
            // Get current category
            string? catToLink = GetCurrentCategory();

            if (string.IsNullOrEmpty(catToLink))
            {
                Page? parentPage = FindParentPage(linkBtn);
                if (parentPage != null)
                    await parentPage.DisplayAlert("No Category", "Select a category first.", "OK");
                return;
            }

            await ShowLinkSetupAsync(linkBtn, catToLink, existingCategories, username);
        };
        categoryActionsRow.Children.Add(linkBtn);

        var reclassifyBtn = (new Button
        {
            Text = "⇄ Reclassify",
            CornerRadius = 6,
            Padding = new Thickness(10, 6),
            FontSize = 12,
            HeightRequest = 32
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Button.TextColorProperty, ThemeKey.WarningText);
        reclassifyBtn.Clicked += async (_, _) =>
        {
            string? selectedCategory = null;
            string currentClassification = "";

            if (normalCategoryPicker.SelectedIndex >= 0)
            {
                selectedCategory = normalCategoryPicker.Items[normalCategoryPicker.SelectedIndex];
                currentClassification = "normal";
            }
            else if (llmCategoryPicker.SelectedIndex >= 0)
            {
                selectedCategory = llmCategoryPicker.Items[llmCategoryPicker.SelectedIndex];
                currentClassification = "llm";
            }

            if (string.IsNullOrWhiteSpace(selectedCategory))
            {
                await page.DisplayAlert(
                    "No category selected",
                    "Select a category from the Normal or LLM dropdown first, then tap Reclassify.",
                    "OK");
                return;
            }

            var newClassification = currentClassification == "normal" ? "llm" : "normal";
            var fromLabel = currentClassification == "normal" ? "Normal" : "LLM";
            var toLabel = newClassification == "normal" ? "Normal" : "LLM";

            var confirm = await page.DisplayAlert(
                "Reclassify category?",
                $"Move '{selectedCategory}' from {fromLabel} to {toLabel}?",
                "Move",
                "Cancel");

            if (!confirm) return;

            await _ideas.SetClassificationAsync(username, selectedCategory, newClassification);

            var updatedClassifications = await _ideas.GetClassificationsAsync(username);
            classificationMap = updatedClassifications
                .GroupBy(c => c.CategoryName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Classification, StringComparer.OrdinalIgnoreCase);

            normalCategories = existingCategories
                .Where(c => !classificationMap.TryGetValue(c, out var cls) ||
                            string.Equals(cls, "normal", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            llmCategories = existingCategories
                .Where(c => classificationMap.TryGetValue(c, out var cls) &&
                            string.Equals(cls, "llm", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            suppressPickerSync = true;
            normalCategoryPicker.Items.Clear();
            foreach (var cat in normalCategories) normalCategoryPicker.Items.Add(cat);
            llmCategoryPicker.Items.Clear();
            foreach (var cat in llmCategories) llmCategoryPicker.Items.Add(cat);

            if (newClassification == "normal")
            {
                var idx = normalCategories.FindIndex(c =>
                    string.Equals(c, selectedCategory, StringComparison.OrdinalIgnoreCase));
                normalCategoryPicker.SelectedIndex = idx;
                llmCategoryPicker.SelectedIndex = -1;
            }
            else
            {
                var idx = llmCategories.FindIndex(c =>
                    string.Equals(c, selectedCategory, StringComparison.OrdinalIgnoreCase));
                llmCategoryPicker.SelectedIndex = idx;
                normalCategoryPicker.SelectedIndex = -1;
            }
            suppressPickerSync = false;

            holder[0] = selectedCategory;
        };
        categoryActionsRow.Children.Add(reclassifyBtn);

        categorySection.Children.Add(categoryActionsRow);
        mainStack.Children.Add(categorySection);
        mainStack.Children.Add(customEntry);

        // ====== SUBCATEGORY (optional, per-category) ======
        var subHolder = new[] { "" };

        var subSection = new VerticalStackLayout { Spacing = 6, IsVisible = false };
        subSection.Children.Add((new Label { Text = "Subcategory (optional):", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

        Layout subRow = isPhone
            ? new VerticalStackLayout { Spacing = 8 }
            : new HorizontalStackLayout { Spacing = 8 };
        var subPicker = (new Picker { Title = "(none)", FontSize = 13, WidthRequest = 180 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        if (isPhone) subPicker.WidthRequest = -1;
        StylePhonePicker(subPicker);
        subPicker.SelectedIndexChanged += (s, e) =>
        {
            if (subPicker.SelectedIndex > 0) // index 0 = "(none)"
                subHolder[0] = subPicker.Items[subPicker.SelectedIndex];
            else
                subHolder[0] = "";
        };
        subRow.Children.Add(subPicker);

        var newSubBtn = (new Button { Text = "+ Sub", CornerRadius = 6, Padding = new Thickness(10, 6), FontSize = 12, HeightRequest = 32 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        subRow.Children.Add(newSubBtn);
        subSection.Children.Add(subRow);

        var newSubEntry = (new Entry { Placeholder = "New subcategory name", IsVisible = false, WidthRequest = 180 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        if (isPhone) newSubEntry.WidthRequest = -1;
        StylePhoneEntry(newSubEntry);
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
        normalCategoryPicker.SelectedIndexChanged += (s, e) =>
        {
            if (suppressPickerSync) return;
            if (normalCategoryPicker.SelectedIndex >= 0)
            {
                holder[0] = normalCategoryPicker.Items[normalCategoryPicker.SelectedIndex];
                suppressPickerSync = true;
                llmCategoryPicker.SelectedIndex = -1;
                suppressPickerSync = false;
                if (favPicker != null) favPicker.SelectedIndex = -1;
                customEntry.IsVisible = false;
                RefreshSubPicker(subPicker, holder[0]);
            }
        };

        llmCategoryPicker.SelectedIndexChanged += (s, e) =>
        {
            if (suppressPickerSync) return;
            if (llmCategoryPicker.SelectedIndex >= 0)
            {
                holder[0] = llmCategoryPicker.Items[llmCategoryPicker.SelectedIndex];
                suppressPickerSync = true;
                normalCategoryPicker.SelectedIndex = -1;
                suppressPickerSync = false;
                if (favPicker != null) favPicker.SelectedIndex = -1;
                customEntry.IsVisible = false;
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
                    suppressPickerSync = true;
                    normalCategoryPicker.SelectedIndex = normalCategories.FindIndex(c => string.Equals(c, holder[0], StringComparison.OrdinalIgnoreCase));
                    llmCategoryPicker.SelectedIndex = llmCategories.FindIndex(c => string.Equals(c, holder[0], StringComparison.OrdinalIgnoreCase));
                    suppressPickerSync = false;
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
                suppressPickerSync = true;
                normalCategoryPicker.SelectedIndex = -1;
                llmCategoryPicker.SelectedIndex = -1;
                suppressPickerSync = false;
                if (favPicker != null) favPicker.SelectedIndex = -1;
                subSection.IsVisible = false;
                subHolder[0] = "";
            }
        };

        if (!string.IsNullOrWhiteSpace(initialCategory))
        {
            var normalIdx = normalCategories.FindIndex(c =>
                string.Equals(c, initialCategory, StringComparison.OrdinalIgnoreCase));
            var llmIdx = llmCategories.FindIndex(c =>
                string.Equals(c, initialCategory, StringComparison.OrdinalIgnoreCase));

            if (normalIdx >= 0)
                normalCategoryPicker.SelectedIndex = normalIdx;
            else if (llmIdx >= 0)
                llmCategoryPicker.SelectedIndex = llmIdx;
            else
            {
                customEntry.Text = initialCategory;
                customEntry.IsVisible = true;
            }
        }

        // Show subcategories if initial category has them
        if (!string.IsNullOrEmpty(initialCategory) && _subcategories.ContainsKey(initialCategory))
            RefreshSubPicker(subPicker, initialCategory);
        if (!string.IsNullOrWhiteSpace(request.Subcategory) && !string.IsNullOrWhiteSpace(initialCategory))
        {
            if (!subSection.IsVisible) RefreshSubPicker(subPicker, initialCategory);
            if (!subPicker.Items.Contains(request.Subcategory)) subPicker.Items.Add(request.Subcategory);
            subPicker.SelectedIndex = subPicker.Items.IndexOf(request.Subcategory);
        }

        // ====== RATING ======
        mainStack.Children.Add((new Label { Text = "Rating:", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

        int selectedRating = Math.Clamp(request.Rating ?? 50, 0, 100);
        Button? selectedRatingBtn = null;
        Layout ratingFlow = isPhone
            ? new HorizontalStackLayout { Spacing = 4 }
            : new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, JustifyContent = Microsoft.Maui.Layouts.FlexJustify.Start };
        var ratingButtons = new List<Button>();

        ThemeKey GetRatingColor(int r, bool sel) => !sel ? ThemeKey.Separator : r >= 70 ? ThemeKey.SuccessButtonBackground : r >= 40 ? ThemeKey.WarningButtonBackground : ThemeKey.DisabledButtonBackground;

        void UpdateRatingSelection(Button btn, int r)
        {
            selectedRating = r;
            foreach (var b in ratingButtons) { b.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator); b.ThemeColor(Button.TextColorProperty, ThemeKey.CardText1); }
            btn.ThemeColor(VisualElement.BackgroundColorProperty, GetRatingColor(r, true)); btn.ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText); selectedRatingBtn = btn;
        }

        foreach (var r in new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 })
        {
            var rb = (new Button
            {
                Text = r.ToString(),
                CornerRadius = 6, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(8, 4),
                FontSize = 12, WidthRequest = 40, HeightRequest = 32
            }).ThemeColor(Button.TextColorProperty, r == selectedRating ? ThemeKey.ButtonPrimaryText : ThemeKey.CardText1)
                .ThemeColor(VisualElement.BackgroundColorProperty, r == selectedRating ? GetRatingColor(r, true) : ThemeKey.Separator);
            var cr = r;
            rb.Clicked += (s, e) => UpdateRatingSelection(rb, cr);
            ratingButtons.Add(rb); ratingFlow.Children.Add(rb);
            if (r == selectedRating) selectedRatingBtn = rb;
        }

        var customRating = (new Entry { Placeholder = "Custom", Keyboard = Keyboard.Numeric, WidthRequest = 60, HeightRequest = 32 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        StylePhoneEntry(customRating);
        if (!new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 }.Contains(selectedRating))
            customRating.Text = selectedRating.ToString();
        customRating.TextChanged += (s, e) =>
        {
            if (int.TryParse(e.NewTextValue, out int c) && c >= 0 && c <= 100)
            {
                selectedRating = c;
                foreach (var b in ratingButtons) { b.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator); b.ThemeColor(Button.TextColorProperty, ThemeKey.CardText1); }
                selectedRatingBtn = null;
            }
        };
        ratingFlow.Children.Add(customRating);
        if (isPhone)
        {
            mainStack.Children.Add(new ScrollView
            {
                Orientation = ScrollOrientation.Horizontal,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Always,
                Content = ratingFlow
            });
        }
        else
        {
            mainStack.Children.Add(ratingFlow);
        }

        // ====== ACTION BUTTONS ======
        var cancelBtn = (new Button { Text = "Cancel", CornerRadius = 6, Padding = new Thickness(20, 10) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        var saveBtn = (new Button { Text = "💾 Save Idea", CornerRadius = 6, Padding = new Thickness(20, 10), FontAttributes = FontAttributes.Bold }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);

        if (isPhone)
        {
            saveBtn.Text = "Save Idea";
            cancelBtn.HeightRequest = 44;
            saveBtn.HeightRequest = 44;
            cancelBtn.Padding = new Thickness(8, 0);
            saveBtn.Padding = new Thickness(8, 0);

            var btnGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8,
                Margin = new Thickness(0, 8, 0, 0)
            };
            btnGrid.Add(cancelBtn, 0, 0);
            btnGrid.Add(saveBtn, 1, 0);
            mainStack.Children.Add(btnGrid);
        }
        else
        {
            var btnRow = new HorizontalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.End, Margin = new Thickness(0, 8, 0, 0) };
            btnRow.Children.Add(cancelBtn);
            btnRow.Children.Add(saveBtn);
            mainStack.Children.Add(btnRow);
        }

        card.Content = isPhone
            ? new ScrollView { Content = mainStack, VerticalScrollBarVisibility = ScrollBarVisibility.Always }
            : mainStack;
        popup.Children.Add(card);

        // ====== ATTACH TO PAGE ======
        if (page is not ContentPage contentPage)
        {
            tcs.TrySetResult(null);
            return await tcs.Task;
        }

        void Cancel(Grid container) { container.Children.Remove(popup); tcs.TrySetResult(null); }
        async Task SaveAsync(Grid container)
        {
            string title = titleEntry.Text?.Trim() ?? "";
            string fullIdea = fullIdeaEditor.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(fullIdea))
            {
                Page? parentPage = FindParentPage(saveBtn);
                if (parentPage != null)
                    await parentPage.DisplayAlert("Required Fields", "Title and Full Idea are both required.", "OK");
                return;
            }

            var finalCategory = GetCurrentCategory()?.Trim() ?? "";
            bool isKnownCategory = !string.IsNullOrWhiteSpace(finalCategory) && existingCategories.Any(c =>
                string.Equals(c, finalCategory, StringComparison.OrdinalIgnoreCase));
            bool isLlmSource = !string.IsNullOrWhiteSpace(finalCategory) && IsLlmCategory(finalCategory);

            if (!string.IsNullOrWhiteSpace(finalCategory) && !isKnownCategory)
            {
                var classChoice = await page.DisplayActionSheet(
                    $"Classify new category '{finalCategory}':",
                    "Cancel",
                    null,
                    "Normal category",
                    "LLM category");

                if (classChoice == "Cancel" || string.IsNullOrWhiteSpace(classChoice))
                    return;

                isLlmSource = classChoice == "LLM category";
            }

            container.Children.Remove(popup);
            tcs.TrySetResult(request with
            {
                Title = title,
                Body = fullIdea,
                Category = finalCategory,
                Subcategory = string.IsNullOrWhiteSpace(subHolder[0]) ? null : subHolder[0],
                Rating = selectedRating,
                IsLlmSource = isLlmSource
            });
        }

        if (contentPage.Content is Grid pageGrid)
        {
            if (pageGrid.RowDefinitions.Count > 0)
                Grid.SetRowSpan(popup, pageGrid.RowDefinitions.Count);
            pageGrid.Children.Add(popup);
            cancelBtn.Clicked += (s, e) => Cancel(pageGrid);
            saveBtn.Clicked += async (s, e) => await SaveAsync(pageGrid);
            titleEntry.Focus();
        }
        else
        {
            var wrapper = new Grid();
            if (contentPage.Content != null) wrapper.Children.Add(contentPage.Content);
            wrapper.Children.Add(popup);
            contentPage.Content = wrapper;
            cancelBtn.Clicked += (s, e) => Cancel(wrapper);
            saveBtn.Clicked += async (s, e) => await SaveAsync(wrapper);
            titleEntry.Focus();
        }

        return await tcs.Task;
    }

    // ====== LINKED CATEGORIES ======

    private async Task HandleLinkedCategoriesAsync(Page page, IdeaLogRequest request, List<string> linkedCategories)
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
                await SaveIdeaCoreAsync(
                    request with { Category = cat, Subcategory = null, IsLlmSource = null },
                    null,
                    false);
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
        string audioLinkStatus = _audioLinkedCategories.ContainsKey(category)
            ? $"🔊 Audio Link: {_audioLinkedCategories[category]} (tap to remove)"
            : "🔊 Link to Audio Library";

        string choice = await page.DisplayActionSheet(
            $"Links for \"{category}\"",
            "Done",
            null,
            "➕ Add linked category",
            currentLinks.Count > 0 ? $"🗑️ Remove link ({currentLinks.Count} linked)" : "📋 No links yet",
            audioLinkStatus);

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
        else if (choice.StartsWith("🔊"))
        {
            if (_audioLinkedCategories.ContainsKey(category))
            {
                // Already linked — remove it
                bool remove = await page.DisplayAlert("Remove Audio Link",
                    $"Remove audio library link from \"{category}\" → \"{_audioLinkedCategories[category]}\"?",
                    "Remove", "Cancel");
                if (remove)
                {
                    _audioLinkedCategories.Remove(category);
                    await SaveAudioLinkAsync(username, category);
                    await page.DisplayAlert("Removed", "Audio library link removed.", "OK");
                }
            }
            else
            {
                // Set up new audio link — ask for the audio category name
                string? audioCat = await page.DisplayPromptAsync(
                    "Audio Library Category",
                    $"When logging to \"{category}\", also create an audio item in which Audio Library category?",
                    "Link",
                    "Cancel",
                    placeholder: category, // default to same name
                    initialValue: category);

                if (!string.IsNullOrWhiteSpace(audioCat))
                {
                    _audioLinkedCategories[category] = audioCat.Trim();
                    linkBtn.Text = "🔗✓";
                    await SaveAudioLinkAsync(username, category);
                    await page.DisplayAlert("Audio Linked",
                        $"\"{category}\" → Audio Library: \"{audioCat.Trim()}\"\n\n" +
                        "When you log an idea to this category, you'll be prompted to also add it to the Audio Library.",
                        "OK");
                }
            }
        }
    }

    private static Page? FindParentPage(Element element)
    {
        Element? el = element;
        while (el != null) { if (el is Page p) return p; el = el.Parent; }
        return Application.Current?.MainPage;
    }

    private async Task HandleAudioLinkAsync(Page page, string username, string text, string audioCategory)
    {
        if (_audioLib == null) return;

        string choice = await page.DisplayActionSheet(
            $"Also add to Audio Library ({audioCategory})?",
            "Skip",
            null,
            "📂 Yes + Browse audio file",
            "🎙️ Yes + Generate Audio",
            "📝 Yes (no audio yet)");

        if (string.IsNullOrEmpty(choice) || choice == "Skip") return;

        // Create the audio item
        var audioItem = await _audioLib.CreateAsync(username, text, audioCategory);

        if (choice.StartsWith("📂"))
        {
            // Browse for audio file
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select audio file",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                    {
                        { DevicePlatform.WinUI, new[] { ".mp3", ".wav", ".m4a", ".ogg", ".flac", ".aac" } },
                        { DevicePlatform.Android, new[] { "audio/*" } },
                        { DevicePlatform.iOS, new[] { "public.audio" } }
                    })
                });

                if (result != null)
                {
                    using var stream = await result.OpenReadAsync();
                    await _audioLib.AttachAudioFromStreamAsync(audioItem, stream, result.FileName);
                    await page.DisplayAlert("Added", "Audio item created with audio file.", "OK");
                }
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Error", $"Failed to attach audio: {ex.Message}", "OK");
            }
        }
        else if (choice.StartsWith("🎙️"))
        {
            // Generate TTS audio directly
#if WINDOWS
            try
            {
                var synth = new Windows.Media.SpeechSynthesis.SpeechSynthesizer();
                var stream = await synth.SynthesizeTextToStreamAsync(text);

                string tempPath = Path.Combine(Path.GetTempPath(), $"tts_temp_{DateTime.Now.Ticks}.wav");
                using (var fileStream = File.Create(tempPath))
                {
                    var inputStream = stream.AsStreamForRead();
                    await inputStream.CopyToAsync(fileStream);
                }

                await _audioLib.MarkAudioAsGeneratedAsync(audioItem, tempPath);
                try { File.Delete(tempPath); } catch { }

                await page.DisplayAlert("Audio Generated", "TTS audio created and attached.", "OK");
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("TTS Error", $"Failed to generate: {ex.Message}\n\nText copied to clipboard instead.", "OK");
                await Clipboard.SetTextAsync(text);
            }
#else
            await Clipboard.SetTextAsync(text);
            await page.DisplayAlert("Text Copied",
                "Text copied to clipboard.\nTTS generation only available on Windows.\nAttach audio manually in Audio Library.",
                "OK");
#endif
        }
        // "📝 Yes (no audio yet)" — item already created, nothing more to do
    }

    private static string GetIdeaContentText(string? fullIdea, string title)
    {
        return !string.IsNullOrWhiteSpace(fullIdea)
            ? fullIdea.Trim()
            : title.Trim();
    }

    private async Task SaveAudioLinkAsync(string username, string category)
    {
        try
        {
            var conn = await _db.GetConnectionAsync();
            string key = $"audiolink:{category}";

            if (_audioLinkedCategories.TryGetValue(category, out var audioCategory))
            {
                var existing = await conn.Table<IdeaLoggerSetting>()
                    .FirstOrDefaultAsync(s => s.Username == username && s.Key == key);
                if (existing != null) { existing.Value = audioCategory; await conn.UpdateAsync(existing); }
                else await conn.InsertAsync(new IdeaLoggerSetting { Username = username, Key = key, Value = audioCategory });
            }
            else
            {
                // Remove the link
                await conn.ExecuteAsync("DELETE FROM idea_logger_settings WHERE Username = ? AND Key = ?", username, key);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[IDEA LOGGER] Failed to save audio link: {ex.Message}");
        }
    }
}

using Bannister.Helpers;
using Bannister.Models;
using Bannister.ViewModels;

namespace Bannister.Views;

/// <summary>
/// Partial class containing data loading methods
/// </summary>
public partial class ActivityGamePage
{
    private async Task LoadGameAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] Starting LoadGameAsync, GameId='{GameId}'");
            
            if (string.IsNullOrEmpty(GameId))
            {
                await DisplayAlert("Error", "No game ID provided", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            string username = _auth.CurrentUsername;
            _game = await _games.GetGameAsync(username, GameId);

            if (_game == null)
            {
                await DisplayAlert("Error", $"Game '{GameId}' not found", "OK");
                await Shell.Current.GoToAsync("..");
                return;
            }

            Title = _game.DisplayName;
            lblGameTitle.Text = $"{_game.DisplayName} Game";

            await _exp.EnsureUserStateAsync(username, _game.GameId);
            await LoadDragonAsync();
            
            // IMPORTANT: Check broken streaks FIRST before auto-award
            // This ensures penalties are applied before any bonuses
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] About to call CheckBrokenStreaksAsync");
            await CheckBrokenStreaksAsync(); // Check for broken display day streaks
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] Finished CheckBrokenStreaksAsync");
            
            // Now check auto-award (after broken streaks are handled)
            await CheckAutoAwardActivitiesAsync();
            
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] About to call CheckAndMoveExpiredActivitiesAsync");
            await CheckAndMoveExpiredActivitiesAsync(); // Check for expired activities
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] Finished CheckAndMoveExpiredActivitiesAsync");
            
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] About to call CheckAndMoveStaleActivitiesAsync");
            await CheckAndMoveStaleActivitiesAsync(); // Check for stale activities
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] Finished CheckAndMoveStaleActivitiesAsync");
            
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] About to call CheckHabitTargetsAsync");
            await CheckHabitTargetsAsync(); // Check for habit target decisions and expired targets
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] Finished CheckHabitTargetsAsync");
            
            await RefreshExpAsync();
            await UpdateEscalationTimerAsync(); // Update escalation timer display
            await LoadCategoriesAsync();

            _currentMetaFilter = "All Activities"; // Set default
            metaFilterPicker.SelectedIndex = 0;

            await RefreshActivitiesAsync();
            await CheckAndShowMissingImagesPopup();
            await LoadChartDataAsync();
            
            System.Diagnostics.Debug.WriteLine($"[LOAD GAME] LoadGameAsync complete");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in LoadGameAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Check for expired activities and move them to Expired category.
    /// Shows a notification on first game load of the day if any were moved.
    /// </summary>
    private async Task CheckAndMoveExpiredActivitiesAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[EXPIRED CHECK] Method entered, _game is null: {_game == null}");
        
        if (_game == null) return;

        string lastCheckedKey = $"ExpiredActivitiesCheck_{_auth.CurrentUsername}_{_game.GameId}";
        string lastCheckedDate = Preferences.Get(lastCheckedKey, "");
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        System.Diagnostics.Debug.WriteLine($"[EXPIRED CHECK] Key='{lastCheckedKey}'");
        System.Diagnostics.Debug.WriteLine($"[EXPIRED CHECK] Last checked: '{lastCheckedDate}', Today: '{today}'");

        System.Diagnostics.Debug.WriteLine($"[EXPIRED CHECK] Calling AutoMoveExpiredActivitiesAsync for user='{_auth.CurrentUsername}', game='{_game.GameId}'");
        
        int movedCount = await _activities.AutoMoveExpiredActivitiesAsync(_auth.CurrentUsername, _game.GameId);
        
        System.Diagnostics.Debug.WriteLine($"[EXPIRED CHECK] Moved {movedCount} activities");
        
        Preferences.Set(lastCheckedKey, today);
        
        if (movedCount > 0 && lastCheckedDate != today)
        {
            await DisplayAlert(
                "Expired Activities", 
                $"{movedCount} activity(ies) passed their end date and were moved to the 'Expired' category.\n\n" +
                "Use the 'Expired' filter to view them.",
                "OK");
        }
    }

    /// <summary>
    /// Check for stale activities (not used in 30+ days) and offer to move them.
    /// </summary>
    private async Task CheckAndMoveStaleActivitiesAsync()
    {
        if (_game == null) return;

        string lastCheckedKey = $"StaleActivitiesCheck_{_auth.CurrentUsername}_{_game.GameId}";
        string lastCheckedDate = Preferences.Get(lastCheckedKey, "");
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        // Only check once per day
        if (lastCheckedDate == today) return;

        // Get exp logs first for stale detection
        var expLogs = await _db.GetExpLogsForGameAsync(_auth.CurrentUsername, _game.GameId);

        var staleActivities = await _activities.GetStaleActivitiesAsync(
            _auth.CurrentUsername, 
            _game.GameId, 
            expLogs);

        // Filter out activities already in Stale or Expired categories
        staleActivities = staleActivities
            .Where(a => a.Category != "Stale" && a.Category != "Expired")
            .ToList();

        Preferences.Set(lastCheckedKey, today);

        if (staleActivities.Count > 0)
        {
            // Show confirmation page with toggles for each activity
            var confirmPage = new StaleActivitiesConfirmationPage(
                staleActivities,
                _activities);

            await Navigation.PushModalAsync(confirmPage);
            await confirmPage.GetMovedCountAsync();
        }
    }

    private async Task LoadDragonAsync()
    {
        if (_game == null) return;

        var dragon = await _dragons.GetDragonAsync(_auth.CurrentUsername, _game.GameId);

        if (dragon != null)
        {
            lblDragonTitle.Text = dragon.Title;
            lblDragonSubtitle.Text = dragon.Description ?? "";
            lblDragonDesc.Text = ""; // Hide description when defined
            btnDefineDragon.Text = "Edit Dragon";

            if (!string.IsNullOrEmpty(dragon.ImagePath))
            {
                imgDragon.Source = ImageSource.FromFile(dragon.ImagePath);
            }
            else
            {
                imgDragon.Source = "diet_dragon_default.png";
            }
        }
        else
        {
            lblDragonTitle.Text = $"Your {_game.DisplayName} Dragon";
            lblDragonSubtitle.Text = "This is a placeholder.";
            lblDragonDesc.Text = $"Your {_game.DisplayName} Dragon represents the long-term goal you've struggled to achieve.";
            btnDefineDragon.Text = "Define Dragon";
            imgDragon.Source = "diet_dragon_default.png";
        }
    }

    private async Task LoadCategoriesAsync()
    {
        if (_game == null) return;

        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _game.GameId);

        // Group categories case-insensitively, keeping the first occurrence's casing
        _categories = allActivities
            .Select(a => a.Category ?? "Misc")
            .Where(c => !c.Equals("Expired", StringComparison.OrdinalIgnoreCase) 
                     && !c.Equals("Stale", StringComparison.OrdinalIgnoreCase))
            .GroupBy(c => c.ToLowerInvariant())
            .Select(g => g.First()) // Keep first occurrence's casing
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Always start at index 0
        _currentCategoryIndex = 0;

        if (_categories.Count == 0)
        {
            _categories.Add("Misc");
            lblPageInfo.Text = "No categories";
            btnPrevPage.IsEnabled = false;
            btnNextPage.IsEnabled = false;
        }
        else
        {
            UpdateCategoryDisplay();
        }

        // Temporarily unsubscribe to avoid triggering refresh during setup
        categoryPicker.SelectedIndexChanged -= OnCategoryChanged;
        
        // Category picker gets ONLY actual categories (no "All" or "Expired")
        categoryPicker.ItemsSource = _categories;
        
        // Small delay to ensure ItemsSource is fully set (Windows MAUI fix)
        await Task.Delay(50);
        
        // Set selected index with bounds checking
        if (_categories.Count > 0 && _currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count)
        {
            categoryPicker.SelectedIndex = _currentCategoryIndex;
        }
        
        // Force UI update
        await Dispatcher.DispatchAsync(() => 
        {
            categoryPicker.SelectedIndex = _currentCategoryIndex;
        });
        
        // Re-subscribe after setup
        categoryPicker.SelectedIndexChanged += OnCategoryChanged;
    }

    private async Task LoadChartDataAsync()
    {
        if (_game == null) return;

        try
        {
            var logs = await _db.GetExpLogsForGameAsync(_auth.CurrentUsername, _game.GameId);

            if (logs.Count == 0)
                return;

            logs = logs.OrderBy(l => l.LoggedAt).ToList();

            // Build EXP chart data
            _expChartData.Clear();
            foreach (var log in logs)
            {
                _expChartData.Add(new ChartDataPoint
                {
                    Date = log.LoggedAt,
                    Value = log.TotalExp
                });
            }

            // Build Level chart data
            _levelChartData.Clear();
            foreach (var log in logs)
            {
                _levelChartData.Add(new ChartDataPoint
                {
                    Date = log.LoggedAt,
                    Value = log.LevelAfter
                });
            }

            _expChartView?.Invalidate();
            _levelChartView?.Invalidate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR loading chart data: {ex.Message}");
        }
    }

    private async Task RefreshActivitiesAsync()
    {
        if (_game == null) return;

        // Save current selections and temporary multipliers before refreshing
        var selectedIds = _allActivities.Where(a => a.IsSelected).Select(a => a.Id).ToHashSet();
        var tempMultipliers = _allActivities
            .Where(a => a.TemporaryMultiplier > 1)
            .ToDictionary(a => a.Id, a => a.TemporaryMultiplier);

        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _game.GameId);

        _allActivities = allActivities
            .Select(a => new ActivityGameViewModel(a))
            .ToList();

        // Restore selections and temporary multipliers
        foreach (var activityVM in _allActivities)
        {
            if (selectedIds.Contains(activityVM.Id))
            {
                activityVM.IsSelected = true;
            }
            if (tempMultipliers.TryGetValue(activityVM.Id, out int tempMult))
            {
                activityVM.TemporaryMultiplier = tempMult;
            }
        }

        // Load last used dates
        var expLogs = await _db.GetExpLogsForGameAsync(_auth.CurrentUsername, _game.GameId);

        foreach (var activityVM in _allActivities)
        {
            var lastLog = expLogs
                .Where(log => log.ActivityId == activityVM.Id)
                .OrderByDescending(log => log.LoggedAt)
                .FirstOrDefault();

            activityVM.LastUsedDate = lastLog?.LoggedAt;
        }

        // Load streak info for streak-tracked activities
        await LoadStreakInfoAsync();

        // For Expired and Stale filters, show all activities in those categories regardless of visibility rules
        List<ActivityGameViewModel> filtered;
        
        if (_currentMetaFilter == "Expired")
        {
            // Show all activities in Expired category
            filtered = _allActivities
                .Where(vm => vm.Activity.Category == "Expired")
                .ToList();
        }
        else if (_currentMetaFilter == "Stale")
        {
            // Show all activities in Stale category
            filtered = _allActivities
                .Where(vm => vm.Activity.Category == "Stale")
                .ToList();
        }
        else if (_currentMetaFilter == "Missing Image")
        {
            // Show all activities without images across all categories
            filtered = _allActivities
                .Where(vm => string.IsNullOrEmpty(vm.Activity.ImagePath))
                .ToList();
        }
        else
        {
            var visibleActivities = await _activities.GetVisibleActivitiesAsync(
                _auth.CurrentUsername,
                _game.GameId,
                _currentLevel,
                _showAllActivities);

            filtered = visibleActivities
                .Select(a => _allActivities.First(vm => vm.Id == a.Id))
                .ToList();
        }

        // Set current level on all VMs for percent-based EXP calculation
        foreach (var vm in filtered)
        {
            vm.CurrentLevel = _currentLevel;
        }

        // For "Possible", "Expired", "Stale", and "Missing Image" filters, skip category filtering
        if (_currentMetaFilter != "Possible" && _currentMetaFilter != "Expired" && _currentMetaFilter != "Stale" && _currentMetaFilter != "Missing Image")
        {
            // Apply category filter based on current category (case-insensitive)
            if (_categories.Count > 0 && _currentCategoryIndex >= 0 && _currentCategoryIndex < _categories.Count)
            {
                string currentCategory = _categories[_currentCategoryIndex];
                filtered = filtered.Where(vm => 
                    string.Equals(vm.Category, currentCategory, StringComparison.OrdinalIgnoreCase)).ToList();
            }
        }

        // Apply meta filter (All Activities, Has Multiplier, etc.)
        filtered = ActivityFilterHelper.ApplyMetaFilter(filtered, _currentMetaFilter);
        filtered = ActivityFilterHelper.ApplySorting(filtered, sortPicker?.SelectedItem?.ToString() ?? "Last Used (Recent First)");

        BuildActivitiesGridWithHeaders(filtered);
    }

    /// <summary>
    /// Load streak info for activities that have streak tracking enabled
    /// </summary>
    private async Task LoadStreakInfoAsync()
    {
        if (_game == null) return;

        try
        {
            // Get all active streaks for this game
            var activeStreaks = await _streaks.GetActiveStreaksAsync(_auth.CurrentUsername, _game.GameId);

            foreach (var activityVM in _allActivities)
            {
                if (activityVM.Activity.IsStreakTracked)
                {
                    // Find the active streak for this activity
                    var streak = activeStreaks.FirstOrDefault(s => s.ActivityId == activityVM.Id);
                    
                    if (streak != null)
                    {
                        activityVM.StreakCount = streak.DaysAchieved;
                        activityVM.StreakAttemptNumber = streak.AttemptNumber;
                    }
                    else
                    {
                        // No active streak yet
                        activityVM.StreakCount = 0;
                        activityVM.StreakAttemptNumber = 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR loading streak info: {ex.Message}");
        }
    }

    private void UpdateCategoryDisplay()
    {
        if (_categories.Count == 0) return;

        lblPageInfo.Text = $"{_currentCategoryIndex + 1}/{_categories.Count}: {_categories[_currentCategoryIndex]}";
        btnPrevPage.IsEnabled = _currentCategoryIndex > 0;
        btnNextPage.IsEnabled = _currentCategoryIndex < _categories.Count - 1;

        // Update picker to match current category (no offset needed)
        categoryPicker.SelectedIndex = _currentCategoryIndex;
    }

    private async Task CheckAndShowMissingImagesPopup()
    {
        if (_game == null)
            return;

        string lastShownKey = $"MissingImagesPopup_LastShown_{_auth.CurrentUsername}";
        string lastShownDate = Preferences.Get(lastShownKey, "");
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        if (lastShownDate == today)
            return;

        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _game.GameId);
        
        // Exclude Possible, Expired, and Stale activities from missing images check
        var missingImages = allActivities
            .Where(a => string.IsNullOrEmpty(a.ImagePath) 
                && !a.IsPossible 
                && a.Category != "Expired" 
                && a.Category != "Stale")
            .ToList();

        if (missingImages.Count > 0)
        {
            // Increment prompt count for each activity
            foreach (var activity in missingImages)
            {
                activity.MissingImagePromptCount++;
                await _activities.UpdateActivityAsync(activity);
            }
            
            // Check for activities on their 3rd prompt
            var thirdTimeActivities = missingImages.Where(a => a.MissingImagePromptCount >= 3).ToList();
            
            if (thirdTimeActivities.Count > 0)
            {
                // Show warning for 3rd time activities
                string activityNames = string.Join("\n• ", thirdTimeActivities.Select(a => a.Name));
                
                bool assignImages = await DisplayAlert(
                    "⚠️ Third Time Warning",
                    $"These activities have appeared in the missing images popup 3 times:\n\n• {activityNames}\n\n" +
                    "Would you like to assign images now, or auto-assign a black placeholder?",
                    "Assign Images Now",
                    "Use Black Placeholder");
                
                if (!assignImages)
                {
                    // Auto-assign black image
                    await AssignBlackImageToActivitiesAsync(thirdTimeActivities);
                    
                    // Remove these from the list so they don't appear in the popup
                    missingImages = missingImages.Where(a => a.MissingImagePromptCount < 3).ToList();
                    
                    await DisplayAlert(
                        "Images Assigned",
                        $"Black placeholder images have been assigned to {thirdTimeActivities.Count} activities.",
                        "OK");
                }
            }
            
            // Show popup for remaining activities without images
            var stillMissing = missingImages.Where(a => string.IsNullOrEmpty(a.ImagePath)).ToList();
            
            if (stillMissing.Count > 0)
            {
                Preferences.Set(lastShownKey, today);
                var popup = new ActivitiesMissingImagesPage(_auth, _activities, _game.GameId);
                await Navigation.PushModalAsync(popup);
            }
            else
            {
                Preferences.Set(lastShownKey, today);
            }
        }
    }

    /// <summary>
    /// Create and assign a solid black image to activities.
    /// </summary>
    private async Task AssignBlackImageToActivitiesAsync(List<Activity> activities)
    {
        try
        {
            // Create black image folder if needed
            string imageFolder = Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
            Directory.CreateDirectory(imageFolder);
            
            // Create a single black image file (reuse for all)
            string blackImagePath = Path.Combine(imageFolder, "black_placeholder.png");
            
            if (!File.Exists(blackImagePath))
            {
                // Create a simple 100x100 black PNG
                // PNG header + IHDR + IDAT with black pixels + IEND
                byte[] blackPng = CreateBlackPng(100, 100);
                await File.WriteAllBytesAsync(blackImagePath, blackPng);
            }
            
            // Assign to all activities
            foreach (var activity in activities)
            {
                activity.ImagePath = "black_placeholder.png";
                await _activities.UpdateActivityAsync(activity);
                System.Diagnostics.Debug.WriteLine($"[BLACK IMAGE] Assigned black placeholder to: {activity.Name}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BLACK IMAGE] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a simple solid black PNG image.
    /// </summary>
    private byte[] CreateBlackPng(int width, int height)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        
        // PNG Signature
        bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        
        // IHDR chunk
        var ihdr = new byte[13];
        WriteInt32BE(ihdr, 0, width);
        WriteInt32BE(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // color type (RGB)
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(bw, "IHDR", ihdr);
        
        // IDAT chunk - create raw image data
        using var dataMs = new MemoryStream();
        using var deflate = new System.IO.Compression.DeflateStream(dataMs, System.IO.Compression.CompressionLevel.Fastest, true);
        
        // Write scanlines (filter byte + RGB for each pixel)
        for (int y = 0; y < height; y++)
        {
            deflate.WriteByte(0); // No filter
            for (int x = 0; x < width; x++)
            {
                deflate.WriteByte(0); // R
                deflate.WriteByte(0); // G
                deflate.WriteByte(0); // B
            }
        }
        deflate.Close();
        
        // Wrap in zlib format
        var rawData = dataMs.ToArray();
        var zlibData = new byte[rawData.Length + 6];
        zlibData[0] = 0x78; // CMF
        zlibData[1] = 0x9C; // FLG
        Array.Copy(rawData, 0, zlibData, 2, rawData.Length);
        
        // Adler-32 checksum (simplified - just use 1 for black image)
        uint adler = 1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width * 3 + 1; x++)
            {
                adler = ((adler & 0xFFFF) + 0) % 65521 | (((adler >> 16) + (adler & 0xFFFF)) % 65521 << 16);
            }
        }
        zlibData[zlibData.Length - 4] = (byte)(adler >> 24);
        zlibData[zlibData.Length - 3] = (byte)(adler >> 16);
        zlibData[zlibData.Length - 2] = (byte)(adler >> 8);
        zlibData[zlibData.Length - 1] = (byte)adler;
        
        WriteChunk(bw, "IDAT", zlibData);
        
        // IEND chunk
        WriteChunk(bw, "IEND", Array.Empty<byte>());
        
        return ms.ToArray();
    }

    private void WriteInt32BE(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private void WriteChunk(BinaryWriter bw, string type, byte[] data)
    {
        // Length (big-endian)
        bw.Write((byte)(data.Length >> 24));
        bw.Write((byte)(data.Length >> 16));
        bw.Write((byte)(data.Length >> 8));
        bw.Write((byte)data.Length);
        
        // Type
        bw.Write(System.Text.Encoding.ASCII.GetBytes(type));
        
        // Data
        bw.Write(data);
        
        // CRC32
        uint crc = Crc32(System.Text.Encoding.ASCII.GetBytes(type), data);
        bw.Write((byte)(crc >> 24));
        bw.Write((byte)(crc >> 16));
        bw.Write((byte)(crc >> 8));
        bw.Write((byte)crc);
    }

    private uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        uint[] table = new uint[256];
        
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        
        foreach (byte b in type)
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data)
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        
        return crc ^ 0xFFFFFFFF;
    }

    /// <summary>
    /// Check for activities that need habit target decisions and those with expired targets.
    /// </summary>
    private async Task CheckHabitTargetsAsync()
    {
        if (_game == null) return;

        string lastCheckedKey = $"HabitTargetCheck_{_auth.CurrentUsername}_{_game.GameId}";
        string lastCheckedDate = Preferences.Get(lastCheckedKey, "");
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        // Only check once per day per game
        if (lastCheckedDate == today) return;

        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _game.GameId);
        
        // Find activities with expired habit targets
        var expiredTargets = allActivities
            .Where(a => a.IsHabitTargetExpired && a.IsActive && !a.IsPossible)
            .ToList();

        // Handle expired targets first
        if (expiredTargets.Count > 0)
        {
            var expiredPage = new HabitTargetDecisionPage(_activities, expiredTargets, isExpiredMode: true);
            await Navigation.PushModalAsync(expiredPage);
        }

        // Find activities that need a habit decision (positive EXP, not negative, no decision made)
        var needsDecision = allActivities
            .Where(a => a.NeedsHabitDecision && a.IsActive && !a.IsPossible && a.Category != "Negative")
            .ToList();

        // Prompt for activities needing decision
        if (needsDecision.Count > 0)
        {
            bool showAll = await DisplayAlert(
                "Set Habit Targets",
                $"You have {needsDecision.Count} activities without habit targets.\n\n" +
                "Would you like to set targets for them now?",
                "Yes, show them",
                "Skip for today");

            if (showAll)
            {
                var decisionPage = new HabitTargetDecisionPage(_activities, needsDecision, isExpiredMode: false);
                await Navigation.PushModalAsync(decisionPage);
            }
        }

        Preferences.Set(lastCheckedKey, today);
    }

    /// <summary>
    /// Check for broken display day streaks and apply penalties.
    /// Shows a dedicated page for each broken streak decision.
    /// Also handles level-down for activities with level caps.
    /// </summary>
    private async Task CheckBrokenStreaksAsync()
    {
        if (_game == null) return;

        string lastCheckedKey = $"BrokenStreakCheck_{_auth.CurrentUsername}_{_game.GameId}";
        string lastCheckedDate = Preferences.Get(lastCheckedKey, "");
        string today = DateTime.Now.ToString("yyyy-MM-dd");

        // Only check once per day per game
        if (lastCheckedDate == today) return;

        var brokenStreaks = await _activities.CheckAndBreakMissedStreaksAsync(_auth.CurrentUsername, _game.GameId);

        if (brokenStreaks.Count > 0)
        {
            // Show the dedicated streak broken page
            var streakPage = new StreakBrokenPage(_auth, _activities, _exp, _game.GameId, brokenStreaks);
            await Navigation.PushModalAsync(streakPage);
            await streakPage.GetResultAsync();

            // Get results from the page
            var levelDowns = streakPage.LevelDowns;
            var penaltyDetails = streakPage.PenaltyDetails;
            int totalPenalty = streakPage.TotalPenalty;

            // Show level down summary if any occurred
            if (levelDowns.Count > 0)
            {
                var message = new System.Text.StringBuilder();
                message.AppendLine("Your level was reduced due to broken streaks:\n");
                
                foreach (var (activity, fromLevel, toLevel, expLost) in levelDowns)
                {
                    message.AppendLine($"• {activity.Name}");
                    message.AppendLine($"  Level {fromLevel} → Level {toLevel}");
                    message.AppendLine($"  Lost {expLost:N0} EXP\n");
                }

                message.AppendLine("Rebuild your streak to progress past this level.");

                await DisplayAlert("⚠️ LEVEL DOWN", message.ToString(), "OK");
            }

            // Show summary if multiple streaks broken and penalties applied
            if (penaltyDetails.Count > 1)
            {
                await DisplayAlert(
                    "Streak Penalties Applied",
                    $"{penaltyDetails.Count} streaks were broken.\n\n" +
                    $"Total penalty: {totalPenalty} EXP\n\n" +
                    string.Join("\n", penaltyDetails),
                    "OK");
            }
            else if (penaltyDetails.Count == 1 && levelDowns.Count == 0)
            {
                await DisplayAlert("Penalty Applied", $"Total penalty: {totalPenalty} EXP", "OK");
            }

            // Refresh EXP display
            await RefreshExpAsync();
        }

        Preferences.Set(lastCheckedKey, today);
    }
}

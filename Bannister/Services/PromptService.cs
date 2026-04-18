using Bannister.Models;
using SQLite;

namespace Bannister.Services;

/// <summary>
/// Service for managing prompts and generating random selections.
/// Handles database operations, seeding, and group-based selection logic.
/// </summary>
public class PromptService
{
    private readonly DatabaseService _db;
    private bool _isSeeded = false;
    private static readonly Random _random = new();

    public PromptService(DatabaseService db)
    {
        _db = db;
    }

    #region Database Setup

    /// <summary>
    /// Ensure the prompts table exists and seed if empty
    /// </summary>
    public async Task InitializeAsync()
    {
        var conn = await _db.GetConnectionAsync();
        await conn.CreateTableAsync<PromptItem>();
        await conn.CreateTableAsync<PromptGroup>();
        
        // Check if we need to seed
        var count = await conn.Table<PromptItem>().CountAsync();
        if (count == 0 && !_isSeeded)
        {
            await SeedDefaultPromptsAsync();
            _isSeeded = true;
        }
    }

    /// <summary>
    /// Seed the database with sample prompts for initial use
    /// </summary>
    private async Task SeedDefaultPromptsAsync()
    {
        var seedPrompts = new List<PromptItem>
        {
            // Writing Pack - Hook Group
            new() { PackName = "Writing", GroupName = "Hook", Text = "Start with a provocative question that challenges assumptions", Rating = 4, Probability = 1.0, SecondFromGroupProbability = 0.3 },
            new() { PackName = "Writing", GroupName = "Hook", Text = "Open with a surprising statistic or fact", Rating = 3, Probability = 0.8, SecondFromGroupProbability = 0.3 },
            new() { PackName = "Writing", GroupName = "Hook", Text = "Begin with a vivid sensory description", Rating = 4, Probability = 0.9, SecondFromGroupProbability = 0.3 },
            new() { PackName = "Writing", GroupName = "Hook", Text = "Start in medias res - drop the reader into action", Rating = 5, Probability = 1.0, SecondFromGroupProbability = 0.3 },
            
            // Writing Pack - Structure Group
            new() { PackName = "Writing", GroupName = "Structure", Text = "Use the rule of three for main arguments", Rating = 4, Probability = 1.0, SecondFromGroupProbability = 0.4 },
            new() { PackName = "Writing", GroupName = "Structure", Text = "Build tension through progressive revelation", Rating = 4, Probability = 0.9, SecondFromGroupProbability = 0.4 },
            new() { PackName = "Writing", GroupName = "Structure", Text = "Create parallel structure between sections", Rating = 3, Probability = 0.7, SecondFromGroupProbability = 0.4 },
            
            // Writing Pack - Voice Group
            new() { PackName = "Writing", GroupName = "Voice", Text = "Write as if explaining to a curious friend", Rating = 4, Probability = 1.0, SecondFromGroupProbability = 0.3 },
            new() { PackName = "Writing", GroupName = "Voice", Text = "Vary sentence length for rhythm", Rating = 3, Probability = 0.8, SecondFromGroupProbability = 0.3 },
            new() { PackName = "Writing", GroupName = "Voice", Text = "Use concrete examples instead of abstractions", Rating = 5, Probability = 1.0, SecondFromGroupProbability = 0.3 },
            
            // Writing Pack - Depth Group
            new() { PackName = "Writing", GroupName = "Depth", Text = "Acknowledge the strongest counterargument", Rating = 5, Probability = 1.0, SecondFromGroupProbability = 0.5 },
            new() { PackName = "Writing", GroupName = "Depth", Text = "Connect to a universal human experience", Rating = 4, Probability = 0.9, SecondFromGroupProbability = 0.5 },
            new() { PackName = "Writing", GroupName = "Depth", Text = "Show the stakes - why does this matter?", Rating = 5, Probability = 1.0, SecondFromGroupProbability = 0.5 },
            
            // Writing Pack - Polish Group
            new() { PackName = "Writing", GroupName = "Polish", Text = "Cut the first paragraph - start where it gets interesting", Rating = 4, Probability = 0.8, SecondFromGroupProbability = 0.2 },
            new() { PackName = "Writing", GroupName = "Polish", Text = "Remove every adverb and justify keeping any", Rating = 3, Probability = 0.7, SecondFromGroupProbability = 0.2 },
            new() { PackName = "Writing", GroupName = "Polish", Text = "Read aloud to catch awkward phrasing", Rating = 4, Probability = 0.9, SecondFromGroupProbability = 0.2 },
            
            // Reflection Pack - Self Group
            new() { PackName = "Reflection", GroupName = "Self", Text = "What am I avoiding thinking about?", Rating = 5, Probability = 1.0, SecondFromGroupProbability = 0.3 },
            new() { PackName = "Reflection", GroupName = "Self", Text = "What would I do if I weren't afraid?", Rating = 4, Probability = 0.9, SecondFromGroupProbability = 0.3 },
            new() { PackName = "Reflection", GroupName = "Self", Text = "What story am I telling myself that isn't serving me?", Rating = 5, Probability = 1.0, SecondFromGroupProbability = 0.3 },
            
            // Reflection Pack - Growth Group
            new() { PackName = "Reflection", GroupName = "Growth", Text = "What did I learn this week that changed my mind?", Rating = 4, Probability = 0.9, SecondFromGroupProbability = 0.4 },
            new() { PackName = "Reflection", GroupName = "Growth", Text = "Where am I holding back from my full potential?", Rating = 4, Probability = 0.8, SecondFromGroupProbability = 0.4 },
            new() { PackName = "Reflection", GroupName = "Growth", Text = "What skill would change everything if I mastered it?", Rating = 5, Probability = 1.0, SecondFromGroupProbability = 0.4 },
            
            // Reflection Pack - Gratitude Group
            new() { PackName = "Reflection", GroupName = "Gratitude", Text = "What small thing brought unexpected joy today?", Rating = 3, Probability = 0.8, SecondFromGroupProbability = 0.3 },
            new() { PackName = "Reflection", GroupName = "Gratitude", Text = "Who has helped me that I haven't properly thanked?", Rating = 4, Probability = 0.9, SecondFromGroupProbability = 0.3 },
            
            // Planning Pack - Priority Group
            new() { PackName = "Planning", GroupName = "Priority", Text = "What is the ONE thing that would make everything else easier?", Rating = 5, Probability = 1.0, SecondFromGroupProbability = 0.3 },
            new() { PackName = "Planning", GroupName = "Priority", Text = "What should I stop doing to make room for what matters?", Rating = 4, Probability = 0.9, SecondFromGroupProbability = 0.3 },
            
            // Planning Pack - Execution Group
            new() { PackName = "Planning", GroupName = "Execution", Text = "What's the smallest next action I can take right now?", Rating = 4, Probability = 1.0, SecondFromGroupProbability = 0.4 },
            new() { PackName = "Planning", GroupName = "Execution", Text = "What obstacle will I face and how will I handle it?", Rating = 4, Probability = 0.9, SecondFromGroupProbability = 0.4 },
            new() { PackName = "Planning", GroupName = "Execution", Text = "Who can help me with this?", Rating = 3, Probability = 0.8, SecondFromGroupProbability = 0.4 },
        };

        var conn = await _db.GetConnectionAsync();
        await conn.InsertAllAsync(seedPrompts);
        
        System.Diagnostics.Debug.WriteLine($"✓ Seeded {seedPrompts.Count} default prompts");
    }

    #endregion

    #region CRUD Operations

    /// <summary>
    /// Get all prompts for a pack
    /// </summary>
    public async Task<List<PromptItem>> GetPromptsAsync(string packName)
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PromptItem>()
            .Where(p => p.PackName == packName)
            .OrderBy(p => p.GroupName)
            .ThenByDescending(p => p.Rating)
            .ToListAsync();
    }

    /// <summary>
    /// Get all active prompts for a pack
    /// </summary>
    public async Task<List<PromptItem>> GetActivePromptsAsync(string packName)
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PromptItem>()
            .Where(p => p.PackName == packName && p.IsActive)
            .ToListAsync();
    }

    /// <summary>
    /// Get all available pack names
    /// </summary>
    public async Task<List<string>> GetPackNamesAsync()
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        var prompts = await conn.Table<PromptItem>().ToListAsync();
        return prompts.Select(p => p.PackName).Distinct().OrderBy(n => n).ToList();
    }

    /// <summary>
    /// Get all groups within a pack
    /// </summary>
    public async Task<List<string>> GetGroupNamesAsync(string packName)
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        var prompts = await conn.Table<PromptItem>()
            .Where(p => p.PackName == packName)
            .ToListAsync();
        return prompts.Select(p => p.GroupName).Distinct().OrderBy(n => n).ToList();
    }

    /// <summary>
    /// Add a new prompt
    /// </summary>
    public async Task<PromptItem> AddPromptAsync(PromptItem prompt)
    {
        await InitializeAsync();
        prompt.CreatedAt = DateTime.Now;
        var conn = await _db.GetConnectionAsync();
        await conn.InsertAsync(prompt);
        return prompt;
    }

    /// <summary>
    /// Update an existing prompt
    /// </summary>
    public async Task UpdatePromptAsync(PromptItem prompt)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(prompt);
    }

    /// <summary>
    /// Update a prompt by ID with an action
    /// </summary>
    public async Task UpdatePromptAsync(int promptId, Action<PromptItem> updateAction)
    {
        var conn = await _db.GetConnectionAsync();
        var prompt = await conn.GetAsync<PromptItem>(promptId);
        if (prompt != null)
        {
            updateAction(prompt);
            await conn.UpdateAsync(prompt);
        }
    }

    /// <summary>
    /// Delete a prompt
    /// </summary>
    public async Task DeletePromptAsync(int promptId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<PromptItem>(promptId);
    }

    /// <summary>
    /// Get pack statistics
    /// </summary>
    public async Task<(int total, int active, int groups)> GetPackStatsAsync(string packName)
    {
        await InitializeAsync();
        var prompts = await GetPromptsAsync(packName);
        return (
            prompts.Count,
            prompts.Count(p => p.IsActive),
            prompts.Select(p => p.GroupName).Distinct().Count()
        );
    }

    #endregion

    #region Generation Logic

    /// <summary>
    /// Diminishing factor for selecting additional items at the same priority level.
    /// Each subsequent item at the same priority has (factor^n) chance of being included.
    /// 0.8 means: 1st=100%, 2nd=80%, 3rd=64%, 4th=51%, etc.
    /// </summary>
    private const double SamePriorityDiminishingFactor = 0.8;

    /// <summary>
    /// Generate a random selection of prompts from a pack.
    /// Applies probability filtering, priority ordering, diminishing odds, and group-based diversity rules.
    /// </summary>
    /// <param name="packName">The pack to select from</param>
    /// <param name="minCount">Minimum number of prompts to generate</param>
    /// <param name="maxCount">Maximum number of prompts to generate</param>
    /// <returns>List of selected prompts ordered by priority then random</returns>
    public async Task<List<PromptItem>> GeneratePromptsAsync(string packName, int minCount = 5, int maxCount = 5)
    {
        await InitializeAsync();
        
        // Determine actual count (random between min and max)
        int count = minCount == maxCount ? minCount : _random.Next(minCount, maxCount + 1);
        
        // Get all active prompts for the pack
        var allPrompts = await GetActivePromptsAsync(packName);
        
        if (allPrompts.Count == 0)
            return new List<PromptItem>();
        
        // Get group settings for max per generation
        var groupSettings = await GetGroupSettingsAsync(packName);
        
        // Step 1: Apply probability filter (each prompt rolls against its Probability)
        var probabilityFiltered = allPrompts
            .Where(p => _random.NextDouble() <= p.Probability)
            .ToList();
        
        // If too few passed, use all active prompts
        if (probabilityFiltered.Count < count)
            probabilityFiltered = allPrompts.ToList();
        
        // Step 2: Separate priority items from non-priority items
        var priorityItems = probabilityFiltered
            .Where(p => p.Priority > 0)
            .OrderBy(p => p.Priority)
            .ThenBy(_ => _random.Next()) // Randomize within same priority
            .ToList();
        
        var nonPriorityItems = probabilityFiltered
            .Where(p => p.Priority == 0)
            .OrderBy(_ => _random.Next())
            .ToList();
        
        // Combine: priority items first (in order), then shuffled non-priority
        var shuffled = priorityItems.Concat(nonPriorityItems).ToList();
        
        // Step 3: Apply group-based selection rules with diminishing odds for same priority
        var selected = new List<PromptItem>();
        var groupCounts = new Dictionary<string, int>();
        var groupSecondAllowed = new Dictionary<string, bool>();
        var priorityCounts = new Dictionary<int, int>(); // Track how many selected at each priority
        
        foreach (var prompt in shuffled)
        {
            if (selected.Count >= count)
                break;
            
            string group = prompt.GroupName;
            int currentGroupCount = groupCounts.GetValueOrDefault(group, 0);
            
            // Get max for this group (default 2 if not configured)
            int maxForGroup = groupSettings.GetValueOrDefault(group.ToLowerInvariant(), 2);
            
            // Rule: Check against group's MaxPerGeneration
            if (currentGroupCount >= maxForGroup)
                continue;
            
            // Diminishing odds for same priority level
            // First item at a priority is guaranteed (if it passes other checks)
            // Subsequent items have diminishing chance: factor^(n-1)
            int priority = prompt.Priority;
            int samePriorityCount = priorityCounts.GetValueOrDefault(priority, 0);
            
            if (samePriorityCount > 0)
            {
                double diminishingChance = Math.Pow(SamePriorityDiminishingFactor, samePriorityCount);
                if (_random.NextDouble() > diminishingChance)
                    continue; // Failed diminishing odds roll, skip this prompt
            }
            
            // Rule: First item from group is always allowed
            if (currentGroupCount == 0)
            {
                selected.Add(prompt);
                groupCounts[group] = 1;
                priorityCounts[priority] = samePriorityCount + 1;
                
                // Pre-roll whether second from this group will be allowed (only if max > 1)
                if (maxForGroup > 1)
                    groupSecondAllowed[group] = _random.NextDouble() <= prompt.SecondFromGroupProbability;
            }
            // Rule: Second item only if roll succeeded and max allows it
            else if (currentGroupCount == 1 && maxForGroup > 1 && groupSecondAllowed.GetValueOrDefault(group, false))
            {
                selected.Add(prompt);
                groupCounts[group] = 2;
                priorityCounts[priority] = samePriorityCount + 1;
            }
            // Otherwise skip this prompt
        }
        
        // If we couldn't fill the quota due to group/diminishing rules, try a second pass
        // with relaxed rules (allow second from any group, reset diminishing for non-priority)
        if (selected.Count < count)
        {
            foreach (var prompt in shuffled)
            {
                if (selected.Count >= count)
                    break;
                
                if (selected.Contains(prompt))
                    continue;
                
                string group = prompt.GroupName;
                int currentGroupCount = groupCounts.GetValueOrDefault(group, 0);
                int maxForGroup = groupSettings.GetValueOrDefault(group.ToLowerInvariant(), 2);
                
                // Still enforce max per group
                if (currentGroupCount < maxForGroup)
                {
                    selected.Add(prompt);
                    groupCounts[group] = currentGroupCount + 1;
                }
            }
        }
        
        // Final sort: priority items first (by priority), then non-priority items preserve selection order
        var finalList = new List<PromptItem>();
        finalList.AddRange(selected.Where(p => p.Priority > 0).OrderBy(p => p.Priority));
        finalList.AddRange(selected.Where(p => p.Priority == 0));
        
        // Update usage statistics
        var conn = await _db.GetConnectionAsync();
        foreach (var prompt in finalList)
        {
            prompt.LastUsedAt = DateTime.Now;
            prompt.UsageCount++;
            await conn.UpdateAsync(prompt);
        }
        
        return finalList;
    }

    #endregion

    #region Group Settings

    /// <summary>
    /// Get group settings as a dictionary (groupName lowercase -> maxPerGeneration)
    /// </summary>
    private async Task<Dictionary<string, int>> GetGroupSettingsAsync(string packName)
    {
        var conn = await _db.GetConnectionAsync();
        var groups = await conn.Table<PromptGroup>()
            .Where(g => g.PackName == packName)
            .ToListAsync();
        
        return groups.ToDictionary(
            g => g.GroupName.ToLowerInvariant(), 
            g => g.MaxPerGeneration);
    }

    /// <summary>
    /// Get all group settings for a pack
    /// </summary>
    public async Task<List<PromptGroup>> GetGroupsAsync(string packName)
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PromptGroup>()
            .Where(g => g.PackName == packName)
            .OrderBy(g => g.GroupName)
            .ToListAsync();
    }

    /// <summary>
    /// Get or create a group setting
    /// </summary>
    public async Task<PromptGroup> GetOrCreateGroupAsync(string packName, string groupName)
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        
        var existing = await conn.Table<PromptGroup>()
            .Where(g => g.PackName == packName && g.GroupName == groupName)
            .FirstOrDefaultAsync();
        
        if (existing != null)
            return existing;
        
        var newGroup = new PromptGroup
        {
            PackName = packName,
            GroupName = groupName,
            MaxPerGeneration = 2 // default
        };
        await conn.InsertAsync(newGroup);
        return newGroup;
    }

    /// <summary>
    /// Set max per generation for a group
    /// </summary>
    public async Task SetGroupMaxAsync(string packName, string groupName, int max)
    {
        var group = await GetOrCreateGroupAsync(packName, groupName);
        group.MaxPerGeneration = Math.Max(1, max); // minimum 1
        var conn = await _db.GetConnectionAsync();
        await conn.UpdateAsync(group);
    }

    /// <summary>
    /// Ensure all groups used in prompts have a settings entry
    /// </summary>
    public async Task SyncGroupSettingsAsync(string packName)
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        
        // Get all distinct group names from prompts
        var prompts = await conn.Table<PromptItem>()
            .Where(p => p.PackName == packName)
            .ToListAsync();
        var groupNames = prompts.Select(p => p.GroupName).Distinct().ToList();
        
        // Get existing group settings
        var existingGroups = await conn.Table<PromptGroup>()
            .Where(g => g.PackName == packName)
            .ToListAsync();
        var existingNames = existingGroups.Select(g => g.GroupName).ToHashSet();
        
        // Create missing entries
        foreach (var name in groupNames)
        {
            if (!existingNames.Contains(name))
            {
                await conn.InsertAsync(new PromptGroup
                {
                    PackName = packName,
                    GroupName = name,
                    MaxPerGeneration = 2
                });
            }
        }
    }

    #endregion

    #region Bulk Operations

    /// <summary>
    /// Import prompts from a list (for future JSON import feature)
    /// </summary>
    public async Task ImportPromptsAsync(List<PromptItem> prompts)
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        
        foreach (var prompt in prompts)
        {
            prompt.CreatedAt = DateTime.Now;
        }
        
        await conn.InsertAllAsync(prompts);
    }

    /// <summary>
    /// Clear all prompts for a pack (for reimporting)
    /// </summary>
    public async Task ClearPackAsync(string packName)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "DELETE FROM prompt_items WHERE PackName = ?", 
            packName);
    }

    /// <summary>
    /// Rename a pack (updates all prompts with the old pack name)
    /// </summary>
    public async Task RenamePackAsync(string oldName, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || oldName == newName)
            return;
            
        var conn = await _db.GetConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE prompt_items SET PackName = ? WHERE PackName = ?",
            newName.Trim(), oldName);
    }

    /// <summary>
    /// Archive a prompt (soft delete - keeps in All Prompts)
    /// </summary>
    public async Task ArchivePromptAsync(int promptId)
    {
        var conn = await _db.GetConnectionAsync();
        var prompt = await conn.GetAsync<PromptItem>(promptId);
        if (prompt != null)
        {
            prompt.IsArchived = true;
            prompt.IsActive = false;
            await conn.UpdateAsync(prompt);
        }
    }

    /// <summary>
    /// Restore an archived prompt
    /// </summary>
    public async Task RestorePromptAsync(int promptId)
    {
        var conn = await _db.GetConnectionAsync();
        var prompt = await conn.GetAsync<PromptItem>(promptId);
        if (prompt != null)
        {
            prompt.IsArchived = false;
            prompt.IsActive = true;
            await conn.UpdateAsync(prompt);
        }
    }

    /// <summary>
    /// Move prompt to a different category/group within the same pack
    /// </summary>
    public async Task MoveToGroupAsync(int promptId, string newGroupName)
    {
        var conn = await _db.GetConnectionAsync();
        var prompt = await conn.GetAsync<PromptItem>(promptId);
        if (prompt != null)
        {
            prompt.GroupName = newGroupName.Trim();
            await conn.UpdateAsync(prompt);
        }
    }

    /// <summary>
    /// Copy prompt to another pack
    /// </summary>
    public async Task CopyToPackAsync(int promptId, string targetPackName, string? targetGroupName = null)
    {
        var conn = await _db.GetConnectionAsync();
        var original = await conn.GetAsync<PromptItem>(promptId);
        if (original != null)
        {
            var copy = new PromptItem
            {
                Text = original.Text,
                PackName = targetPackName.Trim(),
                GroupName = targetGroupName?.Trim() ?? original.GroupName,
                Rating = original.Rating,
                Probability = original.Probability,
                SecondFromGroupProbability = original.SecondFromGroupProbability,
                IsActive = true,
                IsArchived = false,
                Priority = 0,
                Notes = original.Notes,
                CreatedAt = DateTime.Now
            };
            await conn.InsertAsync(copy);
        }
    }

    /// <summary>
    /// Get all prompts including archived (for All Prompts view)
    /// </summary>
    public async Task<List<PromptItem>> GetAllPromptsAsync()
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        return await conn.Table<PromptItem>()
            .OrderBy(p => p.PackName)
            .ThenBy(p => p.GroupName)
            .ToListAsync();
    }

    /// <summary>
    /// Get only archived prompts
    /// </summary>
    public async Task<List<PromptItem>> GetArchivedPromptsAsync()
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        var all = await conn.Table<PromptItem>().ToListAsync();
        return all
            .Where(p => p.IsArchived)
            .OrderByDescending(p => p.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Get prompts for a pack (excludes archived)
    /// </summary>
    public async Task<List<PromptItem>> GetPackPromptsAsync(string packName)
    {
        await InitializeAsync();
        var conn = await _db.GetConnectionAsync();
        // Note: IsArchived may be null for existing rows, so we check != true
        var all = await conn.Table<PromptItem>()
            .Where(p => p.PackName == packName)
            .ToListAsync();
        return all
            .Where(p => !p.IsArchived)
            .OrderBy(p => p.GroupName)
            .ThenByDescending(p => p.Rating)
            .ToList();
    }

    /// <summary>
    /// Permanently delete a prompt (use sparingly)
    /// </summary>
    public async Task DeletePermanentlyAsync(int promptId)
    {
        var conn = await _db.GetConnectionAsync();
        await conn.DeleteAsync<PromptItem>(promptId);
    }

    #endregion
}

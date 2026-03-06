using SQLite;
using ConversationPractice.Models;

namespace ConversationPractice.Services;

/// <summary>
/// Service for managing conversation scenarios and practice sessions
/// Designed to be standalone - no dependencies on Bannister services
/// </summary>
public class ConversationService
{
    private readonly string _dbPath;
    private SQLiteAsyncConnection? _db;

    public ConversationService(string dbPath)
    {
        _dbPath = dbPath;
    }

    private async Task InitAsync()
    {
        if (_db != null) return;

        // Store DateTime as readable ISO8601 strings instead of ticks
        _db = new SQLiteAsyncConnection(_dbPath, storeDateTimeAsTicks: false);
        
        // Create tables
        await _db.CreateTableAsync<Conversation>();
        await _db.CreateTableAsync<PracticeSession>();
        await _db.CreateTableAsync<ConversationMessage>();
        await _db.CreateTableAsync<ConversationNode>();
        
        // Seed default scenarios if none exist
        await SeedDefaultScenariosAsync();
    }

    #region Conversation CRUD

    public async Task<List<Conversation>> GetConversationsAsync(string? username = null)
    {
        await InitAsync();
        
        if (username == null)
        {
            return await _db!.Table<Conversation>()
                .Where(c => c.IsActive)
                .OrderBy(c => c.ScenarioType)
                .ThenBy(c => c.Title)
                .ToListAsync();
        }
        
        return await _db!.Table<Conversation>()
            .Where(c => c.IsActive && c.Username == username)
            .OrderBy(c => c.ScenarioType)
            .ThenBy(c => c.Title)
            .ToListAsync();
    }

    public async Task<Conversation?> GetConversationAsync(int id)
    {
        await InitAsync();
        return await _db!.Table<Conversation>()
            .Where(c => c.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<Conversation> CreateConversationAsync(Conversation conversation)
    {
        await InitAsync();
        conversation.CreatedAt = DateTime.UtcNow;
        await _db!.InsertAsync(conversation);
        return conversation;
    }

    public async Task UpdateConversationAsync(Conversation conversation)
    {
        await InitAsync();
        await _db!.UpdateAsync(conversation);
    }

    public async Task DeleteConversationAsync(int id)
    {
        await InitAsync();
        var conversation = await GetConversationAsync(id);
        if (conversation != null)
        {
            conversation.IsActive = false;
            await _db!.UpdateAsync(conversation);
        }
    }

    #endregion

    #region Practice Sessions

    public async Task<PracticeSession> CreateSessionAsync(int conversationId, string? username = null)
    {
        await InitAsync();
        
        var session = new PracticeSession
        {
            ConversationId = conversationId,
            Username = username,
            StartedAt = DateTime.UtcNow
        };
        
        await _db!.InsertAsync(session);
        return session;
    }

    public async Task UpdateSessionAsync(PracticeSession session)
    {
        await InitAsync();
        await _db!.UpdateAsync(session);
    }

    public async Task CompleteSessionAsync(int sessionId, int? rating = null, string? notes = null)
    {
        await InitAsync();
        
        var session = await _db!.GetAsync<PracticeSession>(sessionId);
        session.EndedAt = DateTime.UtcNow;
        session.DurationSeconds = (int)(session.EndedAt.Value - session.StartedAt).TotalSeconds;
        session.Completed = true;
        session.Rating = rating;
        session.Notes = notes;
        
        await _db!.UpdateAsync(session);
        
        // Update conversation stats
        var conversation = await _db!.GetAsync<Conversation>(session.ConversationId);
        conversation.TimesCompleted++;
        conversation.LastPracticedAt = DateTime.UtcNow;
        await _db!.UpdateAsync(conversation);
    }

    public async Task<List<PracticeSession>> GetSessionsForConversationAsync(int conversationId)
    {
        await InitAsync();
        return await _db!.Table<PracticeSession>()
            .Where(s => s.ConversationId == conversationId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync();
    }

    #endregion

    #region Messages

    public async Task<ConversationMessage> AddMessageAsync(int sessionId, string role, string content)
    {
        await InitAsync();
        
        var message = new ConversationMessage
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        };
        
        await _db!.InsertAsync(message);
        
        // Update session message count
        var session = await _db!.GetAsync<PracticeSession>(sessionId);
        session.MessageCount++;
        await _db!.UpdateAsync(session);
        
        return message;
    }

    public async Task<List<ConversationMessage>> GetMessagesForSessionAsync(int sessionId)
    {
        await InitAsync();
        return await _db!.Table<ConversationMessage>()
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    #endregion

    #region Conversation Nodes (Tree Structure)

    public async Task<List<ConversationNode>> GetNodesForConversationAsync(int conversationId)
    {
        await InitAsync();
        return await _db!.Table<ConversationNode>()
            .Where(n => n.ConversationId == conversationId)
            .OrderBy(n => n.SortOrder)
            .ToListAsync();
    }

    public async Task<List<ConversationNode>> GetChildNodesAsync(int? parentNodeId, int conversationId)
    {
        await InitAsync();
        return await _db!.Table<ConversationNode>()
            .Where(n => n.ConversationId == conversationId && n.ParentNodeId == parentNodeId)
            .OrderBy(n => n.SortOrder)
            .ToListAsync();
    }

    public async Task<ConversationNode> GetNodeAsync(int nodeId)
    {
        await InitAsync();
        return await _db!.GetAsync<ConversationNode>(nodeId);
    }

    public async Task<ConversationNode> CreateNodeAsync(ConversationNode node)
    {
        await InitAsync();
        
        // Get next sort order for siblings
        var siblings = await GetChildNodesAsync(node.ParentNodeId, node.ConversationId);
        node.SortOrder = siblings.Count > 0 ? siblings.Max(n => n.SortOrder) + 1 : 0;
        
        await _db!.InsertAsync(node);
        return node;
    }

    public async Task UpdateNodeAsync(ConversationNode node)
    {
        await InitAsync();
        await _db!.UpdateAsync(node);
    }

    public async Task DeleteNodeAsync(int nodeId)
    {
        await InitAsync();
        
        // Delete this node and all its descendants recursively
        var children = await _db!.Table<ConversationNode>()
            .Where(n => n.ParentNodeId == nodeId)
            .ToListAsync();
        
        foreach (var child in children)
        {
            await DeleteNodeAsync(child.Id);
        }
        
        await _db!.DeleteAsync<ConversationNode>(nodeId);
    }

    public async Task<ConversationNode?> GetRandomChildNodeAsync(int? parentNodeId, int conversationId)
    {
        await InitAsync();
        var children = await GetChildNodesAsync(parentNodeId, conversationId);
        
        if (children.Count == 0)
            return null;
        
        // Random selection
        var random = new Random();
        var selected = children[random.Next(children.Count)];
        
        // Increment times reached
        selected.TimesReached++;
        await _db!.UpdateAsync(selected);
        
        return selected;
    }

    #endregion

    #region EXP and Leveling System

    /// <summary>
    /// Award EXP to a conversation and level it up if needed
    /// Also increments TimesCompleted and updates LastPracticedAt
    /// Max level is 100
    /// </summary>
    public async Task<(int newLevel, int newLevelExp, bool leveledUp)> AwardExpAsync(int conversationId, int expToAdd)
    {
        await InitAsync();
        
        var conversation = await _db!.GetAsync<Conversation>(conversationId);
        
        // Add EXP
        conversation.TotalExp += expToAdd;
        conversation.CurrentLevelExp += expToAdd;
        
        bool leveledUp = false;
        
        // Check for level ups (can level up multiple times if huge EXP award)
        while (conversation.CurrentLevel < 100 && conversation.CurrentLevelExp >= GetExpForNextLevel(conversation.CurrentLevel))
        {
            int expNeeded = GetExpForNextLevel(conversation.CurrentLevel);
            conversation.CurrentLevelExp -= expNeeded;
            conversation.CurrentLevel++;
            leveledUp = true;
        }
        
        // Cap at level 100
        if (conversation.CurrentLevel >= 100)
        {
            conversation.CurrentLevel = 100;
            conversation.CurrentLevelExp = 0; // No overflow at max level
        }

        // Update practice stats
        conversation.TimesCompleted++;
        conversation.LastPracticedAt = DateTime.UtcNow;
        
        await _db!.UpdateAsync(conversation);
        
        return (conversation.CurrentLevel, conversation.CurrentLevelExp, leveledUp);
    }

    /// <summary>
    /// Get EXP required to reach next level
    /// Uses formula: 100 * level (so level 1->2 needs 100, level 2->3 needs 200, etc.)
    /// </summary>
    public int GetExpForNextLevel(int currentLevel)
    {
        if (currentLevel >= 100)
            return 0; // Max level
        
        return 100 * currentLevel;
    }

    /// <summary>
    /// Get total EXP needed to reach a specific level from level 1
    /// </summary>
    public int GetTotalExpForLevel(int level)
    {
        if (level <= 1)
            return 0;
        
        int total = 0;
        for (int i = 1; i < level; i++)
        {
            total += GetExpForNextLevel(i);
        }
        return total;
    }

    #endregion

    #region Default Scenarios

    private async Task SeedDefaultScenariosAsync()
    {
        var existing = await _db!.Table<Conversation>().CountAsync();
        if (existing > 0) return;

        var defaults = new List<Conversation>
        {
            new Conversation
            {
                ScenarioType = "Job Interview",
                Title = "Software Engineer - Technical Interview",
                Description = "Practice technical interview questions for a senior software engineer position",
                UserRole = "Job Candidate",
                AiRole = "Senior Engineering Manager",
                Icon = "💼",
                DifficultyLevel = 4,
                IsTemplate = true,
                SystemPrompt = @"You are a senior engineering manager conducting a technical interview for a software engineer position. Ask challenging but fair technical questions about algorithms, system design, and coding. Be professional, encouraging, and provide helpful feedback. Start by introducing yourself and asking the candidate to tell you about themselves."
            },
            new Conversation
            {
                ScenarioType = "Sales Call",
                Title = "Cold Call - SaaS Product",
                Description = "Practice making a cold call to sell a SaaS product to a busy executive",
                UserRole = "Sales Representative",
                AiRole = "Busy Executive",
                Icon = "📞",
                DifficultyLevel = 5,
                IsTemplate = true,
                SystemPrompt = @"You are a busy executive who receives many sales calls. You're skeptical, short on time, and will hang up unless the sales rep quickly demonstrates value. You have objections like 'we already have a solution', 'not in the budget', 'call me next quarter'. If the rep is persistent, asks good questions, and shows genuine understanding of your business needs, you'll gradually become more interested. Start by answering 'This is [name], I only have a minute, what's this about?'"
            },
            new Conversation
            {
                ScenarioType = "Customer Service",
                Title = "Angry Customer - Product Defect",
                Description = "Handle an angry customer who received a defective product",
                UserRole = "Customer Service Rep",
                AiRole = "Angry Customer",
                Icon = "😠",
                DifficultyLevel = 4,
                IsTemplate = true,
                SystemPrompt = @"You are an angry customer who just received a defective product. You're frustrated, feel like you wasted money, and want immediate resolution. You'll be hostile at first but will calm down if the rep shows empathy, takes responsibility, and offers a genuine solution. Start by saying 'I'm furious! I just opened the package and this thing is completely broken!'"
            },
            new Conversation
            {
                ScenarioType = "Negotiation",
                Title = "Salary Negotiation",
                Description = "Practice negotiating your salary after receiving a job offer",
                UserRole = "Job Candidate",
                AiRole = "HR Manager",
                Icon = "💰",
                DifficultyLevel = 3,
                IsTemplate = true,
                SystemPrompt = @"You are an HR manager who has extended a job offer. You have some flexibility on salary but need to stay within budget. You'll ask the candidate to justify why they deserve more, and you'll push back initially but may be willing to meet somewhere in the middle if they make a strong case. Start by saying 'Congratulations! We'd like to offer you the position at $X. What do you think?'"
            },
            new Conversation
            {
                ScenarioType = "Difficult Conversation",
                Title = "Giving Critical Feedback to Team Member",
                Description = "Practice giving difficult performance feedback to an underperforming team member",
                UserRole = "Manager",
                AiRole = "Team Member",
                Icon = "📋",
                DifficultyLevel = 4,
                IsTemplate = true,
                SystemPrompt = @"You are a team member who has been underperforming but doesn't realize it. You might get defensive when receiving criticism. You'll respond better to specific examples, empathy, and a collaborative approach to improvement rather than harsh judgment. Start by saying 'Hi, you wanted to talk to me?'"
            }
        };

        foreach (var scenario in defaults)
        {
            await _db!.InsertAsync(scenario);
        }
    }

    #endregion
}

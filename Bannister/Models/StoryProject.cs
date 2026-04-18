using SQLite;

namespace Bannister.Models;

[Table("story_projects")]
public class StoryProject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public string Username { get; set; } = "";
    
    public string Name { get; set; } = "";
    
    public string Description { get; set; } = "";
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? CompletedAt { get; set; }
    
    // Status: "active", "completed", "archived"
    public string Status { get; set; } = "active";
    
    // Draft versioning: null = original, otherwise points to parent project
    public int? ParentProjectId { get; set; } = null;
    
    // Draft version number (1 = original, 2+ = drafts)
    public int DraftVersion { get; set; } = 1;
    
    // Source of this draft: "manual", "ai-import", etc.
    public string DraftSource { get; set; } = "manual";
    
    // Is this the "latest" working draft for the project family?
    public bool IsLatest { get; set; } = false;
    
    // Manual comparison override: null = auto-compare to previous version
    public int? CompareToProjectId { get; set; } = null;
    
    // === PUBLICATION TRACKING ===
    
    // Is this project published?
    public bool IsPublished { get; set; } = false;
    
    // Date when published
    public DateTime? PublishedAt { get; set; }
    
    // Initial projection: expected number of clips
    public int ProjectedClipCount { get; set; } = 0;
    
    // Initial projection: expected production days
    public int ProjectedDays { get; set; } = 0;
    
    // Actual final clip count (set on publish)
    public int FinalClipCount { get; set; } = 0;
    
    [Ignore]
    public bool IsActive => Status == "active";
    
    [Ignore]
    public bool IsCompleted => Status == "completed";
    
    [Ignore]
    public bool IsDraft => ParentProjectId != null;
    
    [Ignore]
    public int ActualDays => IsPublished && PublishedAt.HasValue 
        ? (int)(PublishedAt.Value.Date - CreatedAt.Date).TotalDays + 1
        : (int)(DateTime.UtcNow.Date - CreatedAt.Date).TotalDays + 1;
    
    [Ignore]
    public string DisplayName => DraftVersion > 1 ? $"{Name} (Draft v{DraftVersion})" : Name;
}

[Table("story_lines")]
public class StoryLine
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }
    
    [Indexed]
    public int ProjectId { get; set; }
    
    // Order in the story
    public int LineOrder { get; set; }
    
    // The script/narration text
    public string LineText { get; set; } = "";
    
    // Visual description or notes for this line
    public string VisualDescription { get; set; } = "";
    
    // Path to visual asset (image/video)
    public string VisualAssetPath { get; set; } = "";
    
    // Has the visual been prepared?
    public bool VisualPrepared { get; set; } = false;
    
    // Silent line: visual-only, no narration
    public bool IsSilent { get; set; } = false;
    
    // Optional: duration in seconds for this segment
    public int DurationSeconds { get; set; } = 0;
    
    // Optional: notes for editing/production
    public string ProductionNotes { get; set; } = "";
    
    // === SHOT BREAKDOWN & PROMPTS ===
    
    // JSON array of shots: [{"desc":"shot 1","imagePrompt":"...","videoPrompt":"...","assetPath":"...","done":false}, ...]
    public string ShotsJson { get; set; } = "";
    
    // Quick access: how many shots does this visual have? (0 = single shot, not broken down)
    public int ShotCount { get; set; } = 0;
    
    // For simple single-shot visuals, store prompts directly
    public string ImagePrompt { get; set; } = "";      // ChatGPT/DALL-E/Midjourney prompt for starting frame
    public string VideoPrompt { get; set; } = "";      // Luma/Runway prompt for video generation
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public DateTime? ModifiedAt { get; set; }
    
    [Ignore]
    public bool HasVisualAsset => !string.IsNullOrEmpty(VisualAssetPath);
    
    [Ignore]
    public bool HasShots => ShotCount > 0;
    
    [Ignore]
    public bool HasPrompts => !string.IsNullOrEmpty(ImagePrompt) || !string.IsNullOrEmpty(VideoPrompt);
}

/// <summary>
/// Represents a single shot within a visual breakdown
/// </summary>
public class VisualShot
{
    public int Index { get; set; }
    public string Description { get; set; } = "";
    public string ImagePrompt { get; set; } = "";
    public string VideoPrompt { get; set; } = "";
    public string AssetPath { get; set; } = "";
    public bool Done { get; set; } = false;
    
    // Task tracking
    public bool Task1_ImageGenerated { get; set; } = false;   // Got the starting frame image
    public bool Task2_VideoGenerated { get; set; } = false;   // Generated video from image
    
    // Computed
    public bool AllTasksDone => Task1_ImageGenerated && Task2_VideoGenerated;
}

using SQLite;

namespace Bannister.Models;

/// <summary>
/// An image production project — stores the story/video concept
/// and tracks progress through image generation workflows.
/// </summary>
[Table("image_projects")]
public class ImageProject
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public string Username { get; set; } = "";

    /// <summary>Project name / title</summary>
    public string Name { get; set; } = "";

    /// <summary>The full story or video concept pasted by the user</summary>
    public string StoryDescription { get; set; } = "";

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ModifiedAt { get; set; }

    /// <summary>Status: 0=Active, 1=Completed, 2=Archived</summary>
    public int Status { get; set; } = 0;

    /// <summary>Locked idea text for the current workflow (persists across restarts)</summary>
    public string? LockedIdea { get; set; }

    /// <summary>Which workflow the locked idea belongs to (e.g. "hook_frames", "clip_start")</summary>
    public string? LockedWorkflow { get; set; }

    /// <summary>Scene description for clip-specific workflows</summary>
    public string? SceneContext { get; set; }
}

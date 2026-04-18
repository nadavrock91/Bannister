namespace Bannister.Services;

/// <summary>
/// Shared prompt templates for story production exports.
/// Used by both StoryProductionPage and StoryPointsPage.
/// </summary>
public static class StoryPromptTemplates
{
    /// <summary>
    /// Gets the C# format instructions for LLM to generate importable story drafts.
    /// </summary>
    public static string GetDraftFormatInstructions()
    {
        return @"---
Output ONLY a C# code block with the full story in this exact format:

```csharp
// NARRATION
lines[1].Script = ""The narration text"";
lines[1].Visual = ""What the viewer sees"";
lines[1].Shots = ""shot1: description | shot2: description"";  // If multiple shots needed
lines[1].Shot1_ImagePrompt = ""Detailed prompt for shot 1 image generation"";
lines[1].Shot1_VideoPrompt = ""Camera motion and action prompt for shot 1 video"";
lines[1].Shot2_ImagePrompt = ""Detailed prompt for shot 2 image generation"";
lines[1].Shot2_VideoPrompt = ""Camera motion and action prompt for shot 2 video"";
lines[1].ImagePrompt = ""Line-level image prompt (for reference/fallback)"";
lines[1].VideoPrompt = ""Line-level video prompt (for reference/fallback)"";

// VISUAL-ONLY
lines[2].Script = """";
lines[2].Visual = ""Silent moment description"";
lines[2].Shots = ""shot1: single shot"";
lines[2].Shot1_ImagePrompt = ""..."";
lines[2].Shot1_VideoPrompt = ""..."";
lines[2].ImagePrompt = ""..."";
lines[2].VideoPrompt = ""..."";
```

RULES:
- Output ALL lines, numbered sequentially
- Use // NARRATION or // VISUAL-ONLY before each line
- Lines marked ✓ LOCKED: Do NOT change Script, Visual, Shots, or any prompts - copy them exactly as-is
- Lines with existing Shots/Prompts: If you don't change the Visual, keep all existing prompts exactly
- Only generate new prompts for lines where you change the Visual or where prompts are missing
- For complex visuals (montages, multiple scenes), break into shots separated by |
- Shot#_ImagePrompt: Write detailed prompts suitable for DALL-E/Midjourney for EACH shot (describe scene, style, lighting, composition)
- Shot#_VideoPrompt: Write prompts for Luma/Runway for EACH shot (describe camera movement, action, transitions)
- ImagePrompt/VideoPrompt: Line-level prompts for reference or single-shot visuals
- No commentary outside the code block";
    }

    /// <summary>
    /// Gets the surgical edit format instructions for LLM to generate specific changes.
    /// </summary>
    public static string GetSurgicalEditFormatInstructions()
    {
        return @"---
Output ONLY a C# code block with surgical edit commands in this exact format:

```csharp
// SURGICAL EDIT COMMANDS
// Draft: ""[DRAFT_NAME]""
// Lines: [TOTAL_LINE_COUNT]

// DELETE line 5
delete[5];

// UPDATE line 3 (only include fields you're changing)
update[3].Script = ""New script text"";
update[3].Visual = ""New visual description"";

// INSERT new line after line 7
insert[7].Script = ""New line script"";
insert[7].Visual = ""New line visual"";
insert[7].Shots = ""shot1: description"";
insert[7].ImagePrompt = ""..."";
insert[7].VideoPrompt = ""..."";

// REORDER: move line 10 to position 3
reorder[10] = 3;
```

RULES:
- Start with // Draft: ""[exact draft name]"" and // Lines: [count] for validation
- DELETE: Use delete[N]; to remove line N
- UPDATE: Use update[N].Field = ""value""; - only include fields being changed
- INSERT: Use insert[N].Field = ""value""; - inserts AFTER line N (use insert[0] for start)
- REORDER: Use reorder[from] = to; - moves line from position to new position
- Line numbers refer to CURRENT positions before any changes
- Generate prompts for any new or changed visuals
- No commentary outside the code block";
    }

    /// <summary>
    /// Gets the reorder format instructions for LLM to reorder points.
    /// </summary>
    public static string GetReorderFormatInstructions(int pointCount)
    {
        return $@"---
Output ONLY a C# code block with the reordered numbers in this exact format:

```csharp
// REORDERED POINTS (most important first)
int[] order = {{ 3, 1, 5, 2, 4 }};
```

RULES:
- Output ALL numbers from 1 to {pointCount}
- Most important/foundational points first
- No commentary outside the code block";
    }

    /// <summary>
    /// Builds a prompt for creating a new draft from story points.
    /// </summary>
    public static string BuildDraftFromPointsPrompt(
        string projectName,
        IEnumerable<string> activePoints,
        IEnumerable<string> possiblePoints,
        IEnumerable<string> irrelevantPoints)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"Create a video essay script for: {projectName}");
        sb.AppendLine();
        
        var activeList = activePoints.ToList();
        var possibleList = possiblePoints.ToList();
        var irrelevantList = irrelevantPoints.ToList();
        
        if (activeList.Count > 0)
        {
            sb.AppendLine("MUST INCLUDE (Active Points):");
            for (int i = 0; i < activeList.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {activeList[i]}");
            }
            sb.AppendLine();
        }
        
        if (possibleList.Count > 0)
        {
            sb.AppendLine("CONSIDER INCLUDING (Possible Points):");
            for (int i = 0; i < possibleList.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {possibleList[i]}");
            }
            sb.AppendLine();
        }
        
        if (irrelevantList.Count > 0)
        {
            sb.AppendLine("DO NOT INCLUDE (Irrelevant Points):");
            for (int i = 0; i < irrelevantList.Count; i++)
            {
                sb.AppendLine($"{i + 1}. {irrelevantList[i]}");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine(GetDraftFormatInstructions());
        
        return sb.ToString();
    }

    /// <summary>
    /// Builds a prompt for surgical edits to an existing draft.
    /// </summary>
    public static string BuildSurgicalEditPrompt(
        string draftName,
        int lineCount,
        string draftSummary,
        string userInstructions)
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"// Draft: \"{draftName}\"");
        sb.AppendLine($"// Lines: {lineCount}");
        sb.AppendLine();
        sb.AppendLine("CURRENT DRAFT SUMMARY:");
        sb.AppendLine(draftSummary);
        sb.AppendLine();
        sb.AppendLine("REQUESTED CHANGES:");
        sb.AppendLine(userInstructions);
        sb.AppendLine();
        sb.AppendLine(GetSurgicalEditFormatInstructions());
        
        return sb.ToString();
    }

    /// <summary>
    /// Builds a prompt for revising an existing draft.
    /// </summary>
    public static string BuildRevisionPrompt(string projectName, string existingDraft, string instruction = "Revise or complete this story.")
    {
        var sb = new System.Text.StringBuilder();
        
        sb.AppendLine($"// Story: {projectName}");
        sb.AppendLine();
        sb.AppendLine("// CURRENT DRAFT:");
        sb.AppendLine("```csharp");
        sb.AppendLine(existingDraft);
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine(instruction);
        sb.AppendLine(GetDraftFormatInstructions());
        
        return sb.ToString();
    }
}

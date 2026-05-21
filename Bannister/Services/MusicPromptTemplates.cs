namespace Bannister.Services;

public static class MusicPromptTemplates
{
    public static string GetDraftFormatInstructions()
    {
        return @"---
Output ONLY one C# code block with the story split into Script and Visual lines in this exact format:

```csharp
// NARRATION
lines[1].Script = ""The narration text for this line"";
lines[1].Visual = ""What the viewer sees"";

// VISUAL-ONLY
lines[2].Script = """";
lines[2].Visual = ""A silent visual moment"";
```

RULES:
- Output one lines[N] entry per story line, scene, or beat, numbered sequentially starting at 1
- Script is the spoken narration for that line
- Use Script = """" for silent or visual-only beats
- Visual is what appears on screen
- Do not include Music, Emotion, Rhythm, Layers, Decision, Cue, Shots, ImagePrompt, VideoPrompt, or audio-analysis fields
- No commentary outside the code block";
    }
}

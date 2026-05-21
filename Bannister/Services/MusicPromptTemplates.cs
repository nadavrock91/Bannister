namespace Bannister.Services;

public static class MusicPromptTemplates
{
    public static string GetDraftFormatInstructions()
    {
        return @"---
Output ONLY one C# code block with the full story and music plan in this exact format:

```csharp
// NARRATION
lines[1].Script = ""The narration text for this line"";
lines[1].Visual = ""What the viewer sees"";
lines[1].Emotion = ""tension building"";
lines[1].Rhythm = ""Repetitive"";
lines[1].Layers = ""piano,drone"";
lines[1].Decision = ""UseOriginalCue"";
lines[1].Cue = ""Main DNA"";

// VISUAL-ONLY
lines[2].Script = """";
lines[2].Visual = ""A silent visual moment"";
lines[2].Emotion = ""quiet dread"";
lines[2].Rhythm = ""Evolving"";
lines[2].Layers = ""drone,strings"";
lines[2].Decision = ""Variation"";
lines[2].Cue = ""Dark Variation"";

cue[""Main DNA""].Mood = ""ominous, fragile"";
cue[""Main DNA""].Pulse = ""slow heartbeat pulse"";
cue[""Main DNA""].Motif = ""single piano note repeating"";
cue[""Main DNA""].Energy = ""Low"";
cue[""Main DNA""].IsPrimaryDNA = true;
cue[""Main DNA""].MustLoop = true;
cue[""Main DNA""].MustSitUnderNarration = true;
cue[""Main DNA""].VariationType = ""Original"";

cue[""Dark Variation""].Mood = ""darker, tenser version of the theme"";
cue[""Dark Variation""].Pulse = ""same slow pulse with more pressure"";
cue[""Dark Variation""].Motif = ""same piano note, lower and more haunted"";
cue[""Dark Variation""].Energy = ""Medium"";
cue[""Dark Variation""].VariationOf = ""Main DNA"";
cue[""Dark Variation""].VariationType = ""DarkRemix"";
cue[""Dark Variation""].MustLoop = true;
cue[""Dark Variation""].MustSitUnderNarration = true;
```

RULES:
- This is a soundtrack for a short video built from a SMALL number of reusable ~30-second cue blocks
- Aim for 4-8 unique cues total, NOT one cue per line
- Assign cues by REUSING the same cue name across many consecutive or related lines
- Most lines should reuse an existing cue; introduce a new cue only when the emotional need genuinely changes
- Mark exactly ONE cue as IsPrimaryDNA = true
- If a cue is a variation of another, set VariationOf and VariationType
- VariationType must be one of: Original, AddLayers, DarkRemix, RemovePercussion, ExposePiano, IncreaseBassDrone, Custom
- Output one lines[N] entry per story line, scene, or beat, numbered sequentially starting at 1
- Script is the spoken narration for that line
- Use Script = """" for silent or visual-only beats
- Visual is what appears on screen
- Rhythm must be one of: Repetitive, Evolving, Shifting
- Layers must be a comma list drawn from: piano, percussion, drone, strings, bass, silence
- Decision must be one of: UseOriginalCue, Variation, Intensified, Stripped, NewCue, Silence, Callback
- Energy must be one of: Low, Medium, High
- Define every unique cue referenced by lines[N].Cue in the cue[""Name""] block
- Do not include Shots, ImagePrompt, VideoPrompt, or any audio-analysis fields
- No commentary outside the code block";
    }
}

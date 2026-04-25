using Bannister.Models;
using Bannister.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Bannister.Views;

/// <summary>
/// Configuration for an image generation workflow.
/// Each config provides prompt builders for all three steps.
/// Prompt builders receive contextual data and return prompt text.
/// </summary>
public class ImageWorkflowConfig
{
    public string PageTitle { get; set; } = "Image Workflow";
    public string PageIcon { get; set; } = "🎨";
    public string IdeasCategoryPrefix { get; set; } = "image_ideas";

    /// <summary>If true, shows a scene context input before Step 1 (for clip-specific workflows)</summary>
    public bool NeedsSceneContext { get; set; } = false;

    /// <summary>Step 1: Build the prompt that asks an LLM for ideas. Receives (story, sceneContext).</summary>
    public Func<string, string, string> BuildIdeasPrompt { get; set; } = (story, scene) => "";

    /// <summary>Step 3A: Build the direct image prompt meta-prompt. Receives (idea, title).</summary>
    public Func<string, string, string> BuildDirectPrompt { get; set; } = (idea, title) => "";

    /// <summary>Step 3B1: Build the blockout meta-prompt. Receives (idea, title).</summary>
    public Func<string, string, string> BuildBlockoutPrompt { get; set; } = (idea, title) => "";

    /// <summary>Step 3B2: Build the refined-from-blockout prompt. Receives (idea, title).</summary>
    public Func<string, string, string> BuildRefinedPrompt { get; set; } = (idea, title) => "";
}

/// <summary>
/// Hook First Frame configuration — prompts specialized for creating
/// an opening frame that works as both thumbnail and video start.
/// </summary>
public static class HookFirstFrameConfig
{
    public static ImageWorkflowConfig Create() => new ImageWorkflowConfig
    {
        PageTitle = "Hook First Frame",
        PageIcon = "🎬",
        IdeasCategoryPrefix = "hook_frames",

        BuildIdeasPrompt = (story, scene) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("I'm creating a short-form video and I need 10 ideas for the HOOK FIRST FRAME — the very first image the viewer sees.");
            sb.AppendLine();
            sb.AppendLine("This first frame needs to do two jobs at once:");
            sb.AppendLine("1. THUMBNAIL — if someone sees it as a thumbnail while scrolling, it must make them want to click");
            sb.AppendLine("2. VIDEO OPENING — the video starts immediately with this frame, so it must set the tone and pull the viewer in from frame one");
            sb.AppendLine();
            sb.AppendLine("Here is what the video is about:");
            sb.AppendLine("---");
            sb.AppendLine(story);
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("For each of the 10 ideas, consider:");
            sb.AppendLine("- What exactly the viewer sees (composition, subject, setting, mood, lighting)");
            sb.AppendLine("- Why it works as a thumbnail (makes someone click)");
            sb.AppendLine("- Why it works as a video opening (makes someone keep watching)");
            sb.AppendLine("- Camera angle (low angle, close-up, wide establishing, etc.)");
            sb.AppendLine();
            sb.AppendLine("Make the ideas varied — mix mysterious/intriguing, dramatic/bold, intimate/personal, and unexpected visual contrast.");
            sb.AppendLine("Be specific and visual. No vague descriptions.");
            sb.AppendLine();
            sb.AppendLine("CRITICAL OUTPUT FORMAT:");
            sb.AppendLine("Respond with ONLY C# code lines, nothing else. No explanation, no markdown, no commentary.");
            sb.AppendLine("Each idea must be exactly one line in this format:");
            sb.AppendLine();
            sb.AppendLine("ideas.Add(\"[SHORT TITLE]: [Full description of what the viewer sees, composition, mood, camera angle, why it hooks]\");");
            sb.AppendLine();
            sb.AppendLine("Example:");
            sb.AppendLine("ideas.Add(\"The Empty Throne: Wide shot of an ornate golden throne in a dark empty hall, single beam of light cutting through dust, low angle looking up, creates mystery — who belongs here and where are they?\");");
            sb.AppendLine("ideas.Add(\"Eyes in the Mirror: Extreme close-up of weathered eyes reflected in a cracked mirror, warm amber lighting, shallow depth of field, intimate and unsettling — the viewer feels watched\");");
            sb.AppendLine();
            sb.AppendLine("Output exactly 10 lines. Nothing else.");
            return sb.ToString();
        },

        BuildDirectPrompt = (idea, title) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("I need you to write a detailed image generation prompt I can feed directly to an AI image generator (DALL-E, Midjourney, Flux, etc.).");
            sb.AppendLine();
            sb.AppendLine("The image is the HOOK FIRST FRAME of a short video — it serves as both the thumbnail and the opening frame.");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(title)) sb.AppendLine($"Concept: \"{title}\"");
            sb.AppendLine($"Scene: {idea}");
            sb.AppendLine();
            sb.AppendLine("Write the prompt as a single detailed paragraph. Include:");
            sb.AppendLine("- Exact camera angle and shot type");
            sb.AppendLine("- Subject description and placement in frame");
            sb.AppendLine("- Foreground, midground, background layers");
            sb.AppendLine("- Lighting direction, quality, and color temperature");
            sb.AppendLine("- Mood and atmosphere");
            sb.AppendLine("- Art style (cinematic, photorealistic, etc.)");
            sb.AppendLine("- Aspect ratio: 9:16 (vertical/portrait)");
            sb.AppendLine();
            sb.AppendLine("Requirements:");
            sb.AppendLine("- Must read clearly at small thumbnail size");
            sb.AppendLine("- No text, no watermarks, no UI elements");
            sb.AppendLine("- Strong visual hierarchy — one clear focal point");
            sb.AppendLine("- Should feel like a paused cinematic moment that implies story");
            sb.AppendLine();
            sb.AppendLine("Output ONLY the image prompt paragraph. Nothing else.");
            return sb.ToString();
        },

        BuildBlockoutPrompt = (idea, title) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("I need you to write a blockout prompt I can feed directly to an AI image generator.");
            sb.AppendLine("The blockout is a monochrome composition diagram — NOT a finished image.");
            sb.AppendLine();
            sb.AppendLine("Here is the scene I need blocked out:");
            if (!string.IsNullOrEmpty(title)) sb.AppendLine($"Title: \"{title}\"");
            sb.AppendLine($"Scene: {idea}");
            sb.AppendLine();
            sb.AppendLine("Write the prompt in this exact style and structure (follow this as a template):");
            sb.AppendLine();
            sb.AppendLine("---EXAMPLE START---");
            sb.AppendLine("Create a very simple monochrome terrain blockout diagram, not a detailed sketch, not realistic art. Show the scene as a clean composition study for line-of-sight and elevation only.");
            sb.AppendLine("Requirements:");
            sb.AppendLine("- vertical 9:16 frame");
            sb.AppendLine("- extremely simple shapes");
            sb.AppendLine("- no character details");
            sb.AppendLine("- no texture, no rendering, no shading except flat light gray vs dark gray");
            sb.AppendLine("- no cinematic polish");
            sb.AppendLine("- no handwritten notes");
            sb.AppendLine("- no realism");
            sb.AppendLine("Composition:");
            sb.AppendLine("- [describe exact placement of every element top to bottom]");
            sb.AppendLine("- [specify what's in lower half vs upper half of frame]");
            sb.AppendLine("- [describe spatial relationships — what hides what, what's above/below]");
            sb.AppendLine("- [specify scale relationships between elements]");
            sb.AppendLine("- [describe negative space and where it goes]");
            sb.AppendLine("Style:");
            sb.AppendLine("- ultra simple concept blockout");
            sb.AppendLine("- grayscale only");
            sb.AppendLine("- clean shapes");
            sb.AppendLine("- visual-development diagram");
            sb.AppendLine("- fast composition study");
            sb.AppendLine("- like an animation pre-vis frame");
            sb.AppendLine("---EXAMPLE END---");
            sb.AppendLine();
            sb.AppendLine("CRITICAL RULES for the prompt you write:");
            sb.AppendLine("- Be extremely specific about WHERE every element goes in the frame (upper half, lower third, center-left, etc.)");
            sb.AppendLine("- Describe spatial relationships: what's in front of what, what occludes what, elevation differences");
            sb.AppendLine("- Specify the camera angle as element placement (e.g. 'feet toward camera, upper body farther away' not just 'low angle')");
            sb.AppendLine("- Keep the aggressive negative requirements (no texture, no detail, no realism, no shading)");
            sb.AppendLine("- Think about what makes the composition readable at thumbnail size");
            sb.AppendLine("- The prompt should read like a technical placement spec, not a creative description");
            sb.AppendLine();
            sb.AppendLine("Output ONLY the blockout prompt. Nothing else. No explanation.");
            return sb.ToString();
        },

        BuildRefinedPrompt = (idea, title) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("I have a composition blockout image (attached/referenced) that shows the exact layout and spatial arrangement I want.");
            sb.AppendLine("Now I need the full cinematic version of this scene, keeping the EXACT same composition, camera angle, and element placement as the blockout.");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(title)) sb.AppendLine($"Concept: \"{title}\"");
            sb.AppendLine($"Scene: {idea}");
            sb.AppendLine();
            sb.AppendLine("Match the blockout's composition exactly, but now render it with:");
            sb.AppendLine("- Full photorealistic / cinematic detail");
            sb.AppendLine("- Rich textures and materials");
            sb.AppendLine("- Dramatic cinematic lighting");
            sb.AppendLine("- Atmosphere (haze, dust, volumetric light where appropriate)");
            sb.AppendLine("- Depth of field to guide the eye");
            sb.AppendLine("- Color grading that sets the emotional tone");
            sb.AppendLine();
            sb.AppendLine("Critical:");
            sb.AppendLine("- Keep the SAME element placement as the blockout");
            sb.AppendLine("- Keep the SAME camera angle and perspective");
            sb.AppendLine("- Keep the SAME scale relationships");
            sb.AppendLine("- 9:16 aspect ratio (vertical/portrait)");
            sb.AppendLine("- No text, no watermarks");
            sb.AppendLine("- Must read clearly at thumbnail size");
            sb.AppendLine();
            sb.AppendLine("Output ONLY the image prompt. Nothing else.");
            return sb.ToString();
        }
    };
}

/// <summary>
/// Clip Start Frame configuration — prompts for creating the first frame of any specific clip/scene.
/// Requires scene context (what scene/clip this frame is for).
/// </summary>
public static class ClipStartFrameConfig
{
    public static ImageWorkflowConfig Create() => new ImageWorkflowConfig
    {
        PageTitle = "Clip Start Frame",
        PageIcon = "🎞️",
        IdeasCategoryPrefix = "clip_start",
        NeedsSceneContext = true,

        BuildIdeasPrompt = (story, scene) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("I'm creating a short-form video and I need 10 ideas for the FIRST FRAME of a specific clip/scene.");
            sb.AppendLine("This frame is the very first thing the viewer sees when this clip starts.");
            sb.AppendLine();
            sb.AppendLine("Here is what the full video is about:");
            sb.AppendLine("---");
            sb.AppendLine(story);
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("Here is the specific scene/clip I need the first frame for:");
            sb.AppendLine("---");
            sb.AppendLine(scene);
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("For each of the 10 ideas, consider:");
            sb.AppendLine("- What exactly the viewer sees (composition, subject, setting, mood, lighting)");
            sb.AppendLine("- How it sets up the scene that follows");
            sb.AppendLine("- Camera angle and framing");
            sb.AppendLine("- How it transitions from the previous scene (if applicable)");
            sb.AppendLine();
            sb.AppendLine("Make the ideas varied. Be specific and visual.");
            sb.AppendLine();
            sb.AppendLine("CRITICAL OUTPUT FORMAT:");
            sb.AppendLine("Respond with ONLY C# code lines, nothing else. No explanation, no markdown, no commentary.");
            sb.AppendLine("Each idea must be exactly one line in this format:");
            sb.AppendLine();
            sb.AppendLine("ideas.Add(\"[SHORT TITLE]: [Full description of what the viewer sees]\");");
            sb.AppendLine();
            sb.AppendLine("Output exactly 10 lines. Nothing else.");
            return sb.ToString();
        },

        BuildDirectPrompt = (idea, title) =>
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("I need you to write a detailed image generation prompt I can feed directly to an AI image generator.");
            sb.AppendLine();
            sb.AppendLine("The image is the opening frame of a specific clip within a cinematic short video.");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(title)) sb.AppendLine($"Concept: \"{title}\"");
            sb.AppendLine($"Scene: {idea}");
            sb.AppendLine();
            sb.AppendLine("Write the prompt as a single detailed paragraph. Include:");
            sb.AppendLine("- Exact camera angle and shot type");
            sb.AppendLine("- Subject description and placement in frame");
            sb.AppendLine("- Foreground, midground, background layers");
            sb.AppendLine("- Lighting direction, quality, and color temperature");
            sb.AppendLine("- Mood and atmosphere");
            sb.AppendLine("- Aspect ratio: 9:16 (vertical/portrait)");
            sb.AppendLine();
            sb.AppendLine("Output ONLY the image prompt paragraph. Nothing else.");
            return sb.ToString();
        },

        BuildBlockoutPrompt = HookFirstFrameConfig.Create().BuildBlockoutPrompt,
        BuildRefinedPrompt = HookFirstFrameConfig.Create().BuildRefinedPrompt
    };
}

/// <summary>
/// Reusable 3-step image generation workflow page.
/// Step 1: Get Ideas → Step 2: Choose Idea → Step 3: Generate Image (direct attempt, then blockout on failure).
/// Configured via ImageWorkflowConfig for different use cases.
/// </summary>
public class ImageWorkflowPage : ContentPage
{
    private readonly ImageProject _project;
    private readonly ImageWorkflowConfig _config;
    private readonly ImageProductionService? _imageService;

    private Editor _sceneContextEditor;
    private Editor _promptEditor, _responsePasteEditor;
    private VerticalStackLayout _ideasListStack;
    private Label _chosenLabel;
    private string? _chosenIdea = null;
    private Editor _manualIdeaEntry;
    private Editor _directPromptEditor, _blockoutPromptEditor, _refinedPromptEditor;
    private Editor _fixDescriptionEditor, _iterationPromptEditor;
    private VerticalStackLayout _stageBContainer, _iterationContainer, _b2Container;
    private int _iterationCount = 0;
    private Label _iterationCountLabel;
    private Frame _sceneFrame, _step1Content, _step2Content, _step3Content;
    private Label _step1Arrow, _step2Arrow, _step3Arrow;

    public ImageWorkflowPage(ImageProject project, ImageWorkflowConfig config, ImageProductionService? imageService = null)
    {
        _project = project;
        _config = config;
        _imageService = imageService;
        Title = config.PageTitle;
        BackgroundColor = Color.FromArgb("#F5F5F5");

        // Restore locked idea if it matches this workflow
        if (_project.LockedWorkflow == config.IdeasCategoryPrefix && !string.IsNullOrWhiteSpace(_project.LockedIdea))
            _chosenIdea = _project.LockedIdea;

        BuildUI();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 16, Spacing = 2 };

        mainStack.Children.Add(new Label { Text = $"{_config.PageIcon} {_config.PageTitle}", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#FF6F00"), Margin = new Thickness(4, 0, 0, 4) });
        mainStack.Children.Add(new Label { Text = $"Project: {_project.Name}", FontSize = 13, TextColor = Color.FromArgb("#666"), Margin = new Thickness(4, 0, 0, 2) });

        string preview = _project.StoryDescription.Length > 150 ? _project.StoryDescription.Substring(0, 150) + "..." : _project.StoryDescription;
        var storyFrame = new Frame { BackgroundColor = Color.FromArgb("#FFF8E1"), BorderColor = Color.FromArgb("#FFB74D"), Padding = 10, CornerRadius = 8, HasShadow = false, Margin = new Thickness(0, 0, 0, 8) };
        storyFrame.Content = new Label { Text = $"📝 {preview}", FontSize = 11, TextColor = Color.FromArgb("#666") };
        mainStack.Children.Add(storyFrame);

        // Scene context (only for configs that need it)
        if (_config.NeedsSceneContext)
            mainStack.Children.Add(BuildSceneContext());

        mainStack.Children.Add(BuildStep1());
        mainStack.Children.Add(BuildStep2());
        mainStack.Children.Add(BuildStep3());

        // If idea is already locked, show it and open step 3
        if (_chosenIdea != null)
        {
            _chosenLabel.Text = $"✅ Locked: {(_chosenIdea.Length > 100 ? _chosenIdea.Substring(0, 97) + "..." : _chosenIdea)}";
            _chosenLabel.TextColor = Color.FromArgb("#2E7D32");
            _step1Content.IsVisible = false; _step1Arrow.Text = "▶";
            _step2Content.IsVisible = false; _step2Arrow.Text = "▶";
            _step3Content.IsVisible = true; _step3Arrow.Text = "▼";
        }

        Content = new ScrollView { Content = mainStack };
    }

    // ===================== SCENE CONTEXT =====================

    private View BuildSceneContext()
    {
        _sceneFrame = new Frame { BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#CE93D8"), CornerRadius = 0, Padding = 16, HasShadow = false, Margin = new Thickness(0, 0, 0, 2) };
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(new Label { Text = "SCENE CONTEXT", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#7B1FA2"), CharacterSpacing = 1 });
        stack.Children.Add(new Label { Text = "Describe the specific scene/clip you need the first frame for:", FontSize = 12, TextColor = Color.FromArgb("#666") });
        _sceneContextEditor = new Editor { Text = _project.SceneContext ?? "", Placeholder = "e.g. The wizard trapped under a fallen tree, lion approaching from above...", BackgroundColor = Color.FromArgb("#FAFAFA"), HeightRequest = 80, FontSize = 13 };
        stack.Children.Add(_sceneContextEditor);
        var saveBtn = new Button { Text = "💾 Save Scene", FontSize = 12, HeightRequest = 34, BackgroundColor = Color.FromArgb("#7B1FA2"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0), HorizontalOptions = LayoutOptions.Start };
        saveBtn.Clicked += async (s, e) =>
        {
            _project.SceneContext = _sceneContextEditor.Text?.Trim();
            if (_imageService != null) await _imageService.UpdateProjectAsync(_project);
            saveBtn.Text = "✓ Saved"; await Task.Delay(1500); saveBtn.Text = "💾 Save Scene";
        };
        stack.Children.Add(saveBtn);
        _sceneFrame.Content = stack;
        return _sceneFrame;
    }

    // ===================== STEP 1: GET IDEAS =====================

    private View BuildStep1()
    {
        var container = new VerticalStackLayout { Spacing = 0 };
        var header = BuildStepHeader("STEP 1: GET IDEAS", "Generate prompt → paste LLM response → import", Color.FromArgb("#FF6F00"), out _step1Arrow);
        _step1Content = new Frame { BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), CornerRadius = 0, Padding = 16, HasShadow = false, IsVisible = _chosenIdea == null };
        header.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => { _step1Content.IsVisible = !_step1Content.IsVisible; _step1Arrow.Text = _step1Content.IsVisible ? "▼" : "▶"; }) });
        if (_chosenIdea == null) _step1Arrow.Text = "▼";

        var stack = new VerticalStackLayout { Spacing = 10 };

        var generateBtn = new Button { Text = "⚡ Generate Prompt", FontSize = 13, HeightRequest = 40, BackgroundColor = Color.FromArgb("#FF6F00"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0) };
        generateBtn.Clicked += (s, e) =>
        {
            string scene = _config.NeedsSceneContext ? (_sceneContextEditor?.Text?.Trim() ?? "") : "";
            _promptEditor.Text = _config.BuildIdeasPrompt(_project.StoryDescription.Trim(), scene);
        };
        stack.Children.Add(generateBtn);

        _promptEditor = new Editor { Placeholder = "Prompt will appear here...", BackgroundColor = Color.FromArgb("#FAFAFA"), HeightRequest = 160, FontSize = 11, FontFamily = "Consolas", IsReadOnly = true };
        stack.Children.Add(_promptEditor);
        stack.Children.Add(CreateCopyButton(_promptEditor));

        stack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E0E0E0"), Margin = new Thickness(0, 6) });
        stack.Children.Add(new Label { Text = "Paste LLM response:", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#666") });
        _responsePasteEditor = new Editor { Placeholder = "Paste the ideas.Add(\"...\") lines here...", BackgroundColor = Color.FromArgb("#FAFAFA"), HeightRequest = 160, FontSize = 12 };
        stack.Children.Add(_responsePasteEditor);

        var btnRow = new HorizontalStackLayout { Spacing = 8 };
        var importBtn = new Button { Text = "📥 Import to Ideas", FontSize = 13, HeightRequest = 40, BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0) };
        importBtn.Clicked += OnImportClicked;
        btnRow.Children.Add(importBtn);
        var skipBtn = new Button { Text = "Skip →", FontSize = 12, HeightRequest = 40, BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        skipBtn.Clicked += (s, e) => { _step1Content.IsVisible = false; _step1Arrow.Text = "▶"; _step2Content.IsVisible = true; _step2Arrow.Text = "▼"; };
        btnRow.Children.Add(skipBtn);
        stack.Children.Add(btnRow);

        _step1Content.Content = stack;
        container.Children.Add(header); container.Children.Add(_step1Content);
        return container;
    }

    // ===================== STEP 2: CHOOSE IDEA =====================

    private View BuildStep2()
    {
        var container = new VerticalStackLayout { Spacing = 0 };
        var header = BuildStepHeader("STEP 2: CHOOSE IDEA", "Pick which idea to develop", Color.FromArgb("#1565C0"), out _step2Arrow);
        _step2Content = new Frame { BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), CornerRadius = 0, Padding = 16, HasShadow = false, IsVisible = false };
        header.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => { _step2Content.IsVisible = !_step2Content.IsVisible; _step2Arrow.Text = _step2Content.IsVisible ? "▼" : "▶"; if (_step2Content.IsVisible) await LoadIdeasAsync(); }) });

        var stack = new VerticalStackLayout { Spacing = 10 };

        var loadBtn = new Button { Text = "🔄 Load Ideas", FontSize = 12, HeightRequest = 34, BackgroundColor = Color.FromArgb("#E3F2FD"), TextColor = Color.FromArgb("#1565C0"), CornerRadius = 6, Padding = new Thickness(12, 0), HorizontalOptions = LayoutOptions.Start };
        loadBtn.Clicked += async (s, e) => await LoadIdeasAsync();
        stack.Children.Add(loadBtn);

        _ideasListStack = new VerticalStackLayout { Spacing = 6 };
        _ideasListStack.Children.Add(new Label { Text = "No ideas loaded yet.", FontSize = 12, TextColor = Color.FromArgb("#999") });
        stack.Children.Add(_ideasListStack);

        stack.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E0E0E0"), Margin = new Thickness(0, 4) });
        stack.Children.Add(new Label { Text = "Or write your own:", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#666") });

        _manualIdeaEntry = new Editor { Placeholder = "Write a custom idea here...", BackgroundColor = Color.FromArgb("#FAFAFA"), HeightRequest = 60, FontSize = 13 };
        stack.Children.Add(_manualIdeaEntry);

        var manualRow = new HorizontalStackLayout { Spacing = 8 };
        var lockBtn = new Button { Text = "✅ Lock Custom Idea", FontSize = 12, HeightRequest = 34, BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        lockBtn.Clicked += async (s, e) =>
        {
            var t = _manualIdeaEntry.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(t)) return;

            // Save custom idea to Ideas DB so it persists
            IdeasService? svc = null;
            try { svc = Application.Current?.Handler?.MauiContext?.Services?.GetService<IdeasService>(); } catch { }
            if (svc != null)
            {
                try { await svc.CreateIdeaAsync(_project.Username, t, GetCategory()); } catch { }
            }

            await LockIdeaAsync(t);
        };
        manualRow.Children.Add(lockBtn);
        var skipBtn2 = new Button { Text = "Skip →", FontSize = 12, HeightRequest = 34, BackgroundColor = Color.FromArgb("#9E9E9E"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        skipBtn2.Clicked += (s, e) => { _step2Content.IsVisible = false; _step2Arrow.Text = "▶"; _step3Content.IsVisible = true; _step3Arrow.Text = "▼"; };
        manualRow.Children.Add(skipBtn2);
        stack.Children.Add(manualRow);

        _chosenLabel = new Label { Text = "No idea chosen yet.", FontSize = 12, TextColor = Color.FromArgb("#999"), Margin = new Thickness(0, 4, 0, 0) };
        stack.Children.Add(_chosenLabel);

        // Unlock button
        var unlockBtn = new Button { Text = "🔓 Unlock Current Idea", FontSize = 11, HeightRequest = 30, BackgroundColor = Color.FromArgb("#FFEBEE"), TextColor = Color.FromArgb("#C62828"), CornerRadius = 6, Padding = new Thickness(10, 0), HorizontalOptions = LayoutOptions.Start };
        unlockBtn.Clicked += async (s, e) =>
        {
            _chosenIdea = null;
            _project.LockedIdea = null; _project.LockedWorkflow = null;
            if (_imageService != null) await _imageService.UpdateProjectAsync(_project);
            _chosenLabel.Text = "No idea chosen yet."; _chosenLabel.TextColor = Color.FromArgb("#999");
            _step3Content.IsVisible = false; _step3Arrow.Text = "▶";
        };
        stack.Children.Add(unlockBtn);

        _step2Content.Content = stack;
        container.Children.Add(header); container.Children.Add(_step2Content);
        return container;
    }

    // ===================== STEP 3: GENERATE IMAGE =====================

    private View BuildStep3()
    {
        var container = new VerticalStackLayout { Spacing = 0 };
        var header = BuildStepHeader("STEP 3: GENERATE IMAGE", "Direct attempt first, then blockout on failure", Color.FromArgb("#2E7D32"), out _step3Arrow);
        _step3Content = new Frame { BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#E0E0E0"), CornerRadius = 0, Padding = 16, HasShadow = false, IsVisible = false };
        header.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => { _step3Content.IsVisible = !_step3Content.IsVisible; _step3Arrow.Text = _step3Content.IsVisible ? "▼" : "▶"; }) });

        var mainStack = new VerticalStackLayout { Spacing = 12 };

        // Stage A
        mainStack.Children.Add(StageLabel("STAGE A: DIRECT ATTEMPT", "Generate a detailed image prompt.", Color.FromArgb("#2E7D32")));
        var directBtn = new Button { Text = "⚡ Build Direct Image Prompt", FontSize = 13, HeightRequest = 40, BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0) };
        directBtn.Clicked += (s, e) => { var (i, t) = SplitIdea(); _directPromptEditor.Text = _config.BuildDirectPrompt(i, t); };
        mainStack.Children.Add(directBtn);
        _directPromptEditor = new Editor { Placeholder = "Image prompt...", BackgroundColor = Color.FromArgb("#FAFAFA"), HeightRequest = 180, FontSize = 12, FontFamily = "Consolas" };
        mainStack.Children.Add(_directPromptEditor);
        mainStack.Children.Add(CreateCopyButton(_directPromptEditor));

        mainStack.Children.Add(new Label { Text = "Did it work?", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 6, 0, 0) });
        var resultRow = new HorizontalStackLayout { Spacing = 10 };
        var successBtn = new Button { Text = "✅ Success!", FontSize = 13, HeightRequest = 40, BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0) };
        successBtn.Clicked += async (s, e) => await DisplayAlert("Done!", "Your image is ready for production.", "OK");
        resultRow.Children.Add(successBtn);
        var failBtn = new Button { Text = "❌ Failed — Try Blockout", FontSize = 13, HeightRequest = 40, BackgroundColor = Color.FromArgb("#C62828"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0) };
        failBtn.Clicked += (s, e) => _stageBContainer.IsVisible = true;
        resultRow.Children.Add(failBtn);
        mainStack.Children.Add(resultRow);

        // Stage B
        _stageBContainer = new VerticalStackLayout { Spacing = 10, IsVisible = false };
        _stageBContainer.Children.Add(new BoxView { HeightRequest = 2, BackgroundColor = Color.FromArgb("#C62828"), Margin = new Thickness(0, 8) });
        _stageBContainer.Children.Add(StageLabel("STAGE B: STRUCTURAL BLOCKOUT", "Start simple, nail composition, then build up.", Color.FromArgb("#E65100")));

        _stageBContainer.Children.Add(new Label { Text = "B1: BLOCKOUT PROMPT", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#E65100") });
        var blockBtn = new Button { Text = "🔲 Build Blockout Prompt", FontSize = 13, HeightRequest = 40, BackgroundColor = Color.FromArgb("#E65100"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0) };
        blockBtn.Clicked += (s, e) => { var (i, t) = SplitIdea(); _blockoutPromptEditor.Text = _config.BuildBlockoutPrompt(i, t); };
        _stageBContainer.Children.Add(blockBtn);
        _blockoutPromptEditor = new Editor { Placeholder = "Blockout prompt...", BackgroundColor = Color.FromArgb("#FFF3E0"), HeightRequest = 140, FontSize = 12, FontFamily = "Consolas" };
        _stageBContainer.Children.Add(_blockoutPromptEditor);
        _stageBContainer.Children.Add(CreateCopyButton(_blockoutPromptEditor));

        // Blockout result buttons
        _stageBContainer.Children.Add(new Label { Text = "How did the blockout turn out?", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 6, 0, 0) });

        var blockoutResultRow = new HorizontalStackLayout { Spacing = 8 };
        var blockoutOkBtn = new Button { Text = "✅ Composition is right → B2", FontSize = 12, HeightRequest = 36, BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        blockoutOkBtn.Clicked += (s, e) => { _b2Container.IsVisible = true; };
        blockoutResultRow.Children.Add(blockoutOkBtn);

        var blockoutFixBtn = new Button { Text = "🔄 Close but needs fixes", FontSize = 12, HeightRequest = 36, BackgroundColor = Color.FromArgb("#FF6F00"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        blockoutFixBtn.Clicked += (s, e) => { _iterationContainer.IsVisible = true; };
        blockoutResultRow.Children.Add(blockoutFixBtn);

        var blockoutRetryBtn = new Button { Text = "❌ Totally wrong → Retry", FontSize = 12, HeightRequest = 36, BackgroundColor = Color.FromArgb("#C62828"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        blockoutRetryBtn.Clicked += (s, e) => { var (i, t) = SplitIdea(); _blockoutPromptEditor.Text = _config.BuildBlockoutPrompt(i, t); _iterationCount = 0; _iterationCountLabel.Text = ""; };
        blockoutResultRow.Children.Add(blockoutRetryBtn);
        _stageBContainer.Children.Add(blockoutResultRow);

        // ====== ITERATION LOOP (hidden until "needs fixes") ======
        _iterationContainer = new VerticalStackLayout { Spacing = 10, IsVisible = false };

        _iterationContainer.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#FF6F00"), Margin = new Thickness(0, 6) });
        _iterationContainer.Children.Add(new Label { Text = "🔄 FIX BLOCKOUT", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#FF6F00") });

        _iterationCountLabel = new Label { Text = "", FontSize = 11, TextColor = Color.FromArgb("#999") };
        _iterationContainer.Children.Add(_iterationCountLabel);

        _iterationContainer.Children.Add(new Label { Text = "Describe what's wrong with the blockout:", FontSize = 12, TextColor = Color.FromArgb("#666") });

        _fixDescriptionEditor = new Editor { Placeholder = "e.g. Robot is too far away, should fill more of the upper frame. Man needs to be slightly larger. Road is too wide — narrow it so machinery is closer. Machinery on left side is too short.", BackgroundColor = Color.FromArgb("#FAFAFA"), HeightRequest = 80, FontSize = 13 };
        _iterationContainer.Children.Add(_fixDescriptionEditor);

        // Two options: with reference image or without
        _iterationContainer.Children.Add(new Label { Text = "Generate correction prompt:", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 4, 0, 0) });

        var iterBtnRow = new HorizontalStackLayout { Spacing = 8 };

        var iterWithRefBtn = new Button { Text = "🖼️ With Reference Image", FontSize = 12, HeightRequest = 36, BackgroundColor = Color.FromArgb("#FF6F00"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        iterWithRefBtn.Clicked += (s, e) => BuildIterationPrompt(withReference: true);
        iterBtnRow.Children.Add(iterWithRefBtn);

        var iterNoRefBtn = new Button { Text = "📝 Without Reference", FontSize = 12, HeightRequest = 36, BackgroundColor = Color.FromArgb("#795548"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        iterNoRefBtn.Clicked += (s, e) => BuildIterationPrompt(withReference: false);
        iterBtnRow.Children.Add(iterNoRefBtn);

        _iterationContainer.Children.Add(iterBtnRow);

        _iterationPromptEditor = new Editor { Placeholder = "Correction prompt will appear here...", BackgroundColor = Color.FromArgb("#FFF3E0"), HeightRequest = 180, FontSize = 12, FontFamily = "Consolas" };
        _iterationContainer.Children.Add(_iterationPromptEditor);
        _iterationContainer.Children.Add(CreateCopyButton(_iterationPromptEditor));

        _iterationContainer.Children.Add(new Label { Text = "Copy this → paste to LLM → get corrected blockout prompt → feed to image generator → check again.", FontSize = 11, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic });

        // After iteration, same three choices
        var iterResultRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        var iterOkBtn = new Button { Text = "✅ Now it's right → B2", FontSize = 12, HeightRequest = 36, BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        iterOkBtn.Clicked += (s, e) => { _b2Container.IsVisible = true; };
        iterResultRow.Children.Add(iterOkBtn);

        var iterAgainBtn = new Button { Text = "🔄 Still needs fixes", FontSize = 12, HeightRequest = 36, BackgroundColor = Color.FromArgb("#FF6F00"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(12, 0) };
        iterAgainBtn.Clicked += (s, e) => { _fixDescriptionEditor.Text = ""; _iterationPromptEditor.Text = ""; };
        iterResultRow.Children.Add(iterAgainBtn);
        _iterationContainer.Children.Add(iterResultRow);

        _stageBContainer.Children.Add(_iterationContainer);

        // ====== B2: FULL PROMPT (hidden until composition approved) ======
        _b2Container = new VerticalStackLayout { Spacing = 10, IsVisible = false };

        _b2Container.Children.Add(new BoxView { HeightRequest = 1, BackgroundColor = Color.FromArgb("#E0E0E0"), Margin = new Thickness(0, 6) });
        _b2Container.Children.Add(new Label { Text = "B2: FULL PROMPT (from blockout)", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#E65100") });
        _b2Container.Children.Add(new Label { Text = "Feed the blockout image as reference alongside this prompt.", FontSize = 12, TextColor = Color.FromArgb("#666") });
        var refBtn = new Button { Text = "🎨 Build Full Prompt from Blockout", FontSize = 13, HeightRequest = 40, BackgroundColor = Color.FromArgb("#BF360C"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(16, 0) };
        refBtn.Clicked += (s, e) => { var (i, t) = SplitIdea(); _refinedPromptEditor.Text = _config.BuildRefinedPrompt(i, t); };
        _b2Container.Children.Add(refBtn);
        _refinedPromptEditor = new Editor { Placeholder = "Refined prompt...", BackgroundColor = Color.FromArgb("#FAFAFA"), HeightRequest = 180, FontSize = 12, FontFamily = "Consolas" };
        _b2Container.Children.Add(_refinedPromptEditor);
        _b2Container.Children.Add(CreateCopyButton(_refinedPromptEditor));

        _stageBContainer.Children.Add(_b2Container);

        mainStack.Children.Add(_stageBContainer);
        _step3Content.Content = mainStack;
        container.Children.Add(header); container.Children.Add(_step3Content);
        return container;
    }

    // ===================== BLOCKOUT ITERATION =====================

    private void BuildIterationPrompt(bool withReference)
    {
        string fixes = _fixDescriptionEditor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(fixes))
        {
            _iterationPromptEditor.Text = "Describe what's wrong first.";
            return;
        }

        _iterationCount++;
        _iterationCountLabel.Text = $"Iteration #{_iterationCount}";

        var (idea, title) = SplitIdea();

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("I previously generated a monochrome composition blockout diagram for a scene, but it needs corrections.");
        sb.AppendLine();

        if (withReference)
        {
            sb.AppendLine("I am attaching/providing the previous blockout image as reference.");
            sb.AppendLine("Look at the attached image and apply the following corrections to it.");
        }
        else
        {
            sb.AppendLine("I do NOT have a reference image to share. Recreate the blockout from scratch based on the original scene description plus the corrections below.");
        }

        sb.AppendLine();
        sb.AppendLine("Original scene:");
        if (!string.IsNullOrEmpty(title)) sb.AppendLine($"Title: \"{title}\"");
        sb.AppendLine($"Description: {idea}");
        sb.AppendLine();
        sb.AppendLine("What needs to be fixed:");
        sb.AppendLine("---");
        sb.AppendLine(fixes);
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Write a NEW blockout prompt I can feed to an AI image generator.");
        sb.AppendLine("The prompt must follow this exact style:");
        sb.AppendLine("- Start with \"Create a very simple monochrome terrain blockout diagram\"");
        sb.AppendLine("- Include aggressive negative requirements (no texture, no detail, no realism, no shading except flat gray)");
        sb.AppendLine("- Include a Composition section with EXACT element placement (upper half, lower third, etc.)");
        sb.AppendLine("- Include a Style section (ultra simple, grayscale, clean shapes, pre-vis frame)");
        sb.AppendLine("- Vertical 9:16 frame");
        sb.AppendLine();
        sb.AppendLine("CRITICAL: Apply ALL the corrections listed above. The new blockout must fix every issue described.");

        if (withReference)
        {
            sb.AppendLine();
            sb.AppendLine("Since I'm providing the previous blockout as reference, the new prompt should explicitly say:");
            sb.AppendLine("\"Using the attached image as a starting point, recreate this blockout with the following changes: [corrections]\"");
        }

        sb.AppendLine();
        sb.AppendLine("Output ONLY the corrected blockout prompt. Nothing else. No explanation.");

        _iterationPromptEditor.Text = sb.ToString();
    }

    // ===================== HELPERS =====================

    private (string idea, string title) SplitIdea()
    {
        string idea = _chosenIdea ?? ""; string title = "";
        int c = idea.IndexOf(':');
        if (c > 0 && c < 60) { title = idea.Substring(0, c).Trim(); idea = idea.Substring(c + 1).Trim(); }
        return (idea, title);
    }

    private string GetCategory()
    {
        string cat = $"{_config.IdeasCategoryPrefix}_{_project.Name.ToLower().Replace(" ", "_")}";
        return cat.Length > 50 ? cat.Substring(0, 50) : cat;
    }

    private async Task LockIdeaAsync(string text)
    {
        _chosenIdea = text;
        _chosenLabel.Text = $"✅ Locked: {(text.Length > 100 ? text.Substring(0, 97) + "..." : text)}";
        _chosenLabel.TextColor = Color.FromArgb("#2E7D32");

        // Persist
        _project.LockedIdea = text;
        _project.LockedWorkflow = _config.IdeasCategoryPrefix;
        if (_imageService != null) await _imageService.UpdateProjectAsync(_project);

        _step3Content.IsVisible = true; _step3Arrow.Text = "▼";
    }

    private View StageLabel(string t, string s, Color c) { var st = new VerticalStackLayout { Spacing = 2, Margin = new Thickness(0, 2, 0, 4) }; st.Children.Add(new Label { Text = t, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = c, CharacterSpacing = 1 }); st.Children.Add(new Label { Text = s, FontSize = 11, TextColor = Color.FromArgb("#888") }); return st; }
    private Button CreateCopyButton(Editor ed) { var b = new Button { Text = "📋 Copy", FontSize = 12, HeightRequest = 34, BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#455A64"), CornerRadius = 6, Padding = new Thickness(12, 0), HorizontalOptions = LayoutOptions.Start }; b.Clicked += async (s, e) => { if (!string.IsNullOrEmpty(ed.Text)) { await Clipboard.SetTextAsync(ed.Text); b.Text = "✓"; await Task.Delay(1500); b.Text = "📋 Copy"; } }; return b; }
    private Grid BuildStepHeader(string t, string s, Color c, out Label arrow) { var g = new Grid { BackgroundColor = c, Padding = new Thickness(14, 10), ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10 }; arrow = new Label { Text = "▶", FontSize = 13, TextColor = Colors.White, VerticalOptions = LayoutOptions.Center }; g.Add(arrow, 0, 0); var ts = new VerticalStackLayout { Spacing = 1 }; ts.Children.Add(new Label { Text = t, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, CharacterSpacing = 1 }); ts.Children.Add(new Label { Text = s, FontSize = 10, TextColor = Colors.White.WithAlpha(0.7f) }); g.Add(ts, 1, 0); return g; }

    // ===================== IMPORT =====================

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        string response = _responsePasteEditor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(response)) { await DisplayAlert("Empty", "Paste the LLM response first.", "OK"); return; }
        var ideas = ParseIdeas(response);
        if (ideas.Count == 0) { await DisplayAlert("No Ideas", "Could not parse any ideas.Add(\"...\") lines.", "OK"); return; }
        string cat = GetCategory();
        if (!await DisplayAlert("Import", $"Found {ideas.Count} ideas.\nImport to \"{cat}\"?", "Import", "Cancel")) return;
        IdeasService? svc = null;
        try { svc = Application.Current?.Handler?.MauiContext?.Services?.GetService<IdeasService>(); } catch { }
        if (svc == null) { await DisplayAlert("Error", "Could not access Ideas service.", "OK"); return; }
        int n = 0;
        foreach (var t in ideas) { try { await svc.CreateIdeaAsync(_project.Username, t, cat); n++; } catch { } }
        await DisplayAlert("Imported!", $"{n} ideas imported to \"{cat}\".", "OK");
        _step1Content.IsVisible = false; _step1Arrow.Text = "▶";
        _step2Content.IsVisible = true; _step2Arrow.Text = "▼";
        await LoadIdeasAsync();
    }

    private async Task LoadIdeasAsync()
    {
        _ideasListStack.Children.Clear();
        IdeasService? svc = null;
        try { svc = Application.Current?.Handler?.MauiContext?.Services?.GetService<IdeasService>(); } catch { }
        if (svc == null) { _ideasListStack.Children.Add(new Label { Text = "Could not load.", FontSize = 12, TextColor = Color.FromArgb("#C62828") }); return; }
        var ideas = await svc.GetIdeasByCategoryAsync(_project.Username, GetCategory());
        if (ideas.Count == 0) { _ideasListStack.Children.Add(new Label { Text = "No ideas found. Import in Step 1.", FontSize = 12, TextColor = Color.FromArgb("#999") }); return; }
        _ideasListStack.Children.Add(new Label { Text = $"{ideas.Count} ideas:", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        for (int i = 0; i < ideas.Count; i++)
        {
            var idea = ideas[i]; int num = i + 1;
            var card = new Frame { BackgroundColor = Color.FromArgb("#F5F5F5"), BorderColor = Color.FromArgb("#E0E0E0"), CornerRadius = 8, Padding = 10, HasShadow = false };
            var cg = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10 };
            var badge = new Frame { BackgroundColor = Color.FromArgb("#1565C0"), CornerRadius = 12, Padding = new Thickness(8, 4), HasShadow = false, VerticalOptions = LayoutOptions.Start };
            badge.Content = new Label { Text = num.ToString(), FontSize = 11, TextColor = Colors.White, FontAttributes = FontAttributes.Bold };
            cg.Add(badge, 0, 0);
            string dt = idea.Title.Length > 120 ? idea.Title.Substring(0, 117) + "..." : idea.Title;
            cg.Add(new Label { Text = dt, FontSize = 12, TextColor = Color.FromArgb("#333"), LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center }, 1, 0);
            string ft = idea.Title;
            var sb = new Button { Text = "Select", FontSize = 11, HeightRequest = 30, BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 6, Padding = new Thickness(10, 0), VerticalOptions = LayoutOptions.Center };
            sb.Clicked += async (s, e) => await LockIdeaAsync(ft);
            cg.Add(sb, 2, 0);
            card.Content = cg;
            _ideasListStack.Children.Add(card);
        }
    }

    // ===================== PARSER =====================

    private static List<string> ParseIdeas(string response)
    {
        var ideas = new List<string>();
        const string open = "ideas.Add(\""; const string close = "\");";
        int pos = 0;
        while (pos < response.Length)
        {
            int oi = response.IndexOf(open, pos, StringComparison.Ordinal);
            if (oi < 0) break;
            int cs = oi + open.Length; int ci = -1; int sf = cs;
            while (sf < response.Length) { int c = response.IndexOf(close, sf, StringComparison.Ordinal); if (c < 0) break; if (c > 0 && response[c - 1] == '\\') { sf = c + 1; continue; } ci = c; break; }
            if (ci < 0) { sf = cs; while (sf < response.Length) { int c = response.IndexOf("\")", sf, StringComparison.Ordinal); if (c < 0) break; if (c > 0 && response[c - 1] == '\\') { sf = c + 1; continue; } ci = c; break; } }
            if (ci > cs) { string t = response.Substring(cs, ci - cs).Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\"); if (!string.IsNullOrWhiteSpace(t)) ideas.Add(t.Trim()); pos = ci + 2; }
            else pos = cs;
        }
        return ideas;
    }
}

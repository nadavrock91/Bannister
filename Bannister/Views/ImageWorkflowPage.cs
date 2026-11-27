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
public class ImageWorkflowPage : Bannister.Views.ModeThemedPage
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


        // Restore locked idea if it matches this workflow
        if (_project.LockedWorkflow == config.IdeasCategoryPrefix && !string.IsNullOrWhiteSpace(_project.LockedIdea))
            _chosenIdea = _project.LockedIdea;

        BuildUI();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 16, Spacing = 2 };

        mainStack.Children.Add((new Label { Text = $"{_config.PageIcon} {_config.PageTitle}", FontSize = 22, FontAttributes = FontAttributes.Bold, Margin = new Thickness(4, 0, 0, 4) }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText1));
        mainStack.Children.Add((new Label { Text = $"Project: {_project.Name}", FontSize = 13, Margin = new Thickness(4, 0, 0, 2) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2));

        string preview = _project.StoryDescription.Length > 150 ? _project.StoryDescription.Substring(0, 150) + "..." : _project.StoryDescription;
        var storyFrame = (new Frame { Padding = 10, CornerRadius = 8, HasShadow = false, Margin = new Thickness(0, 0, 0, 8) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightYellow).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.WarningButtonBackground);
        storyFrame.Content = (new Label { Text = $"📝 {preview}", FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3);
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
            _chosenLabel.ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText1);
            _step1Content.IsVisible = false; _step1Arrow.Text = "▶";
            _step2Content.IsVisible = false; _step2Arrow.Text = "▶";
            _step3Content.IsVisible = true; _step3Arrow.Text = "▼";
        }

        Content = new ScrollView { Content = mainStack };
    }

    // ===================== SCENE CONTEXT =====================

    private View BuildSceneContext()
    {
        _sceneFrame = (new Frame { CornerRadius = 0, Padding = 16, HasShadow = false, Margin = new Thickness(0, 0, 0, 2) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.HighlightVioletText);
        var stack = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add((new Label { Text = "SCENE CONTEXT", FontSize = 12, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1 }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText3));
        stack.Children.Add((new Label { Text = "Describe the specific scene/clip you need the first frame for:", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        _sceneContextEditor = (new Editor { Text = _project.SceneContext ?? "", Placeholder = "e.g. The wizard trapped under a fallen tree, lion approaching from above...", HeightRequest = 80, FontSize = 13 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        stack.Children.Add(_sceneContextEditor);
        var saveBtn = (new Button { Text = "💾 Save Scene", FontSize = 12, HeightRequest = 34, CornerRadius = 6, Padding = new Thickness(12, 0), HorizontalOptions = LayoutOptions.Start }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
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
        var header = BuildStepHeader("STEP 1: GET IDEAS", "Generate prompt → paste LLM response → import", ThemeKey.WarningText, out _step1Arrow);
        _step1Content = (new Frame { CornerRadius = 0, Padding = 16, HasShadow = false, IsVisible = _chosenIdea == null }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
        header.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => { _step1Content.IsVisible = !_step1Content.IsVisible; _step1Arrow.Text = _step1Content.IsVisible ? "▼" : "▶"; }) });
        if (_chosenIdea == null) _step1Arrow.Text = "▼";

        var stack = new VerticalStackLayout { Spacing = 10 };

        var generateBtn = (new Button { Text = "⚡ Generate Prompt", FontSize = 13, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        generateBtn.Clicked += (s, e) =>
        {
            string scene = _config.NeedsSceneContext ? (_sceneContextEditor?.Text?.Trim() ?? "") : "";
            _promptEditor.Text = _config.BuildIdeasPrompt(_project.StoryDescription.Trim(), scene);
        };
        stack.Children.Add(generateBtn);

        _promptEditor = (new Editor { Placeholder = "Prompt will appear here...", HeightRequest = 160, FontSize = 11, FontFamily = "Consolas", IsReadOnly = true }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        stack.Children.Add(_promptEditor);
        stack.Children.Add(CreateCopyButton(_promptEditor));

        stack.Children.Add((new BoxView { HeightRequest = 1, Margin = new Thickness(0, 6) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator));
        stack.Children.Add((new Label { Text = "Paste LLM response:", FontSize = 12, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        _responsePasteEditor = (new Editor { Placeholder = "Paste the ideas.Add(\"...\") lines here...", HeightRequest = 160, FontSize = 12 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        stack.Children.Add(_responsePasteEditor);

        var btnRow = new HorizontalStackLayout { Spacing = 8 };
        var importBtn = (new Button { Text = "📥 Import to Ideas", FontSize = 13, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        importBtn.Clicked += OnImportClicked;
        btnRow.Children.Add(importBtn);
        var skipBtn = (new Button { Text = "Skip →", FontSize = 12, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
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
        var header = BuildStepHeader("STEP 2: CHOOSE IDEA", "Pick which idea to develop", ThemeKey.InfoText, out _step2Arrow);
        _step2Content = (new Frame { CornerRadius = 0, Padding = 16, HasShadow = false, IsVisible = false }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
        header.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => { _step2Content.IsVisible = !_step2Content.IsVisible; _step2Arrow.Text = _step2Content.IsVisible ? "▼" : "▶"; if (_step2Content.IsVisible) await LoadIdeasAsync(); }) });

        var stack = new VerticalStackLayout { Spacing = 10 };

        var loadBtn = (new Button { Text = "🔄 Load Ideas", FontSize = 12, HeightRequest = 34, CornerRadius = 6, Padding = new Thickness(12, 0), HorizontalOptions = LayoutOptions.Start }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        loadBtn.Clicked += async (s, e) => await LoadIdeasAsync();
        stack.Children.Add(loadBtn);

        _ideasListStack = new VerticalStackLayout { Spacing = 6 };
        _ideasListStack.Children.Add((new Label { Text = "No ideas loaded yet.", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));
        stack.Children.Add(_ideasListStack);

        stack.Children.Add((new BoxView { HeightRequest = 1, Margin = new Thickness(0, 4) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator));
        stack.Children.Add((new Label { Text = "Or write your own:", FontSize = 12, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        _manualIdeaEntry = (new Editor { Placeholder = "Write a custom idea here...", HeightRequest = 60, FontSize = 13 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        stack.Children.Add(_manualIdeaEntry);

        var manualRow = new HorizontalStackLayout { Spacing = 8 };
        var lockBtn = (new Button { Text = "✅ Lock Custom Idea", FontSize = 12, HeightRequest = 34, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        lockBtn.Clicked += async (s, e) =>
        {
            var t = _manualIdeaEntry.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(t)) return;

            // Save custom idea to Ideas DB so it persists
            var logger = Application.Current?.Handler?.MauiContext?.Services.GetService<IdeaLoggerService>();
            if (logger != null)
                await logger.LogSilentlyAsync(new IdeaLogRequest
                {
                    Username = _project.Username,
                    Title = t,
                    Body = t,
                    Category = GetCategory()
                });

            await LockIdeaAsync(t);
        };
        manualRow.Children.Add(lockBtn);
        var skipBtn2 = (new Button { Text = "Skip →", FontSize = 12, HeightRequest = 34, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        skipBtn2.Clicked += (s, e) => { _step2Content.IsVisible = false; _step2Arrow.Text = "▶"; _step3Content.IsVisible = true; _step3Arrow.Text = "▼"; };
        manualRow.Children.Add(skipBtn2);
        stack.Children.Add(manualRow);

        _chosenLabel = (new Label { Text = "No idea chosen yet.", FontSize = 12, Margin = new Thickness(0, 4, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3);
        stack.Children.Add(_chosenLabel);

        // Unlock button
        var unlockBtn = (new Button { Text = "🔓 Unlock Current Idea", FontSize = 11, HeightRequest = 30, CornerRadius = 6, Padding = new Thickness(10, 0), HorizontalOptions = LayoutOptions.Start }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
        unlockBtn.Clicked += async (s, e) =>
        {
            _chosenIdea = null;
            _project.LockedIdea = null; _project.LockedWorkflow = null;
            if (_imageService != null) await _imageService.UpdateProjectAsync(_project);
            _chosenLabel.Text = "No idea chosen yet."; _chosenLabel.ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText1);
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
        var header = BuildStepHeader("STEP 3: GENERATE IMAGE", "Direct attempt first, then blockout on failure", ThemeKey.SuccessText, out _step3Arrow);
        _step3Content = (new Frame { CornerRadius = 0, Padding = 16, HasShadow = false, IsVisible = false }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
        header.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(() => { _step3Content.IsVisible = !_step3Content.IsVisible; _step3Arrow.Text = _step3Content.IsVisible ? "▼" : "▶"; }) });

        var mainStack = new VerticalStackLayout { Spacing = 12 };

        // Stage A
        mainStack.Children.Add(StageLabel("STAGE A: DIRECT ATTEMPT", "Generate a detailed image prompt.", ThemeKey.SuccessText));
        var directBtn = (new Button { Text = "⚡ Build Direct Image Prompt", FontSize = 13, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        directBtn.Clicked += (s, e) => { var (i, t) = SplitIdea(); _directPromptEditor.Text = _config.BuildDirectPrompt(i, t); };
        mainStack.Children.Add(directBtn);
        _directPromptEditor = (new Editor { Placeholder = "Image prompt...", HeightRequest = 180, FontSize = 12, FontFamily = "Consolas" }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        mainStack.Children.Add(_directPromptEditor);
        mainStack.Children.Add(CreateCopyButton(_directPromptEditor));

        mainStack.Children.Add((new Label { Text = "Did it work?", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 6, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        var resultRow = new HorizontalStackLayout { Spacing = 10 };
        var successBtn = (new Button { Text = "✅ Success!", FontSize = 13, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        successBtn.Clicked += async (s, e) => await DisplayAlert("Done!", "Your image is ready for production.", "OK");
        resultRow.Children.Add(successBtn);
        var failBtn = (new Button { Text = "❌ Failed — Try Blockout", FontSize = 13, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        failBtn.Clicked += (s, e) => _stageBContainer.IsVisible = true;
        resultRow.Children.Add(failBtn);
        mainStack.Children.Add(resultRow);

        // Stage B
        _stageBContainer = new VerticalStackLayout { Spacing = 10, IsVisible = false };
        _stageBContainer.Children.Add((new BoxView { HeightRequest = 2, Margin = new Thickness(0, 8) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorText));
        _stageBContainer.Children.Add(StageLabel("STAGE B: STRUCTURAL BLOCKOUT", "Start simple, nail composition, then build up.", ThemeKey.WarningText));

        _stageBContainer.Children.Add((new Label { Text = "B1: BLOCKOUT PROMPT", FontSize = 11, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3));
        var blockBtn = (new Button { Text = "🔲 Build Blockout Prompt", FontSize = 13, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        blockBtn.Clicked += (s, e) => { var (i, t) = SplitIdea(); _blockoutPromptEditor.Text = _config.BuildBlockoutPrompt(i, t); };
        _stageBContainer.Children.Add(blockBtn);
        _blockoutPromptEditor = (new Editor { Placeholder = "Blockout prompt...", HeightRequest = 140, FontSize = 12, FontFamily = "Consolas" }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground);
        _stageBContainer.Children.Add(_blockoutPromptEditor);
        _stageBContainer.Children.Add(CreateCopyButton(_blockoutPromptEditor));

        // Blockout result buttons
        _stageBContainer.Children.Add((new Label { Text = "How did the blockout turn out?", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 6, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        var blockoutResultRow = new HorizontalStackLayout { Spacing = 8 };
        var blockoutOkBtn = (new Button { Text = "✅ Composition is right → B2", FontSize = 12, HeightRequest = 36, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        blockoutOkBtn.Clicked += (s, e) => { _b2Container.IsVisible = true; };
        blockoutResultRow.Children.Add(blockoutOkBtn);

        var blockoutFixBtn = (new Button { Text = "🔄 Close but needs fixes", FontSize = 12, HeightRequest = 36, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        blockoutFixBtn.Clicked += (s, e) => { _iterationContainer.IsVisible = true; };
        blockoutResultRow.Children.Add(blockoutFixBtn);

        var blockoutRetryBtn = (new Button { Text = "❌ Totally wrong → Retry", FontSize = 12, HeightRequest = 36, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        blockoutRetryBtn.Clicked += (s, e) => { var (i, t) = SplitIdea(); _blockoutPromptEditor.Text = _config.BuildBlockoutPrompt(i, t); _iterationCount = 0; _iterationCountLabel.Text = ""; };
        blockoutResultRow.Children.Add(blockoutRetryBtn);
        _stageBContainer.Children.Add(blockoutResultRow);

        // ====== ITERATION LOOP (hidden until "needs fixes") ======
        _iterationContainer = new VerticalStackLayout { Spacing = 10, IsVisible = false };

        _iterationContainer.Children.Add((new BoxView { HeightRequest = 1, Margin = new Thickness(0, 6) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningText));
        _iterationContainer.Children.Add((new Label { Text = "🔄 FIX BLOCKOUT", FontSize = 11, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3));

        _iterationCountLabel = (new Label { Text = "", FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3);
        _iterationContainer.Children.Add(_iterationCountLabel);

        _iterationContainer.Children.Add((new Label { Text = "Describe what's wrong with the blockout:", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        _fixDescriptionEditor = (new Editor { Placeholder = "e.g. Robot is too far away, should fill more of the upper frame. Man needs to be slightly larger. Road is too wide — narrow it so machinery is closer. Machinery on left side is too short.", HeightRequest = 80, FontSize = 13 }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
        _iterationContainer.Children.Add(_fixDescriptionEditor);

        // Two options: with reference image or without
        _iterationContainer.Children.Add((new Label { Text = "Generate correction prompt:", FontSize = 12, FontAttributes = FontAttributes.Bold, Margin = new Thickness(0, 4, 0, 0) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));

        var iterBtnRow = new HorizontalStackLayout { Spacing = 8 };

        var iterWithRefBtn = (new Button { Text = "🖼️ With Reference Image", FontSize = 12, HeightRequest = 36, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        iterWithRefBtn.Clicked += (s, e) => BuildIterationPrompt(withReference: true);
        iterBtnRow.Children.Add(iterWithRefBtn);

        var iterNoRefBtn = (new Button { Text = "📝 Without Reference", FontSize = 12, HeightRequest = 36, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        iterNoRefBtn.Clicked += (s, e) => BuildIterationPrompt(withReference: false);
        iterBtnRow.Children.Add(iterNoRefBtn);

        _iterationContainer.Children.Add(iterBtnRow);

        _iterationPromptEditor = (new Editor { Placeholder = "Correction prompt will appear here...", HeightRequest = 180, FontSize = 12, FontFamily = "Consolas" }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningBackground);
        _iterationContainer.Children.Add(_iterationPromptEditor);
        _iterationContainer.Children.Add(CreateCopyButton(_iterationPromptEditor));

        _iterationContainer.Children.Add((new Label { Text = "Copy this → paste to LLM → get corrected blockout prompt → feed to image generator → check again.", FontSize = 11, FontAttributes = FontAttributes.Italic }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3));

        // After iteration, same three choices
        var iterResultRow = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        var iterOkBtn = (new Button { Text = "✅ Now it's right → B2", FontSize = 12, HeightRequest = 36, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        iterOkBtn.Clicked += (s, e) => { _b2Container.IsVisible = true; };
        iterResultRow.Children.Add(iterOkBtn);

        var iterAgainBtn = (new Button { Text = "🔄 Still needs fixes", FontSize = 12, HeightRequest = 36, CornerRadius = 6, Padding = new Thickness(12, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        iterAgainBtn.Clicked += (s, e) => { _fixDescriptionEditor.Text = ""; _iterationPromptEditor.Text = ""; };
        iterResultRow.Children.Add(iterAgainBtn);
        _iterationContainer.Children.Add(iterResultRow);

        _stageBContainer.Children.Add(_iterationContainer);

        // ====== B2: FULL PROMPT (hidden until composition approved) ======
        _b2Container = new VerticalStackLayout { Spacing = 10, IsVisible = false };

        _b2Container.Children.Add((new BoxView { HeightRequest = 1, Margin = new Thickness(0, 6) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator));
        _b2Container.Children.Add((new Label { Text = "B2: FULL PROMPT (from blockout)", FontSize = 11, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.WarningText, ThemeKey.PageText3));
        _b2Container.Children.Add((new Label { Text = "Feed the blockout image as reference alongside this prompt.", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3));
        var refBtn = (new Button { Text = "🎨 Build Full Prompt from Blockout", FontSize = 13, HeightRequest = 40, CornerRadius = 6, Padding = new Thickness(16, 0) }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        refBtn.Clicked += (s, e) => { var (i, t) = SplitIdea(); _refinedPromptEditor.Text = _config.BuildRefinedPrompt(i, t); };
        _b2Container.Children.Add(refBtn);
        _refinedPromptEditor = (new Editor { Placeholder = "Refined prompt...", HeightRequest = 180, FontSize = 12, FontFamily = "Consolas" }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);
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
        _chosenLabel.ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText1);

        // Persist
        _project.LockedIdea = text;
        _project.LockedWorkflow = _config.IdeasCategoryPrefix;
        if (_imageService != null) await _imageService.UpdateProjectAsync(_project);

        _step3Content.IsVisible = true; _step3Arrow.Text = "▼";
    }

    private View StageLabel(string t, string s, ThemeKey c) { var st = new VerticalStackLayout { Spacing = 2, Margin = new Thickness(0, 2, 0, 4) }; st.Children.Add((new Label { Text = t, FontSize = 13, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1 }).ThemeColor(Label.TextColorProperty, c, ThemeKey.PageText1)); st.Children.Add((new Label { Text = s, FontSize = 11 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3)); return st; }
    private Button CreateCopyButton(Editor ed) { var b = (new Button { Text = "📋 Copy", FontSize = 12, HeightRequest = 34, CornerRadius = 6, Padding = new Thickness(12, 0), HorizontalOptions = LayoutOptions.Start }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText2); b.Clicked += async (s, e) => { if (!string.IsNullOrEmpty(ed.Text)) { await Clipboard.SetTextAsync(ed.Text); b.Text = "✓"; await Task.Delay(1500); b.Text = "📋 Copy"; } }; return b; }
    private Grid BuildStepHeader(string t, string s, ThemeKey c, out Label arrow) { var g = (new Grid { Padding = new Thickness(14, 10), ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10 }).ThemeColor(VisualElement.BackgroundColorProperty, c); arrow = (new Label { Text = "▶", FontSize = 13, VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1); g.Add(arrow, 0, 0); var ts = new VerticalStackLayout { Spacing = 1 }; ts.Children.Add((new Label { Text = t, FontSize = 13, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1 }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1)); ts.Children.Add(new Label { Text = s, FontSize = 10, TextColor = AppColors.Get(ThemeKey.ButtonPrimaryText).WithAlpha(0.7f) }); g.Add(ts, 1, 0); return g; }

    // ===================== IMPORT =====================

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        string response = _responsePasteEditor.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(response)) { await DisplayAlert("Empty", "Paste the LLM response first.", "OK"); return; }
        var ideas = ParseIdeas(response);
        if (ideas.Count == 0) { await DisplayAlert("No Ideas", "Could not parse any ideas.Add(\"...\") lines.", "OK"); return; }
        string cat = GetCategory();
        if (!await DisplayAlert("Import", $"Found {ideas.Count} ideas.\nImport to \"{cat}\"?", "Import", "Cancel")) return;
        var logger = Application.Current?.Handler?.MauiContext?.Services.GetService<IdeaLoggerService>();
        int savedCount = 0;
        IReadOnlyList<IdeaLogFailure> failures = Array.Empty<IdeaLogFailure>();
        if (logger != null)
        {
            var requests = ideas.Select(t => new IdeaLogRequest
            {
                Username = _project.Username,
                Title = t,
                Body = t,
                Category = cat
            });
            var result = await logger.LogBatchSilentAsync(requests);
            savedCount = result.SavedCount;
            failures = result.Failures;
        }

        string failureText = logger == null
            ? "\nIdeas logger unavailable; logging was skipped."
            : failures.Count == 0
                ? ""
                : $"\nFailed: {failures.Count}\n" + string.Join("\n", failures.Take(5).Select(f => $"• {f.Request.Title}: {f.Error}"));
        await DisplayAlert("Import Complete", $"Saved {savedCount} of {ideas.Count} ideas to \"{cat}\".{failureText}", "OK");
        _step1Content.IsVisible = false; _step1Arrow.Text = "▶";
        _step2Content.IsVisible = true; _step2Arrow.Text = "▼";
        await LoadIdeasAsync();
    }

    private async Task LoadIdeasAsync()
    {
        _ideasListStack.Children.Clear();
        IdeasService? svc = null;
        try { svc = Application.Current?.Handler?.MauiContext?.Services?.GetService<IdeasService>(); } catch { }
        if (svc == null) { _ideasListStack.Children.Add((new Label { Text = "Could not load.", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, ThemeKey.PageText3)); return; }
        var ideas = await svc.GetIdeasByCategoryAsync(_project.Username, GetCategory());
        if (ideas.Count == 0) { _ideasListStack.Children.Add((new Label { Text = "No ideas found. Import in Step 1.", FontSize = 12 }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3)); return; }
        _ideasListStack.Children.Add((new Label { Text = $"{ideas.Count} ideas:", FontSize = 12, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText3));
        for (int i = 0; i < ideas.Count; i++)
        {
            var idea = ideas[i]; int num = i + 1;
            var card = (new Frame { CornerRadius = 8, Padding = 10, HasShadow = false }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);
            var cg = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 10 };
            var badge = (new Frame { CornerRadius = 12, Padding = new Thickness(8, 4), HasShadow = false, VerticalOptions = LayoutOptions.Start }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoText);
            badge.Content = (new Label { Text = num.ToString(), FontSize = 11, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3);
            cg.Add(badge, 0, 0);
            string dt = idea.Title.Length > 120 ? idea.Title.Substring(0, 117) + "..." : idea.Title;
            cg.Add((new Label { Text = dt, FontSize = 12, LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3), 1, 0);
            string ft = !string.IsNullOrWhiteSpace(idea.FullIdea) ? idea.FullIdea.Trim() : idea.Title;
            var sb = (new Button { Text = "Select", FontSize = 11, HeightRequest = 30, CornerRadius = 6, Padding = new Thickness(10, 0), VerticalOptions = LayoutOptions.Center }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
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

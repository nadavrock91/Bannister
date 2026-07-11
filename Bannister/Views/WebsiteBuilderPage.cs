using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Storage;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Bannister.Views;

public class WebsiteBuilderPage : ContentPage
{
    private enum WebsiteWorkflowState
    {
        Idle = 0,
        WaitingForLLM = 1,
        ReadyToExecute = 2,
        ReadyToCommit = 3
    }

    private const string IdeasPrompt = @"I'm looking for 100 website ideas to build for the purpose of making money online.

Generate the ideas based on what you know about me from public information and context: I'm Nadav, an independent content creator and software developer. I make AI-generated short films and philosophical video essays — typically 3-minute shorts and 8-minute vlog essays — with a painterly storybook aesthetic and themes around philosophy, AI, consciousness, fear, discipline, and human potential. On the software side I build Bannister, a gamified productivity app using .NET MAUI 8 and C# with SQLite, plus a few side projects involving image processing pipelines, Etsy publishing workflows, and AI image management tools. I have a daily creative discipline of producing one story or video per day. I'm comfortable with C#, .NET, basic web stack, and AI tooling.

Generate 100 distinct website ideas tailored to my background and skills that could realistically generate revenue. For each idea include:
- A short name/title (one line)
- A one-paragraph description of what the site is and how it makes money
- The primary monetization model (e.g. ads, subscription, digital product, affiliate, services, marketplace)
- Rough difficulty to build (low/medium/high) and time to first revenue (weeks/months)

Format the output as a numbered list 1 to 100. Be specific and concrete. Avoid generic ideas like 'a blog' or 'sell a course' — every idea should have a unique angle, niche, or mechanic. Some ideas should leverage my existing creator audience and content, some should leverage my dev skills, some should be standalone web products that don't depend on either.

No preamble. Start directly with idea 1.";

    private const string DomainNamesPromptTemplate = @"I have an idea for a website I want to build. Suggest 20 domain name candidates for it.

My idea:
Title: {0}

Description:
{1}

Constraints for the domain suggestions:
- Short (ideally 4 to 14 characters before the TLD)
- Brandable and easy to spell
- Easy to remember and pronounce
- No hyphens, no numbers
- .com preferred; also include some .ai .io .co .app variants
- Mix of literal-descriptive names (clear what the site is about) and made-up brandable names (memorable, distinctive)
- Suggest both single-word and compound-word options
- Avoid trademark conflicts with major brands

Output as a plain numbered list 1 to 20, one domain per line, with the TLD included on each line. No commentary, no explanation, no preamble. Just the numbered list of domain names.";

    private const string UpdateSummaryPrompt = "You are reviewing a website-in-progress to produce a summary that will give context to a fresh LLM chat session later.\n\nRead through the project files in this folder and produce a concise summary covering:\n\n1. WHAT THE SITE IS: one-paragraph description of the project's purpose and target audience based on the code.\n2. CURRENT STATE: what pages, components, and features exist right now.\n3. TECH STACK: framework, key dependencies, styling approach.\n4. STRUCTURE: notable folder organization decisions.\n5. RECENT WORK: from git log if available, what's been changed recently.\n6. WHAT'S MISSING: obvious gaps or unfinished pieces.\n\nKeep the summary to 200-400 words total. Use plain text with clear section headers. Output ONLY the summary text - no preamble, no follow-up offers, just the summary I can copy into a separate tool.";

    private const string VisionRefinementPromptTemplate = "I'm building a website called {0}. Below is my raw description of what I want this site to be, written informally in my own words. Please rewrite this as a clear concise vision statement suitable for handing to other LLMs as context for development tasks. Keep the spirit of what I said but make it well-structured, complete, and actionable. Cover: target audience, core purpose, key features the site should have, the tone/feel, and what makes it different from alternatives. Output ONLY the rewritten vision text - no preamble, no follow-up offers.\n\nMY RAW DESCRIPTION:\n{1}";

    private const string LegacyBatchNextTaskPromptTemplateHeader = """
You're planning the next {BATCH_SIZE} tasks for a website project. Below is the project context. Instead of one task, propose {BATCH_SIZE} tasks in a coherent short arc where each task assumes the previous one landed. Order them so the arc makes sense.

For each task output:
- A short TITLE (5-10 words) describing the change.
- A CODEX PROMPT ready to paste directly into codex with all the context Codex needs to execute the change alone.

Output format — return ONLY this block, one section per task numbered TASK 1 through TASK {BATCH_SIZE}, no explanation, no preamble, no closing remarks:

TASK 1:
TITLE: <short title>
CODEX PROMPT:
<multi-line codex prompt>

TASK 2:
TITLE: <short title>
CODEX PROMPT:
<multi-line codex prompt>

...continue through TASK {BATCH_SIZE}.

PROJECT CONTEXT BELOW:

""";

    private const string BatchNextTaskPromptTemplateHeader = """
You're planning the next {BATCH_SIZE} tasks worth of work for a website project as a single combined arc. Instead of returning {BATCH_SIZE} separate Codex prompts, return ONE Codex prompt that describes all {BATCH_SIZE} tasks' worth of changes bundled together. Codex will execute the whole thing in one session and output one commit message at the end.

Plan a coherent arc where each piece of work builds on the previous. Order the changes so they compound sensibly. Be specific about file paths, function names, and expected behavior for every piece of the arc.

Output format — return ONLY these two sections in this order, no explanation, no preamble, no closing remarks:

ARC TITLE:
<one short title, 6-12 words, summarizing the whole batch>

CODEX PROMPT:
<the full combined codex prompt covering all {BATCH_SIZE} tasks' worth of changes>

The CODEX PROMPT section should end with these instructions to Codex verbatim:

"IMPORTANT: Do NOT run git add, git commit, git push, or any other git command. Do NOT stage or commit changes. Only edit files. Bannister will run the commit and push for you after you output the commit message below.
At the end of your work, output a single line in this format: COMMIT MESSAGE: <one-line git commit message describing everything you did across all changes>"

PROJECT CONTEXT BELOW:

""";

    private const string DefaultInvestigationPrompt = """
INVESTIGATION TASK — no code changes, no builds, no git. Read-only.

Report findings under a REPORT heading. Read the codebase carefully. Do not attempt to fix anything in this pass.

The following symptom has recurred multiple times across previous fix attempts:

{SYMPTOM_DESCRIPTION}

Related files/code paths (if the user has surfaced any):
{RELATED_PATHS}

Your task:

1. Locate the code paths involved in this symptom. Report file paths, class names, method names, and quote the current implementation.

2. Read prior fix attempts (recent commits or code comments referencing the symptom) and report what was previously tried. This is critical — if the same fix keeps being re-attempted, that pattern is itself evidence of a wrong root-cause hypothesis.

3. Trace the actual data flow: what does the code EXPECT to happen, what could ACTUALLY happen at each step, and where does the assumption break down. Consider:
   - API endpoint responses may differ from documentation.
   - Rate limits, timeouts, or empty responses.
   - Race conditions on client-side hydration.
   - Cached stale data.
   - Silent try/catch swallowing errors.
   - Type coercion issues.
   - Off-by-one on pagination or timestamps.

4. Propose a ROOT-CAUSE hypothesis, not a symptom-level patch. Explain what is fundamentally wrong. If multiple hypotheses fit the evidence, list them ranked by likelihood.

5. Propose a structural fix. Describe what to change and why. Do NOT write the code — this is a diagnosis, not a fix. The user will submit a follow-up fix task based on your findings.

6. If the true root cause is undiagnosable without more evidence, list what evidence would clarify it (specific log lines to add, specific test cases to run, specific API responses to capture).

Report format: numbered sections matching the questions above. Quote code where relevant. At the end, one short paragraph with your best-guess root cause and the single most important next diagnostic or fix step.

Do not modify any files. Do not run builds. Do not run git.
""";

    private const string DefaultArcFromPicksPrompt = """
You're planning the next arc of work for a website project as a single combined Codex prompt. Below are 5 items picked at random from the latest QA report, spanning BROKEN, ROUGH, and MISSING categories. Bundle them into ONE Codex prompt that describes all 5 tasks' worth of changes as a coherent arc.

Plan the arc so each piece of work builds on the previous where possible. Order the changes so they compound sensibly. Be specific about file paths, function names, and expected behavior for every piece of the arc.

If a MISSING item is large enough that it would consume the whole arc's scope, dedicate the arc to that single MISSING item and note that the other picks were deferred. Do not add net-new items — work only from the 5 below.

Output format — return ONLY these two sections in this order, no explanation, no preamble, no closing remarks:

ARC TITLE:
<one short title, 6-12 words>

CODEX PROMPT:
<the full combined codex prompt covering the picked items>

The CODEX PROMPT section should end with these instructions to Codex verbatim:

"IMPORTANT: Do NOT run git add, git commit, git push, or any other git command. Do NOT stage or commit changes. Only edit files. Bannister will run the commit and push for you after you output the commit message below.
At the end of your work, output a single line in this format: COMMIT MESSAGE: <one-line git commit message describing everything you did across all changes>"

THE 5 PICKED ITEMS:

{PICKED_ITEMS}
""";

    private readonly AuthService _auth;
    private readonly WebsiteProjectService _projectService;
    private readonly WebsiteIdeaService _ideaService;
    private readonly GameService _gameService;
    private readonly Picker _ideaPicker;
    private readonly Picker _projectPicker;
    private readonly Entry _ideaTitleEntry;
    private readonly Editor _ideaEditor;
    private readonly Button _deleteIdeaButton;
    private readonly VerticalStackLayout _ideaSection;
    private readonly VerticalStackLayout _domainSection;
    private readonly Entry _purchasedDomainEntry;
    private readonly VerticalStackLayout _taskCounterSection;
    private readonly Frame _workflowStatusBanner;
    private readonly Label _workflowStatusIcon;
    private readonly Label _workflowStatusTitle;
    private readonly Label _workflowStatusSubtitle;
    private readonly Grid _workflowStartRow;
    private readonly Button _pickFromQaBtn;
    private readonly Button _investigateBtn;
    private readonly Button _workflowCopyNextTaskPromptButton;
    private readonly Picker _batchSizePicker;
    private readonly HorizontalStackLayout _batchSizeRow;
    private readonly Button _copyBatchPromptButton;
    private readonly Button _pasteTaskPlanButton;
    private readonly Button _cancelWorkflowButton;
    private readonly Button _copyCodexPromptButton;
    private readonly Button _pasteCodexResultButton;
    private readonly Button _editTaskTitleButton;
    private readonly Button _editCodexPromptButton;
    private readonly Button _editCommitMessageButton;
    private readonly Button _commitAndPushButton;
    private readonly Label _qaStatusLabel;
    private readonly Button _copyQAExplorationPromptButton;
    private readonly Button _pasteQAReportButton;
    private readonly Button _clearQAReportButton;
    private readonly Label _projectTitleHeaderLabel;
    private readonly Label _projectIdeaReferenceLabel;
    private readonly Label _visionStatusLabel;
    private readonly Label _visionPreviewLabel;
    private readonly Button _editVisionRawButton;
    private readonly Button _copyVisionRefinementPromptButton;
    private readonly Button _pasteVisionRefinedButton;
    private readonly Label _taskCountLabel;
    private readonly Button _incrementButton;
    private readonly Button _decrementButton;
    private readonly Button _editCountButton;
    private readonly Button _setTargetButton;
    private readonly Button _viewTaskLogButton;
    private readonly Frame _celebrationFrame;
    private readonly Button _setNewTargetButton;
    private readonly Label _summaryStalenessLabel;
    private readonly Label _summaryPreviewLabel;
    private readonly Button _copySummaryPromptButton;
    private readonly Button _pasteSummaryResultButton;
    private readonly Label _codebasePathLabel;
    private readonly Button _editCodebasePathButton;
    private readonly Button _copyCodexCommandButton;
    private readonly Button _openTerminalButton;
    private readonly Button _deleteProjectButton;

    private List<WebsiteIdea> _ideasCache = new();
    private List<WebsiteProject> _projectsCache = new();
    private List<(string Category, string Body)> _parsedItems = new();
    private List<(string Category, string Body)> _pickedItems = new();
    private int _currentIdeaId;
    private int _currentProjectId;
    private bool _isRefreshingPickers;
    private bool _isLoadingBatchSize;

    public WebsiteBuilderPage(AuthService auth, WebsiteProjectService projectService, WebsiteIdeaService ideaService, GameService gameService)
    {
        _auth = auth;
        _projectService = projectService;
        _ideaService = ideaService;
        _gameService = gameService;
        Title = "Website Builder";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _ideaPicker = CreatePicker("Load a saved idea...");
        _ideaPicker.SelectedIndexChanged += async (_, _) => await OnIdeaSelectedAsync();

        _projectPicker = CreatePicker("Load a saved project...");
        _projectPicker.SelectedIndexChanged += async (_, _) => await OnProjectSelectedAsync();

        var newIdeaButton = CreateSecondaryButton("New Idea");
        newIdeaButton.Clicked += async (_, _) => await ClearAllAsync();

        var newProjectButton = CreateSecondaryButton("New Project");
        newProjectButton.Clicked += async (_, _) => await ClearProjectStateAsync();

        var copyIdeasButton = CreatePrimaryButton("Copy Ideas Prompt to Clipboard", Color.FromArgb("#01579B"));
        copyIdeasButton.Clicked += async (_, _) => await CopyIdeasPromptAsync();

        _ideaTitleEntry = new Entry
        {
            Placeholder = "Short idea title",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888")
        };

        _ideaEditor = new Editor
        {
            Placeholder = "Paste the selected website idea or notes here...",
            AutoSize = EditorAutoSizeOption.TextChanges,
            MinimumHeightRequest = 200,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888")
        };

        var saveIdeaButton = CreatePrimaryButton("Save Idea", Color.FromArgb("#2E7D32"));
        saveIdeaButton.Clicked += async (_, _) => await SaveIdeaAsync();

        _deleteIdeaButton = CreateDangerButton("Delete Idea");
        _deleteIdeaButton.IsVisible = false;
        _deleteIdeaButton.Clicked += async (_, _) => await DeleteIdeaAsync();

        var copyDomainPromptButton = CreatePrimaryButton("Copy Domain Names Prompt to Clipboard", Color.FromArgb("#01579B"));
        copyDomainPromptButton.Clicked += async (_, _) => await CopyDomainNamesPromptAsync();

        var goToGoDaddyButton = CreatePrimaryButton("Go to GoDaddy", Color.FromArgb("#01579B"));
        goToGoDaddyButton.TextColor = Colors.White;
        goToGoDaddyButton.FontAttributes = FontAttributes.Bold;
        goToGoDaddyButton.Clicked += async (_, _) => await Launcher.OpenAsync("https://www.godaddy.com/en");

        _purchasedDomainEntry = new Entry
        {
            Placeholder = "e.g. hookbrain.com",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888")
        };

        var saveProjectFromDomainButton = CreatePrimaryButton("Save as Project", Color.FromArgb("#2E7D32"));
        saveProjectFromDomainButton.Clicked += async (_, _) => await SaveProjectFromPurchasedDomainAsync();

        _domainSection = new VerticalStackLayout
        {
            Spacing = 12,
            IsVisible = false,
            Children =
            {
                CreateSectionHeader("Step 2: Pick and Purchase Domain"),
                copyDomainPromptButton,
                goToGoDaddyButton,
                new Label { Text = "Purchased Domain", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") },
                _purchasedDomainEntry,
                saveProjectFromDomainButton
            }
        };

        _ideaSection = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                CreateSectionHeader("Step 1: Ideas for Website"),
                copyIdeasButton,
                new Label { Text = "Idea Title", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") },
                _ideaTitleEntry,
                new Label { Text = "Selected Idea / Notes", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#333") },
                _ideaEditor,
                saveIdeaButton,
                _deleteIdeaButton
            }
        };

        _workflowStatusIcon = new Label
        {
            FontSize = 24,
            VerticalOptions = LayoutOptions.Start,
            WidthRequest = 34
        };

        _workflowStatusTitle = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#555")
        };

        _workflowStatusSubtitle = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#555"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _workflowCopyNextTaskPromptButton = new Button
        {
            Text = "Copy Next Task Prompt",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        _workflowCopyNextTaskPromptButton.Clicked += async (_, _) => await CopyNextTaskPromptAsync();

        _pickFromQaBtn = new Button
        {
            Text = " Pick 5 from QA report",
            BackgroundColor = Color.FromArgb("#FFF9C4"),
            TextColor = Color.FromArgb("#F57F17"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };
        _pickFromQaBtn.Clicked += OnPickFromQaClicked;

        _investigateBtn = new Button
        {
            Text = " Investigate stuck symptom",
            BackgroundColor = Color.FromArgb("#FFE0B2"),
            TextColor = Color.FromArgb("#E65100"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };
        _investigateBtn.Clicked += OnInvestigateStuckClicked;

        _workflowStartRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8
        };
        _workflowStartRow.Add(_pickFromQaBtn, 0, 0);
        _workflowStartRow.Add(_investigateBtn, 1, 0);
        _workflowStartRow.Add(_workflowCopyNextTaskPromptButton, 2, 0);

        _batchSizePicker = CreatePicker("Batch size");
        _batchSizePicker.ItemsSource = new List<string> { "3", "5", "7", "10" };
        _batchSizePicker.SelectedIndex = 1;
        _batchSizePicker.WidthRequest = 110;
        _batchSizePicker.SelectedIndexChanged += async (_, _) =>
        {
            UpdateCopyBatchPromptButtonText();
            await SaveBatchSizeSelectionAsync();
        };

        _batchSizeRow = new HorizontalStackLayout
        {
            Spacing = 8,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Batch size:",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#555"),
                    VerticalOptions = LayoutOptions.Center
                },
                _batchSizePicker
            }
        };

        _copyBatchPromptButton = new Button
        {
            Text = "Copy Batch Prompt (5 tasks)",
            BackgroundColor = Color.FromArgb("#F3E5F5"),
            TextColor = Color.FromArgb("#6A1B9A"),
            CornerRadius = 8,
            HeightRequest = 46,
            FontAttributes = FontAttributes.Bold
        };
        _copyBatchPromptButton.Clicked += async (_, _) => await CopyBatchNextTaskPromptAsync();

        _pasteTaskPlanButton = new Button
        {
            Text = "Paste Task Plan",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 46,
            FontAttributes = FontAttributes.Bold
        };
        _pasteTaskPlanButton.Clicked += async (_, _) => await PasteTaskPlanAsync();

        _cancelWorkflowButton = new Button
        {
            Text = "Cancel (Back to Start)",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };
        _cancelWorkflowButton.Clicked += async (_, _) => await CancelWorkflowAsync();

        _copyCodexPromptButton = new Button
        {
            Text = "Copy Codex Prompt",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 8,
            HeightRequest = 46,
            FontAttributes = FontAttributes.Bold
        };
        _copyCodexPromptButton.Clicked += async (_, _) => await CopyCodexPromptAsync();

        _pasteCodexResultButton = new Button
        {
            Text = "Paste Codex Result",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 46,
            FontAttributes = FontAttributes.Bold
        };
        _pasteCodexResultButton.Clicked += async (_, _) => await PasteCodexResultAsync();

        _editTaskTitleButton = CreateWorkflowSmallButton("Edit Task Title");
        _editTaskTitleButton.Clicked += async (_, _) => await EditPendingTaskTitleAsync();

        _editCodexPromptButton = CreateWorkflowSmallButton("Edit Codex Prompt");
        _editCodexPromptButton.Clicked += async (_, _) => await EditPendingCodexPromptAsync();

        _editCommitMessageButton = CreateWorkflowSmallButton("Edit Commit Message");
        _editCommitMessageButton.Clicked += async (_, _) => await EditPendingCommitMessageAsync();

        _commitAndPushButton = new Button
        {
            Text = "Commit and Push",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        _commitAndPushButton.Clicked += async (_, _) => await CommitAndPushAsync();

        var workflowHeader = new HorizontalStackLayout
        {
            Spacing = 10,
            Children =
            {
                _workflowStatusIcon,
                new VerticalStackLayout
                {
                    Spacing = 2,
                    HorizontalOptions = LayoutOptions.Fill,
                    Children =
                    {
                        _workflowStatusTitle,
                        _workflowStatusSubtitle
                    }
                }
            }
        };

        _workflowStatusBanner = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    workflowHeader,
                    _workflowStartRow,
                    _batchSizeRow,
                    _copyBatchPromptButton,
                    _pasteTaskPlanButton,
                    _copyCodexPromptButton,
                    _pasteCodexResultButton,
                    _editTaskTitleButton,
                    _editCodexPromptButton,
                    _editCommitMessageButton,
                    _commitAndPushButton,
                    _cancelWorkflowButton
                }
            }
        };

        _qaStatusLabel = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#555555"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _copyQAExplorationPromptButton = CreateSecondaryButton("Copy QA Exploration Prompt");
        _copyQAExplorationPromptButton.Clicked += async (_, _) => await CopyQAExplorationPromptAsync();

        _pasteQAReportButton = CreateSecondaryButton("Paste QA Report");
        _pasteQAReportButton.Clicked += async (_, _) => await PasteQAReportAsync();

        _clearQAReportButton = new Button
        {
            Text = "Clear QA Report",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333333"),
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 12,
            Padding = new Thickness(12, 0)
        };
        _clearQAReportButton.Clicked += async (_, _) => await ClearQAReportAsync();

        var qaButtonRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                _copyQAExplorationPromptButton,
                _pasteQAReportButton,
                _clearQAReportButton
            }
        };

        var qaExplorationSection = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "QA Exploration (optional)",
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#00695C")
                },
                _qaStatusLabel,
                qaButtonRow
            }
        };

        _projectTitleHeaderLabel = new Label
        {
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        };
        _projectIdeaReferenceLabel = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        _visionStatusLabel = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#888")
        };

        _visionPreviewLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#444"),
            MaxLines = 3,
            LineBreakMode = LineBreakMode.TailTruncation,
            IsVisible = false
        };

        _editVisionRawButton = new Button
        {
            Text = "Edit Vision (Your Words)",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        _editVisionRawButton.Clicked += async (_, _) => await EditVisionRawAsync();

        _copyVisionRefinementPromptButton = new Button
        {
            Text = "Copy Vision Refinement Prompt",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        _copyVisionRefinementPromptButton.Clicked += async (_, _) => await CopyVisionRefinementPromptAsync();

        _pasteVisionRefinedButton = new Button
        {
            Text = "Paste Refined Vision",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 42
        };
        _pasteVisionRefinedButton.Clicked += async (_, _) => await PasteVisionRefinedAsync();

        var visionSection = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Vision",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#333")
                },
                _visionStatusLabel,
                _visionPreviewLabel,
                _editVisionRawButton,
                _copyVisionRefinementPromptButton,
                _pasteVisionRefinedButton
            }
        };

        _taskCountLabel = new Label
        {
            FontSize = 48,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#222")
        };
        _incrementButton = CreatePrimaryButton("+1 Task", Color.FromArgb("#2E7D32"));
        _incrementButton.FontSize = 20;
        _incrementButton.HeightRequest = 58;
        _incrementButton.Clicked += async (_, _) => await OnIncrementClickedAsync();

        _decrementButton = CreateSecondaryButton("-1 Task");
        _decrementButton.Clicked += async (_, _) => await OnDecrementClickedAsync();

        _editCountButton = CreateSecondaryButton("Edit Count");
        _editCountButton.Clicked += async (_, _) => await OnEditCountClickedAsync();

        _setTargetButton = CreateSecondaryButton("Set Target");
        _setTargetButton.Clicked += async (_, _) => await OnSetTargetClickedAsync();

        _viewTaskLogButton = new Button
        {
            Text = "View Task Log",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        };
        _viewTaskLogButton.Clicked += async (_, _) => await ViewTaskLogAsync();

        var nextTaskSection = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Task Log",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#333")
                },
                new Label
                {
                    Text = "View or edit the list of completed tasks.",
                    FontSize = 12,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = Color.FromArgb("#888")
                },
                _viewTaskLogButton
            }
        };

        _setNewTargetButton = CreatePrimaryButton("Set New Target", Color.FromArgb("#2E7D32"));
        _setNewTargetButton.Clicked += async (_, _) => await OnSetNewTargetClickedAsync();

        _celebrationFrame = new Frame
        {
            Padding = 14,
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            BorderColor = Color.FromArgb("#2E7D32"),
            HasShadow = false,
            IsVisible = false,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label
                    {
                        Text = "🎉 Target reached!",
                        FontSize = 18,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#1B5E20")
                    },
                    _setNewTargetButton
                }
            }
        };

        _codebasePathLabel = new Label
        {
            FontSize = 13,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.TailTruncation
        };

        _editCodebasePathButton = new Button
        {
            Text = "Edit Code Folder Path",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13
        };
        _editCodebasePathButton.Clicked += async (_, _) => await EditCodebasePathAsync();

        _copyCodexCommandButton = new Button
        {
            Text = "Copy CD + Codex Command",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 8,
            HeightRequest = 42,
            FontAttributes = FontAttributes.Bold
        };
        _copyCodexCommandButton.Clicked += async (_, _) => await CopyCodexCommandAsync();

        _openTerminalButton = new Button
        {
            Text = "Open Terminal",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        _openTerminalButton.Clicked += async (_, _) => await OpenTerminalAsync();

        _summaryStalenessLabel = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#888")
        };

        _summaryPreviewLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#444"),
            MaxLines = 3,
            LineBreakMode = LineBreakMode.TailTruncation,
            IsVisible = false
        };

        _copySummaryPromptButton = new Button
        {
            Text = "Copy Update Summary Prompt",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 8,
            HeightRequest = 42
        };
        _copySummaryPromptButton.Clicked += async (_, _) => await CopyUpdateSummaryPromptAsync();

        _pasteSummaryResultButton = new Button
        {
            Text = "Paste Summary Result",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42,
            FontAttributes = FontAttributes.Bold
        };
        _pasteSummaryResultButton.Clicked += async (_, _) => await PasteSummaryResultAsync();

        var projectSummarySection = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Project Summary",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#333")
                },
                _summaryStalenessLabel,
                _summaryPreviewLabel,
                _copySummaryPromptButton,
                _pasteSummaryResultButton
            }
        };

        var projectFilesSection = new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = "Project Files",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#333")
                },
                _codebasePathLabel,
                _editCodebasePathButton,
                _copyCodexCommandButton,
                _openTerminalButton
            }
        };

        _deleteProjectButton = CreateDangerButton("Delete Project");
        _deleteProjectButton.Clicked += async (_, _) => await DeleteProjectAsync();

        var counterEditGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        counterEditGrid.Add(_decrementButton, 0, 0);
        counterEditGrid.Add(_editCountButton, 1, 0);

        _taskCounterSection = new VerticalStackLayout
        {
            Spacing = 14,
            IsVisible = false,
            Children =
            {
                qaExplorationSection,
                _workflowStatusBanner,
                _projectTitleHeaderLabel,
                _projectIdeaReferenceLabel,
                visionSection,
                _taskCountLabel,
                _incrementButton,
                counterEditGrid,
                _setTargetButton,
                nextTaskSection,
                _celebrationFrame,
                projectSummarySection,
                projectFilesSection,
                _deleteProjectButton
            }
        };

        var setupGuideButton = new Button
        {
            Text = "Setup Guide",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 6,
            HeightRequest = 32,
            FontSize = 13,
            Padding = new Thickness(12, 0),
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        };
        setupGuideButton.Clicked += async (_, _) => await Navigation.PushAsync(new WebsiteBuilderSetupGuidePage(_auth, _projectService));

        var mainScroll = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    CreateTitleRow(setupGuideButton),
                    new Label
                    {
                        Text = "Generate website ideas via LLM, save selected ones as projects.",
                        FontSize = 15,
                        TextColor = Color.FromArgb("#666"),
                        Margin = new Thickness(0, -6, 0, 8)
                    },
                    CreatePickerGrid(newIdeaButton, newProjectButton),
                    _taskCounterSection,
                    _ideaSection,
                    _domainSection
                }
            }
        };

        Content = new Grid
        {
            Children =
            {
                mainScroll
            }
        };
    }

    private static View CreateTitleRow(Button setupGuideButton)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        grid.Add(new Label
        {
            Text = "Website Builder",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        grid.Add(setupGuideButton, 1, 0);
        return grid;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadBatchSizeSelectionAsync();
        await RefreshPickersAsync();
        await TryLoadLastSelectedProjectAsync();
        RefreshStateVisibility();
        await ShowGamificationPromptIfNeededAsync();
    }

    private async Task ShowGamificationPromptIfNeededAsync()
    {
        var key = GetGamificationPromptShownKey();
        string? shown = null;
        try { shown = await SecureStorage.GetAsync(key); } catch { }

        if (!string.IsNullOrWhiteSpace(shown))
            return;

        var confirm = await DisplayAlert(
            "Make this a habit?",
            "Want to add a daily Game Activity for one website task per day with streak tracking?",
            "Yes",
            "No");

        try { await SecureStorage.SetAsync(key, DateTime.UtcNow.ToString("O")); } catch { }

        if (!confirm)
            return;

        var game = await EnsureWebsiteBuildingGameAsync();
        if (game == null)
            return;

        var gameId = Uri.EscapeDataString(game.GameId);
        var name = Uri.EscapeDataString("Daily Website Task till 1000");
        await Shell.Current.GoToAsync($"addactivity?gameId={gameId}&prefillName={name}&prefillStreakTracked=true&prefillStreakTargetDays=1000");
    }

    private async Task<Game?> EnsureWebsiteBuildingGameAsync()
    {
        var games = await _gameService.GetGamesAsync(_auth.CurrentUsername);
        var existing = games.FirstOrDefault(g => g.DisplayName.Equals("Website Building", StringComparison.OrdinalIgnoreCase));
        if (existing != null)
            return existing;

        return await _gameService.CreateGameAsync(_auth.CurrentUsername, "Website Building");
    }

    private string GetGamificationPromptShownKey() => $"website_builder_gamify_prompt_shown_{_auth.CurrentUsername}";

    private View CreatePickerGrid(Button newIdeaButton, Button newProjectButton)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 10,
            RowSpacing = 10
        };

        grid.Add(_ideaPicker, 0, 0);
        grid.Add(_projectPicker, 1, 0);
        grid.Add(newIdeaButton, 0, 1);
        grid.Add(newProjectButton, 1, 1);
        return grid;
    }

    private static Picker CreatePicker(string title)
    {
        return new Picker
        {
            Title = title,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
    }

    private static Label CreateSectionHeader(string text)
    {
        return new Label
        {
            Text = text,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            Margin = new Thickness(0, 12, 0, 0)
        };
    }

    private static Button CreatePrimaryButton(string text, Color backgroundColor)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = backgroundColor,
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 46,
            FontAttributes = FontAttributes.Bold
        };
    }

    private static Button CreateSecondaryButton(string text)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 8,
            HeightRequest = 42
        };
    }

    private static Button CreateWorkflowSmallButton(string text)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 32,
            FontSize = 11
        };
    }

    private static Button CreateDangerButton(string text)
    {
        return new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            HeightRequest = 44
        };
    }

    private async Task CopyIdeasPromptAsync()
    {
        await Clipboard.SetTextAsync(IdeasPrompt);
        await DisplayAlert("Copied", "Prompt copied. Paste into Gemini or ChatGPT, then copy a selected idea back into the editor below.", "OK");
    }

    private async Task CopyDomainNamesPromptAsync()
    {
        if (_currentIdeaId <= 0)
        {
            await DisplayAlert("Save idea first", "Save or load an idea before copying the domain prompt.", "OK");
            return;
        }

        var title = _ideaTitleEntry.Text?.Trim() ?? "";
        var ideaText = _ideaEditor.Text?.Trim() ?? "";
        var prompt = string.Format(DomainNamesPromptTemplate, title, ideaText);
        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert("Copied", "Domain names prompt copied with your idea details baked in.", "OK");
    }

    private string GetLastProjectStorageKey()
    {
        return $"website_builder_last_project_id_{_auth.CurrentUsername}";
    }

    private string GetBatchSizeStorageKey()
    {
        return $"website_builder_batch_size_{_auth.CurrentUsername}";
    }

    private string GetInvestigationPromptStorageKey()
    {
        return $"website_builder_investigation_prompt_{_auth.CurrentUsername}";
    }

    private int GetSelectedBatchSize()
    {
        var selected = _batchSizePicker.SelectedItem?.ToString();
        return int.TryParse(selected, out var size) && (size == 3 || size == 5 || size == 7 || size == 10)
            ? size
            : 5;
    }

    private async Task LoadBatchSizeSelectionAsync()
    {
        _isLoadingBatchSize = true;
        try
        {
            var saved = await SecureStorage.GetAsync(GetBatchSizeStorageKey());
            var items = new List<string> { "3", "5", "7", "10" };
            var index = items.IndexOf(saved ?? "5");
            _batchSizePicker.SelectedIndex = index >= 0 ? index : 1;
        }
        catch
        {
            _batchSizePicker.SelectedIndex = 1;
        }
        finally
        {
            _isLoadingBatchSize = false;
            UpdateCopyBatchPromptButtonText();
        }
    }

    private async Task SaveBatchSizeSelectionAsync()
    {
        if (_isLoadingBatchSize)
            return;

        try
        {
            await SecureStorage.SetAsync(GetBatchSizeStorageKey(), GetSelectedBatchSize().ToString());
        }
        catch
        {
        }
    }

    private void UpdateCopyBatchPromptButtonText()
    {
        _copyBatchPromptButton.Text = $"Copy Batch Prompt ({GetSelectedBatchSize()} tasks)";
    }

    private async Task TryLoadLastSelectedProjectAsync()
    {
        string? storedValue;
        try
        {
            storedValue = await SecureStorage.GetAsync(GetLastProjectStorageKey());
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(storedValue))
            return;

        if (!int.TryParse(storedValue, out var projectId))
        {
            await ClearLastSelectedProjectIdAsync();
            return;
        }

        var project = await _projectService.GetByIdAsync(projectId);
        if (project == null || !string.Equals(project.Username, _auth.CurrentUsername, StringComparison.OrdinalIgnoreCase))
        {
            await ClearLastSelectedProjectIdAsync();
            return;
        }

        LoadProject(project);
        var index = _projectsCache.FindIndex(p => p.Id == project.Id);
        if (index >= 0)
        {
            _isRefreshingPickers = true;
            _projectPicker.SelectedIndex = index;
            _isRefreshingPickers = false;
        }
    }

    private async Task SaveLastSelectedProjectIdAsync(int projectId)
    {
        try
        {
            await SecureStorage.SetAsync(GetLastProjectStorageKey(), projectId.ToString());
        }
        catch
        {
        }
    }

    private Task ClearLastSelectedProjectIdAsync()
    {
        try
        {
            SecureStorage.Remove(GetLastProjectStorageKey());
        }
        catch
        {
        }

        return Task.CompletedTask;
    }

    private async Task RefreshPickersAsync(int? selectIdeaId = null, int? selectProjectId = null)
    {
        _isRefreshingPickers = true;
        _ideasCache = await _ideaService.GetAllForUserAsync(_auth.CurrentUsername);
        _projectsCache = await _projectService.GetAllForUserAsync(_auth.CurrentUsername);

        _ideaPicker.ItemsSource = _ideasCache.Select(idea => idea.Title).ToList();
        var projectTitles = _projectsCache.Select(project => project.Title).ToList();
        projectTitles.Add("+ New Project");
        _projectPicker.ItemsSource = projectTitles;

        SetPickerSelection(_ideaPicker, _ideasCache.Select(i => i.Id).ToList(), selectIdeaId ?? (_currentIdeaId > 0 ? _currentIdeaId : null));
        SetPickerSelection(_projectPicker, _projectsCache.Select(p => p.Id).ToList(), selectProjectId ?? (_currentProjectId > 0 ? _currentProjectId : null));
        _isRefreshingPickers = false;
    }

    private static void SetPickerSelection(Picker picker, List<int> ids, int? selectedId)
    {
        if (!selectedId.HasValue)
        {
            picker.SelectedIndex = -1;
            return;
        }

        picker.SelectedIndex = ids.IndexOf(selectedId.Value);
    }

    private async Task OnIdeaSelectedAsync()
    {
        if (_isRefreshingPickers || _ideaPicker.SelectedIndex < 0 || _ideaPicker.SelectedIndex >= _ideasCache.Count)
            return;

        var idea = _ideasCache[_ideaPicker.SelectedIndex];
        LoadIdea(idea);
        await Task.CompletedTask;
    }

    private async Task OnProjectSelectedAsync()
    {
        if (_isRefreshingPickers || _projectPicker.SelectedIndex < 0)
            return;

        if (_projectPicker.SelectedIndex == _projectsCache.Count)
        {
            await ClearProjectStateAsync();
            return;
        }

        if (_projectPicker.SelectedIndex > _projectsCache.Count)
            return;

        var project = _projectsCache[_projectPicker.SelectedIndex];
        LoadProject(project);
        await Task.CompletedTask;
    }

    private void LoadIdea(WebsiteIdea idea)
    {
        _currentIdeaId = idea.Id;
        _currentProjectId = 0;
        _ideaTitleEntry.Text = idea.Title;
        _ideaEditor.Text = idea.IdeaText;
        _purchasedDomainEntry.Text = "";
        _projectPicker.SelectedIndex = -1;
        _ = ClearLastSelectedProjectIdAsync();
        RefreshStateVisibility();
    }

    private void LoadProject(WebsiteProject project)
    {
        _currentProjectId = project.Id;
        _currentIdeaId = 0;
        UpdateTaskCounterDisplay(project);
        _ideaTitleEntry.Text = "";
        _ideaEditor.Text = "";
        _purchasedDomainEntry.Text = "";
        _ideaPicker.SelectedIndex = -1;
        _ = SaveLastSelectedProjectIdAsync(project.Id);
        RefreshStateVisibility();
    }

    private async Task SaveIdeaAsync()
    {
        var title = _ideaTitleEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(title))
        {
            await DisplayAlert("Title required", "Enter a short idea title before saving.", "OK");
            return;
        }

        WebsiteIdea idea;
        if (_currentIdeaId > 0)
        {
            idea = await _ideaService.GetByIdAsync(_currentIdeaId) ?? new WebsiteIdea
            {
                Id = _currentIdeaId,
                Username = _auth.CurrentUsername
            };
        }
        else
        {
            idea = new WebsiteIdea { Username = _auth.CurrentUsername };
        }

        try
        {
            idea.Username = _auth.CurrentUsername;
            idea.Title = title;
            idea.IdeaText = _ideaEditor.Text?.Trim() ?? "";
            _currentIdeaId = await _ideaService.SaveAsync(idea);
            _currentProjectId = 0;
            await RefreshPickersAsync(selectIdeaId: _currentIdeaId);
            RefreshStateVisibility();
            await DisplayAlert("Saved", "Idea saved.", "OK");
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task DeleteIdeaAsync()
    {
        if (_currentIdeaId <= 0)
            return;

        var confirm = await DisplayAlert("Delete this idea?", "This website idea will be permanently deleted.", "Delete", "Cancel");
        if (!confirm)
            return;

        try
        {
            await _ideaService.DeleteAsync(_currentIdeaId);
            await ClearAllAsync();
            await DisplayAlert("Deleted", "Idea deleted.", "OK");
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task DeleteProjectAsync()
    {
        if (_currentProjectId <= 0)
            return;

        var confirm = await DisplayAlert("Delete this project?", "This website project will be permanently deleted.", "Delete", "Cancel");
        if (!confirm)
            return;

        try
        {
            await _projectService.DeleteAsync(_currentProjectId);
            await ClearLastSelectedProjectIdAsync();
            await ClearAllAsync();
            await DisplayAlert("Deleted", "Project deleted.", "OK");
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task SaveProjectFromPurchasedDomainAsync()
    {
        var domain = (_purchasedDomainEntry.Text ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(domain))
        {
            await DisplayAlert("Domain required", "Enter a purchased domain first.", "OK");
            return;
        }

        if (domain.Any(char.IsWhiteSpace))
        {
            await DisplayAlert("Invalid domain", "Domain cannot contain spaces.", "OK");
            return;
        }

        if (!domain.Contains('.'))
        {
            await DisplayAlert("Invalid domain", "Enter a domain like hookbrain.com.", "OK");
            return;
        }

        try
        {
            await PromoteIdeaToProjectAsync(domain);
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }


    private async Task PromoteIdeaToProjectAsync(string domain)
    {
        if (_currentIdeaId <= 0)
            return;

        var confirm = await DisplayAlert(
            "Save domain as project?",
            $"Save '{domain}' as the project name? This will create a project using this domain and delete the source idea.",
            "Save Project",
            "Cancel");

        if (!confirm)
            return;

        var ideaTitle = _ideaTitleEntry.Text?.Trim() ?? "";
        var ideaText = _ideaEditor.Text?.Trim() ?? "";
        var project = new WebsiteProject
        {
            Username = _auth.CurrentUsername,
            Title = domain,
            IdeaText = $"{ideaTitle}\n\n{ideaText}".Trim()
        };

        await _projectService.SaveAsync(project);
        await _ideaService.DeleteAsync(_currentIdeaId);
        await ClearAllAsync();
        await RefreshPickersAsync(selectProjectId: project.Id);
        var savedProject = await _projectService.GetByIdAsync(project.Id);
        if (savedProject != null)
            LoadProject(savedProject);
        await DisplayAlert("Project created", $"Project '{domain}' created.", "OK");
    }

    private async Task ClearAllAsync()
    {
        await ClearLastSelectedProjectIdAsync();
        _currentIdeaId = 0;
        _currentProjectId = 0;
        _ideaTitleEntry.Text = "";
        _ideaEditor.Text = "";
        _projectTitleHeaderLabel.Text = "";
        _projectIdeaReferenceLabel.Text = "";
        _taskCountLabel.Text = "";
        _purchasedDomainEntry.Text = "";
        await RefreshPickersAsync();
        RefreshStateVisibility();
    }

    private async Task ClearProjectStateAsync()
    {
        await ClearLastSelectedProjectIdAsync();
        _currentProjectId = 0;
        _projectTitleHeaderLabel.Text = "";
        _projectIdeaReferenceLabel.Text = "";
        _taskCountLabel.Text = "";
        _projectPicker.SelectedIndex = -1;
        RefreshStateVisibility();
        await Task.CompletedTask;
    }

    private void UpdateTaskCounterDisplay(WebsiteProject project)
    {
        _projectTitleHeaderLabel.Text = project.Title;
        _projectIdeaReferenceLabel.Text = project.IdeaText;
        _taskCountLabel.Text = $"{project.TaskCount} / {project.TaskTarget}";
        _decrementButton.IsEnabled = project.TaskCount > 0;
        _celebrationFrame.IsVisible = project.TaskCount >= project.TaskTarget;
        UpdateWorkflowDisplay(project);
        UpdateQADisplay(project);
        UpdateVisionDisplay(project);
        UpdateProjectSummaryDisplay(project);
        UpdateCodebasePathDisplay(project);
    }

    private void UpdateQADisplay(WebsiteProject project)
    {
        var hasReport = !string.IsNullOrWhiteSpace(project.LatestQAReport);
        _qaStatusLabel.Text = hasReport
            ? $"QA report attached — {project.LatestQAReport.Length} characters, captured {FormatQACapturedAt(project.LatestQAReportCapturedAt)}."
            : "No QA report attached. Skip this step or run QA before planning the next task.";
        _clearQAReportButton.IsVisible = hasReport;
    }

    private static string FormatQACapturedAt(DateTime? capturedAt)
    {
        return capturedAt.HasValue
            ? capturedAt.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm")
            : "unknown time";
    }

    private static string FormatQAReportUtc(DateTime? capturedAt)
    {
        return capturedAt.HasValue
            ? $"{capturedAt.Value.ToUniversalTime():yyyy-MM-dd HH:mm} UTC"
            : "unknown time UTC";
    }

    private void UpdateWorkflowDisplay(WebsiteProject project)
    {
        var state = ToWorkflowState(project.WorkflowState);
        var isWindows = IsWindows();

        _workflowStartRow.IsVisible = false;
        _pickFromQaBtn.IsVisible = false;
        _investigateBtn.IsVisible = false;
        _workflowCopyNextTaskPromptButton.IsVisible = false;
        _batchSizeRow.IsVisible = false;
        _copyBatchPromptButton.IsVisible = false;
        _pasteTaskPlanButton.IsVisible = false;
        _cancelWorkflowButton.IsVisible = false;
        _copyCodexPromptButton.IsVisible = false;
        _pasteCodexResultButton.IsVisible = false;
        _editTaskTitleButton.IsVisible = false;
        _editCodexPromptButton.IsVisible = false;
        _editCommitMessageButton.IsVisible = false;
        _commitAndPushButton.IsVisible = false;
        _cancelWorkflowButton.Text = "Cancel (Back to Start)";

        switch (state)
        {
            case WebsiteWorkflowState.WaitingForLLM:
                ApplyWorkflowBanner("#FFEBEE", "#C62828", "#C62828", "🔴", "Waiting for LLM response",
                    "Paste the prompt into Claude/ChatGPT, then tap Paste Task Plan with the response.");
                _pasteTaskPlanButton.IsVisible = true;
                _cancelWorkflowButton.IsVisible = true;
                break;

            case WebsiteWorkflowState.ReadyToExecute:
                ApplyWorkflowBanner("#FFF8E1", "#F57C00", "#E65100", "🟠", "Ready to execute in Codex",
                    $"Task: {project.PendingTaskTitle}. Copy the Codex prompt, run it in Codex, then paste the result.");
                _copyCodexPromptButton.IsVisible = true;
                _pasteCodexResultButton.IsVisible = true;
                _editTaskTitleButton.IsVisible = true;
                _editCodexPromptButton.IsVisible = true;
                _cancelWorkflowButton.IsVisible = true;
                break;

            case WebsiteWorkflowState.ReadyToCommit:
                ApplyWorkflowBanner("#E8F5E9", "#2E7D32", "#1B5E20", "🟢", "Ready to commit",
                    $"Commit: {project.PendingCommitMessage}. Tap Commit and Push to finish.");
                _commitAndPushButton.IsVisible = isWindows;
                _editCommitMessageButton.IsVisible = true;
                _editTaskTitleButton.IsVisible = true;
                _cancelWorkflowButton.IsVisible = true;
                break;

            default:
                ApplyWorkflowBanner("#F5F5F5", "#BBBBBB", "#555555", "⚪", "Ready for next task",
                    "Tap Copy Next Task Prompt for one task, or use Batch Prompt to queue a short arc.");
                _workflowStartRow.IsVisible = true;
                _pickFromQaBtn.IsVisible = true;
                _investigateBtn.IsVisible = true;
                _workflowCopyNextTaskPromptButton.IsVisible = true;
                _batchSizeRow.IsVisible = true;
                _copyBatchPromptButton.IsVisible = true;
                break;
        }
    }

    private void ApplyWorkflowBanner(string background, string border, string titleColor, string icon, string title, string subtitle)
    {
        _workflowStatusBanner.BackgroundColor = Color.FromArgb(background);
        _workflowStatusBanner.BorderColor = Color.FromArgb(border);
        _workflowStatusIcon.Text = icon;
        _workflowStatusTitle.Text = title;
        _workflowStatusTitle.TextColor = Color.FromArgb(titleColor);
        _workflowStatusSubtitle.Text = subtitle;
    }

    private static WebsiteWorkflowState ToWorkflowState(int state)
    {
        return Enum.IsDefined(typeof(WebsiteWorkflowState), state)
            ? (WebsiteWorkflowState)state
            : WebsiteWorkflowState.Idle;
    }

    private void UpdateVisionDisplay(WebsiteProject project)
    {
        var hasRaw = !string.IsNullOrWhiteSpace(project.VisionRaw);
        var hasRefined = !string.IsNullOrWhiteSpace(project.VisionRefined);

        _visionStatusLabel.Text = hasRefined
            ? "Refined vision set"
            : hasRaw
                ? "Raw vision set - not yet refined"
                : "No vision set yet";

        var preview = hasRefined ? project.VisionRefined : hasRaw ? project.VisionRaw : "";
        _visionPreviewLabel.Text = preview;
        _visionPreviewLabel.IsVisible = !string.IsNullOrWhiteSpace(preview);
    }

    private void UpdateProjectSummaryDisplay(WebsiteProject project)
    {
        var hasSummary = !string.IsNullOrWhiteSpace(project.ProjectSummary);
        if (!hasSummary)
        {
            _summaryStalenessLabel.Text = "Summary not yet generated";
            _summaryPreviewLabel.IsVisible = false;
            _summaryPreviewLabel.Text = "";
            return;
        }

        _summaryStalenessLabel.Text = project.TasksSinceSummaryUpdate > 0
            ? $"{project.TasksSinceSummaryUpdate} tasks since last summary update"
            : "Summary up to date";
        _summaryPreviewLabel.Text = project.ProjectSummary;
        _summaryPreviewLabel.IsVisible = true;
    }

    private void UpdateCodebasePathDisplay(WebsiteProject project)
    {
        var hasPath = !string.IsNullOrWhiteSpace(project.CodebasePath);
        _codebasePathLabel.Text = hasPath
            ? $"Code folder: {project.CodebasePath}"
            : "No code folder set yet (use Setup Guide Step 9).";

        var isWindows = IsWindows();
        _copyCodexCommandButton.IsVisible = isWindows;
        _openTerminalButton.IsVisible = isWindows;
    }

    private static bool IsWindows()
    {
        return DeviceInfo.Current.Platform == DevicePlatform.WinUI;
    }

    private async Task RefreshCurrentProjectAsync()
    {
        if (_currentProjectId <= 0)
            return;

        var project = await _projectService.GetByIdAsync(_currentProjectId);
        if (project == null)
        {
            await ClearAllAsync();
            return;
        }

        UpdateTaskCounterDisplay(project);
        await RefreshPickersAsync(selectProjectId: project.Id);
        RefreshStateVisibility();
    }

    private async Task OnIncrementClickedAsync()
    {
        if (_currentProjectId <= 0)
            return;

        var input = await DisplayPromptAsync(
            "What did you complete?",
            "Brief title for this task (or leave empty to increment without logging):",
            "Done",
            "Skip Title",
            maxLength: 200);

        try
        {
            if (await _projectService.LogAndIncrementTaskAsync(_currentProjectId, input?.Trim() ?? ""))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task OnDecrementClickedAsync()
    {
        if (_currentProjectId <= 0)
            return;

        try
        {
            if (await _projectService.DecrementTaskCountAsync(_currentProjectId))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task OnEditCountClickedAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var input = await DisplayPromptAsync(
            "Edit Count",
            "Enter the current completed task count:",
            "Save",
            "Cancel",
            initialValue: project.TaskCount.ToString(),
            keyboard: Keyboard.Numeric);

        if (input == null)
            return;

        if (!int.TryParse(input.Trim(), out var newCount) || newCount < 0)
        {
            await DisplayAlert("Invalid count", "Task count must be 0 or a positive number.", "OK");
            return;
        }

        try
        {
            if (await _projectService.SetTaskCountAsync(project.Id, newCount))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task OnSetTargetClickedAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        await PromptAndSetTargetAsync(project, project.TaskTarget.ToString());
    }

    private async Task OnSetNewTargetClickedAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        await PromptAndSetTargetAsync(project, Math.Max(1, project.TaskTarget * 2).ToString());
    }

    private async Task PromptAndSetTargetAsync(WebsiteProject project, string initialValue)
    {
        var input = await DisplayPromptAsync(
            "Set Target",
            "Enter the task target:",
            "Save",
            "Cancel",
            initialValue: initialValue,
            keyboard: Keyboard.Numeric);

        if (input == null)
            return;

        if (!int.TryParse(input.Trim(), out var newTarget) || newTarget <= 0)
        {
            await DisplayAlert("Invalid target", "Task target must be a positive number.", "OK");
            return;
        }

        try
        {
            if (await _projectService.SetTaskTargetAsync(project.Id, newTarget))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task<WebsiteProject?> GetCurrentProjectOrAlertAsync()
    {
        if (_currentProjectId <= 0)
            return null;

        var project = await _projectService.GetByIdAsync(_currentProjectId);
        if (project != null)
            return project;

        await DisplayAlert("Project missing", "This project could not be found.", "OK");
        await ClearAllAsync();
        return null;
    }

    private async Task CopyUpdateSummaryPromptAsync()
    {
        await Clipboard.SetTextAsync(UpdateSummaryPrompt);
        await DisplayAlert(
            "Prompt copied",
            "Prompt copied. Paste into Codex CLI in your project folder. Codex will read the project files and output a summary. Then tap Paste Summary Result here and paste the response.",
            "OK");
    }

    private async Task PasteSummaryResultAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Edit Project Summary",
            "Paste the summary Codex returned below. Multi-line content is preserved.",
            project.ProjectSummary,
            "Paste the full summary text here...");

        if (result == null)
            return;

        try
        {
            if (await _projectService.SetProjectSummaryAsync(project.Id, result))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task EditVisionRawAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Edit Vision (Your Words)",
            "Describe what you want this website to be, in your own words. Don't worry about polish - this is your raw vision.",
            project.VisionRaw,
            "Type your raw vision here...");

        if (result == null)
            return;

        try
        {
            if (await _projectService.SetVisionRawAsync(project.Id, result))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task CopyVisionRefinementPromptAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        if (string.IsNullOrWhiteSpace(project.VisionRaw))
        {
            await DisplayAlert(
                "Raw vision missing",
                "Write your raw vision first via Edit Vision (Your Words).",
                "OK");
            return;
        }

        var prompt = string.Format(VisionRefinementPromptTemplate, project.Title, project.VisionRaw);
        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert(
            "Prompt copied",
            "Prompt copied. Paste into Claude/ChatGPT to get a refined vision. Then tap Paste Refined Vision.",
            "OK");
    }

    private async Task CopyQAExplorationPromptAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var prompt = BuildQAExplorationPrompt(project);
        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert(
            "QA exploration prompt copied",
            "Paste this into ChatGPT Agent Mode (or any browsing agent). When it returns the report, tap Paste QA Report.",
            "OK");
    }

    private async Task PasteQAReportAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Paste QA Report",
            "Paste the agent's full report. It will be folded into the next planning prompt and cleared after the next commit.",
            project.LatestQAReport,
            "QA report text…");

        if (result == null)
            return;

        try
        {
            if (await _projectService.SetLatestQAReportAsync(project.Id, result))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task ClearQAReportAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var confirm = await DisplayAlert(
            "Clear QA Report?",
            "The current QA report will be discarded.",
            "Clear",
            "Cancel");

        if (!confirm)
            return;

        try
        {
            if (await _projectService.ClearLatestQAReportAsync(project.Id))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task PasteVisionRefinedAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Paste Refined Vision",
            "Paste the refined vision the LLM returned.",
            project.VisionRefined,
            "Paste refined vision text here...");

        if (result == null)
            return;

        try
        {
            if (await _projectService.SetVisionRefinedAsync(project.Id, result))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async void OnInvestigateStuckClicked(object? sender, EventArgs e)
    {
        await ShowInvestigateStuckModalAsync();
    }

    private async Task ShowInvestigateStuckModalAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        if (Content is not Grid parent)
        {
            await DisplayAlert("Layout error", "Investigation modal requires the page root grid.", "OK");
            return;
        }

        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };

        var symptomEditor = new Editor
        {
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = 200,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888"),
            Placeholder = "Example: 'Price history has been broken and re-attempted daily for 6 consecutive days. Chart appears empty, sometimes partial. Related: pages/collections/[slug]/history.tsx.'"
        };

        var pathsEntry = new Entry
        {
            Placeholder = "e.g. pages/collections/[slug]/history.tsx, lib/atomicmarket.ts",
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888")
        };

        var copyBtn = new Button
        {
            Text = " Copy investigation prompt",
            BackgroundColor = Color.FromArgb("#E65100"),
            TextColor = Colors.White,
            CornerRadius = 8
        };

        copyBtn.Clicked += async (_, _) =>
        {
            var symptom = (symptomEditor.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(symptom))
            {
                await DisplayAlert("Empty description", "Describe the stuck symptom before copying.", "OK");
                return;
            }

            var pathsRaw = (pathsEntry.Text ?? "").Trim();
            var pathsFormatted = string.IsNullOrWhiteSpace(pathsRaw) ? "(none provided)" : pathsRaw;

            string template;
            try
            {
                template = await SecureStorage.GetAsync(GetInvestigationPromptStorageKey()) ?? DefaultInvestigationPrompt;
            }
            catch
            {
                template = DefaultInvestigationPrompt;
            }

            var assembled = template
                .Replace("{SYMPTOM_DESCRIPTION}", symptom, StringComparison.Ordinal)
                .Replace("{RELATED_PATHS}", pathsFormatted, StringComparison.Ordinal);

            try
            {
                await Clipboard.SetTextAsync(assembled);
                parent.Children.Remove(overlay);
                tcs.TrySetResult(true);
                await DisplayAlert(
                    "Investigation prompt copied",
                    "Paste into Codex. It will investigate and return a written diagnosis without code changes. Use the findings to inform a follow-up fix task.",
                    "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Copy failed", ex.Message, "OK");
            }
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 8
        };

        cancelBtn.Clicked += (_, _) =>
        {
            parent.Children.Remove(overlay);
            tcs.TrySetResult(false);
        };

        var editTemplateBtn = new Button
        {
            Text = "Edit template",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 32
        };
        editTemplateBtn.Clicked += async (_, _) => await ShowInvestigationTemplateEditorAsync();

        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Children = { editTemplateBtn, cancelBtn, copyBtn }
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 640,
            MinimumHeightRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = " Investigate Stuck Symptom", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") },
                    new Label
                    {
                        Text = "Break out of the fix-loop. Ask Codex to diagnose a recurring issue without making changes.",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666"),
                        FontAttributes = FontAttributes.Italic
                    },
                    new Label { Text = "Describe the stuck symptom and its history:", FontSize = 13, TextColor = Color.FromArgb("#333") },
                    symptomEditor,
                    new Label { Text = "Related files or code paths (optional):", FontSize = 13, TextColor = Color.FromArgb("#333") },
                    pathsEntry,
                    buttonRow
                }
            }
        };

        overlay.Children.Add(card);
        parent.Children.Add(overlay);

        await tcs.Task;
    }

    private async Task ShowInvestigationTemplateEditorAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        if (Content is not Grid parent)
        {
            await DisplayAlert("Layout error", "Template editor requires the page root grid.", "OK");
            return;
        }

        var overlay = new Grid { BackgroundColor = Color.FromArgb("#90000000") };
        var templateKey = GetInvestigationPromptStorageKey();
        string currentTemplate;
        try
        {
            currentTemplate = await SecureStorage.GetAsync(templateKey) ?? DefaultInvestigationPrompt;
        }
        catch
        {
            currentTemplate = DefaultInvestigationPrompt;
        }

        var editor = new Editor
        {
            Text = currentTemplate,
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = 420,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888"),
            Placeholder = "Investigation prompt template..."
        };

        var saveBtn = new Button
        {
            Text = "Save",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        saveBtn.Clicked += async (_, _) =>
        {
            var value = editor.Text ?? "";
            if (!value.Contains("{SYMPTOM_DESCRIPTION}", StringComparison.Ordinal) ||
                !value.Contains("{RELATED_PATHS}", StringComparison.Ordinal))
            {
                await DisplayAlert(
                    "Placeholders required",
                    "The template must include {SYMPTOM_DESCRIPTION} and {RELATED_PATHS}.",
                    "OK");
                return;
            }

            try
            {
                await SecureStorage.SetAsync(templateKey, value);
                parent.Children.Remove(overlay);
                tcs.TrySetResult(true);
                await DisplayAlert("Saved", "Investigation template saved.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Save failed", ex.Message, "OK");
            }
        };

        var resetBtn = new Button
        {
            Text = "Reset to Default",
            BackgroundColor = Color.FromArgb("#FFE0B2"),
            TextColor = Color.FromArgb("#E65100"),
            CornerRadius = 8
        };
        resetBtn.Clicked += async (_, _) =>
        {
            var confirm = await DisplayAlert(
                "Reset template?",
                "Replace your custom investigation template with the default?",
                "Reset",
                "Cancel");
            if (!confirm)
                return;

            try
            {
                SecureStorage.Remove(templateKey);
            }
            catch
            {
            }

            editor.Text = DefaultInvestigationPrompt;
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        cancelBtn.Clicked += (_, _) =>
        {
            parent.Children.Remove(overlay);
            tcs.TrySetResult(false);
        };

        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Children = { resetBtn, cancelBtn, saveBtn }
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 720,
            MinimumHeightRequest = 560,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = "Edit Investigation Template", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") },
                    new Label
                    {
                        Text = "Keep both placeholders: {SYMPTOM_DESCRIPTION} and {RELATED_PATHS}.",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666"),
                        FontAttributes = FontAttributes.Italic
                    },
                    editor,
                    buttonRow
                }
            }
        };

        overlay.Children.Add(card);
        parent.Children.Add(overlay);

        await tcs.Task;
    }

    private async void OnPickFromQaClicked(object? sender, EventArgs e)
    {
        await ShowPickFromQaModalAsync();
    }

    private async Task ShowPickFromQaModalAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        if (Content is not Grid parent)
        {
            await DisplayAlert("Layout error", "QA picker modal requires the page root grid.", "OK");
            return;
        }

        var overlay = new Grid { BackgroundColor = Color.FromArgb("#80000000") };

        var qaEditor = new Editor
        {
            AutoSize = EditorAutoSizeOption.Disabled,
            HeightRequest = 300,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            TextColor = Color.FromArgb("#222"),
            PlaceholderColor = Color.FromArgb("#888"),
            Placeholder = "Paste the QA report here. Include BROKEN, ROUGH, MISSING section headers."
        };

        var parseBtn = new Button
        {
            Text = "Parse and pick",
            BackgroundColor = Color.FromArgb("#F57F17"),
            TextColor = Colors.White,
            CornerRadius = 8
        };

        Button BuildCancelButton()
        {
            var button = new Button
            {
                Text = "Cancel",
                BackgroundColor = Color.FromArgb("#9E9E9E"),
                TextColor = Colors.White,
                CornerRadius = 8
            };
            button.Clicked += (_, _) =>
            {
                parent.Children.Remove(overlay);
                tcs.TrySetResult(false);
            };
            return button;
        }

        var pasteButtonRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.End,
            Children = { BuildCancelButton(), parseBtn }
        };

        var pasteContent = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = " Pick 5 from QA report", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") },
                new Label
                {
                    Text = "Paste the QA report below. Bannister will parse it, extract every BROKEN, ROUGH, and MISSING item, and pick 5 at random with equal probability across categories.",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#666"),
                    FontAttributes = FontAttributes.Italic
                },
                new Label { Text = "QA report:", FontSize = 13, TextColor = Color.FromArgb("#333") },
                qaEditor,
                pasteButtonRow
            }
        };

        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 20,
            WidthRequest = 700,
            MinimumHeightRequest = 500,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = pasteContent
        };
        overlay.Children.Add(card);
        parent.Children.Add(overlay);

        void RenderPreview()
        {
            var brokenCount = _parsedItems.Count(i => i.Category == "BROKEN");
            var roughCount = _parsedItems.Count(i => i.Category == "ROUGH");
            var missingCount = _parsedItems.Count(i => i.Category == "MISSING");

            var picksStack = new VerticalStackLayout { Spacing = 8 };
            foreach (var item in _pickedItems)
                picksStack.Children.Add(BuildPickedItemView(item));

            var picksScroll = new ScrollView
            {
                HeightRequest = 300,
                Content = picksStack
            };

            var repickBtn = new Button
            {
                Text = "Re-pick",
                BackgroundColor = Color.FromArgb("#FFF9C4"),
                TextColor = Color.FromArgb("#F57F17"),
                CornerRadius = 8
            };
            repickBtn.Clicked += (_, _) =>
            {
                _pickedItems = PickFive(_parsedItems);
                RenderPreview();
            };

            var copyBtn = new Button
            {
                Text = "Copy arc prompt",
                BackgroundColor = Color.FromArgb("#2E7D32"),
                TextColor = Colors.White,
                CornerRadius = 8
            };
            copyBtn.Clicked += async (_, _) =>
            {
                var prompt = AssembleArcPromptWithPicks(_pickedItems);
                try
                {
                    await Clipboard.SetTextAsync(prompt);
                    parent.Children.Remove(overlay);
                    tcs.TrySetResult(true);
                    await DisplayAlert(
                        "Arc prompt copied",
                        "Paste into your LLM to generate the arc.",
                        "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Copy failed", ex.Message, "OK");
                }
            };

            var backBtn = new Button
            {
                Text = "Back to paste",
                BackgroundColor = Color.FromArgb("#9E9E9E"),
                TextColor = Colors.White,
                CornerRadius = 8
            };
            backBtn.Clicked += (_, _) =>
            {
                card.Content = pasteContent;
            };

            var previewButtonRow = new HorizontalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.End,
                Children = { repickBtn, copyBtn, backBtn, BuildCancelButton() }
            };

            var previewContent = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = " Picked 5 items", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") },
                    new Label
                    {
                        Text = $"Found {_parsedItems.Count} items total: {brokenCount} BROKEN, {roughCount} ROUGH, {missingCount} MISSING",
                        FontSize = 12,
                        TextColor = Color.FromArgb("#666"),
                        FontAttributes = FontAttributes.Italic
                    },
                    picksScroll,
                    previewButtonRow
                }
            };
            card.Content = previewContent;
        }

        parseBtn.Clicked += async (_, _) =>
        {
            var report = qaEditor.Text ?? "";
            if (string.IsNullOrWhiteSpace(report))
            {
                await DisplayAlert("Empty report", "Paste a QA report first.", "OK");
                return;
            }

            _parsedItems = ParseQAReport(report);
            if (_parsedItems.Count == 0)
            {
                await DisplayAlert(
                    "No items parsed",
                    "Could not find any items under BROKEN, ROUGH, or MISSING headers. Check that the report has those section headers and top-level list items starting with *, -, or •.",
                    "OK");
                return;
            }

            _pickedItems = PickFive(_parsedItems);
            RenderPreview();
        };

        await tcs.Task;
    }

    private static List<(string Category, string Body)> ParseQAReport(string report)
    {
        var items = new List<(string Category, string Body)>();
        if (string.IsNullOrWhiteSpace(report))
            return items;

        var lines = report.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        var sectionRegex = new Regex(@"^(BROKEN|ROUGH|MISSING)\s*$", RegexOptions.IgnoreCase);
        var workingRegex = new Regex(@"^WORKING\s*$", RegexOptions.IgnoreCase);
        var citationOnlyRegex = new Regex(@"^\(\[[^\]]+\]\[\d+\]\)\s*$");

        string? currentCategory = null;
        var currentItemLines = new List<string>();

        static bool IsTopLevelItemStart(string line) =>
            line.StartsWith("* ", StringComparison.Ordinal) ||
            line.StartsWith("- ", StringComparison.Ordinal) ||
            line.StartsWith("• ", StringComparison.Ordinal);

        string StripCitationOnlyLines(IEnumerable<string> sourceLines)
        {
            var cleaned = sourceLines
                .Where(l => !citationOnlyRegex.IsMatch(l.Trim()))
                .Select(l => l.TrimEnd())
                .ToList();

            while (cleaned.Count > 0 && string.IsNullOrWhiteSpace(cleaned[0]))
                cleaned.RemoveAt(0);
            while (cleaned.Count > 0 && string.IsNullOrWhiteSpace(cleaned[^1]))
                cleaned.RemoveAt(cleaned.Count - 1);

            return string.Join("\n", cleaned).Trim();
        }

        void FlushCurrent()
        {
            if (currentCategory == null || currentItemLines.Count == 0)
                return;

            var body = StripCitationOnlyLines(currentItemLines);
            if (!string.IsNullOrWhiteSpace(body))
                items.Add((currentCategory, body));

            currentItemLines.Clear();
        }

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var sectionMatch = sectionRegex.Match(trimmed);
            if (sectionMatch.Success)
            {
                FlushCurrent();
                currentCategory = sectionMatch.Groups[1].Value.ToUpperInvariant();
                continue;
            }

            if (workingRegex.IsMatch(trimmed))
            {
                FlushCurrent();
                currentCategory = null;
                continue;
            }

            if (currentCategory == null)
                continue;

            if (IsTopLevelItemStart(line))
            {
                FlushCurrent();
                currentItemLines.Add(line[2..].TrimEnd());
                continue;
            }

            if (currentItemLines.Count > 0)
            {
                currentItemLines.Add(line);
                continue;
            }

        }

        FlushCurrent();
        return items;
    }

    private static List<(string Category, string Body)> PickFive(List<(string Category, string Body)> items)
    {
        var rng = new Random();
        var shuffled = items.OrderBy(_ => rng.Next()).ToList();
        return shuffled.Take(Math.Min(5, shuffled.Count)).ToList();
    }

    private static string AssembleArcPromptWithPicks(List<(string Category, string Body)> picks)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < picks.Count; i++)
        {
            sb.AppendLine($"[{picks[i].Category}] {picks[i].Body}");
            if (i < picks.Count - 1)
                sb.AppendLine();
        }

        return DefaultArcFromPicksPrompt.Replace("{PICKED_ITEMS}", sb.ToString(), StringComparison.Ordinal);

    }

    private View BuildPickedItemView((string Category, string Body) item)
    {
        var badgeColor = item.Category switch
        {
            "BROKEN" => Color.FromArgb("#EF5350"),
            "ROUGH" => Color.FromArgb("#FFA726"),
            "MISSING" => Color.FromArgb("#42A5F5"),
            _ => Color.FromArgb("#9E9E9E")
        };

        var badge = new Frame
        {
            BackgroundColor = badgeColor,
            CornerRadius = 4,
            Padding = new Thickness(6, 2),
            HasShadow = false,
            HorizontalOptions = LayoutOptions.Start,
            Content = new Label
            {
                Text = item.Category,
                FontSize = 10,
                TextColor = Colors.White,
                FontAttributes = FontAttributes.Bold
            }
        };

        var bodyLabel = new Label
        {
            Text = item.Body,
            FontSize = 12,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        return new Frame
        {
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 8,
            Padding = 10,
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children = { badge, bodyLabel }
            }
        };
    }

    private async Task CopyNextTaskPromptAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        if (ToWorkflowState(project.WorkflowState) != WebsiteWorkflowState.Idle)
        {
            await DisplayAlert("Task already in progress", "Finish or cancel the current workflow before starting a new task.", "OK");
            return;
        }

        var prompt = BuildNextTaskPrompt(project);
        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert(
            "Prompt copied",
            "Prompt copied to clipboard. Paste it into Claude or ChatGPT in a browser. The LLM will respond with a NEXT TASK and a CODEX PROMPT. Copy the LLM's ENTIRE response (both sections together) and tap Paste Task Plan back in Bannister.",
            "OK");

        try
        {
            if (await _projectService.AdvanceToWaitingForLLMAsync(project.Id))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task CopyBatchNextTaskPromptAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        if (ToWorkflowState(project.WorkflowState) != WebsiteWorkflowState.Idle)
        {
            await DisplayAlert("Task already in progress", "Finish or cancel the current workflow before starting a new batch.", "OK");
            return;
        }

        var batchSize = GetSelectedBatchSize();
        var prompt = BatchNextTaskPromptTemplateHeader
            .Replace("{BATCH_SIZE}", batchSize.ToString(), StringComparison.Ordinal)
            + BuildProjectContextBlock(project);

        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert(
            "Batch prompt copied",
            $"Batch prompt copied. Paste it into Claude or ChatGPT. The LLM should return ARC TITLE and one combined CODEX PROMPT covering {batchSize} tasks worth of work. Copy the entire response and tap Paste Task Plan back in Bannister.",
            "OK");

        try
        {
            if (await _projectService.AdvanceToWaitingForLLMAsync(project.Id))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task PasteTaskPlanAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Paste LLM Response",
            "Paste the full response from Claude or ChatGPT below.",
            "",
            "Paste the NEXT TASK and CODEX PROMPT response here...");

        if (result == null)
            return;

        var batchPlan = ParseBatchTaskResponse(result);
        if (batchPlan != null)
        {
            try
            {
                if (await _projectService.AdvanceToReadyToExecuteAsync(project.Id, batchPlan.Value.ArcTitle, batchPlan.Value.CodexPrompt, GetSelectedBatchSize()))
                {
                    await RefreshCurrentProjectAsync();
                    await DisplayAlert("Batch parsed", $"Parsed combined batch arc. Bannister stored the arc title and one Codex prompt worth {GetSelectedBatchSize()} task(s).", "OK");
                    return;
                }
            }
            catch (ReadOnlyDatabaseException)
            {
                await ShowReadOnlyAlertAsync();
                return;
            }
        }

        var parsed = ParseLlmTaskResponse(result);
        if (parsed == null)
        {
            await DisplayAlert(
                "Could not parse",
                "Could not parse the LLM response. Expected either batch ARC TITLE: and CODEX PROMPT: sections, or single-task NEXT TASK: and CODEX PROMPT: sections.",
                "OK");
            return;
        }

        try
        {
            if (await _projectService.AdvanceToReadyToExecuteAsync(project.Id, parsed.Value.TaskTitle, parsed.Value.CodexPrompt))
            {
                await RefreshCurrentProjectAsync();
                await DisplayAlert("Task plan parsed", "Task plan parsed. Bannister extracted the task title and the Codex prompt. Tap Copy Codex Prompt to put just the Codex prompt on your clipboard, then paste into Codex CLI.", "OK");
            }
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task CancelWorkflowAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var confirm = await DisplayAlert(
            "Cancel current task?",
            "Cancel the current task and clear all pending data?",
            "Yes",
            "No");

        if (!confirm)
            return;

        try
        {
            if (await _projectService.ResetWorkflowAsync(project.Id))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task CopyCodexPromptAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        if (string.IsNullOrWhiteSpace(project.PendingCodexPrompt))
        {
            await DisplayAlert("No Codex prompt", "No pending Codex prompt is available.", "OK");
            return;
        }

        await Clipboard.SetTextAsync(project.PendingCodexPrompt);
        await DisplayAlert(
            "Codex prompt copied",
            "Codex prompt copied. Open Terminal (the button in Project Files), run 'codex', paste this prompt, let Codex finish, then tap Paste Codex Result with Codex's response.",
            "OK");
    }

    private async Task PasteCodexResultAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Paste Codex Result",
            "Paste Codex's final output. Bannister will extract the commit message.",
            "",
            "Paste Codex's output here...");

        if (result == null)
            return;

        var commitMessage = ParseCodexCommitMessage(result);
        if (string.IsNullOrWhiteSpace(commitMessage))
        {
            await DisplayAlert(
                "Commit message missing",
                "Could not find a COMMIT MESSAGE: line in Codex's output. You can edit the commit message manually using Edit Commit Message.",
                "OK");
            commitMessage = "Codex task";
        }

        try
        {
            if (await _projectService.AdvanceToReadyToCommitAsync(project.Id, commitMessage))
            {
                await RefreshCurrentProjectAsync();
                await DisplayAlert("Result parsed", "Result parsed. Review the commit message and tap Commit and Push.", "OK");
            }
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task EditPendingTaskTitleAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Edit Task Title",
            "Edit the pending task title.",
            project.PendingTaskTitle,
            "Task title...");

        if (result == null)
            return;

        try
        {
            if (await _projectService.SetPendingTaskTitleAsync(project.Id, result.Trim()))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task EditPendingCodexPromptAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Edit Codex Prompt",
            "Edit the prompt that will be pasted into Codex CLI.",
            project.PendingCodexPrompt,
            "Codex prompt...");

        if (result == null)
            return;

        try
        {
            if (await _projectService.SetPendingCodexPromptAsync(project.Id, result))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task EditPendingCommitMessageAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Edit Commit Message",
            "Edit the git commit message for this task.",
            project.PendingCommitMessage,
            "Commit message...");

        if (result == null)
            return;

        try
        {
            if (await _projectService.SetPendingCommitMessageAsync(project.Id, result.Trim()))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task CommitAndPushAsync()
    {
        if (!IsWindows())
        {
            await DisplayAlert("Windows only", "Commit and Push is only available on Windows. Run git manually on your PC.", "OK");
            return;
        }

        if (_projectService.IsReadOnly)
        {
            await ShowReadOnlyAlertAsync();
            return;
        }

        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        if (string.IsNullOrWhiteSpace(project.CodebasePath))
        {
            await DisplayAlert("Code folder not set", "Code folder not set. Use Setup Guide Step 9 to create the project folder first.", "OK");
            return;
        }

        if (project.CodebasePath.Contains('"'))
        {
            await DisplayAlert("Invalid path", "Invalid characters in code folder path.", "OK");
            return;
        }

        if (!Directory.Exists(project.CodebasePath))
        {
            await DisplayAlert("Folder not found", $"Folder not found at {project.CodebasePath}. Re-create the folder via Setup Guide Step 9.", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(project.PendingCommitMessage))
        {
            await DisplayAlert("Commit message missing", "Enter a commit message before committing.", "OK");
            return;
        }

        var sanitizedCommitMessage = project.PendingCommitMessage
            .Replace('"', '\'')
            .Replace('`', '\'')
            .Replace('%', ' ')
            .Replace('&', '+')
            .Replace('|', ' ')
            .Replace('<', '(')
            .Replace('>', ')')
            .Replace('^', ' ')
            .Trim();
        if (string.IsNullOrWhiteSpace(sanitizedCommitMessage))
        {
            await DisplayAlert("Commit message empty", "Commit message empty after sanitization. Edit it and try again.", "OK");
            return;
        }

        var arguments = $"/c cd /d \"{project.CodebasePath}\" && git add . && git commit -m \"{sanitizedCommitMessage}\" && git push";

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = project.CodebasePath
                }
            };

            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode == 0)
            {
                try
                {
                    if (await _projectService.CompleteWorkflowAsync(project.Id, project.PendingTaskTitle))
                    {
                        var updated = await _projectService.GetByIdAsync(project.Id);
                        await RefreshCurrentProjectAsync();
                        await DisplayAlert("Committed and pushed!", $"Committed and pushed! Counter incremented to {updated?.TaskCount ?? project.TaskCount + Math.Max(1, project.PendingBatchSize)}.", "OK");
                    }
                }
                catch (ReadOnlyDatabaseException)
                {
                    await ShowReadOnlyAlertAsync();
                }
                return;
            }

            await DisplayAlert(
                "Commit/push failed",
                $"Commit/push failed. Error:\n{stderr}\n\nOutput:\n{stdout}\n\nFix the issue and try again. Common causes: not logged in to git remote, no changes to commit, push conflict - pull first.",
                "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Could not run git", $"Could not run git: {ex.Message}", "OK");
        }
    }

    private async Task ViewTaskLogAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var result = await ShowMultilineEditorAsync(
            "Task Log",
            "Edit your completed task log. Most recent at top.",
            project.CompletedTaskTitles,
            "No tasks logged yet.");

        if (result == null)
            return;

        try
        {
            if (await _projectService.SetCompletedTaskTitlesAsync(project.Id, result))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task<string?> ShowMultilineEditorAsync(
        string title,
        string subtitle,
        string initialText,
        string placeholder)
    {
        var editorPage = new WebsiteMultilineEditorPage(title, subtitle, initialText, placeholder);
        await Navigation.PushModalAsync(editorPage);
        return await editorPage.ShowAsync();
    }

    private static string GetProjectVisionContext(WebsiteProject project)
    {
        return !string.IsNullOrWhiteSpace(project.VisionRefined)
            ? project.VisionRefined
            : !string.IsNullOrWhiteSpace(project.VisionRaw)
                ? project.VisionRaw
                : !string.IsNullOrWhiteSpace(project.IdeaText)
                    ? project.IdeaText
                    : "(no vision set yet)";
    }

    private static List<string> GetRecentCompletedTaskTitles(WebsiteProject project, int count)
    {
        return (project.CompletedTaskTitles ?? "")
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(count)
            .ToList();
    }

    private static string BuildQAExplorationPrompt(WebsiteProject project)
    {
        var liveUrl = $"https://{project.Title}";
        var vision = GetProjectVisionContext(project);
        var summary = !string.IsNullOrWhiteSpace(project.ProjectSummary)
            ? project.ProjectSummary
            : "(no project summary saved yet)";
        var recentTasks = GetRecentCompletedTaskTitles(project, 5);

        var prompt = new StringBuilder();
        prompt.AppendLine("You are a QA exploration agent for a live website.");
        prompt.AppendLine();
        prompt.AppendLine($"LIVE SITE URL: {liveUrl}");
        prompt.AppendLine();
        prompt.AppendLine("PROJECT VISION:");
        prompt.AppendLine(vision);
        prompt.AppendLine();
        prompt.AppendLine("PROJECT SUMMARY:");
        prompt.AppendLine(summary);
        prompt.AppendLine();
        prompt.AppendLine("MOST RECENT COMPLETED TASKS:");
        if (recentTasks.Count == 0)
        {
            prompt.AppendLine("(no completed tasks logged yet)");
        }
        else
        {
            foreach (var task in recentTasks)
                prompt.AppendLine($"- {task}");
        }
        prompt.AppendLine();
        prompt.AppendLine("INSTRUCTIONS:");
        prompt.AppendLine("Visit the live site as a real user would. Walk through the site, click into key surfaces, try the main flows, and observe the actual deployed behavior.");
        prompt.AppendLine("Do not edit any code. Do not propose implementation tasks. Only observe and report what you find.");
        prompt.AppendLine("Output plain text only, with no markdown code fences anywhere, so the whole response pastes cleanly into Bannister.");
        prompt.AppendLine();
        prompt.AppendLine("Organize your report under these exact labeled sections:");
        prompt.AppendLine("WORKING");
        prompt.AppendLine("What is functioning well.");
        prompt.AppendLine();
        prompt.AppendLine("BROKEN");
        prompt.AppendLine("What is actively broken, with steps to reproduce.");
        prompt.AppendLine();
        prompt.AppendLine("ROUGH");
        prompt.AppendLine("What works but feels unfinished or confusing.");
        prompt.AppendLine();
        prompt.AppendLine("MISSING");
        prompt.AppendLine("What a user would expect to find that is not there.");
        prompt.AppendLine();
        prompt.AppendLine("Be concrete and specific: name the route, name the element, and describe what happened.");
        return prompt.ToString();
    }

    private static string BuildProjectContextBlock(WebsiteProject project)
    {
        var vision = GetProjectVisionContext(project);
        var summary = !string.IsNullOrWhiteSpace(project.ProjectSummary)
            ? project.ProjectSummary
            : "(not yet generated - tap Copy Update Summary Prompt after Codex has built something)";
        var taskLog = !string.IsNullOrWhiteSpace(project.CompletedTaskTitles)
            ? project.CompletedTaskTitles
            : "(no tasks logged yet - this is the first task)";

        var prompt = new StringBuilder();
        prompt.AppendLine($"PROJECT: {project.Title}");
        prompt.AppendLine($"PROGRESS: {project.TaskCount} of {project.TaskTarget} tasks completed");
        prompt.AppendLine();
        prompt.AppendLine("VISION:");
        prompt.AppendLine(vision);
        prompt.AppendLine();
        prompt.AppendLine("CURRENT STATE (latest Codex-generated summary):");
        prompt.AppendLine(summary);
        prompt.AppendLine();
        prompt.AppendLine("COMPLETED TASKS SO FAR (most recent first):");
        prompt.AppendLine(taskLog);
        prompt.AppendLine();
        if (!string.IsNullOrWhiteSpace(project.LatestQAReport))
        {
            prompt.AppendLine("LATEST QA REPORT:");
            prompt.AppendLine($"Captured at: {FormatQAReportUtc(project.LatestQAReportCapturedAt)}");
            prompt.AppendLine(project.LatestQAReport);
            prompt.AppendLine();
            prompt.AppendLine("Weight the QA findings heavily when choosing the next task. Prioritize BROKEN items above MISSING items, MISSING items above ROUGH items, and ROUGH items above net new ideas.");
            prompt.AppendLine();
        }
        return prompt.ToString();
    }

    private static string BuildNextTaskPrompt(WebsiteProject project)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are helping me build a website step by step using Codex CLI. Suggest the next concrete development task.");
        prompt.AppendLine();
        prompt.Append(BuildProjectContextBlock(project));
        prompt.AppendLine("YOUR JOB:");
        prompt.AppendLine("Pick ONE concrete next task that advances this project. Be specific: which file or feature, what to add, why this step matters. Then write a ready-to-paste Codex prompt for the task. Respond in this exact format:");
        prompt.AppendLine();
        prompt.AppendLine("NEXT TASK:");
        prompt.AppendLine("(brief 1-2 sentence description of what to build and why)");
        prompt.AppendLine();
        prompt.AppendLine("CODEX PROMPT:");
        prompt.AppendLine("(the full prompt to paste into Codex CLI - clear, self-contained, specific about what files/components to edit)");
        prompt.AppendLine();
        prompt.AppendLine("IMPORTANT FORMATTING:");
        prompt.AppendLine("- Output the entire response as plain text. NO markdown code fences (no triple-backticks, no backticks anywhere). NO markdown bold or italics. Just plain text with the section headers NEXT TASK: and CODEX PROMPT:.");
        prompt.AppendLine("- Do this so the user can copy your entire response in one block and paste it into Bannister, which will parse out the CODEX PROMPT portion for them.");
        prompt.AppendLine("- NEXT TASK should be ONE LINE (max 100 chars) - this becomes the task title.");
        prompt.AppendLine("- CODEX PROMPT should be a complete self-contained prompt Codex can execute directly. Write it as flowing plain text with paragraph breaks and indented sub-points if needed, but NO code fences.");
        prompt.AppendLine("- End your CODEX PROMPT with these exact lines (no formatting):");
        prompt.AppendLine("  IMPORTANT: Do NOT run git add, git commit, git push, or any other git command. Do NOT stage or commit changes. Only edit files. Bannister will run the commit and push for you after you output the commit message below.");
        prompt.AppendLine("  At the end of your work, output a single line in this format: COMMIT MESSAGE: <one-line git commit message describing what you did>");
        prompt.AppendLine("- Output the commit message as a single unbroken paragraph with no line breaks anywhere inside the COMMIT MESSAGE: section. The parser reads only the first line after the marker.");
        prompt.AppendLine("- This lets the automation extract the commit message after Codex finishes.");
        prompt.AppendLine();
        prompt.Append("That's it. No other commentary.");
        return prompt.ToString();
    }

    private static (string TaskTitle, string CodexPrompt)? ParseLlmTaskResponse(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        const string nextMarker = "NEXT TASK:";
        const string codexMarker = "CODEX PROMPT:";
        var nextIndex = IndexOfMarker(response, nextMarker);
        var codexIndex = IndexOfMarker(response, codexMarker);

        if (nextIndex < 0 || codexIndex < 0 || codexIndex <= nextIndex)
            return null;

        var taskSectionStart = nextIndex + nextMarker.Length;
        var taskSection = response[taskSectionStart..codexIndex].Trim();
        var codexPrompt = response[(codexIndex + codexMarker.Length)..].Trim();

        var taskTitle = ExtractFirstMeaningfulLine(taskSection);
        if (string.IsNullOrWhiteSpace(taskTitle) || string.IsNullOrWhiteSpace(codexPrompt))
            return null;

        return (taskTitle, codexPrompt);
    }

    private static (string ArcTitle, string CodexPrompt)? ParseBatchTaskResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        raw = raw.Replace("\r\n", "\n").Replace("\r", "\n");

        var arcTitleMatch = Regex.Match(
            raw,
            @"(?im)^\s*ARC\s+TITLE\s*:\s*(?<title>.+?)\s*$",
            RegexOptions.CultureInvariant);
        if (!arcTitleMatch.Success)
            return null;

        var promptMarkerMatch = Regex.Match(
            raw,
            @"(?im)^\s*CODEX\s+PROMPT\s*:\s*$",
            RegexOptions.CultureInvariant);
        if (!promptMarkerMatch.Success)
            return null;

        var arcTitle = arcTitleMatch.Groups["title"].Value.Trim();
        var codexPromptStart = promptMarkerMatch.Index + promptMarkerMatch.Length;
        var codexPrompt = raw[codexPromptStart..].Trim();

        if (string.IsNullOrWhiteSpace(arcTitle) || string.IsNullOrWhiteSpace(codexPrompt))
            return null;

        return (arcTitle, codexPrompt);
    }

    private static string? ParseCodexCommitMessage(string codexOutput)
    {
        if (string.IsNullOrWhiteSpace(codexOutput))
            return null;

        const string marker = "COMMIT MESSAGE:";
        var markerIndex = IndexOfMarker(codexOutput, marker);
        if (markerIndex < 0)
            return null;

        var start = markerIndex + marker.Length;
        var lineEnd = codexOutput.IndexOfAny(new[] { '\r', '\n' }, start);
        var rawMessage = lineEnd >= 0
            ? codexOutput[start..lineEnd]
            : codexOutput[start..];
        var message = rawMessage.Trim().TrimStart(':').Trim();
        return string.IsNullOrWhiteSpace(message) ? null : message;
    }

    private static int IndexOfMarker(string text, string marker)
    {
        return text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
    }

    private static string ExtractFirstMeaningfulLine(string text)
    {
        var lines = text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => !(line.StartsWith("(") && line.EndsWith(")")))
            .ToList();

        if (lines.Count == 0)
            return "";

        var first = lines[0];
        if (first.StartsWith("- "))
            first = first[2..].Trim();
        if (first.StartsWith("* "))
            first = first[2..].Trim();

        return first.Length > 100 ? first[..100].Trim() : first;
    }

    private async Task CopyCodexCommandAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        if (string.IsNullOrWhiteSpace(project.CodebasePath))
        {
            await DisplayAlert(
                "Code folder not set",
                "Code folder not set. Use Setup Guide Step 9 to create the project folder first.",
                "OK");
            return;
        }

        if (project.CodebasePath.Contains('"'))
        {
            await DisplayAlert("Invalid path", "Invalid characters in code folder path.", "OK");
            return;
        }

        var command = $"cd /d \"{project.CodebasePath}\" && codex";
        await Clipboard.SetTextAsync(command);
        await DisplayAlert(
            "Command copied",
            $"Command copied: {command}. Paste into a Command Prompt to navigate to your project folder and start Codex.",
            "OK");
    }

    private async Task EditCodebasePathAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        var input = await DisplayPromptAsync(
            "Edit Code Folder Path",
            "Enter the full path to your project's code folder:",
            "Save",
            "Cancel",
            initialValue: project.CodebasePath,
            maxLength: 500);

        if (input == null)
            return;

        var trimmedPath = input.Trim();
        if (string.IsNullOrWhiteSpace(trimmedPath))
        {
            var clear = await DisplayAlert(
                "Clear the code folder path?",
                "Clear the stored code folder path for this project?",
                "Yes",
                "No");

            if (!clear)
                return;
        }

        try
        {
            if (await _projectService.SetCodebasePathAsync(project.Id, trimmedPath))
                await RefreshCurrentProjectAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await ShowReadOnlyAlertAsync();
        }
    }

    private async Task OpenTerminalAsync()
    {
        var project = await GetCurrentProjectOrAlertAsync();
        if (project == null)
            return;

        if (string.IsNullOrWhiteSpace(project.CodebasePath))
        {
            await DisplayAlert(
                "Code folder not set",
                "Code folder not set. Use Setup Guide Step 9 to create the project folder first.",
                "OK");
            return;
        }

        if (project.CodebasePath.Contains('"'))
        {
            await DisplayAlert("Invalid path", "Invalid characters in code folder path.", "OK");
            return;
        }

        if (!Directory.Exists(project.CodebasePath))
        {
            await DisplayAlert(
                "Folder not found",
                $"Folder not found at {project.CodebasePath}. Re-create the folder via Setup Guide Step 9.",
                "OK");
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/k cd /d \"{project.CodebasePath}\"",
                UseShellExecute = true,
                WorkingDirectory = project.CodebasePath
            };
            Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Could not open terminal", $"Could not open terminal: {ex.Message}", "OK");
        }
    }

    private async Task ShowReadOnlyAlertAsync()
    {
        await DisplayAlert("Read-only", "Read-only on this device. Sync from master to modify Website Builder data.", "OK");
    }

    private void RefreshStateVisibility()
    {
        var hasIdea = _currentIdeaId > 0;
        var hasProject = _currentProjectId > 0;
        _deleteIdeaButton.IsVisible = hasIdea;
        _ideaSection.IsVisible = !hasProject;
        _domainSection.IsVisible = hasIdea && !hasProject;
        _taskCounterSection.IsVisible = hasProject;
    }
}

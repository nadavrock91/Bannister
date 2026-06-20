using Bannister.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace Bannister.Views;

public class WebsiteBuilderSetupGuidePage : ContentPage
{
    private const int TotalSteps = 14;

    private readonly AuthService _auth;
    private readonly WebsiteProjectService _projectService;
    private readonly List<StepData> _steps;
    private readonly HashSet<int> _completedSteps = new();
    private readonly HorizontalStackLayout _stepIndicator;
    private readonly ContentView _stepCardContainer;
    private readonly VerticalStackLayout _completionSection;
    private readonly Grid _navigationRow;
    private readonly Button _backButton;
    private readonly Button _skipButton;
    private readonly Button _markDoneButton;

    private int _currentStep = 1;

    public WebsiteBuilderSetupGuidePage(AuthService auth, WebsiteProjectService projectService)
    {
        _auth = auth;
        _projectService = projectService;
        _steps = CreateSteps();

        Title = "Setup Guide";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _stepIndicator = new HorizontalStackLayout
        {
            Spacing = 4,
            HorizontalOptions = LayoutOptions.Center
        };

        var restartButton = new Button
        {
            Text = "Restart Guide",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            TextColor = Color.FromArgb("#666"),
            CornerRadius = 6,
            FontSize = 11,
            HeightRequest = 32,
            Padding = new Thickness(12, 0),
            HorizontalOptions = LayoutOptions.End
        };
        restartButton.Clicked += async (_, _) => await RestartGuideAsync();

        _stepCardContainer = new ContentView();

        _backButton = new Button
        {
            Text = "Back",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 44
        };
        _backButton.Clicked += async (_, _) => await BackAsync();

        _skipButton = new Button
        {
            Text = "Skip for Now",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            TextColor = Color.FromArgb("#666"),
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 40,
            Padding = new Thickness(10, 0)
        };
        _skipButton.Clicked += async (_, _) => await SkipAsync();

        _markDoneButton = new Button
        {
            Text = "Mark Done & Next",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 44
        };
        _markDoneButton.Clicked += async (_, _) => await MarkDoneAsync();

        _navigationRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            Children =
            {
                _backButton,
                _skipButton,
                _markDoneButton
            }
        };
        Grid.SetColumn(_backButton, 0);
        Grid.SetColumn(_skipButton, 1);
        Grid.SetColumn(_markDoneButton, 2);

        _completionSection = CreateCompletionSection();

        var content = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = "Setup Guide",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                new Label
                {
                    Text = "14 steps to your first deployed website. Follow in order - Bannister handles some steps for you.",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = Color.FromArgb("#666")
                },
                _stepIndicator,
                restartButton,
                _stepCardContainer,
                _navigationRow,
                _completionSection
            }
        };

        Content = new ScrollView { Content = content };
        DisplayStep(1);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadStateAsync();

        if (_currentStep == TotalSteps + 1 || _completedSteps.Count >= TotalSteps)
            DisplayCompletion();
        else
            DisplayStep(_currentStep);
    }

    private void DisplayStep(int stepNumber)
    {
        if (stepNumber < 1 || stepNumber > TotalSteps)
            stepNumber = 1;

        _currentStep = stepNumber;
        UpdateStepIndicator();

        var step = _steps[stepNumber - 1];
        _stepCardContainer.Content = CreateStepCard(stepNumber, step);
        _stepCardContainer.IsVisible = true;
        _navigationRow.IsVisible = true;
        _completionSection.IsVisible = false;

        _backButton.IsEnabled = stepNumber > 1;
        _skipButton.IsVisible = stepNumber < TotalSteps;
        _markDoneButton.Text = stepNumber == TotalSteps
            ? "Mark Done & Finish"
            : "Mark Done & Next";
    }

    private void DisplayCompletion()
    {
        _currentStep = TotalSteps + 1;
        for (var i = 1; i <= TotalSteps; i++)
            _completedSteps.Add(i);

        UpdateStepIndicator();
        _stepCardContainer.IsVisible = false;
        _navigationRow.IsVisible = false;
        _completionSection.IsVisible = true;
    }

    private void UpdateStepIndicator()
    {
        _stepIndicator.Children.Clear();

        for (var i = 1; i <= TotalSteps; i++)
        {
            var isCurrent = _currentStep == i;
            var isDone = _completedSteps.Contains(i) || _currentStep == TotalSteps + 1;
            var dot = new Frame
            {
                WidthRequest = 18,
                HeightRequest = 18,
                CornerRadius = 9,
                Padding = 0,
                HasShadow = false,
                BorderColor = isCurrent ? Color.FromArgb("#00897B") : Color.FromArgb("#BBB"),
                BackgroundColor = isDone && !isCurrent ? Color.FromArgb("#00897B") : Colors.Transparent
            };

            _stepIndicator.Children.Add(dot);
        }
    }

    private async Task BackAsync()
    {
        if (_currentStep <= 1)
            return;

        DisplayStep(_currentStep - 1);
        await SaveStateAsync();
    }

    private async Task SkipAsync()
    {
        if (_currentStep >= TotalSteps)
            return;

        DisplayStep(_currentStep + 1);
        await SaveStateAsync();
    }

    private async Task MarkDoneAsync()
    {
        if (_currentStep < 1 || _currentStep > TotalSteps)
            return;

        _completedSteps.Add(_currentStep);

        if (_completedSteps.Count >= TotalSteps || _currentStep == TotalSteps)
        {
            DisplayCompletion();
            await SaveStateAsync();
            return;
        }

        DisplayStep(_currentStep + 1);
        await SaveStateAsync();
    }

    private async Task RestartGuideAsync()
    {
        var confirm = await DisplayAlert(
            "Restart Guide",
            "Restart the guide from step 1? Completed steps will be cleared.",
            "Restart",
            "Cancel");

        if (!confirm)
            return;

        _completedSteps.Clear();
        DisplayStep(1);
        await SaveStateAsync();
    }

    private async Task LoadStateAsync()
    {
        try
        {
            _completedSteps.Clear();

            var currentStepValue = await SecureStorage.GetAsync(CurrentStepKey);
            if (!int.TryParse(currentStepValue, out _currentStep) || _currentStep < 1 || _currentStep > TotalSteps + 1)
                _currentStep = 1;

            var completedValue = await SecureStorage.GetAsync(CompletedStepsKey);
            if (!string.IsNullOrWhiteSpace(completedValue))
            {
                foreach (var part in completedValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(part, out var step) && step >= 1 && step <= TotalSteps)
                        _completedSteps.Add(step);
                }
            }
        }
        catch
        {
            _currentStep = 1;
            _completedSteps.Clear();
        }
    }

    private async Task SaveStateAsync()
    {
        try
        {
            await SecureStorage.SetAsync(CurrentStepKey, _currentStep.ToString());
            await SecureStorage.SetAsync(CompletedStepsKey, string.Join(",", _completedSteps.OrderBy(step => step)));
        }
        catch
        {
            // SecureStorage can fail on some platforms/configurations; the guide still works in-memory.
        }
    }

    private string CurrentStepKey => $"setup_guide_current_step_{_auth.CurrentUsername}";

    private string CompletedStepsKey => $"setup_guide_completed_steps_{_auth.CurrentUsername}";

    private VerticalStackLayout CreateCompletionSection()
    {
        var backButton = CreateInternalActionButton("Back to Website Builder", async () => await Navigation.PopAsync());
        var restartButton = new Button
        {
            Text = "Restart Guide",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            HeightRequest = 42,
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Start
        };
        restartButton.Clicked += async (_, _) => await RestartGuideAsync();

        return new VerticalStackLayout
        {
            IsVisible = false,
            Spacing = 14,
            Children =
            {
                new Frame
                {
                    BackgroundColor = Colors.White,
                    BorderColor = Color.FromArgb("#E0E0E0"),
                    CornerRadius = 10,
                    Padding = 16,
                    HasShadow = false,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            new Label
                            {
                                Text = "You're Set Up!",
                                FontSize = 22,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Color.FromArgb("#222")
                            },
                            new Label
                            {
                                Text = "All 14 steps complete. From here, the recurring loop is: Codex edits files -> npm run dev to test -> git push to deploy. Bannister's task counter tracks your progress.",
                                FontSize = 14,
                                TextColor = Color.FromArgb("#333"),
                                LineBreakMode = LineBreakMode.WordWrap
                            },
                            backButton,
                            restartButton
                        }
                    }
                }
            }
        };
    }

    private View CreateStepCard(int number, StepData step)
    {
        var actions = step.Actions.Select(CreateActionButton).ToArray();
        var badge = new Frame
        {
            WidthRequest = 36,
            HeightRequest = 36,
            CornerRadius = 18,
            Padding = 0,
            HasShadow = false,
            BorderColor = Color.FromArgb("#00897B"),
            BackgroundColor = Color.FromArgb("#00897B"),
            Content = new Label
            {
                Text = number.ToString(),
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Colors.White,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };
        Grid.SetColumn(badge, 0);

        var titleLabel = new Label
        {
            Text = step.Title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };
        Grid.SetColumn(titleLabel, 1);

        var titleRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                badge,
                titleLabel
            }
        };

        var cardContent = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                titleRow,
                new Label
                {
                    Text = step.Body,
                    FontSize = 14,
                    TextColor = Color.FromArgb("#333"),
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };

        if (actions.Length > 0)
        {
            var actionStack = new VerticalStackLayout
            {
                Spacing = 8
            };

            foreach (var action in actions)
                actionStack.Children.Add(action);

            cardContent.Children.Add(actionStack);
        }

        return new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            CornerRadius = 10,
            Padding = 16,
            HasShadow = false,
            Content = cardContent
        };
    }

    private Button CreateActionButton(StepAction action)
    {
        return action.Type switch
        {
            StepActionType.PopNavigation => CreateInternalActionButton(action.Label, async () => await Navigation.PopAsync()),
            StepActionType.CreateProjectFolder => CreateInternalActionButton(action.Label, async () => await CreateProjectFolderFromWizardAsync()),
            _ => CreateExternalLinkButton(action.Label, action.Payload)
        };
    }

    private async Task CreateProjectFolderFromWizardAsync()
    {
        if (DeviceInfo.Current.Platform != DevicePlatform.WinUI)
        {
            await DisplayAlert("Windows only", "Folder creation is supported on Windows only.", "OK");
            return;
        }

        var projectId = await GetLastSelectedProjectIdAsync();
        if (projectId <= 0)
        {
            await DisplayAlert(
                "Save a project first",
                "Go to Website Builder, save an idea, purchase a domain, and save it as a project. Then return to this step to create the project folder.",
                "OK");
            return;
        }

        var project = await _projectService.GetByIdAsync(projectId);
        if (project == null || !string.Equals(project.Username, _auth.CurrentUsername, StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert(
                "Save a project first",
                "The last selected project could not be found. Go to Website Builder, load or create a project, then return to this step.",
                "OK");
            return;
        }

        var parentPath = await WebsiteFolderHelper.PickParentFolderPathAsync(this);
        if (string.IsNullOrWhiteSpace(parentPath))
            return;

        var folderName = WebsiteFolderHelper.DeriveFolderName(project.Title, project.Id);
        var targetPath = Path.Combine(parentPath, folderName);

        try
        {
            Directory.CreateDirectory(targetPath);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Folder not created", $"Could not create the folder: {ex.Message}", "OK");
            return;
        }

        if (await _projectService.SetCodebasePathAsync(project.Id, targetPath))
            await DisplayAlert("Folder created", $"Folder created at {targetPath}", "OK");
    }

    private async Task<int> GetLastSelectedProjectIdAsync()
    {
        try
        {
            var value = await SecureStorage.GetAsync($"website_builder_last_project_id_{_auth.CurrentUsername}");
            return int.TryParse(value, out var projectId) ? projectId : 0;
        }
        catch
        {
            return 0;
        }
    }

    private static Button CreateExternalLinkButton(string label, string url)
    {
        var button = new Button
        {
            Text = label,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#01579B"),
            CornerRadius = 8,
            HeightRequest = 40,
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Start
        };
        button.Clicked += async (_, _) => await Launcher.OpenAsync(url);
        return button;
    }

    private static Button CreateInternalActionButton(string label, Func<Task> onClick)
    {
        var button = new Button
        {
            Text = label,
            BackgroundColor = Color.FromArgb("#00897B"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 42,
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Start
        };
        button.Clicked += async (_, _) => await onClick();
        return button;
    }

    private static List<StepData> CreateSteps()
    {
        return new List<StepData>
        {
            new(
                "Install Node.js",
                @"Node.js is the runtime that lets you install and run Codex CLI. Download the LTS (Long Term Support) version for your operating system. Run the installer with default options. After installation, restart any open terminal or command prompt.

On Windows: open Command Prompt and type 'node --version' to verify. You should see a version number like v20.x.x.",
                new List<StepAction> { new("Download Node.js", StepActionType.ExternalLink, "https://nodejs.org/") }),
            new(
                "Create OpenAI Account & Get API Key",
                @"Codex uses OpenAI's API, which requires an OpenAI platform account (different from a regular ChatGPT account). You'll add credits and create an API key.

1. Sign up or log in at platform.openai.com
2. Go to Billing - add a payment method and prepay $10-20 in credits to start
3. Go to Billing - Limits - set a hard spending limit (e.g. $50/month) as a safety net
4. Go to API Keys - Create new secret key - copy and save it securely (you can't view it again later)

Codex usage costs vary by what you ask it to do, typically $0.05 to $1 per coding session.",
                new List<StepAction> { new("OpenAI Platform", StepActionType.ExternalLink, "https://platform.openai.com/") }),
            new(
                "Install Codex CLI",
                @"With Node.js installed, open a terminal (Command Prompt on Windows, Terminal on Mac/Linux) and run:

    npm install -g @openai/codex

The -g flag installs Codex globally so you can run it from any folder.

After install, verify it works:

    codex --version

If you see a version number, Codex is installed. If you get 'command not found', restart your terminal and try again.",
                new List<StepAction> { new("Codex CLI Documentation", StepActionType.ExternalLink, "https://github.com/openai/codex") }),
            new(
                "Configure Codex with Your API Key",
                @"Codex needs your OpenAI API key to make requests. In the same terminal, run:

    codex login

Follow the prompts to paste in your API key. Codex stores it locally for future sessions.

Alternative: you can set the OPENAI_API_KEY environment variable permanently in your system settings if you prefer. Codex will pick it up automatically.

Verify the setup works by running 'codex' to open Codex in interactive mode. Type 'exit' to quit.",
                new List<StepAction>()),
            new(
                "Install VS Code",
                @"Codex edits files in your project folder, but you'll also want to look at and edit those files yourself. VS Code is free, popular, and works well alongside Codex.

Download and install with default options. Open VS Code at least once to make sure it launches.",
                new List<StepAction> { new("Download VS Code", StepActionType.ExternalLink, "https://code.visualstudio.com/") }),
            new(
                "Install Git",
                @"Git is the version control system that tracks changes to your code and lets you push it to GitHub for hosting. Codex and most modern workflows assume Git is installed.

Download and install with default options. After install, open a new terminal and verify:

    git --version

Then configure Git with your name and email - these get attached to every commit you make:

    git config --global user.name ""Your Name""
    git config --global user.email ""your@email.com""

Use the same email you'll use for your GitHub account.",
                new List<StepAction> { new("Download Git", StepActionType.ExternalLink, "https://git-scm.com/downloads") }),
            new(
                "Create GitHub Account",
                @"GitHub stores your code remotely and connects to your hosting provider for one-click deployment. It's free for both public and private repositories.

Sign up with the same email you used in your Git config (previous step). Pick a username - you'll use this in URLs and when pushing code.",
                new List<StepAction> { new("Sign up for GitHub", StepActionType.ExternalLink, "https://github.com/signup") }),
            new(
                "Buy a Domain Name",
                @"Decide on the domain you want to buy. Domains typically cost $10-15/year for a .com.

Bannister's main Website Builder page has a Go to GoDaddy button in the Step 2 section when an idea is loaded. You can also pick from other registrars below:

- GoDaddy - common, easy interface
- Namecheap - often cheaper renewal prices
- Porkbun - low prices, good interface
- Cloudflare Registrar - sells at cost, no markup

After purchase, return to Bannister's Website Builder, type the purchased domain into the Purchased Domain entry, and save it as a project.",
                new List<StepAction>
                {
                    new("GoDaddy", StepActionType.ExternalLink, "https://www.godaddy.com/en"),
                    new("Namecheap", StepActionType.ExternalLink, "https://www.namecheap.com/"),
                    new("Porkbun", StepActionType.ExternalLink, "https://porkbun.com/"),
                    new("Cloudflare Registrar", StepActionType.ExternalLink, "https://www.cloudflare.com/products/registrar/")
                }),
            new(
                "Create Project Folder",
                @"Bannister creates the local folder where Codex will edit files. Tap the button below to browse to a parent location (e.g. C:\projects) - Bannister creates the subfolder named after your domain there and saves the path on your project.

Note: this requires a project to be currently loaded in Bannister. If you haven't saved a project yet, go to Website Builder first (save an idea, type a purchased domain, save as project), then come back.",
                new List<StepAction> { new("Create Project Folder", StepActionType.CreateProjectFolder, "") }),
            new(
                "Initialize Tech Stack",
                @"Open a terminal in your new project folder (the one Bannister created in the previous step). You can do this via the Bannister Open Folder button to navigate Explorer to it, then Shift+Right-click inside the folder and pick 'Open in Terminal'.

Initialize a Next.js project (recommended starting stack):

    npx create-next-app@latest .

The dot tells it to use the current folder. Answer the prompts with defaults - it'll ask about TypeScript, ESLint, Tailwind CSS, App Router, etc.

Once setup finishes, test it works:

    npm run dev

Open http://localhost:3000 in your browser. You should see the Next.js welcome page.

Alternative stacks if you prefer:
- Astro - better for content-heavy sites
- Vite + React - lighter than Next.js
- Plain HTML/CSS/JS - simplest, no framework",
                new List<StepAction>
                {
                    new("Next.js Documentation", StepActionType.ExternalLink, "https://nextjs.org/docs"),
                    new("Astro Documentation", StepActionType.ExternalLink, "https://docs.astro.build/"),
                    new("Vite Documentation", StepActionType.ExternalLink, "https://vitejs.dev/")
                }),
            new(
                "Sign Up for Hosting",
                @"Hosting is where your website actually runs. For most modern websites, Vercel is the recommended choice - designed specifically for Next.js (which Codex works well with) and a generous free tier.

Sign up with your GitHub account so deployment is automatic later.

Alternative hosts:
- Netlify - similar to Vercel, also great free tier
- Cloudflare Pages - cheapest at scale, slightly more setup
- GitHub Pages - free, but only for static sites

For your first project, Vercel is the easiest path.",
                new List<StepAction>
                {
                    new("Vercel", StepActionType.ExternalLink, "https://vercel.com/signup"),
                    new("Netlify", StepActionType.ExternalLink, "https://www.netlify.com/"),
                    new("Cloudflare Pages", StepActionType.ExternalLink, "https://pages.cloudflare.com/"),
                    new("GitHub Pages", StepActionType.ExternalLink, "https://pages.github.com/")
                }),
            new(
                "Push Your Code to GitHub",
                @"Open a terminal in your project folder. Initialize Git and commit your code:

    git init
    git add .
    git commit -m ""initial commit""

Then create a new EMPTY repository on github.com - do NOT initialize it with a README or .gitignore (you already have local files). GitHub will show you commands like:

    git remote add origin https://github.com/USERNAME/REPO.git
    git branch -M main
    git push -u origin main

Run those in your terminal (use your actual username and repository name). Your code is now on GitHub.",
                new List<StepAction> { new("Create New Repo", StepActionType.ExternalLink, "https://github.com/new") }),
            new(
                "Deploy to Vercel",
                @"In Vercel, click Add New - Project - import your GitHub repository. Vercel auto-detects Next.js and deploys with default settings.

Vercel gives you a URL like my-site-abc123.vercel.app within a minute or two. Your site is live.

From here, every time you push code to GitHub (git push), Vercel auto-rebuilds and redeploys within 2 minutes. No more manual deploy steps.",
                new List<StepAction> { new("Vercel Dashboard", StepActionType.ExternalLink, "https://vercel.com/dashboard") }),
            new(
                "Connect Your Domain",
                @"In your Vercel project: Settings - Domains - type your domain and click Add. Vercel shows you DNS records to add at your domain registrar (e.g. GoDaddy).

Log into your registrar's DNS settings and add the records exactly as Vercel shows. Delete any conflicting default records (parking pages, etc.). Save.

DNS propagation takes 5 minutes to 48 hours. Once it completes, Vercel auto-issues an SSL certificate and your domain points to your site.",
                new List<StepAction> { new("Vercel Domains Docs", StepActionType.ExternalLink, "https://vercel.com/docs/projects/domains") })
        };
    }

    private record StepData(string Title, string Body, List<StepAction> Actions);

    private record StepAction(string Label, StepActionType Type, string Payload);

    private enum StepActionType
    {
        ExternalLink,
        PopNavigation,
        CreateProjectFolder
    }
}

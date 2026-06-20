using Microsoft.Maui.ApplicationModel;

namespace Bannister.Views;

public class WebsiteBuilderSetupGuidePage : ContentPage
{
    public WebsiteBuilderSetupGuidePage()
    {
        Title = "Setup Guide";
        BackgroundColor = Color.FromArgb("#F5F5F5");

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
                new BoxView
                {
                    HeightRequest = 1,
                    Color = Color.FromArgb("#DADADA"),
                    Margin = new Thickness(0, 4, 0, 4)
                },
                CreateStepCard(
                    1,
                    "Install Node.js",
                    @"Node.js is the runtime that lets you install and run Codex CLI. Download the LTS (Long Term Support) version for your operating system. Run the installer with default options. After installation, restart any open terminal or command prompt.

On Windows: open Command Prompt and type 'node --version' to verify. You should see a version number like v20.x.x.",
                    CreateExternalLinkButton("Download Node.js", "https://nodejs.org/")),
                CreateStepCard(
                    2,
                    "Create OpenAI Account & Get API Key",
                    @"Codex uses OpenAI's API, which requires an OpenAI platform account (different from a regular ChatGPT account). You'll add credits and create an API key.

1. Sign up or log in at platform.openai.com
2. Go to Billing - add a payment method and prepay $10-20 in credits to start
3. Go to Billing - Limits - set a hard spending limit (e.g. $50/month) as a safety net
4. Go to API Keys - Create new secret key - copy and save it securely (you can't view it again later)

Codex usage costs vary by what you ask it to do, typically $0.05 to $1 per coding session.",
                    CreateExternalLinkButton("OpenAI Platform", "https://platform.openai.com/")),
                CreateStepCard(
                    3,
                    "Install Codex CLI",
                    @"With Node.js installed, open a terminal (Command Prompt on Windows, Terminal on Mac/Linux) and run:

    npm install -g @openai/codex

The -g flag installs Codex globally so you can run it from any folder.

After install, verify it works:

    codex --version

If you see a version number, Codex is installed. If you get 'command not found', restart your terminal and try again.",
                    CreateExternalLinkButton("Codex CLI Documentation", "https://github.com/openai/codex")),
                CreateStepCard(
                    4,
                    "Configure Codex with Your API Key",
                    @"Codex needs your OpenAI API key to make requests. In the same terminal, run:

    codex login

Follow the prompts to paste in your API key. Codex stores it locally for future sessions.

Alternative: you can set the OPENAI_API_KEY environment variable permanently in your system settings if you prefer. Codex will pick it up automatically.

Verify the setup works by running 'codex' to open Codex in interactive mode. Type 'exit' to quit."),
                CreateStepCard(
                    5,
                    "Install VS Code",
                    @"Codex edits files in your project folder, but you'll also want to look at and edit those files yourself. VS Code is free, popular, and works well alongside Codex.

Download and install with default options. Open VS Code at least once to make sure it launches.",
                    CreateExternalLinkButton("Download VS Code", "https://code.visualstudio.com/")),
                CreateStepCard(
                    6,
                    "Install Git",
                    @"Git is the version control system that tracks changes to your code and lets you push it to GitHub for hosting. Codex and most modern workflows assume Git is installed.

Download and install with default options. After install, open a new terminal and verify:

    git --version

Then configure Git with your name and email - these get attached to every commit you make:

    git config --global user.name ""Your Name""
    git config --global user.email ""your@email.com""

Use the same email you'll use for your GitHub account.",
                    CreateExternalLinkButton("Download Git", "https://git-scm.com/downloads")),
                CreateStepCard(
                    7,
                    "Create GitHub Account",
                    @"GitHub stores your code remotely and connects to your hosting provider for one-click deployment. It's free for both public and private repositories.

Sign up with the same email you used in your Git config (previous step). Pick a username - you'll use this in URLs and when pushing code.",
                    CreateExternalLinkButton("Sign up for GitHub", "https://github.com/signup")),
                CreateStepCard(
                    8,
                    "Buy a Domain Name",
                    @"Decide on the domain you want to buy. Domains typically cost $10-15/year for a .com.

Bannister's main Website Builder page has a Go to GoDaddy button in the Step 2 section when an idea is loaded. You can also pick from other registrars below:

- GoDaddy - common, easy interface
- Namecheap - often cheaper renewal prices
- Porkbun - low prices, good interface
- Cloudflare Registrar - sells at cost, no markup

After purchase, return to Bannister's Website Builder, type the purchased domain into the Purchased Domain entry, and save it as a project.",
                    CreateExternalLinkButton("GoDaddy", "https://www.godaddy.com/en"),
                    CreateExternalLinkButton("Namecheap", "https://www.namecheap.com/"),
                    CreateExternalLinkButton("Porkbun", "https://porkbun.com/"),
                    CreateExternalLinkButton("Cloudflare Registrar", "https://www.cloudflare.com/products/registrar/")),
                CreateStepCard(
                    9,
                    "Create Project Folder",
                    @"Once your domain is saved as a Bannister project, you can create the local folder where Codex will edit files.

Bannister handles this for you. Tap the button below to return to Website Builder, load your project (or stay on it if already loaded), and tap Create Local Folder in the Project Files section. Pick where on your computer you want the folder to live (e.g. C:\projects).

Bannister creates the subfolder using your domain name and saves the path on the project.",
                    CreateInternalActionButton("Go to Website Builder", async () => await Navigation.PopAsync())),
                CreateStepCard(
                    10,
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
                    CreateExternalLinkButton("Next.js Documentation", "https://nextjs.org/docs"),
                    CreateExternalLinkButton("Astro Documentation", "https://docs.astro.build/"),
                    CreateExternalLinkButton("Vite Documentation", "https://vitejs.dev/")),
                CreateStepCard(
                    11,
                    "Sign Up for Hosting",
                    @"Hosting is where your website actually runs. For most modern websites, Vercel is the recommended choice - designed specifically for Next.js (which Codex works well with) and a generous free tier.

Sign up with your GitHub account so deployment is automatic later.

Alternative hosts:
- Netlify - similar to Vercel, also great free tier
- Cloudflare Pages - cheapest at scale, slightly more setup
- GitHub Pages - free, but only for static sites

For your first project, Vercel is the easiest path.",
                    CreateExternalLinkButton("Vercel", "https://vercel.com/signup"),
                    CreateExternalLinkButton("Netlify", "https://www.netlify.com/"),
                    CreateExternalLinkButton("Cloudflare Pages", "https://pages.cloudflare.com/"),
                    CreateExternalLinkButton("GitHub Pages", "https://pages.github.com/")),
                CreateStepCard(
                    12,
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
                    CreateExternalLinkButton("Create New Repo", "https://github.com/new")),
                CreateStepCard(
                    13,
                    "Deploy to Vercel",
                    @"In Vercel, click Add New - Project - import your GitHub repository. Vercel auto-detects Next.js and deploys with default settings.

Vercel gives you a URL like my-site-abc123.vercel.app within a minute or two. Your site is live.

From here, every time you push code to GitHub (git push), Vercel auto-rebuilds and redeploys within 2 minutes. No more manual deploy steps.",
                    CreateExternalLinkButton("Vercel Dashboard", "https://vercel.com/dashboard")),
                CreateStepCard(
                    14,
                    "Connect Your Domain",
                    @"In your Vercel project: Settings - Domains - type your domain and click Add. Vercel shows you DNS records to add at your domain registrar (e.g. GoDaddy).

Log into your registrar's DNS settings and add the records exactly as Vercel shows. Delete any conflicting default records (parking pages, etc.). Save.

DNS propagation takes 5 minutes to 48 hours. Once it completes, Vercel auto-issues an SSL certificate and your domain points to your site.",
                    CreateExternalLinkButton("Vercel Domains Docs", "https://vercel.com/docs/projects/domains")),
                new Label
                {
                    Text = "From here, the loop is: Codex edits files -> npm run dev to test -> git push to deploy. Bannister's task counter tracks your progress.",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = Color.FromArgb("#666"),
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 8, 0, 12)
                }
            }
        };

        Content = new ScrollView { Content = content };
    }

    private static View CreateStepCard(int number, string title, string body, params View[] actions)
    {
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
            Text = title,
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

        var actionStack = new VerticalStackLayout
        {
            Spacing = 8
        };

        foreach (var action in actions)
            actionStack.Children.Add(action);

        var cardContent = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                titleRow,
                new Label
                {
                    Text = body,
                    FontSize = 14,
                    TextColor = Color.FromArgb("#333"),
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };

        if (actions.Length > 0)
            cardContent.Children.Add(actionStack);

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
}

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
            Spacing = 20,
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
                    Text = "Step-by-step setup to build websites using Codex CLI. Follow in order. Skip steps you've already completed.",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Italic,
                    TextColor = Color.FromArgb("#666")
                },
                CreateSection(
                    "What You'll Need",
                    @"Before you start, you'll be creating accounts and installing tools across these services:

- Node.js (free, runs Codex CLI)
- OpenAI account with API credits (you'll pay per Codex use)
- A code editor (VS Code recommended)
- GitHub account (for source control and deployment)
- A domain name (purchased separately)
- A hosting account (Vercel recommended, free tier available)

Plan for 30-60 minutes for the first-time setup. Most of it is account creation and one-time installs."),
                CreateSection(
                    "Step 1: Install Node.js",
                    @"Node.js is the runtime that lets you install and run Codex CLI. Download the LTS (Long Term Support) version for your operating system. Run the installer with default options. After installation, restart any open terminal or command prompt.

On Windows: open Command Prompt and type 'node --version' to verify. You should see a version number like v20.x.x.

On Mac/Linux: use the official installer or your package manager."),
                CreateLinkButton("Download Node.js", "https://nodejs.org/"),
                CreateSection(
                    "Step 2: Create OpenAI Account & Get API Key",
                    @"Codex uses OpenAI's API, which requires an OpenAI platform account (different from a regular ChatGPT account). You'll add credits and create an API key.

1. Sign up or log in at platform.openai.com
2. Go to Billing -> add a payment method and prepay $10-20 in credits to start
3. Go to Billing -> Limits -> set a hard spending limit (e.g. $50/month) as a safety net
4. Go to API Keys -> Create new secret key -> copy and save it securely (you can't view it again later)

Codex usage costs vary by what you ask it to do, typically $0.05 to $1 per coding session."),
                CreateLinkButton("OpenAI Platform", "https://platform.openai.com/"),
                CreateSection(
                    "Step 3: Install Codex CLI",
                    @"With Node.js installed, open a terminal (Command Prompt on Windows, Terminal on Mac/Linux) and run:

    npm install -g @openai/codex

The -g flag installs Codex globally so you can run it from any folder.

After install, verify it works:

    codex --version

If you see a version number, Codex is installed. If you get 'command not found', restart your terminal and try again."),
                CreateLinkButton("Codex CLI Documentation", "https://github.com/openai/codex"),
                CreateSection(
                    "Step 4: Configure Codex with Your API Key",
                    @"Codex needs your OpenAI API key to make requests. In the same terminal, run:

    codex login

Follow the prompts to paste in your API key. Codex stores it locally for future sessions.

Alternative: you can set the OPENAI_API_KEY environment variable permanently in your system settings if you prefer. Codex will pick it up automatically.

Verify the setup works:

    codex

This opens Codex in interactive mode. Type 'help' or just ask a simple question to test. Type 'exit' to quit."),
                CreateSection(
                    "Step 5: Install VS Code (Recommended Editor)",
                    @"Codex edits files in your project folder, but you'll also want to look at and edit those files yourself. VS Code is free, popular, and works well alongside Codex.

Download and install with default options. Open VS Code at least once to make sure it launches."),
                CreateLinkButton("Download VS Code", "https://code.visualstudio.com/"),
                CreateSection(
                    "Step 6: Create a GitHub Account",
                    @"GitHub stores your code remotely and connects to your hosting provider for one-click deployment. It's free for public and private repositories.

Sign up with your email and pick a username. You'll use this account to push code and authorize hosting platforms later.

For deployment automation, you'll also want to install GitHub Desktop or set up Git via command line. GitHub Desktop is easier for beginners."),
                CreateLinkButton("Sign up for GitHub", "https://github.com/signup"),
                CreateLinkButton("Download GitHub Desktop", "https://desktop.github.com/"),
                CreateSection(
                    "Step 7: Buy a Domain Name",
                    @"Decide on the domain you want to buy. Domains typically cost $10-15/year for a .com.

GoDaddy is one common registrar with built-in domain search. Bannister already has a Go to GoDaddy button in the Step 2 section of the Website Builder page (visible when an idea is loaded).

Other registrars worth considering:
- Namecheap (often cheaper renewal prices)
- Porkbun (low prices, good interface)
- Cloudflare Registrar (sells at cost -- no markup)

Buy the domain. You'll connect it to your hosting in a later step."),
                CreateLinkButton("GoDaddy", "https://www.godaddy.com/en"),
                CreateLinkButton("Namecheap", "https://www.namecheap.com/"),
                CreateLinkButton("Porkbun", "https://porkbun.com/"),
                CreateLinkButton("Cloudflare Registrar", "https://www.cloudflare.com/products/registrar/"),
                CreateSection(
                    "Step 8: Sign Up for Hosting",
                    @"Hosting is where your website actually runs. For most modern websites, Vercel is the recommended choice -- it's designed specifically for the Next.js framework (which Codex works well with) and has a generous free tier.

Sign up with your GitHub account so deployment is automatic later.

Alternative hosts:
- Netlify (similar to Vercel, also great free tier)
- Cloudflare Pages (cheapest at scale, slightly more setup)
- GitHub Pages (free, but only for static sites)

For your first project, Vercel is the easiest path."),
                CreateLinkButton("Vercel", "https://vercel.com/signup"),
                CreateLinkButton("Netlify", "https://www.netlify.com/"),
                CreateLinkButton("Cloudflare Pages", "https://pages.cloudflare.com/"),
                CreateLinkButton("GitHub Pages", "https://pages.github.com/"),
                CreateSection(
                    "Step 9: Create Your Project Folder",
                    @"Decide where on your computer your website will live. A common pattern:

Windows: C:\projects\my-site
Mac/Linux: ~/projects/my-site

Open a terminal in your chosen parent folder, then create the project:

    mkdir my-site
    cd my-site

Replace 'my-site' with your actual project name (lowercase, no spaces -- use hyphens). This is where Codex will edit files."),
                CreateSection(
                    "Step 10: Initialize a Tech Stack",
                    @"Pick a tech stack to start with. For most websites, Next.js is the recommended starting point:

    npx create-next-app@latest .

The dot tells it to use the current folder. Answer the prompts with defaults (or read each one -- it'll ask about TypeScript, ESLint, Tailwind CSS, etc.).

Once setup finishes, test it works:

    npm run dev

Open http://localhost:3000 in your browser. You should see the Next.js welcome page.

Alternative stacks:
- Astro (better for content-heavy sites like blogs)
- Vite + React (lighter weight than Next.js)
- Plain HTML/CSS/JS (simplest, no framework)"),
                CreateLinkButton("Next.js Documentation", "https://nextjs.org/docs"),
                CreateLinkButton("Astro Documentation", "https://docs.astro.build/"),
                CreateLinkButton("Vite Documentation", "https://vitejs.dev/"),
                CreateSection(
                    "Step 11: Start Building with Codex",
                    @"With everything set up, open a terminal in your project folder and start Codex:

    codex

Now you can issue prompts to Codex to build features. Use Bannister's Website Builder counter section to track your progress. For each task:

1. In Bannister, tap +1 Task after completing a feature
2. Write a prompt describing the next feature (or use Bannister to generate prompts -- coming in a future feature)
3. Paste the prompt into Codex
4. Codex reads your project files, makes changes, and shows diffs
5. Review the diffs, accept the changes
6. Test locally via 'npm run dev'
7. Return to Bannister, +1, repeat

Codex works best with focused single-task prompts rather than huge multi-feature requests. Build incrementally."),
                CreateSection(
                    "Step 12: Deploy to Vercel",
                    @"When you have something worth showing the world:

1. In your project folder, initialize Git:
     git init
     git add .
     git commit -m ""initial commit""

2. Create a new repository on GitHub (don't initialize it with files -- leave it empty).

3. Push your local code to GitHub. Follow the instructions GitHub shows on the empty repo page (uses 'git remote add' and 'git push').

4. In Vercel, click Add New -> Project -> import your GitHub repository. Vercel auto-detects Next.js and deploys with default settings.

5. Vercel gives you a URL like my-site-abc123.vercel.app within a minute or two. Your site is live."),
                CreateSection(
                    "Step 13: Connect Your Domain",
                    @"In your Vercel project's Settings -> Domains, add your domain name. Vercel shows you DNS records to add at your domain registrar.

Log into GoDaddy (or your registrar), find DNS settings for your domain, and add the records exactly as Vercel shows. Save changes.

DNS propagation takes 5 minutes to 48 hours. Once it completes, your domain points to your Vercel site."),
                CreateLinkButton("Vercel Domains Documentation", "https://vercel.com/docs/projects/domains"),
                CreateSection(
                    "You're Set Up",
                    @"From here, the loop is:

- Use Bannister's Website Builder to plan ideas and pick domains
- Use Codex to build features
- Track progress with the Bannister task counter
- Commit and push to GitHub when you reach milestones
- Vercel auto-deploys every push

Total cost to get started: ~$15 for a domain + your OpenAI credits as you use Codex (~$0.05-$1 per session). Vercel free tier covers most personal projects indefinitely.

Refer back to this guide whenever you forget a step or set up a new project.")
            }
        };

        Content = new ScrollView { Content = content };
    }

    private static View CreateSection(string title, string body)
    {
        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                new Label
                {
                    Text = body,
                    FontSize = 14,
                    TextColor = Color.FromArgb("#333"),
                    LineBreakMode = LineBreakMode.WordWrap
                }
            }
        };
    }

    private static Button CreateLinkButton(string label, string url)
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
}

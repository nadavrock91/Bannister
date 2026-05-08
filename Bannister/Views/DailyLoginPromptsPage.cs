using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class DailyLoginPromptsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DailyLoginPromptService _prompts;
    private readonly VerticalStackLayout _listStack = new() { Spacing = 12 };

    public DailyLoginPromptsPage(AuthService auth, DailyLoginPromptService prompts)
    {
        _auth = auth;
        _prompts = prompts;

        Title = "Daily Login Prompts";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        headerGrid.Add(new Label
        {
            Text = "Daily Login Prompts",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            VerticalOptions = LayoutOptions.Center
        }, 0);

        var btnAdd = new Button
        {
            Text = "+ Add",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 42
        };
        btnAdd.Clicked += async (s, e) => await AddPromptAsync();
        headerGrid.Add(btnAdd, 1);

        mainStack.Children.Add(headerGrid);
        mainStack.Children.Add(new Label
        {
            Text = "Active prompts appear once per day after the user reaches Home. Use hex colors like #FFFFFF.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666666")
        });
        mainStack.Children.Add(_listStack);

        Content = new ScrollView { Content = mainStack };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPromptsAsync();
    }

    private async Task LoadPromptsAsync()
    {
        _listStack.Children.Clear();
        var prompts = await _prompts.GetPromptsAsync(_auth.CurrentUsername);

        if (prompts.Count == 0)
        {
            _listStack.Children.Add(new Label
            {
                Text = "No daily prompts configured.",
                FontSize = 15,
                TextColor = Color.FromArgb("#777777"),
                Margin = new Thickness(0, 16, 0, 0)
            });
            return;
        }

        for (int i = 0; i < prompts.Count; i++)
        {
            _listStack.Children.Add(CreatePromptCard(prompts[i], i + 1));
        }
    }

    private View CreatePromptCard(DailyLoginPrompt prompt, int position)
    {
        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 8,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            BackgroundColor = SafeColor(prompt.BackgroundColor, "#FFFFFF")
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        var textStack = new VerticalStackLayout { Spacing = 4 };
        textStack.Children.Add(new Label
        {
            Text = $"{position}. {(prompt.IsActive ? "Active" : "Inactive")}",
            FontSize = 12,
            TextColor = SafeColor(prompt.FontColor, "#333333"),
            Opacity = 0.75
        });
        textStack.Children.Add(new Label
        {
            Text = prompt.Text,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = SafeColor(prompt.FontColor, "#333333"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        textStack.Children.Add(new Label
        {
            Text = $"Font {prompt.FontColor} | Background {prompt.BackgroundColor}",
            FontSize = 11,
            TextColor = SafeColor(prompt.FontColor, "#333333"),
            Opacity = 0.65
        });

        grid.Add(textStack, 0);

        var menuButton = new Button
        {
            Text = "⋮",
            FontSize = 22,
            BackgroundColor = Color.FromArgb("#00000022"),
            TextColor = SafeColor(prompt.FontColor, "#333333"),
            CornerRadius = 18,
            WidthRequest = 42,
            HeightRequest = 38,
            Padding = 0,
            VerticalOptions = LayoutOptions.Start
        };
        menuButton.Clicked += async (s, e) => await ShowPromptMenuAsync(prompt);
        grid.Add(menuButton, 1);

        frame.Content = grid;
        return frame;
    }

    private async Task ShowPromptMenuAsync(DailyLoginPrompt prompt)
    {
        string activeLabel = prompt.IsActive ? "Disable" : "Enable";
        string action = await DisplayActionSheet(
            "Prompt",
            "Cancel",
            "Delete",
            "Edit Text",
            "Edit Font Color",
            "Edit Background Color",
            "Move Up",
            "Move Down",
            activeLabel,
            "Preview");

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        switch (action)
        {
            case "Edit Text":
                await EditTextAsync(prompt);
                break;
            case "Edit Font Color":
                await EditColorAsync(prompt, editBackground: false);
                break;
            case "Edit Background Color":
                await EditColorAsync(prompt, editBackground: true);
                break;
            case "Move Up":
                await _prompts.MovePromptAsync(prompt, -1);
                break;
            case "Move Down":
                await _prompts.MovePromptAsync(prompt, 1);
                break;
            case "Enable":
            case "Disable":
                prompt.IsActive = !prompt.IsActive;
                await _prompts.UpdatePromptAsync(prompt);
                break;
            case "Preview":
                await DailyLoginPromptDisplayPage.ShowAsync(Navigation, prompt, prompt.SortOrder, 1);
                break;
            case "Delete":
                if (await DisplayAlert("Delete Prompt", "Delete this daily login prompt?", "Delete", "Cancel"))
                {
                    await _prompts.DeletePromptAsync(prompt);
                }
                break;
        }

        await LoadPromptsAsync();
    }

    private async Task AddPromptAsync()
    {
        string? text = await DisplayPromptAsync(
            "Prompt Text",
            "Enter the text to show on first login each day:",
            "Next",
            "Cancel",
            maxLength: 500,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(text)) return;

        string? fontColor = await DisplayPromptAsync(
            "Font Color",
            "Enter a hex font color:",
            "Next",
            "Cancel",
            initialValue: "#FFFFFF",
            maxLength: 7);

        if (string.IsNullOrWhiteSpace(fontColor)) return;

        string? backgroundColor = await DisplayPromptAsync(
            "Background Color",
            "Enter a hex background color:",
            "Save",
            "Cancel",
            initialValue: "#5B63EE",
            maxLength: 7);

        if (string.IsNullOrWhiteSpace(backgroundColor)) return;

        await _prompts.AddPromptAsync(_auth.CurrentUsername, text.Trim(), fontColor, backgroundColor);
        await LoadPromptsAsync();
    }

    private async Task EditTextAsync(DailyLoginPrompt prompt)
    {
        string? text = await DisplayPromptAsync(
            "Prompt Text",
            "Edit the prompt text:",
            "Save",
            "Cancel",
            initialValue: prompt.Text,
            maxLength: 500,
            keyboard: Keyboard.Text);

        if (string.IsNullOrWhiteSpace(text)) return;

        prompt.Text = text.Trim();
        await _prompts.UpdatePromptAsync(prompt);
    }

    private async Task EditColorAsync(DailyLoginPrompt prompt, bool editBackground)
    {
        string current = editBackground ? prompt.BackgroundColor : prompt.FontColor;
        string? color = await DisplayPromptAsync(
            editBackground ? "Background Color" : "Font Color",
            "Enter a hex color like #FFFFFF:",
            "Save",
            "Cancel",
            initialValue: current,
            maxLength: 7);

        if (string.IsNullOrWhiteSpace(color)) return;

        if (editBackground)
            prompt.BackgroundColor = color;
        else
            prompt.FontColor = color;

        await _prompts.UpdatePromptAsync(prompt);
    }

    private static Color SafeColor(string value, string fallback)
    {
        try { return Color.FromArgb(value); }
        catch { return Color.FromArgb(fallback); }
    }
}

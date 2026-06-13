namespace Bannister.Views;

public sealed record HomePromptDefinition(
    string Id,
    string DisplayName,
    string Description,
    Func<Task<bool>> IsPendingAsync,
    Func<Task> AddressAsync,
    Func<Task> SkipTodayAsync);

public sealed record HomePromptManagerResult(string Action, string? PromptId = null)
{
    public const string Address = "address";
    public const string AddressAll = "address_all";
}

public class HomePromptManagerPage : ContentPage
{
    private readonly TaskCompletionSource<HomePromptManagerResult?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<HomePromptDefinition> _prompts;
    private readonly VerticalStackLayout _promptStack = new() { Spacing = 12 };
    private readonly Label _subtitle;
    private readonly Button _addressAllButton;
    private bool _isClosing;

    private HomePromptManagerPage(IReadOnlyList<HomePromptDefinition> prompts)
    {
        _prompts = prompts.ToList();
        Title = "Today's Prompts";
        BackgroundColor = Color.FromArgb("#80000000");

        var title = new Label
        {
            Text = "Today's Prompts",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238")
        };

        _subtitle = new Label
        {
            FontSize = 14,
            TextColor = Color.FromArgb("#607D8B"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var scroll = new ScrollView
        {
            Content = _promptStack,
            MaximumHeightRequest = 520
        };

        _addressAllButton = new Button
        {
            Text = "Address All in Order",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 46
        };
        _addressAllButton.Clicked += async (_, _) => await CloseAsync(new HomePromptManagerResult(HomePromptManagerResult.AddressAll));

        var closeButton = new Button
        {
            Text = "Close",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#263238"),
            CornerRadius = 8,
            HeightRequest = 46
        };
        closeButton.Clicked += async (_, _) => await CloseAsync(null);

        var bottomButtons = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        bottomButtons.Add(_addressAllButton, 0, 0);
        bottomButtons.Add(closeButton, 1, 0);

        var card = new Frame
        {
            Padding = 22,
            CornerRadius = 12,
            HasShadow = true,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#D0D7DE"),
            WidthRequest = 620,
            MaximumWidthRequest = 760,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = new VerticalStackLayout
            {
                Spacing = 14,
                Children =
                {
                    title,
                    _subtitle,
                    scroll,
                    bottomButtons
                }
            }
        };

        Content = new Grid
        {
            Padding = 20,
            BackgroundColor = Color.FromArgb("#80000000"),
            Children = { card }
        };

        RenderPrompts();
    }

    public static async Task<HomePromptManagerResult?> ShowAsync(INavigation navigation, IReadOnlyList<HomePromptDefinition> prompts)
    {
        var page = new HomePromptManagerPage(prompts);
        await navigation.PushModalAsync(page, false);
        return await page._completion.Task;
    }

    private void RenderPrompts()
    {
        _promptStack.Children.Clear();
        _subtitle.Text = $"You have {_prompts.Count} pending prompt{(_prompts.Count == 1 ? "" : "s")}. Address them in any order or skip for today.";
        _addressAllButton.IsEnabled = _prompts.Count > 0;

        if (_prompts.Count == 0)
        {
            _promptStack.Children.Add(new Label
            {
                Text = "No prompts remain.",
                FontSize = 15,
                TextColor = Color.FromArgb("#607D8B")
            });
            return;
        }

        foreach (var prompt in _prompts.ToList())
        {
            _promptStack.Children.Add(BuildPromptCard(prompt));
        }
    }

    private View BuildPromptCard(HomePromptDefinition prompt)
    {
        var name = new Label
        {
            Text = prompt.DisplayName,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#263238")
        };

        var description = new Label
        {
            Text = prompt.Description,
            FontSize = 13,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666666"),
            LineBreakMode = LineBreakMode.WordWrap
        };

        var address = new Button
        {
            Text = "Address",
            BackgroundColor = Color.FromArgb("#00796B"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 40
        };
        address.Clicked += async (_, _) => await CloseAsync(new HomePromptManagerResult(HomePromptManagerResult.Address, prompt.Id));

        var skip = new Button
        {
            Text = "Skip Today",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#263238"),
            CornerRadius = 8,
            HeightRequest = 40
        };
        skip.Clicked += async (_, _) =>
        {
            await prompt.SkipTodayAsync();
            _prompts.RemoveAll(p => p.Id == prompt.Id);
            RenderPrompts();
        };

        var buttons = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };
        buttons.Add(address, 0, 0);
        buttons.Add(skip, 1, 0);

        return new Frame
        {
            Padding = 14,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    name,
                    description,
                    buttons
                }
            }
        };
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!_isClosing)
        {
            _completion.TrySetResult(null);
        }
    }

    private async Task CloseAsync(HomePromptManagerResult? result)
    {
        if (_isClosing)
            return;

        _isClosing = true;

        try
        {
            await Navigation.PopModalAsync(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to close home prompt manager: {ex.Message}");
        }

        _completion.TrySetResult(result);
    }
}

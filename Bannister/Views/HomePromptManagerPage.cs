using Bannister.Services;

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

public class HomePromptManagerPage : Bannister.Views.ModeThemedPage
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


        var title = (new Label
        {
            Text = "Today's Prompts",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);

        _subtitle = (new Label
        {
            FontSize = 14,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.NeutralDarkButton, ThemeKey.PageText1, classifyPageText: true);

        var scroll = new ScrollView
        {
            Content = _promptStack,
            MaximumHeightRequest = 520
        };

        _addressAllButton = (new Button
        {
            Text = "Address All in Order",
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 46
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        _addressAllButton.Clicked += async (_, _) => await CloseAsync(new HomePromptManagerResult(HomePromptManagerResult.AddressAll));

        var closeButton = (new Button
        {
            Text = "Close",
            CornerRadius = 8,
            HeightRequest = 46
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
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

        var card = (new Frame
        {
            Padding = 22,
            CornerRadius = 12,
            HasShadow = true,
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
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

        Content = (new Grid
        {
            Padding = 20,
            Children = { card }
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay);

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
            _promptStack.Children.Add((new Label
            {
                Text = "No prompts remain.",
                FontSize = 15
            }).ThemeColor(Label.TextColorProperty, ThemeKey.NeutralDarkButton, ThemeKey.PageText1, classifyPageText: true));
            return;
        }

        foreach (var prompt in _prompts.ToList())
        {
            _promptStack.Children.Add(BuildPromptCard(prompt));
        }
    }

    private View BuildPromptCard(HomePromptDefinition prompt)
    {
        var name = (new Label
        {
            Text = prompt.DisplayName,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);

        var description = (new Label
        {
            Text = prompt.Description,
            FontSize = 13,
            FontAttributes = FontAttributes.Italic,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);

        var address = (new Button
        {
            Text = "Address",
            CornerRadius = 8,
            HeightRequest = 40
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightTealText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        address.Clicked += async (_, _) => await CloseAsync(new HomePromptManagerResult(HomePromptManagerResult.Address, prompt.Id));

        var skip = (new Button
        {
            Text = "Skip Today",
            CornerRadius = 8,
            HeightRequest = 40
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
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

        return (new Frame
        {
            Padding = 14,
            CornerRadius = 8,
            HasShadow = false,
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
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);
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

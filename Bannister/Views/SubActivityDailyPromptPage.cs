using Bannister.Models;

namespace Bannister.Views;

public record SubActivityStepOption(int StepIndex, string Name);

public class SubActivityDailyPromptPage : ContentPage
{
    private readonly TaskCompletionSource<string?> _completion = new();
    private bool _isClosing;

    private SubActivityDailyPromptPage(string processName, IReadOnlyList<SubActivityStepOption> pendingSteps)
    {
        Title = "Daily Routine Check";
        BackgroundColor = Color.FromArgb("#80000000");

        var card = new Frame
        {
            Padding = 22,
            CornerRadius = 12,
            HasShadow = true,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#D0D7DE"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 560,
            MaximumWidthRequest = 720
        };

        var content = new VerticalStackLayout { Spacing = 14 };
        content.Children.Add(new Label
        {
            Text = "Daily Routine Check",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1F2937"),
            HorizontalTextAlignment = TextAlignment.Center
        });
        content.Children.Add(new Label
        {
            Text = processName,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111827"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        content.Children.Add(new Label
        {
            Text = "Remaining steps",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#6B7280")
        });

        var stepsStack = new VerticalStackLayout { Spacing = 8 };
        foreach (var step in pendingSteps)
        {
            stepsStack.Children.Add(new Label
            {
                Text = $"• {step.Name}",
                FontSize = 15,
                TextColor = Color.FromArgb("#374151"),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        content.Children.Add(new ScrollView
        {
            MaximumHeightRequest = 260,
            Content = stepsStack
        });

        var buttons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        buttons.Add(CreateButton("Mark All Done", Color.FromArgb("#2E7D32"), Colors.White, "all_done"), 0, 0);
        buttons.Add(CreateButton("Some Done", Color.FromArgb("#F9A825"), Colors.Black, "some_done"), 1, 0);
        buttons.Add(CreateButton("Not Yet", Color.FromArgb("#ECEFF1"), Color.FromArgb("#263238"), "not_yet"), 2, 0);
        content.Children.Add(buttons);

        card.Content = content;
        Content = new Grid
        {
            Padding = 20,
            BackgroundColor = Color.FromArgb("#80000000"),
            Children = { card }
        };
    }

    public static async Task<string?> ShowAsync(INavigation navigation, string processName, IReadOnlyList<SubActivityStepOption> pendingSteps)
    {
        var page = new SubActivityDailyPromptPage(processName, pendingSteps);
        await navigation.PushModalAsync(page, false);
        return await page._completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_isClosing) return;
        _completion.TrySetResult(null);
    }

    private Button CreateButton(string text, Color background, Color textColor, string result)
    {
        var button = new Button
        {
            Text = text,
            BackgroundColor = background,
            TextColor = textColor,
            CornerRadius = 8,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 44
        };
        button.Clicked += async (s, e) => await CloseAsync(result);
        return button;
    }

    private async Task CloseAsync(string? result)
    {
        if (_isClosing) return;
        _isClosing = true;
        await Navigation.PopModalAsync(false);
        _completion.TrySetResult(result);
    }
}

public class SubActivityStepPickerPage : ContentPage
{
    private readonly TaskCompletionSource<List<int>?> _completion = new();
    private readonly HashSet<int> _selectedIndexes = new();
    private bool _isClosing;

    private SubActivityStepPickerPage(string processName, IReadOnlyList<SubActivityStepOption> pendingSteps)
    {
        Title = "Some Done";
        BackgroundColor = Color.FromArgb("#80000000");

        var card = new Frame
        {
            Padding = 22,
            CornerRadius = 12,
            HasShadow = true,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#D0D7DE"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 560,
            MaximumWidthRequest = 720
        };

        var content = new VerticalStackLayout { Spacing = 14 };
        content.Children.Add(new Label
        {
            Text = "Which steps are done?",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1F2937"),
            HorizontalTextAlignment = TextAlignment.Center
        });
        content.Children.Add(new Label
        {
            Text = processName,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111827"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var stepsStack = new VerticalStackLayout { Spacing = 8 };
        foreach (var step in pendingSteps)
        {
            stepsStack.Children.Add(CreateStepRow(step));
        }

        content.Children.Add(new ScrollView
        {
            MaximumHeightRequest = 300,
            Content = stepsStack
        });

        var buttons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };
        var confirm = new Button
        {
            Text = "Confirm Selected",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 44
        };
        confirm.Clicked += async (s, e) => await CloseAsync(_selectedIndexes.OrderBy(i => i).ToList());
        var cancel = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#263238"),
            CornerRadius = 8,
            HeightRequest = 44
        };
        cancel.Clicked += async (s, e) => await CloseAsync(null);
        buttons.Add(confirm, 0, 0);
        buttons.Add(cancel, 1, 0);
        content.Children.Add(buttons);

        card.Content = content;
        Content = new Grid
        {
            Padding = 20,
            BackgroundColor = Color.FromArgb("#80000000"),
            Children = { card }
        };
    }

    public static async Task<List<int>?> ShowAsync(INavigation navigation, string processName, IReadOnlyList<SubActivityStepOption> pendingSteps)
    {
        var page = new SubActivityStepPickerPage(processName, pendingSteps);
        await navigation.PushModalAsync(page, false);
        return await page._completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_isClosing) return;
        _completion.TrySetResult(null);
    }

    private View CreateStepRow(SubActivityStepOption step)
    {
        var checkbox = new CheckBox
        {
            InputTransparent = true,
            VerticalOptions = LayoutOptions.Center
        };
        var label = new Label
        {
            Text = step.Name,
            FontSize = 15,
            TextColor = Color.FromArgb("#374151"),
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center
        };
        var row = new Grid
        {
            Padding = new Thickness(8, 6),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10,
            BackgroundColor = Color.FromArgb("#F8FAFC")
        };
        row.Add(checkbox, 0, 0);
        row.Add(label, 1, 0);

        row.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                checkbox.IsChecked = !checkbox.IsChecked;
                if (checkbox.IsChecked)
                {
                    _selectedIndexes.Add(step.StepIndex);
                    row.BackgroundColor = Color.FromArgb("#E8F5E9");
                }
                else
                {
                    _selectedIndexes.Remove(step.StepIndex);
                    row.BackgroundColor = Color.FromArgb("#F8FAFC");
                }
            })
        });
        return row;
    }

    private async Task CloseAsync(List<int>? selectedIndexes)
    {
        if (_isClosing) return;
        _isClosing = true;
        await Navigation.PopModalAsync(false);
        _completion.TrySetResult(selectedIndexes);
    }
}

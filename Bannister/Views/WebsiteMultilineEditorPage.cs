using Bannister.Services;

namespace Bannister.Views;

public class WebsiteMultilineEditorPage : ContentPage
{
    private static readonly (string Text, bool IsDefault)[] SuggestedConstraints =
    {
        ("Never writes a fix prompt before an inspection. If the root cause isn't confirmed from actual code or a live API response, the only valid output is a READ-ONLY diagnostic prompt. A fix prompt without a preceding diagnostic is a guess, and guesses ship wrong commits.", true),
        ("Never ask for repo access — always give Codex prompts instead.", false),
        ("Never ask for full files — start with a read-only diagnostic Codex prompt to get oriented, then describe the feature to Codex.", false),
        ("When a feature already exists elsewhere in the codebase, read the working implementation first, then tell Codex exactly which lines to copy and where.", false),
        ("Codex should implement and build only — do not include the commit/push command inside Codex prompts.", false),
        ("Do not add attribution lines to git commit messages.", false),
        ("NEVER add new npm dependencies without explicit instruction.", false),
        ("NEVER use Vercel cron schedules — Hobby plan silently rejects deploys with sub-daily cron intervals.", false),
    };

    private readonly Editor _editor;
    private readonly TaskCompletionSource<string?> _resultTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _closingFromButton;
    private bool _isClosing;

    public WebsiteMultilineEditorPage(
        string title,
        string subtitle,
        string initialText,
        string placeholder = "")
    {
        Title = title;
        BackgroundColor = AppColors.Get(ThemeKey.SecondarySurface);

        _editor = (new Editor
        {
            AutoSize = EditorAutoSizeOption.Disabled,
            Text = initialText,
            FontSize = 13,
            HeightRequest = 400,
            MinimumHeightRequest = 300,
            Placeholder = placeholder
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText);

        var suggestionsStack = new VerticalStackLayout { Spacing = 8 };
        suggestionsStack.Children.Add((new Label
        {
            Text = "Suggested constraints — tap to add:",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));
        foreach (var suggestion in SuggestedConstraints)
        {
            var constraintText = suggestion.Text;
            var checkBox = new CheckBox
            {
                IsChecked = initialText.Contains(constraintText, StringComparison.Ordinal),
                VerticalOptions = LayoutOptions.Start
            };
            checkBox.CheckedChanged += (_, args) =>
            {
                var lines = TextParsing.NormalizeLineEndings(_editor.Text)
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .ToList();

                if (args.Value)
                {
                    if (!lines.Contains(constraintText, StringComparer.Ordinal))
                        lines.Add(constraintText);
                }
                else
                {
                    lines.RemoveAll(line => line.Equals(constraintText, StringComparison.Ordinal));
                }

                _editor.Text = string.Join(Environment.NewLine, lines);
            };

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 8
            };
            row.Add(checkBox, 0, 0);
            row.Add((new Label
            {
                Text = constraintText,
                FontSize = 12,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText3, classifyPageText: true), 1, 0);
            suggestionsStack.Children.Add(row);
        }

        var suggestionsFrame = (new Frame
        {
            Content = suggestionsStack,
            CornerRadius = 8,
            Padding = 10,
            HasShadow = false
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var cancelButton = (new Button
        {
            Text = "Cancel",
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
        cancelButton.Clicked += async (_, _) => await CloseAsync(null);

        var saveButton = (new Button
        {
            Text = "Save",
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        saveButton.Clicked += async (_, _) => await CloseAsync(_editor.Text ?? string.Empty);

        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        buttonGrid.Add(cancelButton, 0, 0);
        buttonGrid.Add(saveButton, 1, 0);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    (new Label
                    {
                        Text = title,
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                    (new Label
                    {
                        Text = subtitle,
                        FontSize = 14
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true),
                    suggestionsFrame,
                    _editor,
                    buttonGrid
                }
            }
        };
    }

    public Task<string?> ShowAsync()
    {
        return _resultTcs.Task;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        if (!_closingFromButton)
            _resultTcs.TrySetResult(null);
    }

    private async Task CloseAsync(string? result)
    {
        if (_isClosing)
            return;

        _isClosing = true;
        _closingFromButton = true;
        await Navigation.PopModalAsync();
        _resultTcs.TrySetResult(result);
    }
}

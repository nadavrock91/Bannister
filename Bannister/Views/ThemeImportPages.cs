using Bannister.Services;

namespace Bannister.Views;

/// <summary>Light modal inputs close fully before their awaiting caller opens the next page.</summary>
internal abstract class ThemeResultPage<T> : ContentPage
{
    private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly T _cancelled;
    private bool _closing;
    protected ThemeResultPage(T cancelled)
    {
        _cancelled = cancelled;
        this.ThemeColor(BackgroundColorProperty, ThemeKey.CardBackground);
    }

    public Task<T> Result => _completion.Task;
    protected async Task CloseAsync(T result)
    {
        if (_closing) return;
        _closing = true;
        try { await Navigation.PopModalAsync(); }
        catch (Exception ex)
        {
            _closing = false;
            System.Diagnostics.Debug.WriteLine(ex);
            await DisplayAlert("Could not close", ex.Message, "OK");
            return;
        }
        _completion.TrySetResult(result);
    }

    protected override void OnDisappearing()
    {
        if (!_closing) _completion.TrySetResult(_cancelled);
        base.OnDisappearing();
    }

    protected static Button Action(string text, ThemeKey background) => new Button { Text = text, Margin = 3 }
        .ThemeColor(BackgroundColorProperty, background).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
}

internal sealed class ThemePastePage : ThemeResultPage<string?>
{
    public ThemePastePage() : base(null)
    {
        Title = "Import from LLM";
        var editor = new Editor
        {
            Placeholder = "Paste the theme assignments here",
            HeightRequest = 420, AutoSize = EditorAutoSizeOption.Disabled,
            HorizontalOptions = LayoutOptions.Fill
        }.ThemeColor(BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(Editor.TextColorProperty, ThemeKey.InputText);
        var paste = Action("Paste", ThemeKey.ButtonPrimaryBackground);
        paste.Clicked += async (_, _) =>
        {
            try
            {
                string? incoming = await Clipboard.GetTextAsync();
                if (string.IsNullOrEmpty(incoming)) return;
                string current = editor.Text ?? "";
                int start = Math.Clamp(editor.CursorPosition, 0, current.Length);
                int length = Math.Clamp(editor.SelectionLength, 0, current.Length - start);
                editor.Text = current.Remove(start, length).Insert(start, incoming);
                editor.CursorPosition = start + incoming.Length;
                editor.SelectionLength = 0;
            }
            catch (Exception ex) { await DisplayAlert("Paste unavailable", ex.Message, "OK"); }
            finally { editor.Focus(); }
        };
        var review = Action("Review", ThemeKey.SecondaryButtonBackground);
        review.Clicked += async (_, _) => await CloseAsync(editor.Text ?? "");
        var cancel = Action("Cancel", ThemeKey.NeutralHoverButton);
        cancel.Clicked += async (_, _) => await CloseAsync(null);
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20, Spacing = 12,
                Children =
                {
                    new Label { Text = Title, FontSize = 22, FontAttributes = FontAttributes.Bold }, editor,
                    new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { paste, review, cancel } }
                }
            }
        };
    }
}

internal sealed class ThemeReviewPage : ThemeResultPage<IReadOnlyList<ThemeColorChange>?>
{
    public ThemeReviewPage(ActivityDisplayMode mode, IReadOnlyDictionary<string, Color> current, ThemeImportResult parsed) : base(null)
    {
        Title = "Review UI Colors";
        var selected = parsed.Changes.ToDictionary(c => c.Definition.Name, _ => true);
        var warningText = new Label { FontSize = 12 }.ThemeColor(Label.TextColorProperty, ThemeKey.CardText2);
        var apply = Action("Apply Selected", ThemeKey.SuccessDarkButtonBackground);
        IReadOnlyList<ThemeColorChange> Selection() => parsed.Changes.Where(c => selected[c.Definition.Name]).ToList();
        void UpdateWarnings()
        {
            var warnings = parsed.Warnings.Concat(ThemeImportExport.ContrastWarnings(mode, current, Selection())).ToList();
            warningText.Text = warnings.Count == 0 ? "No warnings." : string.Join("\n", warnings);
            apply.Text = $"Apply Selected ({selected.Values.Count(value => value)})";
            apply.IsEnabled = selected.Values.Any(value => value);
        }
        var rows = new VerticalStackLayout { Spacing = 14 };
        if (parsed.Changes.Count == 0) rows.Children.Add(new Label { Text = "No color changes recognized; valid values may already match this palette." });
        foreach (var change in parsed.Changes)
        {
            var check = new CheckBox { IsChecked = true, VerticalOptions = LayoutOptions.Start };
            check.CheckedChanged += (_, args) => { selected[change.Definition.Name] = args.Value; UpdateWarnings(); };
            View Swatch(string caption, string hex)
            {
                var swatch = new BoxView { Color = ModeTheme.ParseStoredHex(hex), WidthRequest = 42, HeightRequest = 28 };
                ModeThemedPage.SetPreserveColors(swatch, true);
                return new VerticalStackLayout { Spacing = 2, Children = { new Label { Text = caption, FontSize = 11 }, swatch, new Label { Text = hex, FontSize = 12 } } };
            }
            var values = new FlexLayout
            {
                Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                Children = { Swatch("Old", change.OldHex), Swatch("New", change.NewHex) }
            };
            foreach (var child in values.Children.OfType<View>()) child.Margin = new Thickness(0, 0, 18, 0);
            var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8 };
            row.Add(check, 0);
            row.Add(new VerticalStackLayout
            {
                Spacing = 4,
                Children = { new Label { Text = $"{change.Definition.Label} · {change.Definition.Group}", FontAttributes = FontAttributes.Bold }, values }
            }, 1);
            rows.Children.Add(row);
        }
        apply.Clicked += async (_, _) => await CloseAsync(Selection());
        var cancel = Action("Cancel", ThemeKey.NeutralHoverButton);
        cancel.Clicked += async (_, _) => await CloseAsync(null);
        UpdateWarnings();
        Content = new Grid
        {
            Padding = 20, RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children = { new ScrollView { Content = new VerticalStackLayout
            {
                Spacing = 12, Children =
                {
                    new Label { Text = $"Review {ThemeImportExport.ModeName(mode)} colors", FontSize = 22, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Warnings (contrast is recalculated for the checked colors)", FontAttributes = FontAttributes.Bold },
                    warningText, rows
                }
            } } }
        };
        var actions = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { apply, cancel } };
        ((Grid)Content).Add(actions, 0, 1);
    }
}

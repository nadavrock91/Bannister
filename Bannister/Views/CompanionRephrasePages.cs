using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

internal abstract class CompanionRephraseResultPage<T> : ModeThemedPage
{
    private readonly TaskCompletionSource<T> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly T _cancelled;
    private bool _closing;

    protected CompanionRephraseResultPage(T cancelled) => _cancelled = cancelled;

    public Task<T> Result => _completion.Task;

    protected async Task CloseAsync(T result)
    {
        if (_closing) return;
        _closing = true;
        await Navigation.PopModalAsync();
        _completion.TrySetResult(result);
    }

    protected override void OnDisappearing()
    {
        if (!_closing) _completion.TrySetResult(_cancelled);
        base.OnDisappearing();
    }

    protected static Button Action(string text, ThemeKey background, ThemeKey foreground = ThemeKey.ButtonPrimaryText) =>
        (new Button { Text = text, Margin = 3 })
            .ThemeColor(Button.BackgroundColorProperty, background)
            .ThemeColor(Button.TextColorProperty, foreground);

    protected static Label PageText(string text, ThemeKey color = ThemeKey.PageText1, double size = 14) =>
        (new Label { Text = text, FontSize = size, LineBreakMode = LineBreakMode.WordWrap })
            .ThemeColor(Label.TextColorProperty, color);
}

internal sealed class CompanionRephraseInstructionPage : CompanionRephraseResultPage<string?>
{
    public CompanionRephraseInstructionPage(string scopeName, int pointCount) : base(null)
    {
        Title = "Rephrase with LLM";
        var instruction = (new Editor
        {
            Placeholder = "What should the LLM change?",
            Text = "",
            HeightRequest = 150,
            AutoSize = EditorAutoSizeOption.TextChanges
        }).ThemeColor(Editor.BackgroundColorProperty, ThemeKey.InputBackground)
          .ThemeColor(Editor.TextColorProperty, ThemeKey.InputText);

        var copy = Action("Copy Prompt", ThemeKey.ButtonPrimaryBackground);
        copy.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(instruction.Text))
            {
                await DisplayAlert("Instruction required", "Describe what the LLM should change.", "OK");
                return;
            }
            await CloseAsync(instruction.Text.Trim());
        };
        var cancel = Action("Cancel", ThemeKey.NeutralButtonBackground, ThemeKey.NeutralButtonText);
        cancel.Clicked += async (_, _) => await CloseAsync(null);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 12,
                Children =
                {
                    PageText("Rephrase with LLM", ThemeKey.PageText1, 24).Bold(),
                    PageText($"Scope: {scopeName} · {pointCount} point{(pointCount == 1 ? "" : "s")}", ThemeKey.PageText2, 13),
                    PageText("Tell the LLM how to rewrite the text. Point metadata will not be changed.", ThemeKey.PageText2, 13),
                    instruction,
                    new FlexLayout
                    {
                        Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
                        Children = { copy, cancel }
                    }
                }
            }
        };
    }
}

internal sealed class CompanionRephrasePastePage : CompanionRephraseResultPage<string?>
{
    public CompanionRephrasePastePage() : base(null)
    {
        Title = "Paste Rephrased Points";
        var editor = (new Editor
        {
            Placeholder = "Paste the LLM response here",
            HeightRequest = 380,
            AutoSize = EditorAutoSizeOption.Disabled
        }).ThemeColor(Editor.BackgroundColorProperty, ThemeKey.InputBackground)
          .ThemeColor(Editor.TextColorProperty, ThemeKey.InputText);
        var paste = Action("Paste from clipboard", ThemeKey.ButtonPrimaryBackground);
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
        var cancel = Action("Cancel", ThemeKey.NeutralButtonBackground, ThemeKey.NeutralButtonText);
        cancel.Clicked += async (_, _) => await CloseAsync(null);

        Content = new Grid
        {
            Padding = 20,
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }
        };
        ((Grid)Content).Add(PageText("Paste the LLM response", ThemeKey.PageText1, 22).Bold(), 0, 0);
        ((Grid)Content).Add(editor, 0, 1);
        ((Grid)Content).Add(new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Children = { paste, review, cancel }
        }, 0, 2);
    }
}

internal sealed class CompanionRephraseReviewPage : CompanionRephraseResultPage<IReadOnlyList<CompanionRephraseChange>?>
{
    public CompanionRephraseReviewPage(
        CompanionRephraseParseResult parsed,
        IReadOnlyList<CompanionDataPoint> companionPoints) : base(null)
    {
        Title = "Review Rephrased Points";
        var byId = companionPoints.ToDictionary(x => x.Id);
        var selected = parsed.Changes.ToDictionary(x => x.PointId, _ => true);
        var apply = Action("Apply Selected", ThemeKey.SuccessButtonBackground);
        var rows = new VerticalStackLayout { Spacing = 12 };

        foreach (var change in parsed.Changes)
        {
            var check = new CheckBox { IsChecked = true, VerticalOptions = LayoutOptions.Start };
            check.CheckedChanged += (_, args) =>
            {
                selected[change.PointId] = args.Value;
                apply.Text = $"Apply Selected ({selected.Values.Count(x => x)})";
                apply.IsEnabled = selected.Values.Any(x => x);
            };
            var cardBody = new VerticalStackLayout
            {
                Spacing = 5,
                Children =
                {
                    (new Label { Text = $"Point {change.PointId}", FontAttributes = FontAttributes.Bold })
                        .ThemeColor(Label.TextColorProperty, ThemeKey.CardText1),
                    (new Label { Text = "Old", FontSize = 11, FontAttributes = FontAttributes.Bold })
                        .ThemeColor(Label.TextColorProperty, ThemeKey.CardText3),
                    (new Label { Text = change.OldText, LineBreakMode = LineBreakMode.WordWrap })
                        .ThemeColor(Label.TextColorProperty, ThemeKey.CardText2),
                    (new Label { Text = "New", FontSize = 11, FontAttributes = FontAttributes.Bold })
                        .ThemeColor(Label.TextColorProperty, ThemeKey.CardText3),
                    (new Label { Text = change.NewText, LineBreakMode = LineBreakMode.WordWrap })
                        .ThemeColor(Label.TextColorProperty, ThemeKey.CardText1)
                }
            };
            var grid = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 8
            };
            grid.Add(check, 0);
            grid.Add(cardBody, 1);
            rows.Children.Add((new Frame { Padding = 12, HasShadow = false, Content = grid })
                .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground));
        }

        if (parsed.Changes.Count == 0)
            rows.Children.Add(PageText("No applicable text changes were found.", ThemeKey.PageText2));

        AddReport(rows, "IDs not found", parsed.IdsNotFound, byId);
        AddReport(rows, "IDs outside the exported scope", parsed.IdsOutsideScope, byId);
        AddReport(rows, "Points left unchanged", parsed.UnchangedIds, byId);
        AddReport(rows, "Empty texts", parsed.EmptyTextIds, byId);
        if (parsed.Warnings.Count > 0)
        {
            rows.Children.Add(PageText($"Warnings ({parsed.Warnings.Count})", ThemeKey.PageText1, 15).Bold());
            foreach (string warning in parsed.Warnings)
                rows.Children.Add(PageText(warning, ThemeKey.PageText3, 12));
        }

        apply.Text = $"Apply Selected ({selected.Count})";
        apply.IsEnabled = selected.Count > 0;
        apply.Clicked += async (_, _) => await CloseAsync(parsed.Changes.Where(x => selected[x.PointId]).ToList());
        var cancel = Action("Cancel", ThemeKey.NeutralButtonBackground, ThemeKey.NeutralButtonText);
        cancel.Clicked += async (_, _) => await CloseAsync(null);

        Content = new Grid
        {
            Padding = 20,
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
            Children =
            {
                new ScrollView
                {
                    Content = new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            PageText("Review rephrased points", ThemeKey.PageText1, 22).Bold(),
                            PageText("Only checked text changes will be applied.", ThemeKey.PageText2, 13),
                            rows
                        }
                    }
                }
            }
        };
        ((Grid)Content).Add(new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Children = { apply, cancel }
        }, 0, 1);
    }

    private static void AddReport(
        VerticalStackLayout rows,
        string title,
        IReadOnlyList<int> ids,
        IReadOnlyDictionary<int, CompanionDataPoint> byId)
    {
        rows.Children.Add(PageText($"{title} ({ids.Count})", ThemeKey.PageText1, 15).Bold());
        if (ids.Count == 0)
        {
            rows.Children.Add(PageText("None.", ThemeKey.PageText3, 12));
            return;
        }
        foreach (int id in ids)
        {
            string text = byId.TryGetValue(id, out var point) ? $" · {point.Text}" : "";
            rows.Children.Add(PageText($"ID {id}{text}", ThemeKey.PageText3, 12));
        }
    }
}

internal static class CompanionRephraseTextExtensions
{
    public static Label Bold(this Label label)
    {
        label.FontAttributes = FontAttributes.Bold;
        return label;
    }
}

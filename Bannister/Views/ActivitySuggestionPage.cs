using Bannister.Models;

using Bannister.Services;

namespace Bannister.Views;

public class ActivitySuggestionPage : Bannister.Views.ModeThemedPage
{
    private readonly List<ActivitySuggestion> _suggestions;
    private readonly HashSet<string> _selectedIds = new(
        StringComparer.OrdinalIgnoreCase);
    private readonly TaskCompletionSource<List<ActivitySuggestion>> _tcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public ActivitySuggestionPage(
        List<ActivitySuggestion> suggestions, string gameId)
    {
        _suggestions = suggestions;
        Title = "Activity Suggestions";


        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 10
        };
        stack.Children.Add((new Label
        {
            Text = "Suggested Activities",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = "Choose any activities you want to add to this game.",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        foreach (var suggestion in _suggestions)
        {
            var check = (new CheckBox
            {
                IsChecked = false,
                VerticalOptions = LayoutOptions.Start
            }).ThemeColor(CheckBox.ColorProperty, ThemeKey.InfoText);
            check.CheckedChanged += (_, e) =>
            {
                if (e.Value) _selectedIds.Add(suggestion.Id);
                else _selectedIds.Remove(suggestion.Id);
            };

            var text = new VerticalStackLayout { Spacing = 2 };
            text.Children.Add((new Label
            {
                Text = suggestion.Name,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
            text.Children.Add((new Label
            {
                Text = suggestion.Description,
                FontSize = 12,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));

            var row = (new HorizontalStackLayout
            {
                Spacing = 8,
                Padding = 10
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
            row.Children.Add(check);
            row.Children.Add(text);
            stack.Children.Add((new Border
            {
                Content = row,
                StrokeThickness = 1
            }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground));
        }

        var continueButton = (new Button
        {
            Text = "Continue",
            CornerRadius = 8,
            HeightRequest = 44,
            Margin = new Thickness(0, 8, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ButtonPrimaryBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        continueButton.Clicked += async (_, _) =>
        {
            _tcs.TrySetResult(_suggestions
                .Where(s => _selectedIds.Contains(s.Id))
                .ToList());
            await Navigation.PopAsync();
        };
        stack.Children.Add(continueButton);
        Content = new ScrollView { Content = stack };
    }

    public Task<List<ActivitySuggestion>> GetSelectedAsync() => _tcs.Task;
}

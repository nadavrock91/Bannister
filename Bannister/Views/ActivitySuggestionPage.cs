using Bannister.Models;

namespace Bannister.Views;

public class ActivitySuggestionPage : ContentPage
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
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 10
        };
        stack.Children.Add(new Label
        {
            Text = "Suggested Activities",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Choose any activities you want to add to this game.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        foreach (var suggestion in _suggestions)
        {
            var check = new CheckBox
            {
                IsChecked = false,
                Color = Color.FromArgb("#1565C0"),
                VerticalOptions = LayoutOptions.Start
            };
            check.CheckedChanged += (_, e) =>
            {
                if (e.Value) _selectedIds.Add(suggestion.Id);
                else _selectedIds.Remove(suggestion.Id);
            };

            var text = new VerticalStackLayout { Spacing = 2 };
            text.Children.Add(new Label
            {
                Text = suggestion.Name,
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#222")
            });
            text.Children.Add(new Label
            {
                Text = suggestion.Description,
                FontSize = 12,
                TextColor = Color.FromArgb("#777"),
                LineBreakMode = LineBreakMode.WordWrap
            });

            var row = new HorizontalStackLayout
            {
                Spacing = 8,
                Padding = 10,
                BackgroundColor = Colors.White
            };
            row.Children.Add(check);
            row.Children.Add(text);
            stack.Children.Add(new Border
            {
                Content = row,
                Stroke = Color.FromArgb("#DDDDDD"),
                StrokeThickness = 1,
                BackgroundColor = Colors.White
            });
        }

        var continueButton = new Button
        {
            Text = "Continue",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            Margin = new Thickness(0, 8, 0, 0)
        };
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

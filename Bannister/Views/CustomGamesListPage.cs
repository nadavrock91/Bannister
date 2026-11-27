using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class CustomGamesListPage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly CustomGameService _customGames;
    private readonly VerticalStackLayout _listStack = new() { Spacing = 10 };
    private readonly Label _emptyLabel;

    public CustomGamesListPage(AuthService auth, CustomGameService customGames)
    {
        _auth = auth;
        _customGames = customGames;
        Title = "Custom Games";


        _emptyLabel = (new Label
        {
            Text = "No custom games yet. Create one to start tracking scores.",
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center,
            IsVisible = false,
            Margin = new Thickness(0, 24, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);

        var addButton = (new Button
        {
            Text = "+ New Custom Game",
            CornerRadius = 8,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightViolet).ThemeColor(Button.TextColorProperty, ThemeKey.AccentVioletDeep);
        addButton.Clicked += async (_, _) => await Navigation.PushAsync(new CustomGameEditPage(_auth, _customGames));

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16),
                Spacing = 12,
                Children =
                {
                    (new Label { Text = "Custom Games", FontSize = 28, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true),
                    (new Label { Text = "Create custom mini-games and track top scores.", FontSize = 14, Margin = new Thickness(0, -6, 0, 8) }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true),
                    addButton,
                    _emptyLabel,
                    _listStack
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _listStack.Children.Clear();
        var summaries = await _customGames.GetCustomGamesAsync(_auth.CurrentUsername);
        _emptyLabel.IsVisible = summaries.Count == 0;

        foreach (var summary in summaries)
        {
            _listStack.Children.Add(BuildGameCard(summary));
        }
    }

    private View BuildGameCard(CustomGameSummary summary)
    {
        var game = summary.Game;
        var frame = (new Frame
        {
            Padding = 14,
            CornerRadius = 8,
            HasShadow = false
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var title = (new Label { Text = game.Name, FontSize = 18, FontAttributes = FontAttributes.Bold }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true);
        var details = (new Label
        {
            Text = $"{GetEndSummary(game)}   Scores: {summary.FinishedInstanceCount}   Top: {(summary.TopScore?.ToString() ?? "-")}",
            FontSize = 13
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);

        var menu = (new Button
        {
            Text = "⋮",
            WidthRequest = 40,
            HeightRequest = 40,
            CornerRadius = 8,
            Padding = new Thickness(0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.CardText1);
        menu.Clicked += async (_, _) => await ShowGameMenuAsync(game);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };
        grid.Add(new VerticalStackLayout { Spacing = 4, Children = { title, details } }, 0, 0);
        grid.Add(menu, 1, 0);

        frame.Content = grid;
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await Navigation.PushAsync(new CustomGamePlayPage(_auth, _customGames, game.Id));
        frame.GestureRecognizers.Add(tap);
        return frame;
    }

    private async Task ShowGameMenuAsync(CustomGame game)
    {
        string action = await DisplayActionSheet(game.Name, "Cancel", null, "Rename", "Edit Buttons", "Edit End Condition", "View All Scores", "Delete");
        if (action == "Rename" || action == "Edit Buttons" || action == "Edit End Condition")
        {
            await Navigation.PushAsync(new CustomGameEditPage(_auth, _customGames, game.Id));
        }
        else if (action == "View All Scores")
        {
            await Navigation.PushAsync(new CustomGameTopScoresPage(_auth, _customGames, game.Id));
        }
        else if (action == "Delete")
        {
            bool confirm = await DisplayAlert("Delete Custom Game", $"Delete '{game.Name}' and all scores?", "Delete", "Cancel");
            if (confirm)
            {
                await _customGames.DeleteGameAsync(game.Id);
                await LoadAsync();
            }
        }
    }

    internal static string GetEndSummary(CustomGame game)
    {
        return game.EndType switch
        {
            0 => $"Timer: {TimeSpan.FromSeconds(game.EndValueSeconds ?? 0):mm\\:ss}",
            1 => game.EndValueDate.HasValue ? $"Until {game.EndValueDate.Value.ToLocalTime():g}" : "Date",
            2 => $"Target: {game.EndValueAmount ?? 0}",
            _ => "Custom"
        };
    }
}

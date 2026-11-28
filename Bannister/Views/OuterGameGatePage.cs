using Bannister.Helpers;
using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public sealed class OuterGameGatePage(
    AuthService auth,
    GameService games,
    ActivityService activities,
    OuterGameService outerGames,
    OuterGameRequirements requirements,
    Func<Page> openFeature) : ModeThemedPage
{
    private bool _busy;

    public static async Task OpenAsync(
        INavigation navigation, AuthService auth, GameService games, ActivityService activities,
        OuterGameRequirements requirements, Func<Page> openFeature)
    {
        var services = Application.Current?.Handler?.MauiContext?.Services
            ?? throw new InvalidOperationException("Outer game services are unavailable.");
        var outerGames = services.GetRequiredService<OuterGameService>();
        var status = await outerGames.CheckAsync(auth.CurrentUsername, requirements, createGame: true);
        if (status.IsReady)
            await navigation.PushAsync(openFeature());
        else
            await navigation.PushAsync(new OuterGameGatePage(auth, games, activities, outerGames, requirements, openFeature)
            {
                Title = requirements.GameName
            });
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RunAsync(RefreshAsync);
    }

    private async Task RunAsync(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await DisplayAlert(requirements.GameName, ex.Message, "OK");
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task RefreshAsync()
    {
        var status = await outerGames.CheckAsync(auth.CurrentUsername, requirements, createGame: true);
        if (Navigation.NavigationStack.LastOrDefault() != this)
            return;
        if (status.IsReady)
        {
            await Navigation.PushAsync(openFeature());
            Navigation.RemovePage(this);
            return;
        }

        var list = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        if (status.Games.Count > 1)
        {
            list.Children.Add(Text(status.GameProblem(requirements.GameName)!, ThemeKey.PageText1, bold: true));
            foreach (var game in status.Games)
                list.Children.Add(GameCard(game));
        }
        else
        {
            list.Children.Add(Text($"Before entering {requirements.GameName} you need these activities",
                ThemeKey.PageText1, bold: true));
            list.Children.Add(Text("Keep these exact names. If you change a name, the page will ask for it again.",
                ThemeKey.PageText2));
            foreach (var activity in status.Activities.Where(activity => activity.Problem != null))
            {
                if (activity.Matches.Count == 0)
                {
                    var card = Card(Text(activity.Requirement.Name, ThemeKey.CardText1, bold: true),
                        Text(activity.Requirement.Description, ThemeKey.CardText2),
                        ActionButton("Create activity", () => CreateActivityAsync(activity.Requirement), ThemeKey.CardText1));
                    list.Children.Add(card);
                }
                else
                {
                    list.Children.Add(Text(activity.Problem!, ThemeKey.PageText1, bold: true));
                    foreach (var match in activity.Matches)
                        list.Children.Add(ActivityCard(match));
                }
            }
        }
        list.Children.Add(ActionButton("Check again", RefreshAsync, ThemeKey.PageText1));
        Content = new ScrollView { Content = list };
    }

    private async Task CreateActivityAsync(OuterGameActivity requirement)
    {
        var status = await outerGames.CheckAsync(auth.CurrentUsername, requirements, createGame: true);
        if (status.Games.Count == 1 &&
            status.Activities.Single(activity => activity.Requirement == requirement).Matches.Count == 0)
        {
            var created = await ActivityCreationPage.CreateActivityModalAsync(
                Navigation, auth, activities, games, status.Games[0].GameId,
                prefillName: requirement.Name, prefillLevel: (requirement.SuggestedExp / 2).ToString(),
                isNegative: requirement.SuggestedExp < 0, noHabitTarget: true,
                prefillDescription: requirement.Description);
            if (created != null && !OuterGameService.NamesMatch(created.Name, requirement.Name))
                await DisplayAlert("Required name still missing",
                    $"'{created.Name}' was saved. It won't count because the required name is '{requirement.Name}'.", "OK");
        }
        await RefreshAsync();
    }

    private View GameCard(Game game) => Card(
        Text($"{game.DisplayName} ? #{game.Id} ? {game.GameId}", ThemeKey.CardText1, bold: true),
        ActionButton("Open game", () => Shell.Current.GoToAsync(
            $"activitygrid?gameId={Uri.EscapeDataString(game.GameId)}"), ThemeKey.CardText1),
        ActionButton("Rename", async () =>
        {
            var name = await AskNameAsync(game.DisplayName);
            if (name != null)
            {
                game.DisplayName = name;
                await games.UpdateGameAsync(game);
            }
            await RefreshAsync();
        }, ThemeKey.CardText1),
        ActionButton("Remove", async () =>
        {
            if (await DisplayAlert("Remove game?", $"Remove '{game.DisplayName}' (#{game.Id}) from active games?", "Remove", "Cancel"))
                await games.RemoveGameAsync(game.Id);
            await RefreshAsync();
        }, ThemeKey.CardText1));

    private View ActivityCard(Activity activity) => Card(
        Text($"{activity.Name} ? #{activity.Id} ? {activity.Category} ? {activity.TimesCompleted} completions",
            ThemeKey.CardText1, bold: true),
        ActionButton("Open", () => Navigation.PushAsync(
            new EditActivityPage(auth, activities, activity.Game, activity)), ThemeKey.CardText1),
        ActionButton("Rename", async () =>
        {
            var name = await AskNameAsync(activity.Name);
            if (name != null && !OuterGameService.NamesMatch(name, activity.Name))
            {
                var existing = await activities.GetActivitiesAsync(auth.CurrentUsername, activity.Game);
                if (existing.Any(other => other.Id != activity.Id && OuterGameService.NamesMatch(other.Name, name)))
                    await DisplayAlert("Duplicate activity", $"'{name}' already exists in this game.", "OK");
                else
                {
                    activity.Name = name;
                    await activities.UpdateActivityAsync(activity);
                }
            }
            await RefreshAsync();
        }, ThemeKey.CardText1),
        ActionButton("Delete", async () =>
        {
            if (await DisplayAlert("Delete activity?", $"Delete '{activity.Name}' (#{activity.Id})?", "Delete", "Cancel"))
                await activities.DeleteActivityAsync(activity.Id);
            await RefreshAsync();
        }, ThemeKey.CardText1));

    private async Task<string?> AskNameAsync(string oldName)
    {
        var name = (await DisplayPromptAsync("Rename", "New name", "Save", "Cancel", initialValue: oldName))
            ?.ReplaceLineEndings("\n").Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private Button ActionButton(string title, Func<Task> action, ThemeKey textColor)
    {
        var button = (new Button { Text = title })
            .ThemeColor(Button.TextColorProperty, textColor)
            .ThemeColor(Button.BackgroundColorProperty, ThemeKey.CardBackground);
        button.Clicked += async (_, _) => await RunAsync(action);
        return button;
    }

    private static Label Text(string value, ThemeKey color, bool bold = false) =>
        (new Label { Text = value, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None })
            .ThemeColor(Label.TextColorProperty, color);

    private static Frame Card(params View[] views)
    {
        var content = new VerticalStackLayout { Spacing = 6 };
        foreach (var view in views)
            content.Children.Add(view);
        return (new Frame { Content = content, Padding = 12, HasShadow = false })
            .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
    }
}

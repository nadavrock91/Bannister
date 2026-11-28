using Bannister.Models;

namespace Bannister.Services;

public sealed record OuterGameActivity(string ActionKey, string Name, int SuggestedExp, string Description);

public sealed record OuterGameRequirements(string Key, string GameName, IReadOnlyList<OuterGameActivity> Activities);

public sealed record OuterGameActivityStatus(OuterGameActivity Requirement, IReadOnlyList<Activity> Matches)
{
    public string? Problem => Matches.Count switch
    {
        0 => $"The required activity '{Requirement.Name}' is missing.",
        1 => null,
        _ => $"There are {Matches.Count} activities named '{Requirement.Name}' ? keep one and rename or delete the others."
    };
}

public sealed record OuterGameStatus(IReadOnlyList<Game> Games, IReadOnlyList<OuterGameActivityStatus> Activities)
{
    public bool IsReady => Games.Count == 1 && Activities.All(activity => activity.Problem == null);

    public string? GameProblem(string name) => Games.Count switch
    {
        0 => $"The required game '{name}' is missing.",
        1 => null,
        _ => $"There are {Games.Count} games named '{name}' ? keep one and rename or remove the others."
    };
}

public sealed class OuterGameService(GameService games, ActivityService activities, ExpAwarder awarder)
{
    private readonly SemaphoreSlim _gameCreationLock = new(1);

    public static bool NamesMatch(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    public async Task<OuterGameStatus> CheckAsync(string username, OuterGameRequirements requirements, bool createGame = false)
    {
        await _gameCreationLock.WaitAsync();
        try
        {
            var matches = (await games.GetGamesAsync(username))
                .FindAll(game => NamesMatch(game.DisplayName, requirements.GameName));
            if (matches.Count == 0 && createGame)
                matches.Add(await games.CreateGameAsync(username, requirements.GameName));

            if (matches.Count != 1)
                return new(matches, []);

            var gameActivities = await activities.GetActivitiesAsync(username, matches[0].GameId);
            var statuses = requirements.Activities.Select(requirement => new OuterGameActivityStatus(
                requirement, gameActivities.FindAll(activity => NamesMatch(activity.Name, requirement.Name))));
            return new(matches, statuses.ToList());
        }
        finally
        {
            _gameCreationLock.Release();
        }
    }

    public async Task<ExpAwardResult> AwardAsync(
        string username, OuterGameRequirements requirements, string actionKey,
        Func<SQLite.SQLiteConnection, bool>? markAward = null)
    {
        try
        {
            var status = await CheckAsync(username, requirements);
            string? problem = status.GameProblem(requirements.GameName);
            if (problem != null)
                return Failed(problem);

            var activity = status.Activities.SingleOrDefault(activity => activity.Requirement.ActionKey == actionKey);
            if (activity == null)
                return Failed($"'{actionKey}' is not configured for '{requirements.Key}'.");
            if (activity.Problem is string activityProblem)
                return Failed(activityProblem);

            return await awarder.AwardAsync(username, activity.Matches[0], markAward: markAward);
        }
        catch (Exception ex)
        {
            return Failed(ex.Message);
        }
    }

    private static ExpAwardResult Failed(string reason) => new(0, 0, 0, false, "", reason);
}

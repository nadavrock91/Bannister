using System.Text.Json;

namespace Bannister.Services;

public sealed class LastCalculatedLocation
{
    public string GameId { get; set; } = "";
    public string Category { get; set; } = "";
    public int ActivityId { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
}

public sealed class LastCalculatedLocationService
{
    private const string SettingKey = "last_calculated_exp_location";
    private readonly AppSettingsService _settings;

    public LastCalculatedLocationService(AppSettingsService settings) => _settings = settings;

    public Task SaveAsync(string username, string gameId, string category, int activityId) =>
        _settings.SetAsync(username, SettingKey, JsonSerializer.Serialize(new LastCalculatedLocation
        {
            GameId = gameId,
            Category = category,
            ActivityId = activityId,
            CalculatedAtUtc = DateTime.UtcNow
        }));

    public async Task<LastCalculatedLocation?> GetAsync(string username)
    {
        string? json = await _settings.GetAsync(username, SettingKey);
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<LastCalculatedLocation>(json); }
        catch { return null; }
    }
}

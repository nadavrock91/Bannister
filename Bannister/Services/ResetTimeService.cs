namespace Bannister.Services;

public class ResetTimeService
{
    private const string KeyPrefix = "daily_reset_hour_";

    public int GetResetHour(string username)
    {
        int hour = Preferences.Default.Get(
            KeyPrefix + username, 0);
        return Math.Clamp(hour, 0, 23);
    }

    public void SetResetHour(string username, int hour)
    {
        Preferences.Default.Set(
            KeyPrefix + username, Math.Clamp(hour, 0, 23));
    }

    public DateTime GetNextResetDateTime(string username)
    {
        var now = DateTime.Now;
        var reset = now.Date.AddHours(GetResetHour(username));
        return reset > now ? reset : reset.AddDays(1);
    }

    public string GetTodayResetKey(string username)
    {
        var now = DateTime.Now;
        var date = now.Hour < GetResetHour(username)
            ? now.Date.AddDays(-1)
            : now.Date;
        return date.ToString("yyyy-MM-dd");
    }
}

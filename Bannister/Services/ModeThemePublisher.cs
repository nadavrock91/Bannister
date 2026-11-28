namespace Bannister.Services;

/// <summary>Loads and publishes the current user's palette once, independently of page lifetimes.</summary>
public sealed class ModeThemePublisher : IDisposable
{
    private readonly AuthService _auth;
    private readonly PrivacyModeService _privacy;
    private readonly AppSettingsService _settings;
    private readonly object _gate = new();
    private long _revision;
    private bool _disposed;
    private PaletteRequest? _applied;
    private PaletteRequest? _loading;
    private Task? _loadTask;
    private sealed record PaletteRequest(Bannister.Models.User Session, string Username, ActivityDisplayMode Mode, long Revision);

    public ModeThemePublisher(AuthService auth, PrivacyModeService privacy, AppSettingsService settings)
    {
        _auth = auth;
        _privacy = privacy;
        _settings = settings;
        PrivacyModeService.ModeChanged += OnModeChanged;
    }

    public Task ApplyCurrentAsync()
    {
        lock (_gate)
        {
            if (_disposed || _auth.CurrentUser is not { } session) return Task.CompletedTask;
            var request = new PaletteRequest(session, _auth.CurrentUsername, _privacy.GetDisplayMode(_auth.CurrentUsername), _revision);
            if (_applied == request) return Task.CompletedTask;
            if (_loading == request && _loadTask != null) return _loadTask;
            _loading = request;
            return _loadTask = LoadAndPublishAsync(request);
        }
    }

    private void OnModeChanged(object? sender, DisplayModeChangedEventArgs args)
    {
        if (!string.Equals(args.Username, _auth.CurrentUsername, StringComparison.OrdinalIgnoreCase)) return;
        lock (_gate) _revision++;
        _ = ApplyCurrentAsync();
    }

    private async Task LoadAndPublishAsync(PaletteRequest request)
    {
        try
        {
            var colors = await ModeTheme.GetAsync(request.Mode, request.Username, _settings);
            await MainThread.InvokeOnMainThreadAsync(() => Publish(request, colors));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[THEME] Could not load colors: {ex}");
            // A transient settings/database failure must not prevent the next appearance retrying.
            await MainThread.InvokeOnMainThreadAsync(() => Publish(request, ModeTheme.Get(request.Mode), remember: false));
        }
        finally
        {
            lock (_gate) if (_loading == request) { _loading = null; _loadTask = null; }
        }
    }

    private void Publish(PaletteRequest request, ModeThemeColors colors, bool remember = true)
    {
        lock (_gate)
        {
            if (_disposed || request.Revision != _revision || !ReferenceEquals(request.Session, _auth.CurrentUser) ||
                request.Username != _auth.CurrentUsername || request.Mode != _privacy.GetDisplayMode(request.Username)) return;
            var resources = Application.Current?.Resources;
            if (resources == null) return;
            var values = colors.Tokens.ToDictionary(token => "Mode" + token.Key, token => token.Value);
            values["ModeForeground"] = colors.Foreground;
            values["ModeMutedForeground"] = colors.MutedForeground;
            foreach (var (key, color) in values)
                if (!resources.TryGetValue(key, out var previous) || !Equals(previous, color)) resources[key] = color;
            if (remember) _applied = request;
        }
    }

    public void Dispose()
    {
        lock (_gate) { _disposed = true; _revision++; }
        PrivacyModeService.ModeChanged -= OnModeChanged;
    }
}

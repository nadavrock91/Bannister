using Bannister.Helpers;
using Bannister.Services;

namespace Bannister.Views;

public enum ExpBarPresentation
{
    CenteredCard,
    LeftCard,
    Page
}

public sealed class ExpBarLevelChangedEventArgs(int previousLevel, int currentLevel) : EventArgs
{
    public int PreviousLevel { get; } = previousLevel;
    public int CurrentLevel { get; } = currentLevel;
}

public sealed class ExpBarView : ContentView
{
    private readonly ExpService _exp;
    private readonly ExpAwarder _awarder;
    private readonly string _username;
    private readonly Label _levelLabel;
    private readonly Label _progressLabel;
    private readonly Label _totalLabel;
    private readonly ProgressBar _progressBar;
    private readonly ThemeKey _levelSurfaceText;
    private readonly SemaphoreSlim _animationLock = new(1, 1);
    private string _gameId;
    private Page? _page;
    private bool _subscribed;
    private bool _hasRenderedTotal;
    private int _renderedTotal;

    public event Func<ExpBarLevelChangedEventArgs, Task>? LevelChanged;

    public ExpBarView(
        string username,
        string gameId,
        ExpBarPresentation presentation = ExpBarPresentation.CenteredCard)
    {
        _username = username;
        _gameId = gameId;
        var services = Application.Current?.Handler?.MauiContext?.Services
            ?? throw new InvalidOperationException("Application services are unavailable.");
        _exp = services.GetRequiredService<ExpService>();
        _awarder = services.GetRequiredService<ExpAwarder>();

        bool centered = presentation == ExpBarPresentation.CenteredCard;
        bool pageSurface = presentation == ExpBarPresentation.Page;
        var primaryText = pageSurface ? ThemeKey.PageText1 : ThemeKey.CardText1;
        var secondaryText = pageSurface ? ThemeKey.PageText2 : ThemeKey.CardText2;
        var tertiaryText = pageSurface ? ThemeKey.PageText3 : ThemeKey.CardText3;
        var horizontal = centered ? LayoutOptions.Center : LayoutOptions.Start;
        _levelSurfaceText = primaryText;

        _levelLabel = (new Label
        {
            Text = "Level 1",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = horizontal
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText, primaryText);

        _progressBar = (new ProgressBar
        {
            Progress = 0,
            HeightRequest = 16,
            Margin = centered ? new Thickness(0, 2) : default
        }).ThemeColor(ProgressBar.ProgressColorProperty, ThemeKey.SuccessButtonBackground)
          .ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator);

        _progressLabel = (new Label
        {
            Text = "0 / 100 EXP",
            FontSize = 10,
            HorizontalOptions = horizontal
        }).ThemeColor(Label.TextColorProperty, secondaryText);

        _totalLabel = (new Label
        {
            Text = "Total EXP: 0",
            FontSize = 11,
            HorizontalOptions = horizontal
        }).ThemeColor(Label.TextColorProperty, tertiaryText);

        Content = new VerticalStackLayout
        {
            Spacing = centered ? 6 : 4,
            Children = { _levelLabel, _progressBar, _progressLabel, _totalLabel }
        };

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public async Task SetGameAsync(string gameId)
    {
        _gameId = gameId;
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            Render(0);
            return;
        }
        Render(await _exp.GetTotalExpAsync(_username, _gameId));
    }

    public async Task RefreshAnimatedAsync()
    {
        if (string.IsNullOrWhiteSpace(_gameId))
        {
            Render(0);
            return;
        }

        int currentTotal = await _exp.GetTotalExpAsync(_username, _gameId);
        await _animationLock.WaitAsync();
        try
        {
            if (!_hasRenderedTotal || currentTotal == _renderedTotal)
            {
                Render(currentTotal);
                return;
            }
            int previousTotal = _renderedTotal;
            var previous = ExpEngine.GetProgress(previousTotal);
            var current = ExpEngine.GetProgress(currentTotal);
            await AnimateOnDispatcherAsync(new ExpAwardedEvent(
                _username, _gameId, previousTotal, currentTotal, previous.level, current.level));
        }
        finally
        {
            _animationLock.Release();
        }
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        AttachPage();
        Subscribe();
        _ = RefreshAnimatedAsync();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        Unsubscribe();
        DetachPage();
    }

    private void AttachPage()
    {
        if (_page != null) return;
        Element? current = this;
        while (current != null && current is not Page)
            current = current.Parent;
        if (current is not Page page) return;
        _page = page;
        page.Appearing += OnPageAppearing;
        page.Disappearing += OnPageDisappearing;
    }

    private void DetachPage()
    {
        if (_page == null) return;
        _page.Appearing -= OnPageAppearing;
        _page.Disappearing -= OnPageDisappearing;
        _page = null;
    }

    private void OnPageAppearing(object? sender, EventArgs e)
    {
        Subscribe();
        _ = RefreshAnimatedAsync();
    }

    private void OnPageDisappearing(object? sender, EventArgs e) => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed) return;
        _awarder.AwardSucceeded += OnAwardSucceededAsync;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _awarder.AwardSucceeded -= OnAwardSucceededAsync;
        _subscribed = false;
    }

    private async Task OnAwardSucceededAsync(ExpAwardedEvent change)
    {
        if (!_subscribed ||
            !string.Equals(change.Username, _username, StringComparison.Ordinal) ||
            !string.Equals(change.GameId, _gameId, StringComparison.OrdinalIgnoreCase))
            return;

        await _animationLock.WaitAsync();
        try
        {
            if (!_subscribed) return;
            await AnimateOnDispatcherAsync(change);
        }
        finally
        {
            _animationLock.Release();
        }
    }

    private Task AnimateOnDispatcherAsync(ExpAwardedEvent change) =>
        Dispatcher.IsDispatchRequired
            ? Dispatcher.DispatchAsync(() => AnimateAsync(change))
            : AnimateAsync(change);

    private async Task AnimateAsync(ExpAwardedEvent change)
    {
        if (change.TotalExpAfter < 0)
        {
            Render(change.TotalExpAfter);
            return;
        }

        var before = ExpEngine.GetProgress(change.TotalExpBefore);
        var after = ExpEngine.GetProgress(change.TotalExpAfter);
        double fromProgress = before.expNeededThisLevel > 0
            ? (double)before.expIntoLevel / before.expNeededThisLevel
            : 0;
        double toProgress = after.expNeededThisLevel > 0
            ? (double)after.expIntoLevel / after.expNeededThisLevel
            : 0;

        RenderText(change.TotalExpAfter, after.level, after.expIntoLevel, after.expNeededThisLevel);
        _progressBar.Progress = fromProgress;
        _progressBar.ThemeColor(ProgressBar.ProgressColorProperty, ThemeKey.SuccessButtonBackground);
        try
        {
            if (change.LevelAfter > change.LevelBefore)
            {
                await _progressBar.ProgressTo(1.0, 500, Easing.CubicOut);
                await SoundHelper.PlayExpGainSound();
                await SoundHelper.PlayLevelUpSound();
                await Task.Delay(200);
                _progressBar.Progress = 0;
                await _progressBar.ProgressTo(toProgress, 300, Easing.CubicOut);
                await RaiseLevelChangedAsync(new ExpBarLevelChangedEventArgs(
                    change.LevelBefore, change.LevelAfter));
            }
            else
            {
                await _progressBar.ProgressTo(toProgress, 500, Easing.CubicOut);
                await SoundHelper.PlayExpGainSound();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EXP bar animation error: {ex.Message}");
        }
        finally
        {
            _renderedTotal = change.TotalExpAfter;
            _hasRenderedTotal = true;
        }
    }

    private async Task RaiseLevelChangedAsync(ExpBarLevelChangedEventArgs change)
    {
        var handlers = LevelChanged?.GetInvocationList()
            .Cast<Func<ExpBarLevelChangedEventArgs, Task>>()
            .ToList();
        if (handlers == null) return;
        foreach (var handler in handlers)
        {
            try
            {
                await handler(change);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"EXP level-change listener failed: {ex.Message}");
            }
        }
    }

    private void Render(int totalExp)
    {
        _renderedTotal = totalExp;
        _hasRenderedTotal = true;
        if (totalExp < 0)
        {
            _levelLabel.Text = "Negative EXP";
            _levelLabel.ThemeColor(Label.TextColorProperty, ThemeKey.ErrorText, _levelSurfaceText);
            _progressLabel.Text = $"{totalExp} EXP (below zero)";
            _totalLabel.Text = $"Total EXP: {totalExp:N0}";
            _progressBar.Progress = 0;
            _progressBar.ThemeColor(ProgressBar.ProgressColorProperty, ThemeKey.ErrorText);
            return;
        }

        var progress = ExpEngine.GetProgress(totalExp);
        RenderText(totalExp, progress.level, progress.expIntoLevel, progress.expNeededThisLevel);
        _progressBar.Progress = progress.expNeededThisLevel > 0
            ? (double)progress.expIntoLevel / progress.expNeededThisLevel
            : 0;
        _progressBar.ThemeColor(ProgressBar.ProgressColorProperty, ThemeKey.SuccessButtonBackground);
    }

    private void RenderText(int totalExp, int level, int expInto, int expNeeded)
    {
        _levelLabel.Text = $"Level {level}";
        _levelLabel.ThemeColor(Label.TextColorProperty, ThemeKey.HighlightIndigoText, _levelSurfaceText);
        _progressLabel.Text = $"{expInto} / {expNeeded} EXP";
        _totalLabel.Text = $"Total EXP: {totalExp:N0}";
    }
}

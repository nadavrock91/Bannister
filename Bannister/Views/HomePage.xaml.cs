using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bannister.Services;
using Bannister.Models;
using ConversationPractice.Services;
using ConversationPractice.Views;

namespace Bannister.Views;

public partial class HomePage : ContentPage
{
    private readonly AuthService _auth;
    private readonly GameService _games;
    private readonly DragonService _dragons;
    private readonly BackupService _backup;
    private readonly AttemptService _attempts;
    private readonly StreakService _streaks;
    private readonly DatabaseService _db;
    private readonly ExpService _exp;
    private readonly CountdownService _countdowns;
    private readonly LearningService _learning;
    private bool _introChecked = false;

    public HomePage(AuthService auth, GameService games, DragonService dragons,
        BackupService backup, AttemptService attempts, StreakService streaks, DatabaseService db, ExpService exp,
        CountdownService countdowns, LearningService learning)
    {
        InitializeComponent();
        _auth = auth;
        _games = games;
        _dragons = dragons;
        _backup = backup;
        _attempts = attempts;
        _streaks = streaks;
        _db = db;
        _exp = exp;
        _countdowns = countdowns;
        _learning = learning;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Auto-backup on login
        await _backup.AutoBackupAsync("login");

        loadingOverlay.IsVisible = false;

        lblWelcome.Text = $"Welcome, {_auth.CurrentUsername}";
        await LoadDataAsync();

        if (!_introChecked)
        {
            _introChecked = true;

            if (!await HasSeenIntroAsync())
            {
                bool goToDiet = await DisplayAlert(
                    "Welcome to Bannister",
                    "This is your Home screen.\n" +
                    "Later you'll choose between different EXP games like Diet, Exercise, and more.\n\n" +
                    "A Diet game has been created for you.\n" +
                    "Want to go in now?",
                    "Go to Diet",
                    "Skip");

                await MarkIntroSeenAsync();

                if (goToDiet)
                {
                    await Shell.Current.GoToAsync("activitygrid?gameId=diet");
                }
            }
        }

        // Check for expired activities
        await CheckExpiredActivitiesAsync();
    }

    private async Task<bool> HasSeenIntroAsync()
    {
        try
        {
            var seen = await SecureStorage.GetAsync($"intro_seen_{_auth.CurrentUsername}");
            return seen == "true";
        }
        catch
        {
            return false;
        }
    }

    private async Task MarkIntroSeenAsync()
    {
        try
        {
            await SecureStorage.SetAsync($"intro_seen_{_auth.CurrentUsername}", "true");
        }
        catch { }
    }

    private async Task LoadDataAsync()
    {
        var games = await _games.GetGamesAsync(_auth.CurrentUsername);

        if (games.Count == 0)
        {
            // Create default games
            await _games.CreateGameAsync(_auth.CurrentUsername, "Conversation Practice");
            await _games.CreateGameAsync(_auth.CurrentUsername, "Diet");
            games = await _games.GetGamesAsync(_auth.CurrentUsername);
        }

        // Update games button with count
        btnGames.Text = $"🎮 Games ({games.Count})";

        // Get all dragons count
        var activeDragons = await _dragons.GetActiveDragonsAsync(_auth.CurrentUsername);
        var slainDragons = await _dragons.GetSlainDragonsAsync(_auth.CurrentUsername);
        var irrelevantDragons = await _dragons.GetIrrelevantDragonsAsync(_auth.CurrentUsername);
        int totalDragons = activeDragons.Count + slainDragons.Count + irrelevantDragons.Count;
        
        btnDragons.Text = $"🐉 Dragons ({activeDragons.Count} active)";

        // Count active streaks across all games
        int activeStreaksCount = 0;
        foreach (var game in games)
        {
            var streaks = await _streaks.GetActiveStreaksAsync(_auth.CurrentUsername, game.GameId);
            activeStreaksCount += streaks.Count;
        }
        btnStreaks.Text = $"🔥 Streaks ({activeStreaksCount} active)";
        
        // Count active countdowns
        var activeCountdowns = await _countdowns.GetActiveCountdownsAsync(_auth.CurrentUsername);
        var expiredCountdowns = await _countdowns.GetExpiredCountdownsAsync(_auth.CurrentUsername);
        if (expiredCountdowns.Count > 0)
        {
            btnCountdowns.Text = $"⏳ Countdowns ({activeCountdowns.Count} active, {expiredCountdowns.Count} need resolution!)";
            btnCountdowns.BackgroundColor = Color.FromArgb("#FFF3E0");
            btnCountdowns.TextColor = Color.FromArgb("#E65100");
        }
        else
        {
            btnCountdowns.Text = $"⏳ Countdowns ({activeCountdowns.Count} active)";
        }

        // Update learning button with stats
        var (totalBooks, completedBooks, totalVideos, completedVideos) = await _learning.GetStatsAsync(_auth.CurrentUsername);
        int pendingBooks = totalBooks - completedBooks;
        int pendingVideos = totalVideos - completedVideos;
        
        if (totalBooks == 0 && totalVideos == 0)
        {
            btnLearning.Text = "📚 Learning (Books & Videos)";
        }
        else
        {
            btnLearning.Text = $"📚 Learning ({pendingBooks} books, {pendingVideos} videos to go)";
        }
    }

    private async Task CheckExpiredActivitiesAsync()
    {
        try
        {
            var activityService = Application.Current?.Handler?.MauiContext?.Services
                .GetService<ActivityService>();

            if (activityService == null) return;

            var expired = await activityService.GetExpiredActivitiesAsync(_auth.CurrentUsername);

            if (expired.Count > 0)
            {
                bool handle = await DisplayAlert(
                    "Expired Activities",
                    $"You have {expired.Count} expired activity(ies). Would you like to review them?",
                    "Yes",
                    "Later");

                if (handle)
                {
                    await Shell.Current.GoToAsync("expiredactivities");
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error checking expired activities: {ex.Message}");
        }
    }

    private async void OnGamesClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("gameslist");
    }

    private async void OnNewHabitsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("habits");
    }

    private async void OnChartsClicked(object sender, EventArgs e)
    {
        var page = new ChartsHubPage(_auth, _games, _exp, _db);
        await Navigation.PushAsync(page);
    }

    private async void OnLearningClicked(object sender, EventArgs e)
    {
        var page = new LearningPage(_auth, _learning);
        await Navigation.PushAsync(page);
    }

    private async void OnDragonsClicked(object sender, EventArgs e)
    {
        var page = new DragonsHubPage(_auth, _dragons, _attempts);
        await Navigation.PushAsync(page);
    }

    private async void OnStreaksClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("streakdashboard");
    }

    private async void OnCountdownsClicked(object sender, EventArgs e)
    {
        var page = new CountdownsHomePage(_auth, _countdowns);
        await Navigation.PushAsync(page);
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        await _backup.AutoBackupAsync("logout");

        _auth.Logout();

        // RESET STACK — ANDROID REQUIRED
        await Shell.Current.GoToAsync("//login");
    }
}

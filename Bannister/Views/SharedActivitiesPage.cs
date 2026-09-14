using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SharedActivitiesPage : ContentPage
{
    private readonly SharedActivityService _sharedService;
    private readonly ActivityService _activityService;
    private readonly GameService _gameService;
    private readonly AuthService _auth;
    private readonly SyncService _syncService;
    private readonly ExpService _expService;
    private readonly DatabaseService _db;
    private VerticalStackLayout _linksContainer = null!;
    private Grid _loadingOverlay = null!;
    private Label _loadingLabel = null!;

    public SharedActivitiesPage(SharedActivityService sharedService,
        ActivityService activityService, GameService gameService,
        AuthService auth, SyncService syncService,
        ExpService expService, DatabaseService db)
    {
        _sharedService = sharedService;
        _activityService = activityService;
        _gameService = gameService;
        _auth = auth;
        _syncService = syncService;
        _expService = expService;
        _db = db;
        Title = "Shared Activities";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshLinksAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 16 };
        stack.Children.Add(new Label
        {
            Text = " Shared Activities", FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Share activities between accounts. Both users must use the same link code and shared password.",
            FontSize = 13, TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var btnRow = new HorizontalStackLayout { Spacing = 8 };
        var createBtn = new Button
        {
            Text = "➕ Create Link", BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White, CornerRadius = 8, FontSize = 13,
            HeightRequest = 40, Padding = new Thickness(12, 0)
        };
        createBtn.Clicked += async (_, _) => await CreateShareLinkAsync();
        btnRow.Children.Add(createBtn);
        var joinBtn = new Button
        {
            Text = " Join with Code", BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White, CornerRadius = 8, FontSize = 13,
            HeightRequest = 40, Padding = new Thickness(12, 0)
        };
        joinBtn.Clicked += async (_, _) => await JoinShareLinkAsync();
        btnRow.Children.Add(joinBtn);
        stack.Children.Add(btnRow);
        _linksContainer = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(_linksContainer);
        var rootGrid = new Grid();
        rootGrid.Children.Add(new ScrollView { Content = stack });

        _loadingLabel = new Label
        {
            Text = "Pulling activities...",
            FontSize = 16,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        _loadingOverlay = new Grid
        {
            BackgroundColor = Color.FromArgb("#CC000000"),
            IsVisible = false,
            InputTransparent = false,
            Children = { _loadingLabel }
        };
        rootGrid.Children.Add(_loadingOverlay);
        Content = rootGrid;
    }

    private async Task RefreshLinksAsync()
    {
        var links = await _sharedService.GetLinksAsync(_auth.CurrentUsername);
        _linksContainer.Children.Clear();
        if (links.Count == 0)
        {
            _linksContainer.Children.Add(new Label
            {
                Text = "No active share links.", FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }
        foreach (var link in links)
            _linksContainer.Children.Add(BuildLinkCard(link));
    }

    private View BuildLinkCard(SharedActivityLink link)
    {
        var inner = new VerticalStackLayout { Spacing = 6 };
        inner.Children.Add(new Label
        {
            Text = $"Partner: {link.PartnerName}", FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        inner.Children.Add(new Label
        {
            Text = $"Code: {link.LinkCode}", FontSize = 13,
            TextColor = Color.FromArgb("#5B63EE"),
            FontAttributes = FontAttributes.Bold
        });
        var acts = _sharedService.GetSharedActivities(link);
        inner.Children.Add(new Label
        {
            Text = $"{acts.Count} activities shared", FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });
        if (link.LastUpdatedAt.HasValue)
            inner.Children.Add(new Label
            {
                Text = $"Last push: {link.LastUpdatedBy} on " +
                    link.LastUpdatedAt.Value.ToLocalTime().ToString("dd MMM yyyy HH:mm"),
                FontSize = 11, TextColor = Color.FromArgb("#888"),
                FontAttributes = FontAttributes.Italic
            });

        var actionRow = new HorizontalStackLayout { Spacing = 8 };
        var capturedLink = link;
        var pushBtn = new Button
        {
            Text = "⬆ Push", BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White, CornerRadius = 6, FontSize = 12,
            HeightRequest = 34, Padding = new Thickness(10, 0)
        };
        pushBtn.Clicked += async (_, _) => await PushLinkAsync(capturedLink);
        actionRow.Children.Add(pushBtn);
        var pullBtn = new Button
        {
            Text = "⬇ Pull", BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White, CornerRadius = 6, FontSize = 12,
            HeightRequest = 34, Padding = new Thickness(10, 0)
        };
        pullBtn.Clicked += async (_, _) => await PullLinkAsync(capturedLink);
        actionRow.Children.Add(pullBtn);
        var delBtn = new Button
        {
            Text = "✕", BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"), CornerRadius = 6,
            FontSize = 13, HeightRequest = 34, WidthRequest = 34, Padding = 0
        };
        delBtn.Clicked += async (_, _) =>
        {
            bool confirm = await DisplayAlert("Remove Link",
                $"Remove share link with {capturedLink.PartnerName}?",
                "Remove", "Cancel");
            if (!confirm) return;
            await _sharedService.DeactivateLinkAsync(capturedLink.Id);
            await RefreshLinksAsync();
        };
        actionRow.Children.Add(delBtn);
        inner.Children.Add(actionRow);
        return new Frame
        {
            BackgroundColor = Colors.White, Padding = 14, CornerRadius = 10,
            HasShadow = true, Content = inner
        };
    }

    private async Task<string?> AskPasswordAsync(
        SharedActivityLink link)
    {
        var tcs = new TaskCompletionSource<string?>();

        var entry = new Entry
        {
            IsPassword = true,
            Placeholder = "Shared password",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222"),
            FontSize = 14,
            Margin = new Thickness(16, 8)
        };

        var okBtn = new Button
        {
            Text = "OK",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            Margin = new Thickness(16, 0, 8, 0)
        };

        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            Margin = new Thickness(8, 0, 16, 0)
        };

        var btnRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            Margin = new Thickness(0, 8, 0, 0)
        };
        btnRow.Add(okBtn, 0, 0);
        btnRow.Add(cancelBtn, 1, 0);

        var pwdPage = new ContentPage
        {
            Title = "Shared Password",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Padding = new Thickness(0, 20, 0, 20),
                Children =
                {
                    new Label
                    {
                        Text = $"Enter the shared password for " +
                               $"link {link.LinkCode}:",
                        FontSize = 14,
                        TextColor = Color.FromArgb("#444"),
                        Margin = new Thickness(16, 0),
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    entry,
                    btnRow
                }
            }
        };

        okBtn.Clicked += async (_, _) =>
        {
            var pwd = entry.Text ?? "";
            await Navigation.PopModalAsync();
            tcs.TrySetResult(string.IsNullOrWhiteSpace(pwd)
                ? null : pwd);
        };

        cancelBtn.Clicked += async (_, _) =>
        {
            await Navigation.PopModalAsync();
            tcs.TrySetResult(null);
        };

        pwdPage.Disappearing += (_, _) =>
            tcs.TrySetResult(null);

        await Navigation.PushModalAsync(pwdPage);
        var result = await tcs.Task;

        if (result == null) return null;

        if (!_sharedService.VerifyPassword(result, link.PasswordHash))
        {
            await DisplayAlert("Wrong password",
                "Password does not match this link.", "OK");
            return null;
        }
        return result;
    }

    private async Task PushLinkAsync(SharedActivityLink link)
    {
        var pwd = await AskPasswordAsync(link);
        if (pwd == null) return;
        var activitiesToPush = new List<Activity>();
        foreach (var (gameId, actName) in _sharedService.GetSharedActivities(link))
        {
            var acts = await _activityService.GetActivitiesAsync(
                _auth.CurrentUsername, gameId);
            var match = acts.FirstOrDefault(a => string.Equals(
                a.Name, actName, StringComparison.OrdinalIgnoreCase));
            if (match != null) activitiesToPush.Add(match);
        }
        if (activitiesToPush.Count == 0)
        {
            await DisplayAlert("Nothing to push",
                "None of the shared activities were found locally.", "OK");
            return;
        }
        var ok = await _syncService.UploadSharedActivitiesAsync(
            _auth.CurrentUsername, link, activitiesToPush, pwd,
            _expService);
        if (ok)
        {
            link.LastUploadedAt = DateTime.UtcNow;
            link.LastUpdatedBy = _auth.CurrentUsername;
            link.LastUpdatedAt = DateTime.UtcNow;
            await _sharedService.UpdateLinkAsync(link);
            await RefreshLinksAsync();
            await DisplayAlert("Pushed",
                $"{activitiesToPush.Count} activities pushed to partner.", "OK");
        }
        else
            await DisplayAlert("Failed",
                "Upload failed. Check your sync server settings.", "OK");
    }

    private async Task PullLinkAsync(SharedActivityLink link)
    {
        var pwd = await AskPasswordAsync(link);
        if (pwd == null) return;

        _loadingOverlay.IsVisible = true;
        _loadingLabel.Text = "Downloading from server...";

        var result = await _syncService.DownloadSharedActivitiesAsync(link, pwd);
        if (result == null)
        {
            _loadingOverlay.IsVisible = false;
            await DisplayAlert("Nothing available",
                "No data found for this link on the server, or decryption failed.", "OK");
            return;
        }
        var (updatedBy, updatedAt, items, expRecords,
            expStates, dlError) = result.Value;

        if (!string.IsNullOrWhiteSpace(dlError))
        {
            _loadingOverlay.IsVisible = false;
            await DisplayAlert("Pull Failed", dlError, "OK");
            return;
        }
        if (link.LastDownloadedAt.HasValue &&
            updatedAt <= link.LastDownloadedAt.Value)
        {
            _loadingOverlay.IsVisible = false;
            await DisplayAlert("Up to date", "No new updates from your partner.", "OK");
            return;
        }

        _loadingOverlay.IsVisible = false;
        bool confirm = await DisplayAlert("New Updates",
            $"{updatedBy} pushed {items.Count} activit{(items.Count == 1 ? "y" : "ies")} " +
            $"on {updatedAt.ToLocalTime():dd MMM HH:mm}.\n\nApply these updates locally?",
            "Apply", "Skip");
        if (!confirm) return;

        _loadingOverlay.IsVisible = true;
        _loadingLabel.Text = "Applying updates...";

        int applied = 0;
        foreach (var item in items)
        {
            try
            {
                var existing = await _activityService.GetActivitiesAsync(
                    _auth.CurrentUsername, item.Game);
                var local = existing.FirstOrDefault(a => string.Equals(
                    a.Name, item.Name, StringComparison.OrdinalIgnoreCase));
                if (local != null)
                {
                    local.Category = item.Category;
                    local.ExpGain = item.ExpGain;
                    local.ImagePath = item.ImagePath;
                    local.IsActive = item.IsActive;
                    local.DisplayDaysOfWeek = item.DisplayDaysOfWeek;
                    local.DisplayDayOfMonth = item.DisplayDayOfMonth;
                    local.IsAutoAward = item.IsAutoAward;
                    local.AutoAwardFrequency = item.AutoAwardFrequency;
                    local.AutoAwardDays = item.AutoAwardDays;
                    local.LastAutoAwarded = item.LastAutoAwarded;
                    local.HabitStreak = item.HabitStreak;
                    local.TimesCompleted = item.TimesCompleted;
                    local.MeaningfulUntilLevel = item.MeaningfulUntilLevel;
                    await _activityService.UpdateActivityAsync(local);
                }
                else
                {
                    await _activityService.CreateActivityAsync(new Activity
                    {
                        Username = _auth.CurrentUsername, Game = item.Game,
                        Name = item.Name, Category = item.Category,
                        ExpGain = item.ExpGain, ImagePath = item.ImagePath,
                        IsActive = item.IsActive,
                        DisplayDaysOfWeek = item.DisplayDaysOfWeek,
                        DisplayDayOfMonth = item.DisplayDayOfMonth,
                        IsAutoAward = item.IsAutoAward,
                        AutoAwardFrequency = item.AutoAwardFrequency,
                        AutoAwardDays = item.AutoAwardDays,
                        HabitStreak = item.HabitStreak,
                        TimesCompleted = item.TimesCompleted,
                        MeaningfulUntilLevel = item.MeaningfulUntilLevel
                    });
                }
                applied++;
            }
            catch { }
        }

        // Insert EXP records not already present in the actual ExpLog table.
        var conn = await GetDbConnectionAsync();
        int expInserted = 0;
        foreach (var rec in expRecords)
        {
            var existing = await conn.Table<ExpLog>()
                .Where(r => r.Username == _auth.CurrentUsername &&
                    r.Game == rec.GameId &&
                    r.ActivityName == rec.ActivityName &&
                    r.LoggedAt == rec.Timestamp)
                .FirstOrDefaultAsync();
            if (existing != null) continue;

            var state = await conn.Table<ExpState>()
                .Where(s => s.Username == _auth.CurrentUsername &&
                    s.Game == rec.GameId)
                .FirstOrDefaultAsync();
            int totalBefore = state?.TotalExp ?? 0;
            var (levelBefore, _, _) = ExpEngine.GetProgress(totalBefore);
            var (levelAfter, _, _) = ExpEngine.GetProgress(
                totalBefore + rec.ExpGained);
            await conn.InsertAsync(new ExpLog
            {
                Username = _auth.CurrentUsername,
                Game = rec.GameId,
                ActivityName = rec.ActivityName,
                DeltaExp = rec.ExpGained,
                TotalExp = totalBefore + rec.ExpGained,
                LevelBefore = levelBefore,
                LevelAfter = levelAfter,
                LoggedAt = rec.Timestamp
            });
            expInserted++;
        }

        // ExpState stores TotalExp; game level is derived by ExpEngine.
        foreach (var state in expStates)
        {
            var localState = await conn.Table<ExpState>()
                .Where(s => s.Username == _auth.CurrentUsername &&
                    s.Game == state.GameId)
                .FirstOrDefaultAsync();
            if (localState == null)
            {
                await conn.InsertAsync(new ExpState
                {
                    Username = _auth.CurrentUsername,
                    Game = state.GameId,
                    TotalExp = state.TotalExp,
                    UpdatedAt = state.LastUpdated
                });
            }
            else if (state.TotalExp > localState.TotalExp)
            {
                localState.TotalExp = state.TotalExp;
                localState.UpdatedAt = state.LastUpdated;
                await conn.UpdateAsync(localState);
            }
        }

        link.LastDownloadedAt = DateTime.UtcNow;
        await _sharedService.UpdateLinkAsync(link);
        await RefreshLinksAsync();
        var summary = new List<string>();
        if (applied > 0)
            summary.Add($"{applied} activit{(applied == 1 ? "y" : "ies")} updated");
        if (expInserted > 0)
            summary.Add($"{expInserted} EXP records added");
        _loadingOverlay.IsVisible = false;
        await DisplayAlert("Done", string.Join(", ", summary) + ".", "OK");
    }

    private async Task<SQLite.ISQLiteAsyncConnection> GetDbConnectionAsync() =>
        await _db.GetConnectionAsync();

    private async Task CreateShareLinkAsync()
    {
        string? partnerName = await DisplayPromptAsync("Partner Name",
            "What is your partner's display name?", "Next", "Cancel",
            placeholder: "e.g. VM Demo Account");
        if (string.IsNullOrWhiteSpace(partnerName)) return;
        string? password = await DisplayPromptAsync("Shared Password",
            "Set a shared password. Both users must enter the same password.",
            "Next", "Cancel", placeholder: "Choose a shared password");
        if (string.IsNullOrWhiteSpace(password)) return;
        string? confirmPwd = await DisplayPromptAsync("Confirm Password",
            "Enter the password again:", "Next", "Cancel",
            placeholder: "Repeat password");
        if (confirmPwd != password)
        {
            await DisplayAlert("Mismatch", "Passwords do not match.", "OK");
            return;
        }
        var linkCode = SharedActivityService.GenerateLinkCode();
        var selectionPage = new SharedActivitySelectionPage(
            _activityService, _gameService, _auth, async selected =>
            {
                if (selected.Count == 0) return;

                var games = await _gameService.GetGamesAsync(
                    _auth.CurrentUsername);
                var gameNameMap = games.ToDictionary(
                    g => g.GameId,
                    g => g.DisplayName,
                    StringComparer.OrdinalIgnoreCase);

                var manifestItems = new List<(string GameId,
                    string GameName, string ActivityName, int ExpGain)>();
                foreach (var (gameId, actName) in selected)
                {
                    var acts = await _activityService.GetActivitiesAsync(
                        _auth.CurrentUsername, gameId);
                    var act = acts.FirstOrDefault(a => string.Equals(
                        a.Name, actName, StringComparison.OrdinalIgnoreCase));
                    if (act == null) continue;
                    manifestItems.Add((gameId,
                        gameNameMap.GetValueOrDefault(gameId, gameId),
                        actName, act.ExpGain));
                }

                string? manifestError = await _syncService
                    .UploadSharedManifestAsync(_auth.CurrentUsername,
                        linkCode, password, manifestItems);

                await _sharedService.CreateLinkAsync(_auth.CurrentUsername,
                    linkCode, partnerName.Trim(), password, selected);

                string manifestNote = manifestError == null
                    ? ""
                    : $"\n\n⚠️ Manifest upload failed:\n{manifestError}";
                await DisplayAlert("Link Created",
                    $"Share this code with {partnerName}:\n\nCODE: {linkCode}\n\n" +
                    "They must enter this code and the same " +
                    $"password to join.{manifestNote}", "OK");
                await RefreshLinksAsync();
            });
        await Navigation.PushAsync(selectionPage);
    }

    private async Task JoinShareLinkAsync()
    {
        string? code = await DisplayPromptAsync(
            "Link Code",
            "Enter the link code from your partner:",
            "Next", "Cancel",
            placeholder: "e.g. AB3D5F8G");
        if (string.IsNullOrWhiteSpace(code)) return;
        code = code.Trim().ToUpperInvariant();

        string? password = await DisplayPromptAsync(
            "Shared Password",
            "Enter the shared password for this link:",
            "Next", "Cancel",
            placeholder: "Shared password");
        if (string.IsNullOrWhiteSpace(password)) return;

        string? partnerName = await DisplayPromptAsync(
            "Partner Name",
            "What is your partner's display name?",
            "Next", "Cancel",
            placeholder: "e.g. Main PC");
        if (string.IsNullOrWhiteSpace(partnerName)) return;

        var manifest = await _syncService
            .DownloadSharedManifestAsync(code, password);

        List<(string GameId, string ActivityName)> approved;
        if (manifest == null)
        {
            bool proceed = await DisplayAlert(
                "No Manifest Found",
                "Could not download the activity list from the " +
                "server. This could mean the creator hasn't pushed " +
                "yet, or the password is wrong.\n\n" +
                "Join anyway and pull later?",
                "Join Anyway", "Cancel");
            if (!proceed) return;
            approved = new List<(string, string)>();
        }
        else
        {
            var (createdBy, items) = manifest.Value;
            var tcs = new TaskCompletionSource<
                List<(string GameId, string ActivityName)>>();
            var selPage = new SharedManifestApprovalPage(
                createdBy, items,
                async approvedItems =>
                {
                    tcs.TrySetResult(approvedItems);
                    await Task.CompletedTask;
                });

            await Navigation.PushAsync(selPage);
            approved = await tcs.Task;
            if (approved.Count == 0)
            {
                await DisplayAlert("Nothing selected",
                    "No activities approved. Join cancelled.", "OK");
                return;
            }
        }

        await _sharedService.CreateLinkAsync(_auth.CurrentUsername,
            code, partnerName.Trim(), password, approved);
        await DisplayAlert("Joined",
            $"Link with {partnerName} saved with {approved.Count} approved activit" +
            $"{(approved.Count == 1 ? "y" : "ies")}.\n\n" +
            "Tap Pull to fetch their latest data.",
            "OK");
        await RefreshLinksAsync();
    }
}

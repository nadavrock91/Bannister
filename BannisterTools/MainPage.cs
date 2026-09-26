using Bannister.Services;

namespace BannisterTools;

public class MainPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly SyncService _sync;
    private readonly Label _statusLabel;

    public MainPage(DatabaseService db, SyncService sync)
    {
        _db = db;
        _sync = sync;
        Title = "BannisterTools";
        BackgroundColor = Color.FromArgb("#CCF5F5F5");

        _statusLabel = new Label
        {
            Text = "Tools go here",
            FontSize = 16,
            TextColor = Color.FromArgb("#555")
        };

        var layout = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16,
            Children =
            {
                new Label
                {
                    Text = "BannisterTools",
                    FontSize = 26,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#222")
                },
                _statusLabel
            }
        };

#if ANDROID
        var syncButton = new Button
        {
            Text = "Sync",
            BackgroundColor = Color.FromArgb("#3949AB"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        syncButton.Clicked += OnSyncClicked;
        layout.Children.Add(syncButton);
#endif

        Content = new ScrollView { Content = layout };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Both apps use this exact shared database path.
        string databasePath = DatabaseService.DatabasePath;
        System.Diagnostics.Debug.WriteLine(
            $"BannisterTools database: {databasePath}");

#if ANDROID
        await ReadLocalDatabaseAsync();
#endif
    }

#if ANDROID
    private async void OnSyncClicked(object? sender, EventArgs e)
    {
        try
        {
            _statusLabel.Text = "Syncing...";
            var result = await _sync.DownloadAsync();
            if (!result.Success)
            {
                _statusLabel.Text = result.Message;
                await DisplayAlert("Sync", result.Message, "OK");
                return;
            }

            await ReadLocalDatabaseAsync();
            await DisplayAlert("Sync", result.Message, "OK");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
            await DisplayAlert("Sync Error", ex.Message, "OK");
        }
    }

    private async Task ReadLocalDatabaseAsync()
    {
        try
        {
            var connection = await _db.GetConnectionAsync();
            await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM sqlite_master");
            _statusLabel.Text = "Tools go here";
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Database unavailable: {ex.Message}";
        }
    }
#endif
}

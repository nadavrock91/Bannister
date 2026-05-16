using Bannister.Services;

namespace Bannister.Views;

public class ImportFromSyncPage : ContentPage
{
    private readonly TaskCompletionSource<ImportFromSyncRequest?> _completion = new();
    private readonly Entry _bannisterUsername;
    private readonly Entry _bannisterPassword;
    private readonly Entry _serverUrl;
    private readonly Entry _syncUsername;
    private readonly Entry _syncPassword;
    private bool _completed;

    public ImportFromSyncPage(string defaultServerUrl)
    {
        Title = "Import from Another Device";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _bannisterUsername = NewEntry("Bannister username");
        _bannisterPassword = NewEntry("Bannister login password", isPassword: true);
        _serverUrl = NewEntry("https://yourdomain.com/bannister/sync.php");
        _serverUrl.Text = defaultServerUrl ?? "";
        _syncUsername = NewEntry("sync username");
        _syncPassword = NewEntry("sync password", isPassword: true);

        var importButton = new Button
        {
            Text = "Import Database",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        importButton.Clicked += OnImportClicked;

        var cancelButton = new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#5B63EE")
        };
        cancelButton.Clicked += async (_, _) => await CompleteAsync(null);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Import from Another Device",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333")
                    },
                    new Label
                    {
                        Text = "Use the Bannister login password from your master device. It must match the downloaded database.",
                        FontSize = 13,
                        TextColor = Color.FromArgb("#666"),
                        LineHeight = 1.3
                    },
                    LabelFor("Bannister username"),
                    _bannisterUsername,
                    LabelFor("Bannister login password"),
                    _bannisterPassword,
                    LabelFor("Sync server URL"),
                    _serverUrl,
                    LabelFor("Sync username"),
                    _syncUsername,
                    LabelFor("Sync password"),
                    _syncPassword,
                    importButton,
                    cancelButton
                }
            }
        };
    }

    public Task<ImportFromSyncRequest?> Result => _completion.Task;

    private static Entry NewEntry(string placeholder, bool isPassword = false) => new()
    {
        Placeholder = placeholder,
        IsPassword = isPassword,
        FontSize = 14,
        TextColor = Color.FromArgb("#222"),
        PlaceholderColor = Color.FromArgb("#999"),
        BackgroundColor = Colors.White
    };

    private static Label LabelFor(string text) => new()
    {
        Text = text,
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        TextColor = Color.FromArgb("#555")
    };

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        var request = new ImportFromSyncRequest(
            _bannisterUsername.Text?.Trim() ?? "",
            _bannisterPassword.Text ?? "",
            _serverUrl.Text?.Trim() ?? "",
            _syncUsername.Text?.Trim() ?? "",
            _syncPassword.Text ?? "");

        if (string.IsNullOrWhiteSpace(request.BannisterUsername) ||
            string.IsNullOrWhiteSpace(request.BannisterPassword) ||
            string.IsNullOrWhiteSpace(request.ServerUrl) ||
            string.IsNullOrWhiteSpace(request.SyncUsername) ||
            string.IsNullOrWhiteSpace(request.SyncPassword))
        {
            await DisplayAlert("Missing Fields", "Fill in all fields to import from another device.", "OK");
            return;
        }

        bool confirm = await DisplayAlert(
            "Import Database?",
            $"Download the database for '{request.BannisterUsername}' and set this device as secondary?",
            "Import",
            "Cancel");

        if (confirm)
            await CompleteAsync(request);
    }

    private async Task CompleteAsync(ImportFromSyncRequest? request)
    {
        if (_completed) return;
        _completed = true;
        _completion.TrySetResult(request);
        await Navigation.PopModalAsync();
    }
}

public record ImportFromSyncRequest(
    string BannisterUsername,
    string BannisterPassword,
    string ServerUrl,
    string SyncUsername,
    string SyncPassword);

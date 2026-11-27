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
        BackgroundColor = AppColors.Get(ThemeKey.SecondarySurface);

        _bannisterUsername = NewEntry("Bannister username");
        _bannisterPassword = NewEntry("Bannister login password", isPassword: true);
        _serverUrl = NewEntry("https://yourdomain.com/bannister/sync.php");
        _serverUrl.Text = defaultServerUrl ?? "";
        _syncUsername = NewEntry("sync username");
        _syncPassword = NewEntry("sync password", isPassword: true);

        var importButton = (new Button
        {
            Text = "Import Database",
            CornerRadius = 8,
            HeightRequest = 48
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);
        importButton.Clicked += OnImportClicked;

        var cancelButton = (new Button
        {
            Text = "Cancel",
            BackgroundColor = Colors.Transparent
        }).ThemeColor(Button.TextColorProperty, ThemeKey.SecondaryAccentText);
        cancelButton.Clicked += async (_, _) => await CompleteAsync(null);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 24,
                Spacing = 14,
                Children =
                {
                    (new Label
                    {
                        Text = "Import from Another Device",
                        FontSize = 24,
                        FontAttributes = FontAttributes.Bold
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true),
                    (new Label
                    {
                        Text = "Use the Bannister login password from your master device. It must match the downloaded database.",
                        FontSize = 13,
                        LineHeight = 1.3
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true),
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

    private static Entry NewEntry(string placeholder, bool isPassword = false) => (new Entry(){
        Placeholder = placeholder,
        IsPassword = isPassword,
        FontSize = 14
    }).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText3).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground);

    private static Label LabelFor(string text) => (new Label(){
        Text = text,
        FontSize = 13,
        FontAttributes = FontAttributes.Bold
    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText1, classifyPageText: true);

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

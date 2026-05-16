using Bannister.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;


namespace Bannister.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly AuthService _auth;
        private readonly DatabaseService _db;
        private readonly DeviceModeService _deviceMode;
        private readonly SyncService _sync;
        private bool _isRegistering = false;

        public LoginPage(AuthService auth, DatabaseService db, DeviceModeService deviceMode, SyncService sync)
        {
            InitializeComponent();
            _auth = auth;
            _db = db;
            _deviceMode = deviceMode;
            _sync = sync;
            LoadSavedCredentials();

#if DEBUG
            btnTestDev.IsVisible = true;
            btnRunFix.IsVisible = true;
            btnDeleteDb.IsVisible = true;
#endif
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (await _auth.TryRestoreSessionAsync())
                await Shell.Current.GoToAsync("//home");
        }

        private async void LoadSavedCredentials()
        {
            try
            {
                var savedUsername = await SecureStorage.GetAsync("saved_username");
                var savedPassword = await SecureStorage.GetAsync("saved_password");

                if (!string.IsNullOrEmpty(savedUsername) && !string.IsNullOrEmpty(savedPassword))
                {
                    txtUsername.Text = savedUsername;
                    txtPassword.Text = savedPassword;
                    chkRemember.IsChecked = true;
                }
            }
            catch { }
        }

        // Long-press handler for mobile - shows action sheet
        private async void OnLongPress(object sender, EventArgs e)
        {
#if ANDROID || IOS
            await ShowContextMenuAsync();
#endif
        }

        private async Task ShowContextMenuAsync()
        {
            string action = await DisplayActionSheet("Options", "Cancel", null,
                "Import Data from Backup",
                "Export Data to Backup",
                "Open Data Folder");

            switch (action)
            {
                case "Import Data from Backup":
                    await ImportDataAsync();
                    break;
                case "Export Data to Backup":
                    await ExportDataAsync();
                    break;
                case "Open Data Folder":
                    OpenDataFolder();
                    break;
            }
        }

        // Context menu handlers (for right-click on Windows)
        private async void OnImportClicked(object sender, EventArgs e) => await ImportDataAsync();
        private async void OnExportClicked(object sender, EventArgs e) => await ExportDataAsync();
        private void OnOpenDataFolderClicked(object sender, EventArgs e) => OpenDataFolder();

        private async Task ImportDataAsync()
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select Database Backup (.db file)",
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".db" } },
                    { DevicePlatform.Android, new[] { "application/octet-stream", "application/x-sqlite3", "*/*" } },
                    { DevicePlatform.iOS, new[] { "public.database" } }
                })
                });

                if (result == null)
                {
                    System.Diagnostics.Debug.WriteLine("File picker cancelled");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"File picked: {result.FileName}");

                // Get BackupService
                var backupService = Handler?.MauiContext?.Services.GetService<BackupService>();
                if (backupService == null)
                {
                    await DisplayAlert("Error", "Backup service not available", "OK");
                    return;
                }

                // Copy file to temp location first (FilePicker.FullPath may not work on Android)
                string tempPath = Path.Combine(FileSystem.CacheDirectory, result.FileName);

                using (var sourceStream = await result.OpenReadAsync())
                using (var destStream = File.Create(tempPath))
                {
                    await sourceStream.CopyToAsync(destStream);
                }

                System.Diagnostics.Debug.WriteLine($"File copied to temp: {tempPath}");

                // Try to restore
                var restoreResult = await backupService.RestoreFromDbFileAsync(tempPath);

                // Clean up temp file
                try { File.Delete(tempPath); } catch { }

                if (!restoreResult.success)
                {
                    await DisplayAlert("Import Failed", restoreResult.message, "OK");
                }
                else
                {
                    await DisplayAlert("Import Successful", restoreResult.message, "OK");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Import error: {ex.Message}");
                await DisplayAlert("Import Failed", $"Error: {ex.Message}", "OK");
            }
        }

        private async Task ExportDataAsync()
        {
            try
            {
                string json = await _db.ExportDataAsync();
                string fileName = $"bannister_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json";

#if WINDOWS
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Bannister", "Backups");
                Directory.CreateDirectory(folder);
                string filePath = Path.Combine(folder, fileName);
                await File.WriteAllTextAsync(filePath, json);
                await DisplayAlert("Export Complete", $"Saved to:\n{filePath}", "OK");
#else
                // On mobile, use Share
                string filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
                await File.WriteAllTextAsync(filePath, json);
                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = "Export Bannister Data",
                    File = new ShareFile(filePath)
                });
#endif
            }
            catch (Exception ex)
            {
                await DisplayAlert("Export Failed", ex.Message, "OK");
            }
        }

        private void OpenDataFolder()
        {
#if WINDOWS
            try
            {
                string folder = FileSystem.AppDataDirectory;
                System.Diagnostics.Process.Start("explorer.exe", folder);
            }
            catch (Exception ex)
            {
                DisplayAlert("Error", ex.Message, "OK");
            }
#else
            DisplayAlert("Info", $"Data folder:\n{FileSystem.AppDataDirectory}", "OK");
#endif
        }

        private async void OnLoginClicked(object sender, EventArgs e)
        {
            string username = txtUsername.Text?.Trim() ?? "";
            string password = txtPassword.Text ?? "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Please enter username and password");
                return;
            }

            if (_isRegistering)
            {
                if (username.Length < 3)
                {
                    ShowError("Username must be at least 3 characters");
                    return;
                }
                if (password.Length < 4)
                {
                    ShowError("Password must be at least 4 characters");
                    return;
                }

                bool success = await _auth.RegisterAsync(username, password);
                if (success)
                {
                    await SaveOrClearCredentials(username, password);
                    bool loggedIn = await _auth.LoginAsync(username, password);
                    if (!loggedIn)
                    {
                        ShowError("Account was created, but login failed. Try logging in again.");
                        return;
                    }

                    await Shell.Current.GoToAsync("//home");
                }
                else
                {
                    ShowError(string.IsNullOrWhiteSpace(_auth.LastRegisterError)
                        ? "Registration failed"
                        : _auth.LastRegisterError);
                }
            }
            else
            {
                bool success = await _auth.LoginAsync(username, password);
                if (success)
                {
                    await SaveOrClearCredentials(username, password);
                    await Shell.Current.GoToAsync("//home");
                }
                else
                    ShowError("Invalid username or password");
            }
        }

        private async void OnImportFromSyncClicked(object sender, EventArgs e)
        {
            var page = new ImportFromSyncPage(_deviceMode.ServerUrl);
            await Navigation.PushModalAsync(page);
            var request = await page.Result;
            if (request == null) return;

            await ImportFromSyncAsync(request);
        }

        private async Task ImportFromSyncAsync(ImportFromSyncRequest request)
        {
            var oldMode = _deviceMode.CurrentMode;
            var oldServerUrl = _deviceMode.ServerUrl;
            var (oldSyncUser, oldSyncHash) = await _deviceMode.GetSyncCredentialsAsync();
            string? rollbackPath = null;
            bool completed = false;

            try
            {
                SetImportUiBusy(true);
                ShowStatus("Preparing import...");

                if (File.Exists(DatabaseService.DatabasePath))
                {
                    rollbackPath = Path.Combine(FileSystem.CacheDirectory, $"bannister_import_rollback_{DateTime.UtcNow.Ticks}.db");
                    File.Copy(DatabaseService.DatabasePath, rollbackPath, overwrite: true);
                }

                _deviceMode.ServerUrl = request.ServerUrl;
                await _deviceMode.SetSyncCredentialsAsync(request.SyncUsername, request.SyncPassword);

                ShowStatus("Downloading database...");
                var download = await _sync.DownloadAsync();
                if (!download.Success)
                    throw new InvalidOperationException(download.Message);

                ShowStatus("Verifying password...");
                if (!await _db.TryOpenWithPasswordAsync(request.BannisterPassword))
                {
                    throw new InvalidOperationException(
                        "Could not open the downloaded database. Make sure the login password matches what you used on the master device.");
                }

                _db.SetPassword(request.BannisterPassword);
                if (_deviceMode.CurrentMode != DeviceModeService.Mode.Master)
                    _deviceMode.SetMode(DeviceModeService.Mode.Master);

                await _db.ReinitializeAsync();

                ShowStatus("Preparing local login...");
                if (!await _auth.EnsureImportedUserAsync(request.BannisterUsername, request.BannisterPassword))
                {
                    throw new InvalidOperationException(
                        "The downloaded database opened, but the local user could not be verified. Make sure the Bannister username and login password match the master device.");
                }

                if (!await _auth.LoginAsync(request.BannisterUsername, request.BannisterPassword))
                    throw new InvalidOperationException("Imported database verified, but local login failed.");

                _deviceMode.SetMode(DeviceModeService.Mode.Secondary);
                await _db.ReinitializeAsync();

                completed = true;
                ShowStatus("Import complete.");
                await DisplayAlert("Import Complete", "Database imported. This device is now in Secondary Device Mode.", "OK");
                await Shell.Current.GoToAsync("//home");
            }
            catch (Exception ex)
            {
                ShowStatus("Import failed.");
                await RollBackImportAsync(rollbackPath, oldMode, oldServerUrl, oldSyncUser, oldSyncHash);
                await DisplayAlert("Import Failed", ex.Message, "OK");
            }
            finally
            {
                if (rollbackPath != null && File.Exists(rollbackPath))
                {
                    try { File.Delete(rollbackPath); } catch { }
                }

                if (!completed)
                    SetImportUiBusy(false);
            }
        }

        private async Task RollBackImportAsync(
            string? rollbackPath,
            DeviceModeService.Mode oldMode,
            string oldServerUrl,
            string oldSyncUser,
            string oldSyncHash)
        {
            try
            {
                _deviceMode.SetMode(oldMode);
                _deviceMode.ServerUrl = oldServerUrl;

                if (!string.IsNullOrWhiteSpace(oldSyncUser) && !string.IsNullOrWhiteSpace(oldSyncHash))
                    await _deviceMode.SetSyncCredentialHashAsync(oldSyncUser, oldSyncHash);
                else
                    _deviceMode.ClearSyncCredentials();

                if (rollbackPath != null && File.Exists(rollbackPath))
                    await _db.ReplaceDatabaseFromAsync(rollbackPath);
                else
                    await _db.DeleteDatabaseFileForImportRollbackAsync();
            }
            catch (Exception rollbackEx)
            {
                System.Diagnostics.Debug.WriteLine($"Import rollback failed: {rollbackEx.Message}");
            }
        }

        private void SetImportUiBusy(bool isBusy)
        {
            btnLogin.IsEnabled = !isBusy;
            btnToggle.IsEnabled = !isBusy;
            btnImportFromSync.IsEnabled = !isBusy;
            txtUsername.IsEnabled = !isBusy;
            txtPassword.IsEnabled = !isBusy;
            chkRemember.IsEnabled = !isBusy;
        }

        private void ShowStatus(string message)
        {
            lblError.Text = message;
            lblError.TextColor = Color.FromArgb("#5B63EE");
            lblError.IsVisible = true;
        }

        private async void OnTestDevClicked(object sender, EventArgs e)
        {
            lblError.IsVisible = false;

            // Generate random credentials
            string suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(2)).ToLowerInvariant();
            string username = $"test_{suffix}";
            string password = "test1234";

            // Register the test user
            bool registered = await _auth.RegisterAsync(username, password);
            if (!registered)
            {
                ShowError("DEV register failed");
                return;
            }

            // Auto-remember for convenience
            chkRemember.IsChecked = true;
            await SaveOrClearCredentials(username, password);

            // Login and navigate
            if (!await _auth.LoginAsync(username, password))
            {
                ShowError("DEV login failed after register");
                return;
            }

            await Shell.Current.GoToAsync("//home");
        }

        private async void OnDeleteDatabaseClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Delete Database", "Are you sure? This will delete all data.", "Delete", "Cancel");
            if (confirm)
            {
                await _db.DeleteDatabaseAsync();
                SecureStorage.RemoveAll();
                txtUsername.Text = "";
                txtPassword.Text = "";
                chkRemember.IsChecked = false;
                await DisplayAlert("Done", "Database deleted. Restart the app.", "OK");
            }
        }

        private async Task SaveOrClearCredentials(string username, string password)
        {
            if (chkRemember.IsChecked)
            {
                await SecureStorage.SetAsync("saved_username", username);
                await SecureStorage.SetAsync("saved_password", password);
            }
            else
            {
                SecureStorage.Remove("saved_username");
                SecureStorage.Remove("saved_password");
            }
        }

        private void OnToggleClicked(object sender, EventArgs e)
        {
            _isRegistering = !_isRegistering;
            lblError.IsVisible = false;

            lblTitle.Text = _isRegistering ? "Create Account" : "Welcome Back";
            btnLogin.Text = _isRegistering ? "Register" : "Login";
            btnToggle.Text = _isRegistering ? "Already have an account?" : "Need an account?";
        }

        private void ShowError(string message)
        {
            lblError.Text = message;
            lblError.TextColor = Colors.Red;
            lblError.IsVisible = true;
        }

        private async void OnRunFixClicked(object sender, EventArgs e)
        {
            bool confirm = await DisplayAlert("Run Dev Fix",
                "This will clear escalation timer data for all games.\n\nContinue?",
                "Run Fix", "Cancel");

            if (confirm)
            {
                try
                {
                    await Bannister.Helpers.DevFixes.RunCurrentFix(_db, _auth);
                    await DisplayAlert("Success", "Dev fix completed successfully!", "OK");
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Fix failed: {ex.Message}", "OK");
                }
            }
        }
    }
}

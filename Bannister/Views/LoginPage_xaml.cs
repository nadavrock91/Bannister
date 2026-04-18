using Bannister.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bannister.Services;
using System.Security.Cryptography;


namespace Bannister.Views
{
    public partial class LoginPage : ContentPage
    {
        private readonly AuthService _auth;
        private readonly DatabaseService _db;
        private bool _isRegistering = false;

        public LoginPage(AuthService auth, DatabaseService db)
        {
            InitializeComponent();
            _auth = auth;
            _db = db;
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
                    await _auth.LoginAsync(username, password);
                    await Shell.Current.GoToAsync("//home");
                }
                else
                {
                    ShowError("Username already exists");
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
            await _auth.LoginAsync(username, password);
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bannister.Services;

namespace Bannister.Views;

[QueryProperty(nameof(GameId), "gameId")]
public partial class SetupGoalPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DragonService _dragons;
    private readonly GameService _games;

    private string _gameId = "";
    private string[] _libraryImages = Array.Empty<string>();
    private string _selectedImagePath = "diet_dragon_default.png";
    private string? _customImagePath = null;
    private string _imageMode = "default";
    private bool _isInitialized = false;

    public string GameId
    {
        get => _gameId;
        set
        {
            _gameId = value;
            OnPropertyChanged();
        }
    }

    public SetupGoalPage(AuthService auth, DragonService dragons, GameService games)
    {
        InitializeComponent();
        _auth = auth;
        _dragons = dragons;
        _games = games;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_isInitialized)
            return;

        _isInitialized = true;

        try
        {
            _libraryImages = await GetAvailableDragonImagesAsync();

            var game = await _games.GetGameAsync(_auth.CurrentUsername, GameId);
            if (game != null)
            {
                Title = $"{game.DisplayName} Dragon (Level 100 Goal)";
                lblPrompt.Text =
                    $"AI Prompt: Create an image of a dragon that represents {game.DisplayName.ToLower()} goals. " +
                    $"The dragon should be compelling and slightly menacing. Clean illustration style. No text.";
            }

            var existing = await _dragons.GetDragonAsync(_auth.CurrentUsername, GameId);
            if (existing != null)
            {
                txtGoalName.Text = existing.Title ?? "";
                txtGoalDesc.Text = existing.Description ?? "";

                if (!string.IsNullOrWhiteSpace(existing.ImagePath))
                {
                    _selectedImagePath = existing.ImagePath;

                    if (IsBundledImageName(_selectedImagePath))
                    {
                        _imageMode = string.Equals(_selectedImagePath, "diet_dragon_default.png", StringComparison.OrdinalIgnoreCase)
                            ? "default"
                            : "library";

                        imgDragonPreview.Source = _selectedImagePath;
                    }
                    else
                    {
                        _imageMode = "own";
                        _customImagePath = _selectedImagePath;
                        imgDragonPreview.Source = ImageSource.FromFile(_selectedImagePath);
                    }
                }
                else
                {
                    _selectedImagePath = "diet_dragon_default.png";
                    _imageMode = "default";
                    imgDragonPreview.Source = _selectedImagePath;
                }

                UpdateButtonStyles();
                return;
            }

            _selectedImagePath = "diet_dragon_default.png";
            _imageMode = "default";
            imgDragonPreview.Source = _selectedImagePath;
            UpdateButtonStyles();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private async Task<string[]> GetAvailableDragonImagesAsync()
    {
        var images = new List<string>
        {
            "diet_dragon_default.png",
            "dragon_1.png",
            "dragon_2.png",
            "dragon_3.png",
            "dragon_4.png",
            "dragon_5.png",
            "dragon_6.png",
            "dragon_7.png",
            "dragon_8.png",
            "dragon_9.jpg",
            "dragon_10.jpg",
            "dragon_11.jpg",
            "dragon_12.png",
            "dragon_13.jpg",
            "dragon_14.jpg",
            "dragon_15.jpg",
            "dragon_16.png",
            "dragon_slayer_1.png"
        };

        return await Task.FromResult(images.ToArray());
    }

    private static bool IsBundledImageName(string pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return false;

        if (Path.IsPathRooted(pathOrName))
            return false;

        if (pathOrName.Contains("/") || pathOrName.Contains("\\"))
            return false;

        return true;
    }

    private void SetPreviewImage(string pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
        {
            imgDragonPreview.Source = "diet_dragon_default.png";
            return;
        }

        if (IsBundledImageName(pathOrName))
            imgDragonPreview.Source = pathOrName;
        else
            imgDragonPreview.Source = ImageSource.FromFile(pathOrName);
    }

    private void UpdateButtonStyles()
    {
        btnImageDefault.BackgroundColor = Color.FromArgb("#EEE");
        btnImageDefault.TextColor = Color.FromArgb("#333");

        btnImageLibrary.BackgroundColor = Color.FromArgb("#EEE");
        btnImageLibrary.TextColor = Color.FromArgb("#333");

        btnImageOwn.BackgroundColor = Color.FromArgb("#EEE");
        btnImageOwn.TextColor = Color.FromArgb("#333");

        switch (_imageMode)
        {
            case "default":
                btnImageDefault.BackgroundColor = Color.FromArgb("#5B63EE");
                btnImageDefault.TextColor = Colors.White;
                break;
            case "library":
                btnImageLibrary.BackgroundColor = Color.FromArgb("#5B63EE");
                btnImageLibrary.TextColor = Colors.White;
                break;
            case "own":
                btnImageOwn.BackgroundColor = Color.FromArgb("#5B63EE");
                btnImageOwn.TextColor = Colors.White;
                break;
        }

        gridCustomImage.IsVisible = _imageMode == "own";
    }

    private void OnDefaultClicked(object sender, EventArgs e)
    {
        _imageMode = "default";
        _selectedImagePath = "diet_dragon_default.png";
        SetPreviewImage(_selectedImagePath);
        UpdateButtonStyles();
    }

    private async void OnLibraryClicked(object sender, EventArgs e)
    {
        _imageMode = "library";
        UpdateButtonStyles();

        var picker = new DragonSelector(_libraryImages);
        await Navigation.PushModalAsync(picker);

        var chosen = await picker.WaitForSelectionAsync();
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            _selectedImagePath = chosen;
            SetPreviewImage(_selectedImagePath);
        }
    }

    private void OnOwnImageClicked(object sender, EventArgs e)
    {
        _imageMode = "own";
        UpdateButtonStyles();

        if (!string.IsNullOrEmpty(_customImagePath))
            SetPreviewImage(_customImagePath);
    }

    private async void OnChooseFile(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Choose a dragon image"
            });

            if (result != null)
            {
                string destFolder = Path.Combine(FileSystem.AppDataDirectory, "DragonImages");
                Directory.CreateDirectory(destFolder);

                string destPath = Path.Combine(
                    destFolder,
                    $"custom_dragon_{DateTime.Now.Ticks}{Path.GetExtension(result.FileName)}"
                );

                using var sourceStream = await result.OpenReadAsync();
                using var destStream = File.Create(destPath);
                await sourceStream.CopyToAsync(destStream);

                _customImagePath = destPath;
                _selectedImagePath = destPath;

                _imageMode = "own";
                UpdateButtonStyles();
                SetPreviewImage(destPath);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load image: {ex.Message}", "OK");
        }
    }

    private async void OnAiGeneratorsClicked(object sender, EventArgs e)
    {
        string action = await DisplayActionSheet("AI Generators", "Cancel", null,
            "ChatGPT (DALL-E)",
            "Bing Image Creator",
            "Leonardo.ai",
            "Midjourney");

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        string url = action switch
        {
            "ChatGPT (DALL-E)" => "https://chat.openai.com",
            "Bing Image Creator" => "https://www.bing.com/images/create",
            "Leonardo.ai" => "https://leonardo.ai",
            "Midjourney" => "https://www.midjourney.com",
            _ => ""
        };

        if (!string.IsNullOrEmpty(url))
            await Launcher.OpenAsync(url);
    }

    private async void OnCopyPrompt(object sender, EventArgs e)
    {
        var game = await _games.GetGameAsync(_auth.CurrentUsername, GameId);
        string gameName = game?.DisplayName ?? "diet";

        string prompt =
            $"Create an image of a dragon that represents {gameName.ToLower()} goals. " +
            $"The dragon should be compelling and slightly menacing. Clean illustration style. No text.";

        await Clipboard.SetTextAsync(prompt);
        await DisplayAlert("Copied", "Prompt copied to clipboard!", "OK");
    }

    private async void OnSaveDragon(object sender, EventArgs e)
    {
        string goalName = txtGoalName.Text?.Trim() ?? "";
        string description = txtGoalDesc.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(goalName))
        {
            await DisplayAlert("Required", "Please enter a goal name.", "OK");
            return;
        }

        if (!goalName.EndsWith("Dragon", StringComparison.OrdinalIgnoreCase))
        {
            await DisplayAlert("Invalid Name", "Goal name must end with \"Dragon\".", "OK");
            return;
        }

        await _dragons.CreateOrUpdateDragonAsync(_auth.CurrentUsername, GameId, goalName, description, _selectedImagePath);
        await Shell.Current.GoToAsync("//home");
        await Shell.Current.GoToAsync($"activitygrid?gameId={_gameId}");
    }

    private async void OnSkip(object sender, EventArgs e)
    {
        var game = await _games.GetGameAsync(_auth.CurrentUsername, GameId);
        string defaultName = $"My {game?.DisplayName ?? "Diet"} Dragon";

        await _dragons.CreateOrUpdateDragonAsync(_auth.CurrentUsername, GameId, defaultName, "", "diet_dragon_default.png");
        await Shell.Current.GoToAsync("//home");
        await Shell.Current.GoToAsync($"activitygrid?gameId={_gameId}");
    }
}

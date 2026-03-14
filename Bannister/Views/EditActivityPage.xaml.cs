using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;

namespace Bannister.Views;

public partial class EditActivityPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly StreakService? _streaks;
    private readonly string _gameId;
    private readonly Activity _activity;
    private string? _selectedImageFilename = null;
    
    // Track original state to detect changes
    private bool _wasStreakTracked;
    private bool _wasStreakContainer;
    private string _originalCategory;

    public EditActivityPage(AuthService auth, ActivityService activities, string gameId, Activity activity)
    {
        InitializeComponent();
        _auth = auth;
        _activities = activities;
        _gameId = gameId;
        _activity = activity;
        
        // Try to get StreakService from DI
        _streaks = Application.Current?.Handler?.MauiContext?.Services.GetService<StreakService>();
        
        // Store original state
        _wasStreakTracked = activity.IsStreakTracked;
        _wasStreakContainer = activity.IsStreakContainer;
        _originalCategory = activity.Category ?? "Misc";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Populate fields with existing data
        txtActivityName.Text = _activity.Name;
        txtMeaningfulLevel.Text = _activity.MeaningfulUntilLevel.ToString();

        // Set reward type
        bool isPercentType = _activity.RewardType == "PercentOfLevel";
        pickerRewardType.SelectedIndex = isPercentType ? 1 : 0;
        fixedExpSection.IsVisible = !isPercentType;
        percentExpSection.IsVisible = isPercentType;
        
        if (isPercentType)
        {
            txtPercentOfLevel.Text = _activity.PercentOfLevel.ToString();
            txtPercentCutoff.Text = _activity.PercentCutoffLevel.ToString();
        }

        // Set streak tracking
        chkStreakTracked.IsChecked = _activity.IsStreakTracked;

        // Set possible status
        chkIsPossible.IsChecked = _activity.IsPossible;

        // Set show times completed badge
        chkShowTimesCompleted.IsChecked = _activity.ShowTimesCompletedBadge;

        // Set habit target
        dateHabitTarget.MinimumDate = DateTime.Now.Date;
        chkNoHabitTarget.IsChecked = _activity.NoHabitTarget;
        if (_activity.HabitTargetDate.HasValue)
        {
            chkHasHabitTarget.IsChecked = true;
            dateHabitTarget.Date = _activity.HabitTargetDate.Value;
            dateHabitTarget.IsEnabled = true;
        }

        // Load categories and select current
        var existingActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _gameId);
        var categories = existingActivities
            .Where(a => !a.IsStreakContainer) // Don't show streak container "categories"
            .Select(a => a.Category ?? "Misc")
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        if (!categories.Contains("Misc"))
            categories.Insert(0, "Misc");
        
        // Add original category if it was stored (for streak containers being reverted)
        if (!string.IsNullOrEmpty(_activity.OriginalCategory) && !categories.Contains(_activity.OriginalCategory))
        {
            categories.Add(_activity.OriginalCategory);
        }

        pickerCategory.ItemsSource = categories;
        
        // For streak containers, show the original category, not the container name
        string categoryToSelect = _activity.IsStreakContainer && !string.IsNullOrEmpty(_activity.OriginalCategory)
            ? _activity.OriginalCategory
            : (_activity.Category ?? "Misc");
            
        int catIndex = categories.IndexOf(categoryToSelect);
        pickerCategory.SelectedIndex = catIndex >= 0 ? catIndex : 0;

        // Set schedule - StartDate/EndDate now include time
        if (_activity.StartDate.HasValue)
        {
            chkStartDate.IsChecked = true;
            dateStart.Date = _activity.StartDate.Value.Date;
            timeStart.Time = _activity.StartDate.Value.TimeOfDay;
        }

        if (_activity.EndDate.HasValue)
        {
            chkEndDate.IsChecked = true;
            dateEnd.Date = _activity.EndDate.Value.Date;
            timeEnd.Time = _activity.EndDate.Value.TimeOfDay;
        }

        // Set image from filename
        _selectedImageFilename = _activity.ImagePath;
        if (!string.IsNullOrEmpty(_selectedImageFilename))
        {
            string fullPath = GetFullImagePath(_selectedImageFilename);
            if (File.Exists(fullPath))
            {
                imgActivityPreview.Source = ImageSource.FromFile(fullPath);
            }
        }
    }

    private void OnRewardTypeChanged(object sender, EventArgs e)
    {
        bool isPercentType = pickerRewardType.SelectedIndex == 1;
        fixedExpSection.IsVisible = !isPercentType;
        percentExpSection.IsVisible = isPercentType;
    }

    private void OnIsPossibleChanged(object sender, CheckedChangedEventArgs e)
    {
        // Could add visual feedback here if needed
    }

    private void OnNoHabitTargetChanged(object sender, CheckedChangedEventArgs e)
    {
        if (e.Value)
        {
            chkHasHabitTarget.IsChecked = false;
            dateHabitTarget.IsEnabled = false;
        }
    }

    private void OnHasHabitTargetChanged(object sender, CheckedChangedEventArgs e)
    {
        dateHabitTarget.IsEnabled = e.Value;
        if (e.Value)
        {
            chkNoHabitTarget.IsChecked = false;
            if (dateHabitTarget.Date < DateTime.Now.Date)
            {
                dateHabitTarget.Date = DateTime.Now.AddDays(30);
            }
        }
    }

    private void OnHabitTarget7Days(object sender, EventArgs e)
    {
        chkHasHabitTarget.IsChecked = true;
        dateHabitTarget.Date = DateTime.Now.AddDays(7);
    }

    private void OnHabitTarget30Days(object sender, EventArgs e)
    {
        chkHasHabitTarget.IsChecked = true;
        dateHabitTarget.Date = DateTime.Now.AddDays(30);
    }

    private void OnHabitTarget90Days(object sender, EventArgs e)
    {
        chkHasHabitTarget.IsChecked = true;
        dateHabitTarget.Date = DateTime.Now.AddDays(90);
    }

    private void OnHabitTarget180Days(object sender, EventArgs e)
    {
        chkHasHabitTarget.IsChecked = true;
        dateHabitTarget.Date = DateTime.Now.AddDays(180);
    }

    private void OnHabitTarget1Year(object sender, EventArgs e)
    {
        chkHasHabitTarget.IsChecked = true;
        dateHabitTarget.Date = DateTime.Now.AddYears(1);
    }

    private void OnMeaningfulLevelChanged(object sender, TextChangedEventArgs e)
    {
        if (int.TryParse(e.NewTextValue, out int level))
        {
            if (level < 1) level = 1;
            if (level > 100) level = 100;

            int exp = level * 2;
            lblExpPreview.Text = $"→ This activity will give: +{exp} EXP";
        }
        else
        {
            lblExpPreview.Text = "→ This activity will give: +40 EXP";
        }
    }

    private void OnStartDateChecked(object sender, CheckedChangedEventArgs e)
    {
        dateStart.IsEnabled = e.Value;
        timeStart.IsEnabled = e.Value;
    }

    private void OnEndDateChecked(object sender, CheckedChangedEventArgs e)
    {
        dateEnd.IsEnabled = e.Value;
        timeEnd.IsEnabled = e.Value;
    }

    private void OnAdd1Day(object sender, EventArgs e)
    {
        if (chkEndDate.IsChecked)
            dateEnd.Date = dateEnd.Date.AddDays(1);
    }

    private void OnAdd7Days(object sender, EventArgs e)
    {
        if (chkEndDate.IsChecked)
            dateEnd.Date = dateEnd.Date.AddDays(7);
    }

    private void OnAdd30Days(object sender, EventArgs e)
    {
        if (chkEndDate.IsChecked)
            dateEnd.Date = dateEnd.Date.AddDays(30);
    }

    private void OnAdd90Days(object sender, EventArgs e)
    {
        if (chkEndDate.IsChecked)
            dateEnd.Date = dateEnd.Date.AddDays(90);
    }

    private void OnAdd180Days(object sender, EventArgs e)
    {
        if (chkEndDate.IsChecked)
            dateEnd.Date = dateEnd.Date.AddDays(180);
    }

    private void OnAdd1Year(object sender, EventArgs e)
    {
        if (chkEndDate.IsChecked)
            dateEnd.Date = dateEnd.Date.AddYears(1);
    }

    private async void OnBrowseImage(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                FileTypes = FilePickerFileType.Images,
                PickerTitle = "Choose an activity image"
            });

            if (result != null)
            {
                string destFolder = GetImagesFolderPath();
                Directory.CreateDirectory(destFolder);

                string filename = $"activity_{DateTime.Now.Ticks}{Path.GetExtension(result.FileName)}";
                string fullPath = Path.Combine(destFolder, filename);

                using var sourceStream = await result.OpenReadAsync();
                using var destStream = File.Create(fullPath);
                await sourceStream.CopyToAsync(destStream);

                _selectedImageFilename = filename;
                imgActivityPreview.Source = ImageSource.FromFile(fullPath);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load image: {ex.Message}", "OK");
        }
    }

    private async void OnSetToBlack(object sender, EventArgs e)
    {
        try
        {
            string destFolder = GetImagesFolderPath();
            Directory.CreateDirectory(destFolder);

            string filename = $"black_{DateTime.Now.Ticks}.png";
            string fullPath = Path.Combine(destFolder, filename);

            // Create a 1x1 black PNG image
            using (var stream = File.Create(fullPath))
            {
                byte[] blackPng = new byte[]
                {
                    0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, // PNG signature
                    0x00, 0x00, 0x00, 0x0D, // IHDR length
                    0x49, 0x48, 0x44, 0x52, // IHDR
                    0x00, 0x00, 0x00, 0x01, // width = 1
                    0x00, 0x00, 0x00, 0x01, // height = 1
                    0x08, 0x02, // bit depth = 8, color type = 2 (RGB)
                    0x00, 0x00, 0x00, // compression, filter, interlace
                    0x90, 0x77, 0x53, 0xDE, // IHDR CRC
                    0x00, 0x00, 0x00, 0x0C, // IDAT length
                    0x49, 0x44, 0x41, 0x54, // IDAT
                    0x08, 0xD7, 0x63, 0x60, 0x60, 0x60, 0x00, 0x00, 0x00, 0x04, 0x00, 0x01, // compressed black pixel
                    0x27, 0x34, 0x27, 0x0A, // IDAT CRC
                    0x00, 0x00, 0x00, 0x00, // IEND length
                    0x49, 0x45, 0x4E, 0x44, // IEND
                    0xAE, 0x42, 0x60, 0x82  // IEND CRC
                };
                await stream.WriteAsync(blackPng, 0, blackPng.Length);
            }

            _selectedImageFilename = filename;
            imgActivityPreview.Source = ImageSource.FromFile(fullPath);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not create black image: {ex.Message}", "OK");
        }
    }

    private async void OnAiGenerators(object sender, EventArgs e)
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

    private async void OnDownloadFromUrl(object sender, EventArgs e)
    {
        string url = txtImageUrl.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(url))
        {
            await DisplayAlert("Required", "Please enter an image URL.", "OK");
            return;
        }

        try
        {
            using var httpClient = new HttpClient();
            var imageBytes = await httpClient.GetByteArrayAsync(url);

            string destFolder = GetImagesFolderPath();
            Directory.CreateDirectory(destFolder);

            string extension = Path.GetExtension(url);
            if (string.IsNullOrEmpty(extension))
                extension = ".png";

            string filename = $"activity_{DateTime.Now.Ticks}{extension}";
            string fullPath = Path.Combine(destFolder, filename);

            await File.WriteAllBytesAsync(fullPath, imageBytes);

            _selectedImageFilename = filename;
            imgActivityPreview.Source = ImageSource.FromFile(fullPath);

            await DisplayAlert("Success", "Image downloaded successfully!", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not download image: {ex.Message}", "OK");
        }
    }

    private async void OnAddActivity(object sender, EventArgs e)
    {
        string activityName = txtActivityName.Text?.Trim() ?? "";
        string category = pickerCategory.SelectedItem?.ToString() ?? "Misc";

        if (string.IsNullOrEmpty(activityName))
        {
            await DisplayAlert("Required", "Please enter an activity name.", "OK");
            return;
        }

        bool isPercentType = pickerRewardType.SelectedIndex == 1;
        bool newStreakTracked = chkStreakTracked.IsChecked;

        if (isPercentType)
        {
            // Percent of level type
            if (!double.TryParse(txtPercentOfLevel.Text, out double percent) || percent <= 0)
            {
                await DisplayAlert("Required", "Please enter a valid percent (e.g., 1 for 1%)", "OK");
                return;
            }
            
            int cutoff = 100;
            if (!int.TryParse(txtPercentCutoff.Text, out cutoff) || cutoff < 1 || cutoff > 100)
            {
                cutoff = 100;
            }

            _activity.RewardType = "PercentOfLevel";
            _activity.PercentOfLevel = percent;
            _activity.PercentCutoffLevel = cutoff;
            _activity.ExpGain = 0; // Dynamic calculation
            _activity.MeaningfulUntilLevel = 100; // Always meaningful for percent type
        }
        else
        {
            // Fixed EXP type
            if (!int.TryParse(txtMeaningfulLevel.Text, out int meaningfulLevel))
            {
                meaningfulLevel = 20;
            }

            if (meaningfulLevel < 1) meaningfulLevel = 1;
            if (meaningfulLevel > 100) meaningfulLevel = 100;

            _activity.RewardType = "Fixed";
            _activity.MeaningfulUntilLevel = meaningfulLevel;
            _activity.ExpGain = meaningfulLevel * 2;
        }

        // Combine date and time into single DateTime values
        DateTime? startDateTime = chkStartDate.IsChecked
            ? dateStart.Date.Add(timeStart.Time)
            : null;

        DateTime? endDateTime = chkEndDate.IsChecked
            ? dateEnd.Date.Add(timeEnd.Time)
            : null;

        try
        {
            // *** HANDLE STREAK CONTAINER CONVERSION ***
            bool convertingToStreak = newStreakTracked && !_wasStreakTracked;
            bool convertingFromStreak = !newStreakTracked && _wasStreakTracked && _wasStreakContainer;

            if (convertingToStreak)
            {
                // Converting TO a streak container
                bool confirm = await DisplayAlert(
                    "Convert to Streak Activity?",
                    $"This will:\n" +
                    $"• Make '{activityName}' its own category\n" +
                    $"• Track each day as a streak attempt\n" +
                    $"• Start your first attempt automatically\n\n" +
                    $"Continue?",
                    "Convert",
                    "Cancel");

                if (!confirm)
                {
                    chkStreakTracked.IsChecked = false;
                    return;
                }

                _activity.OriginalCategory = category;
                _activity.Category = activityName; // Activity becomes its own category
                _activity.IsStreakContainer = true;
                _activity.IsStreakTracked = true;
                _activity.NoHabitTarget = true; // Streak containers don't need habit targets
            }
            else if (convertingFromStreak)
            {
                // Converting FROM a streak container back to normal
                _activity.Category = category;
                _activity.IsStreakContainer = false;
                _activity.IsStreakTracked = false;
                // Keep OriginalCategory for reference
            }
            else
            {
                // Normal update - but handle category correctly for existing streak containers
                if (_activity.IsStreakContainer && newStreakTracked)
                {
                    // Still a streak container - category should stay as the activity name
                    _activity.Category = activityName;
                    // But update OriginalCategory if user changed the picker
                    _activity.OriginalCategory = category;
                }
                else
                {
                    _activity.Category = category;
                }
                _activity.IsStreakTracked = newStreakTracked;
            }

            // Update other fields
            _activity.Name = activityName;
            _activity.ImagePath = _selectedImageFilename ?? "";
            _activity.StartDate = startDateTime;
            _activity.EndDate = endDateTime;
            _activity.IsPossible = chkIsPossible.IsChecked;
            _activity.ShowTimesCompletedBadge = chkShowTimesCompleted.IsChecked;
            
            // Only update habit target if not a streak container
            if (!_activity.IsStreakContainer)
            {
                _activity.NoHabitTarget = chkNoHabitTarget.IsChecked;
                
                // Handle habit target date with tracking
                var newTargetDate = chkHasHabitTarget.IsChecked ? dateHabitTarget.Date : (DateTime?)null;
                
                if (newTargetDate.HasValue)
                {
                    if (!_activity.HabitTargetFirstSet.HasValue)
                    {
                        _activity.HabitTargetFirstSet = DateTime.Now;
                    }
                    else if (_activity.HabitTargetDate.HasValue && newTargetDate.Value > _activity.HabitTargetDate.Value)
                    {
                        _activity.HabitTargetPostponeCount++;
                    }
                    _activity.HabitTargetDate = newTargetDate;
                }
                else
                {
                    _activity.HabitTargetDate = null;
                }
            }

            await _activities.UpdateActivityAsync(_activity);

            // Start first streak attempt if converting to streak container
            if (convertingToStreak && _streaks != null)
            {
                await _streaks.StartNewStreakAsync(
                    _auth.CurrentUsername,
                    _gameId,
                    _activity.Id,
                    _activity.Name);

                // Flag that ActivityGamePage should reload categories
                if (Navigation.ModalStack.Count > 0)
                {
                    var navPage = Navigation.ModalStack[0] as NavigationPage;
                    // The parent page will reload categories automatically via OnAppearing
                }

                await DisplayAlert("Streak Activity Created! 🔥",
                    $"'{activityName}' is now a streak activity.\n\n" +
                    $"Navigate to its category to see your attempts and record daily progress.",
                    "OK");
            }

            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not update activity: {ex.Message}", "OK");
        }
    }

    private async void OnCancel(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }

    private static string GetImagesFolderPath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
    }

    private static string GetFullImagePath(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return "";

        // If it's already a full path (old data), return as-is
        if (Path.IsPathRooted(filename))
            return filename;

        // Otherwise construct full path from filename
        return Path.Combine(GetImagesFolderPath(), filename);
    }
}

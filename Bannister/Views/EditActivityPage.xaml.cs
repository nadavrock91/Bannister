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
    private readonly string _gameId;
    private readonly Activity _activity;
    private string? _selectedImageFilename = null;
    private DisplayDaysSelector? _displayDaysSelector = null;

    public EditActivityPage(AuthService auth, ActivityService activities, string gameId, Activity activity)
    {
        InitializeComponent();
        _auth = auth;
        _activities = activities;
        _gameId = gameId;
        _activity = activity;
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
            .Select(a => a.Category ?? "Misc")
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        if (!categories.Contains("Misc"))
            categories.Insert(0, "Misc");

        pickerCategory.ItemsSource = categories;
        int catIndex = categories.IndexOf(_activity.Category ?? "Misc");
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

        // Add Display Days Selector dynamically (after Schedule section)
        AddDisplayDaysSelector();
        
        // Load display days values
        if (_displayDaysSelector != null)
        {
            _displayDaysSelector.LoadFromActivity(
                _activity.DisplayDaysOfWeek ?? "",
                _activity.DisplayDayOfMonth);
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

    /// <summary>
    /// Add DisplayDaysSelector to the UI dynamically
    /// </summary>
    private void AddDisplayDaysSelector()
    {
        // Find the main VerticalStackLayout (first child of ScrollView)
        if (Content is ScrollView scrollView && 
            scrollView.Content is VerticalStackLayout mainStack)
        {
            // Find the Schedule section index (look for "Schedule (optional)" label)
            int scheduleIndex = -1;
            for (int i = 0; i < mainStack.Children.Count; i++)
            {
                if (mainStack.Children[i] is VerticalStackLayout section)
                {
                    var firstChild = section.Children.FirstOrDefault();
                    if (firstChild is Label label && label.Text == "Schedule (optional)")
                    {
                        scheduleIndex = i;
                        break;
                    }
                }
            }
            
            if (scheduleIndex >= 0)
            {
                // Create and insert DisplayDaysSelector after Schedule section
                _displayDaysSelector = new DisplayDaysSelector();
                mainStack.Children.Insert(scheduleIndex + 1, _displayDaysSelector.Container);
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

    private void OnLevelCapChanged(object sender, CheckedChangedEventArgs e)
    {
        // Could add visual feedback or enable/disable related controls here if needed
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
            // Update existing activity
            _activity.Name = activityName;
            _activity.Category = category;
            _activity.ImagePath = _selectedImageFilename ?? "";
            _activity.StartDate = startDateTime;
            _activity.EndDate = endDateTime;
            _activity.IsStreakTracked = chkStreakTracked.IsChecked;
            _activity.IsPossible = chkIsPossible.IsChecked;
            _activity.ShowTimesCompletedBadge = chkShowTimesCompleted.IsChecked;
            _activity.NoHabitTarget = chkNoHabitTarget.IsChecked;
            
            // Handle habit target date with tracking
            var newTargetDate = chkHasHabitTarget.IsChecked ? dateHabitTarget.Date : (DateTime?)null;
            
            if (newTargetDate.HasValue)
            {
                // Setting a target date
                if (!_activity.HabitTargetFirstSet.HasValue)
                {
                    // First time setting target
                    _activity.HabitTargetFirstSet = DateTime.Now;
                }
                else if (_activity.HabitTargetDate.HasValue && newTargetDate.Value > _activity.HabitTargetDate.Value)
                {
                    // Postponing to a later date
                    _activity.HabitTargetPostponeCount++;
                }
                _activity.HabitTargetDate = newTargetDate;
            }
            else
            {
                _activity.HabitTargetDate = null;
            }

            // Save display days settings
            if (_displayDaysSelector != null)
            {
                _activity.DisplayDaysOfWeek = _displayDaysSelector.GetDisplayDaysOfWeek();
                _activity.DisplayDayOfMonth = _displayDaysSelector.GetDisplayDayOfMonth();
            }

            await _activities.UpdateActivityAsync(_activity);

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

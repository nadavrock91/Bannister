using System;
using System.Collections.Generic;
using System.Linq;
using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;

namespace Bannister.Views;

public partial class BatchAddActivitiesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly string _gameId;
    private List<string> _categories = new();
    private List<BatchActivityRow> _activityRows = new();

    public BatchAddActivitiesPage(AuthService auth, ActivityService activities, string gameId)
    {
        InitializeComponent();
        _auth = auth;
        _activities = activities;
        _gameId = gameId;
        LoadCategories();
    }

    private async void LoadCategories()
    {
        var existingActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _gameId);
        _categories = existingActivities
            .Select(a => a.Category ?? "Misc")
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        if (!_categories.Contains("Misc"))
            _categories.Insert(0, "Misc");

        pickerDefaultCategory.ItemsSource = _categories;
        pickerDefaultCategory.SelectedIndex = 0;
    }

    private void OnContinueClicked(object sender, EventArgs e)
    {
        if (!int.TryParse(txtRowCount.Text, out int rowCount) || rowCount < 1 || rowCount > 100)
        {
            DisplayAlert("Invalid", "Please enter a number between 1 and 100", "OK");
            return;
        }

        // Hide the initial prompt, show the grid
        ((VerticalStackLayout)((Grid)Content).Children[0]).IsVisible = false;
        defaultsFrame.IsVisible = true;
        gridBorder.IsVisible = true;
        footerSection.IsVisible = true;

        BuildGrid(rowCount);
    }

    private void BuildGrid(int rowCount)
    {
        activitiesGrid.Children.Clear();
        activitiesGrid.RowDefinitions.Clear();
        activitiesGrid.ColumnDefinitions.Clear();
        _activityRows.Clear();

        // Define columns
        activitiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });   // Row number
        activitiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });  // Activity Name
        activitiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });  // Category
        activitiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });   // Cutoff
        activitiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });  // Start Date
        activitiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });  // End Date
        activitiesGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(250) });  // Image Path

        // Header row
        activitiesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        AddHeaderCell(0, 0, "");
        AddHeaderCell(0, 1, "Activity Name");
        AddHeaderCell(0, 2, "Category");
        AddHeaderCell(0, 3, "Cutoff");
        AddHeaderCell(0, 4, "Start Date");
        AddHeaderCell(0, 5, "End Date");
        AddHeaderCell(0, 6, "Image Path (paste full path)");

        // Data rows
        for (int i = 0; i < rowCount; i++)
        {
            int rowIndex = i + 1;
            activitiesGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var activityRow = new BatchActivityRow();
            _activityRows.Add(activityRow);

            // Row number
            var rowLabel = new Label
            {
                Text = $"{i + 1}",
                BackgroundColor = Color.FromArgb("#F5F5F5"),
                Padding = new Thickness(8),
                VerticalOptions = LayoutOptions.Fill,
                VerticalTextAlignment = TextAlignment.Center,
                HorizontalTextAlignment = TextAlignment.Center
            };
            Grid.SetRow(rowLabel, rowIndex);
            Grid.SetColumn(rowLabel, 0);
            activitiesGrid.Children.Add(rowLabel);

            // Activity Name
            activityRow.NameEntry = CreateEntry("Enter name...", rowIndex, 1);

            // Category Picker
            activityRow.CategoryPicker = CreatePicker(_categories, rowIndex, 2);

            // Cutoff Entry
            activityRow.CutoffEntry = CreateEntry("20", rowIndex, 3, Keyboard.Numeric);

            // Start Date Entry
            activityRow.StartDateEntry = CreateEntry("Optional", rowIndex, 4);

            // End Date Entry
            activityRow.EndDateEntry = CreateEntry("Optional", rowIndex, 5);

            // Image Path Entry
            activityRow.ImagePathEntry = CreateEntry("Optional image path", rowIndex, 6);
        }

        // Select first cell
        if (_activityRows.Count > 0)
            _activityRows[0].NameEntry?.Focus();
    }

    private void AddHeaderCell(int row, int col, string text)
    {
        var label = new Label
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Color.FromArgb("#E9EBFF"),
            Padding = new Thickness(8),
            VerticalOptions = LayoutOptions.Fill,
            VerticalTextAlignment = TextAlignment.Center
        };

        var border = new Border
        {
            Stroke = Color.FromArgb("#DDD"),
            StrokeThickness = 1,
            Content = label
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        activitiesGrid.Children.Add(border);
    }

    private Entry CreateEntry(string placeholder, int row, int col, Keyboard? keyboard = null)
    {
        var entry = new Entry
        {
            Placeholder = placeholder,
            BackgroundColor = Colors.White,
            Keyboard = keyboard ?? Keyboard.Default,
            MinimumWidthRequest = 80
        };

        var border = new Border
        {
            Stroke = Color.FromArgb("#DDD"),
            StrokeThickness = 1,
            Content = entry
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        activitiesGrid.Children.Add(border);  // ? FIX: Actually add the border to the grid!

        return entry;
    }

    private Picker CreatePicker(List<string> items, int row, int col)
    {
        var picker = new Picker
        {
            ItemsSource = items,
            BackgroundColor = Colors.White,
            MinimumWidthRequest = 100
        };

        var border = new Border
        {
            Stroke = Color.FromArgb("#DDD"),
            StrokeThickness = 1,
            Content = picker
        };

        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
        activitiesGrid.Children.Add(border);  // ? FIX: Actually add the border to the grid!

        return picker;
    }

    private void OnAddRowsClicked(object sender, EventArgs e)
    {
        int currentCount = _activityRows.Count;
        BuildGrid(currentCount + 5);
    }

    private async void OnAddActivitiesClicked(object sender, EventArgs e)
    {
        string defaultCategory = pickerDefaultCategory.SelectedItem?.ToString() ?? "Misc";

        if (!int.TryParse(txtDefaultCutoff.Text, out int defaultCutoff))
            defaultCutoff = 20;

        var activitiesToAdd = new List<ActivityData>();

        foreach (var row in _activityRows)
        {
            string name = row.NameEntry?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(name))
                continue; // Skip empty rows

            string category = string.IsNullOrWhiteSpace(row.CategoryPicker?.SelectedItem?.ToString())
                ? defaultCategory
                : row.CategoryPicker.SelectedItem.ToString()!;

            int cutoff = defaultCutoff;
            if (int.TryParse(row.CutoffEntry?.Text, out int parsedCutoff))
                cutoff = Math.Clamp(parsedCutoff, 1, 100);

            DateTime? startDate = ParseDate(row.StartDateEntry?.Text);
            DateTime? endDate = ParseDate(row.EndDateEntry?.Text);
            string imagePath = row.ImagePathEntry?.Text?.Trim() ?? "";

            activitiesToAdd.Add(new ActivityData
            {
                Name = name,
                Category = category,
                MeaningfulUntilLevel = cutoff,
                ExpGain = cutoff * 2,
                StartDate = startDate,
                EndDate = endDate,
                ImagePath = imagePath
            });
        }

        if (activitiesToAdd.Count == 0)
        {
            await DisplayAlert("No Activities", "Please enter at least one activity name.", "OK");
            return;
        }

        bool confirm = await DisplayAlert(
            "Confirm Batch Add",
            $"Add {activitiesToAdd.Count} activities?",
            "Add",
            "Cancel");

        if (!confirm)
            return;

        try
        {
            int successCount = 0;
            foreach (var activity in activitiesToAdd)
            {
                try
                {
                    await _activities.CreateActivityAsync(
                        _auth.CurrentUsername,
                        _gameId,
                        activity.Name,
                        activity.ExpGain,
                        activity.MeaningfulUntilLevel,
                        activity.Category,
                        activity.ImagePath);

                    // Set dates if provided
                    if (activity.StartDate.HasValue || activity.EndDate.HasValue)
                    {
                        var allActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, _gameId);
                        var createdActivity = allActivities.FirstOrDefault(a => a.Name == activity.Name);
                        if (createdActivity != null)
                        {
                            createdActivity.StartDate = activity.StartDate;
                            createdActivity.EndDate = activity.EndDate;
                            await _activities.UpdateActivityAsync(createdActivity);
                        }
                    }

                    successCount++;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to add '{activity.Name}': {ex.Message}");
                }
            }

            await DisplayAlert("Success", $"Added {successCount} of {activitiesToAdd.Count} activities!", "OK");
            await Navigation.PopModalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Batch add failed: {ex.Message}", "OK");
        }
    }

    private DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        if (DateTime.TryParse(dateStr, out DateTime parsed))
            return parsed;

        return null;
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}

public class BatchActivityRow
{
    public Entry? NameEntry { get; set; }
    public Picker? CategoryPicker { get; set; }
    public Entry? CutoffEntry { get; set; }
    public Entry? StartDateEntry { get; set; }
    public Entry? EndDateEntry { get; set; }
    public Entry? ImagePathEntry { get; set; }
}

public class ActivityData
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "Misc";
    public int MeaningfulUntilLevel { get; set; } = 20;
    public int ExpGain { get; set; } = 40;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string ImagePath { get; set; } = "";
}
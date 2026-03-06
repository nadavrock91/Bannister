using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bannister.Services;
using Bannister.Models;
using Microsoft.Maui.Controls;

namespace Bannister.Views;

[QueryProperty(nameof(GameId), "gameId")]
[QueryProperty(nameof(PrefillName), "prefillName")]
[QueryProperty(nameof(PrefillLevel), "prefillLevel")]
[QueryProperty(nameof(PrefillImage), "prefillImage")]
[QueryProperty(nameof(PrefillCategory), "prefillCategory")]
[QueryProperty(nameof(IsNegativeStr), "isNegative")]
[QueryProperty(nameof(NoHabitTargetStr), "noHabitTarget")]
[QueryProperty(nameof(PrefillStartDate), "prefillStartDate")]
[QueryProperty(nameof(PrefillEndDate), "prefillEndDate")]
public class ActivityCreationPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly GameService _games;

    private string _gameId = "";
    private string? _selectedImageFilename = null;
    private List<string> _categories = new();
    
    // For modal usage - returns created activity
    private TaskCompletionSource<Activity?>? _modalTcs;
    private bool _isModalMode = false;

    // UI Controls
    private Entry txtName;
    private Entry entryCategory;
    private CollectionView categorySuggestions;
    private Entry txtMeaningful;
    private Label lblExpPreview;
    private Picker pickerRewardType;
    private VerticalStackLayout fixedExpSection;
    private VerticalStackLayout percentExpSection;
    private Entry txtPercentOfLevel;
    private Entry txtPercentCutoff;
    private CheckBox chkStartDate;
    private DatePicker dateStart;
    private TimePicker timeStart;
    private CheckBox chkEndDate;
    private DatePicker dateEnd;
    private TimePicker timeEnd;
    private Button btnPickImage;
    private Image imgPreview;
    private CheckBox chkStreakTracked;
    private CheckBox chkIsPossible;
    private CheckBox chkShowTimesCompleted;
    private CheckBox chkNoHabitTarget;
    private CheckBox chkHasHabitTarget;
    private DatePicker dateHabitTarget;
    
    // Display days selector
    private DisplayDaysSelector _displayDaysSelector;

    public string GameId
    {
        get => _gameId;
        set
        {
            _gameId = value;
            OnPropertyChanged();
        }
    }

    // Query property setters for Shell navigation
    public string? PrefillName
    {
        get => _prefillName;
        set => _prefillName = Uri.UnescapeDataString(value ?? "");
    }

    public string? PrefillLevel
    {
        get => _prefillLevel;
        set => _prefillLevel = value;
    }

    public string? PrefillImage
    {
        get => _prefillImage;
        set => _prefillImage = Uri.UnescapeDataString(value ?? "");
    }

    public string? PrefillCategory
    {
        get => _prefillCategory;
        set => _prefillCategory = Uri.UnescapeDataString(value ?? "");
    }

    public string? IsNegativeStr
    {
        get => _isNegative ? "true" : "false";
        set => _isNegative = value?.ToLower() == "true";
    }

    public string? NoHabitTargetStr
    {
        get => _noHabitTarget ? "true" : "false";
        set => _noHabitTarget = value?.ToLower() == "true";
    }

    public string? PrefillStartDate
    {
        get => _prefillStartDate?.ToString("o");
        set
        {
            if (!string.IsNullOrEmpty(value) && DateTime.TryParse(value, out var dt))
                _prefillStartDate = dt;
        }
    }

    public string? PrefillEndDate
    {
        get => _prefillEndDate?.ToString("o");
        set
        {
            if (!string.IsNullOrEmpty(value) && DateTime.TryParse(value, out var dt))
                _prefillEndDate = dt;
        }
    }

    public ActivityCreationPage(AuthService auth, ActivityService activities, GameService games)
    {
        _auth = auth;
        _activities = activities;
        _games = games;
        
        Title = "Add Activity";
        BuildUI();
    }

    /// <summary>
    /// Opens the page as a modal and returns the created activity (or null if cancelled)
    /// </summary>
    public static async Task<Activity?> CreateActivityModalAsync(
        INavigation navigation, 
        AuthService auth, 
        ActivityService activities, 
        GameService games,
        string gameId,
        string? prefillName = null,
        string? prefillLevel = null,
        string? prefillImage = null,
        string? prefillCategory = null,
        bool isNegative = false,
        bool noHabitTarget = false,
        DateTime? prefillStartDate = null,
        DateTime? prefillEndDate = null)
    {
        var page = new ActivityCreationPage(auth, activities, games);
        page._isModalMode = true;
        page._modalTcs = new TaskCompletionSource<Activity?>();
        page.GameId = gameId;
        page._prefillName = prefillName;
        page._prefillLevel = prefillLevel;
        page._prefillImage = prefillImage;
        page._prefillCategory = prefillCategory;
        page._isNegative = isNegative;
        page._noHabitTarget = noHabitTarget;
        page._prefillStartDate = prefillStartDate;
        page._prefillEndDate = prefillEndDate;
        
        await navigation.PushModalAsync(new NavigationPage(page));
        return await page._modalTcs.Task;
    }
    
    // Prefill storage
    private string? _prefillName;
    private string? _prefillLevel;
    private string? _prefillImage;
    private string? _prefillCategory;
    private bool _isNegative;
    private bool _noHabitTarget;
    private DateTime? _prefillStartDate;
    private DateTime? _prefillEndDate;
    private bool _prefillApplied = false;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Apply prefill if set
            if (!_prefillApplied)
            {
                if (!string.IsNullOrEmpty(_prefillName))
                    txtName.Text = _prefillName;
                
                if (!string.IsNullOrEmpty(_prefillLevel) && int.TryParse(_prefillLevel, out int level))
                    txtMeaningful.Text = Math.Abs(level).ToString();
                
                if (!string.IsNullOrEmpty(_prefillCategory))
                    entryCategory.Text = _prefillCategory;
                else if (_isNegative)
                    entryCategory.Text = "Negative";
                
                if (!string.IsNullOrEmpty(_prefillImage))
                {
                    _selectedImageFilename = _prefillImage;
                    string fullPath = Path.Combine(GetImagesFolderPath(), _prefillImage);
                    if (File.Exists(fullPath))
                    {
                        imgPreview.Source = ImageSource.FromFile(fullPath);
                        imgPreview.IsVisible = true;
                    }
                }
                
                // Set "no habit target" checkbox
                if (_noHabitTarget || _isNegative)
                {
                    chkNoHabitTarget.IsChecked = true;
                }
                
                // Set start date if prefilled
                if (_prefillStartDate.HasValue)
                {
                    chkStartDate.IsChecked = true;
                    dateStart.Date = _prefillStartDate.Value.Date;
                    timeStart.Time = _prefillStartDate.Value.TimeOfDay;
                }
                
                // Set end date if prefilled
                if (_prefillEndDate.HasValue)
                {
                    chkEndDate.IsChecked = true;
                    dateEnd.Date = _prefillEndDate.Value.Date;
                    timeEnd.Time = _prefillEndDate.Value.TimeOfDay;
                }
                
                _prefillApplied = true;
            }

            // Load existing categories
            var existingActivities = await _activities.GetActivitiesAsync(_auth.CurrentUsername, GameId);
            _categories = existingActivities
                .Select(a => a.Category ?? "Misc")
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            // Ensure Misc and Negative are always in the list
            if (!_categories.Contains("Misc"))
                _categories.Insert(0, "Misc");
            if (!_categories.Contains("Negative"))
                _categories.Add("Negative");

            // Set up category suggestions
            categorySuggestions.ItemsSource = _categories;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading categories: {ex.Message}");
        }
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        // Activity Name
        mainStack.Children.Add(new Label
        {
            Text = "Activity Name",
            FontAttributes = FontAttributes.Bold
        });

        txtName = new Entry
        {
            Placeholder = "Enter activity name"
        };
        mainStack.Children.Add(txtName);

        // Category with suggestions
        mainStack.Children.Add(new Label
        {
            Text = "Category (type new or select existing)",
            FontAttributes = FontAttributes.Bold
        });

        entryCategory = new Entry
        {
            Placeholder = "Type category name"
        };
        entryCategory.TextChanged += OnCategoryTextChanged;
        mainStack.Children.Add(entryCategory);

        // Category suggestions
        categorySuggestions = new CollectionView
        {
            HeightRequest = 80,
            SelectionMode = SelectionMode.Single,
            ItemTemplate = new DataTemplate(() =>
            {
                var label = new Label
                {
                    Padding = new Thickness(8, 4),
                    TextColor = Color.FromArgb("#5B63EE")
                };
                label.SetBinding(Label.TextProperty, ".");
                return label;
            })
        };
        categorySuggestions.SelectionChanged += OnCategorySuggestionSelected;
        mainStack.Children.Add(categorySuggestions);

        // Reward Type Picker
        mainStack.Children.Add(new Label
        {
            Text = "Reward Type",
            FontAttributes = FontAttributes.Bold
        });

        pickerRewardType = new Picker
        {
            Title = "Select reward type",
            ItemsSource = new List<string>
            {
                "Fixed EXP (based on level cutoff)",
                "Percent of Current Level"
            },
            SelectedIndex = 0
        };
        pickerRewardType.SelectedIndexChanged += OnRewardTypeChanged;
        mainStack.Children.Add(pickerRewardType);

        // Fixed EXP Section (default visible)
        fixedExpSection = new VerticalStackLayout { Spacing = 8 };

        // Meaningful Level
        fixedExpSection.Children.Add(new Label
        {
            Text = "Meaningful until level (1-100)",
            FontAttributes = FontAttributes.Bold
        });

        var meaningfulStack = new HorizontalStackLayout { Spacing = 8 };

        txtMeaningful = new Entry
        {
            Placeholder = "20",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 100
        };
        txtMeaningful.TextChanged += OnMeaningfulChanged;
        meaningfulStack.Children.Add(txtMeaningful);

        lblExpPreview = new Label
        {
            Text = "→ This activity will give: +40 EXP",
            TextColor = Color.FromArgb("#5B63EE"),
            VerticalOptions = LayoutOptions.Center
        };
        meaningfulStack.Children.Add(lblExpPreview);

        fixedExpSection.Children.Add(meaningfulStack);
        mainStack.Children.Add(fixedExpSection);

        // Percent of Level Section (hidden by default)
        percentExpSection = new VerticalStackLayout { Spacing = 8, IsVisible = false };

        percentExpSection.Children.Add(new Label
        {
            Text = "Percent of current level's EXP",
            FontAttributes = FontAttributes.Bold
        });

        var percentStack = new HorizontalStackLayout { Spacing = 8 };

        txtPercentOfLevel = new Entry
        {
            Placeholder = "1",
            Text = "1",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 100
        };
        percentStack.Children.Add(txtPercentOfLevel);

        percentStack.Children.Add(new Label
        {
            Text = "% (e.g., 1 = 1% of level span)",
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });

        percentExpSection.Children.Add(percentStack);

        // Cutoff level
        percentExpSection.Children.Add(new Label
        {
            Text = "Stop scaling at level (1-100)",
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        });

        var cutoffStack = new HorizontalStackLayout { Spacing = 8 };

        txtPercentCutoff = new Entry
        {
            Placeholder = "100",
            Text = "100",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 100
        };
        cutoffStack.Children.Add(txtPercentCutoff);

        cutoffStack.Children.Add(new Label
        {
            Text = "(after this level, EXP stays fixed)",
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        });

        percentExpSection.Children.Add(cutoffStack);

        percentExpSection.Children.Add(new Label
        {
            Text = "💡 EXP scales with your level until cutoff, then stays fixed",
            FontSize = 12,
            TextColor = Color.FromArgb("#4CAF50"),
            FontAttributes = FontAttributes.Italic
        });

        mainStack.Children.Add(percentExpSection);

        // Schedule Section
        mainStack.Children.Add(new Label
        {
            Text = "Schedule (optional)",
            FontAttributes = FontAttributes.Bold
        });

        // Start Date
        var startDateStack = new HorizontalStackLayout { Spacing = 8 };

        chkStartDate = new CheckBox();
        chkStartDate.CheckedChanged += (s, e) =>
        {
            dateStart.IsEnabled = e.Value;
            timeStart.IsEnabled = e.Value;
        };
        startDateStack.Children.Add(chkStartDate);

        startDateStack.Children.Add(new Label
        {
            Text = "Start Date:",
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 100
        });

        dateStart = new DatePicker
        {
            Date = DateTime.Today,
            IsEnabled = false
        };
        startDateStack.Children.Add(dateStart);

        timeStart = new TimePicker
        {
            Time = DateTime.Now.TimeOfDay,
            IsEnabled = false
        };
        startDateStack.Children.Add(timeStart);

        mainStack.Children.Add(startDateStack);

        // End Date
        var endDateStack = new HorizontalStackLayout { Spacing = 8 };

        chkEndDate = new CheckBox();
        chkEndDate.CheckedChanged += (s, e) =>
        {
            dateEnd.IsEnabled = e.Value;
            timeEnd.IsEnabled = e.Value;
        };
        endDateStack.Children.Add(chkEndDate);

        endDateStack.Children.Add(new Label
        {
            Text = "End Date:",
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 100
        });

        dateEnd = new DatePicker
        {
            Date = DateTime.Today,
            IsEnabled = false
        };
        endDateStack.Children.Add(dateEnd);

        timeEnd = new TimePicker
        {
            Time = DateTime.Now.TimeOfDay,
            IsEnabled = false
        };
        endDateStack.Children.Add(timeEnd);

        mainStack.Children.Add(endDateStack);

        // Quick add buttons
        mainStack.Children.Add(new Label
        {
            Text = "Quick add to end date:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        var quickAddGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        AddQuickButton(quickAddGrid, "+1 day", 1, 0, 0);
        AddQuickButton(quickAddGrid, "+7 days", 7, 1, 0);
        AddQuickButton(quickAddGrid, "+30 days", 30, 2, 0);
        AddQuickButton(quickAddGrid, "+90 days", 90, 3, 0);
        AddQuickButton(quickAddGrid, "+180 days", 180, 4, 0);
        AddQuickButton(quickAddGrid, "+1 year", 365, 0, 1);

        mainStack.Children.Add(quickAddGrid);

        // Streak Tracking
        var streakSection = new VerticalStackLayout { Spacing = 8 };
        streakSection.Children.Add(new Label
        {
            Text = "Streak Tracking",
            FontAttributes = FontAttributes.Bold
        });
        
        var streakRow = new HorizontalStackLayout { Spacing = 12 };
        chkStreakTracked = new CheckBox();
        streakRow.Children.Add(chkStreakTracked);
        streakRow.Children.Add(new Label
        {
            Text = "Track consecutive days as streak attempts",
            VerticalOptions = LayoutOptions.Center
        });
        streakSection.Children.Add(streakRow);
        
        streakSection.Children.Add(new Label
        {
            Text = "🔥 When enabled, consecutive days of using this activity will be tracked as streaks",
            FontSize = 12,
            TextColor = Color.FromArgb("#FF9800"),
            FontAttributes = FontAttributes.Italic
        });
        mainStack.Children.Add(streakSection);

        // Activity Status (Possible)
        var statusSection = new VerticalStackLayout { Spacing = 8 };
        statusSection.Children.Add(new Label
        {
            Text = "Activity Status",
            FontAttributes = FontAttributes.Bold
        });
        
        var possibleRow = new HorizontalStackLayout { Spacing = 12 };
        chkIsPossible = new CheckBox();
        possibleRow.Children.Add(chkIsPossible);
        possibleRow.Children.Add(new Label
        {
            Text = "Mark as Possible (idea for future)",
            VerticalOptions = LayoutOptions.Center
        });
        statusSection.Children.Add(possibleRow);
        
        statusSection.Children.Add(new Label
        {
            Text = "💡 Possible activities won't show in normal filters - only under 'Possible' filter",
            FontSize = 12,
            TextColor = Color.FromArgb("#9C27B0"),
            FontAttributes = FontAttributes.Italic
        });

        // Show Times Completed Badge
        var timesCompletedRow = new HorizontalStackLayout { Spacing = 12 };
        chkShowTimesCompleted = new CheckBox();
        timesCompletedRow.Children.Add(chkShowTimesCompleted);
        timesCompletedRow.Children.Add(new Label
        {
            Text = "Show times completed badge (✓X)",
            VerticalOptions = LayoutOptions.Center
        });
        statusSection.Children.Add(timesCompletedRow);
        
        statusSection.Children.Add(new Label
        {
            Text = "💡 Badge shows how many times you've gained EXP from this activity",
            FontSize = 12,
            TextColor = Color.FromArgb("#9C27B0"),
            FontAttributes = FontAttributes.Italic
        });

        mainStack.Children.Add(statusSection);

        // Habit Target Date Section
        var habitTargetSection = new VerticalStackLayout { Spacing = 8 };
        habitTargetSection.Children.Add(new Label
        {
            Text = "Habit Target (required)",
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#D32F2F")
        });
        
        // Option 1: Don't want as habit
        var noHabitRow = new HorizontalStackLayout { Spacing = 12 };
        chkNoHabitTarget = new CheckBox();
        chkNoHabitTarget.CheckedChanged += (s, e) => {
            if (e.Value)
            {
                chkHasHabitTarget.IsChecked = false;
                dateHabitTarget.IsEnabled = false;
            }
        };
        noHabitRow.Children.Add(chkNoHabitTarget);
        noHabitRow.Children.Add(new Label
        {
            Text = "I don't want this as a habit",
            VerticalOptions = LayoutOptions.Center
        });
        habitTargetSection.Children.Add(noHabitRow);
        
        // Option 2: Set target date
        var hasTargetRow = new HorizontalStackLayout { Spacing = 12 };
        chkHasHabitTarget = new CheckBox();
        chkHasHabitTarget.CheckedChanged += (s, e) => {
            dateHabitTarget.IsEnabled = e.Value;
            if (e.Value)
            {
                chkNoHabitTarget.IsChecked = false;
            }
        };
        hasTargetRow.Children.Add(chkHasHabitTarget);
        hasTargetRow.Children.Add(new Label
        {
            Text = "Set habit target date:",
            VerticalOptions = LayoutOptions.Center
        });
        dateHabitTarget = new DatePicker
        {
            Date = DateTime.Now.AddDays(1),
            MinimumDate = DateTime.Now.AddDays(1),
            IsEnabled = false
        };
        hasTargetRow.Children.Add(dateHabitTarget);
        habitTargetSection.Children.Add(hasTargetRow);
        
        // Quick add buttons for habit target
        habitTargetSection.Children.Add(new Label
        {
            Text = "Quick add to target date:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            Margin = new Thickness(0, 4, 0, 0)
        });
        
        var habitQuickButtonsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 4
        };
        
        var btnHabit7 = new Button { Text = "+7d", BackgroundColor = Color.FromArgb("#FFF3E0"), TextColor = Color.FromArgb("#E65100"), CornerRadius = 6, Padding = new Thickness(4, 6) };
        btnHabit7.Clicked += (s, e) => { chkHasHabitTarget.IsChecked = true; dateHabitTarget.Date = dateHabitTarget.Date.AddDays(7); };
        Grid.SetColumn(btnHabit7, 0);
        habitQuickButtonsGrid.Children.Add(btnHabit7);
        
        var btnHabit30 = new Button { Text = "+30d", BackgroundColor = Color.FromArgb("#FFF3E0"), TextColor = Color.FromArgb("#E65100"), CornerRadius = 6, Padding = new Thickness(4, 6) };
        btnHabit30.Clicked += (s, e) => { chkHasHabitTarget.IsChecked = true; dateHabitTarget.Date = dateHabitTarget.Date.AddDays(30); };
        Grid.SetColumn(btnHabit30, 1);
        habitQuickButtonsGrid.Children.Add(btnHabit30);
        
        var btnHabit90 = new Button { Text = "+90d", BackgroundColor = Color.FromArgb("#FFF3E0"), TextColor = Color.FromArgb("#E65100"), CornerRadius = 6, Padding = new Thickness(4, 6) };
        btnHabit90.Clicked += (s, e) => { chkHasHabitTarget.IsChecked = true; dateHabitTarget.Date = dateHabitTarget.Date.AddDays(90); };
        Grid.SetColumn(btnHabit90, 2);
        habitQuickButtonsGrid.Children.Add(btnHabit90);
        
        var btnHabit180 = new Button { Text = "+180d", BackgroundColor = Color.FromArgb("#FFF3E0"), TextColor = Color.FromArgb("#E65100"), CornerRadius = 6, Padding = new Thickness(4, 6) };
        btnHabit180.Clicked += (s, e) => { chkHasHabitTarget.IsChecked = true; dateHabitTarget.Date = dateHabitTarget.Date.AddDays(180); };
        Grid.SetColumn(btnHabit180, 3);
        habitQuickButtonsGrid.Children.Add(btnHabit180);
        
        var btnHabit365 = new Button { Text = "+1yr", BackgroundColor = Color.FromArgb("#FFF3E0"), TextColor = Color.FromArgb("#E65100"), CornerRadius = 6, Padding = new Thickness(4, 6) };
        btnHabit365.Clicked += (s, e) => { chkHasHabitTarget.IsChecked = true; dateHabitTarget.Date = dateHabitTarget.Date.AddDays(365); };
        Grid.SetColumn(btnHabit365, 4);
        habitQuickButtonsGrid.Children.Add(btnHabit365);
        
        habitTargetSection.Children.Add(habitQuickButtonsGrid);
        
        habitTargetSection.Children.Add(new Label
        {
            Text = "📅 You must either set a target date or mark that you don't want this as a habit",
            FontSize = 12,
            TextColor = Color.FromArgb("#FF9800"),
            FontAttributes = FontAttributes.Italic
        });
        mainStack.Children.Add(habitTargetSection);

        // Display Days Section
        _displayDaysSelector = new DisplayDaysSelector();
        mainStack.Children.Add(_displayDaysSelector.Container);

        // Activity Image
        mainStack.Children.Add(new Label
        {
            Text = "Activity Image",
            FontAttributes = FontAttributes.Bold
        });

        var imageButtonsStack = new HorizontalStackLayout { Spacing = 12 };
        
        btnPickImage = new Button
        {
            Text = "Pick Image",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        btnPickImage.Clicked += OnPickImageClicked;
        imageButtonsStack.Children.Add(btnPickImage);
        
        var btnSetBlackImage = new Button
        {
            Text = "Set Black Image",
            BackgroundColor = Color.FromArgb("#333333"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        btnSetBlackImage.Clicked += OnSetBlackImageClicked;
        imageButtonsStack.Children.Add(btnSetBlackImage);
        
        mainStack.Children.Add(imageButtonsStack);

        imgPreview = new Image
        {
            HeightRequest = 200,
            Aspect = Aspect.AspectFit,
            IsVisible = false
        };
        mainStack.Children.Add(imgPreview);

        // Save Button
        var btnSave = new Button
        {
            Text = "Save Activity",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 20, 0, 0)
        };
        btnSave.Clicked += OnSaveClicked;
        mainStack.Children.Add(btnSave);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private void AddQuickButton(Grid grid, string text, int days, int col, int row)
    {
        var btn = new Button
        {
            Text = text,
            FontSize = 11,
            Padding = new Thickness(8, 4),
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#333")
        };
        btn.Clicked += (s, e) =>
        {
            chkEndDate.IsChecked = true;
            var newDate = DateTime.Now.AddDays(days);
            dateEnd.Date = newDate.Date;
            timeEnd.Time = newDate.TimeOfDay;
        };
        Grid.SetColumn(btn, col);
        Grid.SetRow(btn, row);
        grid.Children.Add(btn);
    }

    private void OnCategoryTextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.NewTextValue))
        {
            categorySuggestions.IsVisible = true;
            categorySuggestions.ItemsSource = _categories;
        }
        else
        {
            var filtered = _categories
                .Where(c => c.StartsWith(e.NewTextValue, StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            if (filtered.Count > 0)
            {
                categorySuggestions.ItemsSource = filtered;
                categorySuggestions.IsVisible = true;
            }
            else
            {
                categorySuggestions.IsVisible = false;
            }
        }
        
        UpdateExpPreview();
    }

    private void OnCategorySuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.Count > 0 && e.CurrentSelection[0] is string selected)
        {
            entryCategory.Text = selected;
            categorySuggestions.IsVisible = false;
            UpdateExpPreview();
        }
    }

    private void OnRewardTypeChanged(object sender, EventArgs e)
    {
        bool isPercentType = pickerRewardType.SelectedIndex == 1;
        fixedExpSection.IsVisible = !isPercentType;
        percentExpSection.IsVisible = isPercentType;
    }

    private void OnMeaningfulChanged(object sender, TextChangedEventArgs e)
    {
        UpdateExpPreview();
    }

    private void UpdateExpPreview()
    {
        if (int.TryParse(txtMeaningful.Text, out int meaningful))
        {
            bool isNegativeCategory = entryCategory.Text?.Equals("Negative", StringComparison.OrdinalIgnoreCase) == true;
            bool isNegativeValue = meaningful < 0;
            int absMeaningful = Math.Abs(meaningful);
            
            int exp = (isNegativeCategory || isNegativeValue) ? -(absMeaningful * 2) : (absMeaningful * 2);
            string sign = exp >= 0 ? "+" : "";
            lblExpPreview.Text = $"→ This activity will give: {sign}{exp} EXP";
            lblExpPreview.TextColor = exp < 0 ? Color.FromArgb("#F44336") : Color.FromArgb("#4CAF50");
        }
        else
        {
            lblExpPreview.Text = "→ This activity will give: +0 EXP";
            lblExpPreview.TextColor = Color.FromArgb("#4CAF50");
        }
    }

    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select an image",
                FileTypes = FilePickerFileType.Images,
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
                
                imgPreview.Source = ImageSource.FromFile(fullPath);
                imgPreview.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to pick image: {ex.Message}", "OK");
        }
    }

    private async void OnSetBlackImageClicked(object sender, EventArgs e)
    {
        try
        {
            string destFolder = GetImagesFolderPath();
            Directory.CreateDirectory(destFolder);
            
            string blackImagePath = Path.Combine(destFolder, "black_placeholder.png");
            
            // Create black image if it doesn't exist
            if (!File.Exists(blackImagePath))
            {
                byte[] blackPng = CreateBlackPng(100, 100);
                await File.WriteAllBytesAsync(blackImagePath, blackPng);
            }
            
            _selectedImageFilename = "black_placeholder.png";
            imgPreview.Source = ImageSource.FromFile(blackImagePath);
            imgPreview.IsVisible = true;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to set black image: {ex.Message}", "OK");
        }
    }

    private byte[] CreateBlackPng(int width, int height)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        
        // PNG Signature
        bw.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        
        // IHDR chunk
        var ihdr = new byte[13];
        WriteInt32BE(ihdr, 0, width);
        WriteInt32BE(ihdr, 4, height);
        ihdr[8] = 8;  // bit depth
        ihdr[9] = 2;  // color type (RGB)
        ihdr[10] = 0; // compression
        ihdr[11] = 0; // filter
        ihdr[12] = 0; // interlace
        WriteChunk(bw, "IHDR", ihdr);
        
        // IDAT chunk - create raw image data
        using var dataMs = new MemoryStream();
        using var deflate = new System.IO.Compression.DeflateStream(dataMs, System.IO.Compression.CompressionLevel.Fastest, true);
        
        for (int y = 0; y < height; y++)
        {
            deflate.WriteByte(0); // No filter
            for (int x = 0; x < width; x++)
            {
                deflate.WriteByte(0); // R
                deflate.WriteByte(0); // G
                deflate.WriteByte(0); // B
            }
        }
        deflate.Close();
        
        var rawData = dataMs.ToArray();
        var zlibData = new byte[rawData.Length + 6];
        zlibData[0] = 0x78;
        zlibData[1] = 0x9C;
        Array.Copy(rawData, 0, zlibData, 2, rawData.Length);
        
        uint adler = 1;
        zlibData[zlibData.Length - 4] = (byte)(adler >> 24);
        zlibData[zlibData.Length - 3] = (byte)(adler >> 16);
        zlibData[zlibData.Length - 2] = (byte)(adler >> 8);
        zlibData[zlibData.Length - 1] = (byte)adler;
        
        WriteChunk(bw, "IDAT", zlibData);
        WriteChunk(bw, "IEND", Array.Empty<byte>());
        
        return ms.ToArray();
    }

    private void WriteInt32BE(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }

    private void WriteChunk(BinaryWriter bw, string type, byte[] data)
    {
        bw.Write((byte)(data.Length >> 24));
        bw.Write((byte)(data.Length >> 16));
        bw.Write((byte)(data.Length >> 8));
        bw.Write((byte)data.Length);
        bw.Write(System.Text.Encoding.ASCII.GetBytes(type));
        bw.Write(data);
        uint crc = Crc32(System.Text.Encoding.ASCII.GetBytes(type), data);
        bw.Write((byte)(crc >> 24));
        bw.Write((byte)(crc >> 16));
        bw.Write((byte)(crc >> 8));
        bw.Write((byte)crc);
    }

    private uint Crc32(byte[] type, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        uint[] table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int j = 0; j < 8; j++)
                c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        foreach (byte b in type)
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        foreach (byte b in data)
            crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc ^ 0xFFFFFFFF;
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                await DisplayAlert("Validation Error", "Please enter an activity name", "OK");
                return;
            }

            string category = string.IsNullOrWhiteSpace(entryCategory.Text) ? "Misc" : entryCategory.Text.Trim();
            bool isPercentType = pickerRewardType.SelectedIndex == 1;

            int meaningful = 100;
            int expGain = 0;
            double percentOfLevel = 1.0;
            int percentCutoff = 100;

            if (isPercentType)
            {
                if (!double.TryParse(txtPercentOfLevel.Text, out percentOfLevel) || percentOfLevel <= 0)
                {
                    await DisplayAlert("Validation Error", "Please enter a valid percent (e.g., 1 for 1%)", "OK");
                    return;
                }
                if (!int.TryParse(txtPercentCutoff.Text, out percentCutoff) || percentCutoff < 1 || percentCutoff > 100)
                {
                    percentCutoff = 100;
                }
                expGain = 0;
            }
            else
            {
                if (!int.TryParse(txtMeaningful.Text, out meaningful))
                {
                    await DisplayAlert("Validation Error", "Please enter a valid number for meaningful level", "OK");
                    return;
                }
                
                int absMeaningful = Math.Abs(meaningful);
                if (absMeaningful < 1 || absMeaningful > 1000)
                {
                    await DisplayAlert("Validation Error", "Meaningful level must be between 1 and 1000 (or -1 to -1000 for penalties)", "OK");
                    return;
                }
                
                bool isNegativeCategory = category.Equals("Negative", StringComparison.OrdinalIgnoreCase);
                bool isNegativeValue = meaningful < 0;
                
                meaningful = absMeaningful;
                expGain = (isNegativeCategory || isNegativeValue) ? -(meaningful * 2) : (meaningful * 2);
            }

            if (expGain > 0 && !chkNoHabitTarget.IsChecked && !chkHasHabitTarget.IsChecked)
            {
                await DisplayAlert("Habit Target Required", 
                    "Please either:\n• Set a habit target date, or\n• Check 'I don't want this as a habit'", 
                    "OK");
                return;
            }

            var activity = new Activity
            {
                Username = _auth.CurrentUsername,
                Game = GameId,
                Name = txtName.Text.Trim(),
                Category = category,
                MeaningfulUntilLevel = meaningful,
                ExpGain = expGain,
                RewardType = isPercentType ? "PercentOfLevel" : "Fixed",
                PercentOfLevel = percentOfLevel,
                PercentCutoffLevel = percentCutoff,
                ImagePath = _selectedImageFilename ?? "",
                IsStreakTracked = chkStreakTracked.IsChecked,
                IsPossible = chkIsPossible.IsChecked,
                ShowTimesCompletedBadge = chkShowTimesCompleted.IsChecked,
                NoHabitTarget = chkNoHabitTarget.IsChecked,
                HabitTargetDate = chkHasHabitTarget.IsChecked ? dateHabitTarget.Date : null,
                HabitTargetFirstSet = chkHasHabitTarget.IsChecked ? DateTime.Now : null,
                HabitTargetPostponeCount = 0,
                DisplayDaysOfWeek = _displayDaysSelector.GetDisplayDaysOfWeek(),
                DisplayDayOfMonth = _displayDaysSelector.GetDisplayDayOfMonth()
            };

            if (chkStartDate.IsChecked == true)
            {
                activity.StartDate = dateStart.Date.Add(timeStart.Time);
            }

            if (chkEndDate.IsChecked == true)
            {
                activity.EndDate = dateEnd.Date.Add(timeEnd.Time);
            }

            await _activities.CreateActivityAsync(activity);
            
            _activities.LastCreatedActivityId = activity.Id;

            if (_isModalMode)
            {
                _modalTcs?.TrySetResult(activity);
                await Navigation.PopModalAsync();
            }
            else
            {
                await DisplayAlert("Success", "Activity created!", "OK");
                await Navigation.PopAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save activity: {ex.Message}", "OK");
        }
    }

    protected override bool OnBackButtonPressed()
    {
        if (_isModalMode)
        {
            _modalTcs?.TrySetResult(null);
        }
        return base.OnBackButtonPressed();
    }

    private static string GetImagesFolderPath()
    {
        return Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
    }
}

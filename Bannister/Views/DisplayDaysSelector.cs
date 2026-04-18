using Microsoft.Maui.Controls;

namespace Bannister.Views;

/// <summary>
/// Reusable UI component for selecting which days an activity should display.
/// Supports day-of-week selection and day-of-month selection.
/// </summary>
public class DisplayDaysSelector
{
    // Day of week checkboxes
    public CheckBox ChkSunday { get; private set; }
    public CheckBox ChkMonday { get; private set; }
    public CheckBox ChkTuesday { get; private set; }
    public CheckBox ChkWednesday { get; private set; }
    public CheckBox ChkThursday { get; private set; }
    public CheckBox ChkFriday { get; private set; }
    public CheckBox ChkSaturday { get; private set; }
    
    // Day of month picker
    public Picker PickerDayOfMonth { get; private set; }
    
    // Container for the whole UI
    public VerticalStackLayout Container { get; private set; }
    
    // Track if we're in "day of month" mode
    private bool _isDayOfMonthMode = false;
    
    public DisplayDaysSelector()
    {
        BuildUI();
    }
    
    private void BuildUI()
    {
        Container = new VerticalStackLayout { Spacing = 8 };
        
        // Header
        Container.Children.Add(new Label
        {
            Text = "Display Days (optional)",
            FontAttributes = FontAttributes.Bold
        });
        
        Container.Children.Add(new Label
        {
            Text = "Choose which days this activity should appear:",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });
        
        // Day of week checkboxes in a grid
        var daysGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 4,
            Margin = new Thickness(0, 4, 0, 0)
        };
        
        ChkSunday = CreateDayCheckbox("Sun", 0, daysGrid);
        ChkMonday = CreateDayCheckbox("Mon", 1, daysGrid);
        ChkTuesday = CreateDayCheckbox("Tue", 2, daysGrid);
        ChkWednesday = CreateDayCheckbox("Wed", 3, daysGrid);
        ChkThursday = CreateDayCheckbox("Thu", 4, daysGrid);
        ChkFriday = CreateDayCheckbox("Fri", 5, daysGrid);
        ChkSaturday = CreateDayCheckbox("Sat", 6, daysGrid);
        
        Container.Children.Add(daysGrid);
        
        // Quick selection buttons
        var quickButtonsStack = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };
        
        var btnAllDays = new Button
        {
            Text = "All Days",
            FontSize = 11,
            Padding = new Thickness(8, 4),
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32")
        };
        btnAllDays.Clicked += (s, e) => SetAllDays(true);
        quickButtonsStack.Children.Add(btnAllDays);
        
        var btnWeekdays = new Button
        {
            Text = "Weekdays",
            FontSize = 11,
            Padding = new Thickness(8, 4),
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0")
        };
        btnWeekdays.Clicked += (s, e) => SetWeekdays();
        quickButtonsStack.Children.Add(btnWeekdays);
        
        var btnWeekends = new Button
        {
            Text = "Weekends",
            FontSize = 11,
            Padding = new Thickness(8, 4),
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            TextColor = Color.FromArgb("#E65100")
        };
        btnWeekends.Clicked += (s, e) => SetWeekends();
        quickButtonsStack.Children.Add(btnWeekends);
        
        var btnClearDays = new Button
        {
            Text = "Clear",
            FontSize = 11,
            Padding = new Thickness(8, 4),
            CornerRadius = 4,
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828")
        };
        btnClearDays.Clicked += (s, e) => SetAllDays(false);
        quickButtonsStack.Children.Add(btnClearDays);
        
        Container.Children.Add(quickButtonsStack);
        
        // Separator
        Container.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 8, 0, 4)
        });
        
        // Day of month option
        var dayOfMonthStack = new HorizontalStackLayout { Spacing = 8 };
        
        dayOfMonthStack.Children.Add(new Label
        {
            Text = "OR show only on day of month:",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#666")
        });
        
        PickerDayOfMonth = new Picker
        {
            Title = "Select day",
            WidthRequest = 120
        };
        
        // Add options: None, 1-31
        var dayOptions = new List<string> { "None" };
        for (int i = 1; i <= 31; i++)
        {
            string suffix = i switch
            {
                1 or 21 or 31 => "st",
                2 or 22 => "nd",
                3 or 23 => "rd",
                _ => "th"
            };
            dayOptions.Add($"{i}{suffix}");
        }
        PickerDayOfMonth.ItemsSource = dayOptions;
        PickerDayOfMonth.SelectedIndex = 0;
        
        PickerDayOfMonth.SelectedIndexChanged += (s, e) =>
        {
            _isDayOfMonthMode = PickerDayOfMonth.SelectedIndex > 0;
            
            // If day of month is selected, uncheck all day-of-week boxes
            if (_isDayOfMonthMode)
            {
                SetAllDays(false);
            }
        };
        
        dayOfMonthStack.Children.Add(PickerDayOfMonth);
        Container.Children.Add(dayOfMonthStack);
        
        // Info label
        Container.Children.Add(new Label
        {
            Text = "💡 Leave all unchecked to show every day. Day-of-month overrides day-of-week.",
            FontSize = 11,
            TextColor = Color.FromArgb("#9C27B0"),
            FontAttributes = FontAttributes.Italic
        });
    }
    
    private CheckBox CreateDayCheckbox(string label, int column, Grid grid)
    {
        var stack = new VerticalStackLayout 
        { 
            HorizontalOptions = LayoutOptions.Center,
            Spacing = 2
        };
        
        var checkbox = new CheckBox
        {
            IsChecked = true, // Default all days selected
            HorizontalOptions = LayoutOptions.Center
        };
        
        // When a day checkbox is checked, clear day-of-month selection
        checkbox.CheckedChanged += (s, e) =>
        {
            if (e.Value && _isDayOfMonthMode)
            {
                PickerDayOfMonth.SelectedIndex = 0;
                _isDayOfMonthMode = false;
            }
        };
        
        stack.Children.Add(checkbox);
        stack.Children.Add(new Label
        {
            Text = label,
            FontSize = 10,
            HorizontalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#666")
        });
        
        Grid.SetColumn(stack, column);
        grid.Children.Add(stack);
        
        return checkbox;
    }
    
    private void SetAllDays(bool isChecked)
    {
        ChkSunday.IsChecked = isChecked;
        ChkMonday.IsChecked = isChecked;
        ChkTuesday.IsChecked = isChecked;
        ChkWednesday.IsChecked = isChecked;
        ChkThursday.IsChecked = isChecked;
        ChkFriday.IsChecked = isChecked;
        ChkSaturday.IsChecked = isChecked;
        
        if (isChecked)
        {
            PickerDayOfMonth.SelectedIndex = 0;
            _isDayOfMonthMode = false;
        }
    }
    
    private void SetWeekdays()
    {
        ChkSunday.IsChecked = false;
        ChkMonday.IsChecked = true;
        ChkTuesday.IsChecked = true;
        ChkWednesday.IsChecked = true;
        ChkThursday.IsChecked = true;
        ChkFriday.IsChecked = true;
        ChkSaturday.IsChecked = false;
        
        PickerDayOfMonth.SelectedIndex = 0;
        _isDayOfMonthMode = false;
    }
    
    private void SetWeekends()
    {
        ChkSunday.IsChecked = true;
        ChkMonday.IsChecked = false;
        ChkTuesday.IsChecked = false;
        ChkWednesday.IsChecked = false;
        ChkThursday.IsChecked = false;
        ChkFriday.IsChecked = false;
        ChkSaturday.IsChecked = true;
        
        PickerDayOfMonth.SelectedIndex = 0;
        _isDayOfMonthMode = false;
    }
    
    /// <summary>
    /// Get the comma-separated string of selected days of week
    /// Returns empty string if all days selected or day-of-month mode
    /// </summary>
    public string GetDisplayDaysOfWeek()
    {
        // If day of month is selected, return empty (day of month takes precedence)
        if (PickerDayOfMonth.SelectedIndex > 0)
            return "";
        
        var days = new List<string>();
        
        if (ChkSunday.IsChecked) days.Add("Sun");
        if (ChkMonday.IsChecked) days.Add("Mon");
        if (ChkTuesday.IsChecked) days.Add("Tue");
        if (ChkWednesday.IsChecked) days.Add("Wed");
        if (ChkThursday.IsChecked) days.Add("Thu");
        if (ChkFriday.IsChecked) days.Add("Fri");
        if (ChkSaturday.IsChecked) days.Add("Sat");
        
        // If all 7 days are selected, return empty (means "every day")
        if (days.Count == 7)
            return "";
        
        // If no days selected, also return empty
        if (days.Count == 0)
            return "";
        
        return string.Join(",", days);
    }
    
    /// <summary>
    /// Get the selected day of month (1-31) or 0 if not set
    /// </summary>
    public int GetDisplayDayOfMonth()
    {
        if (PickerDayOfMonth.SelectedIndex <= 0)
            return 0;
        
        return PickerDayOfMonth.SelectedIndex; // Index 1 = day 1, etc.
    }
    
    /// <summary>
    /// Load values from an existing activity
    /// </summary>
    public void LoadFromActivity(string displayDaysOfWeek, int displayDayOfMonth)
    {
        // First, check if day of month is set
        if (displayDayOfMonth > 0 && displayDayOfMonth <= 31)
        {
            SetAllDays(false);
            PickerDayOfMonth.SelectedIndex = displayDayOfMonth;
            _isDayOfMonthMode = true;
            return;
        }
        
        // Otherwise, check day of week
        if (string.IsNullOrEmpty(displayDaysOfWeek))
        {
            // Empty means all days
            SetAllDays(true);
        }
        else
        {
            var days = displayDaysOfWeek.Split(',', StringSplitOptions.RemoveEmptyEntries);
            ChkSunday.IsChecked = days.Contains("Sun");
            ChkMonday.IsChecked = days.Contains("Mon");
            ChkTuesday.IsChecked = days.Contains("Tue");
            ChkWednesday.IsChecked = days.Contains("Wed");
            ChkThursday.IsChecked = days.Contains("Thu");
            ChkFriday.IsChecked = days.Contains("Fri");
            ChkSaturday.IsChecked = days.Contains("Sat");
        }
        
        PickerDayOfMonth.SelectedIndex = 0;
        _isDayOfMonthMode = false;
    }
}

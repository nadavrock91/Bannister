using Bannister.Converters;
using Bannister.Services;
using Bannister.ViewModels;
using Microsoft.Maui.Controls.Shapes;

namespace Bannister.Views;

/// <summary>
/// Partial class containing activity-related UI building and filtering methods
/// </summary>
public partial class ActivityGamePage
{
    private Grid BuildActivitiesPanel()
    {
        var grid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        // Filter controls
        var filterFrame = BuildFilterControls();
        Grid.SetRow(filterFrame, 0);
        grid.Children.Add(filterFrame);

        // Activities scroll view
        activitiesCollection = new ScrollView 
        { 
            Margin = new Thickness(0, 12, 0, 0)
        };
        Grid.SetRow(activitiesCollection, 1);
        grid.Children.Add(activitiesCollection);

        return grid;
    }

    private Frame BuildFilterControls()
    {
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var outerGrid = new Grid
        {
            RowSpacing = 8,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        // Category navigation
        var navGrid = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        btnPrevPage = new Button
        {
            Text = "◀",
            WidthRequest = 40,
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        btnPrevPage.Clicked += OnPrevCategoryClicked;
        Grid.SetColumn(btnPrevPage, 0);
        navGrid.Children.Add(btnPrevPage);

        lblPageInfo = new Label
        {
            Text = "All Activities",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        Grid.SetColumn(lblPageInfo, 1);
        navGrid.Children.Add(lblPageInfo);

        btnNextPage = new Button
        {
            Text = "▶",
            WidthRequest = 40,
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        btnNextPage.Clicked += OnNextCategoryClicked;
        Grid.SetColumn(btnNextPage, 2);
        navGrid.Children.Add(btnNextPage);

        Grid.SetRow(navGrid, 0);
        outerGrid.Children.Add(navGrid);

        // Pickers - 3 columns
        var pickerGrid = new Grid
        {
            ColumnSpacing = 8,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        categoryPicker = new Picker 
        { 
            Title = "Jump to Category",
            TextColor = Colors.Black,
            TitleColor = Color.FromArgb("#999")
        };
        categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        Grid.SetColumn(categoryPicker, 0);
        pickerGrid.Children.Add(categoryPicker);

        metaFilterPicker = new Picker
        {
            Title = "Select filter",
            ItemsSource = new List<string>
            {
                "All Activities",
                "Has Multiplier",
                "No Multiplier",
                "Active Now",
                "Possible",
                "Expired",
                "Stale"
            }
        };
        metaFilterPicker.SelectedIndexChanged += OnMetaFilterChanged;
        Grid.SetColumn(metaFilterPicker, 1);
        pickerGrid.Children.Add(metaFilterPicker);

        sortPicker = new Picker
        {
            Title = "Sort by",
            ItemsSource = new List<string>
            {
                "Last Used (Recent First)",
                "Alphabetical (A-Z)",
                "Alphabetical (Z-A)",
                "EXP (High to Low)",
                "EXP (Low to High)"
            },
            SelectedIndex = 0
        };
        sortPicker.SelectedIndexChanged += OnSortOrderChanged;
        Grid.SetColumn(sortPicker, 2);
        pickerGrid.Children.Add(sortPicker);

        Grid.SetRow(pickerGrid, 1);
        outerGrid.Children.Add(pickerGrid);

        // Show All toggle row
        var showAllRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };

        btnShowAll = new Button
        {
            Text = "👁️ Show All Days",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#666"),
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 32,
            Padding = new Thickness(12, 0)
        };
        btnShowAll.Clicked += OnShowAllToggled;
        showAllRow.Children.Add(btnShowAll);

        showAllRow.Children.Add(new Label
        {
            Text = "(bypass day schedule filter)",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center
        });

        Grid.SetRow(showAllRow, 2);
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        outerGrid.Children.Add(showAllRow);

        frame.Content = outerGrid;
        return frame;
    }

    private void BuildActivitiesGridWithHeaders(List<ActivityGameViewModel> activities)
    {
        var mainStack = new VerticalStackLayout { Spacing = 8 };

        string currentSection = "";
        Grid? currentRow = null;
        int columnIndex = 0;

        foreach (var activity in activities)
        {
            // Check if we need a new section header
            if (!string.IsNullOrEmpty(activity.SectionHeader) && activity.SectionHeader != currentSection)
            {
                currentSection = activity.SectionHeader;
                columnIndex = 0;
                currentRow = null;

                // Add full-width header
                var headerFrame = new Frame
                {
                    Padding = new Thickness(12, 8),
                    CornerRadius = 8,
                    BackgroundColor = Color.FromArgb("#E8EAF6"),
                    BorderColor = Colors.Transparent,
                    HasShadow = false,
                    Margin = new Thickness(0, 16, 0, 8)
                };

                var headerLabel = new Label
                {
                    Text = currentSection,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#3F51B5"),
                    HorizontalOptions = LayoutOptions.Center
                };
                headerFrame.Content = headerLabel;
                mainStack.Children.Add(headerFrame);
            }

            // Create new row if needed (every 3 items)
            if (currentRow == null || columnIndex >= 3)
            {
                currentRow = new Grid
                {
                    ColumnSpacing = 8,
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Star }
                    },
                    Margin = new Thickness(0, 0, 0, 8)
                };
                mainStack.Children.Add(currentRow);
                columnIndex = 0;
            }

            // Create activity card
            var card = CreateActivityCard(activity);
            Grid.SetColumn(card, columnIndex);
            currentRow.Children.Add(card);

            columnIndex++;
        }

        activitiesCollection.Content = mainStack;
    }

    private Frame CreateActivityCard(ActivityGameViewModel activity)
    {
        var outerFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            HeightRequest = 140,
            BindingContext = activity
        };

        var innerFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = false,
            BorderColor = Colors.Transparent,
            IsClippedToBounds = true
        };

        // Selection border
        var selectionBorder = new Border
        {
            Stroke = Color.FromArgb("#4CAF50"),
            StrokeThickness = 4,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Colors.Transparent,
            IsVisible = false,
            InputTransparent = true
        };
        selectionBorder.SetBinding(Border.IsVisibleProperty, "IsSelected");

        var grid = new Grid { InputTransparent = false };

        // Add tap gesture to grid
        var gridTapGesture = new TapGestureRecognizer();
        gridTapGesture.Tapped += (s, e) => {
            activity.IsSelected = !activity.IsSelected;
        };
        grid.GestureRecognizers.Add(gridTapGesture);

        // Background for possible activities without images (solid black)
        if (activity.IsPossible && string.IsNullOrEmpty(activity.ImagePath))
        {
            var blackBackground = new BoxView
            {
                Color = Colors.Black
            };
            grid.Children.Add(blackBackground);
        }

        // Activity Image
        var image = new Image { Aspect = Aspect.AspectFit };
        image.SetBinding(Image.SourceProperty, new Binding("ImagePath", converter: new ImagePathConverter()));
        grid.Children.Add(image);

        // Activity Name
        var nameLabel = new Label
        {
            VerticalOptions = LayoutOptions.End,
            BackgroundColor = Color.FromArgb("#CC000000"),
            TextColor = Colors.White,
            Padding = new Thickness(8, 4),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };
        nameLabel.SetBinding(Label.TextProperty, "Name");
        grid.Children.Add(nameLabel);

        // Multiplier Badge (shows permanent multiplier OR temporary if set)
        var multiplierFrame = new Frame
        {
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.End,
            Padding = new Thickness(6, 2),
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#FF6B6B"),
            BorderColor = Colors.Transparent,
            Margin = 4
        };
        
        // Show if multiplier > 1 (we'll update this when temporary is set)
        multiplierFrame.SetBinding(IsVisibleProperty, "ShowMultiplier");
        
        var multiplierLabel = new Label
        {
            TextColor = Colors.White,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold
        };
        multiplierLabel.SetBinding(Label.TextProperty, new Binding("Multiplier", stringFormat: "x{0}"));
        multiplierFrame.Content = multiplierLabel;
        
        // Add tooltip for multiplier badge
        var multiplierTapGesture = new TapGestureRecognizer();
        multiplierTapGesture.Tapped += async (s, e) => {
            await Application.Current.MainPage.DisplayAlert("Multiplier", 
                $"This activity has a x{activity.Multiplier} multiplier.\n\nEach click counts as {activity.Multiplier} applications.", "OK");
        };
        multiplierFrame.GestureRecognizers.Add(multiplierTapGesture);
        grid.Children.Add(multiplierFrame);

        // EXP Badge (top-left)
        var expFrame = new Frame
        {
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(6, 2),
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#5B63EE"),
            BorderColor = Colors.Transparent,
            Margin = 4
        };
        var expLabel = new Label
        {
            TextColor = Colors.White,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold
        };
        expLabel.SetBinding(Label.TextProperty, new Binding("ExpGain", stringFormat: "+{0}"));
        expFrame.Content = expLabel;
        
        // Add tooltip for EXP badge
        var expTapGesture = new TapGestureRecognizer();
        expTapGesture.Tapped += async (s, e) => {
            await Application.Current.MainPage.DisplayAlert("EXP Reward", 
                $"This activity gives +{activity.ExpGain} EXP when applied.", "OK");
        };
        expFrame.GestureRecognizers.Add(expTapGesture);
        grid.Children.Add(expFrame);

        // Streak Badge (below EXP badge, only if streak tracked)
        if (activity.ShowStreakBadge)
        {
            var streakFrame = new Frame
            {
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.Start,
                Padding = new Thickness(6, 2),
                CornerRadius = 8,
                BackgroundColor = Color.FromArgb("#FF9800"), // Orange for streak
                BorderColor = Colors.Transparent,
                Margin = new Thickness(4, 28, 4, 4) // Below the EXP badge
            };
            
            var streakLabel = new Label
            {
                TextColor = Colors.White,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold
            };
            streakLabel.SetBinding(Label.TextProperty, "StreakDisplay");
            streakFrame.Content = streakLabel;
            
            // Add tooltip for streak badge
            var streakTapGesture = new TapGestureRecognizer();
            streakTapGesture.Tapped += async (s, e) => {
                string attemptInfo = activity.StreakAttemptNumber > 0 
                    ? $"\n\nThis is attempt #{activity.StreakAttemptNumber}" 
                    : "";
                await Application.Current.MainPage.DisplayAlert("Streak", 
                    $"Current streak: {activity.StreakCount} days{attemptInfo}", "OK");
            };
            streakFrame.GestureRecognizers.Add(streakTapGesture);
            grid.Children.Add(streakFrame);
        }

        // Display Day Streak Badge (below streak badge or EXP, only if tracking and has streak)
        if (activity.ShowDisplayDayStreak)
        {
            // Calculate vertical position based on whether streak badge is shown
            int topMargin = activity.ShowStreakBadge ? 52 : 28;
            
            var displayStreakFrame = new Frame
            {
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.Start,
                Padding = new Thickness(6, 2),
                CornerRadius = 8,
                BackgroundColor = Color.FromArgb("#2196F3"), // Blue for display day streak
                BorderColor = Colors.Transparent,
                Margin = new Thickness(4, topMargin, 4, 4)
            };
            
            var displayStreakLabel = new Label
            {
                TextColor = Colors.White,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold
            };
            displayStreakLabel.SetBinding(Label.TextProperty, "DisplayDayStreakDisplay");
            displayStreakFrame.Content = displayStreakLabel;
            
            // Add tooltip for display day streak badge
            var displayStreakTapGesture = new TapGestureRecognizer();
            displayStreakTapGesture.Tapped += async (s, e) => {
                string nextMilestone = ActivityService.GetNextMilestoneInfo(activity.DisplayDayStreak);
                int potentialPenalty = ActivityService.CalculateStreakBreakPenalty(activity.DisplayDayStreak);
                string penaltyWarning = potentialPenalty < 0 
                    ? $"\n\n⚠️ If broken: {potentialPenalty} EXP penalty" 
                    : "";
                await Application.Current.MainPage.DisplayAlert("Display Day Streak", 
                    $"🔥 {activity.DisplayDayStreak} scheduled days in a row!\n\n" +
                    $"Next milestone: {nextMilestone}{penaltyWarning}", "OK");
            };
            displayStreakFrame.GestureRecognizers.Add(displayStreakTapGesture);
            grid.Children.Add(displayStreakFrame);
        }

        // Times Completed Badge (right side, only if enabled and has completions)
        if (activity.ShowTimesCompleted)
        {
            var timesFrame = new Frame
            {
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.End,
                Padding = new Thickness(6, 2),
                CornerRadius = 8,
                BackgroundColor = Color.FromArgb("#9C27B0"), // Purple for completions
                BorderColor = Colors.Transparent,
                Margin = new Thickness(4, 28, 40, 4) // Below multiplier, left of menu
            };
            
            var timesLabel = new Label
            {
                TextColor = Colors.White,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold
            };
            timesLabel.SetBinding(Label.TextProperty, "TimesCompletedDisplay");
            timesFrame.Content = timesLabel;
            
            var timesTapGesture = new TapGestureRecognizer();
            timesTapGesture.Tapped += async (s, e) => {
                await Application.Current.MainPage.DisplayAlert("Times Completed", 
                    $"✓ Completed {activity.TimesCompleted} time(s)", "OK");
            };
            timesFrame.GestureRecognizers.Add(timesTapGesture);
            grid.Children.Add(timesFrame);
        }

        // Notes Badge (bottom-right, above name label, only if has notes)
        System.Diagnostics.Debug.WriteLine($"[NOTES] Activity '{activity.Name}' HasNotes={activity.HasNotes}, Notes='{activity.Notes}'");
        if (activity.HasNotes)
        {
            var notesFrame = new Frame
            {
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.End,
                Padding = new Thickness(6, 2),
                CornerRadius = 8,
                BackgroundColor = Color.FromArgb("#795548"), // Brown for notes
                BorderColor = Colors.Transparent,
                Margin = new Thickness(4, 4, 40, 36) // Above name label, left of menu/+ buttons
            };
            
            var notesLabel = new Label
            {
                Text = "📝",
                TextColor = Colors.White,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold
            };
            notesFrame.Content = notesLabel;
            
            // Add tooltip behavior - tap to view full notes
            var notesTapGesture = new TapGestureRecognizer();
            notesTapGesture.Tapped += async (s, e) => {
                await Application.Current.MainPage.DisplayAlert("Notes", activity.Notes, "OK");
            };
            notesFrame.GestureRecognizers.Add(notesTapGesture);
            
            // Add hover behavior with ToolTipProperties
            ToolTipProperties.SetText(notesFrame, activity.NotesPreview);
            
            grid.Children.Add(notesFrame);
        }

        // Days Since Click Badge (bottom-left, above name label)
        var daysBadgeFrame = new Frame
        {
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(6, 2),
            CornerRadius = 8,
            BackgroundColor = GetDaysBadgeColor(activity),
            BorderColor = Colors.Transparent,
            Margin = new Thickness(4, 4, 4, 36) // Above the name label
        };
        var daysLabel = new Label
        {
            TextColor = Colors.White,
            FontSize = 10,
            FontAttributes = FontAttributes.Bold
        };
        daysLabel.SetBinding(Label.TextProperty, new Binding("DaysSinceClickDisplay"));
        daysBadgeFrame.Content = daysLabel;
        
        // Add tooltip for days badge
        var daysTapGesture = new TapGestureRecognizer();
        daysTapGesture.Tapped += async (s, e) => {
            string message;
            if (!activity.LastUsedDate.HasValue)
            {
                message = "This activity has never been clicked.";
            }
            else if (activity.DaysSinceClick == 0)
            {
                message = "This activity was clicked today.";
            }
            else if (activity.DaysSinceClick == 1)
            {
                message = "This activity was clicked yesterday.";
            }
            else
            {
                message = $"This activity was last clicked {activity.DaysSinceClick} days ago.\n\nLast used: {activity.LastUsedDate.Value:MMM dd, yyyy}";
            }
            await Application.Current.MainPage.DisplayAlert("Days Since Click", message, "OK");
        };
        daysBadgeFrame.GestureRecognizers.Add(daysTapGesture);
        grid.Children.Add(daysBadgeFrame);

        // Menu button
        var menuButton = new Button
        {
            Text = "⋮",
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.End,
            BackgroundColor = Color.FromArgb("#80000000"),
            TextColor = Colors.White,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 30,
            HeightRequest = 30,
            CornerRadius = 15,
            Padding = 0,
            Margin = new Thickness(4, 40, 4, 4)
        };
        menuButton.Clicked += async (s, e) => {
            await ShowContextMenu(activity);
        };
        grid.Children.Add(menuButton);

        // Selection button (+/-)
        var selectionButton = new Button
        {
            Text = activity.IsSelected ? "−" : "+",
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.End,
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 35,
            HeightRequest = 35,
            CornerRadius = 17,
            Padding = 0,
            Margin = new Thickness(4, 75, 4, 4)
        };
        
        selectionButton.SetBinding(Button.TextProperty, new Binding("IsSelected", 
            converter: new SelectionButtonTextConverter()));
        
        selectionButton.Clicked += (s, e) => {
            activity.IsSelected = !activity.IsSelected;
        };
        
        grid.Children.Add(selectionButton);

        innerFrame.Content = grid;

        var containerGrid = new Grid();
        containerGrid.Children.Add(innerFrame);
        containerGrid.Children.Add(selectionBorder);

        outerFrame.Content = containerGrid;
        return outerFrame;
    }

    /// <summary>
    /// Get color for days since click badge based on how long ago
    /// </summary>
    private Color GetDaysBadgeColor(ActivityGameViewModel activity)
    {
        if (!activity.LastUsedDate.HasValue)
        {
            return Color.FromArgb("#9E9E9E"); // Gray - never used
        }

        var days = activity.DaysSinceClick ?? 0;

        if (days == 0)
        {
            return Color.FromArgb("#4CAF50"); // Green - today
        }
        else if (days <= 2)
        {
            return Color.FromArgb("#8BC34A"); // Light green - recent
        }
        else if (days <= 7)
        {
            return Color.FromArgb("#FF9800"); // Orange - getting stale
        }
        else
        {
            return Color.FromArgb("#F44336"); // Red - very stale
        }
    }
}

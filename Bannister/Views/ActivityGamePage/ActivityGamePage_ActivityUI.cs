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
    private CancellationTokenSource? _renderCts;

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
        var frame = (new Frame
        {
            Padding = 12,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var outerGrid = new Grid
        {
            RowSpacing = 8,
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto }
            }
        };

        btnLastCalculated = (new Button
        {
            IsVisible = false,
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Fill
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightIndigo).ThemeColor(Button.TextColorProperty, ThemeKey.HighlightIndigoText);
        btnLastCalculated.Clicked += async (_, _) => await GoToLastCalculatedAsync();
        Grid.SetRow(btnLastCalculated, 0);
        outerGrid.Children.Add(btnLastCalculated);

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

        btnPrevPage = (new Button
        {
            Text = "◀",
            WidthRequest = 40,
            CornerRadius = 8
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
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

        btnNextPage = (new Button
        {
            Text = "▶",
            WidthRequest = 40,
            CornerRadius = 8
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnNextPage.Clicked += OnNextCategoryClicked;
        Grid.SetColumn(btnNextPage, 2);
        navGrid.Children.Add(btnNextPage);

        Grid.SetRow(navGrid, 1);
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

        categoryPicker = (new Picker
        { 
            Title = "Jump to Category"
        }).ThemeColor(Picker.TextColorProperty, ThemeKey.CardText1).ThemeColor(Picker.TitleColorProperty, ThemeKey.CardText3);
        categoryPicker.SelectedIndexChanged += OnCategoryChanged;
        Grid.SetColumn(categoryPicker, 0);
        pickerGrid.Children.Add(categoryPicker);

        metaFilterPicker = new Picker
        {
            Title = "Select filter",
            ItemsSource = new List<string>
            {
                "All Activities",
                "Auto",
                "Has Multiplier",
                "No Multiplier",
                "Active Now",
                "Possible",
                "Expired",
                "Stale",
                "Missing Image"
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

        Grid.SetRow(pickerGrid, 2);
        outerGrid.Children.Add(pickerGrid);

        // Show All toggle row
        var showAllRow = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 4, 0, 0)
        };

        btnShowAll = (new Button
        {
            Text = "👁️ Show All Days",
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 32,
            Padding = new Thickness(12, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Separator).ThemeColor(Button.TextColorProperty, ThemeKey.CardText2);
        btnShowAll.Clicked += OnShowAllToggled;
        showAllRow.Children.Add(btnShowAll);

        showAllRow.Children.Add((new Label
        {
            Text = "(bypass day schedule filter)",
            FontSize = 11,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));

        Grid.SetRow(showAllRow, 3);
        outerGrid.Children.Add(showAllRow);

        frame.Content = outerGrid;
        return frame;
    }

    // *** MODIFIED: Now async and checks for streak containers ***
    private async void BuildActivitiesGridWithHeaders(List<ActivityGameViewModel> activities)
    {
        // Cancel any in-flight render
        _renderCts?.Cancel();
        _renderCts?.Dispose();
        var cts = new CancellationTokenSource();
        _renderCts = cts;
        var ct = cts.Token;
        _activityCards.Clear();

        System.Diagnostics.Debug.WriteLine(
            $"[BUILD_GRID] ENTER activities=" +
            $"{activities.Count} " +
            $"categoryIdx={_currentCategoryIndex}");

        var mainStack = new VerticalStackLayout { Spacing = 8 };

        // Check if the currently displayed category is a streak container.
        if (_navigableCategories.Count > 0 && _currentCategoryIndex >= 0 && _currentCategoryIndex < _navigableCategories.Count)
        {
            string currentCategory = _tempNonNavigableCategory ?? _navigableCategories[_currentCategoryIndex];
            var streakContainer = GetStreakContainerForCategory(currentCategory);
            System.Diagnostics.Debug.WriteLine(
                $"[BUILD_GRID] streakContainer=" +
                $"{streakContainer?.Name ?? "null"}");
            
            if (streakContainer != null)
            {
                // This is a streak container category - show streak attempts instead
                System.Diagnostics.Debug.WriteLine(
                    "[BUILD_GRID] About to await streak render");
                await BuildStreakContainerViewAsync(
                    mainStack, streakContainer, ct);
                if (ct.IsCancellationRequested) return;
                activitiesCollection.Content = mainStack;
                await RevealTargetActivityAsync();
                return;
            }
        }

        // Regular activity display
        string currentSection = "";
        Grid? currentRow = null;
        int columnIndex = 0;

        foreach (var activity in activities)
        {
            // Skip streak container activities - they only show in their own category
            if (activity.Activity.IsStreakContainer)
            {
                continue;
            }

            // Check if we need a new section header
            if (!string.IsNullOrEmpty(activity.SectionHeader) && activity.SectionHeader != currentSection)
            {
                currentSection = activity.SectionHeader;
                columnIndex = 0;
                currentRow = null;

                // Add full-width header
                var headerFrame = (new Frame
                {
                    Padding = new Thickness(12, 8),
                    CornerRadius = 8,
                    BorderColor = Colors.Transparent,
                    HasShadow = false,
                    Margin = new Thickness(0, 16, 0, 8)
                }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightIndigo);

                var headerLabel = (new Label
                {
                    Text = currentSection,
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    HorizontalOptions = LayoutOptions.Center
                }).ThemeColor(Label.TextColorProperty, currentSection == "Activities That Don't Display Every Day" ? ThemeKey.ErrorText : ThemeKey.HighlightIndigoText, ThemeKey.PageText1, classifyPageText: true);
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
            _activityCards[activity.Id] = card;
            Grid.SetColumn(card, columnIndex);
            currentRow.Children.Add(card);

            columnIndex++;
        }

        if (ct.IsCancellationRequested) return;
        activitiesCollection.Content = mainStack;
        await RevealTargetActivityAsync();
    }

    private Frame CreateActivityCard(ActivityGameViewModel activity)
    {
        var outerFrame = (new Frame
        {
            Padding = 0,
            CornerRadius = 12,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            HeightRequest = 140,
            BindingContext = activity
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        var innerFrame = (new Frame
        {
            Padding = 0,
            CornerRadius = 12,
            HasShadow = false,
            BorderColor = Colors.Transparent,
            IsClippedToBounds = true
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);

        // Selection border
        var selectionBorder = (new Border
        {
            StrokeThickness = 4,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Colors.Transparent,
            IsVisible = false,
            InputTransparent = true
        }).ThemeColor(Border.StrokeProperty, ThemeKey.SuccessButtonBackground);
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
            var blackBackground = (new BoxView
            {
            }).ThemeColor(BoxView.ColorProperty, ThemeKey.MediaBackground);
            grid.Children.Add(blackBackground);
        }

        // Activity Image
        var image = new Image { Aspect = Aspect.AspectFit };
        image.SetBinding(Image.SourceProperty, new Binding("ImagePath", converter: new ImagePathConverter()));
        if (activity.Activity.IsImageOnly)
        {
            image.IsVisible = true;
            image.HeightRequest = 120;
        }
        grid.Children.Add(image);

        // Activity Name
        var nameLabel = (new Label
        {
            VerticalOptions = LayoutOptions.End,
            Padding = new Thickness(8, 4),
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2,
            IsVisible = !activity.Activity.IsImageOnly
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);
        nameLabel.SetBinding(Label.TextProperty, "Name");
        grid.Children.Add(nameLabel);

        if (activity.Activity.IsZeroCount)
        {
            var zeroBadge = (new Frame
            {
                CornerRadius = 10,
                Padding = new Thickness(8, 3),
                HasShadow = false,
                BorderColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.End,
                VerticalOptions = LayoutOptions.Start,
                Margin = new Thickness(4, 4, 42, 4)
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightTealText);
            zeroBadge.Content = (new Label
            {
                Text = "ZERO",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);
            grid.Children.Add(zeroBadge);
        }

        // Multiplier Badge (shows permanent multiplier OR temporary if set)
        var multiplierFrame = (new Frame
        {
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.End,
            Padding = new Thickness(6, 2),
            CornerRadius = 8,
            BorderColor = Colors.Transparent,
            Margin = 4
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.TimelineAccent);
        
        // Show if multiplier > 1 (we'll update this when temporary is set)
        multiplierFrame.SetBinding(IsVisibleProperty, "ShowMultiplier");
        
        var multiplierLabel = (new Label
        {
            FontSize = 10,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);
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
        var expFrame = (new Frame
        {
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(6, 2),
            CornerRadius = 8,
            BorderColor = Colors.Transparent,
            Margin = 4
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondaryButtonBackground);
        var expLabel = (new Label
        {
            FontSize = 10,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);
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
            var streakFrame = (new Frame
            {
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.Start,
                Padding = new Thickness(6, 2),
                CornerRadius = 8,
                BorderColor = Colors.Transparent,
                Margin = new Thickness(4, 28, 4, 4) // Below the EXP badge
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.WarningButtonBackground);
            
            var streakLabel = (new Label
            {
                FontSize = 10,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);
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
            
            var displayStreakFrame = (new Frame
            {
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.Start,
                Padding = new Thickness(6, 2),
                CornerRadius = 8,
                BorderColor = Colors.Transparent,
                Margin = new Thickness(4, topMargin, 4, 4)
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoButtonBackground);
            
            var displayStreakLabel = (new Label
            {
                FontSize = 10,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);
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
            var timesFrame = (new Frame
            {
                VerticalOptions = LayoutOptions.Start,
                HorizontalOptions = LayoutOptions.End,
                Padding = new Thickness(6, 2),
                CornerRadius = 8,
                BorderColor = Colors.Transparent,
                Margin = new Thickness(4, 28, 40, 4) // Below multiplier, left of menu
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText);
            
            var timesLabel = (new Label
            {
                FontSize = 10,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);
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
            var notesFrame = (new Frame
            {
                VerticalOptions = LayoutOptions.End,
                HorizontalOptions = LayoutOptions.End,
                Padding = new Thickness(6, 2),
                CornerRadius = 8,
                BorderColor = Colors.Transparent,
                Margin = new Thickness(4, 4, 40, 36) // Above name label, left of menu/+ buttons
            }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton);
            
            var notesLabel = (new Label
            {
                Text = "📝",
                FontSize = 10,
                FontAttributes = FontAttributes.Bold
            }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);
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
        var daysBadgeFrame = (new Frame
        {
            VerticalOptions = LayoutOptions.End,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(6, 2),
            CornerRadius = 8,
            BorderColor = Colors.Transparent,
            Margin = new Thickness(4, 4, 4, 36) // Above the name label
        }).ThemeColor(VisualElement.BackgroundColorProperty, GetDaysBadgeColor(activity));
        var daysLabel = (new Label
        {
            FontSize = 10,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText3, classifyPageText: true);
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
        var menuButton = (new Button
        {
            Text = "⋮",
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.End,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 30,
            HeightRequest = 30,
            CornerRadius = 15,
            Padding = 0,
            Margin = new Thickness(4, 40, 4, 4)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.Overlay).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        menuButton.Clicked += async (s, e) => {
            await ShowContextMenu(activity);
        };
        grid.Children.Add(menuButton);

        // Selection button (+/-)
        var selectionButton = (new Button
        {
            Text = activity.IsSelected ? "−" : "+",
            VerticalOptions = LayoutOptions.Start,
            HorizontalOptions = LayoutOptions.End,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            WidthRequest = 35,
            HeightRequest = 35,
            CornerRadius = 17,
            Padding = 0,
            Margin = new Thickness(4, 75, 4, 4)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        
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
    private ThemeKey GetDaysBadgeColor(ActivityGameViewModel activity)
    {
        if (!activity.LastUsedDate.HasValue)
        {
            return ThemeKey.DisabledButtonBackground; // Gray - never used
        }

        var days = activity.DaysSinceClick ?? 0;

        if (days == 0)
        {
            return ThemeKey.SuccessButtonBackground; // Green - today
        }
        else if (days <= 2)
        {
            return ThemeKey.SuccessButtonBackground; // Light green - recent
        }
        else if (days <= 7)
        {
            return ThemeKey.WarningText; // Orange - getting stale
        }
        else
        {
            return ThemeKey.ErrorButtonBackground; // Red - very stale
        }
    }
}

using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls.Shapes;
using System.ComponentModel;

namespace Bannister.Views;

public class ExpiredActivitiesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly HashSet<int> _selectedActivityIds = new();
    private List<ExpiredActivityViewModel> _viewModels = new();
    private CollectionView _expiredList;

    public ExpiredActivitiesPage(AuthService auth, ActivityService activities)
    {
        _auth = auth;
        _activities = activities;
        
        Title = "Expired Activities";
        BackgroundColor = Color.FromArgb("#F5F7FC");
        
        BuildUI();
    }

    private void BuildUI()
    {
        var rootGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            },
            BackgroundColor = Color.FromArgb("#F5F7FC")
        };

        var topStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16,
            BackgroundColor = Color.FromArgb("#F5F7FC")
        };

        topStack.Children.Add(new Label
        {
            Text = "Expired Activities",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#12161E"),
            HorizontalOptions = LayoutOptions.Center
        });

        topStack.Children.Add(new Label
        {
            Text = "The following activities have expired. For each activity, choose to:\n\u2022 Postpone - Set a new end date\n\u2022 Move to Expired - Activity moves to 'Expired' category",
            FontSize = 14,
            TextColor = Color.FromArgb("#5A6273"),
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            MaximumWidthRequest = 500
        });

        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.Center
        };

        var postponeBtn = new Button
        {
            Text = "Postpone Selected",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            WidthRequest = 150,
            HeightRequest = 45
        };
        postponeBtn.Clicked += OnPostponeClicked;
        buttonRow.Children.Add(postponeBtn);

        var expireBtn = new Button
        {
            Text = "Move to Expired",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#E64848"),
            BorderColor = Color.FromArgb("#E64848"),
            BorderWidth = 1,
            CornerRadius = 8,
            WidthRequest = 150,
            HeightRequest = 45
        };
        expireBtn.Clicked += OnExpireClicked;
        buttonRow.Children.Add(expireBtn);

        topStack.Children.Add(buttonRow);

        var secondaryButtonRow = new HorizontalStackLayout
        {
            Spacing = 10,
            HorizontalOptions = LayoutOptions.Center
        };

        var expireAllBtn = new Button
        {
            Text = "Expire All",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#666"),
            BorderColor = Color.FromArgb("#999"),
            BorderWidth = 1,
            CornerRadius = 8,
            WidthRequest = 150,
            HeightRequest = 45
        };
        expireAllBtn.Clicked += OnExpireAllClicked;
        secondaryButtonRow.Children.Add(expireAllBtn);

        var doneBtn = new Button
        {
            Text = "Done",
            BackgroundColor = Color.FromArgb("#EEE"),
            TextColor = Color.FromArgb("#333"),
            CornerRadius = 8,
            WidthRequest = 150,
            HeightRequest = 45
        };
        doneBtn.Clicked += OnDoneClicked;
        secondaryButtonRow.Children.Add(doneBtn);

        topStack.Children.Add(secondaryButtonRow);
        rootGrid.Add(topStack, 0, 0);

        _expiredList = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            Margin = new Thickness(20, 0, 20, 20)
        };

        _expiredList.ItemTemplate = new DataTemplate(() =>
        {
            var border = new Border
            {
                Padding = 15,
                Margin = new Thickness(0, 0, 0, 10),
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                StrokeThickness = 2,
                Shadow = new Shadow
                {
                    Brush = Colors.Black,
                    Offset = new Point(2, 2),
                    Radius = 4,
                    Opacity = 0.2f
                }
            };
            border.SetBinding(Border.BackgroundColorProperty, nameof(ExpiredActivityViewModel.BackgroundColor));
            border.SetBinding(Border.StrokeProperty, nameof(ExpiredActivityViewModel.BorderBrush));

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += (s, e) =>
            {
                if (s is Border { BindingContext: ExpiredActivityViewModel vm })
                {
                    ToggleSelection(vm);
                }
            };
            border.GestureRecognizers.Add(tapGesture);

            var grid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                },
                ColumnSpacing = 10,
                RowSpacing = 5
            };

            var nameRow = new HorizontalStackLayout { Spacing = 8 };
            
            var checkLabel = new Label
            {
                Text = "\u2713",
                FontSize = 16,
                VerticalOptions = LayoutOptions.Center,
                TextColor = Color.FromArgb("#1976D2")
            };
            checkLabel.SetBinding(Label.IsVisibleProperty, nameof(ExpiredActivityViewModel.IsSelected));
            nameRow.Children.Add(checkLabel);
            
            var nameLabel = new Label
            {
                FontSize = 16,
                FontAttributes = FontAttributes.Bold
            };
            nameLabel.SetBinding(Label.TextProperty, nameof(ExpiredActivityViewModel.Name));
            nameLabel.SetBinding(Label.TextColorProperty, nameof(ExpiredActivityViewModel.NameColor));
            nameRow.Children.Add(nameLabel);
            
            Grid.SetRow(nameRow, 0);
            grid.Children.Add(nameRow);

            var gameLabel = new Label
            {
                FontSize = 14,
                TextColor = Color.FromArgb("#F57C00")
            };
            gameLabel.SetBinding(Label.TextProperty, nameof(ExpiredActivityViewModel.Game), stringFormat: "Game: {0}");
            Grid.SetRow(gameLabel, 1);
            grid.Children.Add(gameLabel);

            var categoryLabel = new Label
            {
                FontSize = 14,
                TextColor = Color.FromArgb("#FF9800")
            };
            categoryLabel.SetBinding(Label.TextProperty, nameof(ExpiredActivityViewModel.Category), stringFormat: "Category: {0}");
            Grid.SetRow(categoryLabel, 2);
            grid.Children.Add(categoryLabel);

            var endDateLabel = new Label
            {
                FontSize = 12,
                TextColor = Color.FromArgb("#999")
            };
            endDateLabel.SetBinding(Label.TextProperty, nameof(ExpiredActivityViewModel.EndDateDisplay));
            Grid.SetRow(endDateLabel, 3);
            grid.Children.Add(endDateLabel);

            border.Content = grid;
            return border;
        });

        _expiredList.EmptyView = new Label
        {
            Text = "No expired activities!",
            FontSize = 16,
            TextColor = Color.FromArgb("#999"),
            Padding = 40,
            HorizontalOptions = LayoutOptions.Center
        };

        rootGrid.Add(_expiredList, 0, 1);
        Content = rootGrid;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadExpiredAsync();
    }

    private async Task LoadExpiredAsync()
    {
        var expired = await _activities.GetExpiredActivitiesAsync(_auth.CurrentUsername);
        
        if (expired.Count == 0)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }
        
        _viewModels = expired.Select(a => new ExpiredActivityViewModel(a)).ToList();
        _selectedActivityIds.Clear();
        _expiredList.ItemsSource = _viewModels;
    }

    private void ToggleSelection(ExpiredActivityViewModel vm)
    {
        vm.IsSelected = !vm.IsSelected;
        if (vm.IsSelected)
        {
            _selectedActivityIds.Add(vm.Activity.Id);
        }
        else
        {
            _selectedActivityIds.Remove(vm.Activity.Id);
        }
    }

    private async void OnPostponeClicked(object? sender, EventArgs e)
    {
        var selectedViewModels = GetSelectedViewModels();
        if (selectedViewModels.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select at least one activity first.", "OK");
            return;
        }

        var promptName = selectedViewModels.Count == 1
            ? selectedViewModels[0].Activity.Name
            : $"{selectedViewModels.Count} selected activities";
        
        // Show custom date picker modal
        var newDate = await ShowPostponeDatePickerAsync(promptName);
        
        if (newDate.HasValue)
        {
            foreach (var vm in selectedViewModels)
            {
                await _activities.PostponeActivityAsync(vm.Activity.Id, newDate.Value);
            }
            await LoadExpiredAsync();
        }
    }

    /// <summary>
    /// Show custom date picker with increment buttons
    /// </summary>
    private async Task<DateTime?> ShowPostponeDatePickerAsync(string activityName)
    {
        var tcs = new TaskCompletionSource<DateTime?>();
        
        // Start with tomorrow
        DateTime currentDate = DateTime.Today.AddDays(1);
        
        var contentStack = new VerticalStackLayout
        {
            Spacing = 16,
            Padding = 24,
            BackgroundColor = Colors.White
        };
        
        // Header
        contentStack.Children.Add(new Label
        {
            Text = "Postpone Activity",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalOptions = LayoutOptions.Center
        });
        
        // Activity name
        contentStack.Children.Add(new Label
        {
            Text = activityName,
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 3
        });
        
        contentStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#E0E0E0")
        });
        
        // Current date display
        var dateLabel = new Label
        {
            Text = currentDate.ToString("dddd, MMMM d, yyyy"),
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 8)
        };
        contentStack.Children.Add(dateLabel);
        
        // Days from today display
        var daysLabel = new Label
        {
            Text = GetDaysFromTodayText(currentDate),
            FontSize = 12,
            TextColor = Color.FromArgb("#999"),
            HorizontalOptions = LayoutOptions.Center
        };
        contentStack.Children.Add(daysLabel);
        
        // Helper to update labels
        void UpdateDateDisplay()
        {
            dateLabel.Text = currentDate.ToString("dddd, MMMM d, yyyy");
            daysLabel.Text = GetDaysFromTodayText(currentDate);
        }
        
        // Increment buttons row 1: +1, +7, +14, +30
        var row1 = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center
        };
        
        foreach (var days in new[] { 1, 7, 14, 30 })
        {
            var btn = new Button
            {
                Text = $"+{days}",
                BackgroundColor = Color.FromArgb("#5B63EE"),
                TextColor = Colors.White,
                CornerRadius = 8,
                WidthRequest = 60,
                HeightRequest = 44,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold
            };
            btn.Clicked += (s, args) =>
            {
                currentDate = currentDate.AddDays(days);
                UpdateDateDisplay();
            };
            row1.Children.Add(btn);
        }
        contentStack.Children.Add(row1);
        
        // Increment buttons row 2: +60, +90, +180, +365
        var row2 = new HorizontalStackLayout
        {
            Spacing = 8,
            HorizontalOptions = LayoutOptions.Center
        };
        
        foreach (var days in new[] { 60, 90, 180, 365 })
        {
            var btn = new Button
            {
                Text = $"+{days}",
                BackgroundColor = Color.FromArgb("#FF9800"),
                TextColor = Colors.White,
                CornerRadius = 8,
                WidthRequest = 60,
                HeightRequest = 44,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold
            };
            btn.Clicked += (s, args) =>
            {
                currentDate = currentDate.AddDays(days);
                UpdateDateDisplay();
            };
            row2.Children.Add(btn);
        }
        contentStack.Children.Add(row2);
        
        // Reset to tomorrow button
        var resetBtn = new Button
        {
            Text = "↺ Reset to Tomorrow",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        resetBtn.Clicked += (s, args) =>
        {
            currentDate = DateTime.Today.AddDays(1);
            UpdateDateDisplay();
        };
        contentStack.Children.Add(resetBtn);
        
        contentStack.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#E0E0E0"),
            Margin = new Thickness(0, 8)
        });
        
        // OK / Cancel buttons
        var buttonRow = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center
        };
        
        var okBtn = new Button
        {
            Text = "Postpone",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            WidthRequest = 120,
            HeightRequest = 48,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        };
        okBtn.Clicked += (s, args) => tcs.TrySetResult(currentDate);
        buttonRow.Children.Add(okBtn);
        
        var cancelBtn = new Button
        {
            Text = "Cancel",
            BackgroundColor = Color.FromArgb("#EEE"),
            TextColor = Color.FromArgb("#666"),
            CornerRadius = 8,
            WidthRequest = 120,
            HeightRequest = 48,
            FontSize = 16
        };
        cancelBtn.Clicked += (s, args) => tcs.TrySetResult(null);
        buttonRow.Children.Add(cancelBtn);
        
        contentStack.Children.Add(buttonRow);
        
        var popup = new Frame
        {
            CornerRadius = 16,
            BackgroundColor = Colors.White,
            Padding = 0,
            HasShadow = true,
            WidthRequest = 340,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Content = contentStack
        };
        
        var overlayPage = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#80000000"),
            Content = popup
        };
        
        await Navigation.PushModalAsync(overlayPage, false);
        
        var result = await tcs.Task;
        
        await Navigation.PopModalAsync(false);
        
        return result;
    }
    
    private string GetDaysFromTodayText(DateTime date)
    {
        int days = (date.Date - DateTime.Today).Days;
        if (days == 0) return "(today)";
        if (days == 1) return "(tomorrow)";
        return $"({days} days from today)";
    }

    private async void OnExpireClicked(object? sender, EventArgs e)
    {
        var selectedViewModels = GetSelectedViewModels();
        if (selectedViewModels.Count == 0)
        {
            await DisplayAlert("No Selection", "Please select at least one activity first.", "OK");
            return;
        }

        foreach (var vm in selectedViewModels)
        {
            await _activities.MoveToExpiredAsync(vm.Activity.Id);
        }
        await LoadExpiredAsync();
    }

    private List<ExpiredActivityViewModel> GetSelectedViewModels()
    {
        return _viewModels
            .Where(vm => _selectedActivityIds.Contains(vm.Activity.Id))
            .ToList();
    }

    private async void OnExpireAllClicked(object? sender, EventArgs e)
    {
        var expired = await _activities.GetExpiredActivitiesAsync(_auth.CurrentUsername);
        
        if (expired.Count == 0)
            return;

        bool confirm = await DisplayAlert(
            "Expire All",
            $"Move all {expired.Count} expired activities to 'Expired' category?",
            "Yes",
            "No");

        if (confirm)
        {
            foreach (var activity in expired)
            {
                await _activities.MoveToExpiredAsync(activity.Id);
            }
            await LoadExpiredAsync();
        }
    }

    private async void OnDoneClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}

public class ExpiredActivityViewModel : INotifyPropertyChanged
{
    public Activity Activity { get; }
    
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
                OnPropertyChanged(nameof(BackgroundColor));
                OnPropertyChanged(nameof(BorderBrush));
                OnPropertyChanged(nameof(NameColor));
            }
        }
    }

    public ExpiredActivityViewModel(Activity activity)
    {
        Activity = activity;
    }

    public string Name => Activity.Name;
    public string Game => Activity.Game;
    public string Category => Activity.Category;
    
    public string EndDateDisplay => Activity.EndDate.HasValue
        ? $"Expired on: {Activity.EndDate.Value:MMM dd, yyyy}"
        : "No end date";
    
    // Selection-based colors
    public Color BackgroundColor => IsSelected 
        ? Color.FromArgb("#E3F2FD")  // Light blue when selected
        : Color.FromArgb("#FFF3E0"); // Original orange tint
    
    // Border uses Brush, not Color
    public Brush BorderBrush => IsSelected 
        ? new SolidColorBrush(Color.FromArgb("#2196F3"))  // Blue border when selected
        : new SolidColorBrush(Colors.Transparent);
    
    public Color NameColor => IsSelected 
        ? Color.FromArgb("#1976D2")  // Blue text when selected
        : Color.FromArgb("#E65100"); // Original orange

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

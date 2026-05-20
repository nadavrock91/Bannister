using Bannister.Models;
using Bannister.ViewModels;

namespace Bannister.Views;

public partial class ActivityGamePage
{
    private readonly record struct BulkDisplaySchedule(string DisplayDaysOfWeek, int DisplayDayOfMonth);

    private void AddSetDisplayDaysButton(VerticalStackLayout stack)
    {
        btnSetDisplayDays = new Button
        {
            Text = "Set Display Days",
            BackgroundColor = Color.FromArgb("#607D8B"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13,
            IsVisible = !_db.IsReadOnly
        };
        btnSetDisplayDays.Clicked += OnSetDisplayDaysClicked;
        stack.Children.Add(btnSetDisplayDays);
    }

    private void SetPageContentWithBusyOverlay(View pageContent)
    {
        _busyOverlayLabel = new Label
        {
            Text = "",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        _busyOverlay = new Grid
        {
            IsVisible = false,
            InputTransparent = false,
            Children =
            {
                new BoxView
                {
                    Color = Colors.Black,
                    Opacity = 0.6
                },
                new Frame
                {
                    BackgroundColor = Colors.White,
                    CornerRadius = 12,
                    Padding = 24,
                    HasShadow = true,
                    WidthRequest = 280,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 14,
                        Children =
                        {
                            new ActivityIndicator
                            {
                                IsRunning = true,
                                Color = Color.FromArgb("#5B63EE"),
                                WidthRequest = 36,
                                HeightRequest = 36,
                                HorizontalOptions = LayoutOptions.Center
                            },
                            _busyOverlayLabel
                        }
                    }
                }
            }
        };

        Content = new Grid
        {
            Children =
            {
                pageContent,
                _busyOverlay
            }
        };
    }

    private void SetBulkBusy(bool isBusy, string statusText = "")
    {
        if (_busyOverlay == null || _busyOverlayLabel == null)
            return;

        _busyOverlayLabel.Text = statusText;
        _busyOverlay.IsVisible = isBusy;
    }

    private async void OnSetDisplayDaysClicked(object? sender, EventArgs e)
    {
        if (_db.IsReadOnly)
            return;

        var selected = _allActivities?.Where(a => a.IsSelected).ToList() ?? new List<ActivityGameViewModel>();
        if (selected.Count == 0)
        {
            await DisplayAlert("No Selection", "Select activities first, then set display days.", "OK");
            return;
        }

        var schedule = await BulkDisplayDaysPage.ShowAsync(Navigation, selected.Count);
        if (schedule == null)
            return;

        try
        {
            SetBulkBusy(true, $"Updating 0 of {selected.Count}...");

            for (int i = 0; i < selected.Count; i++)
            {
                SetBulkBusy(true, $"Updating {i + 1} of {selected.Count}...");

                var activity = selected[i].Activity;
                activity.DisplayDaysOfWeek = schedule.Value.DisplayDaysOfWeek;
                activity.DisplayDayOfMonth = schedule.Value.DisplayDayOfMonth;
                await _activities.UpdateActivityAsync(activity);
            }
        }
        finally
        {
            SetBulkBusy(false);
        }

        await DisplayAlert("Updated", $"Updated {selected.Count} activities.", "OK");
        await RefreshActivitiesAsync();
    }

    private sealed class BulkDisplayDaysPage : ContentPage
    {
        private readonly TaskCompletionSource<BulkDisplaySchedule?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CheckBox _chkSunday;
        private readonly CheckBox _chkMonday;
        private readonly CheckBox _chkTuesday;
        private readonly CheckBox _chkWednesday;
        private readonly CheckBox _chkThursday;
        private readonly CheckBox _chkFriday;
        private readonly CheckBox _chkSaturday;
        private readonly Picker _dayOfMonthPicker;

        private BulkDisplayDaysPage(int selectedCount)
        {
            Title = "Set Display Days";
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.45);

            var daysGrid = new Grid
            {
                ColumnSpacing = 4,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };

            _chkSunday = AddDayCheckbox(daysGrid, "Sun", 0);
            _chkMonday = AddDayCheckbox(daysGrid, "Mon", 1);
            _chkTuesday = AddDayCheckbox(daysGrid, "Tue", 2);
            _chkWednesday = AddDayCheckbox(daysGrid, "Wed", 3);
            _chkThursday = AddDayCheckbox(daysGrid, "Thu", 4);
            _chkFriday = AddDayCheckbox(daysGrid, "Fri", 5);
            _chkSaturday = AddDayCheckbox(daysGrid, "Sat", 6);

            _dayOfMonthPicker = new Picker
            {
                Title = "Select day",
                TextColor = Color.FromArgb("#222222"),
                TitleColor = Color.FromArgb("#999999"),
                BackgroundColor = Colors.White,
                WidthRequest = 140
            };

            var dayOptions = new List<string> { "None" };
            for (int i = 1; i <= 31; i++)
                dayOptions.Add(i.ToString());
            _dayOfMonthPicker.ItemsSource = dayOptions;
            _dayOfMonthPicker.SelectedIndex = 0;

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Color.FromArgb("#ECEFF1"),
                TextColor = Color.FromArgb("#333333"),
                CornerRadius = 8,
                HeightRequest = 44
            };
            cancelButton.Clicked += async (_, _) => await CompleteAsync(null);

            var applyButton = new Button
            {
                Text = "Apply to Selected",
                BackgroundColor = Color.FromArgb("#5B63EE"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44
            };
            applyButton.Clicked += async (_, _) => await CompleteAsync(BuildSchedule());

            var buttonGrid = new Grid
            {
                ColumnSpacing = 10,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                }
            };
            buttonGrid.Add(cancelButton, 0, 0);
            buttonGrid.Add(applyButton, 1, 0);

            Content = new Grid
            {
                Padding = 20,
                Children =
                {
                    new Frame
                    {
                        BackgroundColor = Colors.White,
                        CornerRadius = 12,
                        Padding = 20,
                        HasShadow = true,
                        MaximumWidthRequest = 520,
                        HorizontalOptions = LayoutOptions.Fill,
                        VerticalOptions = LayoutOptions.Center,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 14,
                            Children =
                            {
                                new Label
                                {
                                    Text = $"Apply Display Schedule to {selectedCount} Activities",
                                    FontSize = 20,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#333333")
                                },
                                new Label
                                {
                                    Text = "Choose weekdays and/or a day of the month. This will fully replace the selected activities' current display schedule.",
                                    FontSize = 12,
                                    TextColor = Color.FromArgb("#666666"),
                                    LineBreakMode = LineBreakMode.WordWrap
                                },
                                daysGrid,
                                new HorizontalStackLayout
                                {
                                    Spacing = 8,
                                    Children =
                                    {
                                        new Label
                                        {
                                            Text = "Day of month:",
                                            TextColor = Color.FromArgb("#666666"),
                                            VerticalOptions = LayoutOptions.Center
                                        },
                                        _dayOfMonthPicker
                                    }
                                },
                                buttonGrid
                            }
                        }
                    }
                }
            };
        }

        private static CheckBox AddDayCheckbox(Grid grid, string label, int column)
        {
            var stack = new VerticalStackLayout
            {
                Spacing = 2,
                HorizontalOptions = LayoutOptions.Center
            };

            var checkBox = new CheckBox
            {
                Color = Color.FromArgb("#5B63EE"),
                HorizontalOptions = LayoutOptions.Center
            };

            stack.Children.Add(checkBox);
            stack.Children.Add(new Label
            {
                Text = label,
                FontSize = 11,
                TextColor = Color.FromArgb("#333333"),
                HorizontalTextAlignment = TextAlignment.Center
            });

            Grid.SetColumn(stack, column);
            grid.Children.Add(stack);
            return checkBox;
        }

        private BulkDisplaySchedule BuildSchedule()
        {
            var days = new List<string>();
            if (_chkSunday.IsChecked) days.Add("Sun");
            if (_chkMonday.IsChecked) days.Add("Mon");
            if (_chkTuesday.IsChecked) days.Add("Tue");
            if (_chkWednesday.IsChecked) days.Add("Wed");
            if (_chkThursday.IsChecked) days.Add("Thu");
            if (_chkFriday.IsChecked) days.Add("Fri");
            if (_chkSaturday.IsChecked) days.Add("Sat");

            string displayDaysOfWeek = days.Count == 0 || days.Count == 7
                ? ""
                : string.Join(",", days);

            int displayDayOfMonth = _dayOfMonthPicker.SelectedIndex > 0
                ? _dayOfMonthPicker.SelectedIndex
                : 0;

            return new BulkDisplaySchedule(displayDaysOfWeek, displayDayOfMonth);
        }

        private async Task CompleteAsync(BulkDisplaySchedule? result)
        {
            if (_completion.Task.IsCompleted)
                return;

            _completion.SetResult(result);
            await Navigation.PopModalAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            _completion.TrySetResult(null);
            return base.OnBackButtonPressed();
        }

        public static async Task<BulkDisplaySchedule?> ShowAsync(INavigation navigation, int selectedCount)
        {
            var page = new BulkDisplayDaysPage(selectedCount);
            await navigation.PushModalAsync(page);
            return await page._completion.Task;
        }
    }
}

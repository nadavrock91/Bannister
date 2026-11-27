using Bannister.Models;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Bannister.Services;

namespace Bannister.Views;

public class SetAutoAwardPage : Bannister.Views.ModeThemedPage
{
    private readonly Activity _activity;
    private readonly bool _requireAutoAward;
    private TaskCompletionSource<bool>? _completionSource;

    private Switch switchAutoAward;
    private Picker frequencyPicker;
    private VerticalStackLayout daysPanel;
    private Dictionary<string, CheckBox> dayCheckBoxes;

    public SetAutoAwardPage(Activity activity, bool requireAutoAward = false)
    {
        _activity = activity;
        _requireAutoAward = requireAutoAward;
        dayCheckBoxes = new Dictionary<string, CheckBox>();

        if (_requireAutoAward)
        {
            _activity.IsAutoAward = true;
            if (string.IsNullOrWhiteSpace(_activity.AutoAwardFrequency) || _activity.AutoAwardFrequency == "None")
            {
                _activity.AutoAwardFrequency = "Daily";
            }
        }

        Title = "Auto-Award Settings";


        BuildUI();
        LoadCurrentSettings();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 20
        };

        // Header
        var lblTitle = (new Label
        {
            Text = "Auto-Award Settings",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true);
        mainStack.Children.Add(lblTitle);

        var lblActivity = (new Label
        {
            Text = _activity.Name,
            FontSize = 16
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
        mainStack.Children.Add(lblActivity);

        // Enable/Disable Auto-Award
        var autoAwardFrame = (new Frame
        {
            Padding = 15,
            CornerRadius = 8,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);

        var autoAwardStack = new HorizontalStackLayout
        {
            Spacing = 15
        };

        var lblEnable = new Label
        {
            Text = "Enable Auto-Award",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        autoAwardStack.Children.Add(lblEnable);

        switchAutoAward = new Switch
        {
            OnColor = AppColors.Get(ThemeKey.SuccessButtonBackground),
            VerticalOptions = LayoutOptions.Center,
            IsEnabled = !_requireAutoAward
        };
        switchAutoAward.Toggled += OnAutoAwardToggled;
        autoAwardStack.Children.Add(switchAutoAward);

        autoAwardFrame.Content = autoAwardStack;
        mainStack.Children.Add(autoAwardFrame);

        // Frequency Picker
        var freqFrame = (new Frame
        {
            Padding = 15,
            CornerRadius = 8
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

        var freqStack = new VerticalStackLayout { Spacing = 10 };

        var lblFrequency = new Label
        {
            Text = "Frequency",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        };
        freqStack.Children.Add(lblFrequency);

        frequencyPicker = new Picker
        {
            Title = "Select Frequency",
            ItemsSource = new List<string> { "Daily", "Weekly", "Monthly" },
            FontSize = 14
        };
        frequencyPicker.SelectedIndexChanged += OnFrequencyChanged;
        freqStack.Children.Add(frequencyPicker);

        freqFrame.Content = freqStack;
        mainStack.Children.Add(freqFrame);

        // Days Selection (for Weekly)
        daysPanel = new VerticalStackLayout { Spacing = 10, IsVisible = false };

        var lblDays = new Label
        {
            Text = "Select Days",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 10, 0, 0)
        };
        daysPanel.Children.Add(lblDays);

        var daysFrame = (new Frame
        {
            Padding = 15,
            CornerRadius = 8
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

        var daysStack = new VerticalStackLayout { Spacing = 8 };

        string[] daysOfWeek = { "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday" };
        foreach (var day in daysOfWeek)
        {
            var checkStack = new HorizontalStackLayout { Spacing = 10 };

            var checkbox = (new CheckBox
            {
            }).ThemeColor(CheckBox.ColorProperty, ThemeKey.SuccessButtonBackground);
            dayCheckBoxes[day] = checkbox;
            checkStack.Children.Add(checkbox);

            var lblDay = new Label
            {
                Text = day,
                FontSize = 14,
                VerticalOptions = LayoutOptions.Center
            };
            checkStack.Children.Add(lblDay);

            daysStack.Children.Add(checkStack);
        }

        daysFrame.Content = daysStack;
        daysPanel.Children.Add(daysFrame);

        mainStack.Children.Add(daysPanel);

        // Info labels
        var infoFrame = (new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BorderColor = Colors.Transparent,
            Margin = new Thickness(0, 10, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground);

        var lblInfo = (new Label
        {
            Text = "ℹ️ Auto-award activities will appear in a confirmation dialog when you first enter the game each day.",
            FontSize = 12,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText3, classifyPageText: true);

        infoFrame.Content = lblInfo;
        mainStack.Children.Add(infoFrame);

        // Buttons
        var buttonStack = new HorizontalStackLayout
        {
            Spacing = 10,
            Margin = new Thickness(0, 20, 0, 0)
        };

        var btnSave = (new Button
        {
            Text = "Save",
            CornerRadius = 8,
            HorizontalOptions = LayoutOptions.FillAndExpand
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnSave.Clicked += OnSaveClicked;
        buttonStack.Children.Add(btnSave);

        var btnCancel = (new Button
        {
            Text = "Cancel",
            CornerRadius = 8,
            HorizontalOptions = LayoutOptions.FillAndExpand
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.DisabledButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnCancel.Clicked += OnCancelClicked;
        buttonStack.Children.Add(btnCancel);

        mainStack.Children.Add(buttonStack);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private void LoadCurrentSettings()
    {
        switchAutoAward.IsToggled = _activity.IsAutoAward;
        
        if (_activity.AutoAwardFrequency == "Daily")
            frequencyPicker.SelectedIndex = 0;
        else if (_activity.AutoAwardFrequency == "Weekly")
            frequencyPicker.SelectedIndex = 1;
        else if (_activity.AutoAwardFrequency == "Monthly")
            frequencyPicker.SelectedIndex = 2;

        if (!string.IsNullOrEmpty(_activity.AutoAwardDays))
        {
            var selectedDays = _activity.AutoAwardDays.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var day in selectedDays)
            {
                if (dayCheckBoxes.ContainsKey(day.Trim()))
                {
                    dayCheckBoxes[day.Trim()].IsChecked = true;
                }
            }
        }

        OnAutoAwardToggled(null, new ToggledEventArgs(switchAutoAward.IsToggled));
    }

    private void OnAutoAwardToggled(object? sender, ToggledEventArgs e)
    {
        frequencyPicker.IsEnabled = e.Value;
        UpdateDaysPanelVisibility();
    }

    private void OnFrequencyChanged(object? sender, EventArgs e)
    {
        UpdateDaysPanelVisibility();
    }

    private void UpdateDaysPanelVisibility()
    {
        daysPanel.IsVisible = switchAutoAward.IsToggled && 
                              frequencyPicker.SelectedIndex == 1; // Weekly
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        _activity.IsAutoAward = switchAutoAward.IsToggled;

        if (_requireAutoAward && !_activity.IsAutoAward)
        {
            await DisplayAlert("Auto-Award Required", "Activities created in the Auto category must have auto-award enabled.", "OK");
            return;
        }

        if (switchAutoAward.IsToggled)
        {
            if (frequencyPicker.SelectedIndex == -1)
            {
                await DisplayAlert("Validation", "Please select a frequency", "OK");
                return;
            }

            _activity.AutoAwardFrequency = frequencyPicker.SelectedItem?.ToString() ?? "None";

            if (_activity.AutoAwardFrequency == "Weekly")
            {
                var selectedDays = dayCheckBoxes
                    .Where(kvp => kvp.Value.IsChecked)
                    .Select(kvp => kvp.Key)
                    .ToList();

                if (selectedDays.Count == 0)
                {
                    await DisplayAlert("Validation", "Please select at least one day", "OK");
                    return;
                }

                _activity.AutoAwardDays = string.Join(",", selectedDays);
            }
            else
            {
                _activity.AutoAwardDays = "";
            }

            // Move to "Auto" category
            _activity.Category = "Auto";
        }
        else
        {
            _activity.AutoAwardFrequency = "None";
            _activity.AutoAwardDays = "";
        }

        _completionSource?.SetResult(true);
        await Navigation.PopModalAsync();
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        _completionSource?.SetResult(false);
        await Navigation.PopModalAsync();
    }

    public Task<bool> WaitForResultAsync()
    {
        _completionSource = new TaskCompletionSource<bool>();
        return _completionSource.Task;
    }
}

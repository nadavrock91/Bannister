using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public record SubActivityStepOption(int StepIndex, string Name);

public class SubActivityDailyPromptPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly SubActivityService _subActivityService;
    private readonly SubActivity _process;
    private readonly Dictionary<int, int> _stepStates = new();
    private bool _isClosing;

    private SubActivityDailyPromptPage(SubActivityService subActivityService, SubActivity process)
    {
        _subActivityService = subActivityService;
        _process = process;

        Title = "Daily Routine Check";
        BackgroundColor = Color.FromArgb("#80000000");

        var steps = _subActivityService.GetSteps(process)
            .Select((step, index) => new SubActivityStepOption(index, step.Name))
            .Where(step => !string.IsNullOrWhiteSpace(step.Name))
            .ToList();

        foreach (var step in steps)
        {
            _stepStates[step.StepIndex] = (int)SubActivityStepSubmissionState.Done;
        }

        var card = new Frame
        {
            Padding = 22,
            CornerRadius = 12,
            HasShadow = true,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#D0D7DE"),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            WidthRequest = 640,
            MaximumWidthRequest = 760
        };

        var content = new VerticalStackLayout { Spacing = 14 };
        content.Children.Add(new Label
        {
            Text = "Daily Routine Check",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1F2937"),
            HorizontalTextAlignment = TextAlignment.Center
        });
        content.Children.Add(new Label
        {
            Text = process.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111827"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        content.Children.Add(new Label
        {
            Text = "All steps are marked Done by default. Change any that you didn't complete.",
            FontSize = 13,
            TextColor = Color.FromArgb("#6B7280"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var stepsStack = new VerticalStackLayout { Spacing = 10 };
        foreach (var step in steps)
        {
            stepsStack.Children.Add(CreateStepRow(step));
        }

        content.Children.Add(new ScrollView
        {
            MaximumHeightRequest = 380,
            Content = stepsStack
        });

        var buttons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10
        };

        var submit = new Button
        {
            Text = "Submit Today's Progress",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 46
        };
        submit.Clicked += async (_, _) => await SubmitAsync();

        var skip = new Button
        {
            Text = "Skip",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#263238"),
            CornerRadius = 8,
            HeightRequest = 46
        };
        skip.Clicked += async (_, _) => await CloseAsync(false);

        buttons.Add(submit, 0, 0);
        buttons.Add(skip, 1, 0);
        content.Children.Add(buttons);

        card.Content = content;
        Content = new Grid
        {
            Padding = 20,
            BackgroundColor = Color.FromArgb("#80000000"),
            Children = { card }
        };
    }

    public static async Task<bool> ShowAsync(INavigation navigation, SubActivityService subActivityService, SubActivity process)
    {
        var page = new SubActivityDailyPromptPage(subActivityService, process);
        await navigation.PushModalAsync(page, false);
        return await page._completion.Task;
    }

    private View CreateStepRow(SubActivityStepOption step)
    {
        string groupName = $"sub_step_{_process.Id}_{step.StepIndex}_{Guid.NewGuid():N}";

        var row = new VerticalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(10, 8),
            BackgroundColor = Color.FromArgb("#F8FAFC")
        };

        row.Children.Add(new Label
        {
            Text = step.Name,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#374151"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var options = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            Padding = new Thickness(0, 4)
        };

        var doneCol = CreateStateRadio(step.StepIndex, groupName, "Done", SubActivityStepSubmissionState.Done, true);
        var notDoneCol = CreateStateRadio(step.StepIndex, groupName, "Not Done", SubActivityStepSubmissionState.NotDone, false);
        var notRelevantCol = CreateStateRadio(step.StepIndex, groupName, "Not Relevant", SubActivityStepSubmissionState.NotRelevant, false);

        options.Add(doneCol, 0, 0);
        options.Add(notDoneCol, 1, 0);
        options.Add(notRelevantCol, 2, 0);
        row.Children.Add(options);

        return row;
    }

    private View CreateStateRadio(int stepIndex, string groupName, string label, SubActivityStepSubmissionState state, bool selected)
    {
        var radio = new RadioButton
        {
            GroupName = groupName,
            IsChecked = selected,
            HorizontalOptions = LayoutOptions.Center
        };
        radio.CheckedChanged += (_, e) =>
        {
            System.Diagnostics.Debug.WriteLine($"[SubActivityPrompt] Radio CheckedChanged stepIndex={stepIndex} state={state} newValue={e.Value}");
            if (e.Value)
            {
                _stepStates[stepIndex] = (int)state;
                System.Diagnostics.Debug.WriteLine($"[SubActivityPrompt] _stepStates[{stepIndex}] set to {(int)state} ({state})");
            }
        };

        if (selected)
        {
            System.Diagnostics.Debug.WriteLine($"[SubActivityPrompt] Radio default-checked at construction: stepIndex={stepIndex} state={state}");
        }

        var labelView = new Label
        {
            Text = label,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#374151"),
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var labelTap = new TapGestureRecognizer();
        labelTap.Tapped += (_, _) =>
        {
            radio.IsChecked = true;
        };
        labelView.GestureRecognizers.Add(labelTap);

        var wrapper = new VerticalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Center,
            WidthRequest = 100,
            Padding = new Thickness(4, 6),
            Children =
            {
                labelView,
                radio
            }
        };

        var wrapperTap = new TapGestureRecognizer();
        wrapperTap.Tapped += (_, _) =>
        {
            radio.IsChecked = true;
        };
        wrapper.GestureRecognizers.Add(wrapperTap);

        return wrapper;
    }

    private async Task SubmitAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[SubActivityPrompt] SubmitAsync _stepStates contents ({_stepStates.Count} entries):");
        foreach (var kvp in _stepStates)
        {
            System.Diagnostics.Debug.WriteLine($"[SubActivityPrompt]   [{kvp.Key}] = {kvp.Value} ({(SubActivityStepSubmissionState)kvp.Value})");
        }

        var result = await _subActivityService.SubmitDailySubAsync(_process.Id, _stepStates);

        // Refresh process to get updated values
        var refreshed = await _subActivityService.GetByIdAsync(_process.Id);
        if (refreshed == null)
        {
            await CloseAsync(result.Submitted);
            return;
        }

        // Always show status after submission (like allowance check-in does)
        int activeSteps = _subActivityService.GetSteps(refreshed).Count;
        string statusMessage = $"Streak: {refreshed.ConsecutiveAllDoneDays}/3 days\n" +
                               $"Allowance: {refreshed.Allowance} (active steps: {activeSteps})\n" +
                               $"Total completions: {refreshed.TotalCompletions}";

        if (result.MilestoneReached)
        {
            // Allowance already incremented by SubmitDailySubAsync — do NOT revert if user skips adding
            await DisplayAlert(
                "3-Day Streak! Allowance Increased",
                $"'{refreshed.Name}' allowance raised to {refreshed.Allowance}!\n\n{statusMessage}\n\nYou can fill the new slot now or leave it empty for later.",
                "OK");

            // Offer to fill the new slot
            await OfferFillNewSlotAsync(refreshed);

            // Reset streak regardless of whether slot was filled
            await _subActivityService.ResetConsecutiveAllDoneDaysAsync(refreshed.Id);
        }
        else
        {
            await DisplayAlert(
                $"{refreshed.Name} — Status",
                statusMessage,
                "OK");
        }

        await CloseAsync(result.Submitted);
    }

    private async Task OfferFillNewSlotAsync(SubActivity process)
    {
        var pendingSteps = _subActivityService.GetPendingSteps(process);
        var activeSteps = _subActivityService.GetSteps(process);

        // Build options list
        var options = new List<string>();

        if (pendingSteps.Count > 0)
        {
            foreach (var step in pendingSteps)
                options.Add($"Pending: {step.Name}");
        }

        options.Add("Type manually");
        options.Add("Leave empty for now");

        string choice = await DisplayActionSheet(
            $"Fill new slot in '{process.Name}'?",
            null, // no cancel — must choose
            null,
            options.ToArray());

        if (string.IsNullOrEmpty(choice) || choice == "Leave empty for now")
            return;

        if (choice == "Type manually")
        {
            string? stepName = await DisplayPromptAsync(
                "New Step",
                $"Enter a new step for {process.Name}:",
                "Save",
                "Cancel");

            if (!string.IsNullOrWhiteSpace(stepName))
            {
                await _subActivityService.TryAddStepAsync(process.Id, stepName.Trim());
            }
            return;
        }

        // Selected a pending step — find which one
        if (choice.StartsWith("Pending: "))
        {
            string selectedName = choice.Substring("Pending: ".Length);
            int pendingIndex = pendingSteps.FindIndex(s =>
                string.Equals(s.Name, selectedName, StringComparison.Ordinal));

            if (pendingIndex >= 0)
            {
                // Re-fetch process to ensure fresh state
                var freshProcess = await _subActivityService.GetByIdAsync(process.Id);
                if (freshProcess != null)
                {
                    await _subActivityService.ActivatePendingStepAsync(freshProcess, pendingIndex);
                }
            }
        }
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(false);
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_isClosing) return;
        _completion.TrySetResult(false);
    }

    private async Task CloseAsync(bool submitted)
    {
        if (_isClosing) return;
        _isClosing = true;
        await Navigation.PopModalAsync(false);
        _completion.TrySetResult(submitted);
    }
}

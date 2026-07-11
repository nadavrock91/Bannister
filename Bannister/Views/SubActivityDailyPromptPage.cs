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
            _stepStates[step.StepIndex] = (int)SubActivityStepSubmissionState.NotDone;
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
            Text = "Mark each step for today. Not Relevant counts as complete for the daily streak.",
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

        var options = new HorizontalStackLayout
        {
            Spacing = 14
        };

        options.Children.Add(CreateStateRadio(step.StepIndex, groupName, "Done", SubActivityStepSubmissionState.Done, false));
        options.Children.Add(CreateStateRadio(step.StepIndex, groupName, "Not Done", SubActivityStepSubmissionState.NotDone, true));
        options.Children.Add(CreateStateRadio(step.StepIndex, groupName, "Not Relevant", SubActivityStepSubmissionState.NotRelevant, false));
        row.Children.Add(options);

        return row;
    }

    private View CreateStateRadio(int stepIndex, string groupName, string label, SubActivityStepSubmissionState state, bool selected)
    {
        var radio = new RadioButton
        {
            GroupName = groupName,
            IsChecked = selected,
            VerticalOptions = LayoutOptions.Center
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
            TextColor = Color.FromArgb("#374151"),
            VerticalOptions = LayoutOptions.Center
        };

        var labelTap = new TapGestureRecognizer();
        labelTap.Tapped += (_, _) =>
        {
            radio.IsChecked = true;
        };
        labelView.GestureRecognizers.Add(labelTap);

        var wrapper = new HorizontalStackLayout
        {
            Spacing = 4,
            Children =
            {
                radio,
                labelView
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

        if (result.MilestoneReached)
        {
            string action = await DisplayActionSheet(
                $"Add a step to {_process.Name}?",
                "Cancel",
                null,
                "Yes",
                "No");

            if (action == "Yes")
            {
                string? stepName = await DisplayPromptAsync(
                    "New Step",
                    $"Enter a new step for {_process.Name}:",
                    "Save",
                    "Cancel");

                if (!string.IsNullOrWhiteSpace(stepName) &&
                    await _subActivityService.TryAddStepAsync(_process.Id, stepName.Trim()))
                {
                    await _subActivityService.ResetConsecutiveAllDoneDaysAsync(_process.Id);
                    await CloseAsync(true);
                    return;
                }

                await _subActivityService.RevertAllowanceAsync(_process.Id);
            }
            else
            {
                await _subActivityService.RevertAllowanceAsync(_process.Id);
            }
        }

        await CloseAsync(result.Submitted);
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

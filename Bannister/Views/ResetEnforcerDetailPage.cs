using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ResetEnforcerDetailPage : ContentPage
{
    private readonly ResetEnforcerService _service;
    private readonly AuthService _auth;
    private ResetEnforcer _enforcer;
    private VerticalStackLayout _conditionsContainer = null!;
    private Label _resetCountLabel = null!;
    private Image _enforcerImage = null!;
    private bool _hasImage = false;

    public ResetEnforcerDetailPage(
        ResetEnforcerService service,
        AuthService auth,
        ResetEnforcer enforcer)
    {
        _service = service;
        _auth = auth;
        _enforcer = enforcer;
        Title = enforcer.Name;
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        // Header with image
        _enforcerImage = new Image
        {
            HeightRequest = 200,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        UpdateImageDisplay();
        stack.Children.Add(_enforcerImage);

        var changeImageBtn = new Button
        {
            Text = " Change Image",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(14, 0)
        };
        changeImageBtn.Clicked += async (_, _) =>
            await ChangeImageAsync();
        stack.Children.Add(changeImageBtn);

        var cycleAspectBtn = new Button
        {
            Text = "⟳ Cycle Image Fit",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.Center,
            Padding = new Thickness(14, 0)
        };
        cycleAspectBtn.Clicked += async (_, _) =>
        {
            _enforcer.ImageAspect = (_enforcer.ImageAspect + 1) % 3;
            await _service.UpdateEnforcerAsync(_enforcer);
            UpdateImageDisplay();
        };
        stack.Children.Add(cycleAspectBtn);

        stack.Children.Add(new Label
        {
            Text = _enforcer.Name,
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            HorizontalOptions = LayoutOptions.Center
        });

        // Reset count
        _resetCountLabel = new Label
        {
            Text = $"Total Resets: {_enforcer.TotalResets}",
            FontSize = 16,
            TextColor = Color.FromArgb("#C62828"),
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Center
        };
        stack.Children.Add(_resetCountLabel);

        // Reset button
        var resetBtn = new Button
        {
            Text = "⚡ Reset",
            BackgroundColor = Color.FromArgb("#C62828"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 16,
            HeightRequest = 50,
            FontAttributes = FontAttributes.Bold
        };
        resetBtn.Clicked += async (_, _) => await DoResetAsync();
        stack.Children.Add(resetBtn);

        // Conditions section
        stack.Children.Add(new Label
        {
            Text = "Reset Conditions",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0"),
            Margin = new Thickness(0, 8, 0, 0)
        });
        stack.Children.Add(new Label
        {
            Text = "Define what triggers a reset for this enforcer.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        _conditionsContainer = new VerticalStackLayout { Spacing = 8 };
        stack.Children.Add(_conditionsContainer);

        var addConditionBtn = new Button
        {
            Text = "+ New Reset Condition",
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            TextColor = Color.FromArgb("#2E7D32"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(14, 0)
        };
        addConditionBtn.Clicked += async (_, _) =>
            await AddConditionAsync();
        stack.Children.Add(addConditionBtn);

        Content = new ScrollView { Content = stack };
    }

    private async Task RefreshAsync()
    {
        var updated = await _service.GetEnforcerAsync(_enforcer.Id);
        if (updated != null)
        {
            _enforcer = updated;
            _resetCountLabel.Text = $"Total Resets: {_enforcer.TotalResets}";
        }

        var conditions = await _service.GetConditionsAsync(_enforcer.Id);
        _conditionsContainer.Children.Clear();

        if (conditions.Count == 0)
        {
            _conditionsContainer.Children.Add(new Label
            {
                Text = "No conditions defined yet.",
                FontSize = 12,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        foreach (var condition in conditions)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(36))
                },
                ColumnSpacing = 8
            };

            row.Add(new Label
            {
                Text = condition.Text,
                FontSize = 13,
                TextColor = Color.FromArgb("#222"),
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.WordWrap
            }, 0, 0);

            var delBtn = new Button
            {
                Text = "✕",
                FontSize = 13,
                HeightRequest = 34,
                WidthRequest = 34,
                CornerRadius = 6,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#C62828")
            };
            var capturedId = condition.Id;
            delBtn.Clicked += async (_, _) =>
            {
                bool confirm = await DisplayAlert(
                    "Delete Condition",
                    $"Delete \"{condition.Text}\"?",
                    "Delete", "Cancel");
                if (!confirm) return;
                await _service.DeleteConditionAsync(capturedId);
                await RefreshAsync();
            };
            row.Add(delBtn, 1, 0);
            _conditionsContainer.Children.Add(row);
        }
    }

    private async Task DoResetAsync()
    {
        bool confirm = await DisplayAlert(
            "Confirm Reset",
            $"Increment reset count for \"{_enforcer.Name}\"?",
            "Reset", "Cancel");
        if (!confirm) return;
        await _service.IncrementResetAsync(_enforcer.Id);
        await RefreshAsync();
    }

    private async Task AddConditionAsync()
    {
        string? text = await DisplayPromptAsync(
            "New Reset Condition",
            "What triggers a reset?",
            "Add", "Cancel",
            placeholder: "e.g. Missed a daily commitment");
        if (string.IsNullOrWhiteSpace(text)) return;
        await _service.AddConditionAsync(_enforcer.Id, text.Trim());
        await RefreshAsync();
    }

    private void UpdateImageDisplay()
    {
        _hasImage = !string.IsNullOrWhiteSpace(_enforcer.ImagePath) &&
            File.Exists(_enforcer.ImagePath);
        if (_hasImage)
        {
            _enforcerImage.Source =
                ImageSource.FromFile(_enforcer.ImagePath);
            _enforcerImage.Aspect = AspectFromInt(_enforcer.ImageAspect);
            _enforcerImage.IsVisible = true;
        }
        else
        {
            _enforcerImage.IsVisible = false;
        }
    }

    private static Aspect AspectFromInt(int value) => value switch
    {
        1 => Aspect.AspectFill,
        2 => Aspect.Fill,
        _ => Aspect.AspectFit
    };

    private async Task ChangeImageAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select enforcer image",
                FileTypes = FilePickerFileType.Images
            });
            if (result == null) return;

            _enforcer.ImagePath = result.FullPath;
            await _service.UpdateEnforcerAsync(_enforcer);
            UpdateImageDisplay();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error",
                $"Could not pick image: {ex.Message}", "OK");
        }
    }
}

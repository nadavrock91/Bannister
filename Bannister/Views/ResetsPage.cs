using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class ResetsPage : ContentPage
{
    private readonly ResetEnforcerService _service;
    private readonly AuthService _auth;
    private VerticalStackLayout _cardsContainer = null!;

    public ResetsPage(ResetEnforcerService service, AuthService auth)
    {
        _service = service;
        _auth = auth;
        Title = "Resets";
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

        stack.Children.Add(new Label
        {
            Text = "⚡ Resets",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Track reset enforcers — commitments where breaking " +
                   "them resets your count.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var addBtn = new Button
        {
            Text = "+ Add New Reset Enforcer",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold
        };
        addBtn.Clicked += async (_, _) => await AddEnforcerAsync();
        stack.Children.Add(addBtn);

        _cardsContainer = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(_cardsContainer);

        Content = new ScrollView { Content = stack };
    }

    private async Task RefreshAsync()
    {
        var enforcers = await _service.GetEnforcersAsync(
            _auth.CurrentUsername);
        _cardsContainer.Children.Clear();

        if (enforcers.Count == 0)
        {
            _cardsContainer.Children.Add(new Label
            {
                Text = "No reset enforcers yet. Tap '+ Add New Reset " +
                       "Enforcer' to create one.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        foreach (var enforcer in enforcers)
            _cardsContainer.Children.Add(BuildEnforcerCard(enforcer));
    }

    private View BuildEnforcerCard(ResetEnforcer enforcer)
    {
        var card = new Frame
        {
            BackgroundColor = Colors.White,
            Padding = 16,
            CornerRadius = 12,
            HasShadow = true
        };

        var inner = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(80)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(80))
            },
            ColumnSpacing = 12,
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 4
        };

        // Image
        if (!string.IsNullOrWhiteSpace(enforcer.ImagePath) &&
            File.Exists(enforcer.ImagePath))
        {
            var cardImg = new Image
            {
                Source = ImageSource.FromFile(enforcer.ImagePath),
                HeightRequest = 70,
                WidthRequest = 70,
                Aspect = AspectFromInt(enforcer.ImageAspect),
                VerticalOptions = LayoutOptions.Center
            };
            inner.Add(cardImg, 0, 0);
            Grid.SetRowSpan(cardImg, 2);

            var cycleBtn = new Button
            {
                Text = "⟳",
                FontSize = 10,
                HeightRequest = 22,
                WidthRequest = 70,
                CornerRadius = 4,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#ECEFF1"),
                TextColor = Color.FromArgb("#37474F")
            };
            var capturedEnf = enforcer;
            cycleBtn.Clicked += async (_, _) =>
            {
                capturedEnf.ImageAspect = (capturedEnf.ImageAspect + 1) % 3;
                await _service.UpdateEnforcerAsync(capturedEnf);
                cardImg.Aspect = AspectFromInt(capturedEnf.ImageAspect);
            };
            inner.Add(cycleBtn, 0, 2);
        }
        else
        {
            inner.Add(new Label
            {
                Text = "",
                FontSize = 40,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
        }

        // Name
        inner.Add(new Label
        {
            Text = enforcer.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.End
        }, 1, 0);

        // Reset count
        inner.Add(new Label
        {
            Text = $"Resets: {enforcer.TotalResets}",
            FontSize = 13,
            TextColor = Color.FromArgb("#C62828"),
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Start
        }, 1, 1);

        // Reset button
        var resetBtn = new Button
        {
            Text = "Reset",
            BackgroundColor = Color.FromArgb("#C62828"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 36,
            VerticalOptions = LayoutOptions.Center
        };
        var capturedEnforcer = enforcer;
        resetBtn.Clicked += async (_, _) =>
        {
            bool confirm = await DisplayAlert(
                "Confirm Reset",
                $"Increment reset count for \"{enforcer.Name}\"?",
                "Reset", "Cancel");
            if (!confirm) return;
            await _service.IncrementResetAsync(capturedEnforcer.Id);
            await RefreshAsync();
        };
        inner.Add(resetBtn, 2, 0);
        Grid.SetRowSpan(resetBtn, 2);

        card.Content = inner;

        // Tap card to open detail page
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            await Navigation.PushAsync(
                new ResetEnforcerDetailPage(
                    _service, _auth, capturedEnforcer));
        };
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private static Aspect AspectFromInt(int value) => value switch
    {
        1 => Aspect.AspectFill,
        2 => Aspect.Fill,
        _ => Aspect.AspectFit
    };

    private async Task AddEnforcerAsync()
    {
        string? name = await DisplayPromptAsync(
            "New Reset Enforcer",
            "Name this reset enforcer:",
            "Next", "Cancel",
            placeholder: "e.g. No Junk Food Streak");
        if (string.IsNullOrWhiteSpace(name)) return;

        string imagePath = "";
        bool pickImage = await DisplayAlert(
            "Add Image",
            "Would you like to add an image for this enforcer?",
            "Pick Image", "Skip");

        if (pickImage)
        {
            try
            {
                var result = await FilePicker.PickAsync(new PickOptions
                {
                    PickerTitle = "Select enforcer image",
                    FileTypes = FilePickerFileType.Images
                });
                if (result != null)
                    imagePath = result.FullPath;
            }
            catch { }
        }

        await _service.AddEnforcerAsync(
            _auth.CurrentUsername, name.Trim(), imagePath);
        await RefreshAsync();
    }
}

using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class AllLookoutsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly LookoutService _lookoutService;
    private VerticalStackLayout _contentContainer = null!;

    public AllLookoutsPage(
        AuthService auth,
        LookoutService lookoutService)
    {
        _auth = auth;
        _lookoutService = lookoutService;
        Title = "All Lookouts";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 12
        };

        stack.Children.Add(new Label
        {
            Text = " All Lookouts",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Scenarios to watch for across all dates.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        var addBtn = new Button
        {
            Text = "+ New Lookout",
            BackgroundColor = Color.FromArgb("#6A1B9A"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 40,
            HorizontalOptions = LayoutOptions.Start,
            Padding = new Thickness(16, 0)
        };
        addBtn.Clicked += async (_, _) =>
            await AddLookoutAsync();
        stack.Children.Add(addBtn);

        _contentContainer = new VerticalStackLayout
        {
            Spacing = 6
        };
        stack.Children.Add(_contentContainer);

        Content = new ScrollView { Content = stack };
    }

    private async Task LoadAsync()
    {
        _contentContainer.Children.Clear();

        var lookouts = await _lookoutService
            .GetAllAsync(_auth.CurrentUsername);

        if (lookouts.Count == 0)
        {
            _contentContainer.Children.Add(new Label
            {
                Text = "No lookouts yet. " +
                       "Create one from the calendar or " +
                       "tap + New Lookout above.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
            return;
        }

        // Group by date
        var withDate = lookouts
            .Where(l => l.DisplayDate.HasValue)
            .OrderBy(l => l.DisplayDate)
            .ToList();
        var noDate = lookouts
            .Where(l => !l.DisplayDate.HasValue)
            .OrderByDescending(l => l.CreatedAt)
            .ToList();

        if (withDate.Count > 0)
        {
            _contentContainer.Children.Add(new Label
            {
                Text = " By Date",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0"),
                Margin = new Thickness(0, 4, 0, 2)
            });
            foreach (var l in withDate)
                _contentContainer.Children.Add(
                    BuildLookoutCard(l));
        }

        if (noDate.Count > 0)
        {
            _contentContainer.Children.Add(new Label
            {
                Text = " No Date",
                FontSize = 15,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#555"),
                Margin = new Thickness(0, 8, 0, 2)
            });
            foreach (var l in noDate)
                _contentContainer.Children.Add(
                    BuildLookoutCard(l));
        }
    }

    private View BuildLookoutCard(LookoutScenario lookout)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(70))
            },
            ColumnSpacing = 8,
            Padding = new Thickness(12, 10),
            BackgroundColor = Colors.White
        };

        var infoStack = new VerticalStackLayout { Spacing = 2 };
        infoStack.Children.Add(new Label
        {
            Text = lookout.Title,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.TailTruncation
        });

        string dateStr = lookout.DisplayDate.HasValue
            ? lookout.DisplayDate.Value.ToString("dd MMM yyyy")
            : "No date";
        infoStack.Children.Add(new Label
        {
            Text = dateStr,
            FontSize = 11,
            TextColor = Color.FromArgb("#6A1B9A")
        });

        if (!string.IsNullOrWhiteSpace(lookout.Notes))
            infoStack.Children.Add(new Label
            {
                Text = lookout.Notes,
                FontSize = 11,
                TextColor = Color.FromArgb("#777"),
                LineBreakMode = LineBreakMode.TailTruncation
            });

        row.Add(infoStack, 0, 0);

        var openBtn = new Button
        {
            Text = "Open",
            BackgroundColor = Color.FromArgb("#6A1B9A"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 36,
            Padding = new Thickness(8, 0),
            VerticalOptions = LayoutOptions.Center
        };
        openBtn.Clicked += async (_, _) =>
        {
            var page = new LookoutScenarioPage(
                _auth, _lookoutService, lookout);
            await Navigation.PushAsync(page);
            await LoadAsync();
        };
        row.Add(openBtn, 1, 0);

        return new Frame
        {
            Content = row,
            Padding = 0,
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White,
            Margin = new Thickness(0, 1)
        };
    }

    private async Task AddLookoutAsync()
    {
        string? title = await DisplayPromptAsync(
            "New Lookout",
            "Scenario title:",
            "Add", "Cancel",
            placeholder: "Scenario title...");
        if (string.IsNullOrWhiteSpace(title)) return;

        string? notes = await DisplayPromptAsync(
            "Lookout Notes",
            "Optional notes:",
            "Save", "Skip",
            initialValue: "");

        await _lookoutService.CreateAsync(
            _auth.CurrentUsername,
            title.Trim(),
            null, // no date — can be assigned later
            notes ?? "");

        await LoadAsync();
    }
}

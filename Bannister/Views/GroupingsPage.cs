using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for managing custom activity groupings.
/// Shows all groupings with activity counts, allows creating/renaming/deleting,
/// and tapping a grouping opens ActivityGamePage in grouping mode.
/// </summary>
public class GroupingsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ActivityGroupingService _groupingService;
    private VerticalStackLayout _listContainer;

    public GroupingsPage(AuthService auth, ActivityGroupingService groupingService)
    {
        _auth = auth;
        _groupingService = groupingService;

        Title = "Groupings";
        BackgroundColor = Color.FromArgb("#6B73FF");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadGroupingsAsync();
    }

    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 16
        };

        mainStack.Children.Add(new Label
        {
            Text = "📂 Activity Groupings",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        mainStack.Children.Add(new Label
        {
            Text = "Custom views across all games",
            FontSize = 14,
            TextColor = Color.FromArgb("#D0D0FF")
        });

        _listContainer = new VerticalStackLayout { Spacing = 10 };
        mainStack.Children.Add(_listContainer);

        // Add grouping button
        var btnAdd = new Button
        {
            Text = "+ Add Grouping",
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#5B63EE"),
            CornerRadius = 8,
            HeightRequest = 50,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 10, 0, 0)
        };
        btnAdd.Clicked += OnAddGroupingClicked;
        mainStack.Children.Add(btnAdd);

        scrollView.Content = mainStack;
        Content = scrollView;
    }

    private async Task LoadGroupingsAsync()
    {
        _listContainer.Children.Clear();

        var groupings = await _groupingService.GetGroupingsAsync(_auth.CurrentUsername);

        if (groupings.Count == 0)
        {
            _listContainer.Children.Add(new Label
            {
                Text = "No groupings yet.\n\nCreate a grouping, then assign activities to it\nfrom the ⋮ menu on any activity card.",
                FontSize = 15,
                TextColor = Color.FromArgb("#D0D0FF"),
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        foreach (var grouping in groupings)
        {
            _listContainer.Children.Add(BuildGroupingCard(grouping));
        }
    }

    private Frame BuildGroupingCard(ActivityGrouping grouping)
    {
        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Color.FromArgb("#E8EAF6"),
            BorderColor = Colors.Transparent,
            HasShadow = true
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };

        // Left: name + count
        var infoStack = new VerticalStackLayout
        {
            Spacing = 4,
            VerticalOptions = LayoutOptions.Center
        };

        infoStack.Children.Add(new Label
        {
            Text = grouping.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        infoStack.Children.Add(new Label
        {
            Text = $"{grouping.ActivityCount} activities",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        Grid.SetColumn(infoStack, 0);
        grid.Children.Add(infoStack);

        // Right: 3-dot menu
        var btnMenu = new Button
        {
            Text = "⋮",
            FontSize = 22,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#666"),
            WidthRequest = 44,
            HeightRequest = 44,
            Padding = 0,
            VerticalOptions = LayoutOptions.Center
        };
        int capturedId = grouping.Id;
        string capturedName = grouping.Name;
        btnMenu.Clicked += async (s, e) => await ShowGroupingMenuAsync(capturedId, capturedName);
        Grid.SetColumn(btnMenu, 1);
        grid.Children.Add(btnMenu);

        // Tap card to open
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) =>
        {
            await Shell.Current.GoToAsync($"activitygrid?groupingId={capturedId}");
        };
        frame.GestureRecognizers.Add(tap);

        frame.Content = grid;
        return frame;
    }

    private async Task ShowGroupingMenuAsync(int groupingId, string name)
    {
        string action = await DisplayActionSheet(
            name,
            "Cancel",
            null,
            "✏️ Rename",
            "🗑️ Delete");

        if (string.IsNullOrEmpty(action) || action == "Cancel") return;

        if (action == "✏️ Rename")
        {
            string? newName = await DisplayPromptAsync(
                "Rename Grouping",
                "Enter new name:",
                "Rename",
                "Cancel",
                initialValue: name,
                maxLength: 100);

            if (!string.IsNullOrWhiteSpace(newName))
            {
                await _groupingService.RenameGroupingAsync(groupingId, newName);
                await LoadGroupingsAsync();
            }
        }
        else if (action == "🗑️ Delete")
        {
            bool confirm = await DisplayAlert("Delete Grouping",
                $"Delete '{name}'?\n\nThis won't delete the activities themselves, just the grouping.",
                "Delete", "Cancel");

            if (confirm)
            {
                await _groupingService.DeleteGroupingAsync(groupingId);
                await LoadGroupingsAsync();
            }
        }
    }

    private async void OnAddGroupingClicked(object? sender, EventArgs e)
    {
        string? name = await DisplayPromptAsync(
            "New Grouping",
            "Enter a name for the grouping:",
            "Create",
            "Cancel",
            placeholder: "e.g., Morning Routine, Most Important",
            maxLength: 100);

        if (!string.IsNullOrWhiteSpace(name))
        {
            await _groupingService.CreateGroupingAsync(_auth.CurrentUsername, name);
            await LoadGroupingsAsync();
        }
    }
}

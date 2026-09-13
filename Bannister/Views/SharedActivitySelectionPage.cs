using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class SharedActivitySelectionPage : ContentPage
{
    private readonly ActivityService _activityService;
    private readonly GameService _gameService;
    private readonly AuthService _auth;
    private readonly Func<List<(string GameId, string ActivityName)>, Task> _onConfirm;
    private readonly List<GameGroup> _groups = new();
    private VerticalStackLayout _container = null!;

    private class GameGroup
    {
        public string GameId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public List<Activity> Activities { get; set; } = new();
        public HashSet<int> SelectedIndices { get; set; } = new();
    }

    public SharedActivitySelectionPage(ActivityService activityService,
        GameService gameService, AuthService auth,
        Func<List<(string GameId, string ActivityName)>, Task> onConfirm)
    {
        _activityService = activityService;
        _gameService = gameService;
        _auth = auth;
        _onConfirm = onConfirm;
        Title = "Select Activities to Share";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadGroupsAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        stack.Children.Add(new Label
        {
            Text = "Select activities to share with your partner. Nothing selected by default.",
            FontSize = 13, TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        var globalRow = new HorizontalStackLayout { Spacing = 8 };
        var selAllBtn = new Button
        {
            Text = "Select All", BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"), CornerRadius = 6,
            FontSize = 12, HeightRequest = 32, Padding = new Thickness(10, 0)
        };
        selAllBtn.Clicked += (_, _) =>
        {
            foreach (var g in _groups)
                for (int i = 0; i < g.Activities.Count; i++)
                    g.SelectedIndices.Add(i);
            RenderGroups();
        };
        globalRow.Children.Add(selAllBtn);
        var selNoneBtn = new Button
        {
            Text = "Select None", BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"), CornerRadius = 6,
            FontSize = 12, HeightRequest = 32, Padding = new Thickness(10, 0)
        };
        selNoneBtn.Clicked += (_, _) =>
        {
            foreach (var g in _groups) g.SelectedIndices.Clear();
            RenderGroups();
        };
        globalRow.Children.Add(selNoneBtn);
        stack.Children.Add(globalRow);
        _container = new VerticalStackLayout { Spacing = 12 };
        stack.Children.Add(_container);
        var confirmBtn = new Button
        {
            Text = "✅ Confirm Selection", BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White, CornerRadius = 8, FontSize = 14,
            HeightRequest = 44, FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        };
        confirmBtn.Clicked += async (_, _) =>
        {
            var selected = new List<(string, string)>();
            foreach (var g in _groups)
                foreach (var idx in g.SelectedIndices)
                    selected.Add((g.GameId, g.Activities[idx].Name));
            if (selected.Count == 0)
            {
                await DisplayAlert("Nothing selected", "Select at least one activity.", "OK");
                return;
            }
            await Navigation.PopAsync();
            await _onConfirm(selected);
        };
        stack.Children.Add(confirmBtn);
        Content = new ScrollView { Content = stack };
    }

    private async Task LoadGroupsAsync()
    {
        var games = await _gameService.GetGamesAsync(_auth.CurrentUsername);
        _groups.Clear();
        foreach (var game in games)
        {
            var acts = await _activityService.GetActivitiesAsync(
                _auth.CurrentUsername, game.GameId);
            if (acts.Count == 0) continue;
            _groups.Add(new GameGroup
            {
                GameId = game.GameId,
                DisplayName = game.DisplayName,
                Activities = acts.OrderBy(a => a.Name,
                    StringComparer.OrdinalIgnoreCase).ToList()
            });
        }
        RenderGroups();
    }

    private void RenderGroups()
    {
        _container.Children.Clear();
        foreach (var group in _groups)
        {
            var section = new VerticalStackLayout { Spacing = 4 };
            var headerRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto)
                },
                ColumnSpacing = 6
            };
            headerRow.Add(new Label
            {
                Text = group.DisplayName, FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#1565C0"),
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);
            var capturedGroup = group;
            var allBtn = new Button
            {
                Text = "All", BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0"), CornerRadius = 4,
                FontSize = 10, HeightRequest = 24, Padding = new Thickness(6, 0)
            };
            allBtn.Clicked += (_, _) =>
            {
                for (int i = 0; i < capturedGroup.Activities.Count; i++)
                    capturedGroup.SelectedIndices.Add(i);
                RenderGroups();
            };
            headerRow.Add(allBtn, 1, 0);
            var noneBtn = new Button
            {
                Text = "None", BackgroundColor = Color.FromArgb("#ECEFF1"),
                TextColor = Color.FromArgb("#37474F"), CornerRadius = 4,
                FontSize = 10, HeightRequest = 24, Padding = new Thickness(6, 0)
            };
            noneBtn.Clicked += (_, _) =>
            {
                capturedGroup.SelectedIndices.Clear();
                RenderGroups();
            };
            headerRow.Add(noneBtn, 2, 0);
            section.Children.Add(headerRow);
            var actStack = new VerticalStackLayout
            {
                Spacing = 2, Margin = new Thickness(12, 0, 0, 0)
            };
            for (int i = 0; i < group.Activities.Count; i++)
            {
                int capturedI = i;
                var act = group.Activities[i];
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(32)),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 6
                };
                var cb = new CheckBox
                {
                    IsChecked = group.SelectedIndices.Contains(i)
                };
                cb.CheckedChanged += (_, e) =>
                {
                    if (e.Value) capturedGroup.SelectedIndices.Add(capturedI);
                    else capturedGroup.SelectedIndices.Remove(capturedI);
                };
                row.Add(cb, 0, 0);
                row.Add(new Label
                {
                    Text = act.Name, FontSize = 13,
                    TextColor = Color.FromArgb("#222"),
                    VerticalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.TailTruncation
                }, 1, 0);
                row.Add(new Label
                {
                    Text = $"+{act.ExpGain}", FontSize = 11,
                    TextColor = Color.FromArgb("#2E7D32"),
                    VerticalOptions = LayoutOptions.Center
                }, 2, 0);
                actStack.Children.Add(row);
            }
            section.Children.Add(actStack);
            _container.Children.Add(section);
        }
    }
}

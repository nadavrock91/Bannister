using Bannister.Services;

namespace Bannister.Views;

public class ContextMenuOrderPage : ContentPage
{
    private readonly ContextMenuOrderService _orderService;
    private readonly AuthService _auth;
    private List<string> _orderedKeys = new();
    private CollectionView _collectionView = null!;

    public ContextMenuOrderPage(
        ContextMenuOrderService orderService,
        AuthService auth)
    {
        _orderService = orderService;
        _auth = auth;
        Title = "Context Menu Order";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadOrderAsync();
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
            Text = "☰ Context Menu Order",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        stack.Children.Add(new Label
        {
            Text = "Drag items to reorder. " +
#if ANDROID
                   "Long-press then drag to reorder.",
#else
                   "Drag up or down to reorder.",
#endif
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        _collectionView = new CollectionView
        {
            CanReorderItems = true,
            SelectionMode = SelectionMode.None
        };

        _collectionView.ReorderCompleted += OnReorderCompleted;

        _collectionView.ItemTemplate =
            new DataTemplate(() =>
            {
                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(new GridLength(40))
                    },
                    Padding = new Thickness(12, 10),
                    BackgroundColor = Colors.White,
                    Margin = new Thickness(0, 2)
                };

                var label = new Label
                {
                    FontSize = 14,
                    TextColor = Color.FromArgb("#222"),
                    VerticalOptions = LayoutOptions.Center
                };
                label.SetBinding(Label.TextProperty,
                    new Binding("DisplayLabel"));

                var handle = new Label
                {
                    Text = "⠿",
                    FontSize = 20,
                    TextColor = Color.FromArgb("#999"),
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                };

                grid.Add(label, 0, 0);
                grid.Add(handle, 1, 0);

                return new Frame
                {
                    Content = grid,
                    Padding = 0,
                    CornerRadius = 8,
                    HasShadow = false,
                    BorderColor = Color.FromArgb("#E0E0E0"),
                    BackgroundColor = Colors.White,
                    Margin = new Thickness(0, 2)
                };
            });

        stack.Children.Add(_collectionView);

        var saveBtn = new Button
        {
            Text = " Save Order",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        };
        saveBtn.Clicked += async (_, _) => await SaveOrderAsync();
        stack.Children.Add(saveBtn);

        var resetBtn = new Button
        {
            Text = "↺ Reset to Default",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 38,
            Padding = new Thickness(12, 0)
        };
        resetBtn.Clicked += async (_, _) => await ResetToDefaultAsync();
        stack.Children.Add(resetBtn);

        Content = new ScrollView { Content = stack };
    }

    private async Task LoadOrderAsync()
    {
        _orderedKeys = await _orderService.GetOrderedKeysAsync(
            _auth.CurrentUsername);
        RefreshItems();
    }

    private void RefreshItems()
    {
        var items = _orderedKeys.Select(key =>
            new ContextMenuItem
            {
                Key = key,
                DisplayLabel = ContextMenuOrderService
                    .KeyToLabel(key)
            }).ToList();
        _collectionView.ItemsSource = items;
    }

    private void OnReorderCompleted(object? sender, EventArgs e)
    {
        if (_collectionView.ItemsSource is
            IEnumerable<ContextMenuItem> items)
        {
            _orderedKeys = items.Select(i => i.Key).ToList();
        }
    }

    private async Task SaveOrderAsync()
    {
        await _orderService.SaveOrderAsync(
            _auth.CurrentUsername, _orderedKeys);
        await DisplayAlert("Saved",
            "Context menu order saved.", "OK");
    }

    private async Task ResetToDefaultAsync()
    {
        bool confirm = await DisplayAlert(
            "Reset", "Reset to default order?",
            "Reset", "Cancel");
        if (!confirm) return;

        _orderedKeys = ContextMenuOrderService.DefaultItems
            .Select(i => i.Key).ToList();
        await _orderService.SaveOrderAsync(
            _auth.CurrentUsername, _orderedKeys);
        RefreshItems();
    }

    private class ContextMenuItem
    {
        public string Key { get; set; } = "";
        public string DisplayLabel { get; set; } = "";
    }
}

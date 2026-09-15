using Bannister.Services;

namespace Bannister.Views;

public class ContextMenuOrderPage : ContentPage
{
    private readonly ContextMenuOrderService _orderService;
    private readonly AuthService _auth;
    private List<string> _orderedKeys = new();
    private VerticalStackLayout _listContainer = null!;

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
            Text = "Use ▲ ▼ buttons to reorder items.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        _listContainer = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(_listContainer);

        var btnRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var saveBtn = new Button
        {
            Text = " Save Order",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(16, 0)
        };
        saveBtn.Clicked += async (_, _) => await SaveOrderAsync();
        btnRow.Children.Add(saveBtn);

        var resetBtn = new Button
        {
            Text = "↺ Reset",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 44,
            Padding = new Thickness(12, 0)
        };
        resetBtn.Clicked += async (_, _) => await ResetToDefaultAsync();
        btnRow.Children.Add(resetBtn);

        stack.Children.Add(btnRow);

        Content = new ScrollView { Content = stack };
    }

    private async Task LoadOrderAsync()
    {
        _orderedKeys = await _orderService.GetOrderedKeysAsync(
            _auth.CurrentUsername);
        RenderList();
    }

    private void RenderList()
    {
        _listContainer.Children.Clear();

        for (int i = 0; i < _orderedKeys.Count; i++)
        {
            int capturedI = i;
            var key = _orderedKeys[i];
            var label = ContextMenuOrderService.KeyToLabel(key);

            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(36)),
                    new ColumnDefinition(new GridLength(36))
                },
                ColumnSpacing = 4,
                Padding = new Thickness(12, 8),
                BackgroundColor = Colors.White
            };

            row.Add(new Label
            {
                Text = label,
                FontSize = 13,
                TextColor = Color.FromArgb("#222"),
                VerticalOptions = LayoutOptions.Center
            }, 0, 0);

            var upBtn = new Button
            {
                Text = "▲",
                FontSize = 12,
                HeightRequest = 32,
                WidthRequest = 32,
                CornerRadius = 4,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0"),
                IsEnabled = i > 0
            };
            upBtn.Clicked += (_, _) =>
            {
                if (capturedI <= 0) return;
                (_orderedKeys[capturedI - 1], _orderedKeys[capturedI])
                    = (_orderedKeys[capturedI],
                       _orderedKeys[capturedI - 1]);
                RenderList();
            };
            row.Add(upBtn, 1, 0);

            var downBtn = new Button
            {
                Text = "▼",
                FontSize = 12,
                HeightRequest = 32,
                WidthRequest = 32,
                CornerRadius = 4,
                Padding = 0,
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0"),
                IsEnabled = i < _orderedKeys.Count - 1
            };
            downBtn.Clicked += (_, _) =>
            {
                if (capturedI >= _orderedKeys.Count - 1) return;
                (_orderedKeys[capturedI + 1], _orderedKeys[capturedI])
                    = (_orderedKeys[capturedI],
                       _orderedKeys[capturedI + 1]);
                RenderList();
            };
            row.Add(downBtn, 2, 0);

            var frame = new Frame
            {
                Content = row,
                Padding = 0,
                CornerRadius = 8,
                HasShadow = false,
                BorderColor = Color.FromArgb("#E0E0E0"),
                BackgroundColor = Colors.White,
                Margin = new Thickness(0, 2)
            };

            _listContainer.Children.Add(frame);
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
            "Reset", "Reset to default alphabetical order?",
            "Reset", "Cancel");
        if (!confirm) return;
        _orderedKeys = ContextMenuOrderService.DefaultItems
            .Select(i => i.Key).ToList();
        await _orderService.SaveOrderAsync(
            _auth.CurrentUsername, _orderedKeys);
        RenderList();
    }
}

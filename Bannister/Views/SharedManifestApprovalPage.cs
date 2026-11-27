using Bannister.Services;

namespace Bannister.Views;

public class SharedManifestApprovalPage : ContentPage
{
    private readonly string _createdBy;
    private readonly List<SyncService.SharedManifestItem> _items;
    private readonly Func<List<(string GameId, string ActivityName)>, Task> _onApprove;
    private readonly HashSet<int> _selected = new();
    private VerticalStackLayout _container = null!;

    public SharedManifestApprovalPage(string createdBy,
        List<SyncService.SharedManifestItem> items,
        Func<List<(string GameId, string ActivityName)>, Task> onApprove)
    {
        _createdBy = createdBy;
        _items = items;
        _onApprove = onApprove;
        Title = "Approve Shared Activities";
        BackgroundColor = AppColors.Get(ThemeKey.SecondarySurface);
        BuildUI();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 14 };
        stack.Children.Add((new Label
        {
            Text = $" {_createdBy} wants to share activities",
            FontSize = 18, FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = "Select which activities you want to sync. Only approved activities will be pulled.",
            FontSize = 13,             LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        var globalRow = new HorizontalStackLayout { Spacing = 8 };
        var selAllBtn = (new Button
        {
            Text = "Select All", CornerRadius = 6,
            FontSize = 12, HeightRequest = 32, Padding = new Thickness(10, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        selAllBtn.Clicked += (_, _) =>
        {
            for (int i = 0; i < _items.Count; i++) _selected.Add(i);
            RenderList();
        };
        globalRow.Children.Add(selAllBtn);
        var selNoneBtn = (new Button
        {
            Text = "Select None", CornerRadius = 6,
            FontSize = 12, HeightRequest = 32, Padding = new Thickness(10, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        selNoneBtn.Clicked += (_, _) =>
        {
            _selected.Clear();
            RenderList();
        };
        globalRow.Children.Add(selNoneBtn);
        stack.Children.Add(globalRow);

        _container = new VerticalStackLayout { Spacing = 4 };
        stack.Children.Add(_container);
        var approveBtn = (new Button
        {
            Text = "✅ Approve Selected", CornerRadius = 8, FontSize = 14,
            HeightRequest = 44, FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 8, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        approveBtn.Clicked += async (_, _) =>
        {
            var approved = _selected
                .OrderBy(i => i)
                .Select(i => (_items[i].GameId,
                    _items[i].ActivityName))
                .ToList();
            await Navigation.PopAsync();
            await _onApprove(approved);
        };
        stack.Children.Add(approveBtn);
        Content = new ScrollView { Content = stack };

        for (int i = 0; i < _items.Count; i++) _selected.Add(i);
        RenderList();
    }

    private void RenderList()
    {
        _container.Children.Clear();
        var byGame = _items.Select((item, idx) => (item, idx))
            .GroupBy(x => x.item.GameName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var gameGroup in byGame)
        {
            _container.Children.Add((new Label
            {
                Text = gameGroup.Key, FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 8, 0, 2)
            }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1, classifyPageText: true));
            foreach (var (item, idx) in gameGroup)
            {
                int capturedIdx = idx;
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(new GridLength(32)),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    ColumnSpacing = 6,
                    Margin = new Thickness(12, 0, 0, 0)
                };
                var cb = new CheckBox { IsChecked = _selected.Contains(idx) };
                cb.CheckedChanged += (_, e) =>
                {
                    if (e.Value) _selected.Add(capturedIdx);
                    else _selected.Remove(capturedIdx);
                };
                row.Add(cb, 0, 0);
                row.Add((new Label
                {
                    Text = item.ActivityName, FontSize = 13,
                    VerticalOptions = LayoutOptions.Center,
                    LineBreakMode = LineBreakMode.TailTruncation
                }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true), 1, 0);
                row.Add((new Label
                {
                    Text = $"+{item.ExpGain}", FontSize = 11,
                    VerticalOptions = LayoutOptions.Center
                }).ThemeColor(Label.TextColorProperty, ThemeKey.SuccessText, ThemeKey.PageText3, classifyPageText: true), 2, 0);
                _container.Children.Add(row);
            }
        }
    }
}

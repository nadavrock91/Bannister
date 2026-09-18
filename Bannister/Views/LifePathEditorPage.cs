using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class LifePathEditorPage : ContentPage
{
    private readonly Game _game;
    private readonly LifePathService _lifePathService;
    private readonly GameService _gameService;
    private readonly AuthService _auth;
    private VerticalStackLayout _blocksContainer = null!;
    private bool _isSaving;

    public LifePathEditorPage(Game game, LifePathService lifePathService,
        GameService gameService, AuthService auth)
    {
        _game = game; _lifePathService = lifePathService;
        _gameService = gameService; _auth = auth;
        Title = $"Edit: {game.DisplayName}";
        BackgroundColor = Color.FromArgb("#0D1117");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 12 };
        stack.Children.Add(new Label { Text = _game.DisplayName,
            FontSize = 20, FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White });
        stack.Children.Add(new Label
        {
            Text = $"Started: {_game.CreatedAt.ToLocalTime():dd MMM yyyy}",
            FontSize = 12, TextColor = Color.FromArgb("#8B949E")
        });
        var endBtn = new Button
        {
            Text = _game.LifePathEndedAt.HasValue ? "Edit Chapter End" : "Set Chapter End",
            BackgroundColor = Color.FromArgb("#21262D"),
            TextColor = Color.FromArgb("#F85149"), CornerRadius = 6,
            HeightRequest = 36
        };
        endBtn.Clicked += async (_, _) => await SetEndDateAsync();
        stack.Children.Add(endBtn);
        stack.Children.Add(new Label { Text = " Focuses & Strategies",
            FontSize = 15, FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E6EDF3") });
        var addBtn = new Button { Text = "+ Add Focus Block",
            BackgroundColor = Color.FromArgb("#1F6FEB"),
            TextColor = Colors.White, CornerRadius = 8, HeightRequest = 40 };
        addBtn.Clicked += async (_, _) => await AddBlockAsync(null, 1);
        stack.Children.Add(addBtn);
        _blocksContainer = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(_blocksContainer);
        Content = new ScrollView { Content = stack };
    }

    private async Task LoadAsync()
    {
        _blocksContainer.Children.Clear();
        var blocks = await _lifePathService.GetBlocksForGameAsync(
            _auth.CurrentUsername, _game.GameId);
        foreach (var block in blocks.Where(b => b.ParentBlockId == null)
            .OrderBy(b => b.SortOrder))
            _blocksContainer.Children.Add(BuildBlockCard(block, blocks, 0));
        if (blocks.Count == 0)
            _blocksContainer.Children.Add(new Label { Text = "No focus blocks yet.",
                TextColor = Color.FromArgb("#8B949E") });
    }

    private View BuildBlockCard(LifePathBlock block,
        List<LifePathBlock> all, int depth)
    {
        var stack = new VerticalStackLayout { Spacing = 4,
            Margin = new Thickness(depth * 16, 0, 0, 0) };
        var inner = new VerticalStackLayout { Spacing = 4 };
        inner.Children.Add(new Label { Text = block.Label, FontSize = 14,
            FontAttributes = FontAttributes.Bold, TextColor = Colors.White });
        inner.Children.Add(new Label { Text = block.EndDate.HasValue
            ? $"{block.StartDate:dd MMM yyyy} → {block.EndDate:dd MMM yyyy}"
            : $"{block.StartDate:dd MMM yyyy} → ongoing",
            FontSize = 11, TextColor = Color.FromArgb("#58A6FF") });
        if (!string.IsNullOrWhiteSpace(block.Reason))
            inner.Children.Add(new Label { Text = block.Reason,
                FontSize = 11, TextColor = Color.FromArgb("#8B949E") });
        var buttons = new HorizontalStackLayout { Spacing = 5 };
        if (depth < 2)
        {
            var sub = new Button { Text = "+ Sub", HeightRequest = 28,
                FontSize = 11, BackgroundColor = Color.FromArgb("#21262D"),
                TextColor = Color.FromArgb("#58A6FF") };
            sub.Clicked += async (_, _) => await AddBlockAsync(block.Id, depth + 1);
            buttons.Children.Add(sub);
        }
        var del = new Button { Text = "✕", HeightRequest = 28, WidthRequest = 36,
            FontSize = 11, BackgroundColor = Color.FromArgb("#21262D"),
            TextColor = Color.FromArgb("#F85149") };
        del.Clicked += async (_, _) => { if (await DisplayAlert("Delete Block",
            $"Delete \"{block.Label}\" and children?", "Delete", "Cancel"))
            { await _lifePathService.DeleteAsync(block.Id); await LoadAsync(); } };
        buttons.Children.Add(del);
        inner.Children.Add(buttons);
        stack.Children.Add(new Frame { Content = inner, Padding = 12,
            CornerRadius = 8, HasShadow = false,
            BackgroundColor = Color.FromArgb("#161B22"),
            BorderColor = Color.FromArgb("#1F6FEB") });
        foreach (var child in all.Where(b => b.ParentBlockId == block.Id)
            .OrderBy(b => b.SortOrder))
            stack.Children.Add(BuildBlockCard(child, all, depth + 1));
        return stack;
    }

    private async Task SetEndDateAsync()
    {
        var value = await DisplayPromptAsync("Chapter End",
            "End date (DD/MM/YYYY), blank to clear:", "Save", "Cancel",
            initialValue: _game.LifePathEndedAt?.ToLocalTime().ToString("dd/MM/yyyy") ?? "");
        if (value == null) return;
        if (string.IsNullOrWhiteSpace(value)) _game.LifePathEndedAt = null;
        else if (DateTime.TryParseExact(value, "dd/MM/yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var date))
            _game.LifePathEndedAt = date;
        else { await DisplayAlert("Invalid Date", "Use DD/MM/YYYY.", "OK"); return; }
        _game.LifePathEndReason = await DisplayPromptAsync("Reason",
            "Optional reason:", "Save", "Skip", initialValue: _game.LifePathEndReason) ?? "";
        await _gameService.UpdateGameAsync(_game);
    }

    private async Task AddBlockAsync(int? parentId, int level)
    {
        string levelName = level == 1 ? "Focus Block" : "Sub-Focus";
        string? label = await DisplayPromptAsync($"New {levelName}",
            "Label for this block:", "Next", "Cancel");
        if (string.IsNullOrWhiteSpace(label)) return;
        string? startStr = await DisplayPromptAsync("Start Date",
            "Start date (DD/MM/YYYY):", "Save", "Cancel",
            initialValue: DateTime.Today.ToString("dd/MM/yyyy"));
        if (startStr == null || !DateTime.TryParseExact(startStr, "dd/MM/yyyy",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var start))
        { await DisplayAlert("Invalid Date", "Use DD/MM/YYYY.", "OK"); return; }
        if (_isSaving) return;
        _isSaving = true;
        try { await _lifePathService.CreateAsync(_auth.CurrentUsername,
            _game.GameId, label.Trim(), start, null, "", parentId, level);
            await LoadAsync(); }
        finally { _isSaving = false; }
    }
}

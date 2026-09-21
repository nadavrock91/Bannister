using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class MotivationSourcesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly MotivationService _service;
    private readonly VerticalStackLayout _content = new() { Spacing = 10 };
    private Entry _titleEntry = null!;
    private Editor _descriptionEditor = null!;
    private bool _showArchived;

    public MotivationSourcesPage(AuthService auth, MotivationService service)
    {
        _auth = auth;
        _service = service;
        Title = "Motivation Sources";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(16, 16, 16, 32),
                Spacing = 12,
                Children = { _content }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        await _service.InitAsync();
        _content.Children.Clear();
        _content.Children.Add(new Label
        {
            Text = "Motivation Sources",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        var toggle = new Button
        {
            Text = _showArchived ? "Hide Archived" : "Show Archived",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 6,
            HeightRequest = 38
        };
        toggle.Clicked += async (_, _) => { _showArchived = !_showArchived; await RefreshAsync(); };
        _content.Children.Add(toggle);

        var sources = _showArchived
            ? await _service.GetAllSourcesAsync(_auth.CurrentUsername)
            : await _service.GetActiveSourcesAsync(_auth.CurrentUsername);
        foreach (var source in sources)
            _content.Children.Add(BuildSourceCard(source));
        _content.Children.Add(BuildAddForm());
    }

    private View BuildSourceCard(MotivationSource source)
    {
        var body = new VerticalStackLayout { Spacing = 6 };
        body.Children.Add(new Label
        {
            Text = source.Title,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        if (!string.IsNullOrWhiteSpace(source.Description))
            body.Children.Add(new Label
            {
                Text = source.Description,
                FontSize = 13,
                TextColor = Color.FromArgb("#777")
            });
        var actions = new HorizontalStackLayout { Spacing = 8 };
        if (source.IsArchived)
        {
            var restore = MakeButton("Restore", "#E8F5E9", "#2E7D32");
            restore.Clicked += async (_, _) =>
            {
                source.IsArchived = false;
                await _service.SaveSourceAsync(source);
                await RefreshAsync();
            };
            actions.Children.Add(restore);
        }
        else
        {
            var archive = MakeButton("Archive", "#FFF3E0", "#E65100");
            archive.Clicked += async (_, _) => { await _service.ArchiveSourceAsync(source.Id); await RefreshAsync(); };
            actions.Children.Add(archive);
        }
        var delete = MakeButton("Delete", "#FFEBEE", "#C62828");
        delete.Clicked += async (_, _) =>
        {
            if (await DisplayAlert("Delete Source", "Delete this motivation source?", "Delete", "Cancel"))
            {
                await _service.DeleteSourceAsync(source.Id);
                await RefreshAsync();
            }
        };
        actions.Children.Add(delete);
        body.Children.Add(actions);
        return new Border
        {
            Content = body,
            Stroke = Color.FromArgb("#DDDDDD"),
            StrokeThickness = 1,
            BackgroundColor = source.IsArchived ? Color.FromArgb("#F2F2F2") : Colors.White,
            Padding = 12
        };
    }

    private View BuildAddForm()
    {
        var form = new VerticalStackLayout { Spacing = 8 };
        form.Children.Add(new Label { Text = "Add Source", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        _titleEntry = new Entry { Placeholder = "Title", BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222") };
        _descriptionEditor = new Editor { Placeholder = "Description (optional)", HeightRequest = 110, AutoSize = EditorAutoSizeOption.Disabled, BackgroundColor = Colors.White, TextColor = Color.FromArgb("#222") };
        form.Children.Add(_titleEntry);
        form.Children.Add(_descriptionEditor);
        var save = MakeButton("Save", "#E8F5E9", "#2E7D32");
        save.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_titleEntry.Text)) return;
            await _service.SaveSourceAsync(new MotivationSource
            {
                Username = _auth.CurrentUsername,
                Title = _titleEntry.Text.Trim(),
                Description = _descriptionEditor.Text?.Trim() ?? "",
                CreatedDate = DateTime.Now
            });
            await RefreshAsync();
        };
        form.Children.Add(save);
        return new Border { Content = form, Stroke = Color.FromArgb("#DDDDDD"), StrokeThickness = 1, BackgroundColor = Color.FromArgb("#FAFAFA"), Padding = 12 };
    }

    private static Button MakeButton(string text, string background, string foreground) => new()
    {
        Text = text, BackgroundColor = Color.FromArgb(background), TextColor = Color.FromArgb(foreground), CornerRadius = 6, HeightRequest = 36, Padding = new Thickness(10, 0), FontSize = 12
    };
}

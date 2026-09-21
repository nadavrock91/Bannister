using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class MotivationNotesPage : ContentPage
{
    private readonly MotivationSource _source;
    private readonly MotivationService _service;
    private readonly AuthService _auth;
    private readonly VerticalStackLayout _content = new() { Spacing = 10 };
    private Editor _noteEditor = null!;

    public MotivationNotesPage(
        MotivationSource source,
        MotivationService service,
        AuthService auth)
    {
        _source = source;
        _service = service;
        _auth = auth;
        Title = source.Title;
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
            Text = _source.Title,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        if (!string.IsNullOrWhiteSpace(_source.Description))
            _content.Children.Add(new Label
            {
                Text = _source.Description,
                FontSize = 13,
                TextColor = Color.FromArgb("#777")
            });

        foreach (var note in await _service.GetNotesAsync(_source.Id))
        {
            var body = new VerticalStackLayout { Spacing = 6 };
            body.Children.Add(new Label
            {
                Text = note.Content,
                FontSize = 14,
                TextColor = Color.FromArgb("#222")
            });
            var footer = new HorizontalStackLayout { Spacing = 8 };
            footer.Children.Add(new Label
            {
                Text = note.CreatedDate.ToString("dd MMM yyyy HH:mm"),
                FontSize = 11,
                TextColor = Color.FromArgb("#888"),
                VerticalOptions = LayoutOptions.Center
            });
            var delete = MakeButton("Delete", "#FFEBEE", "#C62828");
            delete.Clicked += async (_, _) =>
            {
                await _service.DeleteNoteAsync(note.Id);
                await RefreshAsync();
            };
            footer.Children.Add(delete);
            body.Children.Add(footer);
            _content.Children.Add(new Border
            {
                Content = body,
                Stroke = Color.FromArgb("#DDDDDD"),
                StrokeThickness = 1,
                BackgroundColor = Colors.White,
                Padding = 12
            });
        }

        _content.Children.Add(BuildAddForm());
    }

    private View BuildAddForm()
    {
        var form = new VerticalStackLayout { Spacing = 8 };
        form.Children.Add(new Label
        {
            Text = "Add Note",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222")
        });
        _noteEditor = new Editor
        {
            Placeholder = "Write an observation...",
            HeightRequest = 130,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Colors.White,
            TextColor = Color.FromArgb("#222")
        };
        form.Children.Add(_noteEditor);
        var save = MakeButton("Save", "#E8F5E9", "#2E7D32");
        save.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_noteEditor.Text)) return;
            await _service.SaveNoteAsync(new MotivationNote
            {
                Username = _auth.CurrentUsername,
                SourceId = _source.Id,
                Content = _noteEditor.Text.Trim(),
                CreatedDate = DateTime.Now
            });
            await RefreshAsync();
        };
        form.Children.Add(save);
        return new Border
        {
            Content = form,
            Stroke = Color.FromArgb("#DDDDDD"),
            StrokeThickness = 1,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            Padding = 12
        };
    }

    private static Button MakeButton(string text, string background, string foreground) => new()
    {
        Text = text,
        BackgroundColor = Color.FromArgb(background),
        TextColor = Color.FromArgb(foreground),
        CornerRadius = 6,
        HeightRequest = 36,
        Padding = new Thickness(10, 0),
        FontSize = 12
    };
}

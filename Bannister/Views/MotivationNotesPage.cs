using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class MotivationNotesPage : Bannister.Views.ModeThemedPage
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
        _content.Children.Add((new Label
        {
            Text = _source.Title,
            FontSize = 24,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        if (!string.IsNullOrWhiteSpace(_source.Description))
            _content.Children.Add((new Label
            {
                Text = _source.Description,
                FontSize = 13
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        foreach (var note in await _service.GetNotesAsync(_source.Id))
        {
            var body = new VerticalStackLayout { Spacing = 6 };
            body.Children.Add((new Label
            {
                Text = note.Content,
                FontSize = 14
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
            var footer = new HorizontalStackLayout { Spacing = 8 };
            footer.Children.Add((new Label
            {
                Text = note.CreatedDate.ToString("dd MMM yyyy HH:mm"),
                FontSize = 11,
                VerticalOptions = LayoutOptions.Center
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
            var delete = MakeButton("Delete", ThemeKey.ErrorBackground, ThemeKey.ErrorText);
            delete.Clicked += async (_, _) =>
            {
                await _service.DeleteNoteAsync(note.Id);
                await RefreshAsync();
            };
            footer.Children.Add(delete);
            body.Children.Add(footer);
            _content.Children.Add((new Border
            {
                Content = body,
                StrokeThickness = 1,
                Padding = 12
            }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground));
        }

        _content.Children.Add(BuildAddForm());
    }

    private View BuildAddForm()
    {
        var form = new VerticalStackLayout { Spacing = 8 };
        form.Children.Add((new Label
        {
            Text = "Add Note",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        _noteEditor = (new Editor
        {
            Placeholder = "Write an observation...",
            HeightRequest = 130,
            AutoSize = EditorAutoSizeOption.Disabled
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText);
        form.Children.Add(_noteEditor);
        var save = MakeButton("Save", ThemeKey.SuccessBackground, ThemeKey.SuccessText);
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
        return (new Border
        {
            Content = form,
            StrokeThickness = 1,
            Padding = 12
        }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);
    }

    private static Button MakeButton(string text, ThemeKey background, ThemeKey foreground) => (new Button(){
        Text = text,
        CornerRadius = 6,
        HeightRequest = 36,
        Padding = new Thickness(10, 0),
        FontSize = 12
    }).ThemeColor(VisualElement.BackgroundColorProperty, background).ThemeColor(Button.TextColorProperty, foreground);
}

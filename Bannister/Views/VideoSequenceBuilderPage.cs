using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class VideoSequenceBuilderPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DenseVideoSequenceService _service;
    private readonly FFmpegService _ffmpeg;
    private readonly VerticalStackLayout _body = new() { Spacing = 12 };

    public VideoSequenceBuilderPage(AuthService auth, DenseVideoSequenceService service, FFmpegService ffmpeg)
    {
        _auth = auth; _service = service; _ffmpeg = ffmpeg;
        Title = "Sequence Builder";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16, Spacing = 14,
                Children =
                {
                    new Label { Text = "Sequence Builder", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#C2185B") },
                    new Label { Text = "Chain generated clips by keeping usable transition runs and continuing from their endpoint.", FontSize = 13, TextColor = Color.FromArgb("#666") },
                    _body
                }
            }
        };
    }

    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }

    private async Task RefreshAsync()
    {
        _body.Children.Clear();
        _body.Children.Add(BuildCreateCard());
        var sequences = await _service.GetSequencesAsync(_auth.CurrentUsername);
        if (sequences.Count == 0)
            _body.Children.Add(new Label { Text = "No sequences yet.", TextColor = Color.FromArgb("#777") });
        foreach (var sequence in sequences)
        {
            var generations = await _service.GetGenerationsAsync(sequence.Id);
            var button = new Button
            {
                Text = $"{(sequence.IsDone ? "✓ " : "")}{sequence.Name}\n{sequence.SequenceType} • {generations.Count} attempts • {generations.Sum(x => x.UsableSeconds):0.00}s usable",
                BackgroundColor = Colors.White, TextColor = Color.FromArgb("#333"),
                BorderColor = Color.FromArgb("#C2185B"), BorderWidth = 1,
                CornerRadius = 10, HeightRequest = 68, HorizontalOptions = LayoutOptions.Fill
            };
            button.Clicked += async (_, _) => await Navigation.PushAsync(
                new VideoSequenceWorkspacePage(sequence, _service, _ffmpeg));
            _body.Children.Add(button);
        }
    }

    private View BuildCreateCard()
    {
        var name = new Entry { Placeholder = "Sequence name" };
        var type = new Picker { Title = "Sequence tag", ItemsSource = new[] { "short clip", "dense action scene" }, SelectedIndex = 0 };
        var story = new Entry { Placeholder = "Story name (optional)" };
        var create = new Button { Text = "Create sequence", BackgroundColor = Color.FromArgb("#C2185B"), TextColor = Colors.White };
        create.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(name.Text)) return;
            await _service.SaveSequenceAsync(new DenseVideoSequence
            {
                Username = _auth.CurrentUsername, Name = name.Text.Trim(),
                SequenceType = type.SelectedItem?.ToString() ?? "short clip",
                StoryName = story.Text?.Trim() ?? ""
            });
            await RefreshAsync();
        };
        return new Frame
        {
            BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#DDDDDD"),
            CornerRadius = 10, Padding = 12, HasShadow = false,
            Content = new VerticalStackLayout { Spacing = 8, Children = { name, type, story, create } }
        };
    }
}

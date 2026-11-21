using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Layouts;

namespace Bannister.Views;

public class DenseSequenceExperimentPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DenseVideoSequenceService _service;
    private readonly FFmpegService _ffmpeg;
    private readonly VerticalStackLayout _body = new() { Spacing = 12 };
    private DenseSequenceExperiment? _selectedExperiment;
    private int _selectedGridIndex;

    public DenseSequenceExperimentPage(AuthService auth, DenseVideoSequenceService service, FFmpegService ffmpeg)
    {
        _auth = auth; _service = service; _ffmpeg = ffmpeg;
        Title = "Dense Sequence Experiment";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16, Spacing = 14,
                Children =
                {
                    new Label { Text = "Dense Sequence Experiment", FontSize = 24, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#6A1B9A") },
                    new Label { Text = "20 concept grids × 20 images. Select exactly five starts and rotate through their shared sequence workflow.", FontSize = 13, TextColor = Color.FromArgb("#666") },
                    _body
                }
            }
        };
    }

    protected override async void OnAppearing() { base.OnAppearing(); await RefreshAsync(); }

    private async Task RefreshAsync()
    {
        _body.Children.Clear();
        if (_selectedExperiment == null)
        {
            BuildExperimentList(await _service.GetExperimentsAsync(_auth.CurrentUsername));
            return;
        }
        await BuildExperimentAsync(_selectedExperiment);
    }

    private void BuildExperimentList(List<DenseSequenceExperiment> experiments)
    {
        var title = new Entry { Placeholder = "Experiment title" };
        var add = Action("Create experiment", "#6A1B9A");
        add.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(title.Text)) return;
            var experiment = new DenseSequenceExperiment { Username = _auth.CurrentUsername, Title = title.Text.Trim() };
            await _service.SaveExperimentAsync(experiment);
            _selectedExperiment = experiment;
            await RefreshAsync();
        };
        _body.Children.Add(Card(new VerticalStackLayout { Spacing = 8, Children = { title, add } }));
        foreach (var experiment in experiments)
        {
            var open = Action(experiment.Title, "#455A64");
            open.Clicked += async (_, _) => { _selectedExperiment = experiment; _selectedGridIndex = 0; await RefreshAsync(); };
            _body.Children.Add(open);
        }
    }

    private async Task BuildExperimentAsync(DenseSequenceExperiment experiment)
    {
        var back = Action("← Experiments", "#546E7A");
        back.Clicked += async (_, _) => { _selectedExperiment = null; await RefreshAsync(); };
        _body.Children.Add(back);
        _body.Children.Add(new Label { Text = experiment.Title, FontSize = 21, FontAttributes = FontAttributes.Bold });

        var grids = await _service.GetGridsAsync(experiment.Id);
        var images = await _service.GetGridImagesAsync(experiment.Id);
        var selected = images.Where(x => x.IsSelectedStart).ToList();
        var sequences = await _service.GetSequencesAsync(experiment.Username, experiment.Id);

        await BuildSummaryAsync(sequences);
        BuildSequenceRotation(sequences);

        var gridPicker = new Picker
        {
            Title = "Concept grid", ItemsSource = grids,
            ItemDisplayBinding = new Binding(nameof(DenseConceptGrid.Title)),
            SelectedIndex = Math.Clamp(_selectedGridIndex, 0, Math.Max(0, grids.Count - 1))
        };
        gridPicker.SelectedIndexChanged += async (_, _) =>
        {
            _selectedGridIndex = Math.Max(0, gridPicker.SelectedIndex);
            await RefreshAsync();
        };
        _body.Children.Add(gridPicker);
        if (grids.Count == 0) return;
        var grid = grids[_selectedGridIndex];
        var gridImages = images.Where(x => x.GridId == grid.Id).ToList();
        _body.Children.Add(new Label
        {
            Text = $"{grid.Title}: {gridImages.Count}/20 images  •  selected starts {selected.Count}/5",
            FontAttributes = FontAttributes.Bold, TextColor = selected.Count == 5 ? Color.FromArgb("#2E7D32") : Color.FromArgb("#E65100")
        });
        var addImages = Action("Add images to this grid", "#1565C0");
        addImages.IsEnabled = gridImages.Count < 20;
        addImages.Clicked += async (_, _) =>
        {
            var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
            {
                PickerTitle = "Add concept images", FileTypes = FilePickerFileType.Images
            });
            foreach (var file in (files ?? Array.Empty<FileResult>()).Take(20 - gridImages.Count))
            {
                string path = await _service.ImportFileAsync(file, "concepts");
                await _service.SaveGridImageAsync(new DenseConceptGridImage
                {
                    Username = experiment.Username, ExperimentId = experiment.Id,
                    GridId = grid.Id, ImagePath = path
                });
            }
            await RefreshAsync();
        };
        _body.Children.Add(addImages);

        var imageWrap = new FlexLayout { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, JustifyContent = FlexJustify.Start };
        foreach (var image in gridImages)
            imageWrap.Children.Add(BuildImageTile(image, selected.Count));
        _body.Children.Add(imageWrap);

        var createSequences = Action("Create / update the 5 starting sequences", "#2E7D32");
        createSequences.IsEnabled = selected.Count == 5;
        createSequences.Clicked += async (_, _) =>
        {
            await _service.EnsureSelectedSequencesAsync(experiment, selected);
            await RefreshAsync();
        };
        _body.Children.Add(createSequences);
    }

    private View BuildImageTile(DenseConceptGridImage image, int selectedCount)
    {
        var check = new CheckBox { IsChecked = image.IsSelectedStart, Color = Color.FromArgb("#6A1B9A") };
        check.CheckedChanged += async (_, e) =>
        {
            if (e.Value && selectedCount >= 5)
            {
                check.IsChecked = false;
                await DisplayAlert("Five selected", "Exactly five starting thumbnails are allowed.", "OK");
                return;
            }
            image.IsSelectedStart = e.Value;
            await _service.SaveGridImageAsync(image);
            await RefreshAsync();
        };
        string path = _service.ResolvePath(image.ImagePath);
        return new Frame
        {
            WidthRequest = 135, HeightRequest = 150, Margin = 4, Padding = 4,
            HasShadow = false, BorderColor = image.IsSelectedStart ? Color.FromArgb("#6A1B9A") : Color.FromArgb("#DDDDDD"),
            Content = new Grid
            {
                RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
                Children =
                {
                    new Image { Source = File.Exists(path) ? ImageSource.FromFile(path) : null, Aspect = Aspect.AspectFit },
                    check
                }
            }
        };
    }

    private void BuildSequenceRotation(List<DenseVideoSequence> sequences)
    {
        if (sequences.Count == 0) return;
        _body.Children.Add(new Label { Text = "Starting thumbnail sequences", FontSize = 18, FontAttributes = FontAttributes.Bold });
        var row = new FlexLayout { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap };
        foreach (var sequence in sequences)
        {
            var open = Action($"{(sequence.IsDone ? "✓ " : "")}{sequence.Name}", sequence.IsDone ? "#757575" : "#6A1B9A");
            open.Clicked += async (_, _) => await Navigation.PushAsync(new VideoSequenceWorkspacePage(sequence, _service, _ffmpeg));
            row.Children.Add(open);
        }
        _body.Children.Add(row);
        var next = Action("Next sequence", "#C2185B");
        next.Clicked += async (_, _) =>
        {
            DenseVideoSequence? target = null;
            double best = double.MaxValue;
            foreach (var sequence in sequences.Where(x => !x.IsDone))
            {
                double usable = (await _service.GetGenerationsAsync(sequence.Id)).Sum(x => x.UsableSeconds);
                if (usable < best) { best = usable; target = sequence; }
            }
            if (target != null)
                await Navigation.PushAsync(new VideoSequenceWorkspacePage(target, _service, _ffmpeg));
        };
        next.IsEnabled = sequences.Any(x => !x.IsDone);
        _body.Children.Add(next);
    }

    private async Task BuildSummaryAsync(List<DenseVideoSequence> sequences)
    {
        int attempts = 0, usableCount = 0;
        double usableSeconds = 0;
        var summary = new VerticalStackLayout { Spacing = 4 };
        foreach (var sequence in sequences)
        {
            var rows = await _service.GetGenerationsAsync(sequence.Id);
            int failed = rows.Count(x => x.Status == "failed");
            double seconds = rows.Sum(x => x.UsableSeconds);
            attempts += rows.Count; usableCount += rows.Count(x => x.UsableSeconds > 0); usableSeconds += seconds;
            summary.Children.Add(new Label
            {
                Text = $"{sequence.Name}: {rows.Count} attempts • {failed} failed • {seconds:0.00}s • last {(rows.LastOrDefault()?.Status ?? "none")}",
                FontSize = 12
            });
        }
        summary.Children.Insert(0, new Label
        {
            Text = $"Overall: {attempts} attempts • {usableSeconds:0.00}s usable • {(attempts == 0 ? 0 : 100.0 * usableCount / attempts):0.#}% success",
            FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#6A1B9A")
        });
        _body.Children.Add(Card(summary));
    }

    private static Button Action(string text, string color) => new() { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 12, HeightRequest = 42, Margin = 2 };
    private static Frame Card(View content) => new() { Content = content, BackgroundColor = Colors.White, BorderColor = Color.FromArgb("#DDDDDD"), CornerRadius = 10, Padding = 12, HasShadow = false };
}

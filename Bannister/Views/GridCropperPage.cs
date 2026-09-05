using SkiaSharp;
using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class GridCropperPage : ContentPage
{
    private string? _sourceFilePath;
    private SKBitmap? _sourceBitmap;
    private int _cellW = 100;
    private int _cellH = 100;
    private Button _pickButton = null!;
    private Label _imageInfoLabel = null!;
    private Slider _widthSlider = null!;
    private Slider _heightSlider = null!;
    private Label _widthValueLabel = null!;
    private Label _heightValueLabel = null!;
    private Image _previewImage = null!;
    private Label _previewLabel = null!;
    private Button _cropButton = null!;
    private Label _statusLabel = null!;
    private Picker _presetPicker = null!;
    private List<CropPresetItem> _presets = new();
    private readonly CropPresetService _presetService;
    private readonly string _username;

    public GridCropperPage(AuthService auth, CropPresetService presetService)
    {
        _username = auth.CurrentUsername;
        _presetService = presetService;
        Title = "Grid Cropper";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPresetsAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 16 };
        stack.Children.Add(new Label { Text = " Grid Cropper", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        stack.Children.Add(new Label { Text = "Pick a 4×5 grid image. Set cell size with the sliders. The top-left panel previews below. Tap Crop to save 20 copies.", FontSize = 14, TextColor = Color.FromArgb("#666"), LineBreakMode = LineBreakMode.WordWrap });
        stack.Children.Add(BuildSectionCard("Step 1 — Pick the grid image", BuildStep1Content()));
        stack.Children.Add(BuildSectionCard("Step 2 — Set cell size", BuildStep2Content()));
        stack.Children.Add(BuildSectionCard("Step 3 — Preview & Crop", BuildStep3Content()));
        _statusLabel = new Label { Text = "", FontSize = 13, TextColor = Color.FromArgb("#2E7D32"), LineBreakMode = LineBreakMode.WordWrap, IsVisible = false };
        stack.Children.Add(_statusLabel);
        Content = new ScrollView { Content = stack };
    }

    private static Frame BuildSectionCard(string title, View content)
    {
        var inner = new VerticalStackLayout { Spacing = 10 };
        inner.Children.Add(new Label { Text = title, FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#1565C0") });
        inner.Children.Add(content);
        return new Frame { BackgroundColor = Colors.White, Padding = 16, CornerRadius = 12, HasShadow = true, Content = inner };
    }

    private View BuildStep1Content()
    {
        var v = new VerticalStackLayout { Spacing = 8 };
        _pickButton = new Button { Text = " Choose Image File", BackgroundColor = Color.FromArgb("#1565C0"), TextColor = Colors.White, CornerRadius = 8, FontSize = 14, HeightRequest = 44 };
        _pickButton.Clicked += async (_, _) => await PickImageAsync();
        v.Children.Add(_pickButton);
        _imageInfoLabel = new Label { Text = "No image selected.", FontSize = 12, TextColor = Color.FromArgb("#666") };
        v.Children.Add(_imageInfoLabel);
        return v;
    }

    private View BuildStep2Content()
    {
        var v = new VerticalStackLayout { Spacing = 10 };
        var wRow = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(60)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(60)) }, ColumnSpacing = 8 };
        wRow.Add(new Label { Text = "Width", FontSize = 13, TextColor = Color.FromArgb("#444"), VerticalOptions = LayoutOptions.Center }, 0, 0);
        _widthSlider = new Slider { Minimum = 10, Maximum = 1000, Value = 100, IsEnabled = false };
        _widthSlider.ValueChanged += OnSliderChanged;
        wRow.Add(_widthSlider, 1, 0);
        _widthValueLabel = new Label { Text = "100 px", FontSize = 12, TextColor = Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.End };
        wRow.Add(_widthValueLabel, 2, 0);
        v.Children.Add(wRow);
        var hRow = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(60)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(60)) }, ColumnSpacing = 8 };
        hRow.Add(new Label { Text = "Height", FontSize = 13, TextColor = Color.FromArgb("#444"), VerticalOptions = LayoutOptions.Center }, 0, 0);
        _heightSlider = new Slider { Minimum = 10, Maximum = 1000, Value = 100, IsEnabled = false };
        _heightSlider.ValueChanged += OnSliderChanged;
        hRow.Add(_heightSlider, 1, 0);
        _heightValueLabel = new Label { Text = "100 px", FontSize = 12, TextColor = Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.End };
        hRow.Add(_heightValueLabel, 2, 0);
        v.Children.Add(hRow);

        // Preset row
        var presetRow = new HorizontalStackLayout { Spacing = 8 };

        _presetPicker = new Picker
        {
            Title = "No presets saved",
            HorizontalOptions = LayoutOptions.FillAndExpand,
            BackgroundColor = Colors.White
        };
        _presetPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_presetPicker.SelectedIndex < 0 ||
                _presetPicker.SelectedIndex >= _presets.Count) return;
            var p = _presets[_presetPicker.SelectedIndex];
            // Apply preset values to sliders (clamp to current max)
            _widthSlider.Value = Math.Min(p.W, _widthSlider.Maximum);
            _heightSlider.Value = Math.Min(p.H, _heightSlider.Maximum);
            // If image not yet picked, just store the values for later
            _cellW = p.W;
            _cellH = p.H;
            _widthValueLabel.Text = $"{_cellW} px";
            _heightValueLabel.Text = $"{_cellH} px";
        };
        presetRow.Children.Add(_presetPicker);

        var savePresetBtn = new Button
        {
            Text = " Save Preset",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 38,
            Padding = new Thickness(10, 0)
        };
        savePresetBtn.Clicked += async (_, _) =>
        {
            string? name = await DisplayPromptAsync(
                "Save Preset",
                $"Name for {_cellW}×{_cellH}:",
                "Save", "Cancel",
                placeholder: "e.g. Instagram Square");
            if (string.IsNullOrWhiteSpace(name)) return;

            await _presetService.UpsertPresetAsync(_username, name.Trim(), _cellW, _cellH);
            await LoadPresetsAsync();
        };
        presetRow.Children.Add(savePresetBtn);

        var deletePresetBtn = new Button
        {
            Text = "✕",
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 38,
            WidthRequest = 38,
            Padding = 0
        };
        deletePresetBtn.Clicked += async (_, _) =>
        {
            if (_presetPicker.SelectedIndex < 0 ||
                _presetPicker.SelectedIndex >= _presets.Count)
            {
                await DisplayAlert("No preset selected",
                    "Select a preset from the list first.", "OK");
                return;
            }
            var p = _presets[_presetPicker.SelectedIndex];
            bool confirm = await DisplayAlert("Delete Preset",
                $"Delete '{p.Name}'?", "Delete", "Cancel");
            if (!confirm) return;
            await _presetService.DeletePresetAsync(p.Id);
            await LoadPresetsAsync();
        };
        presetRow.Children.Add(deletePresetBtn);

        v.Children.Add(presetRow);
        v.Children.Add(new Label { Text = "Sliders activate after an image is picked.", FontSize = 11, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic });
        return v;
    }

    private View BuildStep3Content()
    {
        var v = new VerticalStackLayout { Spacing = 10 };
        _previewLabel = new Label { Text = "Pick an image and adjust sliders to see the top-left panel preview.", FontSize = 12, TextColor = Color.FromArgb("#666") };
        v.Children.Add(_previewLabel);
        _previewImage = new Image { HeightRequest = 240, Aspect = Aspect.AspectFit, IsVisible = false, HorizontalOptions = LayoutOptions.Center };
        v.Children.Add(_previewImage);
        _cropButton = new Button { Text = "✂️ Crop & Save 20 Panels", BackgroundColor = Color.FromArgb("#2E7D32"), TextColor = Colors.White, CornerRadius = 8, FontSize = 14, HeightRequest = 44, FontAttributes = FontAttributes.Bold, IsVisible = false };
        _cropButton.Clicked += async (_, _) => await CropAndSaveAsync();
        v.Children.Add(_cropButton);
        return v;
    }

    private async Task PickImageAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions { PickerTitle = "Select grid image", FileTypes = FilePickerFileType.Images });
            if (result == null) return;
            _sourceFilePath = result.FullPath;
            await using var stream = File.OpenRead(_sourceFilePath);
            _sourceBitmap?.Dispose();
            _sourceBitmap = SKBitmap.Decode(stream);
            if (_sourceBitmap == null) { await DisplayAlert("Error", "Could not decode the selected image.", "OK"); return; }
            int imgW = _sourceBitmap.Width;
            int imgH = _sourceBitmap.Height;
            _imageInfoLabel.Text = $"{System.IO.Path.GetFileName(_sourceFilePath)} — {imgW} × {imgH} px";
            _widthSlider.Maximum = imgW;
            _widthSlider.Value = Math.Min(_cellW, imgW);
            _heightSlider.Maximum = imgH;
            _heightSlider.Value = Math.Min(_cellH, imgH);
            _widthSlider.IsEnabled = true;
            _heightSlider.IsEnabled = true;
            UpdatePreview();
        }
        catch (Exception ex) { await DisplayAlert("Error", $"Failed to open image: {ex.Message}", "OK"); }
    }

    private void OnSliderChanged(object? sender, ValueChangedEventArgs e)
    {
        _cellW = (int)Math.Round(_widthSlider.Value);
        _cellH = (int)Math.Round(_heightSlider.Value);
        _widthValueLabel.Text = $"{_cellW} px";
        _heightValueLabel.Text = $"{_cellH} px";
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_sourceBitmap == null) return;
        int safeW = Math.Max(1, Math.Min(_cellW, _sourceBitmap.Width));
        int safeH = Math.Max(1, Math.Min(_cellH, _sourceBitmap.Height));
        var srcRect = new SKRectI(0, 0, safeW, safeH);
        var cropped = new SKBitmap(safeW, safeH);
        using var canvas = new SKCanvas(cropped);
        canvas.DrawBitmap(_sourceBitmap, srcRect, new SKRect(0, 0, safeW, safeH));
        using var image = SKImage.FromBitmap(cropped);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = data.ToArray();
        cropped.Dispose();
        _previewImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
        _previewImage.IsVisible = true;
        _previewLabel.Text = $"Preview: top-left panel at {safeW} × {safeH} px";
        _cropButton.IsVisible = true;
        _statusLabel.IsVisible = false;
    }

    private async Task CropAndSaveAsync()
    {
        if (_sourceBitmap == null || _sourceFilePath == null) return;
        int safeW = Math.Max(1, Math.Min(_cellW, _sourceBitmap.Width));
        int safeH = Math.Max(1, Math.Min(_cellH, _sourceBitmap.Height));
        var sourceDir = System.IO.Path.GetDirectoryName(_sourceFilePath) ?? ".";
        var outputDir = System.IO.Path.Combine(sourceDir, "cropped_panels");
        Directory.CreateDirectory(outputDir);
        _cropButton.IsEnabled = false;
        _cropButton.Text = "Cropping…";
        try
        {
            await Task.Run(() =>
            {
                // Walk 4 columns x 5 rows = 20 panels
                const int cols = 4;
                const int rows = 5;
                int panel = 1;

                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        int x = col * safeW;
                        int y = row * safeH;

                        // Clamp to image bounds
                        int actualW = Math.Min(safeW, _sourceBitmap.Width - x);
                        int actualH = Math.Min(safeH, _sourceBitmap.Height - y);
                        if (actualW <= 0 || actualH <= 0) break;

                        var srcRect = new SKRectI(x, y, x + actualW, y + actualH);
                        using var cropped = new SKBitmap(safeW, safeH);
                        using var cropCanvas = new SKCanvas(cropped);
                        cropCanvas.Clear(SKColors.Black);
                        cropCanvas.DrawBitmap(_sourceBitmap, srcRect,
                            new SKRect(0, 0, actualW, actualH));

                        using var image = SKImage.FromBitmap(cropped);
                        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                        var bytes = encoded.ToArray();

                        var fileName = $"panel_{panel:D2}.png";
                        File.WriteAllBytes(
                            System.IO.Path.Combine(outputDir, fileName), bytes);
                        panel++;
                    }
                }
            });
            _statusLabel.Text = $"✓ 20 panels saved to:\n{outputDir}";
            _statusLabel.TextColor = Color.FromArgb("#2E7D32");
            _statusLabel.IsVisible = true;
            await DisplayAlert("Done", $"20 panels saved to:\n{outputDir}", "OK");
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Error: {ex.Message}";
            _statusLabel.TextColor = Color.FromArgb("#C62828");
            _statusLabel.IsVisible = true;
            await DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            _cropButton.IsEnabled = true;
            _cropButton.Text = "✂️ Crop & Save 20 Panels";
        }
    }

    private async Task LoadPresetsAsync()
    {
        _presets = await _presetService.GetPresetsAsync(_username);
        RefreshPresetPicker();
    }

    private void RefreshPresetPicker()
    {
        _presetPicker.Items.Clear();
        foreach (var p in _presets)
            _presetPicker.Items.Add($"{p.Name} ({p.W}×{p.H})");
        _presetPicker.Title = _presets.Count == 0
            ? "No presets saved"
            : "Load a preset…";
        _presetPicker.SelectedIndex = -1;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _sourceBitmap?.Dispose();
        _sourceBitmap = null;
    }

}

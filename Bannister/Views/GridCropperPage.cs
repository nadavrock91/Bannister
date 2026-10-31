using SkiaSharp;
using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class GridCropperPage : ContentPage
{
    private string? _sourceFilePath;
    private SKBitmap? _sourceBitmap;
    private int _imagePixelW;
    private int _imagePixelH;
    private byte[]? _gridImageBytes;
    private int _cellW = 100;
    private int _cellH = 100;
    private int _rows = 5;
    private bool _separateBottomRow = false;
    private int _bottomCols = 4;
    private int _bottomCellH = 100;
    private int _bottomCellW = 100;
    private Slider _bottomWidthSlider = null!;
    private Label _bottomWidthValueLabel = null!;
    private Image _bottomPreviewImage = null!;
    private Label _bottomPreviewLabel = null!;
    private Button _pickButton = null!;
    private Label _imageInfoLabel = null!;
    private Slider _widthSlider = null!;
    private Slider _heightSlider = null!;
    private Label _widthValueLabel = null!;
    private Label _heightValueLabel = null!;
    private Image _previewImage = null!;
    private Image _gridPreviewImage = null!;
    private Label _gridPreviewLabel = null!;
    private Label _cropPreviewHint = null!;
    private Label _gridPreviewHint = null!;
    private Label _gridHeaderLabel = null!;
    private Label _previewLabel = null!;
    private Button _cropButton = null!;
    private Button _separateBottomBtn = null!;
    private VerticalStackLayout _bottomStripPanel = null!;
    private Slider _bottomHeightSlider = null!;
    private Label _bottomHeightValueLabel = null!;
    private Picker _bottomColsPicker = null!;
    private Label _statusLabel = null!;
    private Picker _presetPicker = null!;
    private List<CropPresetItem> _presets = new();
    private readonly CropPresetService _presetService;
    private readonly string _username;
    private readonly IPanelSaver _panelSaver;
    private readonly HookPrefixService _hookPrefixes;
    private HookCropperSettings? _prefixSettings;
    private Switch _requirePrefixSwitch = null!;
    private bool _loadingPrefixSettings;
    private int _cols = 4;
    private bool[,] _selected = new bool[5, 4];
    private Button[,] _selectionButtons = new Button[5, 4];
    private Grid _selectionGrid = null!;
    private Label _descLabel = null!;
    private Picker _colsPicker = null!;
    private Picker _rowsPicker = null!;
    private Label _selectionHeader = null!;
    private HorizontalStackLayout _selectAllRow = null!;
    private Label _selectionHintLabel = null!;

    public GridCropperPage(
        AuthService auth,
        CropPresetService presetService,
        IPanelSaver panelSaver,
        HookPrefixService hookPrefixes)
    {
        _username = auth.CurrentUsername;
        _presetService = presetService;
        _panelSaver = panelSaver;
        _hookPrefixes = hookPrefixes;
        Title = "Grid Cropper";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPresetsAsync();
        await LoadPrefixSettingsAsync();
    }

    private void BuildUI()
    {
        var stack = new VerticalStackLayout { Padding = 20, Spacing = 16 };
        stack.Children.Add(BuildSectionCard("Prefix Tracking", BuildPrefixSettingsContent()));
        stack.Children.Add(new Label { Text = " Grid Cropper", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#222") });
        _descLabel = new Label
        {
            Text = "Pick a 4×5 grid image. Set cell size with the sliders. The top-left panel previews below. Tap Crop to save selected panels.",
            FontSize = 14,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        stack.Children.Add(_descLabel);
        stack.Children.Add(BuildGridSizeRow());
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

    private View BuildGridSizeRow()
    {
        _colsPicker = new Picker
        {
            WidthRequest = 70
        };
        foreach (var value in new[] { "2", "3", "4", "5", "6", "7", "8" })
            _colsPicker.Items.Add(value);
        _colsPicker.SelectedIndex = 2;

        _rowsPicker = new Picker
        {
            WidthRequest = 70
        };
        foreach (var value in new[] { "2", "3", "4", "5", "6", "7", "8", "9", "10" })
            _rowsPicker.Items.Add(value);
        _rowsPicker.SelectedIndex = 3;

        _colsPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_colsPicker.SelectedItem is not string s) return;
            if (!int.TryParse(s, out int v)) return;
            _cols = v;
            RebuildSelectionGrid();
            if (_sourceBitmap != null) UpdatePreview();
            _descLabel.Text = $"Pick a {_cols}×{_rows} grid image. Set cell size with the sliders. The top-left panel previews below. Tap Crop to save selected panels.";
        };
        _rowsPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_rowsPicker.SelectedItem is not string s) return;
            if (!int.TryParse(s, out int v)) return;
            _rows = v;
            RebuildSelectionGrid();
            if (_sourceBitmap != null) UpdatePreview();
            _descLabel.Text = $"Pick a {_cols}×{_rows} grid image. Set cell size with the sliders. The top-left panel previews below. Tap Crop to save selected panels.";
        };

        return new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                new Label { Text = "Cols:", VerticalOptions = LayoutOptions.Center },
                _colsPicker,
                new Label { Text = "Rows:", VerticalOptions = LayoutOptions.Center },
                _rowsPicker
            }
        };
    }

    private View BuildPrefixSettingsContent()
    {
        _requirePrefixSwitch = new Switch();
        _requirePrefixSwitch.Toggled += async (_, _) =>
        {
            if (_loadingPrefixSettings || _prefixSettings == null)
                return;

            _prefixSettings.RequirePrefixBeforeCrop = _requirePrefixSwitch.IsToggled;
            await _hookPrefixes.SaveSettingsAsync(_prefixSettings);
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        grid.Add(new Label
        {
            Text = "Require prefix before crop",
            FontSize = 14,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);
        grid.Add(_requirePrefixSwitch, 1, 0);
        return grid;
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
        var wRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(60)),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(new GridLength(60))
            },
            ColumnSpacing = 8
        };
        wRow.Add(new Label { Text = "Width", FontSize = 13, TextColor = Color.FromArgb("#444"), VerticalOptions = LayoutOptions.Center }, 0, 0);
        var widthMinusBtn = new Button
        {
            Text = "−",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 36,
            WidthRequest = 36,
            CornerRadius = 6,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F")
        };
        widthMinusBtn.Clicked += (_, _) =>
        {
            _widthSlider.Value = Math.Max(
                _widthSlider.Minimum,
                _widthSlider.Value - 5);
        };
        wRow.Add(widthMinusBtn, 1, 0);
        _widthSlider = new Slider { Minimum = 10, Maximum = 1000, Value = 100, IsEnabled = false };
        _widthSlider.ValueChanged += OnSliderChanged;
        wRow.Add(_widthSlider, 2, 0);
        var widthPlusBtn = new Button
        {
            Text = "+",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 36,
            WidthRequest = 36,
            CornerRadius = 6,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F")
        };
        widthPlusBtn.Clicked += (_, _) =>
        {
            _widthSlider.Value = Math.Min(
                _widthSlider.Maximum,
                _widthSlider.Value + 5);
        };
        wRow.Add(widthPlusBtn, 3, 0);
        _widthValueLabel = new Label { Text = "100 px", FontSize = 12, TextColor = Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.End };
        wRow.Add(_widthValueLabel, 4, 0);
        v.Children.Add(wRow);
        var hRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(60)),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(new GridLength(60))
            },
            ColumnSpacing = 8
        };
        hRow.Add(new Label { Text = "Height", FontSize = 13, TextColor = Color.FromArgb("#444"), VerticalOptions = LayoutOptions.Center }, 0, 0);
        var heightMinusBtn = new Button
        {
            Text = "−",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 36,
            WidthRequest = 36,
            CornerRadius = 6,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F")
        };
        heightMinusBtn.Clicked += (_, _) =>
        {
            _heightSlider.Value = Math.Max(
                _heightSlider.Minimum,
                _heightSlider.Value - 5);
        };
        hRow.Add(heightMinusBtn, 1, 0);
        _heightSlider = new Slider { Minimum = 10, Maximum = 1000, Value = 100, IsEnabled = false };
        _heightSlider.ValueChanged += OnSliderChanged;
        hRow.Add(_heightSlider, 2, 0);
        var heightPlusBtn = new Button
        {
            Text = "+",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            HeightRequest = 36,
            WidthRequest = 36,
            CornerRadius = 6,
            Padding = 0,
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F")
        };
        heightPlusBtn.Clicked += (_, _) =>
        {
            _heightSlider.Value = Math.Min(
                _heightSlider.Maximum,
                _heightSlider.Value + 5);
        };
        hRow.Add(heightPlusBtn, 3, 0);
        _heightValueLabel = new Label { Text = "100 px", FontSize = 12, TextColor = Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.End };
        hRow.Add(_heightValueLabel, 4, 0);
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
            UpdatePreview();
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
        _separateBottomBtn = new Button
        {
            Text = "⊟ Separate bottom row",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 13,
            HeightRequest = 38,
            Padding = new Thickness(12, 0),
            HorizontalOptions = LayoutOptions.Start
        };
        _separateBottomBtn.Clicked += (_, _) =>
        {
            _separateBottomRow = !_separateBottomRow;
            _separateBottomBtn.Text = _separateBottomRow
                ? "⊟ Separate bottom row: ON"
                : "⊟ Separate bottom row";
            _separateBottomBtn.BackgroundColor = _separateBottomRow
                ? Color.FromArgb("#1565C0")
                : Color.FromArgb("#ECEFF1");
            _separateBottomBtn.TextColor = _separateBottomRow
                ? Colors.White
                : Color.FromArgb("#37474F");
            _bottomStripPanel.IsVisible = _separateBottomRow;
            if (_separateBottomRow)
            {
                _bottomCellW = _cellW;
                _bottomCellH = _cellH;
                _bottomWidthSlider.Value = _bottomCellW;
                _bottomHeightSlider.Value = _bottomCellH;
                _bottomWidthValueLabel.Text = $"{_bottomCellW} px";
                _bottomHeightValueLabel.Text = $"{_bottomCellH} px";
            }
            if (_sourceBitmap != null) UpdatePreview();
            RebuildSelectionGrid();
        };
        v.Children.Add(_separateBottomBtn);

        _bottomStripPanel = new VerticalStackLayout
        {
            Spacing = 8,
            IsVisible = false,
            Padding = new Thickness(0, 4, 0, 0)
        };

        _bottomStripPanel.Children.Add(new Label
        {
            Text = "Bottom strip settings:",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });

        var bottomColsRow = new HorizontalStackLayout { Spacing = 8 };
        bottomColsRow.Children.Add(new Label
        {
            Text = "Cols:",
            FontSize = 12,
            TextColor = Color.FromArgb("#444"),
            VerticalOptions = LayoutOptions.Center
        });
        _bottomColsPicker = new Picker { WidthRequest = 70 };
        foreach (var s in new[] { "1", "2", "3", "4", "5", "6", "7", "8" })
            _bottomColsPicker.Items.Add(s);
        _bottomColsPicker.SelectedIndex = 3;
        _bottomColsPicker.SelectedIndexChanged += (_, _) =>
        {
            if (_bottomColsPicker.SelectedItem is string s && int.TryParse(s, out int v))
            {
                _bottomCols = v;
                if (_sourceBitmap != null) UpdatePreview();
                RebuildSelectionGrid();
            }
        };
        bottomColsRow.Children.Add(_bottomColsPicker);
        _bottomStripPanel.Children.Add(bottomColsRow);

        var bHRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(60)),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(new GridLength(60))
            },
            ColumnSpacing = 8
        };
        bHRow.Add(new Label { Text = "Height", FontSize = 13,
            TextColor = Color.FromArgb("#444"), VerticalOptions = LayoutOptions.Center }, 0, 0);

        var bHeightMinusBtn = new Button
        {
            Text = "−", FontSize = 16, FontAttributes = FontAttributes.Bold,
            HeightRequest = 36, WidthRequest = 36, CornerRadius = 6, Padding = 0,
            BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F")
        };
        bHeightMinusBtn.Clicked += (_, _) =>
            _bottomHeightSlider.Value = Math.Max(_bottomHeightSlider.Minimum, _bottomHeightSlider.Value - 5);
        bHRow.Add(bHeightMinusBtn, 1, 0);

        _bottomHeightSlider = new Slider { Minimum = 1, Maximum = 5000, Value = 100 };
        _bottomHeightSlider.ValueChanged += (_, _) =>
        {
            _bottomCellH = (int)Math.Round(_bottomHeightSlider.Value);
            _bottomHeightValueLabel.Text = $"{_bottomCellH} px";
            if (_sourceBitmap != null) UpdatePreview();
        };
        bHRow.Add(_bottomHeightSlider, 2, 0);

        var bHeightPlusBtn = new Button
        {
            Text = "+", FontSize = 16, FontAttributes = FontAttributes.Bold,
            HeightRequest = 36, WidthRequest = 36, CornerRadius = 6, Padding = 0,
            BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F")
        };
        bHeightPlusBtn.Clicked += (_, _) =>
            _bottomHeightSlider.Value = Math.Min(_bottomHeightSlider.Maximum, _bottomHeightSlider.Value + 5);
        bHRow.Add(bHeightPlusBtn, 3, 0);

        _bottomHeightValueLabel = new Label
        {
            Text = "100 px", FontSize = 12, TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.End
        };
        bHRow.Add(_bottomHeightValueLabel, 4, 0);
        _bottomStripPanel.Children.Add(bHRow);

        // Bottom strip width row
        var bWRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(60)),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(38)),
                new ColumnDefinition(new GridLength(60))
            },
            ColumnSpacing = 8
        };
        bWRow.Add(new Label { Text = "Width", FontSize = 13,
            TextColor = Color.FromArgb("#444"), VerticalOptions = LayoutOptions.Center }, 0, 0);

        var bWidthMinusBtn = new Button
        {
            Text = "−", FontSize = 16, FontAttributes = FontAttributes.Bold,
            HeightRequest = 36, WidthRequest = 36, CornerRadius = 6, Padding = 0,
            BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F")
        };
        bWidthMinusBtn.Clicked += (_, _) =>
            _bottomWidthSlider.Value = Math.Max(_bottomWidthSlider.Minimum, _bottomWidthSlider.Value - 5);
        bWRow.Add(bWidthMinusBtn, 1, 0);

        _bottomWidthSlider = new Slider { Minimum = 1, Maximum = 5000, Value = 100 };
        _bottomWidthSlider.ValueChanged += (_, _) =>
        {
            _bottomCellW = (int)Math.Round(_bottomWidthSlider.Value);
            _bottomWidthValueLabel.Text = $"{_bottomCellW} px";
            if (_sourceBitmap != null && _separateBottomRow) UpdatePreview();
        };
        bWRow.Add(_bottomWidthSlider, 2, 0);

        var bWidthPlusBtn = new Button
        {
            Text = "+", FontSize = 16, FontAttributes = FontAttributes.Bold,
            HeightRequest = 36, WidthRequest = 36, CornerRadius = 6, Padding = 0,
            BackgroundColor = Color.FromArgb("#ECEFF1"), TextColor = Color.FromArgb("#37474F")
        };
        bWidthPlusBtn.Clicked += (_, _) =>
            _bottomWidthSlider.Value = Math.Min(_bottomWidthSlider.Maximum, _bottomWidthSlider.Value + 5);
        bWRow.Add(bWidthPlusBtn, 3, 0);

        _bottomWidthValueLabel = new Label
        {
            Text = "100 px", FontSize = 12, TextColor = Color.FromArgb("#222"),
            VerticalOptions = LayoutOptions.Center, HorizontalTextAlignment = TextAlignment.End
        };
        bWRow.Add(_bottomWidthValueLabel, 4, 0);
        _bottomStripPanel.Children.Add(bWRow);

        // Bottom strip cell preview
        _bottomPreviewLabel = new Label
        {
            Text = "Bottom-left panel preview:",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#444")
        };
        _bottomStripPanel.Children.Add(_bottomPreviewLabel);

        _bottomPreviewImage = new Image
        {
            HeightRequest = 200,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            IsVisible = false
        };
        _bottomStripPanel.Children.Add(_bottomPreviewImage);

        v.Children.Add(_bottomStripPanel);

        v.Children.Add(new Label { Text = "Sliders activate after an image is picked.", FontSize = 11, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic });
        return v;
    }

    private View BuildStep3Content()
    {
        var v = new VerticalStackLayout { Spacing = 14 };

        // ── CROP PREVIEW ─────────────────────────────────────────────
        v.Children.Add(new Label
        {
            Text = "Cell preview (top-left panel):",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#444")
        });

        _previewLabel = new Label
        {
            Text = "Pick an image and adjust sliders to see the crop preview.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };
        v.Children.Add(_previewLabel);

        _previewImage = new Image
        {
            HeightRequest = 300,
            Aspect = Aspect.AspectFit,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill
        };
        var cropTap = new TapGestureRecognizer();
        cropTap.Tapped += async (_, _) =>
        {
            if (_previewImage.Source == null || !_previewImage.IsVisible) return;
            await Navigation.PushAsync(new FullScreenImagePage(_previewImage.Source));
        };
        _previewImage.GestureRecognizers.Add(cropTap);
        v.Children.Add(_previewImage);
        v.Children.Add(new Label
        {
            Text = "Tap to fullscreen",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            FontAttributes = FontAttributes.Italic,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        });

        // Store tap hint ref so we can show/hide it
        // Use the last-added child approach: store as field
        _cropPreviewHint = (Label)v.Children[v.Children.Count - 1];

        // ── GRID PREVIEW ──────────────────────────────────────────────
        v.Children.Add(new Label
        {
            Text = "Grid preview (full image with numbered cells):",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#444"),
            IsVisible = false
        });
        _gridHeaderLabel = (Label)v.Children[v.Children.Count - 1];

        _gridPreviewLabel = new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            IsVisible = false
        };
        v.Children.Add(_gridPreviewLabel);

        _gridPreviewImage = new Image
        {
            HeightRequest = 400,
            Aspect = Aspect.AspectFit,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill
        };
        var gridTap = new TapGestureRecognizer();
        gridTap.Tapped += async (_, _) =>
        {
            if (_gridPreviewImage.Source == null || !_gridPreviewImage.IsVisible) return;
            await Navigation.PushAsync(new FullScreenImagePage(
                _gridImageBytes ?? Array.Empty<byte>(),
                _selected,
                _selected.GetLength(0),
                _selected.GetLength(1),
                _imagePixelW,
                _imagePixelH,
                updatedSelection =>
                {
                    // Copy state immediately on callback thread
                    for (int r = 0; r < _selected.GetLength(0); r++)
                        for (int c = 0; c < _selected.GetLength(1); c++)
                            _selected[r, c] = updatedSelection[r, c];

                    // Update UI on main thread after page is back in view
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        for (int r = 0; r < _selected.GetLength(0); r++)
                            for (int c = 0; c < _selected.GetLength(1); c++)
                                if (_selectionButtons[r, c] != null)
                                    ApplyButtonStyle(_selectionButtons[r, c],
                                        _selected[r, c]);
                        UpdateSelectionHint();
                    });
                }));
        };
        _gridPreviewImage.GestureRecognizers.Add(gridTap);
        v.Children.Add(_gridPreviewImage);
        v.Children.Add(new Label
        {
            Text = "Tap to fullscreen",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            FontAttributes = FontAttributes.Italic,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        });
        _gridPreviewHint = (Label)v.Children[v.Children.Count - 1];

        // ── SELECTION GRID ────────────────────────────────────────────
        _selectionHeader = new Label
        {
            Text = "Select panels to crop:",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#444"),
            IsVisible = false
        };
        v.Children.Add(_selectionHeader);

        _selectionGrid = new Grid { ColumnSpacing = 4, RowSpacing = 4, IsVisible = false };
        RebuildSelectionGrid();
        v.Children.Add(_selectionGrid);

        _selectAllRow = new HorizontalStackLayout { Spacing = 8, IsVisible = false };

        var selectAllBtn = new Button
        {
            Text = "Select All",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        };
        selectAllBtn.Clicked += (_, _) =>
        {
            for (int r = 0; r < _selected.GetLength(0); r++)
                for (int c = 0; c < _selected.GetLength(1); c++)
                {
                    if (_selectionButtons[r, c] == null) continue;
                    _selected[r, c] = true;
                    ApplyButtonStyle(_selectionButtons[r, c], true);
                }
            UpdateSelectionHint();
        };
        _selectAllRow.Children.Add(selectAllBtn);

        var deselectAllBtn = new Button
        {
            Text = "Deselect All",
            BackgroundColor = Color.FromArgb("#ECEFF1"),
            TextColor = Color.FromArgb("#37474F"),
            CornerRadius = 8,
            FontSize = 12,
            HeightRequest = 36,
            Padding = new Thickness(12, 0)
        };
        deselectAllBtn.Clicked += (_, _) =>
        {
            for (int r = 0; r < _selected.GetLength(0); r++)
                for (int c = 0; c < _selected.GetLength(1); c++)
                {
                    if (_selectionButtons[r, c] == null) continue;
                    _selected[r, c] = false;
                    ApplyButtonStyle(_selectionButtons[r, c], false);
                }
            UpdateSelectionHint();
        };
        _selectAllRow.Children.Add(deselectAllBtn);
        v.Children.Add(_selectAllRow);

        _selectionHintLabel = new Label
        {
            Text = "No panels selected.",
            FontSize = 12,
            TextColor = Color.FromArgb("#999"),
            IsVisible = false
        };
        v.Children.Add(_selectionHintLabel);

        _cropButton = new Button
        {
            Text = "✂️ Crop & Save Selected Panels",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold,
            IsVisible = false
        };
        _cropButton.Clicked += async (_, _) => await CropAndSaveAsync();
        v.Children.Add(_cropButton);

        return v;
    }

    private void RebuildSelectionGrid()
    {
        int mainRows = _separateBottomRow ? Math.Max(1, _rows - 1) : _rows;
        int totalRows = _separateBottomRow ? mainRows + 1 : mainRows;

        _selected = new bool[totalRows, Math.Max(_cols, _bottomCols)];
        _selectionButtons = new Button[totalRows, Math.Max(_cols, _bottomCols)];

        _selectionGrid.Children.Clear();
        _selectionGrid.RowDefinitions.Clear();
        _selectionGrid.ColumnDefinitions.Clear();

        int gridCols = _separateBottomRow ? Math.Max(_cols, _bottomCols) : _cols;
        for (int c = 0; c < gridCols; c++)
            _selectionGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int r = 0; r < totalRows; r++)
            _selectionGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int r = 0; r < mainRows; r++)
        {
            for (int c = 0; c < _cols; c++)
            {
                int panelNumber = r * _cols + c + 1;
                int capturedR = r, capturedC = c;
                var btn = new Button
                {
                    Text = panelNumber.ToString(),
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    HeightRequest = 64,
                    CornerRadius = 8,
                    Padding = 0,
                    HorizontalOptions = LayoutOptions.Fill,
                    BackgroundColor = Color.FromArgb("#E0E0E0"),
                    TextColor = Color.FromArgb("#444")
                };
                btn.Clicked += (_, _) =>
                {
                    _selected[capturedR, capturedC] = !_selected[capturedR, capturedC];
                    ApplyButtonStyle(btn, _selected[capturedR, capturedC]);
                    UpdateSelectionHint();
                };
                _selectionButtons[r, c] = btn;
                _selectionGrid.Add(btn, c, r);
            }
        }

        if (_separateBottomRow)
        {
            int stripOffset = mainRows * _cols;
            for (int c = 0; c < _bottomCols; c++)
            {
                int panelNumber = stripOffset + c + 1;
                int capturedC = c;
                var btn = new Button
                {
                    Text = $"B{panelNumber}",
                    FontSize = 14, FontAttributes = FontAttributes.Bold,
                    HeightRequest = 64, CornerRadius = 8, Padding = 0,
                    HorizontalOptions = LayoutOptions.Fill,
                    BackgroundColor = Color.FromArgb("#E3F2FD"),
                    TextColor = Color.FromArgb("#1565C0")
                };
                btn.Clicked += (_, _) =>
                {
                    _selected[mainRows, capturedC] = !_selected[mainRows, capturedC];
                    ApplyButtonStyle(btn, _selected[mainRows, capturedC]);
                    UpdateSelectionHint();
                };
                _selectionButtons[mainRows, c] = btn;
                _selectionGrid.Add(btn, c, mainRows);
            }
        }

        UpdateSelectionHint();
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
            _bottomWidthSlider.Maximum = imgW;
            _bottomHeightSlider.Maximum = imgH;
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

        int imgW = _sourceBitmap.Width;
        int imgH = _sourceBitmap.Height;
        int safeW = Math.Max(1, Math.Min(_cellW, imgW));
        int safeH = Math.Max(1, Math.Min(_cellH, imgH));
        int mainRows = _separateBottomRow ? Math.Max(1, _rows - 1) : _rows;

        // ── Crop preview: top-left cell ───────────────────────────────
        {
            var srcRect = new SKRectI(0, 0, safeW, safeH);
            using var cropped = new SKBitmap(safeW, safeH);
            using var canvas = new SKCanvas(cropped);
            canvas.DrawBitmap(_sourceBitmap, srcRect,
                new SKRect(0, 0, safeW, safeH));
            using var image = SKImage.FromBitmap(cropped);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = data.ToArray();
            _previewImage.Source = ImageSource.FromStream(
                () => new MemoryStream(bytes));
            _previewImage.IsVisible = true;
            _cropPreviewHint.IsVisible = true;
            _previewLabel.Text =
                $"Top-left panel: {safeW} × {safeH} px";
        }

        // ── Grid preview: full image with grid lines and numbers ───────
        {
            _imagePixelW = imgW;
            _imagePixelH = imgH;

            using var gridBmp = new SKBitmap(imgW, imgH);
            using var gridCanvas = new SKCanvas(gridBmp);
            gridCanvas.DrawBitmap(_sourceBitmap, 0, 0);

            // Draw grid lines
            using var linePaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 200),
                StrokeWidth = 2,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
            for (int col = 1; col < _cols; col++)
                gridCanvas.DrawLine(col * safeW, 0, col * safeW, mainRows * safeH, linePaint);

            for (int row = 1; row < mainRows; row++)
                gridCanvas.DrawLine(0, row * safeH, imgW, row * safeH, linePaint);

            if (_separateBottomRow)
            {
                int splitY = (_rows - 1) * safeH;
                using var splitPaint = new SKPaint
                {
                    Color = new SKColor(255, 220, 50, 230),
                    StrokeWidth = 3, IsAntialias = true,
                    Style = SKPaintStyle.Stroke
                };
                gridCanvas.DrawLine(0, splitY, imgW, splitY, splitPaint);

                int botW = _bottomCols > 0 ? imgW / _bottomCols : imgW;
                for (int col = 1; col < _bottomCols; col++)
                    gridCanvas.DrawLine(col * botW, splitY, col * botW, imgH, linePaint);
            }

            // Draw panel numbers
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = Math.Max(20, safeW / 8f),
                IsAntialias = true,
                FakeBoldText = true
            };
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 180),
                TextSize = textPaint.TextSize,
                IsAntialias = true,
                FakeBoldText = true
            };
            int panel = 1;
            for (int row = 0; row < mainRows; row++)
            {
                for (int col = 0; col < _cols; col++)
                {
                    float tx = col * safeW + safeW * 0.08f;
                    float ty = row * safeH + textPaint.TextSize + safeH * 0.05f;
                    // Shadow
                    gridCanvas.DrawText(panel.ToString(), tx + 2, ty + 2, shadowPaint);
                    // Text
                    gridCanvas.DrawText(panel.ToString(), tx, ty, textPaint);
                    panel++;
                }
            }

            if (_separateBottomRow)
            {
                int splitY = (_rows - 1) * safeH;
                int botW = _bottomCols > 0 ? imgW / _bottomCols : imgW;
                int botH = imgH - splitY;
                int stripOffset = mainRows * _cols;
                for (int col = 0; col < _bottomCols; col++)
                {
                    float tx = col * botW + botW * 0.08f;
                    float ty = splitY + textPaint.TextSize + botH * 0.05f;
                    gridCanvas.DrawText((stripOffset + col + 1).ToString(), tx + 2, ty + 2, shadowPaint);
                    gridCanvas.DrawText((stripOffset + col + 1).ToString(), tx, ty, textPaint);
                }
            }

            using var gridImage = SKImage.FromBitmap(gridBmp);
            using var gridData = gridImage.Encode(SKEncodedImageFormat.Png, 90);
            var gridBytes = gridData.ToArray();
            _gridImageBytes = gridBytes;
            _gridPreviewImage.Source = ImageSource.FromStream(
                () => new MemoryStream(gridBytes));
            _gridPreviewImage.IsVisible = true;
            _gridPreviewHint.IsVisible = true;
            string desc = _separateBottomRow
                ? $"Full grid: {imgW}×{imgH} px — {_cols}×{mainRows} main + {_bottomCols} bottom strip"
                : $"Full grid: {imgW}×{imgH} px — {_cols}×{_rows} cells";
            _gridPreviewLabel.Text = desc;
            _gridPreviewLabel.IsVisible = true;
            _gridHeaderLabel.IsVisible = true;
        }

        // ── Bottom strip cell preview ─────────────────────────────────
        if (_separateBottomRow && _sourceBitmap != null)
        {
            int splitY = (_rows - 1) * safeH;
            int botW = Math.Max(1, Math.Min(_bottomCellW, _sourceBitmap.Width));
            int botH = Math.Max(1, _bottomCellH);
            {
                int srcX2 = Math.Min(splitY + botH, _sourceBitmap.Height);
                int actualBotH = Math.Max(1, srcX2 - splitY);
                var srcRect = new SKRectI(0, splitY, botW, splitY + actualBotH);
                using var cropped = new SKBitmap(botW, botH);
                using var canvas = new SKCanvas(cropped);
                canvas.Clear(SKColors.Black);
                canvas.DrawBitmap(_sourceBitmap, srcRect, new SKRect(0, 0, botW, actualBotH));
                using var image = SKImage.FromBitmap(cropped);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                var bytes = data.ToArray();
                _bottomPreviewImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
                _bottomPreviewImage.IsVisible = true;
                _bottomPreviewLabel.Text = $"Bottom-left panel: {botW} × {botH} px";
            }
        }
        else if (_bottomPreviewImage != null)
        {
            _bottomPreviewImage.IsVisible = false;
        }

        _selectionHeader.IsVisible = true;
        _selectionGrid.IsVisible = true;
        _selectAllRow.IsVisible = true;
        _selectionHintLabel.IsVisible = true;
        UpdateSelectionHint();
        _statusLabel.IsVisible = false;
    }

    private async Task CropAndSaveAsync()
    {
        if (_sourceBitmap == null || _sourceFilePath == null) return;

        int mainRows = _separateBottomRow ? Math.Max(1, _rows - 1) : _rows;
        int totalRows = _separateBottomRow ? mainRows + 1 : mainRows;
        int selectedCount = 0;
        for (int r = 0; r < totalRows; r++)
            for (int c = 0; c < _selected.GetLength(1); c++)
                if (_selected[r, c]) selectedCount++;
        if (selectedCount == 0)
        {
            await DisplayAlert("Nothing to crop",
                "Select at least one panel first.", "OK");
            return;
        }

        var activeSession = await _hookPrefixes.GetActiveSessionAsync(_username);
        var lastPrefixText = _prefixSettings?.LastPrefixText ?? "";
        if (activeSession != null && !string.IsNullOrWhiteSpace(lastPrefixText))
        {
            activeSession.PrefixText = lastPrefixText;
            await _hookPrefixes.SaveSessionAsync(activeSession);
        }

        int safeW = Math.Max(1, Math.Min(_cellW, _sourceBitmap.Width));
        int safeH = Math.Max(1, Math.Min(_cellH, _sourceBitmap.Height));
        var sourceDir = System.IO.Path.GetDirectoryName(_sourceFilePath) ?? ".";
        var outputDir = System.IO.Path.Combine(sourceDir, "cropped_panels");

        _cropButton.IsEnabled = false;
        _cropButton.Text = "Cropping…";
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        try
        {
            var panels = await Task.Run(() =>
            {
                var result = new List<(string FileName, byte[] Bytes)>();
                for (int row = 0; row < mainRows; row++)
                {
                    for (int col = 0; col < _cols; col++)
                    {
                        if (!_selected[row, col]) continue;

                        int panelNumber = row * _cols + col + 1;
                        int x = col * safeW;
                        int y = row * safeH;
                        int cellW = safeW;
                        int cellH = safeH;
                        int actualW = Math.Min(cellW, _sourceBitmap.Width - x);
                        int actualH = Math.Min(cellH, _sourceBitmap.Height - y);
                        if (actualW <= 0 || actualH <= 0) continue;

                        var srcRect = new SKRectI(x, y, x + actualW, y + actualH);
                        using var cropped = new SKBitmap(cellW, cellH);
                        using var cropCanvas = new SKCanvas(cropped);
                        cropCanvas.Clear(SKColors.Black);
                        cropCanvas.DrawBitmap(_sourceBitmap, srcRect,
                            new SKRect(0, 0, actualW, actualH));

                        using var image = SKImage.FromBitmap(cropped);
                        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                        result.Add(($"panel_{panelNumber:D2}_{timestamp}.png",
                            encoded.ToArray()));
                    }
                }

                if (_separateBottomRow)
                {
                    int splitY = (_rows - 1) * safeH;
                    int botW = Math.Max(1, Math.Min(_bottomCellW, _sourceBitmap.Width));
                    int botH = Math.Max(1, _bottomCellH);
                    int stripOffset = mainRows * _cols;
                    for (int col = 0; col < _bottomCols; col++)
                    {
                        if (!_selected[mainRows, col]) continue;
                        int panelNumber = stripOffset + col + 1;
                        int x = col * botW;
                        int y = splitY;
                        int actualW = Math.Min(botW, _sourceBitmap.Width - x);
                        int actualH = Math.Min(_sourceBitmap.Height - y, botH);
                        if (actualW <= 0 || actualH <= 0) continue;
                        var srcRect = new SKRectI(x, y, x + actualW, y + actualH);
                        using var cropped = new SKBitmap(botW, botH);
                        using var cropCanvas = new SKCanvas(cropped);
                        cropCanvas.Clear(SKColors.Black);
                        cropCanvas.DrawBitmap(_sourceBitmap, srcRect, new SKRect(0, 0, actualW, actualH));
                        using var image = SKImage.FromBitmap(cropped);
                        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                        result.Add(($"strip_{panelNumber:D2}_{timestamp}.png", encoded.ToArray()));
                    }
                }
                return result;
            });

            if (panels.Count == 0)
            {
                await DisplayAlert("Nothing to crop",
                    "No valid panels could be cropped.", "OK");
                return;
            }

            string savedLocation = await _panelSaver.SavePanelsAsync(panels, outputDir);

            _statusLabel.Text = $"✓ {panels.Count} panels saved to:\n{savedLocation}";
            _statusLabel.TextColor = Color.FromArgb("#2E7D32");
            _statusLabel.IsVisible = true;

            await DisplayAlert("Done",
                $"{panels.Count} panels saved to:\n{savedLocation}", "OK");

            if (activeSession != null)
            {
                await _hookPrefixes.UpdateSessionCropResultAsync(
                    activeSession.Id,
                    panels.Count,
                    activeSession.TotalExtraordinary ?? 0);
            }

            await PromptForExtraordinaryCountAsync(
                activeSession,
                panels.Count);
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
            _cropButton.Text = "✂️ Crop & Save Selected Panels";
        }
    }

    private async Task PromptForExtraordinaryCountAsync(
        HookPrefixSession? activeSession,
        int croppedCount)
    {
        if (activeSession == null)
            return;

        var value = await DisplayPromptAsync(
            $"How many of the {croppedCount} cropped were extraordinary?",
            "Enter a number:",
            "Save",
            "Skip",
            keyboard: Keyboard.Numeric);
        if (string.IsNullOrWhiteSpace(value))
            return;

        if (!int.TryParse(value, out var extraordinary) ||
            extraordinary < 0)
        {
            await DisplayAlert("Invalid Count", "Enter a valid number.", "OK");
            return;
        }

        await _hookPrefixes.UpdateSessionCropResultAsync(
            activeSession.Id,
            croppedCount,
            extraordinary);
    }

    private static void ApplyButtonStyle(Button btn, bool selected)
    {
        btn.BackgroundColor = selected
            ? Color.FromArgb("#1565C0")
            : Color.FromArgb("#E0E0E0");
        btn.TextColor = selected ? Colors.White : Color.FromArgb("#444");
    }

    private void UpdateSelectionHint()
    {
        int count = 0;
        for (int r = 0; r < _selected.GetLength(0); r++)
            for (int c = 0; c < _selected.GetLength(1); c++)
                if (_selected[r, c]) count++;

        if (_selectionHintLabel == null || _cropButton == null) return;

        _selectionHintLabel.Text = count == 0
            ? "No panels selected."
            : $"{count} panel{(count == 1 ? "" : "s")} selected.";
        _selectionHintLabel.TextColor = count == 0
            ? Color.FromArgb("#999")
            : Color.FromArgb("#1565C0");
        _cropButton.IsVisible = count > 0;
    }

    private async Task LoadPresetsAsync()
    {
        _presets = await _presetService.GetPresetsAsync(_username);
        RefreshPresetPicker();
    }

    private async Task LoadPrefixSettingsAsync()
    {
        _loadingPrefixSettings = true;
        try
        {
            _prefixSettings = await _hookPrefixes.GetSettingsAsync(_username);
            _requirePrefixSwitch.IsToggled =
                _prefixSettings.RequirePrefixBeforeCrop;
        }
        finally
        {
            _loadingPrefixSettings = false;
        }
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
        // Do not dispose here — navigation to sub-pages (e.g. fullscreen)
        // triggers OnDisappearing and would destroy the bitmap.
        // Bitmap is disposed when a new image is picked or page is GC'd.
    }

}

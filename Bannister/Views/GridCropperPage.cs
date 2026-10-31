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
    private byte[]? _cleanImageBytes;
    private int _cellW = 100;
    private int _cellH = 100;
    private int _rows = 5;
    private bool _separateBottomRow = false;
    private int _bottomCols = 4;
    private int _bottomCellH = 100;
    private int _bottomCellW = 100;
    private int[]? _colEdges;
    private int[]? _rowEdges;
    private int? _bottomTop;
    private int[]? _bottomColEdges;
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

    private GridGeometry BuildGeometry()
    {
        var g = new GridGeometry
        {
            Cols = _cols, Rows = _rows, CellW = _cellW, CellH = _cellH,
            SeparateBottom = _separateBottomRow, BottomCols = _bottomCols,
            BottomCellW = _bottomCellW, BottomCellH = _bottomCellH
        };
        if (_colEdges != null) g.ColEdges = (int[])_colEdges.Clone();
        if (_rowEdges != null) g.RowEdges = (int[])_rowEdges.Clone();
        if (_bottomTop != null) g.BottomTop = _bottomTop.Value;
        if (_bottomColEdges != null) g.BottomColEdges = (int[])_bottomColEdges.Clone();
        if (_sourceBitmap != null)
            g.EnsureEdges(_sourceBitmap.Width, _sourceBitmap.Height);
        return g;
    }

    private void ResetEdges()
    {
        _colEdges = null;
        _rowEdges = null;
        _bottomTop = null;
        _bottomColEdges = null;
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
            Text = "Pick a 4×5 grid image. Set cell size with the sliders, then tap the grid preview to fine-tune each line. Tap Crop to save selected panels.",
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
            ResetEdges();
            if (_colsPicker.SelectedItem is not string s) return;
            if (!int.TryParse(s, out int v)) return;
            _cols = v;
            RebuildSelectionGrid();
            if (_sourceBitmap != null) UpdatePreview();
            _descLabel.Text = $"Pick a {_cols}×{_rows} grid image. Set cell size with the sliders, then tap the grid preview to fine-tune each line. Tap Crop to save selected panels.";
        };
        _rowsPicker.SelectedIndexChanged += (_, _) =>
        {
            ResetEdges();
            if (_rowsPicker.SelectedItem is not string s) return;
            if (!int.TryParse(s, out int v)) return;
            _rows = v;
            RebuildSelectionGrid();
            if (_sourceBitmap != null) UpdatePreview();
            _descLabel.Text = $"Pick a {_cols}×{_rows} grid image. Set cell size with the sliders, then tap the grid preview to fine-tune each line. Tap Crop to save selected panels.";
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
            ResetEdges();
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

        v.Children.Add(new Label { Text = "Sliders activate after an image is picked.", FontSize = 11, TextColor = Color.FromArgb("#999"), FontAttributes = FontAttributes.Italic });
        return v;
    }

    private View BuildStep3Content()
    {
        var v = new VerticalStackLayout { Spacing = 14 };

        var cellCol = new VerticalStackLayout
        {
            Spacing = 6,
            HorizontalOptions = LayoutOptions.Center
        };
        cellCol.Children.Add(new Label
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
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        cellCol.Children.Add(_previewLabel);
        _previewImage = new Image
        {
            HeightRequest = 260,
            Aspect = Aspect.AspectFit,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Center
        };
        var cropTap = new TapGestureRecognizer();
        cropTap.Tapped += async (_, _) =>
        {
            if (_previewImage.Source == null || !_previewImage.IsVisible) return;
            await Navigation.PushAsync(new FullScreenImagePage(_previewImage.Source));
        };
        _previewImage.GestureRecognizers.Add(cropTap);
        cellCol.Children.Add(_previewImage);
        _cropPreviewHint = new Label
        {
            Text = "Tap to fullscreen",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            FontAttributes = FontAttributes.Italic,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        cellCol.Children.Add(_cropPreviewHint);
        v.Children.Add(cellCol);

        // ── GRID PREVIEW (full width, as before) ──────────────────────
        _gridHeaderLabel = new Label
        {
            Text = "Grid preview (full image with numbered cells):",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#444"),
            IsVisible = false
        };
        v.Children.Add(_gridHeaderLabel);

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
            HeightRequest = 760,
            Aspect = Aspect.AspectFit,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Fill
        };
        var gridTap = new TapGestureRecognizer();
        gridTap.Tapped += async (_, _) =>
        {
            if (_gridPreviewImage.Source == null || !_gridPreviewImage.IsVisible) return;
            await Navigation.PushAsync(new FullScreenImagePage(
                _cleanImageBytes ?? _gridImageBytes ?? Array.Empty<byte>(),
                _selected,
                _imagePixelW,
                _imagePixelH,
                BuildGeometry(),
                updatedSelection =>
                {
                    for (int r = 0; r < _selected.GetLength(0); r++)
                        for (int c = 0; c < _selected.GetLength(1); c++)
                            _selected[r, c] = updatedSelection[r, c];
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        for (int r = 0; r < _selected.GetLength(0); r++)
                            for (int c = 0; c < _selected.GetLength(1); c++)
                                if (_selectionButtons[r, c] != null)
                                    ApplyButtonStyle(_selectionButtons[r, c], _selected[r, c]);
                        UpdateSelectionHint();
                    });
                },
                geo =>
                {
                    _colEdges = geo.ColEdges;
                    _rowEdges = geo.RowEdges;
                    _bottomTop = geo.SeparateBottom ? geo.BottomTop : null;
                    _bottomColEdges = geo.SeparateBottom ? geo.BottomColEdges : null;
                    MainThread.BeginInvokeOnMainThread(UpdatePreview);
                }));
        };
        _gridPreviewImage.GestureRecognizers.Add(gridTap);
        v.Children.Add(_gridPreviewImage);
        _gridPreviewHint = new Label
        {
            Text = "Tap to fullscreen",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            FontAttributes = FontAttributes.Italic,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        v.Children.Add(_gridPreviewHint);

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
        var g = BuildGeometry();
        int mainRows = g.MainRows;
        int totalRows = g.SeparateBottom ? mainRows + 1 : mainRows;

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
            ResetEdges();
            using (var cleanImage = SKImage.FromBitmap(_sourceBitmap))
            using (var cleanData = cleanImage.Encode(SKEncodedImageFormat.Png, 100))
                _cleanImageBytes = cleanData.ToArray();
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
        ResetEdges();
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
        var g = BuildGeometry();

        // ── Crop preview: top-left cell ──────────────────────────────
        {
            int left = Math.Clamp(g.ColEdges[0], 0, imgW);
            int top = Math.Clamp(g.RowEdges[0], 0, imgH);
            int right = Math.Clamp(g.ColEdges[1], left + 1, imgW);
            int bottom = Math.Clamp(g.RowEdges[1], top + 1, imgH);
            int cellW = right - left;
            int cellH = bottom - top;
            var srcRect = new SKRectI(left, top, right, bottom);
            using var cropped = new SKBitmap(cellW, cellH);
            using var canvas = new SKCanvas(cropped);
            canvas.DrawBitmap(_sourceBitmap, srcRect, new SKRect(0, 0, cellW, cellH));
            using var image = SKImage.FromBitmap(cropped);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            var bytes = data.ToArray();
            _previewImage.Source = ImageSource.FromStream(() => new MemoryStream(bytes));
            _previewImage.IsVisible = true;
            _cropPreviewHint.IsVisible = true;
            _previewLabel.Text = $"Top-left panel: {cellW} × {cellH} px";
        }

        // ── Grid preview ──────────────────────────────────────────────
        {
            _imagePixelW = imgW;
            _imagePixelH = imgH;
            using var gridBmp = new SKBitmap(imgW, imgH);
            using var gridCanvas = new SKCanvas(gridBmp);
            gridCanvas.DrawBitmap(_sourceBitmap, 0, 0);
            using var linePaint = new SKPaint
            {
                Color = new SKColor(255, 255, 255, 200),
                StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke
            };
            using var yellowPaint = new SKPaint
            {
                Color = new SKColor(255, 220, 50, 230),
                StrokeWidth = 3, IsAntialias = true, Style = SKPaintStyle.Stroke
            };
            for (int c = 1; c <= g.Cols; c++)
                gridCanvas.DrawLine(g.ColEdges[c], 0, g.ColEdges[c],
                    g.RowEdges[g.MainRows], linePaint);
            for (int r = 1; r <= g.MainRows; r++)
                gridCanvas.DrawLine(0, g.RowEdges[r], g.ColEdges[g.Cols],
                    g.RowEdges[r], linePaint);
            if (g.SeparateBottom)
            {
                gridCanvas.DrawLine(0, g.BottomTop, imgW, g.BottomTop, yellowPaint);
                for (int c = 1; c <= g.BottomCols; c++)
                    gridCanvas.DrawLine(g.BottomColEdges[c], g.BottomTop,
                        g.BottomColEdges[c], imgH, yellowPaint);
            }

            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = Math.Max(20, (g.ColEdges[1] - g.ColEdges[0]) / 8f),
                IsAntialias = true, FakeBoldText = true
            };
            using var shadowPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 180),
                TextSize = textPaint.TextSize, IsAntialias = true, FakeBoldText = true
            };
            for (int r = 0; r < g.MainRows; r++)
            {
                for (int c = 0; c < g.Cols; c++)
                {
                    int panel = r * g.Cols + c + 1;
                    float tx = g.ColEdges[c] +
                        (g.ColEdges[c + 1] - g.ColEdges[c]) * 0.08f;
                    float ty = g.RowEdges[r] + textPaint.TextSize +
                        (g.RowEdges[r + 1] - g.RowEdges[r]) * 0.05f;
                    gridCanvas.DrawText(panel.ToString(), tx + 2, ty + 2, shadowPaint);
                    gridCanvas.DrawText(panel.ToString(), tx, ty, textPaint);
                }
            }
            if (g.SeparateBottom)
            {
                int stripOffset = g.MainRows * g.Cols;
                for (int c = 0; c < g.BottomCols; c++)
                {
                    int panel = stripOffset + c + 1;
                    float tx = g.BottomColEdges[c] +
                        (g.BottomColEdges[c + 1] - g.BottomColEdges[c]) * 0.08f;
                    float ty = g.BottomTop + textPaint.TextSize +
                        (imgH - g.BottomTop) * 0.05f;
                    gridCanvas.DrawText(panel.ToString(), tx + 2, ty + 2, shadowPaint);
                    gridCanvas.DrawText(panel.ToString(), tx, ty, textPaint);
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
            _gridPreviewLabel.Text =
                $"Full grid: {imgW}×{imgH} px — {_cols}×{g.MainRows} main" +
                (g.SeparateBottom ? $" + {_bottomCols} bottom strip" : "");
            _gridPreviewLabel.IsVisible = true;
            _gridHeaderLabel.IsVisible = true;
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

        var g = BuildGeometry();
        int mainRows = g.MainRows;
        int totalRows = g.SeparateBottom ? mainRows + 1 : mainRows;
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
                    for (int col = 0; col < g.Cols; col++)
                    {
                        if (!_selected[row, col]) continue;

                        int panelNumber = row * g.Cols + col + 1;
                        int x = Math.Clamp(g.ColEdges[col], 0, _sourceBitmap.Width);
                        int y = Math.Clamp(g.RowEdges[row], 0, _sourceBitmap.Height);
                        int right = Math.Clamp(g.ColEdges[col + 1], 0, _sourceBitmap.Width);
                        int bottom = Math.Clamp(g.RowEdges[row + 1], 0, _sourceBitmap.Height);
                        int actualW = right - x;
                        int actualH = bottom - y;
                        if (actualW <= 0 || actualH <= 0) continue;

                        var srcRect = new SKRectI(x, y, right, bottom);
                        using var cropped = new SKBitmap(actualW, actualH);
                        using var cropCanvas = new SKCanvas(cropped);
                        cropCanvas.DrawBitmap(_sourceBitmap, srcRect,
                            new SKRect(0, 0, actualW, actualH));

                        using var image = SKImage.FromBitmap(cropped);
                        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                        result.Add(($"panel_{panelNumber:D2}_{timestamp}.png",
                            encoded.ToArray()));
                    }
                }

                if (g.SeparateBottom)
                {
                    int stripOffset = mainRows * g.Cols;
                    for (int col = 0; col < g.BottomCols; col++)
                    {
                        if (!_selected[mainRows, col]) continue;
                        int panelNumber = stripOffset + col + 1;
                        int x = Math.Clamp(g.BottomColEdges[col], 0, _sourceBitmap.Width);
                        int y = Math.Clamp(g.BottomTop, 0, _sourceBitmap.Height);
                        int right = Math.Clamp(g.BottomColEdges[col + 1], 0, _sourceBitmap.Width);
                        int actualW = right - x;
                        int actualH = _sourceBitmap.Height - y;
                        if (actualW <= 0 || actualH <= 0) continue;
                        var srcRect = new SKRectI(x, y, right, _sourceBitmap.Height);
                        using var cropped = new SKBitmap(actualW, actualH);
                        using var cropCanvas = new SKCanvas(cropped);
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

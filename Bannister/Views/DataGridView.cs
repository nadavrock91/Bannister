using Microsoft.Maui.Controls;

namespace Bannister.Views;

public class CellTappedEventArgs : EventArgs
{
    public int RowIndex { get; set; }       // Absolute row index (across all pages)
    public int ColumnIndex { get; set; }
    public string ColumnName { get; set; } = "";
    public string Value { get; set; } = "";
    public string FullValue { get; set; } = "";
}

public class CellEditRecord
{
    public string IdValue { get; set; } = "";
    public string ColumnName { get; set; } = "";
    public string OldValue { get; set; } = "";
    public string NewValue { get; set; } = "";
    public int RowIndex { get; set; }       // Absolute row index
    public int ColIndex { get; set; }
    public DateTime EditedAt { get; set; } = DateTime.Now;
}

public delegate Task<bool> CellUpdateDelegate(string idValue, string columnName, string newValue);

/// <summary>
/// Self-contained data grid with display box, edit, undo, copy, paste, and pagination.
///
/// Exposes ToolbarView (fixed) and GridView (scrollable, includes pagination bar).
/// Set PageSize to control rows per page (default 100, 0 = show all).
/// CellTapped events report absolute row indices.
/// </summary>
public class DataGridView : ContentView
{
    // Styling
    public Color HeaderBackgroundColor { get; set; } = Color.FromArgb("#1565C0");
    public Color HeaderTextColor { get; set; } = Colors.White;
    public Color CellBackgroundColor { get; set; } = Colors.White;
    public Color CellTextColor { get; set; } = Color.FromArgb("#333333");
    public Color AlternateRowColor { get; set; } = Color.FromArgb("#F5F5F5");
    public Color SelectedCellColor { get; set; } = Color.FromArgb("#BBDEFB");
    public Color EditingCellColor { get; set; } = Color.FromArgb("#FFF9C4");
    public Color BorderColor { get; set; } = Color.FromArgb("#DDDDDD");
    public bool UseAlternatingRowColors { get; set; } = true;
    public bool ShowHeaders { get; set; } = true;
    public double CellPadding { get; set; } = 10;
    public double FontSize { get; set; } = 13;
    public double HeaderFontSize { get; set; } = 14;
    public double MinColumnWidth { get; set; } = 80;
    public double MaxColumnWidth { get; set; } = 300;
    public string? Title { get; set; }

    /// <summary>Rows per page. 0 = no pagination (show all).</summary>
    public int PageSize { get; set; } = 100;

    public CellUpdateDelegate? OnUpdateCell { get; set; }
    public string IdColumnName { get; set; } = "Id";
    public event EventHandler<CellTappedEventArgs>? CellTapped;

    /// <summary>Display box + ⋯ menu. Place OUTSIDE scroll.</summary>
    public View ToolbarView { get; private set; }
    /// <summary>Grid + pagination bar. Place INSIDE scroll.</summary>
    public View GridView { get; private set; }

    // All data (full set)
    private List<string> _headers = new();
    private List<List<string>> _allRows = new();
    private List<List<string>> _allFullRows = new();

    // Current page data
    private List<List<string>> _pageRows = new();
    private List<List<string>> _pageFullRows = new();

    // Grid internals
    private Label[,]? _cellLabels;
    private int _columnCount;
    private int _pageRowCount;
    private Label? _lastSelectedLabel;

    // Toolbar
    private Editor _displayBox;
    private Label _displayHeader;
    private Entry _keyCapture;

    // Pagination
    private int _pageOffset = 0;
    private Label? _pageLabel;
    private Button? _btnPrev;
    private Button? _btnNext;
    private Grid? _paginationBar;
    private VerticalStackLayout _gridWrapper; // holds grid only

    // Selection & edit (absolute indices)
    private int _selectedRow = -1;  // absolute
    private int _selectedCol = -1;
    private string _selectedColumnName = "";
    private string _originalCellValue = "";
    private bool _isEditing = false;

    // Hover
    private int _hoveredRow = -1; // page-relative
    private int _hoveredCol = -1;

    // Undo
    private List<CellEditRecord> _undoHistory = new();
    private const int MaxUndo = 50;

    public DataGridView()
    {
        ToolbarView = new VerticalStackLayout();
        GridView = new VerticalStackLayout();
    }

    // ===================== PUBLIC API =====================

    public void SetData(List<string> headers, List<List<string>> rows)
    {
        _headers = headers ?? new(); _allRows = rows ?? new(); _allFullRows = new();
        _pageOffset = 0; FullRender();
    }

    public void SetData(List<string> headers, List<List<string>> displayRows, List<List<string>> fullRows)
    {
        _headers = headers ?? new(); _allRows = displayRows ?? new(); _allFullRows = fullRows ?? new();
        _pageOffset = 0; FullRender();
    }

    public void SetData(List<List<string>> rows)
    {
        _allRows = rows ?? new(); _allFullRows = new();
        int c = _allRows.Count > 0 ? _allRows.Max(r => r.Count) : 0;
        _headers = Enumerable.Range(1, c).Select(i => $"Col {i}").ToList();
        _pageOffset = 0; FullRender();
    }

    public void UpdateCellDisplay(int absRow, int col, string value)
    {
        // Update in all-data arrays
        if (absRow >= 0 && absRow < _allRows.Count && col >= 0)
        {
            string truncated = value.Length > 50 ? value.Substring(0, 47) + "..." : value;
            if (col < _allRows[absRow].Count) _allRows[absRow][col] = truncated;
            if (absRow < _allFullRows.Count && col < _allFullRows[absRow].Count) _allFullRows[absRow][col] = value;
        }

        // Update in grid if visible on current page
        int pageRow = absRow - _pageOffset;
        if (_cellLabels != null && pageRow >= 0 && pageRow < _cellLabels.GetLength(0) && col >= 0 && col < _cellLabels.GetLength(1))
        {
            string truncated = value.Length > 50 ? value.Substring(0, 47) + "..." : value;
            _cellLabels[pageRow, col].Text = truncated;
        }
    }

    public void ClearUndoHistory() => _undoHistory.Clear();
    public int UndoCount => _undoHistory.Count;

    // ===================== RENDER =====================

    private void FullRender()
    {
        _columnCount = _headers.Count;
        if (_columnCount == 0 && _allRows.Count > 0) _columnCount = _allRows.Max(r => r.Count);
        _selectedRow = -1; _selectedCol = -1; _isEditing = false;

        BuildToolbar();
        _gridWrapper = new VerticalStackLayout { Spacing = 4 };
        RebuildPage();

        var combined = new VerticalStackLayout { Spacing = 4 };
        combined.Children.Add(ToolbarView);
        combined.Children.Add(_gridWrapper);
        GridView = _gridWrapper;
        Content = combined;
    }

    /// <summary>Rebuild just the grid for the current page without rebuilding toolbar.</summary>
    private void RebuildPage()
    {
        _gridWrapper.Children.Clear();

        // Slice current page
        bool paginated = PageSize > 0 && _allRows.Count > PageSize;
        if (paginated)
        {
            _pageRows = _allRows.Skip(_pageOffset).Take(PageSize).ToList();
            _pageFullRows = _allFullRows.Count > 0
                ? _allFullRows.Skip(_pageOffset).Take(PageSize).ToList()
                : new();
        }
        else
        {
            _pageRows = _allRows;
            _pageFullRows = _allFullRows;
        }

        _pageRowCount = _pageRows.Count;

        // Update pagination bar (lives in toolbar, outside scroll)
        if (_paginationBar != null)
        {
            _paginationBar.IsVisible = paginated;
            if (paginated)
            {
                int start = _pageOffset + 1;
                int end = _pageOffset + _pageRowCount;
                if (_pageLabel != null) _pageLabel.Text = $"{start}-{end} of {_allRows.Count}";
                if (_btnPrev != null) _btnPrev.IsEnabled = _pageOffset > 0;
                if (_btnNext != null) _btnNext.IsEnabled = _pageOffset + PageSize < _allRows.Count;
            }
        }

        // Build grid content
        BuildGridContent();
    }

    private async void SaveAndNavigate(int delta)
    {
        if (_isEditing) await AutoSaveAsync();
        _selectedRow = -1; _selectedCol = -1;
        _pageOffset += delta;
        RebuildPage();
        ScrollToTop();

        if (_displayHeader != null)
            _displayHeader.Text = "Left-click: select · Right-click: edit";
        if (_displayBox != null)
            _displayBox.Text = "";
    }

    private void BuildToolbar()
    {
        var stack = new VerticalStackLayout { Spacing = 2 };

        var headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 6
        };

        _displayHeader = new Label
        {
            Text = "Left-click: select · Right-click: edit",
            FontSize = 11, TextColor = Color.FromArgb("#888"),
            VerticalOptions = LayoutOptions.Center
        };
        headerRow.Add(_displayHeader, 0, 0);

        var menuBtn = new Button
        {
            Text = "⋯", FontSize = 16, FontAttributes = FontAttributes.Bold,
            HeightRequest = 28, WidthRequest = 36, Padding = 0,
            BackgroundColor = Color.FromArgb("#E0E0E0"), TextColor = Color.FromArgb("#555"),
            CornerRadius = 4, VerticalOptions = LayoutOptions.Center
        };
        menuBtn.Clicked += OnMenuClicked;
        headerRow.Add(menuBtn, 1, 0);
        stack.Children.Add(headerRow);

        _displayBox = new Editor
        {
            Text = "", FontSize = 13, FontFamily = "Consolas",
            BackgroundColor = Colors.White, TextColor = Color.FromArgb("#333"),
            IsReadOnly = true, HeightRequest = 85,
            AutoSize = EditorAutoSizeOption.Disabled
        };

        _displayBox.Focused += (s, e) =>
        {
            if (_isEditing && _displayBox.Text?.Length > 0)
            {
#if WINDOWS
                var pv = _displayBox.Handler?.PlatformView as Microsoft.UI.Xaml.Controls.TextBox;
                pv?.SelectAll();
#elif ANDROID
                var pv = _displayBox.Handler?.PlatformView as AndroidX.AppCompat.Widget.AppCompatEditText;
                pv?.SelectAll();
#endif
            }
        };
        stack.Children.Add(_displayBox);

        _keyCapture = new Entry { WidthRequest = 0, HeightRequest = 0, Opacity = 0, InputTransparent = false, IsReadOnly = true };
#if WINDOWS
        _keyCapture.HandlerChanged += (s, e) =>
        {
            if (_keyCapture.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox tb)
                tb.PreviewKeyDown += OnWindowsKeyDown;
        };
#endif
        stack.Children.Add(_keyCapture);

        // Pagination bar (part of toolbar so it stays fixed outside scroll)
        _paginationBar = new Grid
        {
            Padding = new Thickness(0, 4),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            IsVisible = false
        };

        _btnPrev = new Button
        {
            Text = "← Prev", FontSize = 12, HeightRequest = 30,
            BackgroundColor = Color.FromArgb("#5B63EE"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(10, 0)
        };
        _btnPrev.Clicked += (s, e) => { if (_pageOffset >= PageSize) SaveAndNavigate(-PageSize); };
        _paginationBar.Add(_btnPrev, 0, 0);

        _pageLabel = new Label
        {
            Text = "", FontSize = 12, TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center, VerticalOptions = LayoutOptions.Center
        };
        _paginationBar.Add(_pageLabel, 1, 0);

        _btnNext = new Button
        {
            Text = "Next →", FontSize = 12, HeightRequest = 30,
            BackgroundColor = Color.FromArgb("#5B63EE"), TextColor = Colors.White,
            CornerRadius = 6, Padding = new Thickness(10, 0)
        };
        _btnNext.Clicked += (s, e) => { if (_pageOffset + PageSize < _allRows.Count) SaveAndNavigate(PageSize); };
        _paginationBar.Add(_btnNext, 2, 0);

        stack.Children.Add(_paginationBar);

        ToolbarView = stack;
    }

#if WINDOWS
    private async void OnWindowsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (_isEditing) return;
        if (_selectedRow < 0 || _selectedCol < 0) return;

        bool handled = false;
        int newAbsRow = _selectedRow, newCol = _selectedCol;

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Up:
                if (_selectedRow > 0) { newAbsRow--; handled = true; }
                break;
            case Windows.System.VirtualKey.Down:
                if (_selectedRow < _allRows.Count - 1) { newAbsRow++; handled = true; }
                break;
            case Windows.System.VirtualKey.Left:
                if (_selectedCol > 0) { newCol--; handled = true; }
                break;
            case Windows.System.VirtualKey.Right:
                if (_selectedCol < _columnCount - 1) { newCol++; handled = true; }
                break;
            case Windows.System.VirtualKey.Delete:
            case Windows.System.VirtualKey.Back:
                await ClearSelectedCellAsync();
                handled = true;
                break;
        }

        if (handled)
        {
            e.Handled = true;
            if (newAbsRow != _selectedRow || newCol != _selectedCol)
            {
                // Check if we need to change page
                if (PageSize > 0 && (newAbsRow < _pageOffset || newAbsRow >= _pageOffset + PageSize))
                {
                    _pageOffset = (newAbsRow / PageSize) * PageSize;
                    _selectedRow = newAbsRow; _selectedCol = newCol;
                    RebuildPage();
                    // Select the cell on the new page
                    int pageRow = newAbsRow - _pageOffset;
                    SelectCellByPageRow(pageRow, newCol);
                }
                else
                {
                    SelectCellAbsolute(newAbsRow, newCol);
                }
            }
        }
    }
#endif

    private void BuildGridContent()
    {
        if (_columnCount == 0)
        {
            _gridWrapper.Children.Add(new Label { Text = "No data to display", TextColor = Color.FromArgb("#666") });
            return;
        }

        int totalGridRows = (ShowHeaders ? 1 : 0) + _pageRowCount;
        var grid = new Grid { ColumnSpacing = 0, RowSpacing = 0, BackgroundColor = BorderColor };

        for (int c = 0; c < _columnCount; c++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        for (int r = 0; r < totalGridRows; r++)
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        int currentRow = 0;

        if (ShowHeaders && _headers.Count > 0)
        {
            for (int col = 0; col < _columnCount; col++)
            {
                var label = new Label
                {
                    Text = col < _headers.Count ? _headers[col] : "",
                    FontSize = HeaderFontSize, FontAttributes = FontAttributes.Bold,
                    TextColor = HeaderTextColor, BackgroundColor = HeaderBackgroundColor,
                    Padding = new Thickness(CellPadding), VerticalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1,
                    MinimumWidthRequest = MinColumnWidth, MaximumWidthRequest = MaxColumnWidth,
                    Margin = new Thickness(0, 0, 1, 1)
                };
                grid.Add(label, col, currentRow);
            }
            currentRow++;
        }

        _cellLabels = new Label[_pageRowCount, _columnCount];
        for (int rowIdx = 0; rowIdx < _pageRowCount; rowIdx++)
        {
            var rowData = _pageRows[rowIdx];
            bool isAlt = UseAlternatingRowColors && rowIdx % 2 == 1;
            Color bgColor = isAlt ? AlternateRowColor : CellBackgroundColor;

            for (int col = 0; col < _columnCount; col++)
            {
                string text = col < rowData.Count ? rowData[col] : "";
                var label = new Label
                {
                    Text = text, FontSize = FontSize, TextColor = CellTextColor,
                    BackgroundColor = bgColor, Padding = new Thickness(CellPadding),
                    VerticalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation, MaxLines = 1,
                    MinimumWidthRequest = MinColumnWidth, MaximumWidthRequest = MaxColumnWidth,
                    Margin = new Thickness(0, 0, 1, 1)
                };

                _cellLabels[rowIdx, col] = label;
                int cPageRow = rowIdx, cCol = col;

                var leftTap = new TapGestureRecognizer();
                leftTap.Tapped += (s, e) => HandleLeftClick(cPageRow, cCol);
                label.GestureRecognizers.Add(leftTap);

                var hover = new PointerGestureRecognizer();
                hover.PointerEntered += (s, e) => { _hoveredRow = cPageRow; _hoveredCol = cCol; };
                label.GestureRecognizers.Add(hover);

                grid.Add(label, col, currentRow);
            }
            currentRow++;
        }

        if (_pageRowCount == 0)
        {
            var emptyLabel = new Label { Text = "No rows", TextColor = Color.FromArgb("#999"), HorizontalTextAlignment = TextAlignment.Center, Padding = new Thickness(20) };
            grid.Add(emptyLabel, 0, currentRow);
            Grid.SetColumnSpan((BindableObject)emptyLabel, _columnCount);
        }

#if WINDOWS
        grid.HandlerChanged += (s, e) =>
        {
            if (grid.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement fe)
                fe.RightTapped += (sender, args) =>
                {
                    if (_hoveredRow >= 0 && _hoveredCol >= 0)
                    { HandleRightClick(_hoveredRow, _hoveredCol); args.Handled = true; }
                };
        };
#endif

        for (int rowIdx = 0; rowIdx < _pageRowCount && _cellLabels != null; rowIdx++)
            for (int col = 0; col < _columnCount; col++)
            {
                int cR = rowIdx, cC = col;
                var lp = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
                lp.Tapped += (s, e) => HandleRightClick(cR, cC);
                _cellLabels[rowIdx, col].GestureRecognizers.Add(lp);
            }

        _gridWrapper.Children.Add(grid);
    }

    // ===================== ABSOLUTE ↔ PAGE ROW HELPERS =====================

    private int PageRowToAbsolute(int pageRow) => _pageOffset + pageRow;
    private int AbsoluteToPageRow(int absRow) => absRow - _pageOffset;
    private bool IsOnCurrentPage(int absRow) => absRow >= _pageOffset && absRow < _pageOffset + _pageRowCount;

    // ===================== LEFT CLICK → SELECT =====================

    private async void HandleLeftClick(int pageRow, int col)
    {
        if (_isEditing) await AutoSaveAsync();
        SelectCellByPageRow(pageRow, col);
        await Task.Delay(30);
        _keyCapture?.Focus();
    }

    private void SelectCellByPageRow(int pageRow, int col)
    {
        SelectCellAbsolute(PageRowToAbsolute(pageRow), col);
    }

    private void SelectCellAbsolute(int absRow, int col)
    {
        if (col < 0 || col >= _columnCount) return;
        if (absRow < 0 || absRow >= _allRows.Count) return;

        // Reset previous
        if (_lastSelectedLabel != null) ResetLabelColor(_lastSelectedLabel);

        // Highlight if on current page
        int pageRow = AbsoluteToPageRow(absRow);
        if (_cellLabels != null && pageRow >= 0 && pageRow < _cellLabels.GetLength(0) && col < _cellLabels.GetLength(1))
        {
            var label = _cellLabels[pageRow, col];
            label.BackgroundColor = SelectedCellColor;
            _lastSelectedLabel = label;
        }

        _selectedRow = absRow;
        _selectedCol = col;
        _selectedColumnName = col < _headers.Count ? _headers[col] : $"Col {col + 1}";

        string fullValue = (_allFullRows.Count > absRow && _allFullRows[absRow].Count > col) ? _allFullRows[absRow][col]
            : (_allRows.Count > absRow && _allRows[absRow].Count > col) ? _allRows[absRow][col] : "";
        _originalCellValue = fullValue;

        if (_displayHeader != null) _displayHeader.Text = $"Row {absRow + 1} • {_selectedColumnName}";
        if (_displayBox != null) _displayBox.Text = fullValue;

        CellTapped?.Invoke(this, new CellTappedEventArgs
        {
            RowIndex = absRow, ColumnIndex = col, ColumnName = _selectedColumnName,
            Value = _allRows[absRow].Count > col ? _allRows[absRow][col] : "", FullValue = fullValue
        });
    }

    // ===================== RIGHT CLICK → EDIT =====================

    private async void HandleRightClick(int pageRow, int col)
    {
        int absRow = PageRowToAbsolute(pageRow);
        if (_isEditing && (absRow != _selectedRow || col != _selectedCol))
            await AutoSaveAsync();

        SelectCellAbsolute(absRow, col);

        if (_selectedColumnName.Equals(IdColumnName, StringComparison.OrdinalIgnoreCase)) return;
        if (OnUpdateCell == null) return;

        EnterEditMode();
    }

    // ===================== DELETE KEY =====================

    private async Task ClearSelectedCellAsync()
    {
        if (_selectedRow < 0 || _selectedCol < 0 || OnUpdateCell == null) return;
        if (_selectedColumnName.Equals(IdColumnName, StringComparison.OrdinalIgnoreCase)) return;

        string? idValue = GetIdForRow(_selectedRow);
        if (idValue == null) return;

        string oldValue = (_allFullRows.Count > _selectedRow && _allFullRows[_selectedRow].Count > _selectedCol)
            ? _allFullRows[_selectedRow][_selectedCol] : "";
        if (string.IsNullOrEmpty(oldValue) || oldValue == "NULL") return;

        try
        {
            bool ok = await OnUpdateCell(idValue, _selectedColumnName, "");
            if (ok)
            {
                PushUndo(idValue, _selectedColumnName, oldValue, "", _selectedRow, _selectedCol);
                UpdateCellDisplay(_selectedRow, _selectedCol, "");
                if (_displayBox != null) _displayBox.Text = "";
                if (_displayHeader != null) _displayHeader.Text = $"Cleared ✓ {_selectedColumnName} in row {_selectedRow + 1}";
            }
        }
        catch (Exception ex) { if (_displayHeader != null) _displayHeader.Text = $"Clear failed: {ex.Message}"; }
    }

    // ===================== EDIT MODE =====================

    private async void EnterEditMode()
    {
        if (_selectedRow < 0 || _selectedCol < 0) return;
        _isEditing = true;
        _displayBox.IsReadOnly = false;
        _displayBox.BackgroundColor = Color.FromArgb("#FFFDE7");
        _displayHeader.Text = $"✏️ EDITING — Row {_selectedRow + 1} • {_selectedColumnName}";

        int pageRow = AbsoluteToPageRow(_selectedRow);
        if (_cellLabels != null && pageRow >= 0 && pageRow < _cellLabels.GetLength(0) && _selectedCol < _cellLabels.GetLength(1))
            _cellLabels[pageRow, _selectedCol].BackgroundColor = EditingCellColor;

        await Task.Delay(50);
        _displayBox.Focus();
    }

    private void ExitEditMode()
    {
        int pageRow = AbsoluteToPageRow(_selectedRow);
        if (_cellLabels != null && pageRow >= 0 && pageRow < _cellLabels.GetLength(0) &&
            _selectedCol >= 0 && _selectedCol < _cellLabels.GetLength(1))
        {
            bool isAlt = UseAlternatingRowColors && pageRow % 2 == 1;
            _cellLabels[pageRow, _selectedCol].BackgroundColor = isAlt ? AlternateRowColor : CellBackgroundColor;
        }
        _isEditing = false;
        if (_displayBox != null) { _displayBox.IsReadOnly = true; _displayBox.BackgroundColor = Colors.White; }
    }

    private async Task AutoSaveAsync()
    {
        if (!_isEditing || _selectedRow < 0 || _selectedCol < 0 || OnUpdateCell == null) return;

        string newValue = _displayBox.Text ?? "";
        if (newValue == _originalCellValue) { ExitEditMode(); return; }

        string? idValue = GetIdForRow(_selectedRow);
        if (idValue == null) { ExitEditMode(); return; }

        int sRow = _selectedRow, sCol = _selectedCol;
        string sColName = _selectedColumnName;

        try
        {
            bool ok = await OnUpdateCell(idValue, sColName, newValue);
            if (ok)
            {
                PushUndo(idValue, sColName, _originalCellValue, newValue, sRow, sCol);
                UpdateCellDisplay(sRow, sCol, newValue);
                ExitEditMode();
                if (_displayHeader != null) _displayHeader.Text = $"Saved ✓ {sColName} in row {sRow + 1}";
            }
            else { ExitEditMode(); if (_displayHeader != null) _displayHeader.Text = $"Save failed for {sColName}"; }
        }
        catch (Exception ex) { ExitEditMode(); if (_displayHeader != null) _displayHeader.Text = $"Error: {ex.Message}"; }
    }

    private void ResetLabelColor(Label label)
    {
        if (_cellLabels == null) { label.BackgroundColor = CellBackgroundColor; return; }
        for (int r = 0; r < _cellLabels.GetLength(0); r++)
            for (int c = 0; c < _cellLabels.GetLength(1); c++)
                if (_cellLabels[r, c] == label)
                { label.BackgroundColor = (UseAlternatingRowColors && r % 2 == 1) ? AlternateRowColor : CellBackgroundColor; return; }
        label.BackgroundColor = CellBackgroundColor;
    }

    // ===================== UNDO =====================

    private void PushUndo(string idValue, string colName, string oldVal, string newVal, int row, int col)
    {
        _undoHistory.Add(new CellEditRecord { IdValue = idValue, ColumnName = colName, OldValue = oldVal, NewValue = newVal, RowIndex = row, ColIndex = col });
        if (_undoHistory.Count > MaxUndo) _undoHistory.RemoveAt(0);
    }

    // ===================== ⋯ MENU =====================

    private async void OnMenuClicked(object? sender, EventArgs e)
    {
        if (_isEditing) await AutoSaveAsync();

        bool hasSel = _selectedRow >= 0 && _selectedCol >= 0;
        bool isId = _selectedColumnName.Equals(IdColumnName, StringComparison.OrdinalIgnoreCase);
        bool canEdit = OnUpdateCell != null;

        var actions = new List<string>();
        actions.Add("📋 Copy to Clipboard");
        if (hasSel && !isId && canEdit) actions.Add("📥 Paste from Clipboard");
        if (hasSel && !isId && canEdit) actions.Add("✏️ Edit Cell");
        if (_undoHistory.Count > 0)
        {
            var last = _undoHistory[^1];
            actions.Add($"↩ Undo ({last.ColumnName} row {last.RowIndex + 1})");
        }

        Page? page = FindParentPage();
        if (page == null) return;

        string result = await page.DisplayActionSheet("Actions", "Cancel", null, actions.ToArray());

        if (result == "📋 Copy to Clipboard")
        { await Clipboard.Default.SetTextAsync(_displayBox.Text ?? ""); if (_displayHeader != null) _displayHeader.Text += " — copied!"; }
        else if (result == "📥 Paste from Clipboard") await PasteAsync();
        else if (result == "✏️ Edit Cell") { if (hasSel && !isId) EnterEditMode(); }
        else if (result != null && result.StartsWith("↩ Undo")) await UndoAsync();
    }

    private async Task PasteAsync()
    {
        if (_selectedRow < 0 || _selectedCol < 0 || OnUpdateCell == null) return;
        string? clip = await Clipboard.Default.GetTextAsync();
        if (string.IsNullOrEmpty(clip)) return;

        string? idValue = GetIdForRow(_selectedRow);
        if (idValue == null) return;

        string oldValue = (_allFullRows.Count > _selectedRow && _allFullRows[_selectedRow].Count > _selectedCol)
            ? _allFullRows[_selectedRow][_selectedCol] : "";

        try
        {
            bool ok = await OnUpdateCell(idValue, _selectedColumnName, clip);
            if (ok)
            {
                PushUndo(idValue, _selectedColumnName, oldValue, clip, _selectedRow, _selectedCol);
                UpdateCellDisplay(_selectedRow, _selectedCol, clip);
                if (_displayBox != null) _displayBox.Text = clip;
                if (_displayHeader != null) _displayHeader.Text = $"Pasted ✓ {_selectedColumnName} in row {_selectedRow + 1}";
            }
        }
        catch (Exception ex) { if (_displayHeader != null) _displayHeader.Text = $"Paste failed: {ex.Message}"; }
    }

    private async Task UndoAsync()
    {
        if (_undoHistory.Count == 0 || OnUpdateCell == null) return;
        var last = _undoHistory[^1]; _undoHistory.RemoveAt(_undoHistory.Count - 1);

        try
        {
            bool ok = await OnUpdateCell(last.IdValue, last.ColumnName, last.OldValue);
            if (ok)
            {
                UpdateCellDisplay(last.RowIndex, last.ColIndex, last.OldValue);
                if (_selectedRow == last.RowIndex && _selectedCol == last.ColIndex && _displayBox != null)
                    _displayBox.Text = last.OldValue;
                if (_displayHeader != null) _displayHeader.Text = $"Undone ✓ {last.ColumnName} row {last.RowIndex + 1}";
            }
        }
        catch (Exception ex) { if (_displayHeader != null) _displayHeader.Text = $"Undo failed: {ex.Message}"; }
    }

    // ===================== HELPERS =====================

    private string? GetIdForRow(int absRow)
    {
        int idx = _headers.FindIndex(h => h.Equals(IdColumnName, StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || _allFullRows.Count <= absRow || _allFullRows[absRow].Count <= idx) return null;
        return _allFullRows[absRow][idx];
    }

    private Page? FindParentPage()
    {
        Element? el = GridView as Element;
        while (el != null) { if (el is Page p) return p; el = el.Parent; }
        el = ToolbarView as Element;
        while (el != null) { if (el is Page p) return p; el = el.Parent; }
        if (Application.Current?.MainPage is Shell shell) return shell.CurrentPage;
        return Application.Current?.MainPage;
    }

    private void ScrollToTop()
    {
        // Walk up from GridView to find the parent ScrollView and scroll to top
        Element? el = _gridWrapper;
        while (el != null)
        {
            if (el is ScrollView sv)
            {
                sv.ScrollToAsync(0, 0, false);
                return;
            }
            el = el.Parent;
        }
    }

    // ===================== BUILDER =====================

    public static DataGridViewBuilder Create(List<string> headers, List<List<string>> rows)
        => new DataGridViewBuilder(headers, rows);

    public static DataGridViewBuilder Create(List<List<string>> rows)
    {
        int c = rows.Count > 0 ? rows.Max(r => r.Count) : 0;
        return new DataGridViewBuilder(Enumerable.Range(1, c).Select(i => $"Col {i}").ToList(), rows);
    }
}

public class DataGridViewBuilder
{
    private readonly DataGridView _grid;
    private readonly List<string> _headers;
    private readonly List<List<string>> _rows;
    private List<List<string>>? _fullRows;

    public DataGridViewBuilder(List<string> headers, List<List<string>> rows) { _grid = new DataGridView(); _headers = headers; _rows = rows; }

    public DataGridViewBuilder WithTitle(string t) { _grid.Title = t; return this; }
    public DataGridViewBuilder WithHeaderStyle(Color bg, Color tx) { _grid.HeaderBackgroundColor = bg; _grid.HeaderTextColor = tx; return this; }
    public DataGridViewBuilder WithCellStyle(Color bg, Color tx) { _grid.CellBackgroundColor = bg; _grid.CellTextColor = tx; return this; }
    public DataGridViewBuilder WithAlternateRowColor(Color c) { _grid.AlternateRowColor = c; _grid.UseAlternatingRowColors = true; return this; }
    public DataGridViewBuilder WithoutAlternatingRows() { _grid.UseAlternatingRowColors = false; return this; }
    public DataGridViewBuilder WithBorderColor(Color c) { _grid.BorderColor = c; return this; }
    public DataGridViewBuilder WithFontSize(double cell, double hdr) { _grid.FontSize = cell; _grid.HeaderFontSize = hdr; return this; }
    public DataGridViewBuilder WithCellPadding(double p) { _grid.CellPadding = p; return this; }
    public DataGridViewBuilder WithColumnWidths(double min, double max) { _grid.MinColumnWidth = min; _grid.MaxColumnWidth = max; return this; }
    public DataGridViewBuilder HideHeaders() { _grid.ShowHeaders = false; return this; }
    public DataGridViewBuilder WithFullRows(List<List<string>> f) { _fullRows = f; return this; }
    public DataGridViewBuilder OnCellTapped(EventHandler<CellTappedEventArgs> h) { _grid.CellTapped += h; return this; }
    public DataGridViewBuilder WithUpdateCallback(CellUpdateDelegate cb) { _grid.OnUpdateCell = cb; return this; }
    public DataGridViewBuilder WithIdColumn(string id) { _grid.IdColumnName = id; return this; }
    public DataGridViewBuilder WithPageSize(int size) { _grid.PageSize = size; return this; }

    public DataGridView Build()
    {
        if (_fullRows != null) _grid.SetData(_headers, _rows, _fullRows);
        else _grid.SetData(_headers, _rows);
        return _grid;
    }
}

public static class DataGridHelper
{
    public static DataGridView CreateDataGrid(List<string> headers, List<List<string>> rows, string? title = null)
    { var g = new DataGridView(); if (title != null) g.Title = title; g.SetData(headers, rows); return g; }

    public static DataGridView CreateDataGrid(List<Dictionary<string, object?>> data, string? title = null)
    {
        if (data == null || data.Count == 0) { var g = new DataGridView(); g.SetData(new(), new()); return g; }
        var h = data[0].Keys.ToList();
        return CreateDataGrid(h, data.Select(d => h.Select(k => d.ContainsKey(k) ? (d[k]?.ToString() ?? "NULL") : "").ToList()).ToList(), title);
    }

    public static DataGridView CreateDataGrid<T>(List<T> items, string? title = null) where T : class
    {
        if (items == null || items.Count == 0) { var g = new DataGridView(); g.SetData(new(), new()); return g; }
        var props = typeof(T).GetProperties();
        return CreateDataGrid(props.Select(p => p.Name).ToList(),
            items.Select(i => props.Select(p => p.GetValue(i)?.ToString() ?? "NULL").ToList()).ToList(), title);
    }
}

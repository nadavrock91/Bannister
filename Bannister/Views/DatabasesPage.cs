using Bannister.Services;
using SQLite;

namespace Bannister.Views;

/// <summary>
/// Database browser. Two dropdowns + DataGridView with toolbar fixed outside scroll.
/// </summary>
public class DatabasesPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly AuthService _auth;

    private Picker _dbPicker;
    private Picker _tablePicker;
    private Label _lblStatus;
    private VerticalStackLayout _toolbarContainer; // Fixed: display box + ⋯ menu
    private VerticalStackLayout _gridContainer;    // Inside scroll: the grid
    private Grid _paginationBar;

    private List<DatabaseFileEntry> _dbFiles = new();
    private SQLiteAsyncConnection? _currentConn;
    private string _currentDbPath = "";
    private string _currentTableName = "";
    private List<string> _columns = new();
    private List<List<string>> _fullRowValues = new();
    private int _offset = 0;
    private int _totalCount = 0;
    private const int PageSize = 100;

    public DatabasesPage(DatabaseService db, AuthService auth)
    {
        _db = db;
        _auth = auth;
        Title = "Databases";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDatabaseFilesAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        CloseExternalConnection();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },    // pickers
                new RowDefinition { Height = GridLength.Auto },    // toolbar (fixed)
                new RowDefinition { Height = GridLength.Star },    // grid (scrollable)
                new RowDefinition { Height = GridLength.Auto }     // pagination
            }
        };

        // ====== Row 0: Pickers ======
        var pickerRow = new Grid
        {
            Padding = new Thickness(12, 10, 12, 4),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 10
        };

        var dbStack = new VerticalStackLayout { Spacing = 2 };
        dbStack.Children.Add(new Label { Text = "Database", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#555") });
        _dbPicker = new Picker { Title = "Select database...", FontSize = 13, BackgroundColor = Colors.White };
        _dbPicker.SelectedIndexChanged += OnDatabaseSelected;
        dbStack.Children.Add(_dbPicker);
        pickerRow.Add(dbStack, 0, 0);

        var tableStack = new VerticalStackLayout { Spacing = 2 };
        tableStack.Children.Add(new Label { Text = "Table", FontSize = 11, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#555") });
        _tablePicker = new Picker { Title = "Select table...", FontSize = 13, BackgroundColor = Colors.White, IsEnabled = false };
        _tablePicker.SelectedIndexChanged += OnTableSelected;
        tableStack.Children.Add(_tablePicker);
        pickerRow.Add(tableStack, 1, 0);

        _lblStatus = new Label { Text = "", FontSize = 11, TextColor = Color.FromArgb("#888"), VerticalOptions = LayoutOptions.End, Margin = new Thickness(0, 0, 0, 6) };
        pickerRow.Add(_lblStatus, 2, 0);

        mainGrid.Add(pickerRow, 0, 0);

        // ====== Row 1: Toolbar container (FIXED, not scrollable) ======
        _toolbarContainer = new VerticalStackLayout
        {
            Padding = new Thickness(12, 2, 12, 4)
        };
        mainGrid.Add(_toolbarContainer, 0, 1);

        // ====== Row 2: Grid container (scrollable) ======
        var scrollView = new ScrollView { Orientation = ScrollOrientation.Both };
        _gridContainer = new VerticalStackLayout { Padding = new Thickness(12, 4), Spacing = 4 };
        scrollView.Content = _gridContainer;
        mainGrid.Add(scrollView, 0, 2);

        // ====== Row 3: Pagination ======
        _paginationBar = new Grid
        {
            Padding = new Thickness(12, 4, 12, 8),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 8,
            IsVisible = false
        };

        var btnPrev = new Button { Text = "← Previous", BackgroundColor = Color.FromArgb("#5B63EE"), TextColor = Colors.White, CornerRadius = 6, FontSize = 13, HeightRequest = 34 };
        btnPrev.Clicked += async (s, e) => { if (_offset >= PageSize) { _offset -= PageSize; await LoadRowsAsync(); } };
        _paginationBar.Add(btnPrev, 0, 0);

        var btnNext = new Button { Text = "Next →", BackgroundColor = Color.FromArgb("#5B63EE"), TextColor = Colors.White, CornerRadius = 6, FontSize = 13, HeightRequest = 34 };
        btnNext.Clicked += async (s, e) => { if (_offset + PageSize < _totalCount) { _offset += PageSize; await LoadRowsAsync(); } };
        _paginationBar.Add(btnNext, 1, 0);

        mainGrid.Add(_paginationBar, 0, 3);

        Content = mainGrid;
    }

    // ===================== DATABASE PICKER =====================

    private async Task LoadDatabaseFilesAsync()
    {
        _dbFiles.Clear(); _dbPicker.Items.Clear();

        string path = DatabaseService.DatabasePath;
        if (System.IO.File.Exists(path))
        {
            var fi = new System.IO.FileInfo(path);
            string sz = fi.Length < 1024 * 1024 ? $"{fi.Length / 1024.0:F0} KB" : $"{fi.Length / (1024.0 * 1024.0):F1} MB";
            _dbFiles.Add(new DatabaseFileEntry { DisplayName = $"bannister.db ({sz})", FilePath = path, IsMain = true });
        }

        foreach (var entry in _dbFiles) _dbPicker.Items.Add(entry.DisplayName);
    }

    private async void OnDatabaseSelected(object? sender, EventArgs e)
    {
        int idx = _dbPicker.SelectedIndex;
        if (idx < 0 || idx >= _dbFiles.Count) return;

        var sel = _dbFiles[idx];
        _tablePicker.Items.Clear(); _tablePicker.SelectedIndex = -1; _tablePicker.IsEnabled = false;
        _toolbarContainer.Children.Clear(); _gridContainer.Children.Clear();
        _paginationBar.IsVisible = false;
        _columns.Clear(); _fullRowValues.Clear(); _currentTableName = ""; _offset = 0;

        CloseExternalConnection();

        try
        {
            if (sel.IsMain) { _currentConn = await _db.GetConnectionAsync(); _currentDbPath = sel.FilePath; }
            else
            {
                _currentConn = new SQLiteAsyncConnection(sel.FilePath, SQLiteOpenFlags.ReadOnly | SQLiteOpenFlags.FullMutex, storeDateTimeAsTicks: false);
                _currentDbPath = sel.FilePath;
            }

            var tables = await _currentConn.QueryAsync<TableInfo>("SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name");
            foreach (var t in tables)
            {
                int cnt = await _currentConn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [{t.Name}]");
                _tablePicker.Items.Add($"{t.Name} ({cnt:N0} rows)");
            }
            _tablePicker.IsEnabled = tables.Count > 0;
            _lblStatus.Text = $"{tables.Count} tables";
        }
        catch (Exception ex) { _lblStatus.Text = $"Error: {ex.Message}"; }
    }

    // ===================== TABLE PICKER =====================

    private async void OnTableSelected(object? sender, EventArgs e)
    {
        int idx = _tablePicker.SelectedIndex;
        if (idx < 0 || _currentConn == null) return;

        _currentTableName = ExtractTableName(_tablePicker.Items[idx]);
        _toolbarContainer.Children.Clear(); _gridContainer.Children.Clear();
        _fullRowValues.Clear(); _offset = 0;

        try
        {
            var colInfo = await _currentConn.QueryAsync<ColumnInfo>($"PRAGMA table_info([{_currentTableName}])");
            _columns = colInfo.Select(c => c.Name).ToList();
        }
        catch (Exception ex) { _lblStatus.Text = $"Error: {ex.Message}"; return; }

        await LoadRowsAsync();
    }

    // ===================== LOAD ROWS =====================

    private async Task LoadRowsAsync()
    {
        if (_currentConn == null || _columns.Count == 0 || string.IsNullOrEmpty(_currentTableName)) return;

        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();
        _gridContainer.Children.Add(new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#5B63EE") });

        try
        {
            _totalCount = await _currentConn.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM [{_currentTableName}]");
            int startRow = _offset + 1, endRow = Math.Min(_offset + PageSize, _totalCount);
            _lblStatus.Text = $"{_currentTableName} • {startRow}-{endRow} of {_totalCount:N0} • {_columns.Count} cols";

            if (_totalCount == 0)
            {
                _gridContainer.Children.Clear();
                _gridContainer.Children.Add(new Label { Text = "Table is empty", TextColor = Color.FromArgb("#666"), HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 20) });
                _paginationBar.IsVisible = false;
                return;
            }

            var displayRows = new List<List<string>>();
            _fullRowValues = new List<List<string>>();

            const string SEP = "║";
            var castCols = _columns.Select(c => $"COALESCE(CAST([{c}] AS TEXT), 'NULL')").ToList();
            var query = $"SELECT {string.Join($" || '{SEP}' || ", castCols)} AS RowData FROM [{_currentTableName}] LIMIT {PageSize} OFFSET {_offset}";

            foreach (var row in await _currentConn.QueryAsync<RowDataResult>(query))
            {
                if (row?.RowData == null) continue;
                var cells = row.RowData.Split(SEP);
                var dRow = new List<string>(); var fRow = new List<string>();
                for (int c = 0; c < _columns.Count; c++)
                {
                    var v = c < cells.Length ? cells[c] : "";
                    fRow.Add(v);
                    dRow.Add(v.Length > 50 ? v.Substring(0, 47) + "..." : v);
                }
                displayRows.Add(dRow); _fullRowValues.Add(fRow);
            }

            // Capture for closure
            var conn = _currentConn;
            var tbl = _currentTableName;

            var dataGrid = DataGridView.Create(_columns, displayRows)
                .WithHeaderStyle(Color.FromArgb("#5B63EE"), Colors.White)
                .WithAlternateRowColor(Color.FromArgb("#F8F9FF"))
                .WithColumnWidths(60, 200)
                .WithCellPadding(6)
                .WithFontSize(12, 12)
                .WithFullRows(_fullRowValues)
                .WithIdColumn("Id")
                .WithUpdateCallback(async (idValue, columnName, newValue) =>
                {
                    if (newValue == "NULL")
                        await conn.ExecuteAsync($"UPDATE [{tbl}] SET [{columnName}] = NULL WHERE [Id] = ?", idValue);
                    else
                        await conn.ExecuteAsync($"UPDATE [{tbl}] SET [{columnName}] = ? WHERE [Id] = ?", newValue, idValue);
                    return true;
                })
                .Build();

            // Place toolbar (fixed) and grid (scrollable) separately
            _toolbarContainer.Children.Clear();
            _toolbarContainer.Children.Add(dataGrid.ToolbarView);

            _gridContainer.Children.Clear();
            _gridContainer.Children.Add(dataGrid.GridView);

            _paginationBar.IsVisible = _totalCount > PageSize;
        }
        catch (Exception ex)
        {
            _gridContainer.Children.Clear();
            _gridContainer.Children.Add(new Label { Text = $"Error: {ex.Message}", TextColor = Colors.Red });
        }
    }

    // ===================== HELPERS =====================

    private static string ExtractTableName(string item) { int i = item.LastIndexOf(" ("); return i > 0 ? item.Substring(0, i) : item; }

    private void CloseExternalConnection()
    {
        if (_currentConn != null && _currentDbPath != DatabaseService.DatabasePath)
            try { _currentConn.CloseAsync().ConfigureAwait(false); } catch { }
        _currentConn = null; _currentDbPath = "";
    }

    private class DatabaseFileEntry { public string DisplayName { get; set; } = ""; public string FilePath { get; set; } = ""; public bool IsMain { get; set; } }
    private class TableInfo { public string Name { get; set; } = ""; }
    private class ColumnInfo { public string Name { get; set; } = ""; public string Type { get; set; } = ""; public int Pk { get; set; } = 0; }
    private class RowDataResult { public string RowData { get; set; } = ""; }
}

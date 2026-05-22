using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class DesignationsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DesignationService _designations;

    private Picker _systemPicker;
    private Picker _statusPicker;
    private VerticalStackLayout _toolbarContainer;
    private VerticalStackLayout _gridContainer;
    private Label _statusLabel;

    private List<DesignationSystem> _systems = new();
    private List<Designation> _currentDesignations = new();
    private DesignationSystem? _selectedSystem;
    private Designation? _selectedDesignation;

    public DesignationsPage(AuthService auth, DesignationService designations)
    {
        _auth = auth;
        _designations = designations;
        Title = "Designations";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadSystemsAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star }
            }
        };

        var header = new VerticalStackLayout
        {
            Padding = new Thickness(12, 10, 12, 6),
            Spacing = 8
        };

        header.Children.Add(new Label
        {
            Text = "Designations",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#283593")
        });

        var systemRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        systemRow.Add(new Label
        {
            Text = "System",
            FontSize = 13,
            TextColor = Color.FromArgb("#555"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        _systemPicker = new Picker { Title = "Choose system", VerticalOptions = LayoutOptions.Center };
        _systemPicker.SelectedIndexChanged += async (s, e) => await OnSystemChangedAsync();
        systemRow.Add(_systemPicker, 1, 0);

        var addSystemBtn = CreateActionButton("+ Add System", Color.FromArgb("#2E7D32"), Colors.White);
        addSystemBtn.IsVisible = !_designations.IsReadOnly;
        addSystemBtn.Clicked += async (s, e) => await AddSystemAsync();
        systemRow.Add(addSystemBtn, 2, 0);

        var removeSystemBtn = CreateActionButton("Remove System", Color.FromArgb("#FFEBEE"), Color.FromArgb("#C62828"));
        removeSystemBtn.IsVisible = !_designations.IsReadOnly;
        removeSystemBtn.Clicked += async (s, e) => await RemoveSystemAsync();
        systemRow.Add(removeSystemBtn, 3, 0);

        header.Children.Add(systemRow);

        var filterRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 8
        };

        filterRow.Add(new Label
        {
            Text = "Status",
            FontSize = 13,
            TextColor = Color.FromArgb("#555"),
            VerticalOptions = LayoutOptions.Center
        }, 0, 0);

        _statusPicker = new Picker { WidthRequest = 160, VerticalOptions = LayoutOptions.Center };
        foreach (var status in new[] { "All", "Pending", "Completed", "Archived", "Failed" })
            _statusPicker.Items.Add(status);
        _statusPicker.SelectedIndex = 0;
        _statusPicker.SelectedIndexChanged += async (s, e) => await LoadDesignationsAsync();
        filterRow.Add(_statusPicker, 1, 0);

        _statusLabel = new Label
        {
            Text = "Add a system to begin.",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            VerticalOptions = LayoutOptions.Center
        };
        filterRow.Add(_statusLabel, 2, 0);

        var addDesignationBtn = CreateActionButton("+ Add Designation", Color.FromArgb("#2E7D32"), Colors.White);
        addDesignationBtn.IsVisible = !_designations.IsReadOnly;
        addDesignationBtn.Clicked += async (s, e) => await AddDesignationAsync();
        filterRow.Add(addDesignationBtn, 3, 0);

        var deleteDesignationBtn = CreateActionButton("Delete Designation", Color.FromArgb("#FFEBEE"), Color.FromArgb("#C62828"));
        deleteDesignationBtn.IsVisible = !_designations.IsReadOnly;
        deleteDesignationBtn.Clicked += async (s, e) => await DeleteSelectedDesignationAsync();
        filterRow.Add(deleteDesignationBtn, 4, 0);

        header.Children.Add(filterRow);
        mainGrid.Add(header, 0, 0);

        _toolbarContainer = new VerticalStackLayout
        {
            Padding = new Thickness(12, 0, 12, 4),
            Spacing = 4
        };
        mainGrid.Add(_toolbarContainer, 0, 1);

        var scrollView = new ScrollView { Orientation = ScrollOrientation.Both };
        _gridContainer = new VerticalStackLayout { Padding = new Thickness(12, 4), Spacing = 4 };
        scrollView.Content = _gridContainer;
        mainGrid.Add(scrollView, 0, 2);

        Content = mainGrid;
    }

    private static Button CreateActionButton(string text, Color background, Color textColor)
    {
        return new Button
        {
            Text = text,
            FontSize = 13,
            HeightRequest = 38,
            BackgroundColor = background,
            TextColor = textColor,
            CornerRadius = 6,
            Padding = new Thickness(14, 0)
        };
    }

    private async Task LoadSystemsAsync(int? selectedSystemId = null)
    {
        _systems = await _designations.GetSystemsAsync(_auth.CurrentUsername);

        _systemPicker.Items.Clear();
        foreach (var system in _systems)
            _systemPicker.Items.Add(system.Name);

        if (_systems.Count == 0)
        {
            _selectedSystem = null;
            _selectedDesignation = null;
            _statusLabel.Text = "No systems yet. Add one first.";
            BuildDataGrid();
            return;
        }

        var index = selectedSystemId.HasValue
            ? _systems.FindIndex(s => s.Id == selectedSystemId.Value)
            : 0;
        _systemPicker.SelectedIndex = index >= 0 ? index : 0;
        await OnSystemChangedAsync();
    }

    private async Task OnSystemChangedAsync()
    {
        if (_systemPicker.SelectedIndex < 0 || _systemPicker.SelectedIndex >= _systems.Count)
        {
            _selectedSystem = null;
            await LoadDesignationsAsync();
            return;
        }

        _selectedSystem = _systems[_systemPicker.SelectedIndex];
        await LoadDesignationsAsync();
    }

    private async Task LoadDesignationsAsync()
    {
        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();
        _selectedDesignation = null;

        if (_selectedSystem == null)
        {
            _currentDesignations.Clear();
            _statusLabel.Text = "No system selected.";
            BuildDataGrid();
            return;
        }

        _gridContainer.Children.Add(new ActivityIndicator { IsRunning = true, Color = Color.FromArgb("#283593") });
        _currentDesignations = await _designations.GetDesignationsAsync(
            _auth.CurrentUsername,
            _selectedSystem.Id,
            _statusPicker.SelectedItem?.ToString() ?? "All");

        _statusLabel.Text = $"{_selectedSystem.Name}: {_currentDesignations.Count} shown";
        BuildDataGrid();
    }

    private void BuildDataGrid()
    {
        _toolbarContainer.Children.Clear();
        _gridContainer.Children.Clear();

        if (_selectedSystem == null)
        {
            _gridContainer.Children.Add(new Label
            {
                Text = "Add a system before creating designations.",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20)
            });
            return;
        }

        if (_currentDesignations.Count == 0)
        {
            _gridContainer.Children.Add(new Label
            {
                Text = "No designations match this filter.",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                Margin = new Thickness(20)
            });
            return;
        }

        var headers = new List<string> { "Id", "Description", "Status", "SpecifiedDate", "StartDate", "EndDate", "StartHour", "EndHour", "Notes", "CreatedAt" };
        var displayRows = new List<List<string>>();
        var fullRows = new List<List<string>>();

        foreach (var designation in _currentDesignations)
        {
            var row = BuildDesignationRow(designation);
            fullRows.Add(row);
            displayRows.Add(row.Select(v => v.Length > 60 ? v.Substring(0, 57) + "..." : v).ToList());
        }

        var dataGrid = DataGridView.Create(headers, displayRows)
            .WithHeaderStyle(Color.FromArgb("#283593"), Colors.White)
            .WithAlternateRowColor(Color.FromArgb("#E8EAF6"))
            .WithColumnWidths(70, 240)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithFullRows(fullRows)
            .WithIdColumn("Id")
            .OnCellTapped((s, e) =>
            {
                if (e.RowIndex >= 0 && e.RowIndex < _currentDesignations.Count)
                    _selectedDesignation = _currentDesignations[e.RowIndex];
            })
            .WithUpdateCallback(UpdateDesignationGridCellAsync)
            .Build();

        _toolbarContainer.Children.Add(dataGrid.ToolbarView);
        _gridContainer.Children.Add(dataGrid.GridView);
    }

    private static List<string> BuildDesignationRow(Designation designation)
    {
        return new List<string>
        {
            designation.Id.ToString(CultureInfo.InvariantCulture),
            designation.Description,
            designation.Status,
            FormatDate(designation.SpecifiedDate),
            FormatDate(designation.StartDate),
            FormatDate(designation.EndDate),
            designation.StartHour ?? "",
            designation.EndHour ?? "",
            designation.Notes ?? "",
            designation.CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm")
        };
    }

    private async Task<bool> UpdateDesignationGridCellAsync(string idValue, string columnName, string newValue)
    {
        if (_designations.IsReadOnly)
            return false;

        bool updated = await _designations.UpdateDesignationCellAsync(_auth.CurrentUsername, idValue, columnName, newValue);
        if (!updated)
            return false;

        await LoadDesignationsAsync();
        _selectedDesignation = _currentDesignations.FirstOrDefault(d => d.Id.ToString(CultureInfo.InvariantCulture) == idValue);
        return true;
    }

    private async Task AddSystemAsync()
    {
        string? name = await DisplayPromptAsync("New System", "System name:", "Add", "Cancel", placeholder: "Diet, Running...");
        if (string.IsNullOrWhiteSpace(name))
            return;

        var system = await _designations.AddSystemAsync(_auth.CurrentUsername, name);
        await LoadSystemsAsync(system.Id);
    }

    private async Task RemoveSystemAsync()
    {
        if (_selectedSystem == null)
        {
            await DisplayAlert("No system selected", "Select a system first.", "OK");
            return;
        }

        if (!await DisplayAlert("Remove system?", $"Remove \"{_selectedSystem.Name}\" and all of its designations?", "Remove", "Cancel"))
            return;

        await _designations.DeleteSystemAsync(_selectedSystem.Id);
        await LoadSystemsAsync();
    }

    private async Task AddDesignationAsync()
    {
        if (_selectedSystem == null)
        {
            await DisplayAlert("No system selected", "Add or select a system first.", "OK");
            return;
        }

        string? description = await DisplayPromptAsync("New Designation", "Specification:", "Next", "Cancel", placeholder: "no more than 2000 calories");
        if (string.IsNullOrWhiteSpace(description))
            return;

        DateTime today = DateTime.Today;
        string? specifiedDateText = await DisplayPromptAsync("Specified Date", "Date this applies to:", "Next", "Cancel", initialValue: FormatDate(today));
        if (!DesignationService.TryParseDate(specifiedDateText, out DateTime specifiedDate))
        {
            await DisplayAlert("Invalid date", "Enter a valid specified date.", "OK");
            return;
        }

        string? startDateText = await DisplayPromptAsync("Start Date", "Date this locks/becomes active:", "Next", "Cancel", initialValue: FormatDate(today));
        if (!DesignationService.TryParseDate(startDateText, out DateTime startDate))
        {
            await DisplayAlert("Invalid date", "Enter a valid start date.", "OK");
            return;
        }

        string? endDateText = await DisplayPromptAsync("End Date", "End date:", "Next", "Cancel", initialValue: FormatDate(specifiedDate));
        if (!DesignationService.TryParseDate(endDateText, out DateTime endDate))
        {
            await DisplayAlert("Invalid date", "Enter a valid end date.", "OK");
            return;
        }

        string? startHourText = await DisplayPromptAsync("Start Hour", "Start time (HH:mm, optional):", "Next", "Skip", initialValue: "");
        if (!DesignationService.TryNormalizeHour(startHourText ?? "", out string startHour))
        {
            await DisplayAlert("Invalid time", "Enter a valid start time like 08:30.", "OK");
            return;
        }

        string? endHourText = await DisplayPromptAsync("End Hour", "End time (HH:mm, optional):", "Next", "Skip", initialValue: "");
        if (!DesignationService.TryNormalizeHour(endHourText ?? "", out string endHour))
        {
            await DisplayAlert("Invalid time", "Enter a valid end time like 18:00.", "OK");
            return;
        }

        string? notes = await DisplayPromptAsync("Notes", "Optional notes:", "Save", "Skip", initialValue: "");

        await _designations.AddDesignationAsync(
            _auth.CurrentUsername,
            _selectedSystem.Id,
            description,
            specifiedDate,
            startDate,
            endDate,
            startHour,
            endHour,
            notes ?? "");

        await LoadDesignationsAsync();
    }

    private async Task DeleteSelectedDesignationAsync()
    {
        if (_selectedDesignation == null)
        {
            await DisplayAlert("No row selected", "Select a row in the grid first.", "OK");
            return;
        }

        if (!await DisplayAlert("Delete designation?", $"Delete \"{_selectedDesignation.Description}\"?", "Delete", "Cancel"))
            return;

        await _designations.DeleteDesignationAsync(_selectedDesignation.Id);
        await LoadDesignationsAsync();
    }

    private static string FormatDate(DateTime date)
    {
        return date.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}

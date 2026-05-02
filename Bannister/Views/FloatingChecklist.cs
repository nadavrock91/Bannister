using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// A floating process checklist button + popup overlay.
/// Drop into any page to give quick access to SubActivity steps
/// without navigating away.
/// 
/// Usage:
///   var checklist = new FloatingChecklist(authService, subActivityService);
///   checklist.AttachTo(this); // call in constructor after BuildUI
/// </summary>
public class FloatingChecklist
{
    private readonly AuthService _auth;
    private readonly SubActivityService _subService;

    private Button _floatingBtn;
    private Grid _popupOverlay;
    private VerticalStackLayout _contentStack;
    private Picker _processPicker;
    private VerticalStackLayout _stepsStack;
    private Label _progressLabel;
    private SubActivity? _selectedProcess;
    private List<SubActivity> _processes = new();
    private ContentPage? _page;

    public FloatingChecklist(AuthService auth, SubActivityService subService)
    {
        _auth = auth;
        _subService = subService;
    }

    /// <summary>
    /// Attaches the floating button to a page. Call after BuildUI / setting Content.
    /// </summary>
    public void AttachTo(ContentPage page)
    {
        _page = page;

        // Wrap existing content in a Grid so we can overlay the button
        var existingContent = page.Content;
        var wrapper = new Grid();
        wrapper.Children.Add(existingContent);

        // Floating button (bottom-right corner)
        _floatingBtn = new Button
        {
            Text = "📋",
            FontSize = 20,
            WidthRequest = 50,
            HeightRequest = 50,
            CornerRadius = 25,
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 20, 20),
            Shadow = new Shadow { Brush = Colors.Black, Offset = new Point(2, 2), Radius = 6, Opacity = 0.3f },
            ZIndex = 100
        };
        _floatingBtn.Clicked += async (s, e) => await TogglePopupAsync();
        wrapper.Children.Add(_floatingBtn);

        // Build popup (hidden initially)
        BuildPopup();
        _popupOverlay.IsVisible = false;
        wrapper.Children.Add(_popupOverlay);

        page.Content = wrapper;
    }

    private void BuildPopup()
    {
        _popupOverlay = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            ZIndex = 99
        };

        // Tap background to close
        var bg = new BoxView { BackgroundColor = Color.FromArgb("#40000000") };
        var bgTap = new TapGestureRecognizer();
        bgTap.Tapped += (s, e) => _popupOverlay.IsVisible = false;
        bg.GestureRecognizers.Add(bgTap);
        _popupOverlay.Children.Add(bg);

        // Card (bottom-right, above the button)
        var card = new Frame
        {
            BackgroundColor = Colors.White,
            CornerRadius = 12,
            Padding = 16,
            HasShadow = true,
            WidthRequest = 350,
            MaximumHeightRequest = 450,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 20, 80)
        };

        _contentStack = new VerticalStackLayout { Spacing = 10 };

        // Header
        var headerRow = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8
        };
        headerRow.Add(new Label { Text = "📋 Process Checklist", FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#5B63EE"), VerticalOptions = LayoutOptions.Center }, 0, 0);
        var closeBtn = new Button { Text = "✕", FontSize = 14, WidthRequest = 32, HeightRequest = 32, BackgroundColor = Color.FromArgb("#F5F5F5"), TextColor = Color.FromArgb("#666"), CornerRadius = 16, Padding = 0 };
        closeBtn.Clicked += (s, e) => _popupOverlay.IsVisible = false;
        headerRow.Add(closeBtn, 1, 0);
        _contentStack.Children.Add(headerRow);

        // Process picker
        _processPicker = new Picker { Title = "Select process...", BackgroundColor = Color.FromArgb("#F5F5F5"), FontSize = 13 };
        _processPicker.SelectedIndexChanged += async (s, e) =>
        {
            if (_processPicker.SelectedIndex >= 0 && _processPicker.SelectedIndex < _processes.Count)
            {
                _selectedProcess = _processes[_processPicker.SelectedIndex];
                await RefreshStepsAsync();
            }
        };
        _contentStack.Children.Add(_processPicker);

        // Progress
        _progressLabel = new Label { Text = "", FontSize = 12, TextColor = Color.FromArgb("#888") };
        _contentStack.Children.Add(_progressLabel);

        // Steps list (scrollable)
        _stepsStack = new VerticalStackLayout { Spacing = 4 };
        var stepsScroll = new ScrollView { Content = _stepsStack, MaximumHeightRequest = 280 };
        _contentStack.Children.Add(stepsScroll);

        card.Content = _contentStack;
        _popupOverlay.Children.Add(card);
    }

    private async Task TogglePopupAsync()
    {
        if (_popupOverlay.IsVisible)
        {
            _popupOverlay.IsVisible = false;
            return;
        }

        // Load processes
        await LoadProcessesAsync();
        _popupOverlay.IsVisible = true;
    }

    private async Task LoadProcessesAsync()
    {
        _processes = await _subService.GetActiveAsync(_auth.CurrentUsername);
        _processPicker.Items.Clear();
        foreach (var p in _processes)
            _processPicker.Items.Add(p.Name);

        // Auto-select if only one, or re-select previous
        if (_selectedProcess != null)
        {
            int idx = _processes.FindIndex(p => p.Id == _selectedProcess.Id);
            if (idx >= 0)
            {
                _processPicker.SelectedIndex = idx;
                _selectedProcess = _processes[idx];
                await RefreshStepsAsync();
            }
        }
        else if (_processes.Count == 1)
        {
            _processPicker.SelectedIndex = 0;
            _selectedProcess = _processes[0];
            await RefreshStepsAsync();
        }
        else
        {
            _stepsStack.Children.Clear();
            _progressLabel.Text = _processes.Count == 0 ? "No active processes." : "Select a process above.";
        }
    }

    private async Task RefreshStepsAsync()
    {
        if (_selectedProcess == null) return;

        // Reload from DB to get latest state
        var fresh = await _subService.GetActiveAsync(_auth.CurrentUsername);
        var updated = fresh.FirstOrDefault(p => p.Id == _selectedProcess.Id);
        if (updated != null) _selectedProcess = updated;

        var steps = _subService.GetSteps(_selectedProcess);
        int done = steps.Count(s => s.Done);
        int total = steps.Count;

        _progressLabel.Text = $"{done}/{total} steps done";
        if (done == total && total > 0)
            _progressLabel.TextColor = Color.FromArgb("#2E7D32");
        else
            _progressLabel.TextColor = Color.FromArgb("#888");

        // Update floating button badge
        _floatingBtn.Text = done == total && total > 0 ? "✅" : $"📋";

        _stepsStack.Children.Clear();

        for (int i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            int idx = i;

            var row = new Grid
            {
                ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) },
                ColumnSpacing = 8,
                Padding = new Thickness(4, 6)
            };

            var cb = new CheckBox
            {
                IsChecked = step.Done,
                Color = Color.FromArgb("#5B63EE")
            };
            cb.CheckedChanged += async (s, e) =>
            {
                await _subService.ToggleStepAsync(_selectedProcess, idx);
                await RefreshStepsAsync();
            };
            row.Add(cb, 0, 0);

            var lbl = new Label
            {
                Text = step.Name,
                FontSize = 13,
                TextColor = step.Done ? Color.FromArgb("#999") : Color.FromArgb("#333"),
                TextDecorations = step.Done ? TextDecorations.Strikethrough : TextDecorations.None,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.WordWrap
            };
            row.Add(lbl, 1, 0);

            _stepsStack.Children.Add(row);
        }

        // Show pending steps count if any
        var pending = _subService.GetPendingSteps(_selectedProcess);
        if (pending.Count > 0)
        {
            _stepsStack.Children.Add(new Label
            {
                Text = $"+ {pending.Count} pending steps",
                FontSize = 11, TextColor = Color.FromArgb("#BBBBBB"),
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(36, 4, 0, 0)
            });
        }
    }
}

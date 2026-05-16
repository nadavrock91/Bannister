using System.Text.Json;
using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class QueuedOperationsPage : ContentPage
{
    private readonly SyncService _sync;
    private readonly OperationApplierService _applier;
    private readonly DatabaseService _db;
    private readonly CollectionView _collection;
    private readonly Label _summaryLabel;
    private readonly Label _statusLabel;
    private readonly Button _applyAllButton;
    private readonly Button _refreshButton;
    private readonly ActivityIndicator _busyIndicator;

    private List<QueuedOperation> _operations = new();
    private HashSet<string> _alreadyApplied = new();
    private bool _isBusy;

    public QueuedOperationsPage(SyncService sync, OperationApplierService applier, DatabaseService db)
    {
        _sync = sync;
        _applier = applier;
        _db = db;

        Title = "Queued Operations";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _summaryLabel = new Label
        {
            Text = "Loading queued operations...",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };

        _statusLabel = new Label
        {
            Text = "",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center
        };

        _busyIndicator = new ActivityIndicator
        {
            IsVisible = false,
            IsRunning = false,
            Color = Color.FromArgb("#5B63EE"),
            WidthRequest = 24,
            HeightRequest = 24
        };

        _collection = new CollectionView
        {
            SelectionMode = SelectionMode.None,
            EmptyView = new Label
            {
                Text = "No queued operations found.",
                TextColor = Color.FromArgb("#777"),
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            },
            ItemTemplate = new DataTemplate(BuildOperationCard)
        };

        _applyAllButton = new Button
        {
            Text = "Apply All",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        _applyAllButton.Clicked += OnApplyAllClicked;

        _refreshButton = new Button
        {
            Text = "Refresh",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 48
        };
        _refreshButton.Clicked += async (_, _) => await LoadAsync();

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            },
            Padding = 20,
            RowSpacing = 14,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        _summaryLabel,
                        new HorizontalStackLayout
                        {
                            Spacing = 8,
                            Children = { _busyIndicator, _statusLabel }
                        }
                    }
                }.AssignGridRow(0),
                _collection.AssignGridRow(1),
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Star },
                        new ColumnDefinition { Width = GridLength.Star }
                    },
                    ColumnSpacing = 12,
                    Children =
                    {
                        _applyAllButton.AssignGridColumn(0),
                        _refreshButton.AssignGridColumn(1)
                    }
                }.AssignGridRow(2)
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private View BuildOperationCard()
    {
        var typeLabel = new Label
        {
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE")
        };
        typeLabel.SetBinding(Label.TextProperty, nameof(OperationRow.OperationType));

        var badge = new Label
        {
            Text = "Applied",
            FontSize = 12,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#777"),
            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.End,
            IsVisible = false
        };
        badge.SetBinding(IsVisibleProperty, nameof(OperationRow.IsApplied));

        var title = new Label
        {
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#222"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        title.SetBinding(Label.TextProperty, nameof(OperationRow.Summary));

        var meta = new Label
        {
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        };
        meta.SetBinding(Label.TextProperty, nameof(OperationRow.Metadata));

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            Children =
            {
                typeLabel.AssignGridColumn(0),
                badge.AssignGridColumn(1)
            }
        };

        var frame = new Frame
        {
            Padding = 14,
            Margin = new Thickness(0, 0, 0, 10),
            CornerRadius = 8,
            HasShadow = false,
            BorderColor = Color.FromArgb("#DDD"),
            BackgroundColor = Colors.White,
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children = { header, title, meta }
            }
        };
        frame.SetBinding(OpacityProperty, nameof(OperationRow.Opacity));

        return frame;
    }

    private async Task LoadAsync()
    {
        SetBusy(true, "Downloading queued operations...");
        try
        {
            _operations = await _sync.DownloadQueueAsync();
            _alreadyApplied = new HashSet<string>();

            foreach (var op in _operations)
            {
                if (await _applier.IsAlreadyAppliedAsync(op.Uuid))
                    _alreadyApplied.Add(op.Uuid);
            }

            RefreshView();
            _statusLabel.Text = "";
        }
        catch (Exception ex)
        {
            _operations = new List<QueuedOperation>();
            _alreadyApplied = new HashSet<string>();
            RefreshView();
            _statusLabel.Text = ex.Message;
            _statusLabel.TextColor = Color.FromArgb("#C62828");
            await DisplayAlert("Queue Download Failed", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnApplyAllClicked(object? sender, EventArgs e)
    {
        if (_operations.Count == 0) return;

        SetBusy(true, "Applying queued operations...");
        try
        {
            var result = await _applier.ApplyAllAsync(_operations);
            string clearMessage = "Nothing cleared from server.";

            if (result.UuidsToClear.Count > 0)
            {
                var clear = await _sync.ClearAppliedFromServerAsync(result.UuidsToClear);
                clearMessage = clear.Success ? "Cleared from server." : clear.Message;
            }

            await DisplayAlert(
                "Queued Operations",
                $"Applied {result.AppliedCount}, skipped {result.SkippedCount} (already applied), failed {result.FailedCount}. {clearMessage}",
                "OK");

            await LoadAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = ex.Message;
            _statusLabel.TextColor = Color.FromArgb("#C62828");
            await DisplayAlert("Apply Failed", ex.Message, "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RefreshView()
    {
        var deviceCount = _operations
            .Select(o => o.SourceDeviceId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Count();

        _summaryLabel.Text = $"{_operations.Count} {_operations.Count.Plural("operation", "operations")} from {deviceCount} {deviceCount.Plural("device", "devices")}";
        _collection.ItemsSource = _operations.Select(ToRow).ToList();
        UpdateButtons();
    }

    private OperationRow ToRow(QueuedOperation op)
    {
        var title = "Queued operation";
        var category = "";

        if (op.OperationType == "idea_logged")
        {
            try
            {
                var password = _db.GetDbPassword();
                if (string.IsNullOrEmpty(password))
                    throw new InvalidOperationException("Not logged in.");

                var plaintext = QueuePayloadCrypto.DecryptPayload(op.PayloadJson, password);
                using var doc = JsonDocument.Parse(plaintext);
                title = ReadString(doc.RootElement, "title") ?? "Untitled idea";
                category = ReadString(doc.RootElement, "category") ?? "";
            }
            catch
            {
                title = "[encrypted operation - cannot preview]";
            }
        }

        var device = string.IsNullOrWhiteSpace(op.SourceDeviceId)
            ? "unknown device"
            : op.SourceDeviceId.Length <= 8 ? op.SourceDeviceId : op.SourceDeviceId[..8];

        return new OperationRow
        {
            Uuid = op.Uuid,
            OperationType = op.OperationType,
            Summary = string.IsNullOrWhiteSpace(category) ? title : $"{title} ({category})",
            Metadata = $"{op.CreatedAt.ToLocalTime():MMM d, yyyy HH:mm} • device {device}",
            IsApplied = _alreadyApplied.Contains(op.Uuid),
            Opacity = _alreadyApplied.Contains(op.Uuid) ? 0.55 : 1.0
        };
    }

    private void SetBusy(bool isBusy, string statusText = "")
    {
        _isBusy = isBusy;
        _busyIndicator.IsVisible = isBusy;
        _busyIndicator.IsRunning = isBusy;

        if (!string.IsNullOrWhiteSpace(statusText))
        {
            _statusLabel.Text = statusText;
            _statusLabel.TextColor = Color.FromArgb("#5B63EE");
        }

        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _applyAllButton.IsEnabled = !_isBusy && _operations.Count > 0;
        _refreshButton.IsEnabled = !_isBusy;
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private class OperationRow
    {
        public string Uuid { get; set; } = "";
        public string OperationType { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Metadata { get; set; } = "";
        public bool IsApplied { get; set; }
        public double Opacity { get; set; } = 1.0;
    }
}

internal static class QueuedOperationsPageLayoutExtensions
{
    public static T AssignGridRow<T>(this T view, int row) where T : BindableObject
    {
        Grid.SetRow(view, row);
        return view;
    }

    public static T AssignGridColumn<T>(this T view, int column) where T : BindableObject
    {
        Grid.SetColumn(view, column);
        return view;
    }

    public static string Plural(this int count, string singular, string plural) =>
        count == 1 ? singular : plural;
}

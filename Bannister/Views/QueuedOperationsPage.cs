using System.Text.Json;
using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class QueuedOperationsPage : ContentPage
{
    private readonly SyncService _sync;
    private readonly OperationQueueService _queue;
    private readonly OperationApplierService _applier;
    private readonly DatabaseService _db;
    private readonly AuthService _auth;
    private readonly ActivityService _activities;
    private readonly GameService _games;
    private readonly PendingActivityIdeaService _pendingIdeas;
    private readonly CollectionView _collection;
    private readonly Label _summaryLabel;
    private readonly Label _statusLabel;
    private readonly Button _applyAllButton;
    private readonly Button _refreshButton;
    private readonly ActivityIndicator _busyIndicator;
    private readonly Button _serverQueueTabButton;
    private readonly Button _localQueueTabButton;

    private List<QueuedOperation> _operations = new();
    private HashSet<string> _alreadyApplied = new();
    private bool _isBusy;
    private bool _showLocalQueue;

    public QueuedOperationsPage(
        SyncService sync,
        OperationApplierService applier,
        DatabaseService db,
        AuthService auth,
        ActivityService activities,
        GameService games,
        PendingActivityIdeaService pendingIdeas,
        OperationQueueService queue)
    {
        _sync = sync;
        _queue = queue;
        _applier = applier;
        _db = db;
        _auth = auth;
        _activities = activities;
        _games = games;
        _pendingIdeas = pendingIdeas;

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

        _serverQueueTabButton = CreateTabButton("Server Queue", true);
        _serverQueueTabButton.Clicked += async (_, _) => await SwitchQueueAsync(false);

        _localQueueTabButton = CreateTabButton("Local Queue", false);
        _localQueueTabButton.Clicked += async (_, _) => await SwitchQueueAsync(true);

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
                        new Grid
                        {
                            ColumnDefinitions =
                            {
                                new ColumnDefinition { Width = GridLength.Star },
                                new ColumnDefinition { Width = GridLength.Star }
                            },
                            ColumnSpacing = 8,
                            Children =
                            {
                                _serverQueueTabButton.AssignGridColumn(0),
                                _localQueueTabButton.AssignGridColumn(1)
                            }
                        },
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
            FontSize = 12,
            TextColor = Colors.White,
            BackgroundColor = Color.FromArgb("#777"),
            Padding = new Thickness(8, 3),
            HorizontalOptions = LayoutOptions.End,
            IsVisible = true
        };
        badge.SetBinding(Label.TextProperty, nameof(OperationRow.StatusText));

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

        var tap = new TapGestureRecognizer();
        tap.SetBinding(TapGestureRecognizer.CommandParameterProperty, ".");
        tap.Tapped += async (_, args) =>
        {
            if (args.Parameter is OperationRow row)
                await ShowOperationDetailsAsync(row.Operation, row.StatusText);
        };
        frame.GestureRecognizers.Add(tap);

        return frame;
    }

    private async Task LoadAsync()
    {
        SetBusy(true, _showLocalQueue
            ? "Loading local queued operations..."
            : "Downloading queued operations...");
        try
        {
            _alreadyApplied = new HashSet<string>();

            if (_showLocalQueue)
            {
                _operations = await _queue.GetAllAsync();
            }
            else
            {
                _operations = await _sync.DownloadQueueAsync();
                foreach (var op in _operations)
                {
                    if (await _applier.IsAlreadyAppliedAsync(op.Uuid))
                        _alreadyApplied.Add(op.Uuid);
                }
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
            await DisplayAlert(
                _showLocalQueue ? "Local Queue Load Failed" : "Queue Download Failed",
                ex.Message,
                "OK");
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
                if (!clear.Success)
                    throw new InvalidOperationException(clear.Message);
            }

            Preferences.Default.Remove("queue_prompt_snoozed_until");

            await DisplayAlert(
                "Queued Operations",
                $"Applied {result.AppliedCount}, skipped {result.SkippedCount} (already applied), failed {result.FailedCount}. {clearMessage}",
                "OK");

            await LoadAsync();

            SetBusy(false);
            if (result.AppliedCount > 0)
                await PromptToProcessPendingActivityIdeasAsync();
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

    private async Task PromptToProcessPendingActivityIdeasAsync()
    {
        var pending = await _pendingIdeas.GetPendingForUserAsync(_auth.CurrentUsername);
        if (pending.Count == 0)
            return;

        bool processNow = await DisplayAlert(
            "Process Pending Activity Ideas",
            $"You have {pending.Count} pending activity {pending.Count.Plural("idea", "ideas")} across your games. Process them now?",
            "Process now",
            "Later");

        if (processNow)
            await ProcessAllPendingAsync();
    }

    private async Task ProcessAllPendingAsync()
    {
        while (true)
        {
            var pending = await _pendingIdeas.GetPendingForUserAsync(_auth.CurrentUsername);
            if (pending.Count == 0)
                return;

            var next = pending.First();
            var action = await PendingActivityIdeaCrossGameProcessPage.ShowAsync(Navigation, next, pending.Count);

            if (action == PendingActivityIdeaProcessAction.Cancel)
                return;

            if (action == PendingActivityIdeaProcessAction.Skip)
            {
                await _pendingIdeas.MarkDismissedAsync(next.Id);
                continue;
            }

            var created = await ActivityCreationPage.CreateActivityModalAsync(
                Navigation,
                _auth,
                _activities,
                _games,
                next.Game,
                prefillName: next.ActivityName,
                prefillCategory: next.ActivityCategory);

            if (created == null)
                return;

            await _pendingIdeas.MarkConvertedAsync(next.Id);

            var remaining = await _pendingIdeas.GetPendingCountForUserAsync(_auth.CurrentUsername);
            if (remaining == 0)
                return;

            bool continueProcessing = await DisplayAlert(
                "Activity Created",
                $"Activity created. {remaining} pending {(remaining == 1 ? "idea remains" : "ideas remain")}.\n\nProcess next pending idea?",
                "Yes",
                "No");

            if (!continueProcessing)
                return;
        }
    }

    private void RefreshView()
    {
        if (_showLocalQueue)
        {
            int pendingCount = _operations.Count(operation => operation.Status == 0);
            _summaryLabel.Text = $"{_operations.Count} local {_operations.Count.Plural("operation", "operations")} ({pendingCount} pending)";
            _collection.ItemsSource = _operations.Select(ToRow).ToList();
            UpdateButtons();
            return;
        }

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
        else if (op.OperationType == "pending_activity_idea_added")
        {
            try
            {
                var password = _db.GetDbPassword();
                if (string.IsNullOrEmpty(password))
                    throw new InvalidOperationException("Not logged in.");

                var plaintext = QueuePayloadCrypto.DecryptPayload(op.PayloadJson, password);
                using var doc = JsonDocument.Parse(plaintext);
                var activityName = ReadString(doc.RootElement, "activity_name") ?? "Untitled activity";
                var game = ReadString(doc.RootElement, "game") ?? "unknown game";
                var activityCategory = ReadString(doc.RootElement, "activity_category") ?? "";
                title = $"Pending activity: {activityName} in {game}";
                category = activityCategory;
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
            Operation = op,
            Uuid = op.Uuid,
            OperationType = op.OperationType,
            Summary = string.IsNullOrWhiteSpace(category) ? title : $"{title} ({category})",
            Metadata = $"{op.CreatedAt.ToLocalTime():MMM d, yyyy HH:mm} • device {device}",
            IsApplied = _alreadyApplied.Contains(op.Uuid),
            StatusText = _showLocalQueue
                ? GetStatusText(op.Status)
                : _alreadyApplied.Contains(op.Uuid) ? "Applied" : "Pending",
            Opacity = _alreadyApplied.Contains(op.Uuid) ? 0.55 : 1.0
        };
    }

    private async Task SwitchQueueAsync(bool showLocalQueue)
    {
        if (_showLocalQueue == showLocalQueue)
            return;

        _showLocalQueue = showLocalQueue;
        UpdateTabStyles();
        await LoadAsync();
    }

    private void UpdateTabStyles()
    {
        SetTabStyle(_serverQueueTabButton, !_showLocalQueue);
        SetTabStyle(_localQueueTabButton, _showLocalQueue);
    }

    private static Button CreateTabButton(string text, bool selected)
    {
        var button = new Button
        {
            Text = text,
            CornerRadius = 8,
            HeightRequest = 42
        };
        SetTabStyle(button, selected);
        return button;
    }

    private static void SetTabStyle(Button button, bool selected)
    {
        button.BackgroundColor = selected
            ? Color.FromArgb("#5B63EE")
            : Color.FromArgb("#E8EAF6");
        button.TextColor = selected ? Colors.White : Color.FromArgb("#3949AB");
    }

    private async Task ShowOperationDetailsAsync(QueuedOperation operation, string status)
    {
        string payload;
        try
        {
            var password = _db.GetDbPassword();
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("No login password is available.");

            var plaintext = QueuePayloadCrypto.DecryptPayload(operation.PayloadJson, password);
            using var document = JsonDocument.Parse(plaintext);
            payload = FormatPayload(document.RootElement);
        }
        catch (Exception ex)
        {
            payload = $"Unable to decrypt payload: {ex.Message}";
        }

        await DisplayAlert(
            operation.OperationType,
            $"UUID: {operation.Uuid}\n" +
            $"Created at: {operation.CreatedAt.ToLocalTime():MMM d, yyyy h:mm:ss tt}\n" +
            $"Source device ID: {operation.SourceDeviceId ?? "(local device)"}\n" +
            $"Status: {status}\n\n" +
            $"Payload:\n{payload}",
            "OK");
    }

    private static string FormatPayload(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            return root.GetRawText();

        return string.Join("\n", root.EnumerateObject().Select(property =>
            $"{property.Name}: {FormatPayloadValue(property.Value)}"));
    }

    private static string FormatPayloadValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Null => "null",
        JsonValueKind.Undefined => "undefined",
        _ => value.GetRawText()
    };

    private static string GetStatusText(int status) => status switch
    {
        0 => "Pending",
        1 => "Synced",
        2 => "Applied",
        3 => "Failed",
        _ => $"Unknown ({status})"
    };

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
        _applyAllButton.IsVisible = !_showLocalQueue;
        _applyAllButton.IsEnabled = !_isBusy && !_showLocalQueue && _operations.Count > 0;
        _refreshButton.IsEnabled = !_isBusy;
        _serverQueueTabButton.IsEnabled = !_isBusy;
        _localQueueTabButton.IsEnabled = !_isBusy;
    }

    private static string? ReadString(JsonElement root, string propertyName)
    {
        return root.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private class OperationRow
    {
        public QueuedOperation Operation { get; set; } = new();
        public string Uuid { get; set; } = "";
        public string OperationType { get; set; } = "";
        public string Summary { get; set; } = "";
        public string Metadata { get; set; } = "";
        public bool IsApplied { get; set; }
        public string StatusText { get; set; } = "Pending";
        public double Opacity { get; set; } = 1.0;
    }

    private enum PendingActivityIdeaProcessAction
    {
        Create,
        Skip,
        Cancel
    }

    private sealed class PendingActivityIdeaCrossGameProcessPage : ContentPage
    {
        private readonly TaskCompletionSource<PendingActivityIdeaProcessAction> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _isCompleting;

        private PendingActivityIdeaCrossGameProcessPage(PendingActivityIdea idea, int count)
        {
            Title = "Process Pending Activity Idea";
            BackgroundColor = Color.FromRgba(0, 0, 0, 0.45);

            var createButton = new Button
            {
                Text = "Create",
                BackgroundColor = Color.FromArgb("#5B63EE"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44
            };
            createButton.Clicked += async (_, _) => await CompleteAsync(PendingActivityIdeaProcessAction.Create);

            var skipButton = new Button
            {
                Text = "Skip",
                BackgroundColor = Color.FromArgb("#FF9800"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44
            };
            skipButton.Clicked += async (_, _) => await CompleteAsync(PendingActivityIdeaProcessAction.Skip);

            var cancelButton = new Button
            {
                Text = "Cancel",
                BackgroundColor = Color.FromArgb("#777"),
                TextColor = Colors.White,
                CornerRadius = 8,
                HeightRequest = 44
            };
            cancelButton.Clicked += async (_, _) => await CompleteAsync(PendingActivityIdeaProcessAction.Cancel);

            var buttonGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Star }
                },
                ColumnSpacing = 8,
                Children =
                {
                    createButton.AssignGridColumn(0),
                    skipButton.AssignGridColumn(1),
                    cancelButton.AssignGridColumn(2)
                }
            };

            Content = new Grid
            {
                Padding = 20,
                Children =
                {
                    new Frame
                    {
                        BackgroundColor = Colors.White,
                        BorderColor = Color.FromArgb("#DDD"),
                        CornerRadius = 10,
                        HasShadow = true,
                        Padding = 18,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        MaximumWidthRequest = 460,
                        Content = new VerticalStackLayout
                        {
                            Spacing = 12,
                            Children =
                            {
                                new Label
                                {
                                    Text = "Process Pending Activity Idea",
                                    FontSize = 20,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#222")
                                },
                                new Label
                                {
                                    Text = $"Pending idea 1 of {count}",
                                    FontSize = 13,
                                    FontAttributes = FontAttributes.Bold,
                                    TextColor = Color.FromArgb("#F57C00")
                                },
                                new Label
                                {
                                    Text = $"Game: {idea.Game}\nName: {idea.ActivityName}\nCategory: {idea.ActivityCategory}",
                                    FontSize = 15,
                                    TextColor = Color.FromArgb("#333"),
                                    LineBreakMode = LineBreakMode.WordWrap
                                },
                                new Label
                                {
                                    Text = "Open Activity Creation with these pre-filled?",
                                    FontSize = 13,
                                    TextColor = Color.FromArgb("#666"),
                                    LineBreakMode = LineBreakMode.WordWrap
                                },
                                buttonGrid
                            }
                        }
                    }
                }
            };
        }

        private async Task CompleteAsync(PendingActivityIdeaProcessAction action)
        {
            if (_isCompleting)
                return;

            _isCompleting = true;
            _completion.TrySetResult(action);
            await Navigation.PopModalAsync();
        }

        protected override bool OnBackButtonPressed()
        {
            if (_isCompleting)
                return true;

            _isCompleting = true;
            _completion.TrySetResult(PendingActivityIdeaProcessAction.Cancel);
            return base.OnBackButtonPressed();
        }

        public static async Task<PendingActivityIdeaProcessAction> ShowAsync(INavigation navigation, PendingActivityIdea idea, int count)
        {
            var page = new PendingActivityIdeaCrossGameProcessPage(idea, count);
            await navigation.PushModalAsync(page);
            return await page._completion.Task;
        }
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

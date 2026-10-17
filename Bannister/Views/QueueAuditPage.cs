using System.Globalization;
using System.Text.Json;
using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class QueueAuditPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly IdeasService _ideas;
    private readonly SyncService _sync;
    private readonly DatabaseService _db;

    private readonly Label _databaseCountLabel;
    private readonly Label _pendingCountLabel;
    private readonly Label _archiveCountLabel;
    private readonly Label _matchedCountLabel;
    private readonly Label _unmatchedCountLabel;
    private readonly Label _statusLabel;
    private readonly VerticalStackLayout _unmatchedSections;
    private readonly Button _refreshButton;
    private readonly Button _uploadPendingButton;
    private readonly Button _clearArchiveButton;
    private readonly ActivityIndicator _busyIndicator;

    public QueueAuditPage(
        AuthService auth,
        IdeasService ideas,
        SyncService sync,
        DatabaseService db)
    {
        _auth = auth;
        _ideas = ideas;
        _sync = sync;
        _db = db;

        Title = "Queue Audit";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _databaseCountLabel = CreateSummaryLabel("Database: 0 ideas", "#333333");
        _pendingCountLabel = CreateSummaryLabel("Pending on server: 0", "#EF6C00");
        _archiveCountLabel = CreateSummaryLabel("Archived (applied): 0", "#2E7D32");
        _matchedCountLabel = CreateSummaryLabel("✅ Matched: 0", "#2E7D32");
        _unmatchedCountLabel = CreateSummaryLabel("⚠️ Not in database: 0", "#EF6C00");
        _statusLabel = new Label
        {
            FontSize = 13,
            TextColor = Color.FromArgb("#666666"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        _unmatchedSections = new VerticalStackLayout { Spacing = 10 };

        _refreshButton = new Button
        {
            Text = "Refresh",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        _refreshButton.Clicked += async (_, _) => await LoadAuditAsync();

        _uploadPendingButton = new Button
        {
            Text = "Upload Pending",
            BackgroundColor = Color.FromArgb("#29B6F6"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        _uploadPendingButton.Clicked += OnUploadPendingClicked;

        _clearArchiveButton = new Button
        {
            Text = "Clear Archive",
            BackgroundColor = Color.FromArgb("#C62828"),
            TextColor = Colors.White,
            CornerRadius = 8
        };
        _clearArchiveButton.Clicked += OnClearArchiveClicked;

        _busyIndicator = new ActivityIndicator
        {
            Color = Color.FromArgb("#5B63EE"),
            IsVisible = false,
            IsRunning = false
        };

        var buttonGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10
        };
        buttonGrid.Add(_refreshButton, 0, 0);
        buttonGrid.Add(_uploadPendingButton, 1, 0);
        buttonGrid.Add(_clearArchiveButton, 2, 0);

        var summaryFrame = new Frame
        {
            Padding = 18,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            Content = new VerticalStackLayout
            {
                Spacing = 8,
                Children =
                {
                    _databaseCountLabel,
                    _pendingCountLabel,
                    _archiveCountLabel,
                    _matchedCountLabel,
                    _unmatchedCountLabel
                }
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 20,
                Spacing = 14,
                Children =
                {
                    new Label
                    {
                        Text = "Queue Audit",
                        FontSize = 28,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333333")
                    },
                    buttonGrid,
                    _busyIndicator,
                    _statusLabel,
                    summaryFrame,
                    new Label
                    {
                        Text = "Unmatched Queue Operations",
                        FontSize = 20,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333333"),
                        Margin = new Thickness(0, 8, 0, 0)
                    },
                    _unmatchedSections
                }
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAuditAsync();
    }

    private async Task LoadAuditAsync()
    {
        SetBusy(true);
        _statusLabel.Text = "Loading queue audit...";

        try
        {
            var activeIdeasTask = _ideas.GetIdeasAsync(_auth.CurrentUsername);
            var archivedIdeasTask = _ideas.GetArchivedIdeasAsync(_auth.CurrentUsername);
            var pendingQueueTask = _sync.DownloadQueueAsync();
            var queueArchiveTask = _sync.DownloadQueueArchiveAsync();
            await Task.WhenAll(activeIdeasTask, archivedIdeasTask, pendingQueueTask, queueArchiveTask);

            var allIdeas = activeIdeasTask.Result
                .Concat(archivedIdeasTask.Result)
                .GroupBy(idea => idea.Id)
                .Select(group => group.First())
                .ToList();
            var pendingIdeaOperations = pendingQueueTask.Result
                .Where(operation => operation.OperationType == "idea_logged")
                .ToList();
            var archivedIdeaOperations = queueArchiveTask.Result
                .Where(operation => operation.OperationType == "idea_logged")
                .ToList();
            var ideaOperations = pendingIdeaOperations
                .Concat(archivedIdeaOperations)
                .ToList();

            var databaseKeys = allIdeas
                .Select(idea => BuildMatchKey(idea.Username, idea.Title))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var decodedOperations = ideaOperations
                .Select(DecodeOperation)
                .ToList();
            var unmatched = decodedOperations
                .Where(item => !databaseKeys.Contains(BuildMatchKey(item.Username, item.Title)))
                .ToList();
            int matchedCount = decodedOperations.Count - unmatched.Count;

            _databaseCountLabel.Text = $"Database: {allIdeas.Count} ideas";
            _pendingCountLabel.Text = $"Pending on server: {pendingIdeaOperations.Count}";
            _archiveCountLabel.Text = $"Archived (applied): {archivedIdeaOperations.Count}";
            _matchedCountLabel.Text = $"✅ Matched: {matchedCount}";
            _unmatchedCountLabel.Text = $"⚠️ Not in database: {unmatched.Count}";
            _statusLabel.Text = "";

            RenderUnmatched(unmatched);
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Unable to load queue audit: {ex.Message}";
            _statusLabel.TextColor = Color.FromArgb("#C62828");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private DecodedIdeaOperation DecodeOperation(QueuedOperation operation)
    {
        try
        {
            var password = _db.GetDbPassword();
            if (string.IsNullOrEmpty(password))
                throw new InvalidOperationException("No login password is available.");

            var plaintext = QueuePayloadCrypto.DecryptPayload(operation.PayloadJson, password);
            using var document = JsonDocument.Parse(plaintext);
            var root = document.RootElement;
            var title = root.TryGetProperty("title", out var titleElement)
                ? titleElement.GetString() ?? "(Untitled idea)"
                : "(Untitled idea)";
            var username = root.TryGetProperty("username", out var usernameElement)
                ? usernameElement.GetString() ?? ""
                : "";

            return new DecodedIdeaOperation(operation, username, title);
        }
        catch
        {
            return new DecodedIdeaOperation(operation, "", "(Unable to decrypt title)");
        }
    }

    private void RenderUnmatched(List<DecodedIdeaOperation> unmatched)
    {
        _unmatchedSections.Children.Clear();
        if (unmatched.Count == 0)
        {
            _unmatchedSections.Children.Add(new Label
            {
                Text = "All queued idea operations have a matching database idea.",
                FontSize = 14,
                TextColor = Color.FromArgb("#2E7D32")
            });
            return;
        }

        foreach (var monthGroup in unmatched
                     .GroupBy(item => new DateTime(item.Operation.CreatedAt.Year, item.Operation.CreatedAt.Month, 1))
                     .OrderByDescending(group => group.Key))
        {
            var itemsStack = new VerticalStackLayout
            {
                Spacing = 8,
                IsVisible = false
            };

            foreach (var item in monthGroup.OrderByDescending(item => item.Operation.CreatedAt))
                itemsStack.Children.Add(CreateOperationCard(item));

            var headerButton = new Button
            {
                Text = $"▶ {monthGroup.Key.ToString("MMMM yyyy", CultureInfo.CurrentCulture)} ({monthGroup.Count()} unmatched)",
                BackgroundColor = Color.FromArgb("#FFF3E0"),
                TextColor = Color.FromArgb("#E65100"),
                FontAttributes = FontAttributes.Bold,
                CornerRadius = 8,
                HorizontalOptions = LayoutOptions.Fill
            };
            headerButton.Clicked += (_, _) =>
            {
                itemsStack.IsVisible = !itemsStack.IsVisible;
                headerButton.Text = $"{(itemsStack.IsVisible ? "▼" : "▶")} {monthGroup.Key.ToString("MMMM yyyy", CultureInfo.CurrentCulture)} ({monthGroup.Count()} unmatched)";
            };

            _unmatchedSections.Children.Add(headerButton);
            _unmatchedSections.Children.Add(itemsStack);
        }
    }

    private static View CreateOperationCard(DecodedIdeaOperation item)
    {
        return new Frame
        {
            Padding = 14,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false,
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    new Label
                    {
                        Text = item.Title,
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Color.FromArgb("#333333"),
                        LineBreakMode = LineBreakMode.WordWrap
                    },
                    new Label
                    {
                        Text = $"Status: {GetStatusText(item.Operation.Status)}",
                        FontSize = 12,
                        TextColor = GetStatusColor(item.Operation.Status)
                    },
                    new Label
                    {
                        Text = item.Operation.CreatedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt", CultureInfo.CurrentCulture),
                        FontSize = 12,
                        TextColor = Color.FromArgb("#777777")
                    }
                }
            }
        };
    }

    private async void OnUploadPendingClicked(object? sender, EventArgs e)
    {
        SetBusy(true);
        _statusLabel.Text = "Uploading pending operations...";
        _statusLabel.TextColor = Color.FromArgb("#5B63EE");

        try
        {
            var result = await _sync.UploadQueueAsync();
            _statusLabel.Text = result.Message;
            _statusLabel.TextColor = result.Success
                ? Color.FromArgb("#2E7D32")
                : Color.FromArgb("#C62828");

            if (result.Success)
                await LoadAuditAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Queue upload failed: {ex.Message}";
            _statusLabel.TextColor = Color.FromArgb("#C62828");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnClearArchiveClicked(object? sender, EventArgs e)
    {
        bool confirmed = await DisplayAlert(
            "Clear Queue Archive?",
            "This permanently deletes all archived queue operations from the server.",
            "Clear Archive",
            "Cancel");
        if (!confirmed)
            return;

        SetBusy(true);
        _statusLabel.Text = "Clearing queue archive...";
        _statusLabel.TextColor = Color.FromArgb("#5B63EE");

        try
        {
            var result = await _sync.ClearQueueArchiveAsync();
            _statusLabel.Text = result.Message;
            _statusLabel.TextColor = result.Success
                ? Color.FromArgb("#2E7D32")
                : Color.FromArgb("#C62828");

            if (result.Success)
                await LoadAuditAsync();
        }
        catch (Exception ex)
        {
            _statusLabel.Text = $"Queue archive clear failed: {ex.Message}";
            _statusLabel.TextColor = Color.FromArgb("#C62828");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool isBusy)
    {
        _busyIndicator.IsVisible = isBusy;
        _busyIndicator.IsRunning = isBusy;
        _refreshButton.IsEnabled = !isBusy;
        _uploadPendingButton.IsEnabled = !isBusy;
        _clearArchiveButton.IsEnabled = !isBusy;
    }

    private static Label CreateSummaryLabel(string text, string color) => new()
    {
        Text = text,
        FontSize = 16,
        FontAttributes = FontAttributes.Bold,
        TextColor = Color.FromArgb(color)
    };

    private static string BuildMatchKey(string? username, string? title)
        => $"{username?.Trim()}\u001F{title?.Trim()}";

    private static string GetStatusText(int status) => status switch
    {
        0 => "Pending",
        1 => "Synced",
        2 => "Applied",
        3 => "Failed",
        _ => $"Unknown ({status})"
    };

    private static Color GetStatusColor(int status) => status switch
    {
        0 => Color.FromArgb("#EF6C00"),
        1 => Color.FromArgb("#1565C0"),
        2 => Color.FromArgb("#2E7D32"),
        3 => Color.FromArgb("#C62828"),
        _ => Color.FromArgb("#666666")
    };

    private sealed record DecodedIdeaOperation(
        QueuedOperation Operation,
        string Username,
        string Title);
}

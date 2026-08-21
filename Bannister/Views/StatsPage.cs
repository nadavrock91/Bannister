using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class StatsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly StatTrackerService _stats;

    private VerticalStackLayout _trackersContainer;
    private bool _showArchived = false;

    public StatsPage(AuthService auth, StatTrackerService stats)
    {
        _auth = auth;
        _stats = stats;
        Title = "Stats Tracker";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTrackersAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout { Padding = 20, Spacing = 12 };

        mainStack.Children.Add(new Label
        {
            Text = "\U0001F4CA Stats Tracker",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });

        mainStack.Children.Add(new Label
        {
            Text = "Track anything. Add results tagged with labels to see breakdowns.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        var addBtn = new Button
        {
            Text = "+ New Tracker",
            BackgroundColor = Color.FromArgb("#1565C0"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        };
        addBtn.Clicked += async (_, _) => await AddTrackerAsync();
        mainStack.Children.Add(addBtn);

        _trackersContainer = new VerticalStackLayout { Spacing = 12 };
        mainStack.Children.Add(_trackersContainer);

        var archivedToggle = new Button
        {
            Text = "Show Archived",
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            TextColor = Color.FromArgb("#666"),
            CornerRadius = 8,
            HeightRequest = 36,
            FontSize = 12
        };
        archivedToggle.Clicked += async (_, _) =>
        {
            _showArchived = !_showArchived;
            archivedToggle.Text = _showArchived ? "Hide Archived" : "Show Archived";
            await LoadTrackersAsync();
        };
        mainStack.Children.Add(archivedToggle);

        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadTrackersAsync()
    {
        _trackersContainer.Children.Clear();

        var trackers = await _stats.GetActiveTrackersAsync(_auth.CurrentUsername);

        if (trackers.Count == 0)
        {
            _trackersContainer.Children.Add(new Label
            {
                Text = "No trackers yet. Tap + New Tracker to start.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic
            });
        }

        foreach (var tracker in trackers)
            _trackersContainer.Children.Add(await BuildTrackerCardAsync(tracker, false));

        if (_showArchived)
        {
            var archived = await _stats.GetArchivedTrackersAsync(_auth.CurrentUsername);
            if (archived.Count > 0)
            {
                _trackersContainer.Children.Add(new Label
                {
                    Text = $"Archived ({archived.Count})",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#999"),
                    Margin = new Thickness(0, 8, 0, 0)
                });

                foreach (var tracker in archived)
                    _trackersContainer.Children.Add(await BuildTrackerCardAsync(tracker, true));
            }
        }
    }

    private async Task<Frame> BuildTrackerCardAsync(StatTracker tracker, bool isArchived)
    {
        var summary = await _stats.GetSummaryAsync(tracker.Id);
        var breakdown = await _stats.GetLabelBreakdownAsync(tracker.Id);

        var frame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = isArchived ? Color.FromArgb("#F5F5F5") : Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            HasShadow = false
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        stack.Children.Add(new Label
        {
            Text = tracker.Title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        if (!string.IsNullOrWhiteSpace(tracker.Description))
        {
            stack.Children.Add(new Label
            {
                Text = tracker.Description,
                FontSize = 12,
                TextColor = Color.FromArgb("#666")
            });
        }

        if (summary.Count > 0)
        {
            var statsRow = new HorizontalStackLayout { Spacing = 12 };

            foreach (var kvp in summary)
            {
                string icon = kvp.Key switch
                {
                    "success" => "\u2705",
                    "failure" => "\u274C",
                    "increment" => "\u2B06\uFE0F",
                    "decrement" => "\u2B07\uFE0F",
                    "note" => "\U0001F4DD",
                    _ => "\U0001F4CA"
                };

                Color color = kvp.Key switch
                {
                    "success" => Color.FromArgb("#2E7D32"),
                    "failure" => Color.FromArgb("#C62828"),
                    "increment" => Color.FromArgb("#1565C0"),
                    "decrement" => Color.FromArgb("#E65100"),
                    _ => Color.FromArgb("#666")
                };

                statsRow.Children.Add(new Label
                {
                    Text = $"{icon} {kvp.Value}",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = color,
                    VerticalOptions = LayoutOptions.Center
                });
            }

            int total = summary.Values.Sum();
            int successCount = summary.GetValueOrDefault("success", 0);
            if (total > 0 && summary.ContainsKey("success") && summary.ContainsKey("failure"))
            {
                int pct = (int)Math.Round(100.0 * successCount / (successCount + summary.GetValueOrDefault("failure", 0)));
                statsRow.Children.Add(new Label
                {
                    Text = $"({pct}%)",
                    FontSize = 12,
                    TextColor = Color.FromArgb("#888"),
                    VerticalOptions = LayoutOptions.Center
                });
            }

            stack.Children.Add(statsRow);
        }

        if (breakdown.Count > 0 && breakdown.Count <= 20)
        {
            var breakdownStack = new VerticalStackLayout { Spacing = 2 };
            breakdownStack.Children.Add(new Label
            {
                Text = "By label:",
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#888")
            });

            foreach (var labelKvp in breakdown.OrderByDescending(b =>
                b.Value.GetValueOrDefault("success", 0) + b.Value.GetValueOrDefault("increment", 0)))
            {
                var parts = new List<string>();
                foreach (var typeKvp in labelKvp.Value)
                {
                    string icon = typeKvp.Key switch
                    {
                        "success" => "\u2705",
                        "failure" => "\u274C",
                        "increment" => "\u2B06",
                        "decrement" => "\u2B07",
                        _ => ""
                    };
                    parts.Add($"{icon}{typeKvp.Value}");
                }

                breakdownStack.Children.Add(new Label
                {
                    Text = $"  {labelKvp.Key}: {string.Join("  ", parts)}",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#555")
                });
            }

            stack.Children.Add(breakdownStack);
        }

        var btnRow = new HorizontalStackLayout { Spacing = 6 };

        if (!isArchived)
        {
            var capturedTracker = tracker;

            var successBtn = new Button
            {
                Text = "\u2705 Success",
                BackgroundColor = Color.FromArgb("#E8F5E9"),
                TextColor = Color.FromArgb("#2E7D32"),
                CornerRadius = 6,
                HeightRequest = 32,
                FontSize = 11,
                Padding = new Thickness(8, 0)
            };
            successBtn.Clicked += async (_, _) => await QuickAddEntryAsync(capturedTracker, "success");
            btnRow.Children.Add(successBtn);

            var failBtn = new Button
            {
                Text = "\u274C Failure",
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#C62828"),
                CornerRadius = 6,
                HeightRequest = 32,
                FontSize = 11,
                Padding = new Thickness(8, 0)
            };
            failBtn.Clicked += async (_, _) => await QuickAddEntryAsync(capturedTracker, "failure");
            btnRow.Children.Add(failBtn);

            var incBtn = new Button
            {
                Text = "\u2B06 +1",
                BackgroundColor = Color.FromArgb("#E3F2FD"),
                TextColor = Color.FromArgb("#1565C0"),
                CornerRadius = 6,
                HeightRequest = 32,
                FontSize = 11,
                Padding = new Thickness(8, 0)
            };
            incBtn.Clicked += async (_, _) => await QuickAddEntryAsync(capturedTracker, "increment");
            btnRow.Children.Add(incBtn);

            var decBtn = new Button
            {
                Text = "\u2B07 -1",
                BackgroundColor = Color.FromArgb("#FFF3E0"),
                TextColor = Color.FromArgb("#E65100"),
                CornerRadius = 6,
                HeightRequest = 32,
                FontSize = 11,
                Padding = new Thickness(8, 0)
            };
            decBtn.Clicked += async (_, _) => await QuickAddEntryAsync(capturedTracker, "decrement");
            btnRow.Children.Add(decBtn);

            var moreBtn = new Button
            {
                Text = "\u22EF",
                BackgroundColor = Color.FromArgb("#E0E0E0"),
                TextColor = Color.FromArgb("#333"),
                CornerRadius = 6,
                HeightRequest = 32,
                WidthRequest = 36,
                FontSize = 14,
                Padding = 0
            };
            moreBtn.Clicked += async (_, _) => await ShowTrackerMenuAsync(capturedTracker);
            btnRow.Children.Add(moreBtn);
        }
        else
        {
            var capturedTracker = tracker;

            var restoreBtn = new Button
            {
                Text = "\u267B\uFE0F Restore",
                BackgroundColor = Color.FromArgb("#E8F5E9"),
                TextColor = Color.FromArgb("#2E7D32"),
                CornerRadius = 6,
                HeightRequest = 32,
                FontSize = 11,
                Padding = new Thickness(8, 0)
            };
            restoreBtn.Clicked += async (_, _) =>
            {
                await _stats.RestoreTrackerAsync(capturedTracker.Id);
                await LoadTrackersAsync();
            };
            btnRow.Children.Add(restoreBtn);

            var deleteBtn = new Button
            {
                Text = "\U0001F5D1 Delete",
                BackgroundColor = Color.FromArgb("#FFEBEE"),
                TextColor = Color.FromArgb("#C62828"),
                CornerRadius = 6,
                HeightRequest = 32,
                FontSize = 11,
                Padding = new Thickness(8, 0)
            };
            deleteBtn.Clicked += async (_, _) =>
            {
                bool confirm = await DisplayAlert("Delete?", $"Permanently delete '{capturedTracker.Title}' and all its entries?", "Delete", "Cancel");
                if (!confirm) return;
                await _stats.DeleteTrackerAsync(capturedTracker.Id);
                await LoadTrackersAsync();
            };
            btnRow.Children.Add(deleteBtn);
        }

        stack.Children.Add(btnRow);
        frame.Content = stack;
        return frame;
    }

    private async Task QuickAddEntryAsync(StatTracker tracker, string entryType)
    {
        string? label = await DisplayPromptAsync(
            $"Add {entryType}",
            "Label (e.g. outfit name, context). Leave empty for no label:",
            "Save",
            "Cancel",
            maxLength: 200);

        if (label == null) return;

        string? notes = null;
        if (entryType == "success" || entryType == "failure")
        {
            notes = await DisplayPromptAsync(
                "Notes (optional)",
                "Any details about this result:",
                "Save",
                "Skip",
                maxLength: 500);
        }

        await _stats.AddEntryAsync(tracker.Id, entryType, label.Trim(), notes?.Trim() ?? "");
        await LoadTrackersAsync();
    }

    private async Task ShowTrackerMenuAsync(StatTracker tracker)
    {
        string? choice = await DisplayActionSheet(tracker.Title, "Cancel", null,
            "View History",
            "Add Note",
            "Edit Title",
            "Edit Description",
            "Archive");

        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        switch (choice)
        {
            case "View History":
                await ViewHistoryAsync(tracker);
                break;
            case "Add Note":
                string? noteText = await DisplayPromptAsync("Add Note", "Note:", "Save", "Cancel", maxLength: 500);
                if (!string.IsNullOrWhiteSpace(noteText))
                {
                    string? noteLabel = await DisplayPromptAsync("Label (optional)", "Label:", "Save", "Skip", maxLength: 200);
                    await _stats.AddEntryAsync(tracker.Id, "note", noteLabel?.Trim() ?? "", noteText.Trim());
                    await LoadTrackersAsync();
                }
                break;
            case "Edit Title":
                string? newTitle = await DisplayPromptAsync("Title", "Tracker title:", "Save", "Cancel", initialValue: tracker.Title, maxLength: 200);
                if (!string.IsNullOrWhiteSpace(newTitle))
                {
                    tracker.Title = newTitle.Trim();
                    await _stats.UpdateTrackerAsync(tracker);
                    await LoadTrackersAsync();
                }
                break;
            case "Edit Description":
                string? newDesc = await DisplayPromptAsync("Description", "Description:", "Save", "Cancel", initialValue: tracker.Description, maxLength: 500);
                if (newDesc != null)
                {
                    tracker.Description = newDesc.Trim();
                    await _stats.UpdateTrackerAsync(tracker);
                    await LoadTrackersAsync();
                }
                break;
            case "Archive":
                await _stats.ArchiveTrackerAsync(tracker.Id);
                await LoadTrackersAsync();
                break;
        }
    }

    private async Task ViewHistoryAsync(StatTracker tracker)
    {
        var entries = await _stats.GetEntriesAsync(tracker.Id, 50);

        if (entries.Count == 0)
        {
            await DisplayAlert("No History", "No entries recorded yet.", "OK");
            return;
        }

        var lines = entries.Select(e =>
        {
            string icon = e.EntryType switch
            {
                "success" => "\u2705",
                "failure" => "\u274C",
                "increment" => "\u2B06",
                "decrement" => "\u2B07",
                "note" => "\U0001F4DD",
                _ => "\U0001F4CA"
            };

            string label = string.IsNullOrWhiteSpace(e.Label) ? "" : $" [{e.Label}]";
            string notes = string.IsNullOrWhiteSpace(e.Notes) ? "" : $" — {e.Notes}";
            string date = e.CreatedAt.ToLocalTime().ToString("MMM dd HH:mm");

            return $"{icon}{label}{notes}  ({date})";
        });

        await DisplayAlert(
            $"{tracker.Title} — Last {entries.Count} entries",
            string.Join("\n", lines),
            "OK");
    }

    private async Task AddTrackerAsync()
    {
        string? title = await DisplayPromptAsync("New Tracker", "What do you want to track?", "Next", "Cancel", maxLength: 200);
        if (string.IsNullOrWhiteSpace(title)) return;

        string? description = await DisplayPromptAsync("Description (optional)", "What is this tracker for?", "Create", "Skip", maxLength: 500);

        await _stats.CreateTrackerAsync(_auth.CurrentUsername, title.Trim(), description?.Trim() ?? "");
        await LoadTrackersAsync();
    }
}

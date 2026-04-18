using Bannister.Services;
using Bannister.Models;

namespace Bannister.Views;

public class SubActivitiesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly SubActivityService _subActivityService;
    
    private VerticalStackLayout _listStack;
    private Grid _loadingOverlay;

    public SubActivitiesPage(AuthService auth, SubActivityService subActivityService)
    {
        _auth = auth;
        _subActivityService = subActivityService;
        Title = "SubActivities";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid();

        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        // Header
        var headerRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        
        var headerStack = new VerticalStackLayout();
        headerStack.Children.Add(new Label
        {
            Text = "🔢 SubActivities",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#00838F")
        });
        headerStack.Children.Add(new Label
        {
            Text = "Break down processes into steps",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });
        Grid.SetColumn(headerStack, 0);
        headerRow.Children.Add(headerStack);

        var addBtn = new Button
        {
            Text = "+ New Process",
            BackgroundColor = Color.FromArgb("#00838F"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(16, 8),
            FontSize = 14,
            HeightRequest = 40,
            VerticalOptions = LayoutOptions.Center
        };
        addBtn.Clicked += OnAddProcessClicked;
        Grid.SetColumn(addBtn, 1);
        headerRow.Children.Add(addBtn);

        mainStack.Children.Add(headerRow);

        // List of processes
        _listStack = new VerticalStackLayout { Spacing = 12 };
        mainStack.Children.Add(_listStack);

        scrollView.Content = mainStack;
        mainGrid.Children.Add(scrollView);

        // Loading overlay
        _loadingOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#80000000")
        };
        _loadingOverlay.Children.Add(new ActivityIndicator
        {
            IsRunning = true,
            Color = Colors.White,
            WidthRequest = 50,
            HeightRequest = 50,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        });
        mainGrid.Children.Add(_loadingOverlay);

        Content = mainGrid;
    }

    private async Task LoadDataAsync()
    {
        _loadingOverlay.IsVisible = true;

        try
        {
            var items = await _subActivityService.GetActiveAsync(_auth.CurrentUsername);
            
            _listStack.Children.Clear();

            if (items.Count == 0)
            {
                _listStack.Children.Add(new Label
                {
                    Text = "No processes yet. Tap '+ New Process' to create one.",
                    TextColor = Color.FromArgb("#999"),
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 40)
                });
            }
            else
            {
                foreach (var item in items)
                {
                    _listStack.Children.Add(BuildProcessCard(item));
                }
            }

            // Archived section
            var archived = await _subActivityService.GetArchivedAsync(_auth.CurrentUsername);
            if (archived.Count > 0)
            {
                _listStack.Children.Add(new Label
                {
                    Text = $"Archived ({archived.Count})",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#999"),
                    Margin = new Thickness(0, 20, 0, 8)
                });

                foreach (var item in archived)
                {
                    _listStack.Children.Add(BuildArchivedCard(item));
                }
            }
        }
        finally
        {
            _loadingOverlay.IsVisible = false;
        }
    }

    private Frame BuildProcessCard(SubActivity item)
    {
        var steps = _subActivityService.GetSteps(item);
        var pendingSteps = _subActivityService.GetPendingSteps(item);
        int doneCount = steps.Count(s => s.Done);
        bool allDone = steps.Count > 0 && doneCount == steps.Count;

        var card = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = allDone ? Color.FromArgb("#E8F5E9") : Colors.White,
            BorderColor = allDone ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0"),
            HasShadow = true
        };

        var cardStack = new VerticalStackLayout { Spacing = 12 };

        // Header row with title and settings
        var headerRow = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        
        var titleStack = new VerticalStackLayout();
        titleStack.Children.Add(new Label
        {
            Text = item.Name,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        // Status badges
        var badgeRow = new HorizontalStackLayout { Spacing = 8 };
        
        // Progress
        badgeRow.Children.Add(new Frame
        {
            Padding = new Thickness(8, 2),
            CornerRadius = 10,
            BackgroundColor = allDone ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E3F2FD"),
            BorderColor = Colors.Transparent,
            Content = new Label
            {
                Text = $"{doneCount}/{steps.Count}",
                FontSize = 11,
                TextColor = allDone ? Colors.White : Color.FromArgb("#1565C0")
            }
        });

        // Reset mode badge
        badgeRow.Children.Add(new Frame
        {
            Padding = new Thickness(8, 2),
            CornerRadius = 10,
            BackgroundColor = item.ResetMode == "daily" ? Color.FromArgb("#FFF3E0") : Color.FromArgb("#F5F5F5"),
            BorderColor = Colors.Transparent,
            Content = new Label
            {
                Text = item.ResetMode == "daily" ? "☀️ Daily" : "🔄 Manual",
                FontSize = 11,
                TextColor = item.ResetMode == "daily" ? Color.FromArgb("#E65100") : Color.FromArgb("#666")
            }
        });

        // Completions
        if (item.TotalCompletions > 0)
        {
            badgeRow.Children.Add(new Frame
            {
                Padding = new Thickness(8, 2),
                CornerRadius = 10,
                BackgroundColor = Color.FromArgb("#E8F5E9"),
                BorderColor = Colors.Transparent,
                Content = new Label
                {
                    Text = $"✓ {item.TotalCompletions}x",
                    FontSize = 11,
                    TextColor = Color.FromArgb("#2E7D32")
                }
            });
        }

        titleStack.Children.Add(badgeRow);
        Grid.SetColumn(titleStack, 0);
        headerRow.Children.Add(titleStack);

        // Settings button
        var settingsBtn = new Button
        {
            Text = "⚙️",
            BackgroundColor = Colors.Transparent,
            FontSize = 18,
            WidthRequest = 40,
            HeightRequest = 40,
            Padding = 0
        };
        settingsBtn.Clicked += async (s, e) => await ShowProcessSettingsAsync(item);
        Grid.SetColumn(settingsBtn, 1);
        headerRow.Children.Add(settingsBtn);

        cardStack.Children.Add(headerRow);

        // Active Steps
        if (steps.Count > 0)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                int index = i;
                
                var stepRow = new Grid
                {
                    ColumnDefinitions = 
                    {
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Padding = new Thickness(0, 4)
                };

                var checkbox = new Button
                {
                    Text = step.Done ? "☑️" : "⬜",
                    BackgroundColor = Colors.Transparent,
                    FontSize = 20,
                    WidthRequest = 36,
                    HeightRequest = 36,
                    Padding = 0
                };
                checkbox.Clicked += async (s, e) =>
                {
                    await _subActivityService.ToggleStepAsync(item, index);
                    await LoadDataAsync();
                };
                Grid.SetColumn(checkbox, 0);
                stepRow.Children.Add(checkbox);

                var stepLabel = new Label
                {
                    Text = step.Name,
                    FontSize = 15,
                    TextColor = step.Done ? Color.FromArgb("#999") : Color.FromArgb("#333"),
                    TextDecorations = step.Done ? TextDecorations.Strikethrough : TextDecorations.None,
                    VerticalOptions = LayoutOptions.Center
                };
                Grid.SetColumn(stepLabel, 1);
                stepRow.Children.Add(stepLabel);

                // Move to pending button
                var moveBtn = new Button
                {
                    Text = "📥",
                    BackgroundColor = Colors.Transparent,
                    FontSize = 14,
                    WidthRequest = 32,
                    HeightRequest = 32,
                    Padding = 0
                };
                moveBtn.Clicked += async (s, e) =>
                {
                    bool confirm = await DisplayAlert("Move to Pending", $"Move '{step.Name}' to pending steps?", "Yes", "No");
                    if (confirm)
                    {
                        await _subActivityService.MoveStepToPendingAsync(item, index);
                        await LoadDataAsync();
                    }
                };
                Grid.SetColumn(moveBtn, 2);
                stepRow.Children.Add(moveBtn);

                cardStack.Children.Add(stepRow);
            }
        }

        // Add step button
        bool canAdd = _subActivityService.CanAddStep(item);
        int neededToAdd = _subActivityService.CompletionsNeededToAdd(item);

        var addStepBtn = new Button
        {
            Text = canAdd ? "+ Add Step" : $"🔒 {neededToAdd} more completion(s) to unlock",
            BackgroundColor = canAdd ? Color.FromArgb("#E0F7FA") : Color.FromArgb("#F5F5F5"),
            TextColor = canAdd ? Color.FromArgb("#00838F") : Color.FromArgb("#999"),
            CornerRadius = 6,
            FontSize = 13,
            HeightRequest = 36,
            IsEnabled = canAdd
        };
        if (canAdd)
        {
            addStepBtn.Clicked += async (s, e) => await AddStepAsync(item, false);
        }
        cardStack.Children.Add(addStepBtn);

        // Pending Steps Section
        if (pendingSteps.Count > 0)
        {
            cardStack.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#E0E0E0"), Margin = new Thickness(0, 8) });
            
            cardStack.Children.Add(new Label
            {
                Text = $"📋 Pending Steps ({pendingSteps.Count})",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#666")
            });

            for (int i = 0; i < pendingSteps.Count; i++)
            {
                var pending = pendingSteps[i];
                int index = i;

                var pendingRow = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Padding = new Thickness(0, 2)
                };

                var pendingLabel = new Label
                {
                    Text = $"• {pending.Name}",
                    FontSize = 13,
                    TextColor = Color.FromArgb("#888"),
                    VerticalOptions = LayoutOptions.Center
                };
                Grid.SetColumn(pendingLabel, 0);
                pendingRow.Children.Add(pendingLabel);

                // Activate button
                var activateBtn = new Button
                {
                    Text = "📤",
                    BackgroundColor = Colors.Transparent,
                    FontSize = 14,
                    WidthRequest = 32,
                    HeightRequest = 32,
                    Padding = 0,
                    IsEnabled = canAdd
                };
                activateBtn.Clicked += async (s, e) =>
                {
                    await _subActivityService.ActivatePendingStepAsync(item, index);
                    await LoadDataAsync();
                };
                Grid.SetColumn(activateBtn, 1);
                pendingRow.Children.Add(activateBtn);

                // Delete pending
                var deleteBtn = new Button
                {
                    Text = "🗑️",
                    BackgroundColor = Colors.Transparent,
                    FontSize = 14,
                    WidthRequest = 32,
                    HeightRequest = 32,
                    Padding = 0
                };
                deleteBtn.Clicked += async (s, e) =>
                {
                    bool confirm = await DisplayAlert("Delete", $"Delete '{pending.Name}' from pending?", "Yes", "No");
                    if (confirm)
                    {
                        await _subActivityService.RemovePendingStepAsync(item, index);
                        await LoadDataAsync();
                    }
                };
                Grid.SetColumn(deleteBtn, 2);
                pendingRow.Children.Add(deleteBtn);

                cardStack.Children.Add(pendingRow);
            }
        }

        // Add to pending button
        var addPendingBtn = new Button
        {
            Text = "+ Add to Pending",
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            TextColor = Color.FromArgb("#666"),
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32
        };
        addPendingBtn.Clicked += async (s, e) => await AddStepAsync(item, true);
        cardStack.Children.Add(addPendingBtn);

        // Reset button (for manual mode or to force reset)
        if (item.ResetMode == "manual" || steps.Any(s => s.Done))
        {
            var resetBtn = new Button
            {
                Text = "🔄 Reset All Steps",
                BackgroundColor = Color.FromArgb("#FFF3E0"),
                TextColor = Color.FromArgb("#E65100"),
                CornerRadius = 6,
                FontSize = 12,
                HeightRequest = 32,
                Margin = new Thickness(0, 4, 0, 0)
            };
            resetBtn.Clicked += async (s, e) =>
            {
                bool confirm = await DisplayAlert("Reset", "Reset all steps to unchecked?", "Yes", "No");
                if (confirm)
                {
                    await _subActivityService.ResetStepsAsync(item);
                    await LoadDataAsync();
                }
            };
            cardStack.Children.Add(resetBtn);
        }

        card.Content = cardStack;
        return card;
    }

    private Frame BuildArchivedCard(SubActivity item)
    {
        var card = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb("#FAFAFA"),
            BorderColor = Color.FromArgb("#E0E0E0")
        };

        var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };

        row.Children.Add(new Label
        {
            Text = item.Name,
            FontSize = 14,
            TextColor = Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center
        });

        var restoreBtn = new Button
        {
            Text = "Restore",
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            TextColor = Color.FromArgb("#1565C0"),
            CornerRadius = 6,
            FontSize = 12,
            HeightRequest = 32,
            Padding = new Thickness(12, 0)
        };
        restoreBtn.Clicked += async (s, e) =>
        {
            await _subActivityService.RestoreAsync(item.Id);
            await LoadDataAsync();
        };
        Grid.SetColumn(restoreBtn, 1);
        row.Children.Add(restoreBtn);

        card.Content = row;
        return card;
    }

    private async void OnAddProcessClicked(object? sender, EventArgs e)
    {
        string? name = await DisplayPromptAsync("New Process", "Enter a name for the process:", "Create", "Cancel", "e.g., Morning Routine");
        if (string.IsNullOrWhiteSpace(name)) return;

        // Ask for reset mode
        string resetMode = await DisplayActionSheet("Reset Mode", "Cancel", null, "☀️ Daily (auto-reset each day)", "🔄 Manual (reset when you choose)");
        if (resetMode == "Cancel" || resetMode == null) return;
        resetMode = resetMode.Contains("Daily") ? "daily" : "manual";

        // Ask for addition mode
        string addMode = await DisplayActionSheet("Adding New Steps", "Cancel", null, "🔓 Unlimited (add anytime)", "🔒 Locked (complete 3x first)");
        if (addMode == "Cancel" || addMode == null) return;
        string additionMode = addMode.Contains("Unlimited") ? "unlimited" : "locked";

        int requiredCompletions = 3;
        if (additionMode == "locked")
        {
            string? numStr = await DisplayPromptAsync("Required Completions", "How many completions before unlocking new steps?", "OK", "Cancel", "3", keyboard: Keyboard.Numeric);
            if (!string.IsNullOrEmpty(numStr) && int.TryParse(numStr, out int num) && num > 0)
            {
                requiredCompletions = num;
            }
        }

        await _subActivityService.CreateAsync(_auth.CurrentUsername, name, resetMode, additionMode, requiredCompletions);
        await LoadDataAsync();
    }

    private async Task AddStepAsync(SubActivity item, bool toPending)
    {
        string? stepName = await DisplayPromptAsync(
            toPending ? "Add Pending Step" : "Add Step",
            "Enter step name:",
            "Add", "Cancel",
            "e.g., Check emails");

        if (string.IsNullOrWhiteSpace(stepName)) return;

        if (toPending)
        {
            await _subActivityService.AddPendingStepAsync(item, stepName);
        }
        else
        {
            await _subActivityService.AddStepAsync(item, stepName);
        }

        await LoadDataAsync();
    }

    private async Task ShowProcessSettingsAsync(SubActivity item)
    {
        string action = await DisplayActionSheet(
            item.Name,
            "Cancel",
            null,
            "✏️ Rename",
            $"☀️ Reset Mode: {(item.ResetMode == "daily" ? "Daily → Manual" : "Manual → Daily")}",
            $"🔒 Addition Mode: {(item.AdditionMode == "unlimited" ? "Unlimited → Locked" : "Locked → Unlimited")}",
            "📊 Stats",
            "📦 Archive",
            "🗑️ Delete");

        if (action == null || action == "Cancel") return;

        if (action.StartsWith("✏️"))
        {
            string? newName = await DisplayPromptAsync("Rename", "Enter new name:", "Save", "Cancel", item.Name);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                item.Name = newName;
                await _subActivityService.UpdateAsync(item);
                await LoadDataAsync();
            }
        }
        else if (action.StartsWith("☀️"))
        {
            item.ResetMode = item.ResetMode == "daily" ? "manual" : "daily";
            await _subActivityService.UpdateAsync(item);
            await LoadDataAsync();
        }
        else if (action.StartsWith("🔒"))
        {
            if (item.AdditionMode == "unlimited")
            {
                string? numStr = await DisplayPromptAsync("Lock Steps", "Completions required before adding:", "OK", "Cancel", "3", keyboard: Keyboard.Numeric);
                if (!string.IsNullOrEmpty(numStr) && int.TryParse(numStr, out int num) && num > 0)
                {
                    item.AdditionMode = "locked";
                    item.RequiredCompletionsToUnlock = num;
                    item.CompletionsSinceLastAddition = 0;
                    await _subActivityService.UpdateAsync(item);
                }
            }
            else
            {
                item.AdditionMode = "unlimited";
                await _subActivityService.UpdateAsync(item);
            }
            await LoadDataAsync();
        }
        else if (action.StartsWith("📊"))
        {
            var steps = _subActivityService.GetSteps(item);
            var pending = _subActivityService.GetPendingSteps(item);
            await DisplayAlert("Stats",
                $"Active Steps: {steps.Count}\n" +
                $"Pending Steps: {pending.Count}\n" +
                $"Total Completions: {item.TotalCompletions}\n" +
                $"Completions Since Last Addition: {item.CompletionsSinceLastAddition}\n" +
                $"Created: {item.CreatedAt:MMM d, yyyy}",
                "OK");
        }
        else if (action.StartsWith("📦"))
        {
            bool confirm = await DisplayAlert("Archive", $"Archive '{item.Name}'?", "Yes", "No");
            if (confirm)
            {
                await _subActivityService.ArchiveAsync(item.Id);
                await LoadDataAsync();
            }
        }
        else if (action.StartsWith("🗑️"))
        {
            bool confirm = await DisplayAlert("Delete", $"Permanently delete '{item.Name}'? This cannot be undone.", "Delete", "Cancel");
            if (confirm)
            {
                await _subActivityService.DeleteAsync(item.Id);
                await LoadDataAsync();
            }
        }
    }
}

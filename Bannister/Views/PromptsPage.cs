using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

/// <summary>
/// Page for generating and viewing prompts from different packs.
/// Supports pack selection, prompt generation with group-based rules,
/// and displays results in a scrollable list.
/// </summary>
public class PromptsPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly PromptService _prompts;
    private readonly IdeaLoggerService _ideaLogger;
    private readonly IdeasService _ideas;
    
    // UI Controls
    private Picker _packPicker;
    private Button _generateBtn;
    private Button _copyTextBtn;
    private Label _statsLabel;
    private Label _resultsCountLabel;
    private VerticalStackLayout _resultsStack;
    private Entry _minCountEntry;
    private Entry _maxCountEntry;
    private Picker _inspirationPicker;
    private Entry _inspirationMinEntry;
    private Entry _inspirationMaxEntry;
    
    // State
    private string _selectedPack = "Writing";
    private List<string> _packNames = new();
    private List<PromptItem> _lastGeneratedPrompts = new();
    private List<string> _ideaCategories = new();
    private string _selectedInspirationCategory = "None";
    private List<IdeaItem> _lastInspirationIdeas = new();

    public PromptsPage(AuthService auth, PromptService prompts, IdeaLoggerService ideaLogger, IdeasService ideas)
    {
        _auth = auth;
        _prompts = prompts;
        _ideaLogger = ideaLogger;
        _ideas = ideas;
        
        Title = "Prompts";
        BackgroundColor = Color.FromArgb("#F5F5F5");
        
        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPacksAsync();
        await LoadIdeaCategoriesAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 16,
            Spacing = 16
        };

        // Header
        var headerLabel = new Label
        {
            Text = "✨ Prompt Generator",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            HorizontalOptions = LayoutOptions.Center
        };
        mainStack.Children.Add(headerLabel);

        // Description
        var descLabel = new Label
        {
            Text = "Generate random prompts to guide your work.\nPrompts are selected with diversity rules to ensure variety.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666"),
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        mainStack.Children.Add(descLabel);

        // Controls Frame
        var controlsFrame = new Frame
        {
            Padding = 16,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent
        };

        var controlsStack = new VerticalStackLayout { Spacing = 12 };

        // Pack picker row
        var packRow = new HorizontalStackLayout { Spacing = 12 };
        
        packRow.Children.Add(new Label
        {
            Text = "Pack:",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#333"),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        });

        _packPicker = new Picker
        {
            Title = "Select Pack",
            WidthRequest = 150,
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };
        _packPicker.SelectedIndexChanged += OnPackChanged;
        packRow.Children.Add(_packPicker);

        // Stats label
        _statsLabel = new Label
        {
            Text = "",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            VerticalOptions = LayoutOptions.Center
        };
        packRow.Children.Add(_statsLabel);

        controlsStack.Children.Add(packRow);

        // Count range row (Min - Max)
        var countRow = new HorizontalStackLayout { Spacing = 8 };

        countRow.Children.Add(new Label
        {
            Text = "Count:",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#333"),
            FontSize = 14
        });

        _minCountEntry = new Entry
        {
            Text = "5",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 50,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        countRow.Children.Add(_minCountEntry);

        countRow.Children.Add(new Label
        {
            Text = "-",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#666"),
            FontSize = 14
        });

        _maxCountEntry = new Entry
        {
            Text = "10",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 50,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        countRow.Children.Add(_maxCountEntry);

        countRow.Children.Add(new Label
        {
            Text = "(random)",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#999"),
            FontSize = 11
        });

        controlsStack.Children.Add(countRow);

        // Inspiration row (ideas to append)
        var inspirationRow = new HorizontalStackLayout { Spacing = 8 };

        inspirationRow.Children.Add(new Label
        {
            Text = "Ideas:",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#333"),
            FontSize = 14
        });

        _inspirationPicker = new Picker
        {
            Title = "None",
            WidthRequest = 130,
            BackgroundColor = Color.FromArgb("#F5F5F5")
        };
        _inspirationPicker.Items.Add("None");
        inspirationRow.Children.Add(_inspirationPicker);

        _inspirationMinEntry = new Entry
        {
            Text = "0",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 40,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        inspirationRow.Children.Add(_inspirationMinEntry);

        inspirationRow.Children.Add(new Label
        {
            Text = "-",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#666"),
            FontSize = 14
        });

        _inspirationMaxEntry = new Entry
        {
            Text = "3",
            Keyboard = Keyboard.Numeric,
            WidthRequest = 40,
            BackgroundColor = Color.FromArgb("#F5F5F5"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        inspirationRow.Children.Add(_inspirationMaxEntry);

        inspirationRow.Children.Add(new Label
        {
            Text = "(append)",
            VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb("#999"),
            FontSize = 11
        });

        controlsStack.Children.Add(inspirationRow);

        // Generate and Copy row
        var generateRow = new HorizontalStackLayout { Spacing = 8 };

        _generateBtn = new Button
        {
            Text = "🎲 Generate",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            HeightRequest = 44,
            Padding = new Thickness(20, 0)
        };
        _generateBtn.Clicked += OnGenerateClicked;
        generateRow.Children.Add(_generateBtn);

        _copyTextBtn = new Button
        {
            Text = "📄 Copy All",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            Padding = new Thickness(12, 0),
            IsEnabled = false
        };
        _copyTextBtn.Clicked += OnCopyTextClicked;
        generateRow.Children.Add(_copyTextBtn);

        // Add Prompt button
        var addPromptBtn = new Button
        {
            Text = "➕ New Prompt",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            Padding = new Thickness(12, 0)
        };
        addPromptBtn.Clicked += async (s, e) => await AddNewPromptAsync();
        generateRow.Children.Add(addPromptBtn);

        // Add Pack button
        var addPackBtn = new Button
        {
            Text = "📦 New Pack",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            Padding = new Thickness(12, 0)
        };
        addPackBtn.Clicked += async (s, e) => await CreateNewPackAsync();
        generateRow.Children.Add(addPackBtn);

        // Settings button
        var settingsBtn = new Button
        {
            Text = "⚙️",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            WidthRequest = 44,
            HeightRequest = 44,
            CornerRadius = 8
        };
        settingsBtn.Clicked += OnSettingsClicked;
        generateRow.Children.Add(settingsBtn);

        controlsStack.Children.Add(generateRow);

        controlsFrame.Content = controlsStack;
        mainStack.Children.Add(controlsFrame);

        // Results Section Header
        var resultsHeaderRow = new HorizontalStackLayout { Spacing = 12 };
        
        var resultsLabel = new Label
        {
            Text = "Generated Prompts",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333"),
            VerticalOptions = LayoutOptions.Center
        };
        resultsHeaderRow.Children.Add(resultsLabel);

        _resultsCountLabel = new Label
        {
            Text = "",
            FontSize = 14,
            TextColor = Color.FromArgb("#5B63EE"),
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        };
        resultsHeaderRow.Children.Add(_resultsCountLabel);

        resultsHeaderRow.Margin = new Thickness(0, 8, 0, 0);
        mainStack.Children.Add(resultsHeaderRow);

        // Results scroll view
        var resultsFrame = new Frame
        {
            Padding = 0,
            CornerRadius = 12,
            BackgroundColor = Colors.White,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            VerticalOptions = LayoutOptions.FillAndExpand
        };

        _resultsStack = new VerticalStackLayout { Spacing = 8, Padding = 12 };
        
        // Initial message
        _resultsStack.Children.Add(new Label
        {
            Text = "Tap 'Generate' to get prompts!",
            TextColor = Color.FromArgb("#999"),
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 20)
        });

        var resultsScroll = new ScrollView 
        { 
            Content = _resultsStack,
            VerticalOptions = LayoutOptions.FillAndExpand
        };
        resultsFrame.Content = resultsScroll;
        mainStack.Children.Add(resultsFrame);

        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadPacksAsync()
    {
        try
        {
            _packNames = await _prompts.GetPackNamesAsync();
            
            _packPicker.Items.Clear();
            foreach (var pack in _packNames)
            {
                _packPicker.Items.Add(pack);
            }

            // Select Writing by default, or first available
            int defaultIndex = _packNames.IndexOf("Writing");
            if (defaultIndex < 0) defaultIndex = 0;
            
            if (_packNames.Count > 0)
            {
                _packPicker.SelectedIndex = defaultIndex;
                _selectedPack = _packNames[defaultIndex];
                await UpdateStatsAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading packs: {ex.Message}");
        }
    }

    private async Task LoadIdeaCategoriesAsync()
    {
        try
        {
            _ideaCategories = await _ideas.GetCategoriesAsync(_auth.CurrentUsername);
            
            _inspirationPicker.Items.Clear();
            _inspirationPicker.Items.Add("None");
            
            foreach (var cat in _ideaCategories.OrderBy(c => c))
            {
                _inspirationPicker.Items.Add(cat);
            }
            
            _inspirationPicker.SelectedIndex = 0;
            _selectedInspirationCategory = "None";
            
            _inspirationPicker.SelectedIndexChanged += (s, e) =>
            {
                if (_inspirationPicker.SelectedIndex >= 0)
                {
                    _selectedInspirationCategory = _inspirationPicker.Items[_inspirationPicker.SelectedIndex];
                }
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading idea categories: {ex.Message}");
        }
    }

    private async void OnPackChanged(object? sender, EventArgs e)
    {
        if (_packPicker.SelectedIndex >= 0 && _packPicker.SelectedIndex < _packNames.Count)
        {
            _selectedPack = _packNames[_packPicker.SelectedIndex];
            await UpdateStatsAsync();
        }
    }

    private async Task CreateNewPackAsync()
    {
        string? packName = await DisplayPromptAsync(
            "New Pack",
            "Enter pack name:",
            "Create",
            "Cancel");

        if (string.IsNullOrWhiteSpace(packName)) return;

        // Check if exists
        if (_packNames.Contains(packName.Trim()))
        {
            await DisplayAlert("Exists", $"Pack '{packName.Trim()}' already exists.", "OK");
            return;
        }

        // Ask for first category and prompt
        string? groupName = await DisplayPromptAsync(
            "First Category",
            "Enter first category name:",
            "Next",
            "Cancel");

        if (string.IsNullOrWhiteSpace(groupName)) return;

        string? text = await DisplayPromptAsync(
            "First Prompt",
            "Enter the first prompt:",
            "Create",
            "Cancel");

        if (string.IsNullOrWhiteSpace(text)) return;

        // Create the prompt (which creates the pack)
        var prompt = new PromptItem
        {
            PackName = packName.Trim(),
            GroupName = groupName.Trim(),
            Text = text.Trim(),
            Rating = 3,
            Probability = 1.0,
            IsActive = true
        };
        await _prompts.AddPromptAsync(prompt);

        // Refresh and select new pack
        _selectedPack = packName.Trim();
        await LoadPacksAsync();
        
        int newIdx = _packNames.IndexOf(_selectedPack);
        if (newIdx >= 0) _packPicker.SelectedIndex = newIdx;
        
        await DisplayAlert("Created", $"Pack '{packName.Trim()}' created!", "OK");
    }

    private async Task UpdateStatsAsync()
    {
        var (total, active, groups) = await _prompts.GetPackStatsAsync(_selectedPack);
        _statsLabel.Text = $"({active} active / {total} total, {groups} groups)";
    }

    private async void OnGenerateClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedPack))
        {
            await DisplayAlert("Select Pack", "Please select a prompt pack first.", "OK");
            return;
        }

        // Parse min/max count
        if (!int.TryParse(_minCountEntry.Text, out int minCount) || minCount < 1)
        {
            minCount = 5;
            _minCountEntry.Text = "5";
        }
        if (!int.TryParse(_maxCountEntry.Text, out int maxCount) || maxCount < minCount)
        {
            maxCount = minCount;
            _maxCountEntry.Text = maxCount.ToString();
        }
        
        minCount = Math.Max(1, Math.Min(minCount, 50));
        maxCount = Math.Max(minCount, Math.Min(maxCount, 50));

        // Disable button during generation
        _generateBtn.IsEnabled = false;
        _generateBtn.Text = "⏳ Generating...";

        try
        {
            var prompts = await _prompts.GeneratePromptsAsync(_selectedPack, minCount, maxCount);
            _lastGeneratedPrompts = prompts;
            DisplayResults(prompts);
            _copyTextBtn.IsEnabled = prompts.Count > 0;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Generation failed: {ex.Message}", "OK");
        }
        finally
        {
            _generateBtn.IsEnabled = true;
            _generateBtn.Text = "🎲 Generate";
        }
    }

    private async void OnCopyTextClicked(object? sender, EventArgs e)
    {
        if (_lastGeneratedPrompts.Count == 0)
        {
            await DisplayAlert("No Prompts", "Generate prompts first.", "OK");
            return;
        }

        // Concatenate all prompts as continuous text, separated by newlines
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join("\n", _lastGeneratedPrompts.Select(p => p.Text)));

        // Append inspiration ideas if category selected and count > 0
        if (_selectedInspirationCategory != "None" && !string.IsNullOrEmpty(_selectedInspirationCategory))
        {
            if (!int.TryParse(_inspirationMinEntry.Text, out int inspMin)) inspMin = 0;
            if (!int.TryParse(_inspirationMaxEntry.Text, out int inspMax)) inspMax = inspMin;
            inspMin = Math.Max(0, inspMin);
            inspMax = Math.Max(inspMin, inspMax);

            if (inspMax > 0)
            {
                // Get random ideas from the category
                var ideas = await _ideas.GetIdeasByCategoryAsync(_auth.CurrentUsername, _selectedInspirationCategory);
                ideas = ideas.Where(i => i.Status != 3).ToList(); // Exclude archived
                
                if (ideas.Count > 0)
                {
                    int count = inspMin == inspMax ? inspMin : new Random().Next(inspMin, inspMax + 1);
                    count = Math.Min(count, ideas.Count);
                    
                    if (count > 0)
                    {
                        var randomIdeas = ideas.OrderBy(_ => Guid.NewGuid()).Take(count).ToList();
                        _lastInspirationIdeas = randomIdeas;
                        
                        sb.AppendLine();
                        sb.AppendLine("---");
                        sb.AppendLine("Feel free to work these directly, be inspired by, or use variations of them in writing the story:");
                        sb.AppendLine();
                        foreach (var idea in randomIdeas)
                        {
                            sb.AppendLine($"- {idea.Title}");
                        }
                    }
                }
            }
        }

        await Clipboard.SetTextAsync(sb.ToString().TrimEnd());

        // Visual feedback
        var originalText = _copyTextBtn.Text;
        var originalColor = _copyTextBtn.BackgroundColor;
        _copyTextBtn.Text = "✓";
        _copyTextBtn.BackgroundColor = Color.FromArgb("#1565C0");
        await Task.Delay(800);
        _copyTextBtn.Text = originalText;
        _copyTextBtn.BackgroundColor = originalColor;
    }

    private void DisplayResults(List<PromptItem> prompts)
    {
        _resultsStack.Children.Clear();

        if (prompts.Count == 0)
        {
            _resultsCountLabel.Text = "";
            _resultsStack.Children.Add(new Label
            {
                Text = "No prompts available for this pack.\nAdd some prompts in settings!",
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 20)
            });
            return;
        }

        // Show count with scroll hint
        _resultsCountLabel.Text = $"— {prompts.Count} generated (scroll for more ↓)";

        int index = 1;
        foreach (var prompt in prompts)
        {
            var card = BuildPromptCard(prompt, index++);
            _resultsStack.Children.Add(card);
        }

        // Add regenerate hint
        _resultsStack.Children.Add(new Label
        {
            Text = "💡 Tap Generate again for different prompts\n📋 Tap a prompt to copy it",
            FontSize = 11,
            TextColor = Color.FromArgb("#999"),
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(0, 12, 0, 0)
        });
    }

    private Frame BuildPromptCard(PromptItem prompt, int index)
    {
        // Color based on group (hash the group name for consistent color)
        var groupColors = new[] { "#E3F2FD", "#E8F5E9", "#FFF3E0", "#F3E5F5", "#E0F7FA", "#FCE4EC" };
        int colorIndex = Math.Abs(prompt.GroupName.GetHashCode()) % groupColors.Length;
        
        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Color.FromArgb(groupColors[colorIndex]),
            HasShadow = false,
            BorderColor = prompt.Priority > 0 ? Color.FromArgb("#FF9800") : Color.FromArgb("#E0E0E0")
        };

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = 40 },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            },
            ColumnSpacing = 12
        };

        // Index number and priority indicator
        var indexStack = new VerticalStackLayout { Spacing = 2 };
        
        var indexLabel = new Label
        {
            Text = $"#{index}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#5B63EE"),
            HorizontalOptions = LayoutOptions.Center
        };
        indexStack.Children.Add(indexLabel);

        if (prompt.Priority > 0)
        {
            var priorityLabel = new Label
            {
                Text = $"P{prompt.Priority}",
                FontSize = 9,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#FF9800"),
                HorizontalOptions = LayoutOptions.Center
            };
            indexStack.Children.Add(priorityLabel);
        }

        Grid.SetColumn(indexStack, 0);
        grid.Children.Add(indexStack);

        // Prompt text and group info
        var textStack = new VerticalStackLayout { Spacing = 4 };
        
        var promptText = new Label
        {
            Text = prompt.Text,
            FontSize = 14,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap
        };
        textStack.Children.Add(promptText);

        var groupLabel = new Label
        {
            Text = $"📁 {prompt.GroupName}",
            FontSize = 10,
            TextColor = Color.FromArgb("#666")
        };
        textStack.Children.Add(groupLabel);

        Grid.SetColumn(textStack, 1);
        grid.Children.Add(textStack);

        // Right side: rating + settings button
        var rightStack = new VerticalStackLayout { Spacing = 4 };
        
        var ratingLabel = new Label
        {
            Text = new string('★', prompt.Rating) + new string('☆', 5 - prompt.Rating),
            FontSize = 12,
            TextColor = Color.FromArgb("#FFC107"),
            HorizontalOptions = LayoutOptions.End
        };
        rightStack.Children.Add(ratingLabel);

        // Settings button for prob/priority
        var settingsBtn = new Button
        {
            Text = "⚙️",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#999"),
            WidthRequest = 30,
            HeightRequest = 24,
            Padding = 0,
            FontSize = 12
        };
        var capturedPrompt = prompt;
        settingsBtn.Clicked += async (s, e) => await ShowPromptSettingsAsync(capturedPrompt);
        rightStack.Children.Add(settingsBtn);

        Grid.SetColumn(rightStack, 2);
        grid.Children.Add(rightStack);

        frame.Content = grid;

        // Copy on tap
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) =>
        {
            await Clipboard.SetTextAsync(prompt.Text);
            
            // Visual feedback
            var originalColor = frame.BackgroundColor;
            frame.BackgroundColor = Color.FromArgb("#C8E6C9");
            await Task.Delay(300);
            frame.BackgroundColor = originalColor;
        };
        frame.GestureRecognizers.Add(tapGesture);

        return frame;
    }

    private async Task ShowPromptSettingsAsync(PromptItem prompt)
    {
        var options = new[]
        {
            $"Probability: {prompt.Probability:P0}",
            $"Priority: {(prompt.Priority == 0 ? "None" : $"P{prompt.Priority}")}",
            "Cancel"
        };

        var result = await DisplayActionSheet($"Settings: {prompt.GroupName}", null, null, options);

        if (result == null || result == "Cancel") return;

        if (result.StartsWith("Probability"))
        {
            var probOptions = new[] { "25%", "50%", "75%", "100%", "Custom..." };
            var probResult = await DisplayActionSheet("Set Probability", "Cancel", null, probOptions);
            
            if (probResult == null || probResult == "Cancel") return;
            
            double prob;
            if (probResult == "Custom...")
            {
                var customValue = await DisplayPromptAsync(
                    "Custom Probability",
                    "Enter probability (0-100 or 0.0-1.0):",
                    "Set",
                    "Cancel",
                    initialValue: (prompt.Probability * 100).ToString("0"),
                    keyboard: Keyboard.Numeric);
                
                if (string.IsNullOrWhiteSpace(customValue)) return;
                
                if (!double.TryParse(customValue.Replace("%", ""), out prob)) return;
                
                if (prob > 1) prob = prob / 100.0; // Convert 75 to 0.75
                prob = Math.Clamp(prob, 0.0, 1.0);
            }
            else
            {
                prob = double.Parse(probResult.Replace("%", "")) / 100.0;
            }
            
            await _prompts.UpdatePromptAsync(prompt.Id, p => p.Probability = prob);
            prompt.Probability = prob;
            await DisplayAlert("Updated", $"Probability set to {prob:P0}", "OK");
        }
        else if (result.StartsWith("Priority"))
        {
            var prioOptions = new[] { "None", "P1 (highest)", "P2", "P3", "Custom..." };
            var prioResult = await DisplayActionSheet("Set Priority", "Cancel", null, prioOptions);
            
            if (prioResult == null || prioResult == "Cancel") return;
            
            int prio;
            if (prioResult == "Custom...")
            {
                var customValue = await DisplayPromptAsync(
                    "Custom Priority",
                    "Enter priority (0 = none, 1 = highest):",
                    "Set",
                    "Cancel",
                    initialValue: prompt.Priority.ToString(),
                    keyboard: Keyboard.Numeric);
                
                if (string.IsNullOrWhiteSpace(customValue)) return;
                
                if (!int.TryParse(customValue, out prio)) return;
                prio = Math.Max(0, prio);
            }
            else
            {
                prio = prioResult == "None" ? 0 : int.Parse(prioResult.Substring(1, 1));
            }
            
            await _prompts.UpdatePromptAsync(prompt.Id, p => p.Priority = prio);
            prompt.Priority = prio;
            await DisplayAlert("Updated", $"Priority set to {(prio == 0 ? "None" : $"P{prio}")}", "OK");
        }
    }

    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var options = new[]
        {
            "📦 Browse Prompts",
            "🔢 Group Limits",
            "✏️ Rename Pack"
        };

        var result = await DisplayActionSheet("Prompt Settings", "Cancel", null, options);

        if (result == null || result == "Cancel") return;

        if (result == "📦 Browse Prompts")
        {
            await Navigation.PushAsync(new PromptsBrowsePage(_prompts));
        }
        else if (result == "🔢 Group Limits")
        {
            await ShowGroupLimitsAsync();
        }
        else if (result == "✏️ Rename Pack")
        {
            await RenamePackAsync();
        }
    }

    private async Task ShowGroupLimitsAsync()
    {
        if (string.IsNullOrEmpty(_selectedPack))
        {
            await DisplayAlert("No Pack", "Select a pack first.", "OK");
            return;
        }

        // Sync group settings to ensure all groups have entries
        await _prompts.SyncGroupSettingsAsync(_selectedPack);

        // Get all groups for this pack
        var groups = await _prompts.GetGroupsAsync(_selectedPack);
        var groupNames = await _prompts.GetGroupNamesAsync(_selectedPack);

        // Add any groups that exist in prompts but not in settings yet
        foreach (var name in groupNames)
        {
            if (!groups.Any(g => g.GroupName == name))
            {
                await _prompts.GetOrCreateGroupAsync(_selectedPack, name);
            }
        }
        groups = await _prompts.GetGroupsAsync(_selectedPack);

        if (groups.Count == 0)
        {
            await DisplayAlert("No Groups", "This pack has no groups yet.", "OK");
            return;
        }

        // Build list for action sheet style selection
        var groupList = groups.OrderBy(g => g.GroupName).ToList();
        var options = groupList.Select(g => $"{g.GroupName} (max: {g.MaxPerGeneration})").ToArray();

        var selected = await DisplayActionSheet(
            $"Group Limits: {_selectedPack}\nTap a group to change its limit",
            "Done",
            null,
            options);

        if (selected == null || selected == "Done")
            return;

        // Find which group was selected
        var selectedIndex = Array.IndexOf(options, selected);
        if (selectedIndex < 0 || selectedIndex >= groupList.Count)
            return;

        var selectedGroup = groupList[selectedIndex];

        // Ask for new limit
        var limitOptions = new[] { "1", "2", "3", "4", "5" };
        var newLimit = await DisplayActionSheet(
            $"Max per generation for '{selectedGroup.GroupName}'",
            "Cancel",
            null,
            limitOptions);

        if (newLimit == null || newLimit == "Cancel")
        {
            // Go back to group list
            await ShowGroupLimitsAsync();
            return;
        }

        if (int.TryParse(newLimit, out int max))
        {
            await _prompts.SetGroupMaxAsync(_selectedPack, selectedGroup.GroupName, max);
            await DisplayAlert("Saved", $"{selectedGroup.GroupName} max set to {max}", "OK");
            
            // Show list again for more edits
            await ShowGroupLimitsAsync();
        }
    }

    private async Task AddNewPromptAsync()
    {
        // Get pack name
        string? packName = _selectedPack;
        if (string.IsNullOrEmpty(packName))
        {
            packName = await DisplayPromptAsync("Pack Name", "Enter pack name:", initialValue: "Writing");
            if (string.IsNullOrWhiteSpace(packName)) return;
        }

        // Get group name
        var existingGroups = await _prompts.GetGroupNamesAsync(packName);
        string? groupName;
        
        if (existingGroups.Count > 0)
        {
            var groupOptions = existingGroups.Concat(new[] { "➕ New Group" }).ToArray();
            groupName = await DisplayActionSheet("Select Group", "Cancel", null, groupOptions);
            
            if (groupName == null || groupName == "Cancel") return;
            
            if (groupName == "➕ New Group")
            {
                groupName = await DisplayPromptAsync("New Group", "Enter group name:");
                if (string.IsNullOrWhiteSpace(groupName)) return;
            }
        }
        else
        {
            groupName = await DisplayPromptAsync("Group Name", "Enter group name:");
            if (string.IsNullOrWhiteSpace(groupName)) return;
        }

        // Get prompt text
        string? text = await DisplayPromptAsync("Prompt Text", "Enter the prompt:");
        if (string.IsNullOrWhiteSpace(text)) return;

        // Ask for probability
        var probOptions = new[] { "100% (default)", "75%", "50%", "25%", "Custom..." };
        var probChoice = await DisplayActionSheet("Probability", "Cancel", null, probOptions);
        if (probChoice == null || probChoice == "Cancel") return;

        double probability = 1.0;
        if (probChoice == "Custom...")
        {
            var customProb = await DisplayPromptAsync("Custom Probability", "Enter probability (0-100):", "Set", "Cancel", initialValue: "100", keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(customProb)) return;
            if (double.TryParse(customProb.Replace("%", ""), out double val))
            {
                probability = val > 1 ? val / 100.0 : val;
                probability = Math.Clamp(probability, 0.0, 1.0);
            }
        }
        else
        {
            probability = double.Parse(probChoice.Split('%')[0]) / 100.0;
        }

        // Ask for priority
        var prioOptions = new[] { "None (default)", "P1 (highest)", "P2", "P3", "Custom..." };
        var prioChoice = await DisplayActionSheet("Priority", "Cancel", null, prioOptions);
        if (prioChoice == null || prioChoice == "Cancel") return;

        int priority = 0;
        if (prioChoice == "Custom...")
        {
            var customPrio = await DisplayPromptAsync("Custom Priority", "Enter priority (0 = none, 1 = highest):", "Set", "Cancel", initialValue: "0", keyboard: Keyboard.Numeric);
            if (string.IsNullOrWhiteSpace(customPrio)) return;
            if (int.TryParse(customPrio, out int val))
            {
                priority = Math.Max(0, val);
            }
        }
        else if (prioChoice.StartsWith("P"))
        {
            priority = int.Parse(prioChoice.Substring(1, 1));
        }

        // Ask whether to add examples
        string? examples = null;
        bool addExamples = await DisplayAlert("Add Examples?", "Would you like to add examples for this prompt?", "Yes", "No");
        if (addExamples)
        {
            var examplesList = new List<string>();
            while (true)
            {
                string? example = await DisplayPromptAsync(
                    "Add Example", 
                    $"Example {examplesList.Count + 1} (leave empty to finish):",
                    "Add", "Done");
                
                if (string.IsNullOrWhiteSpace(example)) break;
                examplesList.Add(example.Trim());
            }
            
            if (examplesList.Count > 0)
            {
                examples = string.Join("\n", examplesList);
            }
        }

        // Create and save prompt
        var prompt = new PromptItem
        {
            PackName = packName.Trim(),
            GroupName = groupName.Trim(),
            Text = text.Trim(),
            Rating = 3,
            Probability = probability,
            SecondFromGroupProbability = 0.3,
            IsActive = true,
            Priority = priority,
            Examples = examples
        };

        await _prompts.AddPromptAsync(prompt);

        // Ask whether to also log to Ideas (with full UI)
        bool logToIdeas = await DisplayAlert(
            "Log to Ideas?", 
            "Also save this prompt as an idea?", 
            "Yes", "No");

        if (logToIdeas)
        {
            var idea = await _ideaLogger.LogIdeaAsync(
                this,
                _auth.CurrentUsername,
                text.Trim(),
                $"Prompts: {packName.Trim()}");

            if (idea != null)
            {
                await DisplayAlert("Added", "Prompt added and saved to Ideas!", "OK");
            }
            else
            {
                await DisplayAlert("Added", "Prompt added (idea logging cancelled).", "OK");
            }
        }
        else
        {
            await DisplayAlert("Added", "Prompt added successfully!", "OK");
        }
        
        // Refresh packs in case new one was created
        await LoadPacksAsync();
        await UpdateStatsAsync();
    }

    private async Task RenamePackAsync()
    {
        if (string.IsNullOrEmpty(_selectedPack))
        {
            await DisplayAlert("No Pack", "Select a pack first.", "OK");
            return;
        }

        string? newName = await DisplayPromptAsync(
            "Rename Pack",
            $"Enter new name for '{_selectedPack}':",
            "Rename",
            "Cancel",
            initialValue: _selectedPack);

        if (string.IsNullOrWhiteSpace(newName) || newName == _selectedPack) return;

        // Check if name already exists
        if (_packNames.Contains(newName.Trim()))
        {
            await DisplayAlert("Name Exists", $"A pack named '{newName.Trim()}' already exists.", "OK");
            return;
        }

        await _prompts.RenamePackAsync(_selectedPack, newName.Trim());
        _selectedPack = newName.Trim();
        
        await LoadPacksAsync();
        await DisplayAlert("Renamed", $"Pack renamed to '{newName.Trim()}'", "OK");
    }
}

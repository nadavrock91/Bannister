using Bannister.Models;
using Bannister.Services;
using System.Text;

namespace Bannister.Views;

public class PromptTechniqueLabPage : Bannister.Views.ModeThemedPage
{
    private static readonly string[] Statuses =
        { "Testing", "Promising", "Confirmed", "Abandoned" };

    private readonly AuthService _auth;
    private readonly PromptTechniqueService _promptTechniques;
    private readonly HorizontalStackLayout _tabs;
    private readonly VerticalStackLayout _body;
    private readonly Editor _globalPrefixEditor;
    private readonly Frame _globalPrefixFrame;
    private readonly List<Button> _copyPromptButtons = new();
    private string _activeTab = "Techniques";
    private List<PromptTechnique> _techniques = new();
    private int _selectedRating = 5;
    private int? _expandedTechniqueId;

    public PromptTechniqueLabPage(
        AuthService auth,
        PromptTechniqueService promptTechniques)
    {
        _auth = auth;
        _promptTechniques = promptTechniques;

        Title = "Prompt Technique Lab";


        _tabs = new HorizontalStackLayout { Spacing = 8 };
        _body = new VerticalStackLayout { Spacing = 14 };
        _globalPrefixEditor = (new Editor
        {
            Placeholder = "Enter your video idea / starting prompt here...",
            HeightRequest = 120,
            AutoSize = EditorAutoSizeOption.TextChanges
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2);
        _globalPrefixEditor.TextChanged += (_, _) => UpdateCopyPromptButtons();
        _globalPrefixFrame = CreateFrame(_globalPrefixEditor);

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void BuildUI()
    {
        var main = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        main.Children.Add((new Label
        {
            Text = "Prompt Technique Lab",
            FontSize = 26,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.InfoText, ThemeKey.PageText1));
        main.Children.Add(_tabs);
        main.Children.Add(_body);

        Content = new ScrollView { Content = main };
        BuildTabs();
    }

    private void BuildTabs()
    {
        _tabs.Children.Clear();
        foreach (var tab in new[] { "Techniques", "Log Result", "Stats" })
        {
            var isActive = tab == _activeTab;
            var button = (new Button
            {
                Text = tab,
                CornerRadius = 8,
                FontSize = 13,
                HeightRequest = 40,
                Padding = new Thickness(14, 0)
            }).ThemeColor(VisualElement.BackgroundColorProperty, isActive ? ThemeKey.ButtonPrimaryBackground : ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, isActive ? ThemeKey.OnColorText : ThemeKey.InfoText);
            button.Clicked += async (_, _) =>
            {
                _activeTab = tab;
                BuildTabs();
                await RefreshAsync();
            };
            _tabs.Children.Add(button);
        }
    }

    private async Task RefreshAsync()
    {
        _techniques = await _promptTechniques.GetTechniquesAsync(_auth.CurrentUsername);
        _body.Children.Clear();

        switch (_activeTab)
        {
            case "Log Result":
                await BuildLogResultTabAsync();
                break;
            case "Stats":
                await BuildStatsTabAsync();
                break;
            default:
                BuildTechniquesTab();
                break;
        }
    }

    private void BuildTechniquesTab()
    {
        _copyPromptButtons.Clear();
        _body.Children.Add(CreateSectionTitle("Techniques"));
        _body.Children.Add(_globalPrefixFrame);

        if (_techniques.Count == 0)
            _body.Children.Add(CreateMutedLabel("No techniques yet."));

        foreach (var technique in _techniques)
            _body.Children.Add(CreateTechniqueCard(technique));

        _body.Children.Add(CreateAddTechniqueCard());
    }

    private View CreateTechniqueCard(PromptTechnique technique)
    {
        var stack = new VerticalStackLayout { Spacing = 8 };
        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        header.Add((new Label
        {
            Text = technique.Title,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1), 0, 0);
        header.Add(CreateStatusBadge(technique.Status), 1, 0);
        stack.Children.Add(header);
        stack.Children.Add((new Label
        {
            Text = technique.Description,
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2));
        var copyPrompt = CreateActionButton("Copy Prompt", ThemeKey.ButtonPrimaryBackground);
        copyPrompt.IsEnabled = HasGlobalPrefix();
        copyPrompt.Clicked += async (_, _) => await CopyTechniquePromptAsync(technique);
        _copyPromptButtons.Add(copyPrompt);
        stack.Children.Add(copyPrompt);

        var frame = CreateFrame(stack);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            _expandedTechniqueId = _expandedTechniqueId == technique.Id
                ? null
                : technique.Id;
            await RefreshAsync();
        };
        frame.GestureRecognizers.Add(tap);

        if (_expandedTechniqueId == technique.Id)
            stack.Children.Add(CreateTechniqueEditForm(technique));

        return frame;
    }

    private View CreateTechniqueEditForm(PromptTechnique technique)
    {
        var title = CreateEntry("Title", technique.Title);
        var description = (new Editor
        {
            Text = technique.Description,
            HeightRequest = 100,
            AutoSize = EditorAutoSizeOption.TextChanges
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText);
        var promptSuffix = (new Editor
        {
            Text = technique.PromptSuffix,
            Placeholder = "Prompt suffix",
            HeightRequest = 120,
            AutoSize = EditorAutoSizeOption.TextChanges
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2);
        var status = CreateStatusPicker(technique.Status);

        var save = CreateActionButton("Save", ThemeKey.SuccessDarkButtonBackground);
        save.Clicked += async (_, _) =>
        {
            technique.Title = title.Text?.Trim() ?? "";
            technique.Description = description.Text?.Trim() ?? "";
            technique.PromptSuffix = promptSuffix.Text?.Trim() ?? "";
            technique.Status = status.SelectedItem?.ToString() ?? "Testing";
            await _promptTechniques.SaveTechniqueAsync(technique);
            _expandedTechniqueId = null;
            await RefreshAsync();
        };

        var delete = CreateActionButton("Delete", ThemeKey.ErrorDarkButtonBackground);
        delete.Clicked += async (_, _) =>
        {
            var confirm = await DisplayAlert(
                "Delete Technique",
                $"Delete {technique.Title}?",
                "Delete",
                "Cancel");
            if (!confirm) return;

            await _promptTechniques.DeleteTechniqueAsync(technique.Id);
            _expandedTechniqueId = null;
            await RefreshAsync();
        };

        return new VerticalStackLayout
        {
            Spacing = 8,
            Children =
            {
                title,
                description,
                promptSuffix,
                status,
                new HorizontalStackLayout { Spacing = 8, Children = { save, delete } }
            }
        };
    }

    private View CreateAddTechniqueCard()
    {
        var title = CreateEntry("Technique title", "");
        var description = (new Editor
        {
            Placeholder = "Description",
            HeightRequest = 120,
            AutoSize = EditorAutoSizeOption.TextChanges
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2);
        var promptSuffix = (new Editor
        {
            Placeholder = "Prompt suffix",
            HeightRequest = 120,
            AutoSize = EditorAutoSizeOption.TextChanges
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2);
        var status = CreateStatusPicker("Testing");

        var save = CreateActionButton("Save Technique", ThemeKey.ButtonPrimaryBackground);
        save.Clicked += async (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(title.Text))
            {
                await DisplayAlert("Missing Title", "Add a title first.", "OK");
                return;
            }

            await _promptTechniques.SaveTechniqueAsync(new PromptTechnique
            {
                Username = _auth.CurrentUsername,
                Title = title.Text.Trim(),
                Description = description.Text?.Trim() ?? "",
                PromptSuffix = promptSuffix.Text?.Trim() ?? "",
                Status = status.SelectedItem?.ToString() ?? "Testing",
                CreatedDate = DateTime.UtcNow
            });
            await RefreshAsync();
        };

        return CreateFrame(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                CreateSectionTitle("Add Technique"),
                title,
                description,
                promptSuffix,
                status,
                save
            }
        });
    }

    private async Task BuildLogResultTabAsync()
    {
        _body.Children.Add(CreateSectionTitle("Log Result"));

        var activeTechniques = _techniques
            .Where(x => x.Status != "Abandoned")
            .ToList();
        var techniquePicker = (new Picker
        {
            Title = "Technique",
            ItemDisplayBinding = new Binding(nameof(PromptTechnique.Title)),
            ItemsSource = activeTechniques
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(Picker.TextColorProperty, ThemeKey.InputText);

        var ratingLabel = (new Label
        {
            Text = $"Quality: {RatingName(_selectedRating)}",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1);
        var ratingRow = new HorizontalStackLayout { Spacing = 8 };
        foreach (var option in new[]
        {
            ("Terrible", 1, ThemeKey.ErrorDarkButtonBackground),
            ("Usable", 5, ThemeKey.WarningHoverButtonBackground),
            ("Extraordinary", 10, ThemeKey.SuccessDarkButtonBackground)
        })
        {
            var rating = option.Item2;
            var color = option.Item3;
            var button = (new Button
            {
                Text = option.Item1,
                HeightRequest = 40,
                Padding = new Thickness(12, 0),
                CornerRadius = 6
            }).ThemeColor(Button.TextColorProperty, rating == _selectedRating ? ThemeKey.OnColorText : ThemeKey.CardText1)
                .ThemeColor(VisualElement.BackgroundColorProperty, rating == _selectedRating ? color : ThemeKey.NeutralButtonBackground);
            button.Clicked += (_, _) =>
            {
                _selectedRating = rating;
                ratingLabel.Text = $"Quality: {RatingName(_selectedRating)}";
                foreach (var child in ratingRow.Children.OfType<Button>())
                {
                    var childOption = child.Text switch
                    {
                        "Terrible" => (value: 1, selectedColor: ThemeKey.ErrorDarkButtonBackground),
                        "Extraordinary" => (value: 10, selectedColor: ThemeKey.SuccessDarkButtonBackground),
                        _ => (value: 5, selectedColor: ThemeKey.WarningHoverButtonBackground)
                    };
                    var isSelected = childOption.value == _selectedRating;
                    child.ThemeColor(VisualElement.BackgroundColorProperty, isSelected ? childOption.selectedColor : ThemeKey.NeutralButtonBackground);
                    child.ThemeColor(Button.TextColorProperty, isSelected ? ThemeKey.OnColorText : ThemeKey.CardText1);
                }
            };
            ratingRow.Children.Add(button);
        }

        var save = CreateActionButton("Save Result", ThemeKey.ButtonPrimaryBackground);
        save.Clicked += async (_, _) =>
        {
            if (techniquePicker.SelectedItem is not PromptTechnique technique)
            {
                await DisplayAlert("Choose Technique", "Pick a technique first.", "OK");
                return;
            }

            await _promptTechniques.SaveResultAsync(new TechniqueResult
            {
                Username = _auth.CurrentUsername,
                TechniqueId = technique.Id,
                QualityRating = _selectedRating,
                CreatedDate = DateTime.UtcNow
            });

            _selectedRating = 5;
            await RefreshAsync();
        };

        _body.Children.Add(CreateFrame(new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                techniquePicker,
                ratingLabel,
                ratingRow,
                save
            }
        }));

        await BuildRecentResultsAsync();
    }

    private async Task BuildRecentResultsAsync()
    {
        _body.Children.Add(CreateSectionTitle("Recent Results"));

        var results = (await _promptTechniques.GetResultsAsync(_auth.CurrentUsername))
            .Take(10)
            .ToList();
        if (results.Count == 0)
        {
            _body.Children.Add(CreateMutedLabel("No results logged yet."));
            return;
        }

        var techniqueLookup = _techniques.ToDictionary(x => x.Id, x => x.Title);
        foreach (var result in results)
        {
            var techniqueTitle = techniqueLookup.TryGetValue(result.TechniqueId, out var title)
                ? title
                : "Unknown technique";

            _body.Children.Add(CreateFrame(new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    (new Label
                    {
                        Text = $"{techniqueTitle} - {result.QualityRating}/10",
                        FontSize = 15,
                        FontAttributes = FontAttributes.Bold
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1),
                    (new Label
                    {
                        Text = result.CreatedDate.ToLocalTime().ToString("MMM d, yyyy h:mm tt"),
                        FontSize = 12
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3),
                    (new Label
                    {
                        Text = result.Notes,
                        FontSize = 13,
                        LineBreakMode = LineBreakMode.WordWrap
                    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1)
                }
            }));
        }
    }

    private async Task BuildStatsTabAsync()
    {
        _body.Children.Add(CreateSectionTitle("Stats"));

        var stats = await _promptTechniques.GetStatsAsync(_auth.CurrentUsername);
        var headers = new List<string>
        {
            "Technique",
            "Status",
            "Total",
            "Terrible",
            "Usable",
            "Extraordinary"
        };
        var rows = stats.Select(x => new List<string>
        {
            x.Title,
            x.Status,
            x.TotalGenerations.ToString(),
            x.TotalTerrible.ToString(),
            x.TotalUsable.ToString(),
            x.TotalExtraordinary.ToString()
        }).ToList();

        var dataGrid = DataGridView.Create(headers, rows)
            .WithHeaderStyle(AppColors.Get(ThemeKey.InfoText), AppColors.Get(ThemeKey.OnColorText))
            .WithAlternateRowColor(AppColors.Get(ThemeKey.RowSelected))
            .WithColumnWidths(100, 260)
            .WithCellPadding(6)
            .WithFontSize(12, 12)
            .WithPageSize(100)
            .Build();

        var export = CreateActionButton("LLM Export", ThemeKey.SuccessDarkButtonBackground);
        export.Clicked += async (_, _) => await CopyExportPromptAsync();

        _body.Children.Add(export);
        _body.Children.Add(dataGrid.ToolbarView);
        _body.Children.Add(dataGrid.GridView);
    }

    private async Task CopyTechniquePromptAsync(PromptTechnique technique)
    {
        if (!HasGlobalPrefix())
            return;

        await Clipboard.SetTextAsync($"{_globalPrefixEditor.Text?.Trim()}\n\n{technique.PromptSuffix?.Trim() ?? ""}");
        await DisplayAlert("Copied", "Technique prompt copied to clipboard.", "OK");
    }

    private bool HasGlobalPrefix() =>
        !string.IsNullOrWhiteSpace(_globalPrefixEditor.Text);

    private void UpdateCopyPromptButtons()
    {
        var enabled = HasGlobalPrefix();
        foreach (var button in _copyPromptButtons)
            button.IsEnabled = enabled;
    }

    private static string RatingName(int rating) => rating switch
    {
        1 => "Terrible",
        10 => "Extraordinary",
        _ => "Usable"
    };

    private async Task CopyExportPromptAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Here is my current prompt technique library for AI video generation:");
        sb.AppendLine();
        foreach (var technique in _techniques)
        {
            sb.AppendLine($"- {technique.Title} ({technique.Status}): {technique.Description}");
        }
        sb.AppendLine();
        sb.AppendLine($"Common failure modes I track: {string.Join(", ", FailureMode.All.Select(x => x.Name))}");
        sb.AppendLine();
        sb.Append("Suggest 5 new prompting techniques I haven't tried yet that might address these failure modes. Focus on techniques for specifying action, timing, speed, and escalation in 10-second video prompts.");

        await Clipboard.SetTextAsync(sb.ToString());
        await DisplayAlert("Copied", "Prompt technique export copied to clipboard.", "OK");
    }

    private static string FormatFailureModes(string ids)
    {
        var lookup = FailureMode.All.ToDictionary(x => x.Id, x => x.Name);
        var names = (ids ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) && lookup.TryGetValue(id, out var name) ? name : "")
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return names.Count == 0 ? "No failures" : string.Join(", ", names);
    }

    private static Picker CreateStatusPicker(string selected)
    {
        var picker = (new Picker
        {
            Title = "Status",
            ItemsSource = Statuses.ToList()
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(Picker.TextColorProperty, ThemeKey.InputText);
        picker.SelectedItem = Statuses.Contains(selected) ? selected : "Testing";
        return picker;
    }

    private static Entry CreateEntry(string placeholder, string text) => (new Entry(){
        Placeholder = placeholder,
        Text = text
    }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InputBackground).ThemeColor(InputView.TextColorProperty, ThemeKey.InputText).ThemeColor(InputView.PlaceholderColorProperty, ThemeKey.CardText2);

    private static Label CreateSectionTitle(string text) => (new Label(){
        Text = text,
        FontSize = 18,
        FontAttributes = FontAttributes.Bold
    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1);

    private static Label CreateMutedLabel(string text) => (new Label(){
        Text = text,
        FontSize = 13
    }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2);

    private static Label CreateStatusBadge(string status) => (new Label(){
        Text = status,
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        Padding = new Thickness(8, 4),
        VerticalOptions = LayoutOptions.Center
    }).ThemeColor(VisualElement.BackgroundColorProperty, StatusColor(status)).ThemeColor(Label.TextColorProperty, ThemeKey.OnColorText, ThemeKey.PageText3);

    private static Button CreateActionButton(string text, ThemeKey color) => (new Button(){
        Text = text,
        CornerRadius = 8,
        HeightRequest = 42,
        Padding = new Thickness(14, 0)
    }).ThemeColor(VisualElement.BackgroundColorProperty, color).ThemeColor(Button.TextColorProperty, ThemeKey.OnColorText);

    private static Frame CreateFrame(View content) => (new Frame(){
        CornerRadius = 8,
        Padding = 14,
        HasShadow = false,
        Content = content
    }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator);

    private static ThemeKey StatusColor(string status) => status switch
    {
        "Confirmed" => ThemeKey.SuccessText,        "Promising" => ThemeKey.InfoText,        "Abandoned" => ThemeKey.NeutralHoverButton,        _ => ThemeKey.WarningStrongText    };
}

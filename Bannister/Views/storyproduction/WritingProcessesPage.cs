using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class WritingProcessesPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly StoryProductionService _storyService;
    private readonly IdeasService? _ideasService;
    private VerticalStackLayout _listStack;

    public WritingProcessesPage(AuthService auth, StoryProductionService storyService, IdeasService? ideasService = null)
    {
        _auth = auth;
        _storyService = storyService;
        _ideasService = ideasService;

        Title = "Writing Processes";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadProcessesAsync();
    }

    private void BuildUI()
    {
        var mainStack = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 16
        };

        mainStack.Children.Add(new Label
        {
            Text = "Writing Processes",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#7B1FA2")
        });

        mainStack.Children.Add(new Label
        {
            Text = "Manage the writing processes you can assign to story projects.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        });

        var addBtn = new Button
        {
            Text = "+ Add Process",
            BackgroundColor = Color.FromArgb("#7B1FA2"),
            TextColor = Colors.White,
            CornerRadius = 8,
            HeightRequest = 44,
            FontAttributes = FontAttributes.Bold,
            HorizontalOptions = LayoutOptions.Start
        };
        addBtn.Clicked += OnAddClicked;
        mainStack.Children.Add(addBtn);

        _listStack = new VerticalStackLayout { Spacing = 8 };
        mainStack.Children.Add(_listStack);

        Content = new ScrollView { Content = mainStack };
    }

    private async Task LoadProcessesAsync()
    {
        _listStack.Children.Clear();

        var processes = await _storyService.GetWritingProcessesAsync(_auth.CurrentUsername);

        if (processes.Count == 0)
        {
            _listStack.Children.Add(new Label
            {
                Text = "No writing processes defined yet. Add one to get started.",
                FontSize = 13,
                TextColor = Color.FromArgb("#999"),
                FontAttributes = FontAttributes.Italic,
                Margin = new Thickness(0, 12)
            });
            return;
        }

        // Also count how many projects use each process
        var projects = await _storyService.GetProjectsAsync(_auth.CurrentUsername);
        var originalProjects = projects.Where(p => p.ParentProjectId == null).ToList();

        foreach (var process in processes)
        {
            int usageCount = originalProjects.Count(p =>
                string.Equals(p.WritingProcess?.Trim(), process.Name, StringComparison.OrdinalIgnoreCase));

            var row = new Frame
            {
                Padding = 12,
                CornerRadius = 8,
                BackgroundColor = Colors.White,
                HasShadow = true,
                BorderColor = Colors.Transparent
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 12
            };

            var infoStack = new VerticalStackLayout { Spacing = 4 };
            infoStack.Children.Add(new Label
            {
                Text = process.Name,
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#333")
            });

            string usageText = usageCount == 1 ? "1 project" : $"{usageCount} projects";
            infoStack.Children.Add(new Label
            {
                Text = usageText,
                FontSize = 12,
                TextColor = Color.FromArgb("#888")
            });

            grid.Add(infoStack, 0, 0);

            var deleteBtn = new Button
            {
                Text = "Delete",
                BackgroundColor = Color.FromArgb("#FFCDD2"),
                TextColor = Color.FromArgb("#C62828"),
                CornerRadius = 8,
                HeightRequest = 36,
                FontSize = 12,
                Padding = new Thickness(12, 0),
                VerticalOptions = LayoutOptions.Center
            };

            var capturedProcess = process;
            var capturedUsageCount = usageCount;
            deleteBtn.Clicked += async (_, _) => await DeleteProcessAsync(capturedProcess, capturedUsageCount);

            grid.Add(deleteBtn, 1, 0);

            row.Content = grid;
            _listStack.Children.Add(row);
        }
    }

    private async void OnAddClicked(object? sender, EventArgs e)
    {
        string? name = await DisplayPromptAsync(
            "New Writing Process",
            "Enter process name:",
            "Add",
            "Cancel",
            placeholder: "e.g., Fable, Video Essay, Documentary...");

        if (string.IsNullOrWhiteSpace(name)) return;

        // Check for duplicate
        var existing = await _storyService.GetWritingProcessesAsync(_auth.CurrentUsername);
        if (existing.Any(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlert("Duplicate", $"A process named '{name.Trim()}' already exists.", "OK");
            return;
        }

        try
        {
            await _storyService.AddWritingProcessAsync(_auth.CurrentUsername, name.Trim());

            // Offer to add as idea under Story Production Processes category
            if (_ideasService != null)
            {
                bool addAsIdea = await DisplayAlert(
                    "Add to Ideas?",
                    $"Add '{name.Trim()}' as an idea under the 'Story Production Processes' category?",
                    "Yes",
                    "No");

                if (addAsIdea)
                {
                    try
                    {
                        await _ideasService.CreateIdeaAsync(
                            _auth.CurrentUsername,
                            name.Trim(),
                            "Story Production Processes");
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Could not add idea", ex.Message, "OK");
                    }
                }
            }

            await LoadProcessesAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await DisplayAlert("Read Only", "Cannot add processes on a secondary device.", "OK");
        }
    }

    private async Task DeleteProcessAsync(WritingProcessDefinition process, int usageCount)
    {
        string message = usageCount > 0
            ? $"Delete '{process.Name}'?\n\nThis process is used by {usageCount} project(s). Those projects will keep their current process label but it won't appear in the defined list."
            : $"Delete '{process.Name}'?";

        bool confirm = await DisplayAlert("Delete Process", message, "Delete", "Cancel");
        if (!confirm) return;

        try
        {
            await _storyService.DeleteWritingProcessAsync(process.Id);
            await LoadProcessesAsync();
        }
        catch (ReadOnlyDatabaseException)
        {
            await DisplayAlert("Read Only", "Cannot delete processes on a secondary device.", "OK");
        }
    }
}

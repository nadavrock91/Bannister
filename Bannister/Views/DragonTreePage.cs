using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Bannister.Views;

[QueryProperty(nameof(GameId), "gameId")]
public class DragonTreePage : ContentPage
{
    private readonly AuthService _auth;
    private readonly DragonService _dragonService;
    
    private string _gameId = "";
    private List<Dragon> _allDragons = new();
    private Dictionary<int, bool> _collapsedDragons = new();
    
    private VerticalStackLayout treeContainer;
    private ScrollView scrollView;

    public string GameId
    {
        get => _gameId;
        set
        {
            _gameId = value;
            OnPropertyChanged();
        }
    }

    public DragonTreePage(AuthService auth, DragonService dragonService)
    {
        _auth = auth;
        _dragonService = dragonService;

        Title = "Dragon Hierarchy";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDragonsAsync();
    }

    private void BuildUI()
    {
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto }, // Header
                new RowDefinition { Height = GridLength.Auto }, // Buttons
                new RowDefinition { Height = GridLength.Star }  // Scrollable tree
            },
            Padding = 16,
            RowSpacing = 12
        };

        // Header
        var lblHeader = new Label
        {
            Text = "🐲 Dragon Hierarchy",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };
        Grid.SetRow(lblHeader, 0);
        mainGrid.Children.Add(lblHeader);

        // Action buttons
        var btnStack = new HorizontalStackLayout { Spacing = 8 };

        var btnAddRoot = new Button
        {
            Text = "+ Add Main Dragon",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        };
        btnAddRoot.Clicked += (s, e) => OnAddDragonClicked(null);
        btnStack.Children.Add(btnAddRoot);

        Grid.SetRow(btnStack, 1);
        mainGrid.Children.Add(btnStack);

        // Tree container in ScrollView
        scrollView = new ScrollView();
        treeContainer = new VerticalStackLayout { Spacing = 8 };
        scrollView.Content = treeContainer;
        
        Grid.SetRow(scrollView, 2);
        mainGrid.Children.Add(scrollView);

        Content = mainGrid;
    }

    private async Task LoadDragonsAsync()
    {
        _allDragons = await _dragonService.GetDragonsAsync(_auth.CurrentUsername, GameId);
        DisplayTree();
    }

    private void DisplayTree()
    {
        treeContainer.Children.Clear();

        var rootDragons = _allDragons
            .Where(d => d.ParentDragonId == null && d.SlainAt == null)
            .OrderBy(d => d.CreatedAt)
            .ToList();

        if (rootDragons.Count == 0)
        {
            treeContainer.Children.Add(new Label
            {
                Text = "No dragons yet. Click '+ Add Main Dragon' to start building your dragon hierarchy!",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                HorizontalOptions = LayoutOptions.Center,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, 40, 0, 0)
            });
            return;
        }

        foreach (var rootDragon in rootDragons)
        {
            treeContainer.Children.Add(BuildDragonView(rootDragon, 0));
        }
    }

    private View BuildDragonView(Dragon dragon, int depth)
    {
        var children = _allDragons
            .Where(d => d.ParentDragonId == dragon.Id && d.SlainAt == null)
            .OrderBy(d => d.CreatedAt)
            .ToList();
        
        bool hasChildren = children.Count > 0;
        bool isCollapsed = _collapsedDragons.ContainsKey(dragon.Id) && _collapsedDragons[dragon.Id];

        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#F44336"),
            Margin = new Thickness(depth * 20, 0, 0, 0),
            HasShadow = depth == 0
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        // Header with collapse button
        var headerStack = new HorizontalStackLayout { Spacing = 8 };

        // Collapse/Expand button (only if has children)
        if (hasChildren)
        {
            var btnToggle = new Button
            {
                Text = isCollapsed ? "▶" : "▼",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#666"),
                FontSize = 16,
                WidthRequest = 30,
                HeightRequest = 30,
                Padding = 0,
                CornerRadius = 4
            };
            btnToggle.Clicked += (s, e) => OnToggleCollapseClicked(dragon);
            headerStack.Children.Add(btnToggle);
        }
        else
        {
            // Spacer for alignment
            headerStack.Children.Add(new BoxView
            {
                WidthRequest = 30,
                BackgroundColor = Colors.Transparent
            });
        }

        // Dragon title
        var titleLabel = new Label
        {
            Text = $"🐲 {dragon.Title}",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#F44336"),
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        headerStack.Children.Add(titleLabel);

        stack.Children.Add(headerStack);

        // Description
        if (!string.IsNullOrEmpty(dragon.Description))
        {
            stack.Children.Add(new Label
            {
                Text = dragon.Description,
                FontSize = 13,
                TextColor = Color.FromArgb("#666"),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }

        // Child count
        if (hasChildren)
        {
            stack.Children.Add(new Label
            {
                Text = $"📊 {children.Count} sub-dragon{(children.Count == 1 ? "" : "s")}",
                FontSize = 11,
                TextColor = Color.FromArgb("#999")
            });
        }

        // Action buttons
        var buttonStack = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };

        var btnAddChild = new Button
        {
            Text = "+ Sub-Dragon",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 11,
            Padding = new Thickness(8, 4)
        };
        btnAddChild.Clicked += (s, e) => OnAddDragonClicked(dragon.Id);
        buttonStack.Children.Add(btnAddChild);

        var btnEdit = new Button
        {
            Text = "✏️ Edit",
            BackgroundColor = Color.FromArgb("#2196F3"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 11,
            Padding = new Thickness(8, 4)
        };
        btnEdit.Clicked += (s, e) => OnEditDragonClicked(dragon);
        buttonStack.Children.Add(btnEdit);

        var btnDelete = new Button
        {
            Text = "🗑️ Delete",
            BackgroundColor = Color.FromArgb("#9E9E9E"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 11,
            Padding = new Thickness(8, 4)
        };
        btnDelete.Clicked += (s, e) => OnDeleteDragonClicked(dragon);
        buttonStack.Children.Add(btnDelete);

        stack.Children.Add(buttonStack);

        frame.Content = stack;

        // Add children recursively (only if not collapsed)
        if (children.Count > 0 && !isCollapsed)
        {
            var childContainer = new VerticalStackLayout { Spacing = 8 };
            childContainer.Children.Add(frame);

            foreach (var child in children)
            {
                childContainer.Children.Add(BuildDragonView(child, depth + 1));
            }

            return childContainer;
        }

        return frame;
    }

    private void OnToggleCollapseClicked(Dragon dragon)
    {
        // Toggle collapsed state
        if (_collapsedDragons.ContainsKey(dragon.Id))
        {
            _collapsedDragons[dragon.Id] = !_collapsedDragons[dragon.Id];
        }
        else
        {
            _collapsedDragons[dragon.Id] = true; // First click = collapse
        }

        // Refresh the tree display
        DisplayTree();
    }

    private async void OnAddDragonClicked(int? parentDragonId)
    {
        string parentText = parentDragonId == null ? "main dragon" : 
            _allDragons.FirstOrDefault(d => d.Id == parentDragonId)?.Title ?? "unknown";

        string title = await DisplayPromptAsync(
            parentDragonId == null ? "Add Main Dragon" : "Add Sub-Dragon",
            parentDragonId == null ? "Enter the name of your main dragon:" : 
                $"Adding sub-dragon under: {parentText}\n\nEnter sub-dragon name:",
            placeholder: "Dragon name",
            maxLength: 100
        );

        if (string.IsNullOrWhiteSpace(title))
            return;

        string description = await DisplayPromptAsync(
            "Dragon Description",
            "Enter a description (optional):",
            placeholder: "What does this dragon represent?",
            maxLength: 500
        );

        // FORCE image selection
        string imagePath = await PickImageAsync();
        
        if (string.IsNullOrEmpty(imagePath))
        {
            await DisplayAlert("Image Required", "You must select an image for the dragon.", "OK");
            return;
        }

        var dragon = new Dragon
        {
            Username = _auth.CurrentUsername,
            Game = GameId,
            ParentDragonId = parentDragonId,
            Title = title.Trim(),
            Description = description?.Trim() ?? "",
            ImagePath = imagePath,
            CreatedAt = DateTime.UtcNow
        };

        await _dragonService.CreateDragonAsync(dragon);
        await LoadDragonsAsync();
        
        await DisplayAlert("Success", $"Dragon '{title}' created!", "OK");
    }

    private async void OnEditDragonClicked(Dragon dragon)
    {
        string title = await DisplayPromptAsync(
            "Edit Dragon",
            "Edit dragon name:",
            initialValue: dragon.Title,
            maxLength: 100
        );

        if (string.IsNullOrWhiteSpace(title))
            return;

        string description = await DisplayPromptAsync(
            "Edit Description",
            "Edit description (optional):",
            initialValue: dragon.Description ?? "",
            maxLength: 500
        );

        // Ask if they want to change the image
        bool changeImage = await DisplayAlert(
            "Change Image?",
            "Do you want to change the dragon image?",
            "Yes",
            "No"
        );

        string imagePath = dragon.ImagePath;
        
        if (changeImage)
        {
            string newImagePath = await PickImageAsync();
            if (!string.IsNullOrEmpty(newImagePath))
            {
                imagePath = newImagePath;
            }
        }

        dragon.Title = title.Trim();
        dragon.Description = description?.Trim() ?? "";
        dragon.ImagePath = imagePath;

        await _dragonService.UpdateDragonAsync(dragon);
        await LoadDragonsAsync();
        
        await DisplayAlert("Success", $"Dragon '{title}' updated!", "OK");
    }

    private async void OnDeleteDragonClicked(Dragon dragon)
    {
        var children = _allDragons.Where(d => d.ParentDragonId == dragon.Id).ToList();
        
        string message = children.Count > 0
            ? $"Delete '{dragon.Title}' and all {children.Count} sub-dragon(s)?"
            : $"Delete '{dragon.Title}'?";

        bool confirm = await DisplayAlert(
            "Delete Dragon",
            message,
            "Delete",
            "Cancel"
        );

        if (!confirm)
            return;

        // Delete recursively
        await DeleteDragonRecursiveAsync(dragon.Id);
        await LoadDragonsAsync();
    }

    private async Task DeleteDragonRecursiveAsync(int dragonId)
    {
        // Get all children
        var children = _allDragons.Where(d => d.ParentDragonId == dragonId).ToList();
        
        // Delete children first
        foreach (var child in children)
        {
            await DeleteDragonRecursiveAsync(child.Id);
        }
        
        // Delete this dragon
        await _dragonService.DeleteDragonAsync(dragonId);
    }

    private async Task<string> PickImageAsync()
    {
        try
        {
            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a dragon image",
                FileTypes = FilePickerFileType.Images
            });

            if (result == null)
                return "";

            // Create ActivityImages folder if it doesn't exist
            string imagesFolder = System.IO.Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
            if (!System.IO.Directory.Exists(imagesFolder))
            {
                System.IO.Directory.CreateDirectory(imagesFolder);
            }

            // Generate unique filename
            string fileName = $"dragon_{Guid.NewGuid()}{System.IO.Path.GetExtension(result.FileName)}";
            string destPath = System.IO.Path.Combine(imagesFolder, fileName);

            // Copy file
            using (var sourceStream = await result.OpenReadAsync())
            using (var destStream = System.IO.File.Create(destPath))
            {
                await sourceStream.CopyToAsync(destStream);
            }

            return fileName; // Return just filename, not full path
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to pick image: {ex.Message}", "OK");
            return "";
        }
    }
}

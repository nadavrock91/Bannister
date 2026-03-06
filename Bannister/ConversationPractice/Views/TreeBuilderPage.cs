using ConversationPractice.Models;
using ConversationPractice.Services;
using Microsoft.Maui.Controls;

namespace ConversationPractice.Views;

/// <summary>
/// Page for building conversation tree branches
/// </summary>
public class TreeBuilderPage : ContentPage
{
    private readonly ConversationService _conversationService;
    private readonly Conversation _conversation;
    private List<ConversationNode> _allNodes = new();
    private Dictionary<int, bool> _collapsedNodes = new(); // Track which nodes are collapsed

    private VerticalStackLayout treeContainer;
    private ScrollView scrollView;

    public TreeBuilderPage(ConversationService conversationService, Conversation conversation)
    {
        _conversationService = conversationService;
        _conversation = conversation;

        Title = $"Build Tree: {conversation.Title}";
        BackgroundColor = Colors.White;

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTreeAsync();
    }

    private void BuildUI()
    {
        // Use Grid for proper layout
        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto }, // Header
                new RowDefinition { Height = GridLength.Auto }, // Description
                new RowDefinition { Height = GridLength.Auto }, // Buttons
                new RowDefinition { Height = GridLength.Star }  // Scrollable tree
            },
            Padding = 16,
            RowSpacing = 12
        };

        // Header
        var lblHeader = new Label
        {
            Text = $"🌳 Conversation Tree Builder",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        };
        Grid.SetRow(lblHeader, 0);
        mainGrid.Children.Add(lblHeader);

        var lblDescription = new Label
        {
            Text = "Build conversation branches. Each branch is a possible response from the other person.",
            FontSize = 13,
            TextColor = Color.FromArgb("#666")
        };
        Grid.SetRow(lblDescription, 1);
        mainGrid.Children.Add(lblDescription);

        // Action buttons
        var btnStack = new HorizontalStackLayout { Spacing = 8 };

        var btnAddRoot = new Button
        {
            Text = "+ Add Opening Line",
            BackgroundColor = Color.FromArgb("#5B63EE"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnAddRoot.Clicked += (s, e) => OnAddNodeClicked(null);
        btnStack.Children.Add(btnAddRoot);

        var btnPractice = new Button
        {
            Text = "▶ Practice",
            BackgroundColor = Color.FromArgb("#4CAF50"),
            TextColor = Colors.White,
            CornerRadius = 8,
            FontSize = 13
        };
        btnPractice.Clicked += OnPracticeClicked;
        btnStack.Children.Add(btnPractice);

        Grid.SetRow(btnStack, 2);
        mainGrid.Children.Add(btnStack);

        // Tree container in ScrollView (takes remaining space)
        scrollView = new ScrollView();
        treeContainer = new VerticalStackLayout { Spacing = 0 };
        scrollView.Content = treeContainer;
        
        Grid.SetRow(scrollView, 3);
        mainGrid.Children.Add(scrollView);

        Content = mainGrid;
    }

    private async Task LoadTreeAsync()
    {
        _allNodes = await _conversationService.GetNodesForConversationAsync(_conversation.Id);
        DisplayTree();
    }

    private void DisplayTree()
    {
        treeContainer.Children.Clear();

        var rootNodes = _allNodes.Where(n => n.ParentNodeId == null).OrderBy(n => n.SortOrder).ToList();

        if (rootNodes.Count == 0)
        {
            treeContainer.Children.Add(new Label
            {
                Text = "No conversation branches yet. Click '+ Add Opening Line' to start!",
                FontSize = 14,
                TextColor = Color.FromArgb("#999"),
                Margin = new Thickness(0, 20, 0, 0),
                HorizontalOptions = LayoutOptions.Center
            });
            return;
        }

        foreach (var rootNode in rootNodes)
        {
            AddNodeAndChildrenToContainer(treeContainer, rootNode, 0);
        }
    }

    private void AddNodeAndChildrenToContainer(VerticalStackLayout container, ConversationNode node, int depth)
    {
        var children = _allNodes.Where(n => n.ParentNodeId == node.Id).OrderBy(n => n.SortOrder).ToList();
        bool hasChildren = children.Count > 0;
        bool isCollapsed = _collapsedNodes.ContainsKey(node.Id) && _collapsedNodes[node.Id];

        // Add the node frame
        container.Children.Add(BuildSingleNodeFrame(node, depth, children.Count, isCollapsed));

        // Add children recursively (only if not collapsed)
        if (hasChildren && !isCollapsed)
        {
            foreach (var child in children)
            {
                AddNodeAndChildrenToContainer(container, child, depth + 1);
            }
        }
    }

    private Frame BuildSingleNodeFrame(ConversationNode node, int depth, int childCount, bool isCollapsed)
    {
        bool hasChildren = childCount > 0;
        bool isTheirTurn = depth % 2 == 0; // Even depth = THEM, Odd depth = YOU

        // Colors: Purple for THEM, Blue for YOU
        var borderColor = isTheirTurn ? Color.FromArgb("#9C27B0") : Color.FromArgb("#2196F3");
        var bgColor = isTheirTurn ? Color.FromArgb("#F3E5F5") : Color.FromArgb("#E3F2FD");

        var frame = new Frame
        {
            Padding = 12,
            CornerRadius = 8,
            BackgroundColor = bgColor,
            BorderColor = borderColor,
            Margin = new Thickness(depth * 24, 4, 0, 4),
            HasShadow = depth == 0
        };

        var stack = new VerticalStackLayout { Spacing = 8 };

        // Turn indicator
        var turnLabel = new Label
        {
            Text = isTheirTurn ? "🗣️ THEM:" : "💬 YOU:",
            FontSize = 10,
            FontAttributes = FontAttributes.Bold,
            TextColor = isTheirTurn ? Color.FromArgb("#7B1FA2") : Color.FromArgb("#1565C0")
        };
        stack.Children.Add(turnLabel);

        // Header with collapse button
        var headerStack = new HorizontalStackLayout { Spacing = 8 };

        // Collapse/Expand button (only if has children)
        if (hasChildren)
        {
            var toggleColor = isTheirTurn ? Color.FromArgb("#9C27B0") : Color.FromArgb("#2196F3");
            var btnToggle = new Button
            {
                Text = isCollapsed ? $"▶ ({childCount})" : "▼",
                BackgroundColor = Colors.Transparent,
                TextColor = toggleColor,
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                HeightRequest = 30,
                Padding = new Thickness(4, 0),
                CornerRadius = 4
            };
            btnToggle.Clicked += (s, e) => OnToggleCollapseClicked(node);
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

        // Node text
        var textLabel = new Label
        {
            Text = node.Text,
            FontSize = 14,
            TextColor = Color.FromArgb("#333"),
            LineBreakMode = LineBreakMode.WordWrap,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.FillAndExpand
        };
        headerStack.Children.Add(textLabel);

        stack.Children.Add(headerStack);

        // Stats row - show children count prominently
        var statsStack = new HorizontalStackLayout { Spacing = 12 };

        if (node.TimesReached > 0)
        {
            statsStack.Children.Add(new Label
            {
                Text = $"🎯 Reached {node.TimesReached}x",
                FontSize = 11,
                TextColor = Color.FromArgb("#4CAF50")
            });
        }

        if (hasChildren)
        {
            statsStack.Children.Add(new Label
            {
                Text = $"💬 {childCount} response{(childCount == 1 ? "" : "s")}",
                FontSize = 11,
                TextColor = Color.FromArgb("#5B63EE"),
                FontAttributes = FontAttributes.Bold
            });
        }

        if (node.Notes != null)
        {
            statsStack.Children.Add(new Label
            {
                Text = $"📝 {node.Notes}",
                FontSize = 11,
                TextColor = Color.FromArgb("#FF9800"),
                LineBreakMode = LineBreakMode.TailTruncation
            });
        }

        if (node.IsTerminal)
        {
            statsStack.Children.Add(new Label
            {
                Text = "🏁 END",
                FontSize = 11,
                TextColor = Color.FromArgb("#F44336"),
                FontAttributes = FontAttributes.Bold
            });
        }

        if (statsStack.Children.Count > 0)
        {
            stack.Children.Add(statsStack);
        }

        // Buttons
        var buttonStack = new HorizontalStackLayout { Spacing = 8, Margin = new Thickness(0, 4, 0, 0) };

        if (!node.IsTerminal)
        {
            var btnAddChild = new Button
            {
                Text = "+ Branch",
                BackgroundColor = Color.FromArgb("#5B63EE"),
                TextColor = Colors.White,
                CornerRadius = 6,
                FontSize = 11,
                Padding = new Thickness(8, 4)
            };
            btnAddChild.Clicked += (s, e) => OnAddNodeClicked(node.Id);
            buttonStack.Children.Add(btnAddChild);
        }

        var btnEdit = new Button
        {
            Text = "✏️ Edit",
            BackgroundColor = Color.FromArgb("#FF9800"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 11,
            Padding = new Thickness(8, 4)
        };
        btnEdit.Clicked += (s, e) => OnEditNodeClicked(node);
        buttonStack.Children.Add(btnEdit);

        var btnDelete = new Button
        {
            Text = "🗑️",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            CornerRadius = 6,
            FontSize = 11,
            Padding = new Thickness(8, 4)
        };
        btnDelete.Clicked += (s, e) => OnDeleteNodeClicked(node);
        buttonStack.Children.Add(btnDelete);

        stack.Children.Add(buttonStack);

        frame.Content = stack;

        return frame;
    }

    private void OnToggleCollapseClicked(ConversationNode node)
    {
        // Toggle collapsed state
        if (_collapsedNodes.ContainsKey(node.Id))
        {
            _collapsedNodes[node.Id] = !_collapsedNodes[node.Id];
        }
        else
        {
            _collapsedNodes[node.Id] = true; // First click = collapse
        }

        // Refresh the tree display
        DisplayTree();
    }

    private async void OnAddNodeClicked(int? parentNodeId)
    {
        string parentText = parentNodeId == null ? "conversation start" : 
            _allNodes.FirstOrDefault(n => n.Id == parentNodeId)?.Text ?? "unknown";

        var text = await DisplayPromptAsync(
            parentNodeId == null ? "Add Opening Line" : "Add Response Branch",
            parentNodeId == null ? "What does the other person say to start?" : 
                $"After: \"{parentText.Substring(0, Math.Min(50, parentText.Length))}...\"\n\nWhat might they say next?",
            placeholder: "Type their response here",
            maxLength: 500
        );

        if (string.IsNullOrWhiteSpace(text))
            return;

        var node = new ConversationNode
        {
            ConversationId = _conversation.Id,
            ParentNodeId = parentNodeId,
            Text = text.Trim()
        };

        await _conversationService.CreateNodeAsync(node);
        await LoadTreeAsync();
    }

    private async void OnEditNodeClicked(ConversationNode node)
    {
        var text = await DisplayPromptAsync(
            "Edit Node",
            "Edit this response:",
            initialValue: node.Text,
            maxLength: 500
        );

        if (string.IsNullOrWhiteSpace(text))
            return;

        node.Text = text.Trim();
        await _conversationService.UpdateNodeAsync(node);
        await LoadTreeAsync();
    }

    private async void OnDeleteNodeClicked(ConversationNode node)
    {
        bool confirm = await DisplayAlert(
            "Delete Node",
            "Delete this response and all its branches?",
            "Delete",
            "Cancel"
        );

        if (confirm)
        {
            await _conversationService.DeleteNodeAsync(node.Id);
            await LoadTreeAsync();
        }
    }

    private async void OnPracticeClicked(object? sender, EventArgs e)
    {
        var rootNodes = _allNodes.Where(n => n.ParentNodeId == null).ToList();

        if (rootNodes.Count == 0)
        {
            await DisplayAlert("No Tree", "Build some conversation branches first!", "OK");
            return;
        }

        await Navigation.PushAsync(new PracticePage(_conversationService, _conversation));
    }
}

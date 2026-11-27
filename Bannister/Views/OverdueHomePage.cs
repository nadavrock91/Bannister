using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public class OverdueHomePage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly CountdownService _countdowns;
    
    private VerticalStackLayout _categoriesLayout;
    private Label _statsLabel;
    
    public OverdueHomePage(AuthService auth, CountdownService countdowns)
    {
        _auth = auth;
        _countdowns = countdowns;
        
        Title = "Overdue";

        
        BuildUI();
    }
    
    private void BuildUI()
    {
        var scrollView = new ScrollView();
        var mainStack = new VerticalStackLayout
        {
            Padding = 24,
            Spacing = 20
        };
        
        // Header
        mainStack.Children.Add((new Label
        {
            Text = "📛 Overdue",
            FontSize = 32,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorButtonBackground, ThemeKey.PageText1, classifyPageText: true));
        
        mainStack.Children.Add((new Label
        {
            Text = "Countdowns that have passed their target",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
        
        // Stats
        _statsLabel = (new Label
        {
            FontSize = 14,
            Margin = new Thickness(0, 0, 0, 10)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorButtonBackground, ThemeKey.PageText1, classifyPageText: true);
        mainStack.Children.Add(_statsLabel);
        
        // Categories section
        mainStack.Children.Add((new Label
        {
            Text = "Categories",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            Margin = new Thickness(0, 10, 0, 0)
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true));
        
        _categoriesLayout = new VerticalStackLayout { Spacing = 12 };
        mainStack.Children.Add(_categoriesLayout);
        
        // Add Category button
        var btnAddCategory = (new Button
        {
            Text = "+ Add Overdue Category",
            CornerRadius = 8,
            HeightRequest = 50,
            Margin = new Thickness(0, 10, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralDarkButton).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnAddCategory.Clicked += OnAddCategoryClicked;
        mainStack.Children.Add(btnAddCategory);
        
        // View All button
        var btnViewAll = (new Button
        {
            Text = "📋 View All Overdue",
            CornerRadius = 8,
            HeightRequest = 50,
            Margin = new Thickness(0, 10, 0, 0)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        btnViewAll.Clicked += OnViewAllClicked;
        mainStack.Children.Add(btnViewAll);
        
        scrollView.Content = mainStack;
        Content = scrollView;
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }
    
    private async Task LoadDataAsync()
    {
        var username = _auth.CurrentUsername;
        
        // Load stats
        var overdue = await _countdowns.GetOverdueCountdownsAsync(username);
        _statsLabel.Text = $"📛 {overdue.Count} overdue countdown{(overdue.Count == 1 ? "" : "s")}";
        
        // Load categories
        _categoriesLayout.Children.Clear();
        var categories = await _countdowns.GetOverdueCategoriesAsync(username);
        
        foreach (var category in categories)
        {
            var countdownsInCategory = await _countdowns.GetOverdueBySubcategoryAsync(username, category);
            var card = CreateCategoryCard(category, countdownsInCategory.Count);
            _categoriesLayout.Children.Add(card);
        }
    }
    
    private Frame CreateCategoryCard(string category, int count)
    {
        var frame = (new Frame
        {
            CornerRadius = 12,
            Padding = 16
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.MediaBackground).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, count > 0 ? ThemeKey.ErrorButtonBackground : (ThemeKey?)null);
        
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Auto }
            }
        };
        
        var leftStack = new VerticalStackLayout { Spacing = 4 };
        
        leftStack.Children.Add((new Label
        {
            Text = "📛 " + category,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ButtonPrimaryText, ThemeKey.PageText1, classifyPageText: true));
        
        leftStack.Children.Add((new Label
        {
            Text = $"{count} overdue",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText3, classifyPageText: true));
        
        Grid.SetColumn(leftStack, 0);
        grid.Children.Add(leftStack);
        
        var arrow = (new Label
        {
            Text = "→",
            FontSize = 24,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, ThemeKey.ErrorButtonBackground, ThemeKey.PageText1, classifyPageText: true);
        Grid.SetColumn(arrow, 1);
        grid.Children.Add(arrow);
        
        frame.Content = grid;
        
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (s, e) => await OnCategoryTapped(category, count);
        frame.GestureRecognizers.Add(tap);
        
        return frame;
    }
    
    private async Task OnCategoryTapped(string category, int count)
    {
        // Go directly to the category page
        var page = new OverdueCategoryPage(_auth, _countdowns, category);
        await Navigation.PushAsync(page);
    }
    
    private async void OnAddCategoryClicked(object sender, EventArgs e)
    {
        string name = await DisplayPromptAsync(
            "New Overdue Category",
            "Enter category name:",
            "Create",
            "Cancel",
            placeholder: "e.g., Technical, Personal, Work");
        
        if (!string.IsNullOrWhiteSpace(name))
        {
            // Navigate to the new category page
            var page = new OverdueCategoryPage(_auth, _countdowns, name.Trim());
            await Navigation.PushAsync(page);
        }
    }
    
    private async void OnViewAllClicked(object sender, EventArgs e)
    {
        var page = new OverdueCategoryPage(_auth, _countdowns, null); // null = all overdue
        await Navigation.PushAsync(page);
    }
}

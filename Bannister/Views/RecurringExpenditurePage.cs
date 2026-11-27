using Bannister.Models;
using Bannister.Services;
using Bannister.Helpers;
using System.Globalization;

namespace Bannister.Views;

public class RecurringExpenditurePage : Bannister.Views.ModeThemedPage
{
    private readonly AuthService _auth;
    private readonly RecurringExpenditureService _recurringExpenditures;

    private readonly Label _approvedTotalLabel;
    private readonly VerticalStackLayout _pendingContainer;
    private readonly VerticalStackLayout _historyContainer;
    private string _currencySymbol = "$";
    private string _currencyCode = "USD";

    public RecurringExpenditurePage(AuthService auth, RecurringExpenditureService recurringExpenditures)
    {
        _auth = auth;
        _recurringExpenditures = recurringExpenditures;

        Title = "Data-Based Recurring Expenditures";


        _approvedTotalLabel = (new Label
        {
            Text = "Approved: $0/mo",
            FontSize = 14
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true);
        _pendingContainer = new VerticalStackLayout { Spacing = 10 };
        _historyContainer = new VerticalStackLayout { Spacing = 10 };

        BuildUI();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private void BuildUI()
    {
        var headerText = new VerticalStackLayout { Spacing = 2 };
        headerText.Children.Add((new Label
        {
            Text = "Data-Based Recurring Expenditures",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true));
        headerText.Children.Add(_approvedTotalLabel);

        var addButton = (new Button
        {
            Text = "+ Add to Trial",
            CornerRadius = 8,
            Padding = new Thickness(16, 8),
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        addButton.Clicked += async (_, _) => await AddToTrialAsync();

        var currencyButton = (new Button
        {
            Text = "$ USD",
            CornerRadius = 8,
            Padding = new Thickness(10, 8),
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.HighlightVioletText).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        currencyButton.Clicked += async (_, _) =>
        {
            var selectedLabel = await DisplayActionSheet(
                "Currency", "Cancel", null,
                CurrencyOption.All.Select(c => c.Label).ToArray());
            if (string.IsNullOrWhiteSpace(selectedLabel) || selectedLabel == "Cancel") return;
            var selected = CurrencyOption.All.FirstOrDefault(c => c.Label == selectedLabel);
            if (selected == null) return;
            _currencySymbol = selected.Symbol;
            _currencyCode = selected.Code;
            currencyButton.Text = $"{selected.Symbol} {selected.Code}";
            await RefreshAsync();
        };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12
        };
        header.Add(headerText, 0, 0);
        header.Add(currencyButton, 1, 0);
        header.Add(addButton, 2, 0);

        var content = new VerticalStackLayout
        {
            Padding = 20,
            Spacing = 18
        };
        content.Children.Add(header);
        content.Children.Add(BuildSection("Pending", _pendingContainer, ThemeKey.HighlightVioletText));
        content.Children.Add(BuildSection("History", _historyContainer, ThemeKey.CardText1));

        Content = new ScrollView { Content = content };
    }

    private static View BuildSection(string title, VerticalStackLayout container, ThemeKey color)
    {
        var section = new VerticalStackLayout { Spacing = 8 };
        section.Children.Add((new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, color, ThemeKey.PageText1, classifyPageText: true));
        section.Children.Add(container);
        return section;
    }

    private async Task RefreshAsync()
    {
        var items = await _recurringExpenditures.GetAllAsync(_auth.CurrentUsername);

        _pendingContainer.Children.Clear();
        _historyContainer.Children.Clear();

        var pending = items.Where(i => i.Status == "Pending").ToList();
        var approved = items.Where(i => i.Status == "Approved").ToList();

        _approvedTotalLabel.Text = $"Approved: {FormatMoney(approved.Sum(i => i.MonthlyCost))}/mo";

        if (pending.Count == 0)
            AddEmptyMessage(_pendingContainer, "No services currently on trial.");
        else
        {
            foreach (var item in pending)
                _pendingContainer.Children.Add(BuildPendingCard(item));
        }

        // Build history: collect ALL verdict events across ALL items, sort most-recent first
        var allTests = new List<(RecurringExpenditureTest Test, RecurringExpenditure Item)>();
        foreach (var item in items)
        {
            var tests = await _recurringExpenditures.GetTestsAsync(item.Id);
            foreach (var test in tests)
                allTests.Add((test, item));
        }

        var sorted = allTests.OrderByDescending(x => x.Test.CreatedAt).ToList();

        if (sorted.Count == 0)
            AddEmptyMessage(_historyContainer, "No verdicts yet.");
        else
        {
            foreach (var (test, item) in sorted)
                _historyContainer.Children.Add(BuildHistoryRow(test, item));
        }
    }

    private static void AddEmptyMessage(VerticalStackLayout container, string message)
    {
        container.Children.Add((new Label
        {
            Text = message,
            FontSize = 13,
            FontAttributes = FontAttributes.Italic
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true));
    }

    private View BuildPendingCard(RecurringExpenditure item)
    {
        int daysLeft = Math.Max(0, (item.TrialEndDate.ToLocalTime() - DateTime.Now).Days);
        var stack = new VerticalStackLayout { Spacing = 7 };
        stack.Children.Add(BuildNameAndCostRow(item, ThemeKey.CardText1));
        stack.Children.Add((new Label
        {
            Text = $"{daysLeft} days left",
            FontSize = 32,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, daysLeft <= 3 ? ThemeKey.ErrorText : ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = $"{item.TrialStartDate.ToLocalTime():yyyy-MM-dd} → {item.TrialEndDate.ToLocalTime():yyyy-MM-dd}",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = $"Justification target: {item.JustificationTarget}",
            FontSize = 13,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, ThemeKey.HighlightVioletText, ThemeKey.PageText1, classifyPageText: true));

        var approveButton = (new Button
        {
            Text = "✓ Approve",
            CornerRadius = 7
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SuccessButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        approveButton.Clicked += async (_, _) => await ResolveTrialAsync(item, "Approved");

        var cutButton = (new Button
        {
            Text = "✗ Cut",
            CornerRadius = 7
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ButtonPrimaryText);
        cutButton.Clicked += async (_, _) => await ResolveTrialAsync(item, "Cut");

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8
        };
        actions.Add(approveButton, 0, 0);
        actions.Add(cutButton, 1, 0);
        stack.Children.Add(actions);

        var deleteButton = BuildOutlineButton("Delete", ThemeKey.ErrorButtonBackground);
        deleteButton.Clicked += async (_, _) => await DeleteAsync(item);
        stack.Children.Add(deleteButton);

        return BuildCard(stack, ThemeKey.CardBackground);
    }

    private View BuildHistoryRow(RecurringExpenditureTest test, RecurringExpenditure item)
    {
        bool isApproved = test.Note == "Approved";
        var color = isApproved ? ThemeKey.SuccessText : ThemeKey.ErrorText;
        var bg = isApproved ? ThemeKey.SuccessBackground : ThemeKey.ErrorBackground;

        var stack = new VerticalStackLayout { Spacing = 4 };

        stack.Children.Add((new Label
        {
            Text = test.Note,
            FontSize = 26,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, color, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = item.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText1, ThemeKey.PageText1, classifyPageText: true));
        stack.Children.Add((new Label
        {
            Text = $"{FormatMoney(item.MonthlyCost)}/mo  ·  {test.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            FontSize = 15
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        if (!string.IsNullOrWhiteSpace(item.JustificationTarget))
            stack.Children.Add((new Label
            {
                Text = $"Target: {item.JustificationTarget}",
                FontSize = 16,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText2, classifyPageText: true));

        if (!string.IsNullOrWhiteSpace(test.ActualResult))
            stack.Children.Add((new Label
            {
                Text = $"Result: {test.ActualResult}",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                LineBreakMode = LineBreakMode.WordWrap
            }).ThemeColor(Label.TextColorProperty, color, ThemeKey.PageText1, classifyPageText: true));

        var editBtn = BuildOutlineButton("Edit Result", ThemeKey.CardText2);
        editBtn.Clicked += async (_, _) =>
        {
            string? newResult = await DisplayPromptAsync(
                "Edit Result",
                "Actual result:",
                "Save",
                "Cancel",
                initialValue: test.ActualResult);
            if (newResult == null) return;
            test.ActualResult = newResult.Trim();
            await _recurringExpenditures.UpdateTestAsync(test);
            await RefreshAsync();
        };

        var deleteBtn = BuildOutlineButton("Delete", ThemeKey.ErrorButtonBackground);
        deleteBtn.Clicked += async (_, _) =>
        {
            bool confirmed = await DisplayAlert("Delete verdict?", $"Remove this {test.Note} entry?", "Delete", "Cancel");
            if (!confirmed) return;
            await _recurringExpenditures.DeleteTestAsync(test.Id);
            await RefreshAsync();
        };

        var btnRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 8,
            Margin = new Thickness(0, 6, 0, 0)
        };
        btnRow.Add(editBtn, 0, 0);
        btnRow.Add(deleteBtn, 1, 0);
        stack.Children.Add(btnRow);

        return (new Frame
        {
            Content = stack,
            Padding = 14,
            CornerRadius = 10,
            HasShadow = true,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, bg);
    }

    private View BuildNameAndCostRow(RecurringExpenditure item, ThemeKey textColor)
    {
        var row = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 10
        };
        row.Add((new Label
        {
            Text = item.Name,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            LineBreakMode = LineBreakMode.WordWrap
        }).ThemeColor(Label.TextColorProperty, textColor, ThemeKey.PageText1, classifyPageText: true), 0, 0);
        row.Add((new Label
        {
            Text = $"{FormatMoney(item.MonthlyCost)}/mo",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center
        }).ThemeColor(Label.TextColorProperty, textColor, ThemeKey.PageText1, classifyPageText: true), 1, 0);
        return row;
    }

    private static View BuildCard(View content, ThemeKey background)
    {
        return (new Frame
        {
            Content = content,
            Padding = 14,
            CornerRadius = 10,
            HasShadow = true,
            BorderColor = Colors.Transparent
        }).ThemeColor(VisualElement.BackgroundColorProperty, background);
    }

    private static Button BuildOutlineButton(string text, ThemeKey color)
    {
        var accent = color;
        return (new Button
        {
            Text = text,
            FontSize = 12,
            BorderWidth = 1,
            CornerRadius = 7,
            HeightRequest = 36,
            Padding = new Thickness(10, 4)
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground).ThemeColor(Button.BorderColorProperty, accent).ThemeColor(Button.TextColorProperty, accent);
    }

    private async Task DeleteAsync(RecurringExpenditure item)
    {
        bool confirmed = await DisplayAlert(
            "Delete expenditure?",
            $"Delete \"{item.Name}\"?",
            "Delete",
            "Cancel");
        if (!confirmed)
            return;

        await _recurringExpenditures.DeleteAsync(item.Id);
        await RefreshAsync();
    }

    private async Task ResolveTrialAsync(RecurringExpenditure item, string status)
    {
        bool confirmed = await DisplayAlert(
            status == "Approved" ? "Approve?" : "Cut?",
            $"{(status == "Approved" ? "Approve" : "Cut")} \"{item.Name}\"?",
            status == "Approved" ? "Approve" : "Cut",
            "Cancel");
        if (!confirmed) return;

        string? actualResult = await DisplayPromptAsync(
            "Actual Result",
            "What actually happened?",
            "Save",
            "Cancel");
        if (actualResult == null) return;

        item.Status = status;
        item.OutcomeNote = "";
        await _recurringExpenditures.UpdateAsync(item);

        await _recurringExpenditures.AddTestAsync(new RecurringExpenditureTest
        {
            ExpenditureId = item.Id,
            Note = status,
            ActualResult = actualResult.Trim(),
            CreatedAt = DateTime.UtcNow
        });

        await RefreshAsync();
    }

    private async Task AddToTrialAsync()
    {
        string? name = await DisplayPromptAsync(
            "Add to Trial",
            "Service name:",
            "Next",
            "Cancel");
        if (string.IsNullOrWhiteSpace(name))
            return;

        string? costText = await DisplayPromptAsync(
            "Monthly Cost",
            "Monthly cost:",
            "Next",
            "Cancel",
            keyboard: Keyboard.Numeric,
            placeholder: "0.00");
        if (!TryParseCost(costText, out decimal monthlyCost) || monthlyCost < 0)
        {
            await DisplayAlert("Invalid cost", "Enter a valid non-negative monthly cost.", "OK");
            return;
        }

        string? startDateText = await DisplayPromptAsync(
            "Trial Start Date",
            "Start date (yyyy-MM-dd):",
            "Next", "Cancel",
            initialValue: DateTime.Now.ToString("yyyy-MM-dd"));
        if (startDateText == null) return;
        DateTime trialStart = DateTime.TryParse(startDateText, out DateTime parsedStart)
            ? parsedStart.ToUniversalTime()
            : DateTime.UtcNow;

        string? endDateText = await DisplayPromptAsync(
            "Trial End Date",
            "End date (yyyy-MM-dd):",
            "Add", "Cancel",
            initialValue: DateTime.Now.AddDays(30).ToString("yyyy-MM-dd"));
        if (endDateText == null) return;
        DateTime trialEnd = DateTime.TryParse(endDateText, out DateTime parsedEnd)
            ? parsedEnd.ToUniversalTime()
            : DateTime.UtcNow.AddDays(30);

        int trialDays = Math.Max(1, (trialEnd - trialStart).Days);

        string? justificationTarget = await DisplayPromptAsync(
            "Justification Target",
            "What result would justify keeping this permanently?",
            "Add",
            "Cancel");
        if (justificationTarget == null)
            return;

        var now = DateTime.UtcNow;
        await _recurringExpenditures.AddAsync(new RecurringExpenditure
        {
            Username = _auth.CurrentUsername,
            Name = name.Trim(),
            MonthlyCost = monthlyCost,
            Status = "Pending",
            JustificationTarget = justificationTarget.Trim(),
            CurrencyCode = _currencyCode,
            TrialStartDate = trialStart,
            TrialEndDate = trialEnd,
            TrialDays = trialDays,
            CreatedAt = now,
            UpdatedAt = now
        });

        await RefreshAsync();
    }

    private static bool TryParseCost(string? value, out decimal cost)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            cost = 0;
            return false;
        }

        string cleaned = value.Trim().Replace("$", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal);
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out cost)
            || decimal.TryParse(cleaned, NumberStyles.Currency, CultureInfo.CurrentCulture, out cost);
    }

    private string FormatMoney(decimal value) =>
        _currencySymbol + value.ToString("N0", CultureInfo.InvariantCulture);
}

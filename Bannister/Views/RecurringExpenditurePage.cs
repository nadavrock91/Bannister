using Bannister.Models;
using Bannister.Services;
using Bannister.Helpers;
using System.Globalization;

namespace Bannister.Views;

public class RecurringExpenditurePage : ContentPage
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
        BackgroundColor = Color.FromArgb("#F5F5F5");

        _approvedTotalLabel = new Label
        {
            Text = "Approved: $0/mo",
            FontSize = 14,
            TextColor = Color.FromArgb("#666")
        };
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
        headerText.Children.Add(new Label
        {
            Text = "Data-Based Recurring Expenditures",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#6A1B9A")
        });
        headerText.Children.Add(_approvedTotalLabel);

        var addButton = new Button
        {
            Text = "+ Add to Trial",
            BackgroundColor = Color.FromArgb("#6A1B9A"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(16, 8),
            VerticalOptions = LayoutOptions.Center
        };
        addButton.Clicked += async (_, _) => await AddToTrialAsync();

        var currencyButton = new Button
        {
            Text = "$ USD",
            BackgroundColor = Color.FromArgb("#6A1B9A"),
            TextColor = Colors.White,
            CornerRadius = 8,
            Padding = new Thickness(10, 8),
            VerticalOptions = LayoutOptions.Center
        };
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
        content.Children.Add(BuildSection("Pending", _pendingContainer, "#6A1B9A"));
        content.Children.Add(BuildSection("History", _historyContainer, "#333333"));

        Content = new ScrollView { Content = content };
    }

    private static View BuildSection(string title, VerticalStackLayout container, string color)
    {
        var section = new VerticalStackLayout { Spacing = 8 };
        section.Children.Add(new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb(color)
        });
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
        container.Children.Add(new Label
        {
            Text = message,
            FontSize = 13,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#999")
        });
    }

    private View BuildPendingCard(RecurringExpenditure item)
    {
        int daysLeft = Math.Max(0, (item.TrialEndDate.ToLocalTime() - DateTime.Now).Days);
        var stack = new VerticalStackLayout { Spacing = 7 };
        stack.Children.Add(BuildNameAndCostRow(item, Color.FromArgb("#333")));
        stack.Children.Add(new Label
        {
            Text = $"{daysLeft} days left",
            FontSize = 32,
            FontAttributes = FontAttributes.Bold,
            TextColor = daysLeft <= 3 ? Color.FromArgb("#C62828") : Color.FromArgb("#6A1B9A")
        });
        stack.Children.Add(new Label
        {
            Text = $"{item.TrialStartDate.ToLocalTime():yyyy-MM-dd} → {item.TrialEndDate.ToLocalTime():yyyy-MM-dd}",
            FontSize = 12,
            TextColor = Color.FromArgb("#777")
        });
        stack.Children.Add(new Label
        {
            Text = $"Justification target: {item.JustificationTarget}",
            FontSize = 13,
            TextColor = Color.FromArgb("#6A1B9A"),
            LineBreakMode = LineBreakMode.WordWrap
        });

        var approveButton = new Button
        {
            Text = "✓ Approve",
            BackgroundColor = Color.FromArgb("#2E7D32"),
            TextColor = Colors.White,
            CornerRadius = 7
        };
        approveButton.Clicked += async (_, _) => await ResolveTrialAsync(item, "Approved");

        var cutButton = new Button
        {
            Text = "✗ Cut",
            BackgroundColor = Color.FromArgb("#C62828"),
            TextColor = Colors.White,
            CornerRadius = 7
        };
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

        var deleteButton = BuildOutlineButton("Delete", "#C62828");
        deleteButton.Clicked += async (_, _) => await DeleteAsync(item);
        stack.Children.Add(deleteButton);

        return BuildCard(stack, Colors.White);
    }

    private View BuildHistoryRow(RecurringExpenditureTest test, RecurringExpenditure item)
    {
        bool isApproved = test.Note == "Approved";
        var color = Color.FromArgb(isApproved ? "#2E7D32" : "#C62828");
        var bg = Color.FromArgb(isApproved ? "#E8F5E9" : "#FFEBEE");

        var stack = new VerticalStackLayout { Spacing = 4 };

        stack.Children.Add(new Label
        {
            Text = test.Note,
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = color
        });
        stack.Children.Add(new Label
        {
            Text = item.Name,
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });
        stack.Children.Add(new Label
        {
            Text = $"{FormatMoney(item.MonthlyCost)}/mo  ·  {test.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm}",
            FontSize = 12,
            TextColor = Color.FromArgb("#777")
        });

        return new Frame
        {
            Content = stack,
            Padding = 14,
            CornerRadius = 10,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            BackgroundColor = bg
        };
    }

    private View BuildNameAndCostRow(RecurringExpenditure item, Color textColor)
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
        row.Add(new Label
        {
            Text = item.Name,
            FontSize = 17,
            FontAttributes = FontAttributes.Bold,
            TextColor = textColor,
            LineBreakMode = LineBreakMode.WordWrap
        }, 0, 0);
        row.Add(new Label
        {
            Text = $"{FormatMoney(item.MonthlyCost)}/mo",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = textColor,
            VerticalOptions = LayoutOptions.Center
        }, 1, 0);
        return row;
    }

    private static View BuildCard(View content, Color background)
    {
        return new Frame
        {
            Content = content,
            Padding = 14,
            CornerRadius = 10,
            HasShadow = true,
            BorderColor = Colors.Transparent,
            BackgroundColor = background
        };
    }

    private static Button BuildOutlineButton(string text, string color)
    {
        var accent = Color.FromArgb(color);
        return new Button
        {
            Text = text,
            FontSize = 12,
            BackgroundColor = Colors.White,
            BorderColor = accent,
            BorderWidth = 1,
            TextColor = accent,
            CornerRadius = 7,
            HeightRequest = 36,
            Padding = new Thickness(10, 4)
        };
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

        item.Status = status;
        item.OutcomeNote = "";
        await _recurringExpenditures.UpdateAsync(item);

        await _recurringExpenditures.AddTestAsync(new RecurringExpenditureTest
        {
            ExpenditureId = item.Id,
            Note = status, // "Approved" or "Cut"
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

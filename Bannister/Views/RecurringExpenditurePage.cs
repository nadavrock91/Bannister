using Bannister.Models;
using Bannister.Services;
using System.Globalization;

namespace Bannister.Views;

public class RecurringExpenditurePage : ContentPage
{
    private readonly AuthService _auth;
    private readonly RecurringExpenditureService _recurringExpenditures;

    private readonly Label _approvedTotalLabel;
    private readonly VerticalStackLayout _pendingContainer;
    private readonly VerticalStackLayout _approvedContainer;
    private readonly VerticalStackLayout _cutContainer;
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
        _approvedContainer = new VerticalStackLayout { Spacing = 10 };
        _cutContainer = new VerticalStackLayout { Spacing = 10 };

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
            string? selected = await DisplayActionSheet(
                "Currency",
                "Cancel",
                null,
                "USD ($)",
                "EUR (€)",
                "GBP (£)",
                "ILS (₪)",
                "JPY (¥)");

            if (string.IsNullOrWhiteSpace(selected) || selected == "Cancel")
                return;

            (_currencyCode, _currencySymbol) = selected switch
            {
                "EUR (€)" => ("EUR", "€"),
                "GBP (£)" => ("GBP", "£"),
                "ILS (₪)" => ("ILS", "₪"),
                "JPY (¥)" => ("JPY", "¥"),
                _ => ("USD", "$")
            };
            currencyButton.Text = $"{_currencySymbol} {_currencyCode}";
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
        content.Children.Add(BuildSection("Approved", _approvedContainer, "#2E7D32"));
        content.Children.Add(BuildSection("Cut", _cutContainer, "#757575"));

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
        _approvedContainer.Children.Clear();
        _cutContainer.Children.Clear();

        var pending = items.Where(item => item.Status == "Pending").ToList();
        var approved = items.Where(item => item.Status == "Approved").ToList();
        var cut = items.Where(item => item.Status == "Cut").ToList();

        _approvedTotalLabel.Text = $"Approved: {FormatMoney(approved.Sum(item => item.MonthlyCost))}/mo";

        if (pending.Count == 0)
        {
            AddEmptyMessage(_pendingContainer, "No services currently on trial.");
        }
        else
        {
            foreach (var item in pending)
            {
                var tests = await _recurringExpenditures.GetTestsAsync(item.Id);
                _pendingContainer.Children.Add(BuildPendingCard(item, tests));
            }
        }
        PopulateSection(_approvedContainer, approved, BuildApprovedCard, "No approved services yet.");
        PopulateSection(_cutContainer, cut, BuildCutCard, "No cut services yet.");
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

    private static void PopulateSection(
        VerticalStackLayout container,
        IReadOnlyCollection<RecurringExpenditure> items,
        Func<RecurringExpenditure, View> buildCard,
        string emptyMessage)
    {
        if (items.Count == 0)
        {
            AddEmptyMessage(container, emptyMessage);
            return;
        }

        foreach (var item in items)
            container.Children.Add(buildCard(item));
    }

    private View BuildPendingCard(RecurringExpenditure item, IReadOnlyCollection<RecurringExpenditureTest> tests)
    {
        int daysInTrial = Math.Max(0, (DateTime.UtcNow - item.TrialStartDate).Days);
        var stack = new VerticalStackLayout { Spacing = 7 };
        stack.Children.Add(BuildNameAndCostRow(item, Color.FromArgb("#333")));
        stack.Children.Add(new Label
        {
            Text = $"Reason: {item.Reason}",
            FontSize = 13,
            TextColor = Color.FromArgb("#555"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        stack.Children.Add(new Label
        {
            Text = $"Justification target: {item.JustificationTarget}",
            FontSize = 13,
            TextColor = Color.FromArgb("#6A1B9A"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        stack.Children.Add(new Label
        {
            Text = $"Trial: {item.TrialStartDate.ToLocalTime():yyyy-MM-dd} → {item.TrialEndDate.ToLocalTime():yyyy-MM-dd}",
            FontSize = 12,
            TextColor = Color.FromArgb("#777")
        });
        stack.Children.Add(new Label
        {
            Text = $"Days in trial: {daysInTrial} of {item.TrialDays}",
            FontSize = 12,
            TextColor = Color.FromArgb("#777")
        });

        var testsStack = new VerticalStackLayout { Spacing = 3 };
        foreach (var test in tests)
        {
            testsStack.Children.Add(new Label
            {
                Text = $"{test.CreatedAt.ToLocalTime():yyyy-MM-dd HH:mm} — {test.Note}",
                FontSize = 12,
                TextColor = Color.FromArgb("#555"),
                LineBreakMode = LineBreakMode.WordWrap
            });
        }
        stack.Children.Add(testsStack);

        var logTestButton = BuildOutlineButton("+ Log Test", "#6A1B9A");
        logTestButton.Clicked += async (_, _) => await LogTestAsync(item);
        stack.Children.Add(logTestButton);

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

    private View BuildApprovedCard(RecurringExpenditure item)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(BuildNameAndCostRow(item, Color.FromArgb("#2E7D32")));
        stack.Children.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(item.OutcomeNote)
                ? "No outcome note."
                : item.OutcomeNote,
            FontSize = 13,
            TextColor = Color.FromArgb("#555"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        stack.Children.Add(new Label
        {
            Text = $"Approved: {item.UpdatedAt.ToLocalTime():yyyy-MM-dd}",
            FontSize = 12,
            TextColor = Color.FromArgb("#777")
        });
        var deleteButton = BuildOutlineButton("Delete", "#C62828");
        deleteButton.Clicked += async (_, _) => await DeleteAsync(item);
        stack.Children.Add(deleteButton);
        return BuildCard(stack, Color.FromArgb("#E8F5E9"));
    }

    private View BuildCutCard(RecurringExpenditure item)
    {
        var stack = new VerticalStackLayout { Spacing = 6 };
        stack.Children.Add(BuildNameAndCostRow(item, Color.FromArgb("#777")));
        stack.Children.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(item.OutcomeNote)
                ? "No outcome note."
                : item.OutcomeNote,
            FontSize = 13,
            TextColor = Color.FromArgb("#888"),
            LineBreakMode = LineBreakMode.WordWrap
        });
        var deleteButton = BuildOutlineButton("Delete", "#C62828");
        deleteButton.Clicked += async (_, _) => await DeleteAsync(item);
        stack.Children.Add(deleteButton);
        return BuildCard(stack, Color.FromArgb("#EEEEEE"));
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

    private async Task LogTestAsync(RecurringExpenditure item)
    {
        string? note = await DisplayPromptAsync(
            "Log Test",
            "What did you test, and what happened?",
            "Save",
            "Cancel");
        if (string.IsNullOrWhiteSpace(note))
            return;

        await _recurringExpenditures.AddTestAsync(new RecurringExpenditureTest
        {
            ExpenditureId = item.Id,
            Note = note.Trim(),
            CreatedAt = DateTime.UtcNow
        });
        await RefreshAsync();
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
        string? note = await DisplayPromptAsync(
            status == "Approved" ? "Approve Expenditure" : "Cut Expenditure",
            "Outcome note:",
            status == "Approved" ? "Approve" : "Cut",
            "Cancel",
            initialValue: item.OutcomeNote);

        if (note == null)
            return;

        item.Status = status;
        item.OutcomeNote = note.Trim();
        await _recurringExpenditures.UpdateAsync(item);
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

        string? reason = await DisplayPromptAsync(
            "Reason",
            "Why are you trying it?",
            "Next",
            "Cancel");
        if (reason == null)
            return;

        string? trialDaysText = await DisplayPromptAsync(
            "Trial Length",
            "Trial length in days:",
            "Add",
            "Cancel",
            keyboard: Keyboard.Numeric,
            initialValue: "30");
        if (!int.TryParse(trialDaysText, out int trialDays) || trialDays < 1)
        {
            await DisplayAlert("Invalid trial length", "Enter a trial length of at least one day.", "OK");
            return;
        }

        string? justificationTarget = await DisplayPromptAsync(
            "Justification Target",
            "What result would justify keeping this permanently?",
            "Add",
            "Cancel");
        if (justificationTarget == null)
            return;

        string? startDateText = await DisplayPromptAsync(
            "Trial Start Date",
            "Start date (yyyy-MM-dd):",
            "Add",
            "Cancel",
            initialValue: DateTime.Now.ToString("yyyy-MM-dd"));
        DateTime trialStart = DateTime.TryParse(startDateText, out DateTime parsedStart)
            ? parsedStart.ToUniversalTime()
            : DateTime.UtcNow;

        var now = DateTime.UtcNow;
        await _recurringExpenditures.AddAsync(new RecurringExpenditure
        {
            Username = _auth.CurrentUsername,
            Name = name.Trim(),
            MonthlyCost = monthlyCost,
            Status = "Pending",
            Reason = reason.Trim(),
            JustificationTarget = justificationTarget.Trim(),
            CurrencyCode = _currencyCode,
            TrialStartDate = trialStart,
            TrialEndDate = trialStart.AddDays(trialDays),
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

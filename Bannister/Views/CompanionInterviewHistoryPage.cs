using Bannister.Models;
using Bannister.Services;

namespace Bannister.Views;

public sealed class CompanionInterviewHistoryPage : Bannister.Views.ModeThemedPage
{
    private readonly string _username;
    private readonly Companion _companion;
    private readonly CompanionService _service;
    private readonly PrivacyModeService _privacyMode;
    private readonly Action<CompanionInterview> _onSelected;
    private readonly Action<int> _onDeleted;
    private readonly VerticalStackLayout _content = new() { Padding = 16, Spacing = 10 };
    private bool _loading;

    public CompanionInterviewHistoryPage(
        string username,
        Companion companion,
        CompanionService service,
        PrivacyModeService privacyMode,
        Action<CompanionInterview> onSelected,
        Action<int> onDeleted)
    {
        _username = username;
        _companion = companion;
        _service = service;
        _privacyMode = privacyMode;
        _onSelected = onSelected;
        _onDeleted = onDeleted;
        Title = $"{companion.Nickname} · Interview History";

        Content = new ScrollView { Content = _content };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            var interviews = await _service.GetInterviewsAsync(_username, _companion.Id);
            _content.Children.Clear();
            if (interviews.Count == 0)
            {
                _content.Children.Add((new Label
                {
                    Text = "No interviews yet."
                }).ThemeColor(Label.TextColorProperty, ThemeKey.OnColorText, ThemeKey.PageText1, classifyPageText: true));
                return;
            }

            foreach (var interview in interviews)
            {
                _content.Children.Add(CompanionInterviewPresentation.BuildRow(
                    interview,
                    async () =>
                    {
                        _onSelected(interview);
                        await Navigation.PopAsync();
                    },
                    async () => await DeleteInterviewAsync(interview)));
            }
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task DeleteInterviewAsync(CompanionInterview interview)
    {
        string? choice = await DisplayActionSheet(
            "Delete Interview",
            "Cancel",
            null,
            "Delete interview only",
            "Delete interview and its data points");
        if (string.IsNullOrWhiteSpace(choice) || choice == "Cancel") return;

        bool deletePoints = choice == "Delete interview and its data points";
        await _service.DeleteInterviewAsync(interview.Id, deletePoints);
        _onDeleted(interview.Id);
        await LoadAsync();
    }
}

internal static class CompanionInterviewPresentation
{
    public static string StepText(CompanionInterview interview) =>
        string.IsNullOrWhiteSpace(interview.Transcript) ? "Step 2 of 3: Paste conversation" :
        !string.IsNullOrWhiteSpace(interview.Notes) ? "Extraction applied (can review another result)" :
        "Step 3 of 3: Paste extraction result";

    public static View BuildRow(
        CompanionInterview interview,
        Func<Task> openOrResume,
        Func<Task> deleteInterview)
    {
        var row = new VerticalStackLayout { Spacing = 4 };
        row.Children.Add(new Label
        {
            Text = $"{interview.StartedAt.ToLocalTime():dd MMM yyyy HH:mm} · {interview.QuestionCount} questions",
            FontAttributes = FontAttributes.Bold
        });
        row.Children.Add((new Label
        {
            Text = $"Added {interview.PointsAdded} · Reprioritized {interview.PointsReprioritized} · Archived {interview.PointsArchived}",
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText2, ThemeKey.PageText3, classifyPageText: true));
        row.Children.Add((new Label
        {
            Text = StepText(interview),
            FontSize = 12
        }).ThemeColor(Label.TextColorProperty, ThemeKey.SecondaryAccentText, ThemeKey.PageText3, classifyPageText: true));

        var resume = MakeButton("Open / Resume", ThemeKey.HighlightIndigo, ThemeKey.HighlightIndigoButton);
        resume.HorizontalOptions = LayoutOptions.Start;
        resume.Clicked += async (_, _) => await openOrResume();
        var delete = MakeButton("Delete", ThemeKey.ErrorBackground, ThemeKey.ErrorText);
        delete.HorizontalOptions = LayoutOptions.Start;
        delete.Clicked += async (_, _) => await deleteInterview();
        row.Children.Add(new HorizontalStackLayout { Spacing = 6, Children = { resume, delete } });

        return (new Frame
        {
            Content = row,
            Padding = 12,
            CornerRadius = 9,
            HasShadow = false
        }).ThemeColor(Microsoft.Maui.Controls.Frame.BorderColorProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground);
    }

    private static Button MakeButton(string text, ThemeKey background, ThemeKey foreground) => (new Button(){
        Text = text,
        CornerRadius = 7,
        FontSize = 12,
        HeightRequest = 38,
        Padding = new Thickness(10, 0)
    }).ThemeColor(VisualElement.BackgroundColorProperty, background).ThemeColor(Button.TextColorProperty, foreground);
}

using Bannister.Models;
using System.Globalization;

namespace Bannister.Views;

public static class ProjectStatsRendering
{
    public static string BuildStatsExportBlock(StoryProject project)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("--- STATS ---");
        sb.AppendLine($"Total views: {FormatLargeNumber(project.YouTubeViews + project.FacebookViews + project.TikTokViews)}");

        if (project.YouTubeStatsCapturedAt.HasValue)
        {
            sb.AppendLine($"YouTube: {FormatLargeNumber(project.YouTubeViews)} views, {FormatLargeNumber(project.YouTubeLikes)} likes, {FormatLargeNumber(project.YouTubeComments)} comments, {FormatDurationFromSeconds(project.YouTubeAverageViewDurationSeconds)}, {FormatLargeNumber(project.YouTubeShares)} shares, {FormatLargeNumber(project.YouTubeNewFollowers)} new followers");
        }

        if (project.FacebookStatsCapturedAt.HasValue)
        {
            sb.AppendLine($"Facebook: {FormatLargeNumber(project.FacebookViews)} views, {FormatLargeNumber(project.FacebookLikes)} likes, {FormatLargeNumber(project.FacebookComments)} comments, {FormatDurationFromSeconds(project.FacebookAverageViewDurationSeconds)}, {FormatLargeNumber(project.FacebookThreeSecondViews)} 3-sec views ({FormatPercentForDenominator(project.FacebookThreeSecondViews, project.FacebookViews)}), {FormatLargeNumber(project.FacebookOneMinuteViews)} 1-min views ({FormatPercentForDenominator(project.FacebookOneMinuteViews, project.FacebookViews)}), {FormatLargeNumber(project.FacebookShares)} shares, {FormatLargeNumber(project.FacebookNewFollowers)} new followers");
        }

        if (project.TikTokStatsCapturedAt.HasValue)
        {
            sb.AppendLine($"TikTok: {FormatLargeNumber(project.TikTokViews)} views, {FormatLargeNumber(project.TikTokLikes)} likes, {FormatLargeNumber(project.TikTokComments)} comments, {FormatDurationFromSeconds(project.TikTokAverageWatchTimeSeconds)}, {FormatPercent(project.TikTokPercentWatchedFullVideo)} watched full video, {FormatLargeNumber(project.TikTokShares)} shares, {FormatLargeNumber(project.TikTokNewFollowers)} new followers");
        }

        return sb.ToString().TrimEnd();
    }

    public static View BuildYouTubeFeedbackSection(
        StoryProject project,
        Func<bool> isReadOnly,
        Func<Task> showReadOnlyAlert,
        Func<StoryProject, Task> onEdit)
    {
        var section = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        section.Children.Add(new Label
        {
            Text = "YouTube Feedback",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828")
        });

        DateTime? capturedAt = project.YouTubeStatsCapturedAt;
        bool hasStats = capturedAt.HasValue;
        section.Children.Add(new Label
        {
            Text = hasStats ? $"Last updated {capturedAt:yyyy-MM-dd HH:mm}" : "No stats entered yet.",
            FontSize = 11,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666")
        });

        var metricsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        AddMetricTile(metricsGrid, 0, 0, "Views", hasStats ? FormatLargeNumber(project.YouTubeViews) : "—");
        AddMetricTile(metricsGrid, 0, 1, "Likes", hasStats ? FormatLargeNumber(project.YouTubeLikes) : "—");
        AddMetricTile(metricsGrid, 0, 2, "Comments", hasStats ? FormatLargeNumber(project.YouTubeComments) : "—");
        AddMetricTile(metricsGrid, 1, 0, "Avg duration", hasStats ? FormatDurationFromSeconds(project.YouTubeAverageViewDurationSeconds) : "—");
        AddMetricTile(metricsGrid, 1, 1, "Shares", hasStats ? FormatLargeNumber(project.YouTubeShares) : "—");
        AddMetricTile(metricsGrid, 1, 2, "New followers", hasStats ? FormatLargeNumber(project.YouTubeNewFollowers) : "—");
        section.Children.Add(metricsGrid);

        section.Children.Add(CreateEditButton("Edit YouTube Stats", "#FFEBEE", "#C62828", project, isReadOnly, showReadOnlyAlert, onEdit));
        return section;
    }

    public static View BuildFacebookFeedbackSection(
        StoryProject project,
        Func<bool> isReadOnly,
        Func<Task> showReadOnlyAlert,
        Func<StoryProject, Task> onEdit)
    {
        var section = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        section.Children.Add(new Label
        {
            Text = "Facebook Feedback",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1877F2")
        });

        DateTime? capturedAt = project.FacebookStatsCapturedAt;
        bool hasStats = capturedAt.HasValue;
        section.Children.Add(new Label
        {
            Text = hasStats ? $"Last updated {capturedAt:yyyy-MM-dd HH:mm}" : "No stats entered yet.",
            FontSize = 11,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666")
        });

        var metricsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        AddMetricTile(metricsGrid, 0, 0, "Views", hasStats ? FormatLargeNumber(project.FacebookViews) : "—");
        AddMetricTile(metricsGrid, 0, 1, "Likes", hasStats ? FormatLargeNumber(project.FacebookLikes) : "—");
        AddMetricTile(metricsGrid, 0, 2, "Comments", hasStats ? FormatLargeNumber(project.FacebookComments) : "—");
        AddMetricTile(metricsGrid, 1, 0, "Avg duration", hasStats ? FormatDurationFromSeconds(project.FacebookAverageViewDurationSeconds) : "—");
        AddMetricTile(metricsGrid, 1, 1, "3s views", FormatCountWithPercent(project.FacebookThreeSecondViews, project.FacebookViews, hasStats));
        AddMetricTile(metricsGrid, 1, 2, "1min views", FormatCountWithPercent(project.FacebookOneMinuteViews, project.FacebookViews, hasStats));
        AddMetricTile(metricsGrid, 2, 0, "Shares", hasStats ? FormatLargeNumber(project.FacebookShares) : "—");
        AddMetricTile(metricsGrid, 2, 1, "New followers", hasStats ? FormatLargeNumber(project.FacebookNewFollowers) : "—");
        section.Children.Add(metricsGrid);

        section.Children.Add(CreateEditButton("Edit Facebook Stats", "#E7F3FF", "#1877F2", project, isReadOnly, showReadOnlyAlert, onEdit));
        return section;
    }

    public static View BuildTikTokFeedbackSection(
        StoryProject project,
        Func<bool> isReadOnly,
        Func<Task> showReadOnlyAlert,
        Func<StoryProject, Task> onEdit)
    {
        var section = new VerticalStackLayout
        {
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };

        section.Children.Add(new Label
        {
            Text = "TikTok Feedback",
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#FE2C55")
        });

        DateTime? capturedAt = project.TikTokStatsCapturedAt;
        bool hasStats = capturedAt.HasValue;
        section.Children.Add(new Label
        {
            Text = hasStats ? $"Last updated {capturedAt:yyyy-MM-dd HH:mm}" : "No stats entered yet.",
            FontSize = 11,
            FontAttributes = FontAttributes.Italic,
            TextColor = Color.FromArgb("#666")
        });

        var metricsGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8,
            RowSpacing = 8
        };

        AddMetricTile(metricsGrid, 0, 0, "Views", hasStats ? FormatLargeNumber(project.TikTokViews) : "—");
        AddMetricTile(metricsGrid, 0, 1, "Likes", hasStats ? FormatLargeNumber(project.TikTokLikes) : "—");
        AddMetricTile(metricsGrid, 0, 2, "Comments", hasStats ? FormatLargeNumber(project.TikTokComments) : "—");
        AddMetricTile(metricsGrid, 1, 0, "Avg watch time", hasStats ? FormatDurationFromSeconds(project.TikTokAverageWatchTimeSeconds) : "—");
        AddMetricTile(metricsGrid, 1, 1, "% full video", hasStats ? FormatPercent(project.TikTokPercentWatchedFullVideo) : "—");
        AddMetricTile(metricsGrid, 1, 2, "Shares", hasStats ? FormatLargeNumber(project.TikTokShares) : "—");
        AddMetricTile(metricsGrid, 2, 0, "New followers", hasStats ? FormatLargeNumber(project.TikTokNewFollowers) : "—");
        section.Children.Add(metricsGrid);

        section.Children.Add(CreateEditButton("Edit TikTok Stats", "#FFE6EA", "#FE2C55", project, isReadOnly, showReadOnlyAlert, onEdit));
        return section;
    }

    public static void AddMetricTile(Grid grid, int row, int col, string label, string value)
    {
        var frame = new Frame
        {
            BackgroundColor = Colors.White,
            BorderColor = Color.FromArgb("#E0E0E0"),
            Padding = 6,
            CornerRadius = 6,
            HasShadow = false
        };

        frame.Content = new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label
                {
                    Text = label,
                    FontSize = 10,
                    TextColor = Color.FromArgb("#666"),
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = value,
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb("#263238"),
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };

        Grid.SetRow(frame, row);
        Grid.SetColumn(frame, col);
        grid.Children.Add(frame);
    }

    public static void AddStatCell(Grid grid, int row, int col, string label, string value)
    {
        var stack = new VerticalStackLayout { Spacing = 0 };
        stack.Children.Add(new Label
        {
            Text = label,
            FontSize = 10,
            TextColor = Color.FromArgb("#999")
        });
        stack.Children.Add(new Label
        {
            Text = value,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        Grid.SetRow(stack, row);
        Grid.SetColumn(stack, col);
        grid.Children.Add(stack);
    }

    public static string FormatDurationFromSeconds(int totalSeconds)
    {
        totalSeconds = Math.Max(0, totalSeconds);
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;

        return hours > 0
            ? $"{hours}:{minutes:00}:{seconds:00}"
            : $"{minutes}:{seconds:00}";
    }

    public static string FormatLargeNumber(int value)
    {
        return Math.Max(0, value).ToString("N0", CultureInfo.InvariantCulture);
    }

    public static string FormatPercent(double value)
    {
        return Math.Clamp(value, 0.0, 100.0).ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    public static string FormatCountWithPercent(int count, int denominator, bool statsCaptured)
    {
        if (!statsCaptured)
            return "—";

        int safeCount = Math.Max(0, count);
        int safeDenominator = Math.Max(0, denominator);
        string raw = FormatLargeNumber(safeCount);

        if (safeDenominator == 0)
            return $"{raw} (—)";

        double percent = safeCount * 100.0 / safeDenominator;
        return $"{raw} ({percent.ToString("0.0", CultureInfo.InvariantCulture)}%)";
    }

    private static string FormatPercentForDenominator(int count, int denominator)
    {
        if (denominator <= 0)
            return "—";

        double percent = Math.Max(0, count) * 100.0 / denominator;
        return percent.ToString("0.0", CultureInfo.InvariantCulture) + "%";
    }

    private static Button CreateEditButton(
        string text,
        string background,
        string foreground,
        StoryProject project,
        Func<bool> isReadOnly,
        Func<Task> showReadOnlyAlert,
        Func<StoryProject, Task> onEdit)
    {
        var editBtn = new Button
        {
            Text = text,
            BackgroundColor = Color.FromArgb(background),
            TextColor = Color.FromArgb(foreground),
            CornerRadius = 8,
            FontSize = 13,
            HorizontalOptions = LayoutOptions.Fill
        };
        editBtn.Clicked += async (s, e) =>
        {
            if (isReadOnly())
            {
                await showReadOnlyAlert();
                return;
            }

            await onEdit(project);
        };
        return editBtn;
    }
}

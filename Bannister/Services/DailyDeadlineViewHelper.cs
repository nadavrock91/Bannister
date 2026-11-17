using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Bannister.Services;

public class DailyDeadlineRowOptions
{
    public bool ShowMoveButton { get; set; } = false;
    public bool ShowDeleteButton { get; set; } = false;
    public Func<DailyDeadlineItem, Task>? OnMove { get; set; }
    public Func<DailyDeadlineItem, Task>? OnDelete { get; set; }
}

public static class DailyDeadlineViewHelper
{
    public static async Task SetCompletedAsync(
        DailyDeadlineItem item,
        bool isCompleted,
        DailyDeadlineService service,
        ResetTimeService resetTime,
        string username,
        Func<Task> reload)
    {
        var log = await service.GetCurrentLogAsync(username)
            ?? new DailyDeadlineLog
            {
                Username = username,
                LogDate = await resetTime.GetTodayResetKey(username)
            };
        var ids = ParseIds(log.CompletedItemIds);
        if (isCompleted)
            ids.Add(item.Id);
        else
            ids.Remove(item.Id);
        log.CompletedItemIds = string.Join(",", ids.OrderBy(i => i));
        var active = await service.GetActiveItemsAsync(username);
        log.AllCompleted = active.Count > 0 && active.All(i => ids.Contains(i.Id));
        await service.SaveLogAsync(log);
        await reload();
    }

    public static Task<View> BuildRowAsync(
        DailyDeadlineItem item,
        Activity? activity,
        HashSet<int> completedIds,
        DailyDeadlineService service,
        ResetTimeService resetTime,
        string username,
        Func<Task> reload,
        DailyDeadlineRowOptions? options = null)
    {
        options ??= new DailyDeadlineRowOptions();

        var colDefs = new ColumnDefinitionCollection
        {
            new ColumnDefinition(GridLength.Auto),
            new ColumnDefinition(GridLength.Star),
        };
        if (options.ShowMoveButton)
            colDefs.Add(new ColumnDefinition(GridLength.Auto));
        if (options.ShowDeleteButton)
            colDefs.Add(new ColumnDefinition(GridLength.Auto));

        var row = new Grid
        {
            ColumnDefinitions = colDefs,
            ColumnSpacing = 8,
            Padding = new Thickness(8)
        };

        if (activity == null)
        {
            row.BackgroundColor = Color.FromArgb("#F0F0F0");
            row.Add(new Label
            {
                Text = "Activity not found",
                TextColor = Color.FromArgb("#888"),
                VerticalOptions = LayoutOptions.Center
            }, 1, 0);
            if (options.ShowDeleteButton && options.OnDelete != null)
            {
                int delCol = options.ShowMoveButton ? 3 : 2;
                row.Add(MakeDeleteButton(item, options.OnDelete), delCol, 0);
            }
            return Task.FromResult<View>(new Border
            {
                Content = row,
                Stroke = Color.FromArgb("#DDDDDD"),
                StrokeThickness = 1,
                BackgroundColor = Color.FromArgb("#F0F0F0")
            });
        }

        var doneCheck = new CheckBox
        {
            IsChecked = completedIds.Contains(item.Id),
            Color = Color.FromArgb("#2E7D32"),
            WidthRequest = 60,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        };
        doneCheck.CheckedChanged += async (_, e) =>
            await SetCompletedAsync(item, e.Value, service, resetTime, username, reload);
        row.Add(doneCheck, 0, 0);
        row.Add(BuildActivityVisual(activity), 1, 0);

        int nextCol = 2;
        if (options.ShowMoveButton && options.OnMove != null)
        {
            var move = new Button
            {
                Text = "Move to Possible",
                FontSize = 11,
                Padding = new Thickness(8, 0),
                HeightRequest = 34,
                BackgroundColor = Color.FromArgb("#ECEFF1"),
                TextColor = Color.FromArgb("#37474F")
            };
            move.Clicked += async (_, _) => await options.OnMove(item);
            row.Add(move, nextCol++, 0);
        }
        if (options.ShowDeleteButton && options.OnDelete != null)
        {
            row.Add(MakeDeleteButton(item, options.OnDelete), nextCol, 0);
        }

        return Task.FromResult<View>(new Border
        {
            Content = row,
            Stroke = Color.FromArgb("#E0E0E0"),
            StrokeThickness = 1,
            BackgroundColor = Colors.White
        });
    }

    public static View BuildActivityVisual(Activity? activity)
    {
        if (activity == null)
            return new Label { Text = "Unavailable activity", TextColor = Color.FromArgb("#999"), VerticalOptions = LayoutOptions.Center };
        if (activity.IsImageOnly && !string.IsNullOrWhiteSpace(activity.ImagePath))
        {
            string path = Path.IsPathRooted(activity.ImagePath)
                ? (File.Exists(activity.ImagePath) ? activity.ImagePath : Path.Combine(FileSystem.AppDataDirectory, "ActivityImages", Path.GetFileName(activity.ImagePath)))
                : Path.Combine(FileSystem.AppDataDirectory, "ActivityImages", activity.ImagePath);
            if (File.Exists(path))
                return new Image { Source = ImageSource.FromFile(path), HeightRequest = 50, WidthRequest = 50, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Start };
        }
        return new Label { Text = activity.Name, FontSize = 14, TextColor = Color.FromArgb("#222"), VerticalOptions = LayoutOptions.Center };
    }

    private static Button MakeDeleteButton(DailyDeadlineItem item, Func<DailyDeadlineItem, Task> onDelete)
    {
        var del = new Button
        {
            Text = "Delete",
            FontSize = 11,
            Padding = new Thickness(8, 0),
            HeightRequest = 34,
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            TextColor = Color.FromArgb("#C62828")
        };
        del.Clicked += async (_, _) => await onDelete(item);
        return del;
    }

    private static HashSet<int> ParseIds(string? ids)
    {
        if (string.IsNullOrWhiteSpace(ids)) return new HashSet<int>();
        return ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var n) ? n : -1)
            .Where(n => n >= 0)
            .ToHashSet();
    }
}

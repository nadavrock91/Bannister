using Bannister.Models;
using Bannister.Services;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;

namespace Bannister.Services;

public class DailyDeadlineRowOptions
{
    public bool ShowMoveButton { get; set; } = false;
    public bool ShowDeleteButton { get; set; } = false;
    public bool StackButtons { get; set; } = false;
    public Func<DailyDeadlineItem, Task>? OnMove { get; set; }
    public Func<DailyDeadlineItem, Task>? OnDelete { get; set; }
}

public static class DailyDeadlineViewHelper
{
    private static string? _imageBaseDirOverride;

    public static void SetImageBaseDirOverride(string path) =>
        _imageBaseDirOverride = path;

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
        if (!options.StackButtons)
        {
            if (options.ShowMoveButton)
                colDefs.Add(new ColumnDefinition(GridLength.Auto));
            if (options.ShowDeleteButton)
                colDefs.Add(new ColumnDefinition(GridLength.Auto));
        }

        var row = new Grid
        {
            ColumnDefinitions = colDefs,
            ColumnSpacing = 8,
            Padding = new Thickness(8)
        };

        if (activity == null)
        {
            row.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);
            var missingLabel = (new Label
            {
                Text = "Activity not found",
                VerticalOptions = LayoutOptions.Center
            }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true);
            if (options.StackButtons)
            {
                var rightSide = new VerticalStackLayout { Spacing = 6 };
                rightSide.Children.Add(missingLabel);
                var buttonRow = BuildButtonRow(item, options);
                if (buttonRow.Children.Count > 0)
                    rightSide.Children.Add(buttonRow);
                row.Add(rightSide, 1, 0);
            }
            else
            {
                row.Add(missingLabel, 1, 0);
                if (options.ShowDeleteButton && options.OnDelete != null)
                {
                    int delCol = options.ShowMoveButton ? 3 : 2;
                    row.Add(MakeDeleteButton(item, options.OnDelete), delCol, 0);
                }
            }
            return Task.FromResult<View>((new Border
            {
                Content = row,
                StrokeThickness = 1
            }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface));
        }

        var doneCheck = (new CheckBox
        {
            IsChecked = completedIds.Contains(item.Id),
            WidthRequest = 60,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center
        }).ThemeColor(CheckBox.ColorProperty, ThemeKey.SuccessText);
        doneCheck.CheckedChanged += async (_, e) =>
            await SetCompletedAsync(item, e.Value, service, resetTime, username, reload);
        row.Add(doneCheck, 0, 0);
        if (options.StackButtons)
        {
            var rightSide = new VerticalStackLayout { Spacing = 6 };
            rightSide.Children.Add(BuildActivityVisual(activity));
            var buttonRow = BuildButtonRow(item, options);
            if (buttonRow.Children.Count > 0)
                rightSide.Children.Add(buttonRow);
            row.Add(rightSide, 1, 0);
        }
        else
        {
            row.Add(BuildActivityVisual(activity), 1, 0);

            int nextCol = 2;
            if (options.ShowMoveButton && options.OnMove != null)
                row.Add(MakeMoveButton(item, options.OnMove), nextCol++, 0);
            if (options.ShowDeleteButton && options.OnDelete != null)
                row.Add(MakeDeleteButton(item, options.OnDelete), nextCol, 0);
        }

        return Task.FromResult<View>((new Border
        {
            Content = row,
            StrokeThickness = 1
        }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground));
    }

    public static Task<View> BuildPossibleRowAsync(
        DailyDeadlineItem item,
        Activity? activity,
        bool isFull,
        DailyDeadlineService service,
        string username,
        Func<Task> reload,
        bool stackButtons = false)
    {
        var colDefs = stackButtons
            ? new ColumnDefinitionCollection
              {
                  new ColumnDefinition(GridLength.Star)
              }
            : new ColumnDefinitionCollection
              {
                  new ColumnDefinition(GridLength.Star),
                  new ColumnDefinition(GridLength.Auto),
                  new ColumnDefinition(GridLength.Auto)
              };

        var row = new Grid { ColumnDefinitions = colDefs, ColumnSpacing = 6, Padding = new Thickness(8) };

        if (activity == null)
        {
            row.ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface);
            row.Add((new Label { Text = "Activity not found", VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true), 0, 0);
            if (!stackButtons)
            {
                var missingDel = MakeDeleteButton(item, async i => { await service.DeleteItemAsync(i.Id); await reload(); });
                row.Add(missingDel, 1, 0);
            }
            return Task.FromResult<View>((new Border { Content = row, StrokeThickness = 1 }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.SecondarySurface));
        }

        var moveBtn = (new Button
        {
            Text = "Move to Active",
            IsEnabled = !isFull,
            FontSize = 11,
            Padding = new Thickness(8, 0),
            HeightRequest = 34
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.InfoBackground).ThemeColor(Button.TextColorProperty, ThemeKey.InfoText);
        moveBtn.Clicked += async (_, _) => { item.IsActive = true; await service.SaveItemAsync(item); await reload(); };

        var delBtn = MakeDeleteButton(item, async i => { await service.DeleteItemAsync(i.Id); await reload(); });

        if (stackButtons)
        {
            var btnRow = new HorizontalStackLayout { Spacing = 6, Children = { moveBtn, delBtn } };
            var stack = new VerticalStackLayout { Spacing = 4, Children = { BuildActivityVisual(activity), btnRow } };
            row.Add(stack, 0, 0);
        }
        else
        {
            row.Add(BuildActivityVisual(activity), 0, 0);
            row.Add(moveBtn, 1, 0);
            row.Add(delBtn, 2, 0);
        }

        return Task.FromResult<View>((new Border { Content = row, StrokeThickness = 1 }).ThemeColor(Border.StrokeProperty, ThemeKey.Separator).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.CardBackground));
    }

    public static View BuildActivityVisual(Activity? activity)
    {
        if (activity == null)
            return (new Label { Text = "Unavailable activity", VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.CardText3, ThemeKey.PageText2, classifyPageText: true);
        if (activity.IsImageOnly)
        {
            if (!string.IsNullOrWhiteSpace(activity.ImagePath))
            {
                string path = Path.IsPathRooted(activity.ImagePath)
                    ? (File.Exists(activity.ImagePath) ? activity.ImagePath : Path.Combine(_imageBaseDirOverride ?? FileSystem.AppDataDirectory, "ActivityImages", Path.GetFileName(activity.ImagePath)))
                    : Path.Combine(_imageBaseDirOverride ?? FileSystem.AppDataDirectory, "ActivityImages", activity.ImagePath);
                if (File.Exists(path))
                    return new Image { Source = ImageSource.FromFile(path), HeightRequest = 50, WidthRequest = 50, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Start };
            }

            return new BoxView
            {
                HeightRequest = 50,
                WidthRequest = 50,
                Color = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Start
            };
        }
        return (new Label { Text = activity.Name, FontSize = 14, VerticalOptions = LayoutOptions.Center }).ThemeColor(Label.TextColorProperty, ThemeKey.CardTextStrong, ThemeKey.PageText1, classifyPageText: true);
    }

    private static HorizontalStackLayout BuildButtonRow(
        DailyDeadlineItem item,
        DailyDeadlineRowOptions options)
    {
        var buttonRow = new HorizontalStackLayout
        {
            HorizontalOptions = LayoutOptions.Start,
            Spacing = 6
        };
        if (options.ShowMoveButton && options.OnMove != null)
            buttonRow.Children.Add(MakeMoveButton(item, options.OnMove));
        if (options.ShowDeleteButton && options.OnDelete != null)
            buttonRow.Children.Add(MakeDeleteButton(item, options.OnDelete));
        return buttonRow;
    }

    private static Button MakeMoveButton(DailyDeadlineItem item, Func<DailyDeadlineItem, Task> onMove)
    {
        var move = (new Button
        {
            Text = "Move to Possible",
            FontSize = 11,
            Padding = new Thickness(8, 0),
            HeightRequest = 34
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.NeutralButtonBackground).ThemeColor(Button.TextColorProperty, ThemeKey.NeutralButtonText);
        move.Clicked += async (_, _) => await onMove(item);
        return move;
    }

    private static Button MakeDeleteButton(DailyDeadlineItem item, Func<DailyDeadlineItem, Task> onDelete)
    {
        var del = (new Button
        {
            Text = "Delete",
            FontSize = 11,
            Padding = new Thickness(8, 0),
            HeightRequest = 34
        }).ThemeColor(VisualElement.BackgroundColorProperty, ThemeKey.ErrorBackground).ThemeColor(Button.TextColorProperty, ThemeKey.ErrorText);
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

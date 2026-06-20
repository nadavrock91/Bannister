using Microsoft.Maui.ApplicationModel;
using System.Text;

namespace Bannister.Services;

public static class WebsiteFolderHelper
{
    public static async Task<string?> PickParentFolderPathAsync(ContentPage host)
    {
#if WINDOWS
        try
        {
            var window = Microsoft.Maui.Controls.Application.Current?.Windows?.FirstOrDefault();
            if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window winuiWindow)
                return null;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(winuiWindow);
            var picker = new Windows.Storage.Pickers.FolderPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        }
        catch (Exception ex)
        {
            await host.DisplayAlert("Folder picker error", $"Could not open folder picker: {ex.Message}", "OK");
            return null;
        }
#else
        await Task.CompletedTask;
        return null;
#endif
    }

    public static string DeriveFolderName(string domain, int projectId)
    {
        var root = domain;
        var dotIndex = root.IndexOf('.');
        if (dotIndex >= 0)
            root = root[..dotIndex];

        root = root.ToLowerInvariant();
        var builder = new StringBuilder();
        var lastWasHyphen = false;

        foreach (var ch in root)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var folderName = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(folderName)
            ? $"project-{projectId}"
            : folderName;
    }
}

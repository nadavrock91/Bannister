using Android.Content;
using Bannister.Services;

namespace Bannister.Platforms.Android;

public class AndroidPanelSaver : IPanelSaver
{
    public Task<string> SavePanelsAsync(
        IReadOnlyList<(string FileName, byte[] Bytes)> panels,
        string fallbackDirectory)
    {
        var context = global::Android.App.Application.Context;
        int saved = MediaStoreHelper.SavePanelsToGallery(context, panels);
        return Task.FromResult(
            $"Gallery → Pictures/BannisterCrops ({saved} panels)");
    }
}

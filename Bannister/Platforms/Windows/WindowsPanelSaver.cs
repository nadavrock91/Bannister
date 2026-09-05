using Bannister.Services;

namespace Bannister.Platforms.Windows;

public class WindowsPanelSaver : IPanelSaver
{
    public async Task<string> SavePanelsAsync(
        IReadOnlyList<(string FileName, byte[] Bytes)> panels,
        string fallbackDirectory)
    {
        Directory.CreateDirectory(fallbackDirectory);
        foreach (var (fileName, bytes) in panels)
        {
            var path = Path.Combine(fallbackDirectory, fileName);
            await File.WriteAllBytesAsync(path, bytes);
        }
        return fallbackDirectory;
    }
}

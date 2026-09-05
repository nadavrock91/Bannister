namespace Bannister.Services;

/// <summary>
/// Platform-specific panel saving. Returns a human-readable
/// description of where files were saved.
/// </summary>
public interface IPanelSaver
{
    Task<string> SavePanelsAsync(
        IReadOnlyList<(string FileName, byte[] Bytes)> panels,
        string fallbackDirectory);
}

namespace Bannister.Helpers;

/// <summary>
/// Helper class for image path operations
/// </summary>
public static class ImageHelper
{
    public static string GetFullImagePath(string? storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return "";

        if (System.IO.Path.IsPathRooted(storedPath))
        {
            if (System.IO.File.Exists(storedPath))
                return storedPath;

            storedPath = System.IO.Path.GetFileName(storedPath);
        }

        string imagesFolder = System.IO.Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
        string fullPath = System.IO.Path.Combine(imagesFolder, storedPath);

        return System.IO.File.Exists(fullPath) ? fullPath : "";
    }
}

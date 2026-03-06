using Microsoft.Maui.Controls;
using System;
using System.Globalization;
using System.IO;

namespace Bannister.Converters
{
    public class ImagePathConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string storedPath || string.IsNullOrWhiteSpace(storedPath))
                return null;

            // If it's already a full path (old data), check if file exists
            if (Path.IsPathRooted(storedPath))
            {
                if (File.Exists(storedPath))
                    return storedPath;

                // Old path doesn't exist - try extracting filename
                storedPath = Path.GetFileName(storedPath);
            }

            // Construct full path from filename
            string imagesFolder = Path.Combine(FileSystem.AppDataDirectory, "ActivityImages");
            string fullPath = Path.Combine(imagesFolder, storedPath);

            // Return the path if file exists, null otherwise
            return File.Exists(fullPath) ? fullPath : null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
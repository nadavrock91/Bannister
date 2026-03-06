using System.Globalization;

namespace Bannister.Converters;

/// <summary>
/// Converter to show section header only when it's not empty
/// </summary>
public class SectionHeaderVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string header)
        {
            return !string.IsNullOrWhiteSpace(header);
        }
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

using System.Globalization;

namespace Bannister.Converters;

/// <summary>
/// Converter to show "+" when not selected, "-" when selected
/// </summary>
public class SelectionButtonTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool isSelected)
        {
            return isSelected ? "−" : "+"; // Using minus sign (−) for selected
        }
        return "+";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

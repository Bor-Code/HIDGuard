using System.Globalization;
using System.Windows.Data;

namespace HidGuard.App.Converters;

public sealed class StringEqualsConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return string.Equals(value as string, parameter as string, StringComparison.OrdinalIgnoreCase);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter is string paramStr)
        {
            return paramStr;
        }
        return System.Windows.Data.Binding.DoNothing;
    }
}

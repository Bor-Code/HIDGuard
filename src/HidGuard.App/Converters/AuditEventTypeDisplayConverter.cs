using System.Globalization;
using System.Windows.Data;
using HidGuard.App.ViewModels;
using HidGuard.Core.Audit;

namespace HidGuard.App.Converters;

public sealed class AuditEventTypeDisplayConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is AuditEventType type ? DisplayText.ForEventType(type) : string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

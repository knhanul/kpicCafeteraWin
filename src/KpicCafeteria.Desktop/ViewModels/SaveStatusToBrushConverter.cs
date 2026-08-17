using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>
/// "Warning" → WarningBrush, "Success" → SuccessBrush, else TextSecondaryBrush.
/// </summary>
public sealed class SaveStatusToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush Warning = (SolidColorBrush)System.Windows.Application.Current.FindResource("WarningBrush");
    private static readonly SolidColorBrush Success = (SolidColorBrush)System.Windows.Application.Current.FindResource("SuccessBrush");
    private static readonly SolidColorBrush Default = (SolidColorBrush)System.Windows.Application.Current.FindResource("TextSecondaryBrush");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Warning" => Warning,
            "Success" => Success,
            _ => Default,
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

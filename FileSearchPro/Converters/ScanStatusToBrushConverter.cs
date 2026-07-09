using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using FileSearchPro.Models;

namespace FileSearchPro.Converters;

public class ScanStatusToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is ScanLogEntryStatus status ? status switch
        {
            ScanLogEntryStatus.Online => Brushes.LimeGreen,
            ScanLogEntryStatus.Offline => Brushes.Orange,
            ScanLogEntryStatus.Unreachable => Brushes.Gray,
            ScanLogEntryStatus.Error => Brushes.Red,
            ScanLogEntryStatus.Scanning => Brushes.DodgerBlue,
            ScanLogEntryStatus.Complete => Brushes.LightBlue,
            ScanLogEntryStatus.Info => Brushes.LightGray,
            _ => Brushes.LightGray
        } : Brushes.LightGray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

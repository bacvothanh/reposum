using System.Globalization;
using System.Windows.Data;

namespace RepoSum.UI;

public sealed class BooleanToReadLabelConverter : IValueConverter
{
    public static BooleanToReadLabelConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is true ? "Mark unread" : "Mark read";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

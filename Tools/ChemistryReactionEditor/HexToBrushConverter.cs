using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ChemistryReactionEditor;

public class HexToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string hex = value as string ?? "#FFFFFF";
        Color color = ColorConversion.NormalizeOrDefault(hex);
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PrismEditor
{
    public class BuiltToColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            (value is bool b && b) ? (Color)ColorConverter.ConvertFromString("#cfa44a") : (Color)ColorConverter.ConvertFromString("#5a4a3a");

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public class BuiltToStatusConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            (value is bool b && b) ? "Built" : "Empty / un-leveled";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }

    public class BuiltToOpacityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            (value is bool b && b) ? 1.0 : 0.4;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
}

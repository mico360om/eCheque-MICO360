using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace eCheque.MICO360.Converters
{
    public class StringToVisibilityConverter : IValueConverter
    {
        public static readonly StringToVisibilityConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public static readonly BoolToVisibilityConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public static readonly InverseBoolToVisibilityConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}

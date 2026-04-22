using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace AarohiWpfControls.Controls.DataGridView
{
    /// <summary>
    /// Converts boolean values to Visibility (True = Visible, False = Collapsed)
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Visible : Visibility.Collapsed;
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Visibility v)
                return v == Visibility.Visible;
            return false;
        }
    }

    /// <summary>
    /// Converts boolean values to Visibility (True = Collapsed, False = Visible) - inverse
    /// </summary>
    public class BoolToVisibilityInverseConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Visibility v)
                return v == Visibility.Collapsed;
            return true;
        }
    }

    /// <summary>
    /// Converts integer row count to a formatted string
    /// </summary>
    public class RowCountConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int count)
                return $"{count} row{(count != 1 ? "s" : "")}";
            return "0 rows";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts page information to display string
    /// </summary>
    public class PageInfoConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (values.Length >= 2 && int.TryParse(values[0].ToString(), out int current) &&
                int.TryParse(values[1].ToString(), out int total))
            {
                return $"Page {current} of {total}";
            }
            return "Page 1 of 1";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts null/empty values to a placeholder string
    /// </summary>
    public class NullToPlaceholderConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value == null || value == DBNull.Value || string.IsNullOrWhiteSpace(value.ToString()))
                return parameter?.ToString() ?? "(empty)";
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean to button enabled state
    /// </summary>
    public class BoolToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool b && b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return value is bool b && b;
        }
    }
}

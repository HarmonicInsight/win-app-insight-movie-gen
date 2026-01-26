namespace InsightMovie.Converters;

using System;
using System.Globalization;
using System.Windows.Data;

/// <summary>
/// Inverts a <see cref="bool"/> value.
/// <c>true</c> becomes <c>false</c> and vice-versa.
/// </summary>
[ValueConversion(typeof(bool), typeof(bool))]
public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return !boolValue;
        }

        return value;
    }
}

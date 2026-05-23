using System;
using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;

namespace FormaStream.Infrastructure.Converters;

/// <summary>
/// Конвертер, который сравнивает переданное значение (value) с параметром (parameter).
/// Если они равны (и не null), возвращает true. Иначе — false.
/// </summary>
public class BoolToClassConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isActive && isActive)
        {
            return "active";
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}


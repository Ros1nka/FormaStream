using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FormaStream.Infrastructure.Converters;

public class StringNotEmptyToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Возвращает true, если строка не null, не пустая и не состоит из пробелов
        return value is string str && !string.IsNullOrWhiteSpace(str);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
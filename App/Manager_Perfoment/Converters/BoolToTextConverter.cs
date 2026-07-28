using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace GM95.App.ManagerPerformance.Converters;

/// <summary>
/// Doi bool -> chu tieng Viet de doc trong luoi (thay vi hien "True/False").
/// Chuoi hien thi dat qua ConverterParameter dang "khi-true|khi-false";
/// khong truyen thi mac dinh "Có|—".
/// </summary>
public sealed class BoolToTextConverter : IValueConverter
{
    /// <summary>Cap chu mac dinh khi khong truyen ConverterParameter.</summary>
    public string TrueText { get; set; } = "Có";
    public string FalseText { get; set; } = "—";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var (t, f) = (TrueText, FalseText);
        if (parameter is string p && p.Contains('|'))
        {
            var parts = p.Split('|', 2);
            (t, f) = (parts[0], parts[1]);
        }
        return value is true ? t : f;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

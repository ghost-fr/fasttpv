using Avalonia.Data.Converters;
using FastTPV.Core.Models;
using System;
using System.Globalization;

namespace FastTPV.Desktop.Converters;

/// <summary>
/// True when an article's real stock has fallen to or below its own configured
/// minimum stock level (Article.MinimumStock). No numbers are invented here —
/// both sides of the comparison come from the article record itself.
/// </summary>
public class LowStockConverter : IValueConverter
{
    public static readonly LowStockConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Article article)
            return article.StockLevel <= article.MinimumStock;
        return false;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Renders "3 left" when stock is at/under the article's own minimum, otherwise
/// "Stock 42" — same real StockLevel value, just phrased to draw attention when low.
/// </summary>
public class StockLabelConverter : IValueConverter
{
    public static readonly StockLabelConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is Article article)
        {
            return article.StockLevel <= article.MinimumStock
                ? $"{article.StockLevel} left"
                : $"Stock {article.StockLevel}";
        }
        return string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

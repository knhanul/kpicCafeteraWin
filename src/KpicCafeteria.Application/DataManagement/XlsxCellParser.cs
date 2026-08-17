using System.Globalization;
using ClosedXML.Excel;

namespace KpicCafeteria.Application.DataManagement;

/// <summary>XLSX 셀 값 파싱 헬퍼.</summary>
public static class XlsxCellParser
{
    public static string CleanText(object? value)
        => value is null ? string.Empty : value.ToString()!.Trim();

    public static int? CleanInt(object? value)
    {
        if (value is null or "")
            return null;
        if (value is int i)
            return i;
        if (value is double d)
            return double.IsNaN(d) ? null : (int)d;
        if (value is long l)
            return (int)l;
        if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
            return (int)m;
        return int.TryParse(value.ToString(), out var n) ? n : null;
    }

    public static double? CleanDouble(object? value)
    {
        if (value is null or "")
            return null;
        if (value is double d)
            return d;
        if (value is int i)
            return i;
        if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var m))
            return (double)m;
        return double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var f) ? f : null;
    }

    public static bool CleanBool(object? value, bool defaultValue = false)
    {
        var text = CleanText(value).ToUpperInvariant();
        if (string.IsNullOrEmpty(text))
            return defaultValue;
        if (text is "Y" or "YES" or "TRUE" or "1" or "사용" or "포함")
            return true;
        if (text is "N" or "NO" or "FALSE" or "0" or "미사용" or "제외")
            return false;
        return defaultValue;
    }

    public static TimeOnly? ParseTime(object? value)
    {
        var text = CleanText(value);
        if (string.IsNullOrEmpty(text))
            return null;

        if (TimeOnly.TryParse(text, CultureInfo.InvariantCulture, out var t))
            return t;

        if (value is double d)
        {
            var totalSeconds = (int)(d * 24 * 3600);
            return new TimeOnly(totalSeconds / 3600, (totalSeconds / 60) % 60);
        }

        return null;
    }

    public static DateOnly? ParseDate(object? value)
    {
        if (value is null or "")
            return null;

        if (value is DateTime dt)
            return DateOnly.FromDateTime(dt);

        if (value is DateOnly d)
            return d;

        if (value is double serial)
            return DateOnly.FromDateTime(DateTime.FromOADate(serial));

        var text = CleanText(value);
        foreach (var fmt in new[] { "yyyy-MM-dd", "yyyy.MM.dd", "yyyy/MM/dd", "yyyy-MM-dd HH:mm:ss" })
        {
            if (DateTime.TryParseExact(text, fmt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                return DateOnly.FromDateTime(parsed);
        }

        if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallback))
            return DateOnly.FromDateTime(fallback);

        return null;
    }

    public static object? GetValueOrDefault(this Dictionary<string, object?> row, string key)
        => row.TryGetValue(key, out var value) ? value : null;

    public static object? GetCellValue(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank)
            return null;
        if (value.IsText)
            return value.GetText();
        if (value.IsNumber)
        {
            var d = value.GetNumber();
            return d == Math.Truncate(d) ? (long)d : d;
        }
        if (value.IsDateTime)
            return value.GetDateTime();
        if (value.IsBoolean)
            return value.GetBoolean();
        return value.ToString();
    }
}

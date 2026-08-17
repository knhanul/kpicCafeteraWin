using System.Globalization;

namespace KpicCafeteria.Desktop.ViewModels.Statistics;

/// <summary>통계 표시용 포맷 헬퍼.</summary>
public static class StatisticsFormat
{
    public static string Number(int? value) => value?.ToString("N0", CultureInfo.InvariantCulture) ?? "-";

    public static string Number(double? value) => value?.ToString("N1", CultureInfo.InvariantCulture) ?? "-";

    public static string Percent(double? value) => value is null ? "-" : $"{value.Value.ToString("N1", CultureInfo.InvariantCulture)}%";

    public static string SignedPercent(double? value) => value is null ? "-" : $"{(value.Value >= 0 ? "+" : "")}{value.Value.ToString("N1", CultureInfo.InvariantCulture)}%";

    public static string SignedNumber(int? value) => value is null ? "-" : $"{(value.Value >= 0 ? "+" : "")}{value.Value.ToString("N0", CultureInfo.InvariantCulture)}";

    public static string Date(DateOnly? value) => value?.ToString("yyyy-MM-dd") ?? "-";

    public static string DateTime(DateTime? value) => value?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "-";
}

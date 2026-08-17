namespace KpicCafeteria.Application.Statistics;

/// <summary>
/// 통계 기간 프리셋 항목. WPF ComboBox 바인딩용 클래스.
/// </summary>
public sealed class PresetItem
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
}

/// <summary>
/// 통계 기간 선택 규칙.
/// 기존 Web 구현(app.js periodRange)의 프리셋을 그대로 유지한다.
/// </summary>
public static class StatisticsPeriod
{
    public const string ThisMonth = "this-month";
    public const string Last3Months = "3m";
    public const string Last6Months = "6m";
    public const string Last12Months = "12m";
    public const string Year = "year";
    public const string Custom = "custom";

    public static readonly IReadOnlyList<PresetItem> Presets =
    [
        new() { Key = ThisMonth, Label = "이번 달" },
        new() { Key = Last3Months, Label = "최근 3개월" },
        new() { Key = Last6Months, Label = "최근 6개월" },
        new() { Key = Last12Months, Label = "최근 12개월" },
        new() { Key = Year, Label = "올해" },
        new() { Key = Custom, Label = "직접 선택" },
    ];

    /// <summary>
    /// 프리셋 기간 계산. custom은 반드시 start/end를 직접 지정한다.
    /// end는 오늘(또는 주입된 기준일), start는 프리셋 규칙에 따라 계산한다.
    /// </summary>
    public static (DateOnly Start, DateOnly End) Resolve(
        string preset,
        DateOnly? customStart = null,
        DateOnly? customEnd = null,
        DateOnly? referenceToday = null)
    {
        var end = referenceToday ?? DateOnly.FromDateTime(DateTime.Today);
        var start = preset switch
        {
            ThisMonth => new DateOnly(end.Year, end.Month, 1),
            Year => new DateOnly(end.Year, 1, 1),
            Last3Months => MonthsBackFirstDay(end, 3),
            Last6Months => MonthsBackFirstDay(end, 6),
            Last12Months => MonthsBackFirstDay(end, 12),
            Custom => customStart ?? end,
            _ => throw new StatisticsException($"지원하지 않는 기간 프리셋입니다: {preset}"),
        };

        var resolvedEnd = preset == Custom ? (customEnd ?? end) : end;
        return resolvedEnd < start ? (resolvedEnd, start) : (start, resolvedEnd);
    }

    /// <summary>end 기준 months개월 전 1일 (예: 6m → 5개월 전 1일 ~ 오늘).</summary>
    private static DateOnly MonthsBackFirstDay(DateOnly value, int months)
    {
        var total = value.Year * 12 + (value.Month - 1) - (months - 1);
        return new DateOnly(total / 12, total % 12 + 1, 1);
    }
}

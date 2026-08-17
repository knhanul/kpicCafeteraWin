using KpicCafeteria.Application.Abstractions.Repositories;

namespace KpicCafeteria.Application.Statistics;

/// <summary>
/// 식수 통계.
/// 기존 statistics_service.py meal_statistics / meal_trend에 대응.
/// </summary>
public sealed class MealStatisticsService
{
    public const int LookbackDays = 56;
    public const int MinComparison = 4;
    public const double CheckThreshold = 10.0;
    public const double ImportantThreshold = 15.0;

    private static readonly IReadOnlyDictionary<string, string> MealTypeNames =
        new Dictionary<string, string> { ["LUNCH"] = "중식", ["DINNER"] = "석식" };

    private readonly IStatisticsRepositoryFactory _factory;

    public MealStatisticsService(IStatisticsRepositoryFactory factory)
    {
        _factory = factory;
    }

    /// <summary>기간 내 식수 통계 (요약/중식·석식/요일별/백데이터/이상치).</summary>
    public async Task<MealStatisticsDto> GetAsync(
        DateOnly start, DateOnly end, string mealType = "all", CancellationToken cancellationToken = default)
    {
        using var repository = _factory.Create();
        var mealTypeCode = MealTypeCode(mealType);
        var services = await repository.GetMealServicesAsync(start, end, mealTypeCode, cancellationToken);
        var history = await repository.GetActualHistoryAsync(start.AddDays(-LookbackDays), end, cancellationToken);

        var plannedSum = services.Sum(s => s.PlannedCount);
        var actualRows = services.Where(s => s.ActualCount is not null).ToList();
        var actualSum = actualRows.Count > 0 ? actualRows.Sum(s => s.ActualCount!.Value) : (int?)null;
        var inputRate = services.Count > 0 ? Math.Round((double)actualRows.Count / services.Count * 100, 1) : (double?)null;

        var breakdown = new Dictionary<string, MealTypeBreakdownDto>();
        foreach (var (code, name) in MealTypeNames)
        {
            var subset = services.Where(s => s.MealType == code).ToList();
            var planned = subset.Sum(s => s.PlannedCount);
            var inputRows = subset.Where(s => s.ActualCount is not null).ToList();
            var actual = inputRows.Count > 0 ? inputRows.Sum(s => s.ActualCount!.Value) : (int?)null;
            breakdown[code.ToLowerInvariant()] = new MealTypeBreakdownDto(
                name,
                subset.Count,
                planned,
                actual,
                actual is not null ? actual - planned : null,
                DeviationRate(actual, planned),
                inputRows.Count,
                subset.Count > 0 ? Math.Round((double)inputRows.Count / subset.Count * 100, 1) : null);
        }

        var weekdayAverages = BuildWeekdayAverages(services);

        var backdata = new List<MealBackdataRowDto>();
        var anomalies = new List<MealAnomalyDto>();
        foreach (var service in services)
        {
            var actual = service.ActualCount;
            var planned = service.PlannedCount;
            var deviation = DeviationRate(actual, planned);
            var (usualMedian, usualCount) = UsualMedian(history, service.ServiceDate, service.MealType);
            var usualDeviation = usualMedian is not null ? DeviationRate(actual, usualMedian) : null;

            backdata.Add(new MealBackdataRowDto(
                service.ServiceDate,
                StatisticsWeekday.Name(service.ServiceDate),
                service.MealType,
                MealTypeNames.GetValueOrDefault(service.MealType, service.MealType),
                planned,
                actual,
                actual is not null ? actual - planned : null,
                deviation,
                usualMedian,
                usualCount,
                usualDeviation,
                actual is not null));

            var reasons = new List<AnomalyReasonDto>();
            if (deviation is not null && Math.Abs(deviation.Value) >= CheckThreshold)
            {
                reasons.Add(new AnomalyReasonDto(
                    "계획 대비", deviation.Value,
                    Math.Abs(deviation.Value) >= ImportantThreshold ? "중요" : "확인"));
            }

            if (usualDeviation is not null && Math.Abs(usualDeviation.Value) >= CheckThreshold)
            {
                reasons.Add(new AnomalyReasonDto(
                    "평소 대비", usualDeviation.Value,
                    Math.Abs(usualDeviation.Value) >= ImportantThreshold ? "중요" : "확인"));
            }

            if (reasons.Count > 0)
            {
                var top = reasons.OrderByDescending(r => Math.Abs(r.Value)).First();
                anomalies.Add(new MealAnomalyDto(
                    top.Value < 0 ? "식수 급감" : "식수 급증",
                    top.Level,
                    service.ServiceDate,
                    StatisticsWeekday.Name(service.ServiceDate),
                    MealTypeNames.GetValueOrDefault(service.MealType, service.MealType),
                    planned,
                    actual,
                    actual is not null ? actual - planned : null,
                    deviation,
                    usualMedian,
                    usualCount,
                    usualDeviation,
                    reasons,
                    usualMedian is null && usualCount < MinComparison));
            }
        }

        return new MealStatisticsDto(
            start,
            end,
            mealType,
            new MealSummaryDto(
                services.Count,
                actualRows.Count,
                inputRate,
                plannedSum,
                actualSum,
                actualSum is not null ? actualSum - plannedSum : null,
                actualSum is not null ? DeviationRate(actualSum, plannedSum) : null),
            breakdown,
            weekdayAverages,
            backdata,
            anomalies
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.MealTypeName)
                .ToList());
    }

    /// <summary>기간 내 월별 계획/실제 식수 추세.</summary>
    public async Task<MealTrendDto> GetTrendAsync(
        DateOnly start, DateOnly end, string mealType = "all", CancellationToken cancellationToken = default)
    {
        using var repository = _factory.Create();
        var mealTypeCode = MealTypeCode(mealType);
        var services = await repository.GetMealServicesAsync(start, end, mealTypeCode, cancellationToken);

        var buckets = new Dictionary<string, (int Planned, int Actual, int PlannedDays, int ActualDays)>();
        foreach (var service in services)
        {
            var key = service.ServiceDate.ToString("yyyy-MM");
            var bucket = buckets.GetValueOrDefault(key);
            bucket.Planned += service.PlannedCount;
            bucket.PlannedDays += 1;
            if (service.ActualCount is not null)
            {
                bucket.Actual += service.ActualCount.Value;
                bucket.ActualDays += 1;
            }

            buckets[key] = bucket;
        }

        return new MealTrendDto(
            start,
            end,
            mealType,
            buckets
                .OrderBy(kv => kv.Key)
                .Select(kv => new MealTrendPointDto(kv.Key, kv.Value.Planned, kv.Value.Actual, kv.Value.PlannedDays, kv.Value.ActualDays))
                .ToList());
    }

    // =======================================================================
    // 계산 규칙 (Python 구현과 동일)
    // =======================================================================

    /// <summary>"all"/"lunch"/"dinner" → "LUNCH"/"DINNER"/null.</summary>
    public static string? MealTypeCode(string mealType) => mealType.ToLowerInvariant() switch
    {
        "lunch" => "LUNCH",
        "dinner" => "DINNER",
        _ => null,
    };

    /// <summary>편차율 = (actual - base) / base * 100. base가 0 이하이면 null.</summary>
    public static double? DeviationRate(int? actual, double? baseValue)
    {
        if (actual is null || baseValue is null || baseValue <= 0)
        {
            return null;
        }

        return Math.Round(((double)actual.Value - baseValue.Value) / baseValue.Value * 100, 1);
    }

    /// <summary>
    /// 평소 중앙값: serviceDate 이전 56일(경계 포함) 동안 같은 MealType의 실제 식수 중앙값.
    /// 비교 데이터가 4건 미만이면 (null, 건수) 반환.
    /// </summary>
    public static (double? Median, int Count) UsualMedian(
        IReadOnlyList<ActualHistoryRow> history, DateOnly serviceDate, string mealType)
    {
        var cutoff = serviceDate.AddDays(-LookbackDays);
        var values = history
            .Where(row => row.ServiceDate < serviceDate && row.ServiceDate >= cutoff && row.MealType == mealType)
            .Select(row => row.ActualCount)
            .ToList();
        if (values.Count < MinComparison)
        {
            return (null, values.Count);
        }

        return (Math.Round(Median(values), 1), values.Count);
    }

    /// <summary>Python statistics.median과 동일: 홀수 개는 중앙값, 짝수 개는 중간 두 값의 평균.</summary>
    public static double Median(IReadOnlyList<int> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var count = sorted.Count;
        if (count == 0)
        {
            throw new InvalidOperationException("중앙값 계산에 빈 데이터는 사용할 수 없습니다.");
        }

        var mid = count / 2;
        return count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2.0;
    }

    private static List<WeekdayAverageDto> BuildWeekdayAverages(IReadOnlyList<MealServiceRow> services)
    {
        var groups = new Dictionary<int, List<(int Planned, int? Actual)>>();
        foreach (var service in services)
        {
            var weekday = StatisticsWeekday.Index(service.ServiceDate); // 0=월
            if (!groups.TryGetValue(weekday, out var list))
            {
                list = [];
                groups[weekday] = list;
            }

            list.Add((service.PlannedCount, service.ActualCount));
        }

        var result = new List<WeekdayAverageDto>();
        for (var weekday = 0; weekday < 5; weekday++) // 월~금
        {
            var rows = groups.GetValueOrDefault(weekday, []);
            var plannedAverage = rows.Count > 0
                ? Math.Round((double)rows.Sum(r => r.Planned) / rows.Count, 1)
                : (double?)null;
            var actualRows = rows.Where(r => r.Actual is not null).ToList();
            var actualAverage = actualRows.Count > 0
                ? Math.Round((double)actualRows.Sum(r => r.Actual!.Value) / actualRows.Count, 1)
                : (double?)null;
            result.Add(new WeekdayAverageDto(StatisticsWeekday.Names[weekday], plannedAverage, actualAverage, rows.Count, actualRows.Count));
        }

        return result;
    }
}

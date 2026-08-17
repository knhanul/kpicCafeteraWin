using KpicCafeteria.Application.Abstractions.Repositories;

namespace KpicCafeteria.Application.Statistics;

/// <summary>
/// 운영 기록 통계.
/// 기존 operations_statistics.py에 대응.
/// 완료율 분모 = 기간 내 MealService 건수, 분자 = 각 기록 완료 건수.
/// </summary>
public sealed class OperationsStatisticsService
{
    public const int LateInputDays = 1;

    private static readonly IReadOnlyDictionary<string, string> MealTypeNames =
        new Dictionary<string, string> { ["LUNCH"] = "중식", ["DINNER"] = "석식" };

    private readonly IStatisticsRepositoryFactory _factory;

    public OperationsStatisticsService(IStatisticsRepositoryFactory factory)
    {
        _factory = factory;
    }

    /// <summary>기간 내 운영 기록 통계 (완료율/누락/지연/보존식 분석/백데이터).</summary>
    public async Task<OperationsStatisticsDto> GetAsync(
        DateOnly start, DateOnly end, string mealType = "all", CancellationToken cancellationToken = default)
    {
        using var repository = _factory.Create();
        var mealTypeCode = MealStatisticsService.MealTypeCode(mealType);
        var services = await repository.GetMealServicesAsync(start, end, mealTypeCode, cancellationToken);
        var rows = services.Select(ToRow).ToList();

        var actualInputCount = rows.Count(r => r.ActualInput);
        var preservationCount = rows.Count(r => r.PreservationCompleted);
        var mealPlanCount = rows.Count(r => r.MealPlanOutput);
        var cookingCount = rows.Count(r => r.CookingOutput);
        var total = rows.Count;

        var breakdown = new Dictionary<string, OperationsBreakdownDto>();
        foreach (var (code, name) in MealTypeNames)
        {
            var subset = rows.Where(r => r.MealType == code).ToList();
            var subTotal = subset.Count;
            var subActual = subset.Count(r => r.ActualInput);
            var subPreservation = subset.Count(r => r.PreservationCompleted);
            var subMealPlan = subset.Count(r => r.MealPlanOutput);
            var subCooking = subset.Count(r => r.CookingOutput);
            breakdown[code.ToLowerInvariant()] = new OperationsBreakdownDto(
                name,
                subTotal,
                subActual,
                Rate(subActual, subTotal),
                subPreservation,
                Rate(subPreservation, subTotal),
                subMealPlan,
                Rate(subMealPlan, subTotal),
                subCooking,
                Rate(subCooking, subTotal));
        }

        var trend = BuildTrend(rows);

        var recordGaps = new List<RecordGapDto>();
        var lateInputs = new List<LateInputDto>();
        foreach (var row in rows)
        {
            if (!row.ActualInput)
            {
                recordGaps.Add(new RecordGapDto("실제 식수 미입력", row.Date, row.Weekday, row.MealTypeName));
            }

            if (!row.PreservationCompleted)
            {
                recordGaps.Add(new RecordGapDto("보존식 기록 미완료", row.Date, row.Weekday, row.MealTypeName));
            }

            if (!row.MealPlanOutput)
            {
                recordGaps.Add(new RecordGapDto("식단표 미출력", row.Date, row.Weekday, row.MealTypeName));
            }

            if (!row.CookingOutput)
            {
                recordGaps.Add(new RecordGapDto("조리지시서 미출력", row.Date, row.Weekday, row.MealTypeName));
            }

            if (row.ActualLate)
            {
                lateInputs.Add(new LateInputDto(
                    row.Date, row.Weekday, row.MealTypeName, row.PlannedCount, row.ActualCount, row.ActualRecordedAt));
            }
        }

        var byManager = new Dictionary<string, int>();
        var temperatureRecords = new List<TemperatureRecordDto>();
        var collectedCount = 0;
        var disposedCount = 0;
        foreach (var row in rows)
        {
            if (row.PreservationCollected)
            {
                collectedCount++;
            }

            if (row.PreservationDisposed)
            {
                disposedCount++;
            }

            if (!string.IsNullOrWhiteSpace(row.PreservationManager))
            {
                byManager[row.PreservationManager!] = byManager.GetValueOrDefault(row.PreservationManager!) + 1;
            }

            if (!string.IsNullOrWhiteSpace(row.PreservationTemperature))
            {
                temperatureRecords.Add(new TemperatureRecordDto(
                    row.Date, row.MealTypeName, row.PreservationTemperature!, row.PreservationManager));
            }
        }

        return new OperationsStatisticsDto(
            start,
            end,
            mealType,
            new OperationsSummaryDto(
                total,
                actualInputCount,
                Rate(actualInputCount, total),
                preservationCount,
                Rate(preservationCount, total),
                mealPlanCount,
                Rate(mealPlanCount, total),
                cookingCount,
                Rate(cookingCount, total)),
            breakdown,
            trend,
            new OperationsAnomaliesDto(recordGaps, lateInputs),
            new PreservationSummaryDto(
                collectedCount,
                Rate(collectedCount, total),
                disposedCount,
                Rate(disposedCount, total),
                byManager
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => new ManagerCountDto(kv.Key, kv.Value))
                    .ToList(),
                temperatureRecords),
            rows);
    }

    // =======================================================================
    // 계산 규칙 (Python 구현과 동일)
    // =======================================================================

    /// <summary>완료율 = count / total * 100. total이 0이면 null.</summary>
    public static double? Rate(int count, int total)
        => total <= 0 ? null : Math.Round((double)count / total * 100, 1);

    /// <summary>지연 입력 여부: RecordedAt 날짜 - ServiceDate &gt; 1일.</summary>
    public static bool IsLateInput(DateOnly serviceDate, DateTime? recordedAt)
    {
        if (recordedAt is null)
        {
            return false;
        }

        var recordedDate = DateOnly.FromDateTime(recordedAt.Value);
        return recordedDate.DayNumber - serviceDate.DayNumber > LateInputDays;
    }

    private static OperationsBackdataRowDto ToRow(MealServiceRow service)
    {
        var actualCount = service.ActualCount;
        var recordedAt = service.RecordedAt;
        return new OperationsBackdataRowDto(
            service.ServiceDate,
            StatisticsWeekday.Name(service.ServiceDate),
            service.MealType,
            MealTypeNames.GetValueOrDefault(service.MealType, service.MealType),
            service.PlannedCount,
            actualCount,
            actualCount is not null,
            recordedAt,
            IsLateInput(service.ServiceDate, recordedAt),
            service.MealPlanOutputAt is not null,
            service.MealPlanOutputAt,
            service.CookingOutputAt is not null,
            service.CookingOutputAt,
            service.PreservationCompleted,
            service.PreservationCollected,
            service.PreservationDisposed,
            service.PreservationManager,
            service.PreservationTemperature);
    }

    private static List<OperationsTrendPointDto> BuildTrend(IReadOnlyList<OperationsBackdataRowDto> rows)
    {
        var buckets = new Dictionary<string, (int ServiceCount, int Actual, int Preservation, int MealPlan, int Cooking)>();
        foreach (var row in rows)
        {
            var key = row.Date.ToString("yyyy-MM");
            var bucket = buckets.GetValueOrDefault(key);
            bucket.ServiceCount++;
            if (row.ActualInput)
            {
                bucket.Actual++;
            }

            if (row.PreservationCompleted)
            {
                bucket.Preservation++;
            }

            if (row.MealPlanOutput)
            {
                bucket.MealPlan++;
            }

            if (row.CookingOutput)
            {
                bucket.Cooking++;
            }

            buckets[key] = bucket;
        }

        return buckets
            .OrderBy(kv => kv.Key)
            .Select(kv => new OperationsTrendPointDto(
                kv.Key,
                kv.Value.ServiceCount,
                Rate(kv.Value.Actual, kv.Value.ServiceCount),
                Rate(kv.Value.Preservation, kv.Value.ServiceCount),
                Rate(kv.Value.MealPlan, kv.Value.ServiceCount),
                Rate(kv.Value.Cooking, kv.Value.ServiceCount)))
            .ToList();
    }
}

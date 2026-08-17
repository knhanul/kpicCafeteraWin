namespace KpicCafeteria.Application.Statistics;

// =======================================================================
// 식수 통계
// =======================================================================

public sealed record MealSummaryDto(
    int ServiceCount,
    int InputCount,
    double? InputRate,
    int PlannedSum,
    int? ActualSum,
    int? Diff,
    double? DeviationRate);

public sealed record MealTypeBreakdownDto(
    string MealTypeName,
    int ServiceCount,
    int PlannedSum,
    int? ActualSum,
    int? Diff,
    double? DeviationRate,
    int InputCount,
    double? InputRate);

public sealed record WeekdayAverageDto(
    string Weekday,
    double? PlannedAverage,
    double? ActualAverage,
    int Records,
    int ActualRecords);

public sealed record MealBackdataRowDto(
    DateOnly Date,
    string Weekday,
    string MealType,
    string MealTypeName,
    int PlannedCount,
    int? ActualCount,
    int? Diff,
    double? DeviationRate,
    double? UsualMedian,
    int UsualCount,
    double? UsualDeviationRate,
    bool Input);

public sealed record AnomalyReasonDto(string Basis, double Value, string Level);

public sealed record MealAnomalyDto(
    string Type,
    string Level,
    DateOnly Date,
    string Weekday,
    string MealTypeName,
    int PlannedCount,
    int? ActualCount,
    int? Diff,
    double? DeviationRate,
    double? UsualMedian,
    int UsualCount,
    double? UsualDeviationRate,
    IReadOnlyList<AnomalyReasonDto> Reasons,
    bool InsufficientComparison);

public sealed record MealStatisticsDto(
    DateOnly StartDate,
    DateOnly EndDate,
    string MealType,
    MealSummaryDto Summary,
    IReadOnlyDictionary<string, MealTypeBreakdownDto> Breakdown,
    IReadOnlyList<WeekdayAverageDto> WeekdayAverages,
    IReadOnlyList<MealBackdataRowDto> Backdata,
    IReadOnlyList<MealAnomalyDto> Anomalies);

public sealed record MealTrendPointDto(string Month, int Planned, int Actual, int PlannedDays, int ActualDays);

public sealed record MealTrendDto(
    DateOnly StartDate,
    DateOnly EndDate,
    string MealType,
    IReadOnlyList<MealTrendPointDto> Trend);

// =======================================================================
// 메뉴 통계
// =======================================================================

public sealed record MenuSummaryDto(
    int UniqueMenuCount,
    int TotalUsageCount,
    int NewMenuCount,
    int RepeatMenuCount,
    int UnusedMenuCount);

public sealed record MenuTopDto(
    int? MenuId,
    string MenuName,
    string Role,
    int UsageCount,
    int LunchCount,
    int DinnerCount,
    DateOnly? FirstUsed,
    DateOnly? LastUsed,
    double? AvgInterval);

public sealed record MenuRepeatDto(int? MenuId, string MenuName, string Type, int Count, int WindowDays);

public sealed record UnusedMenuDto(int MenuId, string MenuName, DateOnly? LastUsed, int? DaysSinceLast);

public sealed record MenuUsageBackdataRowDto(
    DateOnly Date,
    string Weekday,
    string MealTypeName,
    string Role,
    string MenuName,
    int? MenuId,
    int PlannedCount,
    int? ActualCount,
    DateOnly? PreviousUsedDate,
    int? DaysSincePrevious);

public sealed record MenuStatisticsDto(
    DateOnly StartDate,
    DateOnly EndDate,
    string MealType,
    int UnusedDays,
    MenuSummaryDto Summary,
    IReadOnlyList<MenuTopDto> TopMenus,
    IReadOnlyList<MenuRepeatDto> Repeats,
    IReadOnlyList<UnusedMenuDto> UnusedMenus,
    IReadOnlyList<MenuUsageBackdataRowDto> Backdata);

public sealed record MenuDetailSummaryDto(
    int UsageCount,
    int LunchCount,
    int DinnerCount,
    DateOnly? FirstUsed,
    DateOnly? LastUsed,
    double? AvgInterval);

public sealed record MonthlyUsageDto(string Month, int Count);

public sealed record MenuRecentHistoryDto(DateOnly Date, string MealTypeName, int PlannedCount, int? ActualCount);

public sealed record CoUsedMenuDto(int? MenuId, string MenuName, int Count);

public sealed record MenuDetailDto(
    int MenuId,
    string MenuName,
    string Role,
    MenuDetailSummaryDto Summary,
    IReadOnlyList<MonthlyUsageDto> MonthlyUsage,
    IReadOnlyList<MenuRecentHistoryDto> RecentHistory,
    IReadOnlyList<CoUsedMenuDto> CoUsed,
    IReadOnlyList<MenuUsageBackdataRowDto> Backdata);

// =======================================================================
// 식재료 통계
// =======================================================================

public sealed record IngredientSummaryDto(
    int UniqueIngredientCount,
    int TotalUsageCount,
    int NewIngredientCount,
    int UnusedIngredientCount);

public sealed record IngredientTopDto(
    int? IngredientId,
    string IngredientName,
    string StatGroup,
    int UsageCount,
    double Quantity,
    int LunchCount,
    int DinnerCount,
    DateOnly? FirstUsed,
    DateOnly? LastUsed,
    double? AvgInterval);

public sealed record UnusedIngredientDto(int IngredientId, string IngredientName, string StatGroup, DateOnly? LastUsed, int? DaysSinceLast);

public sealed record IngredientUsageBackdataRowDto(
    DateOnly Date,
    string Weekday,
    string MealTypeName,
    string IngredientName,
    int? IngredientId,
    double? Quantity,
    int PlannedCount,
    int? ActualCount,
    DateOnly? PreviousUsedDate,
    int? DaysSincePrevious);

public sealed record IngredientStatisticsDto(
    DateOnly StartDate,
    DateOnly EndDate,
    string MealType,
    int UnusedDays,
    IngredientSummaryDto Summary,
    IReadOnlyList<IngredientTopDto> TopIngredients,
    IReadOnlyList<UnusedIngredientDto> UnusedIngredients,
    IReadOnlyList<IngredientUsageBackdataRowDto> Backdata);

public sealed record IngredientDetailSummaryDto(
    int UsageCount,
    int LunchCount,
    int DinnerCount,
    double Quantity,
    DateOnly? FirstUsed,
    DateOnly? LastUsed,
    double? AvgInterval);

public sealed record IngredientRecentHistoryDto(DateOnly Date, string MealTypeName, string MenuName, double? Quantity, int? ActualCount);

public sealed record CoUsedIngredientDto(int? IngredientId, string IngredientName, int Count);

public sealed record IngredientDetailDto(
    int IngredientId,
    string IngredientName,
    string StatGroup,
    IngredientDetailSummaryDto Summary,
    IReadOnlyList<MonthlyUsageDto> MonthlyUsage,
    IReadOnlyList<IngredientRecentHistoryDto> RecentHistory,
    IReadOnlyList<CoUsedIngredientDto> CoUsed,
    IReadOnlyList<IngredientUsageBackdataRowDto> Backdata);

// =======================================================================
// 운영 기록 통계
// =======================================================================

public sealed record OperationsSummaryDto(
    int ServiceCount,
    int ActualInputCount,
    double? ActualInputRate,
    int PreservationCount,
    double? PreservationRate,
    int MealPlanOutputCount,
    double? MealPlanOutputRate,
    int CookingOutputCount,
    double? CookingOutputRate);

public sealed record OperationsBreakdownDto(
    string MealTypeName,
    int ServiceCount,
    int ActualInputCount,
    double? ActualInputRate,
    int PreservationCount,
    double? PreservationRate,
    int MealPlanOutputCount,
    double? MealPlanOutputRate,
    int CookingOutputCount,
    double? CookingOutputRate);

public sealed record OperationsTrendPointDto(
    string Month,
    int ServiceCount,
    double? ActualInputRate,
    double? PreservationRate,
    double? MealPlanOutputRate,
    double? CookingOutputRate);

public sealed record RecordGapDto(string Type, DateOnly Date, string Weekday, string MealTypeName);

public sealed record LateInputDto(DateOnly Date, string Weekday, string MealTypeName, int PlannedCount, int? ActualCount, DateTime? RecordedAt);

public sealed record ManagerCountDto(string ManagerName, int Count);

public sealed record TemperatureRecordDto(DateOnly Date, string MealTypeName, string Temperature, string? ManagerName);

public sealed record PreservationSummaryDto(
    int CollectedCount,
    double? CollectedRate,
    int DisposedCount,
    double? DisposedRate,
    IReadOnlyList<ManagerCountDto> ByManager,
    IReadOnlyList<TemperatureRecordDto> TemperatureRecords);

public sealed record OperationsBackdataRowDto(
    DateOnly Date,
    string Weekday,
    string MealType,
    string MealTypeName,
    int PlannedCount,
    int? ActualCount,
    bool ActualInput,
    DateTime? ActualRecordedAt,
    bool ActualLate,
    bool MealPlanOutput,
    DateTime? MealPlanOutputAt,
    bool CookingOutput,
    DateTime? CookingOutputAt,
    bool PreservationCompleted,
    bool PreservationCollected,
    bool PreservationDisposed,
    string? PreservationManager,
    string? PreservationTemperature);

public sealed record OperationsAnomaliesDto(
    IReadOnlyList<RecordGapDto> RecordGaps,
    IReadOnlyList<LateInputDto> LateInputs);

public sealed record OperationsStatisticsDto(
    DateOnly StartDate,
    DateOnly EndDate,
    string MealType,
    OperationsSummaryDto Summary,
    IReadOnlyDictionary<string, OperationsBreakdownDto> Breakdown,
    IReadOnlyList<OperationsTrendPointDto> Trend,
    OperationsAnomaliesDto Anomalies,
    PreservationSummaryDto Preservation,
    IReadOnlyList<OperationsBackdataRowDto> Backdata);

// =======================================================================
// 운영 대시보드
// =======================================================================

public sealed record DashboardKpisDto(
    int OperatingDays,
    int UniqueMenuCount,
    MealTypeBreakdownDto? Lunch,
    MealTypeBreakdownDto? Dinner);

public sealed record MenuUsageDto(string MenuName, int Count);

public sealed record RepeatedMenuDto(string MenuName, int PeriodCount, int Previous4Weeks, DateOnly? LastServed);

public sealed record IngredientGroupDto(string Group, int UsageRows, double EstimatedKg);

public sealed record IngredientChangeDto(string Group, double CurrentKg, double PreviousKg, double Rate, string Level);

public sealed record WorkflowCountsDto(int CookingOutput, int PreservationCompleted, int ActualRecorded);

public sealed record DashboardAnomaliesDto(
    IReadOnlyList<MealAnomalyDto> Meal,
    IReadOnlyList<MenuRepeatDto> MenuRepeats,
    IReadOnlyList<IngredientChangeDto> IngredientChanges,
    IReadOnlyList<RecordGapDto> RecordGaps);

public sealed record DashboardDto(
    DateOnly StartDate,
    DateOnly EndDate,
    string MealType,
    DashboardKpisDto Kpis,
    IReadOnlyList<MealTrendPointDto> Trend,
    DashboardAnomaliesDto Anomalies,
    IReadOnlyList<MenuUsageDto> MenuUsage,
    IReadOnlyList<RepeatedMenuDto> RepeatedMenus,
    IReadOnlyList<IngredientGroupDto> IngredientGroups,
    WorkflowCountsDto Workflow);

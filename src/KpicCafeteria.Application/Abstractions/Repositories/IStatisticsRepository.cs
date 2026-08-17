namespace KpicCafeteria.Application.Abstractions.Repositories;

// =======================================================================
// 통계용 읽기 전용 Projection Row
// 불필요한 전체 Entity Graph를 메모리에 올리지 않기 위해 필요한 컬럼만 조회한다.
// =======================================================================

/// <summary>배식 + 실제식수 + 보존식 + 출력 이력 (식수/운영 통계 공용).</summary>
public sealed record MealServiceRow(
    int Id,
    DateOnly ServiceDate,
    string MealType,
    int PlannedCount,
    int? ActualCount,
    DateTime? RecordedAt,
    DateTime? MealPlanOutputAt,
    DateTime? CookingOutputAt,
    bool PreservationCompleted,
    bool PreservationCollected,
    bool PreservationDisposed,
    string? PreservationManager,
    string? PreservationTemperature);

/// <summary>실제 식수 이력 (평소 중앙값 계산용).</summary>
public sealed record ActualHistoryRow(DateOnly ServiceDate, string MealType, int ActualCount);

/// <summary>메뉴 사용 행 (스냅샷 기준).</summary>
public sealed record MenuUsageRow(
    int ServiceId,
    DateOnly Date,
    string MealType,
    string MealTypeName,
    int PlannedCount,
    int? ActualCount,
    int? MenuId,
    string MenuName,
    string Role);

/// <summary>활성 메뉴 기준정보.</summary>
public sealed record ActiveMenuRow(int Id, string Name);

/// <summary>메뉴 단건 정보 (상세 화면용).</summary>
public sealed record MenuInfoRow(int Id, string Name, string Role);

/// <summary>식재료 사용 행 (스냅샷 기준).</summary>
public sealed record IngredientUsageRow(
    int ServiceId,
    int ServiceMenuId,
    DateOnly Date,
    string MealType,
    string MealTypeName,
    int PlannedCount,
    int? ActualCount,
    int? IngredientId,
    string IngredientName,
    double? QuantityTotal,
    double? QuantityPer100,
    string? Unit,
    string StatGroup,
    string MenuName);

/// <summary>활성 식재료 기준정보.</summary>
public sealed record ActiveIngredientRow(int Id, string Name, string StatGroup);

/// <summary>식재료 단건 정보 (상세 화면용).</summary>
public sealed record IngredientInfoRow(int Id, string Name, string StatGroup);

/// <summary>함께 사용된 메뉴 집계.</summary>
public sealed record CoUsedMenuRow(int? MenuId, string MenuName, int Count);

/// <summary>함께 사용된 식재료 집계.</summary>
public sealed record CoUsedIngredientRow(int? IngredientId, string IngredientName, int Count);

/// <summary>메뉴명 스냅샷 사용 횟수 (레거시 대시보드 menu_usage).</summary>
public sealed record MenuNameUsageRow(string MenuName, int Count);

/// <summary>메뉴명 스냅샷 반복 이력 (기간 + 직전 4주).</summary>
public sealed record RepeatedMenuRow(string MenuName, int PeriodCount, int Previous4Weeks, DateOnly? LastServed);

/// <summary>재료군 집계 (레거시 대시보드 ingredient_groups).</summary>
public sealed record IngredientGroupRow(string Group, int UsageRows, double EstimatedKg);

/// <summary>업무 기록 완료 건수 (레거시 대시보드 workflow).</summary>
public sealed record WorkflowCountsRow(int CookingOutput, int PreservationCompleted, int ActualRecorded);

/// <summary>
/// 통계용 읽기 전용 리포지토리.
/// 작업 단위별로 새 DbContext를 주입받아 사용한다 (장기 보유 금지).
/// 모든 조회는 AsNoTracking + Projection으로 수행한다.
/// </summary>
public interface IStatisticsRepository : IDisposable
{
    // ---- 식수 / 운영 통계 ----

    /// <summary>기간 내 배식 목록. mealType은 "LUNCH"/"DINNER" 또는 null(전체).</summary>
    Task<List<MealServiceRow>> GetMealServicesAsync(
        DateOnly start, DateOnly end, string? mealType, CancellationToken cancellationToken = default);

    /// <summary>기간 내 실제 식수 이력 (actual_count NOT NULL).</summary>
    Task<List<ActualHistoryRow>> GetActualHistoryAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    // ---- 메뉴 통계 ----

    /// <summary>기간 내 메뉴 사용 행. mealType은 "LUNCH"/"DINNER" 또는 null(전체).</summary>
    Task<List<MenuUsageRow>> GetMenuUsageRowsAsync(
        DateOnly start, DateOnly end, string? mealType, int? menuId, CancellationToken cancellationToken = default);

    /// <summary>활성 메뉴 목록.</summary>
    Task<List<ActiveMenuRow>> GetActiveMenusAsync(CancellationToken cancellationToken = default);

    /// <summary>메뉴 단건 조회 (활성 여부 무관, 상세 화면용).</summary>
    Task<MenuInfoRow?> GetMenuByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>기간 [cutoff, end] 내 사용된 메뉴 ID 집합.</summary>
    Task<HashSet<int>> GetMenuIdsUsedInWindowAsync(
        DateOnly cutoff, DateOnly end, CancellationToken cancellationToken = default);

    /// <summary>cutoff 이전에 사용된 메뉴 ID 집합 (신규 메뉴 판정).</summary>
    Task<HashSet<int>> GetMenuIdsUsedBeforeAsync(
        IReadOnlyList<int> menuIds, DateOnly before, CancellationToken cancellationToken = default);

    /// <summary>cutoff 이전 마지막 사용일 (미사용 메뉴 판정).</summary>
    Task<Dictionary<int, DateOnly>> GetMenuLastUsedBeforeAsync(
        IReadOnlyList<int> menuIds, DateOnly before, CancellationToken cancellationToken = default);

    /// <summary>같은 배식에 함께 사용된 메뉴 집계 (menuId 제외).</summary>
    Task<List<CoUsedMenuRow>> GetCoUsedMenusAsync(
        IReadOnlyList<int> serviceIds, int menuId, CancellationToken cancellationToken = default);

    /// <summary>메뉴의 end 이하 전체 사용일 (이전 사용일 계산용).</summary>
    Task<List<DateOnly>> GetMenuUsageDatesAsync(
        int menuId, DateOnly end, CancellationToken cancellationToken = default);

    // ---- 식재료 통계 ----

    /// <summary>기간 내 식재료 사용 행. mealType은 "LUNCH"/"DINNER" 또는 null(전체).</summary>
    Task<List<IngredientUsageRow>> GetIngredientUsageRowsAsync(
        DateOnly start, DateOnly end, string? mealType, int? ingredientId, CancellationToken cancellationToken = default);

    /// <summary>활성 식재료 목록.</summary>
    Task<List<ActiveIngredientRow>> GetActiveIngredientsAsync(CancellationToken cancellationToken = default);

    /// <summary>식재료 단건 조회 (활성 여부 무관, 상세 화면용).</summary>
    Task<IngredientInfoRow?> GetIngredientByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>기간 [cutoff, end] 내 사용된 식재료 ID 집합.</summary>
    Task<HashSet<int>> GetIngredientIdsUsedInWindowAsync(
        DateOnly cutoff, DateOnly end, CancellationToken cancellationToken = default);

    /// <summary>cutoff 이전에 사용된 식재료 ID 집합 (신규 식재료 판정).</summary>
    Task<HashSet<int>> GetIngredientIdsUsedBeforeAsync(
        IReadOnlyList<int> ingredientIds, DateOnly before, CancellationToken cancellationToken = default);

    /// <summary>cutoff 이전 마지막 사용일 (미사용 식재료 판정).</summary>
    Task<Dictionary<int, DateOnly>> GetIngredientLastUsedBeforeAsync(
        IReadOnlyList<int> ingredientIds, DateOnly before, CancellationToken cancellationToken = default);

    /// <summary>같은 메뉴에 함께 사용된 식재료 집계 (ingredientId 제외).</summary>
    Task<List<CoUsedIngredientRow>> GetCoUsedIngredientsAsync(
        IReadOnlyList<int> serviceMenuIds, int ingredientId, CancellationToken cancellationToken = default);

    /// <summary>식재료의 end 이하 전체 사용일 (이전 사용일 계산용).</summary>
    Task<List<DateOnly>> GetIngredientUsageDatesAsync(
        int ingredientId, DateOnly end, CancellationToken cancellationToken = default);

    // ---- 대시보드 (레거시 _aggregate 대응) ----

    /// <summary>메뉴명 스냅샷 사용 횟수 (menu_usage).</summary>
    Task<List<MenuNameUsageRow>> GetMenuNameUsageAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    /// <summary>기간 내 사용 메뉴명 + 직전 4주 사용 이력 (repeated_menus).</summary>
    Task<List<RepeatedMenuRow>> GetRepeatedMenusAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    /// <summary>재료군별 사용 행수/추정 중량 (ingredient_groups, analysis_excluded 제외).</summary>
    Task<List<IngredientGroupRow>> GetIngredientGroupsAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default);

    /// <summary>업무 기록 완료 건수 (workflow).</summary>
    Task<WorkflowCountsRow> GetWorkflowCountsAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default);
}

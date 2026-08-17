using KpicCafeteria.Application.Abstractions.Repositories;

namespace KpicCafeteria.Application.Statistics;

/// <summary>
/// 운영 대시보드.
/// 기존 dashboard_service.py operations_dashboard에 대응.
/// 상세 통계 서비스 결과를 조합하며 별도 계산식을 중복 구현하지 않는다.
/// </summary>
public sealed class DashboardService
{
    private readonly IStatisticsRepositoryFactory _factory;
    private readonly MealStatisticsService _mealStatistics;
    private readonly MenuStatisticsService _menuStatistics;
    private readonly OperationsStatisticsService _operationsStatistics;

    public DashboardService(
        IStatisticsRepositoryFactory factory,
        MealStatisticsService mealStatistics,
        MenuStatisticsService menuStatistics,
        OperationsStatisticsService operationsStatistics)
    {
        _factory = factory;
        _mealStatistics = mealStatistics;
        _menuStatistics = menuStatistics;
        _operationsStatistics = operationsStatistics;
    }

    /// <summary>기간 내 운영 대시보드 (KPI/추세/이상징후/요약).</summary>
    public async Task<DashboardDto> GetAsync(
        DateOnly start, DateOnly end, string mealType = "all", CancellationToken cancellationToken = default)
    {
        var meals = await _mealStatistics.GetAsync(start, end, mealType, cancellationToken);
        var trendStart = MonthsBack(end, 11);
        var trend = await _mealStatistics.GetTrendAsync(trendStart, end, mealType, cancellationToken);
        var menuStats = await _menuStatistics.GetAsync(start, end, mealType, unusedDays: 90, cancellationToken);
        var operations = await _operationsStatistics.GetAsync(start, end, mealType, cancellationToken);

        var (previousStart, previousEnd) = PreviousPeriod(start, end);
        using var repository = _factory.Create();
        var menuUsage = await repository.GetMenuNameUsageAsync(start, end, cancellationToken);
        var repeatedMenus = await repository.GetRepeatedMenusAsync(start, end, cancellationToken);
        var ingredientGroups = await repository.GetIngredientGroupsAsync(start, end, cancellationToken);
        var previousIngredientGroups = await repository.GetIngredientGroupsAsync(previousStart, previousEnd, cancellationToken);
        var workflow = await repository.GetWorkflowCountsAsync(start, end, cancellationToken);

        return new DashboardDto(
            start,
            end,
            mealType,
            new DashboardKpisDto(
                meals.Summary.ServiceCount,
                menuUsage.Count,
                meals.Breakdown.GetValueOrDefault("lunch"),
                meals.Breakdown.GetValueOrDefault("dinner")),
            trend.Trend,
            new DashboardAnomaliesDto(
                meals.Anomalies,
                menuStats.Repeats.Where(r => r.MenuId is not null).Take(5).ToList(),
                IngredientChanges(ingredientGroups, previousIngredientGroups),
                operations.Anomalies.RecordGaps.Where(g => g.Type != "식단표 미출력").Take(10).ToList()),
            menuUsage.Take(5).Select(m => new MenuUsageDto(m.MenuName, m.Count)).ToList(),
            repeatedMenus.Take(5).Select(m => new RepeatedMenuDto(m.MenuName, m.PeriodCount, m.Previous4Weeks, m.LastServed)).ToList(),
            ingredientGroups.Take(6).Select(g => new IngredientGroupDto(g.Group, g.UsageRows, g.EstimatedKg)).ToList(),
            new WorkflowCountsDto(workflow.CookingOutput, workflow.PreservationCompleted, workflow.ActualRecorded));
    }

    // =======================================================================
    // 계산 규칙 (Python 구현과 동일)
    // =======================================================================

    /// <summary>end 기준 months개월 전 1일 (대시보드 추세 시작일).</summary>
    public static DateOnly MonthsBack(DateOnly value, int months)
    {
        var total = value.Year * 12 + (value.Month - 1) - months;
        return new DateOnly(total / 12, total % 12 + 1, 1);
    }

    /// <summary>같은 길이의 직전 기간.</summary>
    public static (DateOnly Start, DateOnly End) PreviousPeriod(DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        var previousEnd = start.AddDays(-1);
        return (previousEnd.AddDays(-(days - 1)), previousEnd);
    }

    /// <summary>
    /// 재료군 사용량 변화: 전기 대비 ±25% 이상이면 확인, ±40% 이상이면 중요.
    /// </summary>
    public static List<IngredientChangeDto> IngredientChanges(
        IReadOnlyList<IngredientGroupRow> current, IReadOnlyList<IngredientGroupRow> previous)
    {
        var currentMap = current.ToDictionary(g => g.Group, g => g.EstimatedKg);
        var previousMap = previous.ToDictionary(g => g.Group, g => g.EstimatedKg);

        var changes = new List<IngredientChangeDto>();
        foreach (var (group, kg) in currentMap)
        {
            if (!previousMap.TryGetValue(group, out var previousKg) || previousKg <= 0 || kg <= 0)
            {
                continue;
            }

            var rate = (kg - previousKg) / previousKg * 100;
            if (Math.Abs(rate) >= 25)
            {
                changes.Add(new IngredientChangeDto(
                    group,
                    Math.Round(kg, 1),
                    Math.Round(previousKg, 1),
                    Math.Round(rate, 1),
                    Math.Abs(rate) >= 40 ? "중요" : "확인"));
            }
        }

        return changes.OrderByDescending(c => Math.Abs(c.Rate)).ToList();
    }
}

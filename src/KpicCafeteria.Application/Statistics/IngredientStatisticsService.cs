using KpicCafeteria.Application.Abstractions.Repositories;

namespace KpicCafeteria.Application.Statistics;

/// <summary>
/// 식재료 통계.
/// 기존 ingredient_statistics.py에 대응.
/// 사용량은 실제 식단 Snapshot(MealServiceMenuIngredient)을 우선한다.
/// QuantityTotal 없으면 QuantityPer100 × PlannedCount / 100 fallback.
/// </summary>
public sealed class IngredientStatisticsService
{
    private readonly IStatisticsRepositoryFactory _factory;

    public IngredientStatisticsService(IStatisticsRepositoryFactory factory)
    {
        _factory = factory;
    }

    /// <summary>기간 내 식재료 통계 (사용/미사용/신규/백데이터).</summary>
    public async Task<IngredientStatisticsDto> GetAsync(
        DateOnly start, DateOnly end, string mealType = "all", int unusedDays = 90,
        CancellationToken cancellationToken = default)
    {
        using var repository = _factory.Create();
        var rows = await repository.GetIngredientUsageRowsAsync(start, end, MealStatisticsService.MealTypeCode(mealType), null, cancellationToken);

        var groups = new Dictionary<string, IngredientGroupBuilder>();
        foreach (var row in rows)
        {
            var key = IngredientKey(row);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new IngredientGroupBuilder(row);
                groups[key] = group;
            }

            group.Add(row);
        }

        var usedIds = groups.Values.Where(g => g.IngredientId is not null).Select(g => g.IngredientId!.Value).ToHashSet();
        var previousIds = new HashSet<int>();
        if (usedIds.Count > 0)
        {
            previousIds = await repository.GetIngredientIdsUsedBeforeAsync(usedIds.ToList(), start, cancellationToken);
        }

        var top = new List<IngredientGroupBuilder>();
        var newCount = 0;
        foreach (var group in groups.Values)
        {
            var dates = group.Dates.Distinct().OrderBy(d => d).ToList();
            group.UsageCount = group.Rows.Count;
            group.FirstUsed = dates[0];
            group.LastUsed = dates[^1];
            var gaps = Enumerable.Range(1, dates.Count - 1).Select(i => dates[i].DayNumber - dates[i - 1].DayNumber).ToList();
            group.AvgInterval = gaps.Count > 0 ? Math.Round((double)gaps.Sum() / gaps.Count, 1) : (double?)null;

            if (group.IngredientId is not null && !previousIds.Contains(group.IngredientId.Value))
            {
                newCount++;
            }

            top.Add(group);
        }

        top.Sort((a, b) => b.UsageCount.CompareTo(a.UsageCount));

        var backdata = BuildBackdata(groups.Values);
        var unused = await GetUnusedIngredientsAsync(repository, end, unusedDays, cancellationToken);

        return new IngredientStatisticsDto(
            start,
            end,
            mealType,
            unusedDays,
            new IngredientSummaryDto(
                groups.Count,
                rows.Count,
                newCount,
                unused.Count),
            top.Take(15).Select(g => new IngredientTopDto(
                g.IngredientId, g.IngredientName, g.StatGroup, g.UsageCount,
                Math.Round(g.Quantity, 1), g.Lunch, g.Dinner, g.FirstUsed, g.LastUsed, g.AvgInterval)).ToList(),
            unused,
            backdata);
    }

    /// <summary>식재료 상세 (월별 사용/최근 이력/함께 사용된 식재료/백데이터). 없으면 null.</summary>
    public async Task<IngredientDetailDto?> GetDetailAsync(
        int ingredientId, DateOnly start, DateOnly end, string mealType = "all",
        CancellationToken cancellationToken = default)
    {
        using var repository = _factory.Create();
        var rows = await repository.GetIngredientUsageRowsAsync(start, end, MealStatisticsService.MealTypeCode(mealType), ingredientId, cancellationToken);
        var ingredientInfo = await repository.GetIngredientByIdAsync(ingredientId, cancellationToken);

        if (rows.Count == 0)
        {
            if (ingredientInfo is null)
            {
                return null;
            }

            return new IngredientDetailDto(
                ingredientId,
                ingredientInfo.Name,
                ingredientInfo.StatGroup,
                new IngredientDetailSummaryDto(0, 0, 0, 0, null, null, null),
                [],
                [],
                [],
                []);
        }

        rows = rows.OrderBy(r => r.Date).ThenBy(r => r.MealType).ToList();
        var dates = rows.Select(r => r.Date).Distinct().OrderBy(d => d).ToList();
        var lunch = rows.Count(r => r.MealType == "LUNCH");
        var dinner = rows.Count - lunch;
        var totalQuantity = rows.Sum(r => ResolveQuantity(r) ?? 0);
        var gaps = Enumerable.Range(1, dates.Count - 1).Select(i => dates[i].DayNumber - dates[i - 1].DayNumber).ToList();
        var avgInterval = gaps.Count > 0 ? Math.Round((double)gaps.Sum() / gaps.Count, 1) : (double?)null;

        var monthly = rows
            .GroupBy(r => r.Date.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyUsageDto(g.Key, g.Count()))
            .ToList();

        var serviceMenuIds = rows.Select(r => r.ServiceMenuId).Distinct().ToList();
        var coUsed = await repository.GetCoUsedIngredientsAsync(serviceMenuIds, ingredientId, cancellationToken);

        var fullDates = await repository.GetIngredientUsageDatesAsync(ingredientId, end, cancellationToken);
        var backdata = BuildDetailBackdata(rows, fullDates);

        return new IngredientDetailDto(
            ingredientId,
            rows[0].IngredientName,
            rows[0].StatGroup,
            new IngredientDetailSummaryDto(rows.Count, lunch, dinner, Math.Round(totalQuantity, 1), dates[0], dates[^1], avgInterval),
            monthly,
            rows.TakeLast(20).Select(r => new IngredientRecentHistoryDto(
                r.Date, r.MealTypeName, r.MenuName, ResolveQuantity(r), r.ActualCount)).ToList(),
            coUsed.Select(c => new CoUsedIngredientDto(c.IngredientId, c.IngredientName, c.Count)).ToList(),
            backdata);
    }

    // =======================================================================
    // 계산 규칙 (Python 구현과 동일)
    // =======================================================================

    /// <summary>식재료 집계 키: IngredientId 존재 시 "id", 없으면 "name:{snapshot}".</summary>
    public static string IngredientKey(IngredientUsageRow row)
        => row.IngredientId is not null ? row.IngredientId.Value.ToString() : $"name:{row.IngredientName}";

    /// <summary>
    /// 사용량 결정: QuantityTotal 존재 시 사용, 없으면 QuantityPer100 × PlannedCount / 100.
    /// </summary>
    public static double? ResolveQuantity(IngredientUsageRow row)
    {
        if (row.QuantityTotal is not null)
        {
            return row.QuantityTotal.Value;
        }

        if (row.QuantityPer100 is not null && row.PlannedCount > 0)
        {
            return row.QuantityPer100.Value * row.PlannedCount / 100;
        }

        return null;
    }

    private static async Task<List<UnusedIngredientDto>> GetUnusedIngredientsAsync(
        IStatisticsRepository repository, DateOnly end, int unusedDays, CancellationToken cancellationToken)
    {
        var cutoff = end.AddDays(-unusedDays);
        var ingredients = await repository.GetActiveIngredientsAsync(cancellationToken);
        var ids = ingredients.Select(i => i.Id).ToList();

        var usedInWindow = new HashSet<int>();
        var lastDates = new Dictionary<int, DateOnly>();
        if (ids.Count > 0)
        {
            usedInWindow = await repository.GetIngredientIdsUsedInWindowAsync(cutoff, end, cancellationToken);
            lastDates = await repository.GetIngredientLastUsedBeforeAsync(ids, cutoff, cancellationToken);
        }

        var result = new List<UnusedIngredientDto>();
        foreach (var ingredient in ingredients)
        {
            if (usedInWindow.Contains(ingredient.Id))
            {
                continue;
            }

            var last = lastDates.GetValueOrDefault(ingredient.Id);
            result.Add(new UnusedIngredientDto(
                ingredient.Id,
                ingredient.Name,
                ingredient.StatGroup,
                last,
                last != default ? end.DayNumber - last.DayNumber : null));
        }

        return result
            .OrderByDescending(i => i.DaysSinceLast ?? int.MaxValue)
            .ToList();
    }

    private static List<IngredientUsageBackdataRowDto> BuildBackdata(IEnumerable<IngredientGroupBuilder> groups)
    {
        var backdata = new List<IngredientUsageBackdataRowDto>();
        foreach (var group in groups)
        {
            var ordered = group.Rows.OrderBy(r => r.Date).ThenBy(r => r.MealType).ToList();
            DateOnly? previous = null;
            foreach (var row in ordered)
            {
                backdata.Add(new IngredientUsageBackdataRowDto(
                    row.Date,
                    StatisticsWeekday.Name(row.Date),
                    row.MealTypeName,
                    row.IngredientName,
                    row.IngredientId,
                    ResolveQuantity(row),
                    row.PlannedCount,
                    row.ActualCount,
                    previous,
                    previous is not null ? row.Date.DayNumber - previous.Value.DayNumber : null));
                previous = row.Date;
            }
        }

        return backdata;
    }

    private static List<IngredientUsageBackdataRowDto> BuildDetailBackdata(
        IReadOnlyList<IngredientUsageRow> rows, IReadOnlyList<DateOnly> fullDates)
    {
        var backdata = new List<IngredientUsageBackdataRowDto>();
        var historyIndex = 0;
        foreach (var row in rows)
        {
            while (historyIndex < fullDates.Count && fullDates[historyIndex] < row.Date)
            {
                historyIndex++;
            }

            var previous = historyIndex > 0 ? fullDates[historyIndex - 1] : (DateOnly?)null;
            backdata.Add(new IngredientUsageBackdataRowDto(
                row.Date,
                StatisticsWeekday.Name(row.Date),
                row.MealTypeName,
                row.IngredientName,
                row.IngredientId,
                ResolveQuantity(row),
                row.PlannedCount,
                row.ActualCount,
                previous,
                previous is not null ? row.Date.DayNumber - previous.Value.DayNumber : null));
        }

        return backdata;
    }

    private sealed class IngredientGroupBuilder
    {
        public IngredientGroupBuilder(IngredientUsageRow row)
        {
            IngredientId = row.IngredientId;
            IngredientName = row.IngredientName;
            StatGroup = row.StatGroup;
        }

        public int? IngredientId { get; }

        public string IngredientName { get; }

        public string StatGroup { get; }

        public List<DateOnly> Dates { get; } = [];

        public int Lunch { get; private set; }

        public int Dinner { get; private set; }

        public double Quantity { get; private set; }

        public List<IngredientUsageRow> Rows { get; } = [];

        public int UsageCount { get; set; }

        public DateOnly? FirstUsed { get; set; }

        public DateOnly? LastUsed { get; set; }

        public double? AvgInterval { get; set; }

        public void Add(IngredientUsageRow row)
        {
            Dates.Add(row.Date);
            if (row.MealType == "LUNCH")
            {
                Lunch++;
            }
            else
            {
                Dinner++;
            }

            var quantity = ResolveQuantity(row);
            if (quantity is not null)
            {
                Quantity += quantity.Value;
            }

            Rows.Add(row);
        }
    }
}

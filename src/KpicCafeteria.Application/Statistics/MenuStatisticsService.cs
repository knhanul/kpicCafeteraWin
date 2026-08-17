using KpicCafeteria.Application.Abstractions.Repositories;

namespace KpicCafeteria.Application.Statistics;

/// <summary>
/// 메뉴 통계.
/// 기존 menu_statistics.py에 대응.
/// 메뉴 집계 기준: MenuId 존재 시 MenuId, 없으면 MenuNameSnapshot (Python _menu_key와 동일).
/// </summary>
public sealed class MenuStatisticsService
{
    private readonly IStatisticsRepositoryFactory _factory;

    public MenuStatisticsService(IStatisticsRepositoryFactory factory)
    {
        _factory = factory;
    }

    /// <summary>기간 내 메뉴 통계 (사용/반복/미사용/신규/백데이터).</summary>
    public async Task<MenuStatisticsDto> GetAsync(
        DateOnly start, DateOnly end, string mealType = "all", int unusedDays = 90,
        CancellationToken cancellationToken = default)
    {
        using var repository = _factory.Create();
        var rows = await repository.GetMenuUsageRowsAsync(start, end, MealStatisticsService.MealTypeCode(mealType), null, cancellationToken);

        var groups = new Dictionary<string, MenuGroupBuilder>();
        foreach (var row in rows)
        {
            var key = MenuKey(row);
            if (!groups.TryGetValue(key, out var group))
            {
                group = new MenuGroupBuilder(row);
                groups[key] = group;
            }

            group.Add(row);
        }

        var usedIds = groups.Values.Where(g => g.MenuId is not null).Select(g => g.MenuId!.Value).ToHashSet();
        var previousIds = new HashSet<int>();
        if (usedIds.Count > 0)
        {
            previousIds = await repository.GetMenuIdsUsedBeforeAsync(usedIds.ToList(), start, cancellationToken);
        }

        var top = new List<MenuGroupBuilder>();
        var repeats = new List<MenuRepeatDto>();
        var newCount = 0;
        foreach (var group in groups.Values)
        {
            var dates = group.Dates.Distinct().OrderBy(d => d).ToList();
            group.UsageCount = group.Rows.Count;
            group.FirstUsed = dates[0];
            group.LastUsed = dates[^1];
            var gaps = Enumerable.Range(1, dates.Count - 1).Select(i => (dates[i].DayNumber - dates[i - 1].DayNumber)).ToList();
            group.AvgInterval = gaps.Count > 0 ? Math.Round((double)gaps.Sum() / gaps.Count, 1) : (double?)null;

            if (group.MenuId is not null && !previousIds.Contains(group.MenuId.Value))
            {
                newCount++;
            }

            var shortRepeat = MaxInWindow(dates, 14);
            var longRepeat = MaxInWindow(dates, 28);
            if (shortRepeat >= 2)
            {
                repeats.Add(new MenuRepeatDto(group.MenuId, group.MenuName, "단기 반복", shortRepeat, 14));
            }

            if (longRepeat >= 3)
            {
                repeats.Add(new MenuRepeatDto(group.MenuId, group.MenuName, "과다 반복", longRepeat, 28));
            }

            top.Add(group);
        }

        top.Sort((a, b) => b.UsageCount.CompareTo(a.UsageCount));
        repeats.Sort((a, b) => b.Count.CompareTo(a.Count));

        var backdata = BuildBackdata(groups.Values);
        var unused = await GetUnusedMenusAsync(repository, end, unusedDays, cancellationToken);

        return new MenuStatisticsDto(
            start,
            end,
            mealType,
            unusedDays,
            new MenuSummaryDto(
                groups.Count,
                rows.Count,
                newCount,
                repeats.Select(r => r.MenuName).Distinct().Count(),
                unused.Count),
            top.Take(15).Select(g => new MenuTopDto(
                g.MenuId, g.MenuName, g.Role, g.UsageCount, g.Lunch, g.Dinner, g.FirstUsed, g.LastUsed, g.AvgInterval)).ToList(),
            repeats.Take(10).ToList(),
            unused,
            backdata);
    }

    /// <summary>메뉴 상세 (월별 사용/최근 이력/함께 사용된 메뉴/백데이터). 없으면 null.</summary>
    public async Task<MenuDetailDto?> GetDetailAsync(
        int menuId, DateOnly start, DateOnly end, string mealType = "all",
        CancellationToken cancellationToken = default)
    {
        using var repository = _factory.Create();
        var rows = await repository.GetMenuUsageRowsAsync(start, end, MealStatisticsService.MealTypeCode(mealType), menuId, cancellationToken);
        var menuInfo = await repository.GetMenuByIdAsync(menuId, cancellationToken);

        if (rows.Count == 0)
        {
            if (menuInfo is null)
            {
                return null;
            }

            return new MenuDetailDto(
                menuId,
                menuInfo.Name,
                menuInfo.Role,
                new MenuDetailSummaryDto(0, 0, 0, null, null, null),
                [],
                [],
                [],
                []);
        }

        rows = rows.OrderBy(r => r.Date).ThenBy(r => r.MealType).ToList();
        var dates = rows.Select(r => r.Date).Distinct().OrderBy(d => d).ToList();
        var lunch = rows.Count(r => r.MealType == "LUNCH");
        var dinner = rows.Count - lunch;
        var gaps = Enumerable.Range(1, dates.Count - 1).Select(i => dates[i].DayNumber - dates[i - 1].DayNumber).ToList();
        var avgInterval = gaps.Count > 0 ? Math.Round((double)gaps.Sum() / gaps.Count, 1) : (double?)null;

        var monthly = rows
            .GroupBy(r => r.Date.ToString("yyyy-MM"))
            .OrderBy(g => g.Key)
            .Select(g => new MonthlyUsageDto(g.Key, g.Count()))
            .ToList();

        var serviceIds = rows.Select(r => r.ServiceId).Distinct().ToList();
        var coUsed = await repository.GetCoUsedMenusAsync(serviceIds, menuId, cancellationToken);

        var fullDates = await repository.GetMenuUsageDatesAsync(menuId, end, cancellationToken);
        var backdata = BuildDetailBackdata(rows, fullDates);

        return new MenuDetailDto(
            menuId,
            rows[0].MenuName,
            rows[0].Role,
            new MenuDetailSummaryDto(rows.Count, lunch, dinner, dates[0], dates[^1], avgInterval),
            monthly,
            rows.TakeLast(20).Select(r => new MenuRecentHistoryDto(r.Date, r.MealTypeName, r.PlannedCount, r.ActualCount)).ToList(),
            coUsed.Select(c => new CoUsedMenuDto(c.MenuId, c.MenuName, c.Count)).ToList(),
            backdata);
    }

    // =======================================================================
    // 계산 규칙 (Python 구현과 동일)
    // =======================================================================

    /// <summary>메뉴 집계 키: MenuId 존재 시 "id", 없으면 "name:{snapshot}".</summary>
    public static string MenuKey(MenuUsageRow row)
        => row.MenuId is not null ? row.MenuId.Value.ToString() : $"name:{row.MenuName}";

    /// <summary>windowDays 이내 최대 사용 횟수 (경계 날짜 포함).</summary>
    public static int MaxInWindow(IReadOnlyList<DateOnly> dates, int windowDays)
    {
        var sorted = dates.Distinct().OrderBy(d => d).ToList();
        var best = 0;
        for (var i = 0; i < sorted.Count; i++)
        {
            var j = i;
            while (j < sorted.Count && sorted[j].DayNumber - sorted[i].DayNumber <= windowDays)
            {
                j++;
            }

            best = Math.Max(best, j - i);
        }

        return best;
    }

    private static async Task<List<UnusedMenuDto>> GetUnusedMenusAsync(
        IStatisticsRepository repository, DateOnly end, int unusedDays, CancellationToken cancellationToken)
    {
        var cutoff = end.AddDays(-unusedDays);
        var menus = await repository.GetActiveMenusAsync(cancellationToken);
        var menuIds = menus.Select(m => m.Id).ToList();

        var usedInWindow = new HashSet<int>();
        var lastDates = new Dictionary<int, DateOnly>();
        if (menuIds.Count > 0)
        {
            usedInWindow = await repository.GetMenuIdsUsedInWindowAsync(cutoff, end, cancellationToken);
            lastDates = await repository.GetMenuLastUsedBeforeAsync(menuIds, cutoff, cancellationToken);
        }

        var result = new List<UnusedMenuDto>();
        foreach (var menu in menus)
        {
            if (usedInWindow.Contains(menu.Id))
            {
                continue;
            }

            var last = lastDates.GetValueOrDefault(menu.Id);
            result.Add(new UnusedMenuDto(
                menu.Id,
                menu.Name,
                last,
                last != default ? end.DayNumber - last.DayNumber : null));
        }

        // 미사용 기간이 긴 순 (사용 이력 없음은 최대값으로 취급)
        return result
            .OrderByDescending(m => m.DaysSinceLast ?? int.MaxValue)
            .ToList();
    }

    private static List<MenuUsageBackdataRowDto> BuildBackdata(IEnumerable<MenuGroupBuilder> groups)
    {
        var backdata = new List<MenuUsageBackdataRowDto>();
        foreach (var group in groups)
        {
            var ordered = group.Rows.OrderBy(r => r.Date).ThenBy(r => r.MealType).ToList();
            DateOnly? previous = null;
            foreach (var row in ordered)
            {
                backdata.Add(new MenuUsageBackdataRowDto(
                    row.Date,
                    StatisticsWeekday.Name(row.Date),
                    row.MealTypeName,
                    row.Role,
                    row.MenuName,
                    row.MenuId,
                    row.PlannedCount,
                    row.ActualCount,
                    previous,
                    previous is not null ? row.Date.DayNumber - previous.Value.DayNumber : null));
                previous = row.Date;
            }
        }

        return backdata;
    }

    private static List<MenuUsageBackdataRowDto> BuildDetailBackdata(
        IReadOnlyList<MenuUsageRow> rows, IReadOnlyList<DateOnly> fullDates)
    {
        var backdata = new List<MenuUsageBackdataRowDto>();
        var historyIndex = 0;
        foreach (var row in rows)
        {
            while (historyIndex < fullDates.Count && fullDates[historyIndex] < row.Date)
            {
                historyIndex++;
            }

            var previous = historyIndex > 0 ? fullDates[historyIndex - 1] : (DateOnly?)null;
            backdata.Add(new MenuUsageBackdataRowDto(
                row.Date,
                StatisticsWeekday.Name(row.Date),
                row.MealTypeName,
                row.Role,
                row.MenuName,
                row.MenuId,
                row.PlannedCount,
                row.ActualCount,
                previous,
                previous is not null ? row.Date.DayNumber - previous.Value.DayNumber : null));
        }

        return backdata;
    }

    private sealed class MenuGroupBuilder
    {
        public MenuGroupBuilder(MenuUsageRow row)
        {
            MenuId = row.MenuId;
            MenuName = row.MenuName;
            Role = row.Role;
        }

        public int? MenuId { get; }

        public string MenuName { get; }

        public string Role { get; }

        public List<DateOnly> Dates { get; } = [];

        public int Lunch { get; private set; }

        public int Dinner { get; private set; }

        public List<MenuUsageRow> Rows { get; } = [];

        public int UsageCount { get; set; }

        public DateOnly? FirstUsed { get; set; }

        public DateOnly? LastUsed { get; set; }

        public double? AvgInterval { get; set; }

        public void Add(MenuUsageRow row)
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

            Rows.Add(row);
        }
    }
}

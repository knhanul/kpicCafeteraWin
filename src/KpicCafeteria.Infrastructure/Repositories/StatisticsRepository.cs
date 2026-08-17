using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.Repositories;

/// <summary>
/// 통계용 읽기 전용 리포지토리 구현.
/// 모든 조회는 AsNoTracking + Projection으로 수행하며,
/// 불필요한 전체 Entity Graph를 메모리에 올리지 않는다.
/// </summary>
public sealed class StatisticsRepository : IStatisticsRepository
{
    private readonly CafeteriaDbContext _db;

    public StatisticsRepository(CafeteriaDbContext db)
    {
        _db = db;
    }

    public Task<List<MealServiceRow>> GetMealServicesAsync(
        DateOnly start, DateOnly end, string? mealType, CancellationToken cancellationToken = default)
    {
        var query = _db.MealServices.AsNoTracking()
            .Where(x => x.ServiceDate >= start && x.ServiceDate <= end);
        if (mealType is not null)
        {
            var type = Enum.Parse<MealType>(mealType);
            query = query.Where(x => x.MealType == type);
        }

        return query
            .OrderBy(x => x.ServiceDate).ThenBy(x => x.MealType)
            .Select(x => new MealServiceRow(
                x.Id,
                x.ServiceDate,
                x.MealType.ToString(),
                x.PlannedCount,
                x.Actual != null ? x.Actual.ActualCount : null,
                x.Actual != null ? x.Actual.RecordedAt : null,
                x.MealPlanOutputAt,
                x.CookingOutputAt,
                x.Preservation != null && x.Preservation.CompletedAt != null,
                x.Preservation != null && x.Preservation.CollectedAt != null,
                x.Preservation != null && x.Preservation.DisposalAt != null,
                x.Preservation != null ? x.Preservation.ManagerName : null,
                x.Preservation != null ? x.Preservation.FreezerTemperature : null))
            .ToListAsync(cancellationToken);
    }

    public Task<List<ActualHistoryRow>> GetActualHistoryAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
        => _db.MealServices.AsNoTracking()
            .Where(x => x.ServiceDate >= start && x.ServiceDate <= end && x.Actual != null && x.Actual.ActualCount != null)
            .Select(x => new ActualHistoryRow(x.ServiceDate, x.MealType.ToString(), x.Actual!.ActualCount!.Value))
            .ToListAsync(cancellationToken);

    public Task<List<MenuUsageRow>> GetMenuUsageRowsAsync(
        DateOnly start, DateOnly end, string? mealType, int? menuId, CancellationToken cancellationToken = default)
    {
        var query = _db.MealServiceMenus.AsNoTracking()
            .Where(x => x.Service!.ServiceDate >= start && x.Service.ServiceDate <= end);
        if (mealType is not null)
        {
            var type = Enum.Parse<MealType>(mealType);
            query = query.Where(x => x.Service!.MealType == type);
        }

        if (menuId is not null)
        {
            query = query.Where(x => x.MenuId == menuId);
        }

        return query
            .OrderBy(x => x.Service!.ServiceDate).ThenBy(x => x.Service!.MealType).ThenBy(x => x.SortOrder)
            .Select(x => new MenuUsageRow(
                x.Service!.Id,
                x.Service.ServiceDate,
                x.Service.MealType.ToString(),
                x.Service.MealType == MealType.LUNCH ? "중식" : "석식",
                x.Service.PlannedCount,
                x.Service.Actual != null ? x.Service.Actual.ActualCount : null,
                x.MenuId,
                x.MenuNameSnapshot,
                x.Menu != null ? x.Menu.Role : "기타"))
            .ToListAsync(cancellationToken);
    }

    public Task<List<ActiveMenuRow>> GetActiveMenusAsync(CancellationToken cancellationToken = default)
        => _db.Menus.AsNoTracking()
            .Where(x => x.Active)
            .Select(x => new ActiveMenuRow(x.Id, x.Name))
            .ToListAsync(cancellationToken);

    public Task<MenuInfoRow?> GetMenuByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.Menus.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new MenuInfoRow(x.Id, x.Name, x.Role))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<HashSet<int>> GetMenuIdsUsedInWindowAsync(
        DateOnly cutoff, DateOnly end, CancellationToken cancellationToken = default)
        => _db.MealServiceMenus.AsNoTracking()
            .Where(x => x.MenuId != null && x.Service!.ServiceDate >= cutoff && x.Service.ServiceDate <= end)
            .Select(x => x.MenuId!.Value)
            .Distinct()
            .ToHashSetAsync(cancellationToken);

    public Task<HashSet<int>> GetMenuIdsUsedBeforeAsync(
        IReadOnlyList<int> menuIds, DateOnly before, CancellationToken cancellationToken = default)
        => _db.MealServiceMenus.AsNoTracking()
            .Where(x => x.MenuId != null && menuIds.Contains(x.MenuId.Value) && x.Service!.ServiceDate < before)
            .Select(x => x.MenuId!.Value)
            .Distinct()
            .ToHashSetAsync(cancellationToken);

    public async Task<Dictionary<int, DateOnly>> GetMenuLastUsedBeforeAsync(
        IReadOnlyList<int> menuIds, DateOnly before, CancellationToken cancellationToken = default)
    {
        var rows = await _db.MealServiceMenus.AsNoTracking()
            .Where(x => x.MenuId != null && menuIds.Contains(x.MenuId.Value) && x.Service!.ServiceDate < before)
            .Select(x => new { x.MenuId, x.Service!.ServiceDate })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => r.MenuId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(r => r.ServiceDate));
    }

    public async Task<List<CoUsedMenuRow>> GetCoUsedMenusAsync(
        IReadOnlyList<int> serviceIds, int menuId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.MealServiceMenus.AsNoTracking()
            .Where(x => serviceIds.Contains(x.MealServiceId) && x.MenuId != menuId)
            .Select(x => new { x.MenuId, x.MenuNameSnapshot })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => r.MenuId is not null ? r.MenuId.Value.ToString() : $"name:{r.MenuNameSnapshot}")
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new CoUsedMenuRow(
                g.First().MenuId,
                g.First().MenuNameSnapshot,
                g.Count()))
            .ToList();
    }

    public Task<List<DateOnly>> GetMenuUsageDatesAsync(
        int menuId, DateOnly end, CancellationToken cancellationToken = default)
        => _db.MealServiceMenus.AsNoTracking()
            .Where(x => x.MenuId == menuId && x.Service!.ServiceDate <= end)
            .Select(x => x.Service!.ServiceDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(cancellationToken);

    public Task<List<IngredientUsageRow>> GetIngredientUsageRowsAsync(
        DateOnly start, DateOnly end, string? mealType, int? ingredientId, CancellationToken cancellationToken = default)
    {
        var query = _db.MealServiceMenuIngredients.AsNoTracking()
            .Where(x => x.ServiceMenu!.Service!.ServiceDate >= start && x.ServiceMenu.Service.ServiceDate <= end);
        if (mealType is not null)
        {
            var type = Enum.Parse<MealType>(mealType);
            query = query.Where(x => x.ServiceMenu!.Service!.MealType == type);
        }

        if (ingredientId is not null)
        {
            query = query.Where(x => x.IngredientId == ingredientId);
        }

        return query
            .OrderBy(x => x.ServiceMenu!.Service!.ServiceDate).ThenBy(x => x.ServiceMenu!.Service!.MealType).ThenBy(x => x.SortOrder)
            .Select(x => new IngredientUsageRow(
                x.ServiceMenu!.Service!.Id,
                x.ServiceMenu.Id,
                x.ServiceMenu.Service.ServiceDate,
                x.ServiceMenu.Service.MealType.ToString(),
                x.ServiceMenu.Service.MealType == MealType.LUNCH ? "중식" : "석식",
                x.ServiceMenu.Service.PlannedCount,
                x.ServiceMenu.Service.Actual != null ? x.ServiceMenu.Service.Actual.ActualCount : null,
                x.IngredientId,
                x.IngredientNameSnapshot,
                x.QuantityTotal,
                x.QuantityPer100,
                x.Unit,
                x.Ingredient != null ? x.Ingredient.StatGroup : "기타",
                x.ServiceMenu.MenuNameSnapshot))
            .ToListAsync(cancellationToken);
    }

    public Task<List<ActiveIngredientRow>> GetActiveIngredientsAsync(CancellationToken cancellationToken = default)
        => _db.Ingredients.AsNoTracking()
            .Where(x => x.Active)
            .Select(x => new ActiveIngredientRow(x.Id, x.Name, x.StatGroup))
            .ToListAsync(cancellationToken);

    public Task<IngredientInfoRow?> GetIngredientByIdAsync(int id, CancellationToken cancellationToken = default)
        => _db.Ingredients.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new IngredientInfoRow(x.Id, x.Name, x.StatGroup))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<HashSet<int>> GetIngredientIdsUsedInWindowAsync(
        DateOnly cutoff, DateOnly end, CancellationToken cancellationToken = default)
        => _db.MealServiceMenuIngredients.AsNoTracking()
            .Where(x => x.IngredientId != null && x.ServiceMenu!.Service!.ServiceDate >= cutoff && x.ServiceMenu.Service.ServiceDate <= end)
            .Select(x => x.IngredientId!.Value)
            .Distinct()
            .ToHashSetAsync(cancellationToken);

    public Task<HashSet<int>> GetIngredientIdsUsedBeforeAsync(
        IReadOnlyList<int> ingredientIds, DateOnly before, CancellationToken cancellationToken = default)
        => _db.MealServiceMenuIngredients.AsNoTracking()
            .Where(x => x.IngredientId != null && ingredientIds.Contains(x.IngredientId.Value) && x.ServiceMenu!.Service!.ServiceDate < before)
            .Select(x => x.IngredientId!.Value)
            .Distinct()
            .ToHashSetAsync(cancellationToken);

    public async Task<Dictionary<int, DateOnly>> GetIngredientLastUsedBeforeAsync(
        IReadOnlyList<int> ingredientIds, DateOnly before, CancellationToken cancellationToken = default)
    {
        var rows = await _db.MealServiceMenuIngredients.AsNoTracking()
            .Where(x => x.IngredientId != null && ingredientIds.Contains(x.IngredientId.Value) && x.ServiceMenu!.Service!.ServiceDate < before)
            .Select(x => new { x.IngredientId, x.ServiceMenu!.Service!.ServiceDate })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => r.IngredientId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(r => r.ServiceDate));
    }

    public async Task<List<CoUsedIngredientRow>> GetCoUsedIngredientsAsync(
        IReadOnlyList<int> serviceMenuIds, int ingredientId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.MealServiceMenuIngredients.AsNoTracking()
            .Where(x => serviceMenuIds.Contains(x.MealServiceMenuId) && x.IngredientId != ingredientId)
            .Select(x => new { x.IngredientId, x.IngredientNameSnapshot })
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(r => r.IngredientId is not null ? r.IngredientId.Value.ToString() : $"name:{r.IngredientNameSnapshot}")
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new CoUsedIngredientRow(
                g.First().IngredientId,
                g.First().IngredientNameSnapshot,
                g.Count()))
            .ToList();
    }

    public Task<List<DateOnly>> GetIngredientUsageDatesAsync(
        int ingredientId, DateOnly end, CancellationToken cancellationToken = default)
        => _db.MealServiceMenuIngredients.AsNoTracking()
            .Where(x => x.IngredientId == ingredientId && x.ServiceMenu!.Service!.ServiceDate <= end)
            .Select(x => x.ServiceMenu!.Service!.ServiceDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(cancellationToken);

    public async Task<List<MenuNameUsageRow>> GetMenuNameUsageAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var rows = await _db.MealServiceMenus.AsNoTracking()
            .Where(x => x.Service!.ServiceDate >= start && x.Service.ServiceDate <= end)
            .Select(x => x.MenuNameSnapshot)
            .ToListAsync(cancellationToken);
        return rows
            .GroupBy(name => name)
            .OrderByDescending(g => g.Count())
            .Select(g => new MenuNameUsageRow(g.Key, g.Count()))
            .ToList();
    }

    public async Task<List<RepeatedMenuRow>> GetRepeatedMenusAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var periodNames = await _db.MealServiceMenus.AsNoTracking()
            .Where(x => x.Service!.ServiceDate >= start && x.Service.ServiceDate <= end)
            .Select(x => x.MenuNameSnapshot)
            .ToListAsync(cancellationToken);
        var periodCounts = periodNames
            .GroupBy(name => name)
            .ToDictionary(g => g.Key, g => g.Count());

        var previousStart = start.AddDays(-28);
        var previousEnd = start.AddDays(-1);
        var history = await _db.MealServiceMenus.AsNoTracking()
            .Where(x => x.Service!.ServiceDate >= previousStart && x.Service.ServiceDate <= previousEnd)
            .OrderByDescending(x => x.Service!.ServiceDate)
            .Select(x => new { x.MenuNameSnapshot, x.Service!.ServiceDate })
            .ToListAsync(cancellationToken);
        var historyCounts = history
            .GroupBy(h => h.MenuNameSnapshot)
            .ToDictionary(g => g.Key, g => g.Count());
        var lastDates = new Dictionary<string, DateOnly>();
        foreach (var item in history)
        {
            lastDates.TryAdd(item.MenuNameSnapshot, item.ServiceDate);
        }

        return periodCounts
            .Where(kv => historyCounts.GetValueOrDefault(kv.Key, 0) > 0 || kv.Value > 1)
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new RepeatedMenuRow(
                kv.Key,
                kv.Value,
                historyCounts.GetValueOrDefault(kv.Key, 0),
                lastDates.GetValueOrDefault(kv.Key)))
            .ToList();
    }

    public async Task<List<IngredientGroupRow>> GetIngredientGroupsAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var rows = await _db.MealServiceMenuIngredients.AsNoTracking()
            .Where(x => x.ServiceMenu!.Service!.ServiceDate >= start && x.ServiceMenu.Service.ServiceDate <= end)
            .Select(x => new
            {
                StatGroup = x.Ingredient != null ? x.Ingredient.StatGroup : null,
                AnalysisExcluded = x.Ingredient != null && x.Ingredient.AnalysisExcluded,
                QuantityPer100 = x.QuantityPer100,
                PlannedCount = x.ServiceMenu!.Service!.PlannedCount,
                Unit = x.Unit,
                KgFactor = x.Ingredient != null ? x.Ingredient.KgFactor : null,
            })
            .ToListAsync(cancellationToken);

        var usage = new Dictionary<string, int>();
        var estimatedKg = new Dictionary<string, double>();
        foreach (var row in rows)
        {
            if (row.StatGroup is null || row.AnalysisExcluded)
            {
                continue;
            }

            var group = row.StatGroup;
            usage[group] = usage.GetValueOrDefault(group) + 1;
            if (row.QuantityPer100 is null)
            {
                continue;
            }

            var total = row.QuantityPer100.Value * row.PlannedCount / 100;
            double kg;
            if (row.Unit == "kg")
            {
                kg = total;
            }
            else if (row.Unit == "g")
            {
                kg = total / 1000;
            }
            else if (row.KgFactor is not null)
            {
                kg = total * row.KgFactor.Value;
            }
            else
            {
                continue;
            }

            estimatedKg[group] = estimatedKg.GetValueOrDefault(group) + kg;
        }

        return usage
            .OrderByDescending(kv => kv.Value)
            .Select(kv => new IngredientGroupRow(kv.Key, kv.Value, Math.Round(estimatedKg.GetValueOrDefault(kv.Key), 2)))
            .ToList();
    }

    public async Task<WorkflowCountsRow> GetWorkflowCountsAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken = default)
    {
        var services = await _db.MealServices.AsNoTracking()
            .Where(x => x.ServiceDate >= start && x.ServiceDate <= end)
            .Select(x => new
            {
                x.CookingOutputAt,
                PreservationCompleted = x.Preservation != null && x.Preservation.CompletedAt != null,
                ActualRecorded = x.Actual != null && x.Actual.ActualCount != null,
            })
            .ToListAsync(cancellationToken);

        return new WorkflowCountsRow(
            services.Count(x => x.CookingOutputAt != null),
            services.Count(x => x.PreservationCompleted),
            services.Count(x => x.ActualRecorded));
    }

    public void Dispose() => _db.Dispose();
}

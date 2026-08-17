using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Domain;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;

namespace KpicCafeteria.Application.Orders;

/// <summary>
/// 발주 관리 업무 서비스.
/// 기존 Python orders.py의 업무규칙을 유지하고, 판매 포장단위 기반 추천 발주량을 추가한다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\orders.py
/// </summary>
public sealed class OrderService
{
    private static readonly HashSet<string> OrderStatuses = ["pending", "ordered", "skipped"];

    private readonly IOrderRepositoryFactory _factory;

    public OrderService(IOrderRepositoryFactory factory)
    {
        _factory = factory;
    }

    private IOrderRepository CreateRepository() => _factory.Create();

    /// <summary>
    /// 재료 식별 키.
    /// IngredientId 있음 → ID, 없음 → name:{IngredientNameSnapshot}.
    /// </summary>
    private static string IngredientKey(int? ingredientId, string name)
        => ingredientId is int id ? id.ToString() : $"name:{name}";

    private static OrderStatus ParseStatus(string status) => status.ToLowerInvariant() switch
    {
        "pending" => OrderStatus.Pending,
        "ordered" => OrderStatus.Ordered,
        "skipped" => OrderStatus.Skipped,
        _ => throw new InvalidOrderStatusException(),
    };

    private static (DateOnly Start, DateOnly End) ResolveRange(DateOnly startDate, DateOnly endDate)
        => startDate > endDate ? (endDate, startDate) : (startDate, endDate);

    // =======================================================================
    // 기간별 발주 조회
    // =======================================================================

    /// <summary>
    /// 기간별 발주 조회.
    /// RequiredQuantity는 항상 최신 식단 Snapshot에서 다시 집계하고,
    /// 사용자가 입력한 OrderQuantity/OrderDate/DeliveryDate/Status/OrderNote는 저장값을 유지한다.
    /// </summary>
    public async Task<OrderListResultDto> GetOrdersAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var (start, end) = ResolveRange(startDate, endDate);

        var services = await repository.GetServicesWithIngredientsInRangeAsync(start, end, cancellationToken);
        var mealTypeNames = (await repository.GetMealTypeSettingsAsync(cancellationToken))
            .ToDictionary(x => x.Code, x => x.Name);
        var storedItems = await repository.GetItemsInRangeAsync(start, end, cancellationToken);

        // 식단 집계: (사용일, 재료키) → 필요량 합계 + 출처 메뉴 목록
        var plan = new Dictionary<(DateOnly, string), PlanRow>();
        foreach (var service in services)
        {
            foreach (var menu in service.Menus)
            {
                foreach (var ing in menu.Ingredients)
                {
                    if (ing.QuantityTotal is null)
                    {
                        continue;
                    }

                    var key = (service.ServiceDate, IngredientKey(ing.IngredientId, ing.IngredientNameSnapshot));
                    if (!plan.TryGetValue(key, out var row))
                    {
                        row = new PlanRow
                        {
                            Name = ing.IngredientNameSnapshot,
                            Unit = ing.Unit,
                            PackageQuantity = ing.Ingredient?.PurchasePackageQuantity,
                            PackageUnit = ing.Ingredient?.PurchasePackageUnit,
                        };
                        plan[key] = row;
                    }

                    row.Required += ing.QuantityTotal.Value;
                    if (row.Unit is null && ing.Unit is not null)
                    {
                        row.Unit = ing.Unit;
                    }

                    row.Menus.Add(new OrderSourceMenuDto(
                        menu.MenuNameSnapshot,
                        ing.QuantityTotal.Value,
                        ing.Unit,
                        service.ServiceDate,
                        service.MealType.ToString(),
                        mealTypeNames.GetValueOrDefault(service.MealType.ToString(), service.MealType.ToString())));
                }
            }
        }

        var storedByKey = storedItems.ToDictionary(
            x => (x.ServiceDate, IngredientKey(x.IngredientId, x.IngredientNameSnapshot)));

        var items = new List<OrderItemDto>();

        // 1. 현재 식단의 항목 (필요량은 항상 식단에서).
        foreach (var ((serviceDate, ingKey), row) in plan
                     .OrderBy(kv => kv.Key.Item2).ThenBy(kv => kv.Key.Item1))
        {
            var ingredientId = int.TryParse(ingKey, out var id) ? id : (int?)null;
            if (storedByKey.TryGetValue((serviceDate, ingKey), out var stored))
            {
                items.Add(MapStoredItem(stored, row.Required, row.Unit, row.Menus, inPlan: true));
            }
            else
            {
                items.Add(MapNewItem(serviceDate, ingredientId, row));
            }
        }

        // 2. 식단에서 사라졌지만 저장된 항목 (사용자 입력 보존, InPlan=false).
        foreach (var ((serviceDate, ingKey), stored) in storedByKey)
        {
            if (!plan.ContainsKey((serviceDate, ingKey)))
            {
                items.Add(MapStoredItem(stored, stored.RequiredQuantity, stored.RequiredUnit, [], inPlan: false));
            }
        }

        items.Sort((a, b) =>
        {
            var byName = string.Compare(a.IngredientName, b.IngredientName, StringComparison.Ordinal);
            return byName != 0 ? byName : a.ServiceDate.CompareTo(b.ServiceDate);
        });

        return new OrderListResultDto(start, end, items);
    }

    // =======================================================================
    // 저장 (upsert)
    // =======================================================================

    /// <summary>발주 항목 저장. 다건은 Transaction으로 처리한다.</summary>
    public async Task SaveItemsAsync(IReadOnlyList<OrderItemSaveInput> items, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        await repository.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var row in items)
            {
                await UpsertItemAsync(repository, row, cancellationToken);
            }

            await repository.SaveChangesAsync(cancellationToken);
            await repository.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    // =======================================================================
    // 묶음 발주
    // =======================================================================

    /// <summary>
    /// 같은 재료의 여러 사용일 항목을 하나의 OrderGroup으로 묶는다.
    /// 그룹 소속 항목은 Status=ordered, OrderDate/DeliveryDate를 그룹 값과 동기화한다.
    /// </summary>
    public async Task<int> CreateOrderGroupAsync(OrderGroupCreateInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        await repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var rows = new List<OrderItem>();
            foreach (var row in input.Items)
            {
                var item = await UpsertItemAsync(repository, row, cancellationToken);
                if (!rows.Contains(item))
                {
                    rows.Add(item);
                }
            }

            if (rows.Count == 0)
            {
                throw new EmptyOrderSelectionException();
            }

            // 동일 재료 검증: IngredientId 있으면 ID, 없으면 재료명 스냅샷 기준.
            var firstKey = IngredientKey(rows[0].IngredientId, rows[0].IngredientNameSnapshot);
            if (rows.Any(r => IngredientKey(r.IngredientId, r.IngredientNameSnapshot) != firstKey))
            {
                throw new MixedIngredientGroupException();
            }

            var totalRequired = rows.Where(r => r.RequiredQuantity is not null).Sum(r => r.RequiredQuantity!.Value);
            var requiredUnit = rows.FirstOrDefault(r => r.RequiredUnit is not null)?.RequiredUnit;

            var group = new OrderGroup
            {
                IngredientId = rows[0].IngredientId,
                IngredientNameSnapshot = rows[0].IngredientNameSnapshot,
                OrderQuantity = input.OrderQuantity,
                OrderUnit = input.OrderUnit,
                OrderDate = input.OrderDate,
                DeliveryDate = input.DeliveryDate,
                TotalRequiredQuantity = totalRequired,
                RequiredUnit = requiredUnit,
            };
            repository.Add(group);
            await repository.SaveChangesAsync(cancellationToken);

            foreach (var row in rows)
            {
                row.OrderGroupId = group.Id;
                row.OrderDate = input.OrderDate;
                row.DeliveryDate = input.DeliveryDate;
                row.Status = OrderStatus.Ordered;
            }

            await repository.SaveChangesAsync(cancellationToken);
            await repository.CommitTransactionAsync(cancellationToken);
            return group.Id;
        }
        catch
        {
            await repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    // =======================================================================
    // 일괄 변경
    // =======================================================================

    /// <summary>선택 항목의 OrderDate/DeliveryDate/Status 중 하나 이상을 일괄 변경한다.</summary>
    public async Task<int> BulkUpdateAsync(OrderBulkUpdateInput input, CancellationToken cancellationToken = default)
    {
        if (input.OrderDate is null && input.DeliveryDate is null && input.Status is null)
        {
            throw new NoChangesToApplyException();
        }

        if (input.Status is not null && !OrderStatuses.Contains(input.Status))
        {
            throw new InvalidOrderStatusException();
        }

        using var repository = CreateRepository();
        await repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var rows = new List<OrderItem>();
            foreach (var row in input.Items)
            {
                var item = await UpsertItemAsync(repository, row, cancellationToken);
                if (input.OrderDate is not null)
                {
                    item.OrderDate = input.OrderDate;
                }

                if (input.DeliveryDate is not null)
                {
                    item.DeliveryDate = input.DeliveryDate;
                }

                if (input.Status is not null)
                {
                    item.Status = ParseStatus(input.Status);
                }

                if (!rows.Contains(item))
                {
                    rows.Add(item);
                }
            }

            await repository.SaveChangesAsync(cancellationToken);
            await repository.CommitTransactionAsync(cancellationToken);
            return rows.Count;
        }
        catch
        {
            await repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }

    // =======================================================================
    // 내부 헬퍼
    // =======================================================================

    private static async Task<OrderItem> UpsertItemAsync(
        IOrderRepository repository, OrderItemSaveInput row, CancellationToken cancellationToken)
    {
        if (!OrderStatuses.Contains(row.Status))
        {
            throw new InvalidOrderStatusException();
        }

        var existing = await repository.FindItemAsync(row.ServiceDate, row.IngredientId, row.IngredientName, cancellationToken);
        if (existing is not null)
        {
            existing.IngredientNameSnapshot = row.IngredientName;
            existing.RequiredQuantity = row.RequiredQuantity;
            existing.RequiredUnit = row.RequiredUnit;
            existing.OrderQuantity = row.OrderQuantity;
            existing.OrderUnit = row.OrderUnit;
            existing.OrderDate = row.OrderDate;
            existing.DeliveryDate = row.DeliveryDate;
            existing.Status = ParseStatus(row.Status);
            existing.OrderNote = row.OrderNote;
            return existing;
        }

        var item = new OrderItem
        {
            ServiceDate = row.ServiceDate,
            IngredientId = row.IngredientId,
            IngredientNameSnapshot = row.IngredientName,
            RequiredQuantity = row.RequiredQuantity,
            RequiredUnit = row.RequiredUnit,
            OrderQuantity = row.OrderQuantity,
            OrderUnit = row.OrderUnit,
            OrderDate = row.OrderDate,
            DeliveryDate = row.DeliveryDate,
            Status = ParseStatus(row.Status),
            OrderNote = row.OrderNote,
        };
        repository.Add(item);
        return item;
    }

    /// <summary>신규 항목 기본값: 발주량 = 추천량(계산 가능 시) 또는 필요량, 발주일 = 사용일-1, 배송일 = 사용일, 상태 = pending.</summary>
    private static OrderItemDto MapNewItem(DateOnly serviceDate, int? ingredientId, PlanRow row)
    {
        var suggested = OrderQuantityCalculator.CalculateSuggested(
            row.Required, row.Unit, row.PackageQuantity, row.PackageUnit);
        var suggestedUnit = OrderQuantityCalculator.SuggestedUnit(row.Unit, row.PackageUnit);
        var packageCompatible = row.PackageQuantity is not null && suggested is not null;

        return new OrderItemDto(
            Id: null,
            serviceDate,
            ingredientId,
            row.Name,
            row.Required,
            row.Unit,
            suggested,
            suggestedUnit,
            packageCompatible,
            row.PackageQuantity,
            row.PackageUnit,
            OrderQuantity: suggested ?? row.Required,
            OrderUnit: suggestedUnit ?? row.Unit,
            serviceDate.AddDays(-1),
            serviceDate,
            "pending",
            InPlan: true,
            OrderNote: null,
            OrderGroupId: null,
            OrderGroupQuantity: null,
            OrderGroupUnit: null,
            row.Menus);
    }

    /// <summary>저장된 항목: 사용자 입력 필드는 저장값 유지, 필요량은 식단 최신값.</summary>
    private static OrderItemDto MapStoredItem(
        OrderItem item, double? required, string? unit, IReadOnlyList<OrderSourceMenuDto> menus, bool inPlan)
    {
        var suggested = OrderQuantityCalculator.CalculateSuggested(
            required, unit, item.Ingredient?.PurchasePackageQuantity, item.Ingredient?.PurchasePackageUnit);
        var suggestedUnit = OrderQuantityCalculator.SuggestedUnit(unit, item.Ingredient?.PurchasePackageUnit);
        var packageCompatible = item.Ingredient?.PurchasePackageQuantity is not null && suggested is not null;
        var group = item.OrderGroup;

        return new OrderItemDto(
            item.Id,
            item.ServiceDate,
            item.IngredientId,
            item.IngredientNameSnapshot,
            required,
            unit,
            suggested,
            suggestedUnit,
            packageCompatible,
            item.Ingredient?.PurchasePackageQuantity,
            item.Ingredient?.PurchasePackageUnit,
            item.OrderQuantity,
            item.OrderUnit,
            item.OrderDate,
            item.DeliveryDate,
            item.Status.ToString().ToLowerInvariant(),
            inPlan,
            item.OrderNote,
            item.OrderGroupId,
            group?.OrderQuantity,
            group?.OrderUnit,
            menus);
    }

    /// <summary>식단 집계 중간 행.</summary>
    private sealed class PlanRow
    {
        public string Name { get; init; } = string.Empty;

        public double Required { get; set; }

        public string? Unit { get; set; }

        public double? PackageQuantity { get; init; }

        public string? PackageUnit { get; init; }

        public List<OrderSourceMenuDto> Menus { get; } = [];
    }
}

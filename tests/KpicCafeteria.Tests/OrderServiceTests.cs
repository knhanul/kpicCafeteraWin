using KpicCafeteria.Application.Orders;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Infrastructure.Persistence;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// 발주 관리 업무 서비스 검증.
/// 기존 Python test_orders.py 시나리오를 이식하고, 판매 포장단위/추천 발주량/사용자 입력 보존을 추가 검증한다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\tests\test_orders.py
/// </summary>
public class OrderServiceTests
{
    // =======================================================================
    // 헬퍼 (test_orders.py의 make_* 대응)
    // =======================================================================

    private static Ingredient CreateIngredient(
        CafeteriaDbContext db, string name = "대파", string unit = "kg",
        double? packageQuantity = null, string? packageUnit = null)
    {
        var ing = new Ingredient
        {
            Name = name,
            StatGroup = "채소",
            DefaultUnit = unit,
            Active = true,
            PurchasePackageQuantity = packageQuantity,
            PurchasePackageUnit = packageUnit,
        };
        db.Ingredients.Add(ing);
        db.SaveChanges();
        return ing;
    }

    private static MealService CreateService(CafeteriaDbContext db, DateOnly serviceDate, int plannedCount = 100)
    {
        var svc = new MealService
        {
            ServiceDate = serviceDate,
            MealType = MealType.LUNCH,
            PlannedCount = plannedCount,
        };
        db.MealServices.Add(svc);
        db.SaveChanges();
        return svc;
    }

    private static Menu CreateMenu(CafeteriaDbContext db, string name = "육개장")
    {
        var menu = new Menu { Name = name, CanonicalName = name, Role = "주찬", Active = true };
        db.Menus.Add(menu);
        db.SaveChanges();
        return menu;
    }

    private static void AddIngredientToService(
        CafeteriaDbContext db,
        MealService service,
        Menu menu,
        Ingredient? ingredient,
        double quantityTotal,
        string unit = "kg",
        string? nameSnapshot = null)
    {
        var sm = new MealServiceMenu
        {
            MealServiceId = service.Id,
            MenuId = menu.Id,
            MenuNameSnapshot = menu.Name,
            SortOrder = 1,
        };
        db.MealServiceMenus.Add(sm);
        db.SaveChanges();

        db.MealServiceMenuIngredients.Add(new MealServiceMenuIngredient
        {
            MealServiceMenuId = sm.Id,
            IngredientId = ingredient?.Id,
            IngredientNameSnapshot = nameSnapshot ?? ingredient!.Name,
            QuantityTotal = quantityTotal,
            QuantityPer100 = quantityTotal * 100 / service.PlannedCount,
            Unit = unit,
        });
        db.SaveChanges();
    }

    private static OrderItemSaveInput Item(
        DateOnly serviceDate, int? ingredientId, string name,
        double? required, double? orderQuantity, string status,
        string? unit = "kg", DateOnly? orderDate = null, DateOnly? deliveryDate = null)
        => new(
            serviceDate,
            ingredientId,
            name,
            required,
            unit,
            orderQuantity,
            unit,
            orderDate,
            deliveryDate,
            status,
            OrderNote: null);

    // =======================================================================
    // 동일 날짜 집계 (필수 테스트 44, test_orders.py test_list_orders_aggregates_same_date_same_ingredient)
    // =======================================================================

    [Fact]
    public async Task ListOrders_AggregatesSameDateSameIngredient()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db);
        var menu1 = CreateMenu(db, "육개장");
        var menu2 = CreateMenu(db, "잡채");
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu1, ing, 8.0);
        AddIngredientToService(db, svc, menu2, ing, 4.0);

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        var item = Assert.Single(result.Items);
        Assert.Equal("대파", item.IngredientName);
        Assert.Equal(12.0, item.RequiredQuantity);
        Assert.Equal("kg", item.RequiredUnit);
        Assert.Equal(12.0, item.OrderQuantity); // 기본 = 필요량
        Assert.Equal("pending", item.Status);
        Assert.True(item.InPlan);
        Assert.Equal(2, item.SourceMenus.Count);
        Assert.Equal(new[] { "육개장", "잡채" }, item.SourceMenus.Select(m => m.MenuName).OrderBy(x => x));
        Assert.All(item.SourceMenus, m => Assert.Equal(new DateOnly(2025, 1, 1), m.ServiceDate));
        Assert.All(item.SourceMenus, m => Assert.Equal("LUNCH", m.MealType));
        Assert.All(item.SourceMenus, m => Assert.Equal("중식", m.MealTypeName));
    }

    // =======================================================================
    // IngredientId 없는 Snapshot (필수 테스트 46, test_orders.py test_list_orders_mixed_id_and_name_keys_do_not_crash)
    // =======================================================================

    [Fact]
    public async Task ListOrders_MixedIdAndNameKeys_DoNotCrash()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db);
        var menu = CreateMenu(db);
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing, 5.0);
        AddIngredientToService(db, svc, menu, null, 3.0, nameSnapshot: "삭제된재료");

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(new[] { "대파", "삭제된재료" }, result.Items.Select(i => i.IngredientName).OrderBy(x => x));
    }

    [Fact]
    public async Task ListOrders_NameKeyedRows_AggregateByNameSnapshot()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var menu1 = CreateMenu(db, "육개장");
        var menu2 = CreateMenu(db, "잡채");
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu1, null, 5.0, nameSnapshot: "삭제된재료");
        AddIngredientToService(db, svc, menu2, null, 3.0, nameSnapshot: "삭제된재료");

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        var item = Assert.Single(result.Items);
        Assert.Equal("삭제된재료", item.IngredientName);
        Assert.Equal(8.0, item.RequiredQuantity);
        Assert.Null(item.IngredientId);
        Assert.Equal(2, item.SourceMenus.Count);
    }

    // =======================================================================
    // 날짜 분리 (필수 테스트 45, test_orders.py test_list_orders_separate_rows_per_date)
    // =======================================================================

    [Fact]
    public async Task ListOrders_SeparateRowsPerDate()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db);
        var menu = CreateMenu(db);
        var svc1 = CreateService(db, new DateOnly(2025, 1, 1));
        var svc2 = CreateService(db, new DateOnly(2025, 1, 3));
        AddIngredientToService(db, svc1, menu, ing, 10.0);
        AddIngredientToService(db, svc2, menu, ing, 7.0);

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(new[] { 7.0, 10.0 }, result.Items.Select(i => i.RequiredQuantity!.Value).OrderBy(x => x));
        var byDate = result.Items.ToDictionary(i => i.ServiceDate);
        Assert.Equal(new DateOnly(2024, 12, 31), byDate[new DateOnly(2025, 1, 1)].OrderDate); // 기본 발주일 = 사용일-1
        Assert.Equal(new DateOnly(2025, 1, 1), byDate[new DateOnly(2025, 1, 1)].DeliveryDate);
    }

    // =======================================================================
    // 사용자 입력 보존 (필수 테스트 47, test_orders.py test_save_order_items_upserts_and_preserves_user_input)
    // =======================================================================

    [Fact]
    public async Task SaveItems_UpsertsAndPreservesUserInput()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db);
        var menu = CreateMenu(db);
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing, 12.0);

        var service = harness.CreateOrderService();
        await service.SaveItemsAsync([
            Item(new DateOnly(2025, 1, 1), ing.Id, "대파", 12.0, 15.0, "ordered",
                orderDate: new DateOnly(2025, 1, 1), deliveryDate: new DateOnly(2025, 1, 2)),
        ]);

        using (var verify = harness.CreateContext())
        {
            var stored = verify.OrderItems.Single();
            Assert.Equal(15.0, stored.OrderQuantity);
            Assert.Equal(OrderStatus.Ordered, stored.Status);
        }

        // 식단 변경: 필요량 15kg으로 증가 → RequiredQuantity만 최신화, 사용자 입력 유지
        AddIngredientToService(db, svc, menu, ing, 3.0);
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));
        var item = Assert.Single(result.Items);
        Assert.Equal(15.0, item.RequiredQuantity);
        Assert.Equal(15.0, item.OrderQuantity);
        Assert.Equal("ordered", item.Status);
        Assert.Equal(new DateOnly(2025, 1, 1), item.OrderDate);
    }

    // =======================================================================
    // 식단 제외 (필수 테스트 48, test_orders.py test_stored_item_outside_plan_is_kept)
    // =======================================================================

    [Fact]
    public async Task StoredItemOutsidePlan_IsKeptWithInPlanFalse()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db);
        var menu = CreateMenu(db);
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing, 12.0);

        var service = harness.CreateOrderService();
        await service.SaveItemsAsync([
            Item(new DateOnly(2025, 1, 1), ing.Id, "대파", 12.0, 15.0, "ordered",
                orderDate: new DateOnly(2025, 1, 1), deliveryDate: new DateOnly(2025, 1, 2)),
        ]);

        // 식단에서 재료 제거
        db.MealServiceMenuIngredients.RemoveRange(db.MealServiceMenuIngredients);
        db.SaveChanges();

        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));
        var item = Assert.Single(result.Items);
        Assert.False(item.InPlan);
        Assert.Equal(15.0, item.OrderQuantity);
        Assert.Equal("ordered", item.Status);
        Assert.Empty(item.SourceMenus);
    }

    // =======================================================================
    // 판매 포장단위 → 신규 항목 기본값 (필수 테스트 49/50, 19)
    // =======================================================================

    [Fact]
    public async Task NewItem_WithPackage_DefaultsToSuggested()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        // 데미글라스소스: 기본단위 g, 판매 2kg
        var ing = CreateIngredient(db, "데미글라스소스", "g", packageQuantity: 2, packageUnit: "kg");
        var menu = CreateMenu(db, "함박스테이크");
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing, 800, "g");

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        var item = Assert.Single(result.Items);
        Assert.Equal(800, item.RequiredQuantity);
        Assert.Equal(2.0, item.SuggestedOrderQuantity); // 800g → 2kg
        Assert.Equal("kg", item.SuggestedUnit);
        Assert.True(item.PackageCompatible);
        Assert.Equal(2.0, item.OrderQuantity); // 신규 기본 = 추천량
        Assert.Equal("kg", item.OrderUnit);
        Assert.Equal("pending", item.Status);
    }

    [Fact]
    public async Task NewItem_WithoutPackage_DefaultsToRequired()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db, "대파", "kg");
        var menu = CreateMenu(db, "육개장");
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing, 5.2);

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        var item = Assert.Single(result.Items);
        Assert.Equal(5.2, item.SuggestedOrderQuantity); // 포장단위 없음 → 필요량
        Assert.Equal(5.2, item.OrderQuantity);
        Assert.Equal("kg", item.OrderUnit);
    }

    [Fact]
    public async Task NewItem_IncompatiblePackage_MarksPackageCheckNeeded()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db, "계란", "개", packageQuantity: 30, packageUnit: "판");
        var menu = CreateMenu(db, "계란찜");
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing, 60, "개");

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        var item = Assert.Single(result.Items);
        Assert.Null(item.SuggestedOrderQuantity); // 개↔판 변환 불가 → 추천 없음
        Assert.False(item.PackageCompatible);
        Assert.Equal(60.0, item.OrderQuantity); // 필요량 기본값
        Assert.Equal("개", item.OrderUnit);
    }

    // =======================================================================
    // 사용자 Override (필수 테스트 52)
    // =======================================================================

    [Fact]
    public async Task UserOverride_IsNotOverwrittenBySuggested()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db, "데미글라스소스", "g", packageQuantity: 2, packageUnit: "kg");
        var menu = CreateMenu(db, "함박스테이크");
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing, 800, "g");

        var service = harness.CreateOrderService();
        // 추천 2kg이지만 사용자가 3kg로 직접 수정
        await service.SaveItemsAsync([
            Item(new DateOnly(2025, 1, 1), ing.Id, "데미글라스소스", 800, 3.0, "pending", unit: "kg"),
        ]);

        // 식단 필요량 변경 (800g → 1600g, 추천은 2kg 유지)
        AddIngredientToService(db, svc, menu, ing, 800, "g");
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        var item = Assert.Single(result.Items);
        Assert.Equal(1600, item.RequiredQuantity);
        Assert.Equal(2.0, item.SuggestedOrderQuantity);
        Assert.Equal(3.0, item.OrderQuantity); // 사용자 입력 유지
    }

    // =======================================================================
    // SourceMenus (필수 테스트 53)
    // =======================================================================

    [Fact]
    public async Task SourceMenus_ReportPerMenuQuantities()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db, "양파", "kg");
        var menu1 = CreateMenu(db, "제육볶음");
        var menu2 = CreateMenu(db, "육개장");
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu1, ing, 10.0);
        AddIngredientToService(db, svc, menu2, ing, 5.0);

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));

        var item = Assert.Single(result.Items);
        Assert.Equal(15.0, item.RequiredQuantity);
        Assert.Equal(2, item.SourceMenus.Count);
        var byMenu = item.SourceMenus.ToDictionary(m => m.MenuName);
        Assert.Equal(10.0, byMenu["제육볶음"].Quantity);
        Assert.Equal(5.0, byMenu["육개장"].Quantity);
        Assert.All(item.SourceMenus, m => Assert.Equal("제육볶음" == m.MenuName ? "제육볶음" : "육개장", m.MenuName));
        Assert.All(item.SourceMenus, m => Assert.Equal("중식", m.MealTypeName));
    }

    // =======================================================================
    // 묶음 발주 (필수 테스트 54, test_orders.py test_create_order_group_links_rows_and_marks_ordered)
    // =======================================================================

    [Fact]
    public async Task CreateOrderGroup_LinksRowsAndMarksOrdered()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db, "대파", "kg");
        var menu = CreateMenu(db);
        var svc1 = CreateService(db, new DateOnly(2025, 1, 1));
        var svc2 = CreateService(db, new DateOnly(2025, 1, 3));
        AddIngredientToService(db, svc1, menu, ing, 10.0);
        AddIngredientToService(db, svc2, menu, ing, 7.0);

        var service = harness.CreateOrderService();
        await service.SaveItemsAsync([
            Item(new DateOnly(2025, 1, 1), ing.Id, "대파", 10.0, 10.0, "pending", orderDate: new DateOnly(2024, 12, 31), deliveryDate: new DateOnly(2025, 1, 1)),
            Item(new DateOnly(2025, 1, 3), ing.Id, "대파", 7.0, 7.0, "pending", orderDate: new DateOnly(2025, 1, 2), deliveryDate: new DateOnly(2025, 1, 3)),
        ]);

        var groupId = await service.CreateOrderGroupAsync(new OrderGroupCreateInput(
            [
                Item(new DateOnly(2025, 1, 1), ing.Id, "대파", 10.0, 10.0, "pending", orderDate: new DateOnly(2024, 12, 31), deliveryDate: new DateOnly(2025, 1, 1)),
                Item(new DateOnly(2025, 1, 3), ing.Id, "대파", 7.0, 7.0, "pending", orderDate: new DateOnly(2025, 1, 2), deliveryDate: new DateOnly(2025, 1, 3)),
            ],
            OrderQuantity: 18.0,
            OrderUnit: "kg",
            OrderDate: new DateOnly(2024, 12, 31),
            DeliveryDate: new DateOnly(2025, 1, 1)));

        using (var verify = harness.CreateContext())
        {
            var group = verify.OrderGroups.Single(g => g.Id == groupId);
            Assert.Equal(17.0, group.TotalRequiredQuantity); // 10 + 7
            Assert.Equal("kg", group.RequiredUnit);
            Assert.Equal(18.0, group.OrderQuantity);

            var rows = verify.OrderItems.ToList();
            Assert.Equal(2, rows.Count);
            Assert.All(rows, r => Assert.Equal(groupId, r.OrderGroupId));
            Assert.All(rows, r => Assert.Equal(OrderStatus.Ordered, r.Status));
            Assert.All(rows, r => Assert.Equal(new DateOnly(2024, 12, 31), r.OrderDate));
            Assert.All(rows, r => Assert.Equal(new DateOnly(2025, 1, 1), r.DeliveryDate));
        }
    }

    [Fact]
    public async Task CreateOrderGroup_MixedIngredients_IsRejected()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing1 = CreateIngredient(db, "대파", "kg");
        var ing2 = CreateIngredient(db, "양파", "kg");
        var menu = CreateMenu(db);
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing1, 5.0);
        AddIngredientToService(db, svc, menu, ing2, 3.0);

        var service = harness.CreateOrderService();
        await Assert.ThrowsAsync<MixedIngredientGroupException>(() =>
            service.CreateOrderGroupAsync(new OrderGroupCreateInput(
                [
                    Item(new DateOnly(2025, 1, 1), ing1.Id, "대파", 5.0, 5.0, "pending"),
                    Item(new DateOnly(2025, 1, 1), ing2.Id, "양파", 3.0, 3.0, "pending"),
                ],
                OrderQuantity: 8.0,
                OrderUnit: "kg",
                OrderDate: new DateOnly(2024, 12, 31),
                DeliveryDate: new DateOnly(2025, 1, 1))));
    }

    [Fact]
    public async Task CreateOrderGroup_NameKeyedRows_SameNameAllowed()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var menu = CreateMenu(db);
        var svc1 = CreateService(db, new DateOnly(2025, 1, 1));
        var svc2 = CreateService(db, new DateOnly(2025, 1, 3));
        AddIngredientToService(db, svc1, menu, null, 5.0, nameSnapshot: "삭제된재료");
        AddIngredientToService(db, svc2, menu, null, 3.0, nameSnapshot: "삭제된재료");

        var service = harness.CreateOrderService();
        var groupId = await service.CreateOrderGroupAsync(new OrderGroupCreateInput(
            [
                Item(new DateOnly(2025, 1, 1), null, "삭제된재료", 5.0, 5.0, "pending"),
                Item(new DateOnly(2025, 1, 3), null, "삭제된재료", 3.0, 3.0, "pending"),
            ],
            OrderQuantity: 8.0,
            OrderUnit: "kg",
            OrderDate: new DateOnly(2024, 12, 31),
            DeliveryDate: new DateOnly(2025, 1, 1)));

        using var verify = harness.CreateContext();
        Assert.Equal(2, verify.OrderItems.Count(r => r.OrderGroupId == groupId));
    }

    // =======================================================================
    // 일괄 변경 (필수 테스트 55, test_orders.py test_bulk_update_status_and_dates)
    // =======================================================================

    [Fact]
    public async Task BulkUpdate_StatusAndDates()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing1 = CreateIngredient(db, "대파", "kg");
        var ing2 = CreateIngredient(db, "양파", "kg");
        var menu = CreateMenu(db);
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing1, 5.0);
        AddIngredientToService(db, svc, menu, ing2, 3.0);

        var service = harness.CreateOrderService();
        await service.SaveItemsAsync([
            Item(new DateOnly(2025, 1, 1), ing1.Id, "대파", 5.0, 5.0, "pending", orderDate: new DateOnly(2024, 12, 31), deliveryDate: new DateOnly(2025, 1, 1)),
            Item(new DateOnly(2025, 1, 1), ing2.Id, "양파", 3.0, 3.0, "pending", orderDate: new DateOnly(2024, 12, 31), deliveryDate: new DateOnly(2025, 1, 1)),
        ]);

        var updated = await service.BulkUpdateAsync(new OrderBulkUpdateInput(
            [
                Item(new DateOnly(2025, 1, 1), ing1.Id, "대파", 5.0, 5.0, "pending", orderDate: new DateOnly(2024, 12, 31), deliveryDate: new DateOnly(2025, 1, 1)),
                Item(new DateOnly(2025, 1, 1), ing2.Id, "양파", 3.0, 3.0, "pending", orderDate: new DateOnly(2024, 12, 31), deliveryDate: new DateOnly(2025, 1, 1)),
            ],
            OrderDate: new DateOnly(2025, 1, 2),
            DeliveryDate: null,
            Status: "skipped"));

        Assert.Equal(2, updated);
        using (var verify = harness.CreateContext())
        {
            var rows = verify.OrderItems.ToList();
            Assert.All(rows, r => Assert.Equal(OrderStatus.Skipped, r.Status));
            Assert.All(rows, r => Assert.Equal(new DateOnly(2025, 1, 2), r.OrderDate));
            Assert.All(rows, r => Assert.Equal(new DateOnly(2025, 1, 1), r.DeliveryDate)); // 변경 안 함
        }
    }

    [Fact]
    public async Task BulkUpdate_NoChanges_IsRejected()
    {
        using var harness = new OrderTestHarness();
        var service = harness.CreateOrderService();
        await Assert.ThrowsAsync<NoChangesToApplyException>(() =>
            service.BulkUpdateAsync(new OrderBulkUpdateInput([], OrderDate: null, DeliveryDate: null, Status: null)));
    }

    [Fact]
    public async Task BulkUpdate_InvalidStatus_IsRejected()
    {
        using var harness = new OrderTestHarness();
        var service = harness.CreateOrderService();
        await Assert.ThrowsAsync<InvalidOrderStatusException>(() =>
            service.BulkUpdateAsync(new OrderBulkUpdateInput([], OrderDate: null, DeliveryDate: null, Status: "done")));
    }

    // =======================================================================
    // 저장 검증
    // =======================================================================

    [Fact]
    public async Task SaveItems_InvalidStatus_IsRejected()
    {
        using var harness = new OrderTestHarness();
        var service = harness.CreateOrderService();
        await Assert.ThrowsAsync<InvalidOrderStatusException>(() =>
            service.SaveItemsAsync([Item(new DateOnly(2025, 1, 1), 1, "대파", 5.0, 5.0, "done")]));
    }

    [Fact]
    public async Task SaveItems_OrderNote_IsPersisted()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db, "대파", "kg");
        var menu = CreateMenu(db);
        var svc = CreateService(db, new DateOnly(2025, 1, 1));
        AddIngredientToService(db, svc, menu, ing, 5.0);

        var service = harness.CreateOrderService();
        await service.SaveItemsAsync([
            new OrderItemSaveInput(
                new DateOnly(2025, 1, 1), ing.Id, "대파", 5.0, "kg", 5.0, "kg",
                new DateOnly(2024, 12, 31), new DateOnly(2025, 1, 1), "pending", "납품시간 확인"),
        ]);

        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3));
        var item = Assert.Single(result.Items);
        Assert.Equal("납품시간 확인", item.OrderNote);
    }

    [Fact]
    public async Task SaveItems_NameKeyedUpsert_DoesNotDuplicate()
    {
        using var harness = new OrderTestHarness();
        var service = harness.CreateOrderService();

        await service.SaveItemsAsync([Item(new DateOnly(2025, 1, 1), null, "삭제된재료", 5.0, 5.0, "pending")]);
        await service.SaveItemsAsync([Item(new DateOnly(2025, 1, 1), null, "삭제된재료", 5.0, 6.0, "ordered")]);

        using var verify = harness.CreateContext();
        var row = Assert.Single(verify.OrderItems);
        Assert.Equal(6.0, row.OrderQuantity);
        Assert.Equal(OrderStatus.Ordered, row.Status);
    }

    // =======================================================================
    // 대량 데이터 (필수 테스트 56)
    // =======================================================================

    [Fact]
    public async Task LargeDataset_SpecificIngredientLookup_Works()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();

        // 300개 이상의 식재료와 수백 개 OrderItem
        var menu = CreateMenu(db, "대량테스트메뉴");
        var target = CreateIngredient(db, "특정재료", "kg");
        for (var i = 0; i < 320; i++)
        {
            CreateIngredient(db, $"재료{i:D3}", "kg");
        }

        // 5일 × 60개 재료 = 300개 식단 재료 행
        for (var day = 0; day < 5; day++)
        {
            var svc = CreateService(db, new DateOnly(2025, 3, 3).AddDays(day));
            for (var i = 0; i < 60; i++)
            {
                var ing = db.Ingredients.First(x => x.Name == $"재료{i:D3}");
                AddIngredientToService(db, svc, menu, ing, 1.0 + i);
            }

            AddIngredientToService(db, svc, menu, target, 10.0 + day);
        }

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 7));

        Assert.Equal(305, result.Items.Count); // 5일 × 61개 재료
        var targetItems = result.Items.Where(i => i.IngredientName == "특정재료").ToList();
        Assert.Equal(5, targetItems.Count);
        Assert.Equal(new[] { 10.0, 11.0, 12.0, 13.0, 14.0 }, targetItems.Select(i => i.RequiredQuantity!.Value).OrderBy(x => x));
    }

    // =======================================================================
    // 기간 처리
    // =======================================================================

    [Fact]
    public async Task GetOrders_ReversedRange_IsResolved()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db, "대파", "kg");
        var menu = CreateMenu(db);
        var svc = CreateService(db, new DateOnly(2025, 1, 2));
        AddIngredientToService(db, svc, menu, ing, 5.0);

        var service = harness.CreateOrderService();
        var result = await service.GetOrdersAsync(new DateOnly(2025, 1, 5), new DateOnly(2025, 1, 1));

        Assert.Equal(new DateOnly(2025, 1, 1), result.StartDate);
        Assert.Equal(new DateOnly(2025, 1, 5), result.EndDate);
        Assert.Single(result.Items);
    }

    // =======================================================================
    // Migration (필수 테스트 58)
    // =======================================================================

    [Fact]
    public void Migration_AddsProcurementFields()
    {
        using var harness = new OrderTestHarness();
        using var db = harness.CreateContext();
        var ing = CreateIngredient(db, "판매단위재료", "g", packageQuantity: 2, packageUnit: "kg");

        using var verify = harness.CreateContext();
        var loaded = verify.Ingredients.Single(i => i.Id == ing.Id);
        Assert.Equal(2.0, loaded.PurchasePackageQuantity);
        Assert.Equal("kg", loaded.PurchasePackageUnit);
    }
}

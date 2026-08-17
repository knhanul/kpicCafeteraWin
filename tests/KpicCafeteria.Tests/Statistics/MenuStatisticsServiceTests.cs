using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;
using Xunit;

namespace KpicCafeteria.Tests.Statistics;

public sealed class MenuStatisticsServiceTests
{
    // =======================================================================
    // 순수 계산 규칙 (Python menu_statistics.py와 동일)
    // =======================================================================

    [Fact]
    public void MenuKey_UsesMenuId_WhenPresent()
    {
        var row = new MenuUsageRow(1, new DateOnly(2026, 1, 5), "LUNCH", "중식", 100, 100, 7, "돼지고기김치찌개", "주찬");
        Assert.Equal("7", MenuStatisticsService.MenuKey(row));
    }

    [Fact]
    public void MenuKey_UsesNamePrefix_WhenMenuIdNull()
    {
        var row = new MenuUsageRow(1, new DateOnly(2026, 1, 5), "LUNCH", "중식", 100, 100, null, "특식", "기타");
        Assert.Equal("name:특식", MenuStatisticsService.MenuKey(row));
    }

    [Fact]
    public void MaxInWindow_IncludesBoundaryDay()
    {
        var dates = new List<DateOnly> { new(2026, 1, 5), new(2026, 1, 19) };
        Assert.Equal(2, MenuStatisticsService.MaxInWindow(dates, 14)); // 14일 차이: 경계 포함
        Assert.Equal(1, MenuStatisticsService.MaxInWindow(dates, 13)); // 14일 차이: 초과
    }

    [Fact]
    public void MaxInWindow_CountsConsecutiveWindow()
    {
        var dates = new List<DateOnly>
        {
            new(2026, 1, 5), new(2026, 1, 12), new(2026, 1, 19), new(2026, 2, 2),
        };
        Assert.Equal(3, MenuStatisticsService.MaxInWindow(dates, 14)); // 1/5~1/19
        Assert.Equal(4, MenuStatisticsService.MaxInWindow(dates, 28)); // 1/5~2/2
    }

    // =======================================================================
    // 통합 계산 (실제 SQLite)
    // =======================================================================

    [Fact]
    public async Task GetAsync_GroupsByMenuId_AndMergesByNameWhenIdNull()
    {
        int menuAId;
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menuA = fixture.AddMenu("돼지고기김치찌개", "주찬");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, menuItems: [M(menuA, "돼지고기김치찌개")]);
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.DINNER, 100, menuItems: [M(menuA, "돼지고기김치찌개")]);
            // MenuId 없이 같은 이름 스냅샷 2회 → name 키로 병합
            fixture.AddService(new DateOnly(2026, 1, 7), MealType.LUNCH, 100, menuItems: [M(null, "특식")]);
            fixture.AddService(new DateOnly(2026, 1, 8), MealType.LUNCH, 100, menuItems: [M(null, "특식")]);
            fixture.Save();
            menuAId = fixture.Menus[0].Id; // Save 이후 실제 ID
        }

        var result = await harness.CreateMenuStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(2, result.Summary.UniqueMenuCount);
        Assert.Equal(4, result.Summary.TotalUsageCount);

        var top = result.TopMenus;
        Assert.Equal(2, top.Count);
        Assert.Equal("돼지고기김치찌개", top[0].MenuName);
        Assert.Equal(2, top[0].UsageCount);
        Assert.Equal(1, top[0].LunchCount);
        Assert.Equal(1, top[0].DinnerCount);
        Assert.Equal(menuAId, top[0].MenuId);

        Assert.Equal("특식", top[1].MenuName);
        Assert.Null(top[1].MenuId);
        Assert.Equal(2, top[1].UsageCount);
    }

    [Fact]
    public async Task GetAsync_DetectsNewMenus()
    {
        int menuYId;
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menuX = fixture.AddMenu("기존메뉴");
            var menuY = fixture.AddMenu("신규메뉴");
            // 기존메뉴는 기간 시작 전 사용 이력 존재
            fixture.AddService(new DateOnly(2025, 12, 1), MealType.LUNCH, 100, menuItems: [M(menuX, "기존메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, menuItems: [M(menuX, "기존메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100, menuItems: [M(menuY, "신규메뉴")]);
            fixture.Save();
            menuYId = fixture.Menus[1].Id; // Save 이후 실제 ID
        }

        var result = await harness.CreateMenuStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(1, result.Summary.NewMenuCount);
        var newMenu = result.TopMenus.Single(m => m.MenuName == "신규메뉴");
        Assert.Equal(menuYId, newMenu.MenuId);
    }

    [Fact]
    public async Task GetAsync_DetectsShortAndExcessiveRepeats()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menuR = fixture.AddMenu("반복메뉴");
            // 7일 간격 3회: 14일 창 3회(단기), 28일 창 3회(과다)
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, menuItems: [M(menuR, "반복메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 12), MealType.LUNCH, 100, menuItems: [M(menuR, "반복메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 19), MealType.LUNCH, 100, menuItems: [M(menuR, "반복메뉴")]);
            fixture.Save();
        }

        var result = await harness.CreateMenuStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(2, result.Repeats.Count);
        Assert.Contains(result.Repeats, r => r.Type == "단기 반복" && r.Count == 3 && r.WindowDays == 14);
        Assert.Contains(result.Repeats, r => r.Type == "과다 반복" && r.Count == 3 && r.WindowDays == 28);
        Assert.Equal(1, result.Summary.RepeatMenuCount); // 고유 메뉴명 수
    }

    [Fact]
    public async Task GetAsync_NoRepeat_WhenSpreadOut()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menuS = fixture.AddMenu("드문메뉴");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, menuItems: [M(menuS, "드문메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 26), MealType.LUNCH, 100, menuItems: [M(menuS, "드문메뉴")]);
            fixture.Save();
        }

        var result = await harness.CreateMenuStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Empty(result.Repeats);
    }

    [Fact]
    public async Task GetAsync_UnusedMenus_IncludesBoundaryAndSorts()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var usedMenu = fixture.AddMenu("사용중메뉴");
            var staleMenu = fixture.AddMenu("오래된메뉴");
            var neverMenu = fixture.AddMenu("미사용메뉴");
            var boundaryMenu = fixture.AddMenu("경계메뉴");
            // end=2026-03-31, unusedDays=90 → cutoff=2025-12-31
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, menuItems: [M(usedMenu, "사용중메뉴")]); // 창 내 사용
            fixture.AddService(new DateOnly(2025, 12, 1), MealType.LUNCH, 100, menuItems: [M(staleMenu, "오래된메뉴")]); // cutoff 이전
            fixture.AddService(new DateOnly(2025, 12, 31), MealType.LUNCH, 100, menuItems: [M(boundaryMenu, "경계메뉴")]); // cutoff 당일: 창 포함
            fixture.Save();
        }

        var result = await harness.CreateMenuStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), unusedDays: 90);

        Assert.Equal(2, result.Summary.UnusedMenuCount);
        // 미사용 기간 긴 순: 사용 이력 없음(null)이 먼저
        Assert.Equal("미사용메뉴", result.UnusedMenus[0].MenuName);
        Assert.Null(result.UnusedMenus[0].DaysSinceLast);
        Assert.Equal("오래된메뉴", result.UnusedMenus[1].MenuName);
        Assert.Equal(120, result.UnusedMenus[1].DaysSinceLast); // 2026-03-31 - 2025-12-01
        Assert.DoesNotContain(result.UnusedMenus, u => u.MenuName == "사용중메뉴");
        Assert.DoesNotContain(result.UnusedMenus, u => u.MenuName == "경계메뉴");
    }

    [Fact]
    public async Task GetAsync_Backdata_TracksPreviousUse()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menuM = fixture.AddMenu("주기메뉴");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110, menuItems: [M(menuM, "주기메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 12), MealType.DINNER, 100, menuItems: [M(menuM, "주기메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 19), MealType.LUNCH, 100, menuItems: [M(menuM, "주기메뉴")]);
            fixture.Save();
        }

        var result = await harness.CreateMenuStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(3, result.Backdata.Count);
        Assert.Null(result.Backdata[0].PreviousUsedDate);
        Assert.Equal(new DateOnly(2026, 1, 5), result.Backdata[1].PreviousUsedDate);
        Assert.Equal(7, result.Backdata[1].DaysSincePrevious);
        Assert.Equal(new DateOnly(2026, 1, 12), result.Backdata[2].PreviousUsedDate);
        Assert.Equal(7, result.Backdata[2].DaysSincePrevious);
        Assert.Equal("중식", result.Backdata[0].MealTypeName);
        Assert.Equal(110, result.Backdata[0].ActualCount);
    }

    [Fact]
    public async Task GetDetailAsync_ComputesMonthlyCoUsedAndHistory()
    {
        int menuMId;
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menuM = fixture.AddMenu("메인메뉴", "주찬");
            var menuN = fixture.AddMenu("곁들임메뉴", "부찬");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110,
                menuItems: [M(menuM, "메인메뉴"), M(menuN, "곁들임메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 12), MealType.DINNER, 100,
                menuItems: [M(menuM, "메인메뉴"), M(menuN, "곁들임메뉴")]);
            fixture.AddService(new DateOnly(2026, 2, 2), MealType.LUNCH, 100,
                menuItems: [M(menuM, "메인메뉴")]);
            fixture.Save();
            menuMId = fixture.Menus[0].Id; // Save 이후 실제 ID
        }

        var service = harness.CreateMenuStatisticsService();
        var detail = await service.GetDetailAsync(
            menuMId, new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28));

        Assert.NotNull(detail);
        Assert.Equal("메인메뉴", detail!.MenuName);
        Assert.Equal("주찬", detail.Role);
        Assert.Equal(3, detail.Summary.UsageCount);
        Assert.Equal(2, detail.Summary.LunchCount);
        Assert.Equal(1, detail.Summary.DinnerCount);
        Assert.Equal(new DateOnly(2026, 1, 5), detail.Summary.FirstUsed);
        Assert.Equal(new DateOnly(2026, 2, 2), detail.Summary.LastUsed);

        Assert.Equal(2, detail.MonthlyUsage.Count);
        Assert.Equal("2026-01", detail.MonthlyUsage[0].Month);
        Assert.Equal(2, detail.MonthlyUsage[0].Count);
        Assert.Equal("2026-02", detail.MonthlyUsage[1].Month);

        var coUsed = Assert.Single(detail.CoUsed);
        Assert.Equal("곁들임메뉴", coUsed.MenuName);
        Assert.Equal(2, coUsed.Count);

        Assert.Equal(3, detail.RecentHistory.Count);
        Assert.Equal(3, detail.Backdata.Count);
        Assert.Equal(new DateOnly(2026, 1, 5), detail.Backdata[1].PreviousUsedDate);
    }

    [Fact]
    public async Task GetDetailAsync_UnknownMenu_ReturnsNull()
    {
        using var harness = new StatisticsTestHarness();

        var detail = await harness.CreateMenuStatisticsService().GetDetailAsync(
            999, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetDetailAsync_KnownMenuWithoutUsage_ReturnsEmptyDetail()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menu = fixture.AddMenu("미사용메뉴", "주찬");
            fixture.Save();
        }

        var detail = await harness.CreateMenuStatisticsService().GetDetailAsync(
            1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.NotNull(detail);
        Assert.Equal("미사용메뉴", detail!.MenuName);
        Assert.Equal(0, detail.Summary.UsageCount);
        Assert.Empty(detail.MonthlyUsage);
        Assert.Empty(detail.Backdata);
    }

    private static (Menu? Menu, string MenuName, string Role, (Ingredient? Ingredient, string Name, double? QuantityTotal, double? QuantityPer100, string? Unit)[] Ingredients) M(
        Menu? menu, string name, string role = "주찬")
        => (menu, name, role, []);
}

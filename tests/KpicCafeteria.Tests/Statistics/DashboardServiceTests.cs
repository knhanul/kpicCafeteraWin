using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;
using Xunit;

namespace KpicCafeteria.Tests.Statistics;

public sealed class DashboardServiceTests
{
    [Fact]
    public void MonthsBack_ReturnsFirstDayOfMonth()
    {
        Assert.Equal(new DateOnly(2025, 2, 1), DashboardService.MonthsBack(new DateOnly(2026, 1, 31), 11));
        Assert.Equal(new DateOnly(2025, 1, 1), DashboardService.MonthsBack(new DateOnly(2026, 1, 31), 12));
        Assert.Equal(new DateOnly(2025, 8, 1), DashboardService.MonthsBack(new DateOnly(2026, 7, 15), 11));
    }

    [Fact]
    public void PreviousPeriod_SameLengthBeforeStart()
    {
        var (start, end) = DashboardService.PreviousPeriod(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        Assert.Equal(new DateOnly(2025, 12, 1), start);
        Assert.Equal(new DateOnly(2025, 12, 31), end);
    }

    [Fact]
    public void IngredientChanges_DetectsThresholds()
    {
        var current = new List<IngredientGroupRow>
        {
            new("육류", 2, 10.0),
            new("채소", 2, 5.0),
            new("신규군", 1, 3.0),
        };
        var previous = new List<IngredientGroupRow>
        {
            new("육류", 2, 5.0),
            new("채소", 2, 4.0),
            new("사라진군", 1, 2.0),
        };

        var changes = DashboardService.IngredientChanges(current, previous);

        Assert.Equal(2, changes.Count);
        var meat = changes.Single(c => c.Group == "육류");
        Assert.Equal(100.0, meat.Rate);
        Assert.Equal("중요", meat.Level);
        var veg = changes.Single(c => c.Group == "채소");
        Assert.Equal(25.0, veg.Rate);
        Assert.Equal("확인", veg.Level);
    }

    [Fact]
    public async Task GetAsync_ComputesKpisAndTrend()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menuA = fixture.AddMenu("돼지고기김치찌개", "주찬");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110,
                mealPlanOutputAt: new DateTime(2026, 1, 4, 9, 0, 0),
                cookingOutputAt: new DateTime(2026, 1, 5, 7, 0, 0),
                preservationCompleted: true,
                menuItems: [M(menuA, "돼지고기김치찌개")]);
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100, actualCount: 90,
                menuItems: [M(menuA, "돼지고기김치찌개")]);
            fixture.AddService(new DateOnly(2026, 1, 7), MealType.DINNER, 100,
                menuItems: [M(null, "특식")]);
            fixture.Save();
        }

        var result = await harness.CreateDashboardService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(3, result.Kpis.OperatingDays);
        Assert.Equal(2, result.Kpis.UniqueMenuCount); // (id 1) + (name:특식)
        Assert.Equal(2, result.Kpis.Lunch!.ServiceCount);
        Assert.Equal(200, result.Kpis.Lunch.ActualSum);
        Assert.Equal(1, result.Kpis.Dinner!.ServiceCount);

        // 추세는 end 기준 12개월 전부터, 데이터가 있는 월만 포함
        Assert.Single(result.Trend);
        Assert.Equal("2026-01", result.Trend[0].Month);
        Assert.Equal(300, result.Trend[0].Planned);
        Assert.Equal(200, result.Trend[0].Actual);

        // 메뉴 사용 현황 (스냅샷명 기준)
        Assert.Equal(2, result.MenuUsage.Count);
        Assert.Equal("돼지고기김치찌개", result.MenuUsage[0].MenuName);
        Assert.Equal(2, result.MenuUsage[0].Count);

        // 업무 기록 현황
        Assert.Equal(1, result.Workflow.CookingOutput);
        Assert.Equal(1, result.Workflow.PreservationCompleted);
        Assert.Equal(2, result.Workflow.ActualRecorded);
    }

    [Fact]
    public async Task GetAsync_RepeatedMenus_IncludePeriodAndPrevious4Weeks()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var repeat = fixture.AddMenu("반복메뉴");
            var previous = fixture.AddMenu("직전사용메뉴");
            var once = fixture.AddMenu("단발메뉴");
            // 기간 내 2회
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, menuItems: [M(repeat, "반복메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 12), MealType.LUNCH, 100, menuItems: [M(repeat, "반복메뉴")]);
            // 직전 4주(2025-12-04~12-31) 1회 + 기간 1회
            fixture.AddService(new DateOnly(2025, 12, 10), MealType.LUNCH, 100, menuItems: [M(previous, "직전사용메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100, menuItems: [M(previous, "직전사용메뉴")]);
            // 기간 1회만
            fixture.AddService(new DateOnly(2026, 1, 7), MealType.LUNCH, 100, menuItems: [M(once, "단발메뉴")]);
            fixture.Save();
        }

        var result = await harness.CreateDashboardService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(2, result.RepeatedMenus.Count);
        var repeatStat = result.RepeatedMenus.Single(m => m.MenuName == "반복메뉴");
        Assert.Equal(2, repeatStat.PeriodCount);
        Assert.Equal(0, repeatStat.Previous4Weeks);
        var previousStat = result.RepeatedMenus.Single(m => m.MenuName == "직전사용메뉴");
        Assert.Equal(1, previousStat.PeriodCount);
        Assert.Equal(1, previousStat.Previous4Weeks);
        Assert.Equal(new DateOnly(2025, 12, 10), previousStat.LastServed);
        Assert.DoesNotContain(result.RepeatedMenus, m => m.MenuName == "단발메뉴");
    }

    [Fact]
    public async Task GetAsync_IngredientGroups_ApplyKgConversion()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var meat = fixture.AddIngredient("돼지고기", "육류", "kg");
            var veg = fixture.AddIngredient("배추", "채소", "g");
            var sauce = fixture.AddIngredient("고추장", "양념", "봉", kgFactor: 0.9);
            var piece = fixture.AddIngredient("계란", "기타", "개"); // 환산 불가
            var excluded = fixture.AddIngredient("분석제외", "기타", "kg", analysisExcluded: true);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100,
                menuItems: [M("메뉴", [
                    (meat, "돼지고기", null, 10, "kg"),      // 10 * 100/100 = 10kg
                    (veg, "배추", null, 500, "g"),          // 500 * 100/100 / 1000 = 0.5kg
                    (sauce, "고추장", null, 2, "봉"),       // 2 * 100/100 * 0.9 = 1.8kg
                    (piece, "계란", null, 3, "개"),         // 환산 없음 → kg 미포함
                    (excluded, "분석제외", null, 1, "kg"),  // analysis_excluded → 제외
                ])]);
            fixture.Save();
        }

        var result = await harness.CreateDashboardService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(4, result.IngredientGroups.Count); // 분석제외 제외
        var meatStat = result.IngredientGroups.Single(g => g.Group == "육류");
        Assert.Equal(1, meatStat.UsageRows);
        Assert.Equal(10.0, meatStat.EstimatedKg);
        var vegStat = result.IngredientGroups.Single(g => g.Group == "채소");
        Assert.Equal(0.5, vegStat.EstimatedKg);
        var sauceStat = result.IngredientGroups.Single(g => g.Group == "양념");
        Assert.Equal(1.8, sauceStat.EstimatedKg);
        var pieceStat = result.IngredientGroups.Single(g => g.Group == "기타");
        Assert.Equal(1, pieceStat.UsageRows);
        Assert.Equal(0.0, pieceStat.EstimatedKg);
    }

    [Fact]
    public async Task GetAsync_IngredientChanges_CompareWithPreviousPeriod()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var meat = fixture.AddIngredient("돼지고기", "육류", "kg");
            // 이전 기간(2025-12-01~12-31): 5kg
            fixture.AddService(new DateOnly(2025, 12, 1), MealType.LUNCH, 100,
                menuItems: [M("메뉴", [(meat, "돼지고기", null, 5, "kg")])]);
            // 현재 기간: 10kg → +100% 중요
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100,
                menuItems: [M("메뉴", [(meat, "돼지고기", null, 10, "kg")])]);
            fixture.Save();
        }

        var result = await harness.CreateDashboardService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var change = Assert.Single(result.Anomalies.IngredientChanges);
        Assert.Equal("육류", change.Group);
        Assert.Equal(10.0, change.CurrentKg);
        Assert.Equal(5.0, change.PreviousKg);
        Assert.Equal(100.0, change.Rate);
        Assert.Equal("중요", change.Level);
    }

    [Fact]
    public async Task GetAsync_RecordGaps_ExcludeMealPlanOutput()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100); // 모든 기록 누락
            fixture.Save();
        }

        var result = await harness.CreateDashboardService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        // 대시보드는 식단표 미출력을 제외한 3종만 표시 (Python _record_gaps와 동일)
        Assert.Equal(3, result.Anomalies.RecordGaps.Count);
        Assert.DoesNotContain(result.Anomalies.RecordGaps, g => g.Type == "식단표 미출력");
        Assert.Contains(result.Anomalies.RecordGaps, g => g.Type == "실제 식수 미입력");
        Assert.Contains(result.Anomalies.RecordGaps, g => g.Type == "보존식 기록 미완료");
        Assert.Contains(result.Anomalies.RecordGaps, g => g.Type == "조리지시서 미출력");
    }

    [Fact]
    public async Task GetAsync_MenuRepeats_OnlyMenuIdBased()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menu = fixture.AddMenu("반복메뉴");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, menuItems: [M(menu, "반복메뉴")]);
            fixture.AddService(new DateOnly(2026, 1, 12), MealType.LUNCH, 100, menuItems: [M(menu, "반복메뉴")]);
            // MenuId 없는 스냅샷 반복은 대시보드 메뉴 반복에서 제외 (Python _menu_repeats와 동일)
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.DINNER, 100, menuItems: [M(null, "특식")]);
            fixture.AddService(new DateOnly(2026, 1, 12), MealType.DINNER, 100, menuItems: [M(null, "특식")]);
            fixture.Save();
        }

        var result = await harness.CreateDashboardService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var repeat = Assert.Single(result.Anomalies.MenuRepeats);
        Assert.Equal("반복메뉴", repeat.MenuName);
        Assert.NotNull(repeat.MenuId);
    }

    [Fact]
    public async Task GetAsync_MealAnomalies_Included()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 130); // +30%
            fixture.Save();
        }

        var result = await harness.CreateDashboardService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var anomaly = Assert.Single(result.Anomalies.Meal);
        Assert.Equal("식수 급증", anomaly.Type);
        Assert.Equal("중요", anomaly.Level);
    }

    private static (Menu? Menu, string MenuName, string Role, (Ingredient? Ingredient, string Name, double? QuantityTotal, double? QuantityPer100, string? Unit)[] Ingredients) M(
        Menu? menu, string name)
        => (menu, name, "주찬", []);

    private static (Menu? Menu, string MenuName, string Role, (Ingredient? Ingredient, string Name, double? QuantityTotal, double? QuantityPer100, string? Unit)[] Ingredients) M(
        string menuName, (Ingredient? Ingredient, string Name, double? QuantityTotal, double? QuantityPer100, string? Unit)[] ingredients)
        => (null, menuName, "주찬", ingredients);
}

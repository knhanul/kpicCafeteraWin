using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;
using Xunit;

namespace KpicCafeteria.Tests.Statistics;

public sealed class IngredientStatisticsServiceTests
{
    // =======================================================================
    // 순수 계산 규칙 (Python ingredient_statistics.py와 동일)
    // =======================================================================

    [Fact]
    public void ResolveQuantity_PrefersQuantityTotal()
    {
        var row = new IngredientUsageRow(1, 1, new DateOnly(2026, 1, 5), "LUNCH", "중식", 100, 100, 1, "돼지고기", 12.5, 10, "kg", "육류", "메뉴");
        Assert.Equal(12.5, IngredientStatisticsService.ResolveQuantity(row));
    }

    [Fact]
    public void ResolveQuantity_FallsBackToPer100()
    {
        var row = new IngredientUsageRow(1, 1, new DateOnly(2026, 1, 5), "LUNCH", "중식", 200, 100, 1, "돼지고기", null, 10, "kg", "육류", "메뉴");
        Assert.Equal(20.0, IngredientStatisticsService.ResolveQuantity(row)); // 10 * 200 / 100
    }

    [Fact]
    public void ResolveQuantity_ReturnsNull_WhenNothingAvailable()
    {
        var row = new IngredientUsageRow(1, 1, new DateOnly(2026, 1, 5), "LUNCH", "중식", 100, 100, 1, "돼지고기", null, null, "kg", "육류", "메뉴");
        Assert.Null(IngredientStatisticsService.ResolveQuantity(row));
    }

    [Fact]
    public void ResolveQuantity_NoFallback_WhenPlannedCountZero()
    {
        var row = new IngredientUsageRow(1, 1, new DateOnly(2026, 1, 5), "LUNCH", "중식", 0, 100, 1, "돼지고기", null, 10, "kg", "육류", "메뉴");
        Assert.Null(IngredientStatisticsService.ResolveQuantity(row));
    }

    [Fact]
    public void IngredientKey_UsesId_WhenPresent()
    {
        var row = new IngredientUsageRow(1, 1, new DateOnly(2026, 1, 5), "LUNCH", "중식", 100, 100, 3, "돼지고기", null, null, "kg", "육류", "메뉴");
        Assert.Equal("3", IngredientStatisticsService.IngredientKey(row));
    }

    [Fact]
    public void IngredientKey_UsesNamePrefix_WhenIdNull()
    {
        var row = new IngredientUsageRow(1, 1, new DateOnly(2026, 1, 5), "LUNCH", "중식", 100, 100, null, "특수재료", null, null, "kg", "기타", "메뉴");
        Assert.Equal("name:특수재료", IngredientStatisticsService.IngredientKey(row));
    }

    // =======================================================================
    // 통합 계산 (실제 SQLite)
    // =======================================================================

    [Fact]
    public async Task GetAsync_GroupsAndSumsQuantities()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var pork = fixture.AddIngredient("돼지고기", "육류", "kg");
            var beef = fixture.AddIngredient("소고기", "육류", "kg");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110,
                menuItems: [M("돼지고기찌개", [(pork, "돼지고기", 5.0, null, "kg")])]);
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.DINNER, 100,
                menuItems: [M("돼지고기찌개", [(pork, "돼지고기", 3.0, null, "kg")])]);
            fixture.AddService(new DateOnly(2026, 1, 7), MealType.LUNCH, 100,
                menuItems: [M("소고기무국", [(beef, "소고기", null, 10, "kg")])]); // fallback: 10*100/100=10
            fixture.Save();
        }

        var result = await harness.CreateIngredientStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(2, result.Summary.UniqueIngredientCount);
        Assert.Equal(3, result.Summary.TotalUsageCount);

        var porkStat = result.TopIngredients.Single(i => i.IngredientName == "돼지고기");
        Assert.Equal(2, porkStat.UsageCount);
        Assert.Equal(8.0, porkStat.Quantity); // 5 + 3
        Assert.Equal(1, porkStat.LunchCount);
        Assert.Equal(1, porkStat.DinnerCount);
        Assert.Equal("육류", porkStat.StatGroup);

        var beefStat = result.TopIngredients.Single(i => i.IngredientName == "소고기");
        Assert.Equal(10.0, beefStat.Quantity); // fallback 적용
    }

    [Fact]
    public async Task GetAsync_MergesNameBasedRows_WhenIdNull()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100,
                menuItems: [M("특식", [(null, "특수재료", 1.0, null, "kg")])]);
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100,
                menuItems: [M("특식", [(null, "특수재료", 2.0, null, "kg")])]);
            fixture.Save();
        }

        var result = await harness.CreateIngredientStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var group = Assert.Single(result.TopIngredients);
        Assert.Equal("특수재료", group.IngredientName);
        Assert.Null(group.IngredientId);
        Assert.Equal(2, group.UsageCount);
        Assert.Equal(3.0, group.Quantity);
        Assert.Equal("기타", group.StatGroup);
    }

    [Fact]
    public async Task GetAsync_DetectsNewIngredients()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var old = fixture.AddIngredient("기존재료", "육류");
            var fresh = fixture.AddIngredient("신규재료", "채소");
            fixture.AddService(new DateOnly(2025, 12, 1), MealType.LUNCH, 100,
                menuItems: [M("기존메뉴", [(old, "기존재료", 1.0, null, "kg")])]);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100,
                menuItems: [M("기존메뉴", [(old, "기존재료", 1.0, null, "kg")])]);
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100,
                menuItems: [M("신규메뉴", [(fresh, "신규재료", 1.0, null, "kg")])]);
            fixture.Save();
        }

        var result = await harness.CreateIngredientStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(1, result.Summary.NewIngredientCount);
    }

    [Fact]
    public async Task GetAsync_UnusedIngredients_IncludesBoundary()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var used = fixture.AddIngredient("사용중재료", "육류");
            var stale = fixture.AddIngredient("오래된재료", "채소");
            var never = fixture.AddIngredient("미사용재료", "기타");
            var boundary = fixture.AddIngredient("경계재료", "기타");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100,
                menuItems: [M("메뉴", [(used, "사용중재료", 1.0, null, "kg")])]);
            fixture.AddService(new DateOnly(2025, 12, 1), MealType.LUNCH, 100,
                menuItems: [M("메뉴", [(stale, "오래된재료", 1.0, null, "kg")])]);
            fixture.AddService(new DateOnly(2025, 12, 31), MealType.LUNCH, 100,
                menuItems: [M("메뉴", [(boundary, "경계재료", 1.0, null, "kg")])]);
            fixture.Save();
        }

        var result = await harness.CreateIngredientStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31), unusedDays: 90);

        Assert.Equal(2, result.Summary.UnusedIngredientCount);
        Assert.Equal("미사용재료", result.UnusedIngredients[0].IngredientName);
        Assert.Null(result.UnusedIngredients[0].DaysSinceLast);
        Assert.Equal("오래된재료", result.UnusedIngredients[1].IngredientName);
        Assert.Equal(120, result.UnusedIngredients[1].DaysSinceLast);
        Assert.DoesNotContain(result.UnusedIngredients, i => i.IngredientName == "경계재료");
    }

    [Fact]
    public async Task GetAsync_Backdata_UsesResolvedQuantity()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var pork = fixture.AddIngredient("돼지고기", "육류");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 200,
                menuItems: [M("메뉴", [(pork, "돼지고기", null, 10, "kg")])]); // fallback 20
            fixture.AddService(new DateOnly(2026, 1, 12), MealType.LUNCH, 100,
                menuItems: [M("메뉴", [(pork, "돼지고기", 4.0, null, "kg")])]);
            fixture.Save();
        }

        var result = await harness.CreateIngredientStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(2, result.Backdata.Count);
        Assert.Equal(20.0, result.Backdata[0].Quantity);
        Assert.Equal(4.0, result.Backdata[1].Quantity);
        Assert.Equal(new DateOnly(2026, 1, 5), result.Backdata[1].PreviousUsedDate);
        Assert.Equal(7, result.Backdata[1].DaysSincePrevious);
    }

    [Fact]
    public async Task GetDetailAsync_ComputesCoUsedAndHistory()
    {
        int porkId;
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var pork = fixture.AddIngredient("돼지고기", "육류");
            var onion = fixture.AddIngredient("양파", "채소");
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110,
                menuItems: [M("돼지고기찌개", [(pork, "돼지고기", 5.0, null, "kg"), (onion, "양파", 2.0, null, "kg")])]);
            fixture.AddService(new DateOnly(2026, 1, 12), MealType.DINNER, 100,
                menuItems: [M("돼지고기찌개", [(pork, "돼지고기", 3.0, null, "kg"), (onion, "양파", 1.0, null, "kg")])]);
            fixture.Save();
            porkId = fixture.Ingredients[0].Id; // Save 이후 실제 ID
        }

        var detail = await harness.CreateIngredientStatisticsService().GetDetailAsync(
            porkId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.NotNull(detail);
        Assert.Equal("돼지고기", detail!.IngredientName);
        Assert.Equal(2, detail.Summary.UsageCount);
        Assert.Equal(8.0, detail.Summary.Quantity);
        Assert.Equal(1, detail.Summary.LunchCount);
        Assert.Equal(1, detail.Summary.DinnerCount);

        var coUsed = Assert.Single(detail.CoUsed);
        Assert.Equal("양파", coUsed.IngredientName);
        Assert.Equal(2, coUsed.Count);

        Assert.Equal(2, detail.RecentHistory.Count);
        Assert.Equal("돼지고기찌개", detail.RecentHistory[0].MenuName);
        Assert.Equal(5.0, detail.RecentHistory[0].Quantity);
    }

    [Fact]
    public async Task GetAsync_IncludesAnalysisExcludedIngredients()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var excluded = fixture.AddIngredient("분석제외재료", "기타", analysisExcluded: true);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100,
                menuItems: [M("메뉴", [(excluded, "분석제외재료", 1.0, null, "kg")])]);
            fixture.Save();
        }

        // 식재료 통계(스냅샷 기반)는 analysis_excluded를 제외하지 않는다 (Python과 동일).
        var result = await harness.CreateIngredientStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(1, result.Summary.UniqueIngredientCount);
        Assert.Equal("분석제외재료", result.TopIngredients[0].IngredientName);
    }

    private static (Menu? Menu, string MenuName, string Role, (Ingredient? Ingredient, string Name, double? QuantityTotal, double? QuantityPer100, string? Unit)[] Ingredients) M(
        string menuName, (Ingredient? Ingredient, string Name, double? QuantityTotal, double? QuantityPer100, string? Unit)[] ingredients)
        => (null, menuName, "주찬", ingredients);
}

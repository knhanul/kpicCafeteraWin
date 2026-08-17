using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;
using Xunit;

namespace KpicCafeteria.Tests.Statistics;

/// <summary>
/// 대량 데이터 Smoke 테스트.
/// 1년치 배식(중식/석식) + 메뉴/재료 스냅샷을 넣고 통계 계산이 제한 시간 내 완료되는지 확인한다.
/// </summary>
public sealed class StatisticsPerformanceTests
{
    [Fact]
    public async Task AllStatistics_WithOneYearData_CompletesQuickly()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            var menus = Enumerable.Range(1, 30).Select(i => fixture.AddMenu($"메뉴{i}", "주찬")).ToList();
            var ingredients = Enumerable.Range(1, 40)
                .Select(i => fixture.AddIngredient($"재료{i}", i % 3 == 0 ? "채소" : "육류", i % 2 == 0 ? "g" : "kg"))
                .ToList();

            var start = new DateOnly(2025, 1, 1);
            var end = new DateOnly(2025, 12, 31);
            var day = start;
            var counter = 0;
            while (day <= end)
            {
                if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                {
                    day = day.AddDays(1);
                    continue;
                }

                foreach (var mealType in new[] { MealType.LUNCH, MealType.DINNER })
                {
                    var planned = 80 + (counter % 40);
                    var menu = menus[counter % menus.Count];
                    var ingredientsForMeal = ingredients.Skip(counter % 5).Take(4).ToArray();
                    var items = ingredientsForMeal
                        .Select((ing, idx) => (Ingredient: (Ingredient?)ing, Name: ing.Name, QuantityTotal: (double?)(1.0 + idx), QuantityPer100: (double?)null, Unit: ing.DefaultUnit))
                        .ToArray();
                    fixture.AddService(
                        day, mealType, planned,
                        actualCount: counter % 7 == 0 ? null : planned + (counter % 10),
                        mealPlanOutputAt: counter % 3 == 0 ? new DateTime(day.Year, day.Month, day.Day, 9, 0, 0) : null,
                        cookingOutputAt: counter % 4 == 0 ? new DateTime(day.Year, day.Month, day.Day, 7, 0, 0) : null,
                        preservationCompleted: counter % 5 != 0,
                        preservationCollected: counter % 5 != 0,
                        preservationDisposed: counter % 11 == 0,
                        preservationManager: counter % 2 == 0 ? "김주방" : "이조리",
                        preservationTemperature: "-18°C",
                        menuItems: [((Menu?)menu, menu.Name, "주찬", items)]);
                    counter++;
                }

                day = day.AddDays(1);
            }

            fixture.Save();
        }

        var startDate = new DateOnly(2025, 1, 1);
        var endDate = new DateOnly(2025, 12, 31);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var meal = await harness.CreateMealStatisticsService().GetAsync(startDate, endDate);
        var trend = await harness.CreateMealStatisticsService().GetTrendAsync(startDate, endDate);
        var menuStats = await harness.CreateMenuStatisticsService().GetAsync(startDate, endDate);
        var ingredient = await harness.CreateIngredientStatisticsService().GetAsync(startDate, endDate);
        var operations = await harness.CreateOperationsStatisticsService().GetAsync(startDate, endDate);
        var dashboard = await harness.CreateDashboardService().GetAsync(startDate, endDate);

        sw.Stop();

        Assert.True(meal.Summary.ServiceCount > 400, "1년치 평일 중식/석식 배식이 존재해야 한다.");
        Assert.NotEmpty(menuStats.TopMenus);
        Assert.NotEmpty(ingredient.TopIngredients);
        Assert.NotEmpty(operations.Anomalies.RecordGaps);
        Assert.NotEmpty(dashboard.Trend);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(30), $"전체 통계 계산이 30초를 초과했습니다: {sw.Elapsed}");
    }
}

using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;
using Xunit;

namespace KpicCafeteria.Tests.Statistics;

public sealed class OperationsStatisticsServiceTests
{
    [Fact]
    public void IsLateInput_MoreThanOneDay_IsLate()
    {
        var serviceDate = new DateOnly(2026, 1, 5);
        Assert.True(OperationsStatisticsService.IsLateInput(serviceDate, new DateTime(2026, 1, 7, 9, 0, 0)));
        Assert.False(OperationsStatisticsService.IsLateInput(serviceDate, new DateTime(2026, 1, 6, 9, 0, 0))); // 1일 후: 정상
        Assert.False(OperationsStatisticsService.IsLateInput(serviceDate, new DateTime(2026, 1, 5, 18, 0, 0))); // 당일: 정상
        Assert.False(OperationsStatisticsService.IsLateInput(serviceDate, null));
    }

    [Fact]
    public void Rate_ComputesPercent()
    {
        Assert.Equal(50.0, OperationsStatisticsService.Rate(5, 10));
        Assert.Equal(66.7, OperationsStatisticsService.Rate(2, 3));
        Assert.Null(OperationsStatisticsService.Rate(0, 0));
    }

    [Fact]
    public async Task GetAsync_ComputesSummaryAndBreakdown()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110,
                mealPlanOutputAt: new DateTime(2026, 1, 4, 9, 0, 0),
                cookingOutputAt: new DateTime(2026, 1, 5, 7, 0, 0),
                preservationCompleted: true, preservationCollected: true, preservationDisposed: true,
                preservationManager: "김주방", preservationTemperature: "-18°C");
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100, actualCount: 90,
                mealPlanOutputAt: new DateTime(2026, 1, 5, 9, 0, 0));
            fixture.AddService(new DateOnly(2026, 1, 7), MealType.DINNER, 100);
            fixture.Save();
        }

        var result = await harness.CreateOperationsStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(3, result.Summary.ServiceCount);
        Assert.Equal(2, result.Summary.ActualInputCount);
        Assert.Equal(66.7, result.Summary.ActualInputRate);
        Assert.Equal(1, result.Summary.PreservationCount);
        Assert.Equal(33.3, result.Summary.PreservationRate);
        Assert.Equal(2, result.Summary.MealPlanOutputCount);
        Assert.Equal(66.7, result.Summary.MealPlanOutputRate);
        Assert.Equal(1, result.Summary.CookingOutputCount);
        Assert.Equal(33.3, result.Summary.CookingOutputRate);

        var lunch = result.Breakdown["lunch"];
        Assert.Equal(2, lunch.ServiceCount);
        Assert.Equal(2, lunch.ActualInputCount);
        Assert.Equal(1, lunch.PreservationCount);
        Assert.Equal(1, lunch.CookingOutputCount);

        var dinner = result.Breakdown["dinner"];
        Assert.Equal(1, dinner.ServiceCount);
        Assert.Equal(0, dinner.ActualInputCount);
        Assert.Equal(0.0, dinner.ActualInputRate);
    }

    [Fact]
    public async Task GetAsync_DetectsRecordGaps()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            // 완전 누락 배식
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100);
            // 실제 식수만 입력된 배식
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100, actualCount: 90);
            fixture.Save();
        }

        var result = await harness.CreateOperationsStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(7, result.Anomalies.RecordGaps.Count); // 4 + 3
        var types = result.Anomalies.RecordGaps.Select(g => g.Type).Distinct().ToList();
        Assert.Contains("실제 식수 미입력", types);
        Assert.Contains("보존식 기록 미완료", types);
        Assert.Contains("식단표 미출력", types);
        Assert.Contains("조리지시서 미출력", types);

        // 1/5는 4종 누락, 1/6은 보존식/식단표/조리지시서 3종 누락
        Assert.Equal(4, result.Anomalies.RecordGaps.Count(g => g.Date == new DateOnly(2026, 1, 5)));
        Assert.Equal(3, result.Anomalies.RecordGaps.Count(g => g.Date == new DateOnly(2026, 1, 6)));
    }

    [Fact]
    public async Task GetAsync_DetectsLateInputs()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100,
                actualCount: 110, recordedAt: new DateTime(2026, 1, 7, 9, 0, 0)); // 2일 후: 지연
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100,
                actualCount: 90, recordedAt: new DateTime(2026, 1, 6, 18, 0, 0)); // 당일: 정상
            fixture.Save();
        }

        var result = await harness.CreateOperationsStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var late = Assert.Single(result.Anomalies.LateInputs);
        Assert.Equal(new DateOnly(2026, 1, 5), late.Date);
        Assert.Equal(110, late.ActualCount);
        Assert.Equal(new DateTime(2026, 1, 7, 9, 0, 0), late.RecordedAt);
    }

    [Fact]
    public async Task GetAsync_ComputesPreservationSummary()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100,
                preservationCompleted: true, preservationCollected: true, preservationDisposed: true,
                preservationManager: "김주방", preservationTemperature: "-18°C");
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100,
                preservationCompleted: true, preservationCollected: true,
                preservationManager: "김주방", preservationTemperature: "-20°C");
            fixture.AddService(new DateOnly(2026, 1, 7), MealType.DINNER, 100,
                preservationCompleted: true, preservationCollected: true,
                preservationManager: "이조리", preservationTemperature: "-19°C");
            fixture.Save();
        }

        var result = await harness.CreateOperationsStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(3, result.Preservation.CollectedCount);
        Assert.Equal(100.0, result.Preservation.CollectedRate);
        Assert.Equal(1, result.Preservation.DisposedCount);
        Assert.Equal(33.3, result.Preservation.DisposedRate);

        Assert.Equal(2, result.Preservation.ByManager.Count);
        Assert.Equal("김주방", result.Preservation.ByManager[0].ManagerName);
        Assert.Equal(2, result.Preservation.ByManager[0].Count);

        Assert.Equal(3, result.Preservation.TemperatureRecords.Count);
        Assert.Equal("-18°C", result.Preservation.TemperatureRecords[0].Temperature);
    }

    [Fact]
    public async Task GetAsync_ComputesMonthlyTrend()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110,
                mealPlanOutputAt: new DateTime(2026, 1, 4, 9, 0, 0));
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100);
            fixture.AddService(new DateOnly(2026, 2, 2), MealType.LUNCH, 100, actualCount: 90,
                mealPlanOutputAt: new DateTime(2026, 2, 1, 9, 0, 0),
                cookingOutputAt: new DateTime(2026, 2, 2, 7, 0, 0));
            fixture.Save();
        }

        var result = await harness.CreateOperationsStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28));

        Assert.Equal(2, result.Trend.Count);
        Assert.Equal("2026-01", result.Trend[0].Month);
        Assert.Equal(2, result.Trend[0].ServiceCount);
        Assert.Equal(50.0, result.Trend[0].ActualInputRate);
        Assert.Equal(50.0, result.Trend[0].MealPlanOutputRate);
        Assert.Equal(0.0, result.Trend[0].CookingOutputRate);

        Assert.Equal("2026-02", result.Trend[1].Month);
        Assert.Equal(1, result.Trend[1].ServiceCount);
        Assert.Equal(100.0, result.Trend[1].ActualInputRate);
        Assert.Equal(100.0, result.Trend[1].CookingOutputRate);
    }

    [Fact]
    public async Task GetAsync_MealTypeFilter_Applies()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.DINNER, 100);
            fixture.Save();
        }

        var result = await harness.CreateOperationsStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "dinner");

        Assert.Equal(1, result.Summary.ServiceCount);
        Assert.Equal(0, result.Summary.ActualInputCount);
        Assert.Equal(0, result.Breakdown["lunch"].ServiceCount);
    }

    [Fact]
    public async Task GetAsync_Backdata_ContainsAllFields()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110,
                recordedAt: new DateTime(2026, 1, 5, 12, 0, 0),
                mealPlanOutputAt: new DateTime(2026, 1, 4, 9, 0, 0),
                cookingOutputAt: new DateTime(2026, 1, 5, 7, 0, 0),
                preservationCompleted: true, preservationCollected: true, preservationDisposed: true,
                preservationManager: "김주방", preservationTemperature: "-18°C");
            fixture.Save();
        }

        var result = await harness.CreateOperationsStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var row = Assert.Single(result.Backdata);
        Assert.Equal(new DateOnly(2026, 1, 5), row.Date);
        Assert.Equal("월요일", row.Weekday);
        Assert.Equal("LUNCH", row.MealType);
        Assert.Equal("중식", row.MealTypeName);
        Assert.Equal(100, row.PlannedCount);
        Assert.Equal(110, row.ActualCount);
        Assert.True(row.ActualInput);
        Assert.False(row.ActualLate);
        Assert.True(row.MealPlanOutput);
        Assert.True(row.CookingOutput);
        Assert.True(row.PreservationCompleted);
        Assert.True(row.PreservationCollected);
        Assert.True(row.PreservationDisposed);
        Assert.Equal("김주방", row.PreservationManager);
        Assert.Equal("-18°C", row.PreservationTemperature);
    }
}

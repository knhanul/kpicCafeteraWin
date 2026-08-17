using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;
using Xunit;

namespace KpicCafeteria.Tests.Statistics;

public sealed class MealStatisticsServiceTests
{
    // =======================================================================
    // 순수 계산 규칙 (Python statistics_service.py와 동일)
    // =======================================================================

    [Fact]
    public void Median_OddCount_ReturnsMiddleValue()
    {
        Assert.Equal(2, MealStatisticsService.Median([1, 2, 3]));
        Assert.Equal(30, MealStatisticsService.Median([10, 20, 30, 40, 50]));
    }

    [Fact]
    public void Median_EvenCount_ReturnsAverageOfTwoMiddleValues()
    {
        Assert.Equal(2.5, MealStatisticsService.Median([1, 2, 3, 4]));
        Assert.Equal(25.0, MealStatisticsService.Median([10, 20, 30, 40]));
    }

    [Fact]
    public void DeviationRate_ComputesPercent()
    {
        Assert.Equal(10.0, MealStatisticsService.DeviationRate(110, 100));
        Assert.Equal(-10.0, MealStatisticsService.DeviationRate(90, 100));
        Assert.Equal(5.0, MealStatisticsService.DeviationRate(105, 100));
        Assert.Equal(0.0, MealStatisticsService.DeviationRate(100, 100));
    }

    [Fact]
    public void DeviationRate_NullOrInvalidBase_ReturnsNull()
    {
        Assert.Null(MealStatisticsService.DeviationRate(null, 100));
        Assert.Null(MealStatisticsService.DeviationRate(100, null));
        Assert.Null(MealStatisticsService.DeviationRate(100, 0));
        Assert.Null(MealStatisticsService.DeviationRate(100, -5));
    }

    [Fact]
    public void UsualMedian_IncludesCutoffExcludesServiceDate()
    {
        // serviceDate = 2026-01-10, cutoff = 2025-11-15 (56일 전, 경계 포함)
        var history = new List<ActualHistoryRow>
        {
            new(new DateOnly(2025, 11, 15), "LUNCH", 100), // 경계: 포함
            new(new DateOnly(2025, 11, 16), "LUNCH", 110),
            new(new DateOnly(2025, 11, 17), "LUNCH", 120),
            new(new DateOnly(2026, 1, 9), "LUNCH", 130), // serviceDate 이전: 포함
            new(new DateOnly(2026, 1, 10), "LUNCH", 999), // serviceDate 당일: 제외
            new(new DateOnly(2026, 1, 11), "LUNCH", 999), // 이후: 제외
            new(new DateOnly(2025, 11, 14), "LUNCH", 999), // cutoff 이전: 제외
            new(new DateOnly(2025, 11, 16), "DINNER", 999), // 다른 MealType: 제외
        };

        var (median, count) = MealStatisticsService.UsualMedian(history, new DateOnly(2026, 1, 10), "LUNCH");

        Assert.Equal(4, count);
        Assert.Equal(115.0, median); // [100, 110, 120, 130] 중앙값
    }

    [Fact]
    public void UsualMedian_FewerThanMinComparison_ReturnsNull()
    {
        var history = new List<ActualHistoryRow>
        {
            new(new DateOnly(2025, 11, 15), "LUNCH", 100),
            new(new DateOnly(2025, 11, 16), "LUNCH", 110),
            new(new DateOnly(2025, 11, 17), "LUNCH", 120),
        };

        var (median, count) = MealStatisticsService.UsualMedian(history, new DateOnly(2026, 1, 10), "LUNCH");

        Assert.Null(median);
        Assert.Equal(3, count);
    }

    // =======================================================================
    // 통합 계산 (실제 SQLite)
    // =======================================================================

    [Fact]
    public async Task GetAsync_ComputesSummaryAndBreakdown()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110);
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100, actualCount: 90);
            fixture.AddService(new DateOnly(2026, 1, 7), MealType.DINNER, 100);
            fixture.Save();
        }

        var result = await harness.CreateMealStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(3, result.Summary.ServiceCount);
        Assert.Equal(2, result.Summary.InputCount);
        Assert.Equal(66.7, result.Summary.InputRate);
        Assert.Equal(300, result.Summary.PlannedSum);
        Assert.Equal(200, result.Summary.ActualSum);
        Assert.Equal(-100, result.Summary.Diff);
        Assert.Equal(-33.3, result.Summary.DeviationRate);

        var lunch = result.Breakdown["lunch"];
        Assert.Equal("중식", lunch.MealTypeName);
        Assert.Equal(2, lunch.ServiceCount);
        Assert.Equal(200, lunch.PlannedSum);
        Assert.Equal(200, lunch.ActualSum);
        Assert.Equal(0, lunch.Diff);
        Assert.Equal(0.0, lunch.DeviationRate);
        Assert.Equal(100.0, lunch.InputRate);

        var dinner = result.Breakdown["dinner"];
        Assert.Equal(1, dinner.ServiceCount);
        Assert.Equal(100, dinner.PlannedSum);
        Assert.Null(dinner.ActualSum);
        Assert.Null(dinner.Diff);
        Assert.Null(dinner.DeviationRate);
        Assert.Equal(0.0, dinner.InputRate);
    }

    [Fact]
    public async Task GetAsync_ComputesWeekdayAverages()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110); // 월
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100, actualCount: 90); // 화
            fixture.AddService(new DateOnly(2026, 1, 7), MealType.LUNCH, 100); // 수 (실제 미입력)
            fixture.Save();
        }

        var result = await harness.CreateMealStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(5, result.WeekdayAverages.Count);
        Assert.Equal("월요일", result.WeekdayAverages[0].Weekday);
        Assert.Equal(100.0, result.WeekdayAverages[0].PlannedAverage);
        Assert.Equal(110.0, result.WeekdayAverages[0].ActualAverage);
        Assert.Equal(1, result.WeekdayAverages[0].Records);
        Assert.Equal(1, result.WeekdayAverages[0].ActualRecords);

        Assert.Equal(100.0, result.WeekdayAverages[1].PlannedAverage);
        Assert.Equal(90.0, result.WeekdayAverages[1].ActualAverage);

        Assert.Equal(100.0, result.WeekdayAverages[2].PlannedAverage);
        Assert.Null(result.WeekdayAverages[2].ActualAverage);
        Assert.Equal(0, result.WeekdayAverages[2].ActualRecords);

        Assert.Null(result.WeekdayAverages[3].PlannedAverage);
        Assert.Null(result.WeekdayAverages[3].ActualAverage);
        Assert.Equal(0, result.WeekdayAverages[3].Records);
    }

    [Fact]
    public async Task GetAsync_DetectsAnomaliesWithLevels()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 130); // +30% 중요 급증
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100, actualCount: 88); // -12% 확인 급감
            fixture.AddService(new DateOnly(2026, 1, 7), MealType.LUNCH, 100, actualCount: 95); // -5% 정상
            fixture.Save();
        }

        var result = await harness.CreateMealStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(2, result.Anomalies.Count);
        // 날짜 내림차순 정렬: 1/6 먼저
        Assert.Equal(new DateOnly(2026, 1, 6), result.Anomalies[0].Date);
        Assert.Equal("식수 급감", result.Anomalies[0].Type);
        Assert.Equal("확인", result.Anomalies[0].Level);
        Assert.Equal(-12.0, result.Anomalies[0].DeviationRate);
        Assert.True(result.Anomalies[0].InsufficientComparison); // 평소 비교 데이터 없음

        Assert.Equal(new DateOnly(2026, 1, 5), result.Anomalies[1].Date);
        Assert.Equal("식수 급증", result.Anomalies[1].Type);
        Assert.Equal("중요", result.Anomalies[1].Level);
        Assert.Single(result.Anomalies[1].Reasons);
        Assert.Equal("계획 대비", result.Anomalies[1].Reasons[0].Basis);
        Assert.Equal(30.0, result.Anomalies[1].Reasons[0].Value);
    }

    [Fact]
    public async Task GetAsync_UsualMedianReason_WhenHistoryExists()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            // 평소 이력: 56일 이내 4건 (중앙값 100)
            fixture.AddService(new DateOnly(2025, 11, 15), MealType.LUNCH, 100, actualCount: 100);
            fixture.AddService(new DateOnly(2025, 11, 16), MealType.LUNCH, 100, actualCount: 100);
            fixture.AddService(new DateOnly(2025, 11, 17), MealType.LUNCH, 100, actualCount: 100);
            fixture.AddService(new DateOnly(2025, 11, 18), MealType.LUNCH, 100, actualCount: 100);
            // 대상: 평소 대비 +20% (중요)
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 120);
            fixture.Save();
        }

        var result = await harness.CreateMealStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var anomaly = Assert.Single(result.Anomalies);
        Assert.Equal(100.0, anomaly.UsualMedian);
        Assert.Equal(4, anomaly.UsualCount);
        Assert.Equal(20.0, anomaly.UsualDeviationRate);
        Assert.False(anomaly.InsufficientComparison);
        Assert.Equal(2, anomaly.Reasons.Count); // 계획 대비 +20, 평소 대비 +20
        Assert.Equal("중요", anomaly.Level);
    }

    [Fact]
    public async Task GetTrendAsync_AggregatesByMonth()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110);
            fixture.AddService(new DateOnly(2026, 1, 6), MealType.LUNCH, 100, actualCount: 90);
            fixture.AddService(new DateOnly(2026, 2, 2), MealType.LUNCH, 100);
            fixture.Save();
        }

        var result = await harness.CreateMealStatisticsService().GetTrendAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 28));

        Assert.Equal(2, result.Trend.Count);
        Assert.Equal("2026-01", result.Trend[0].Month);
        Assert.Equal(200, result.Trend[0].Planned);
        Assert.Equal(200, result.Trend[0].Actual);
        Assert.Equal(2, result.Trend[0].PlannedDays);
        Assert.Equal(2, result.Trend[0].ActualDays);

        Assert.Equal("2026-02", result.Trend[1].Month);
        Assert.Equal(100, result.Trend[1].Planned);
        Assert.Equal(0, result.Trend[1].Actual);
        Assert.Equal(1, result.Trend[1].PlannedDays);
        Assert.Equal(0, result.Trend[1].ActualDays);
    }

    [Fact]
    public async Task GetAsync_MealTypeFilter_Applies()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 110);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.DINNER, 100, actualCount: 90);
            fixture.Save();
        }

        var service = harness.CreateMealStatisticsService();
        var lunch = await service.GetAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "lunch");
        Assert.Equal(1, lunch.Summary.ServiceCount);
        Assert.Equal(110, lunch.Summary.ActualSum);
        Assert.Equal(0, lunch.Breakdown["dinner"].ServiceCount);

        var dinner = await service.GetAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), "dinner");
        Assert.Equal(1, dinner.Summary.ServiceCount);
        Assert.Equal(90, dinner.Summary.ActualSum);
    }

    [Fact]
    public async Task GetAsync_EmptyPeriod_ReturnsZeroSummary()
    {
        using var harness = new StatisticsTestHarness();

        var result = await harness.CreateMealStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        Assert.Equal(0, result.Summary.ServiceCount);
        Assert.Equal(0, result.Summary.PlannedSum);
        Assert.Null(result.Summary.ActualSum);
        Assert.Null(result.Summary.InputRate);
        Assert.Empty(result.Anomalies);
        Assert.Empty(result.Backdata);
    }

    [Fact]
    public async Task GetAsync_BackdataContainsUsualMedian()
    {
        using var harness = new StatisticsTestHarness();
        using (var db = harness.CreateContext())
        {
            var fixture = new StatisticsFixture(db);
            fixture.AddService(new DateOnly(2025, 11, 15), MealType.LUNCH, 100, actualCount: 100);
            fixture.AddService(new DateOnly(2025, 11, 16), MealType.LUNCH, 100, actualCount: 100);
            fixture.AddService(new DateOnly(2025, 11, 17), MealType.LUNCH, 100, actualCount: 100);
            fixture.AddService(new DateOnly(2025, 11, 18), MealType.LUNCH, 100, actualCount: 100);
            fixture.AddService(new DateOnly(2026, 1, 5), MealType.LUNCH, 100, actualCount: 120);
            fixture.Save();
        }

        var result = await harness.CreateMealStatisticsService().GetAsync(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));

        var row = Assert.Single(result.Backdata);
        Assert.Equal(new DateOnly(2026, 1, 5), row.Date);
        Assert.Equal("월요일", row.Weekday);
        Assert.Equal("중식", row.MealTypeName);
        Assert.Equal(100.0, row.UsualMedian);
        Assert.Equal(20.0, row.UsualDeviationRate);
        Assert.True(row.Input);
    }
}

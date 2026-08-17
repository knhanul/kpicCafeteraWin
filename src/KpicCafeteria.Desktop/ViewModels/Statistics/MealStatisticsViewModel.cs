using CommunityToolkit.Mvvm.ComponentModel;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels.Statistics;

/// <summary>
/// 식수 통계 ViewModel.
/// 기존 statistics_service.py meal_statistics / meal_trend 출력을 표시한다.
/// </summary>
public sealed partial class MealStatisticsViewModel : StatisticsViewModelBase
{
    private readonly MealStatisticsService _service;

    public MealStatisticsViewModel(MealStatisticsService service, IMessageService messages, ILogger<MealStatisticsViewModel> logger)
        : base(messages, logger)
    {
        _service = service;
    }

    // ---- KPI ----

    [ObservableProperty]
    private string serviceCount = "-";

    [ObservableProperty]
    private string plannedSum = "-";

    [ObservableProperty]
    private string actualSum = "-";

    [ObservableProperty]
    private string inputRate = "-";

    [ObservableProperty]
    private string deviationRate = "-";

    [ObservableProperty]
    private string lunchSummary = "-";

    [ObservableProperty]
    private string dinnerSummary = "-";

    // ---- 요일별 평균 ----

    [ObservableProperty]
    private IReadOnlyList<WeekdayAverageDto> weekdayAverages = [];

    // ---- 추세 ----

    [ObservableProperty]
    private IReadOnlyList<BarChartItem> plannedTrend = [];

    [ObservableProperty]
    private IReadOnlyList<BarChartItem> actualTrend = [];

    // ---- 이상치 / 백데이터 ----

    [ObservableProperty]
    private IReadOnlyList<MealAnomalyDto> anomalies = [];

    [ObservableProperty]
    private IReadOnlyList<MealBackdataRowDto> backdata = [];

    [ObservableProperty]
    private string backdataFilter = string.Empty;

    public IReadOnlyList<MealBackdataRowDto> FilteredBackdata
        => string.IsNullOrWhiteSpace(BackdataFilter)
            ? Backdata
            : Backdata.Where(r =>
                r.Date.ToString("yyyy-MM-dd").Contains(BackdataFilter, StringComparison.OrdinalIgnoreCase)
                || r.MealTypeName.Contains(BackdataFilter, StringComparison.OrdinalIgnoreCase)
                || r.Weekday.Contains(BackdataFilter, StringComparison.OrdinalIgnoreCase)).ToList();

    partial void OnBackdataFilterChanged(string value) => OnPropertyChanged(nameof(FilteredBackdata));

    protected override async Task LoadCoreAsync(DateOnly start, DateOnly end, string mealType)
    {
        var result = await _service.GetAsync(start, end, mealType);
        var trend = await _service.GetTrendAsync(start, end, mealType);

        ServiceCount = StatisticsFormat.Number(result.Summary.ServiceCount);
        PlannedSum = StatisticsFormat.Number(result.Summary.PlannedSum);
        ActualSum = StatisticsFormat.Number(result.Summary.ActualSum);
        InputRate = StatisticsFormat.Percent(result.Summary.InputRate);
        DeviationRate = StatisticsFormat.SignedPercent(result.Summary.DeviationRate);
        LunchSummary = Summarize(result.Breakdown.GetValueOrDefault("lunch"));
        DinnerSummary = Summarize(result.Breakdown.GetValueOrDefault("dinner"));

        WeekdayAverages = result.WeekdayAverages;

        PlannedTrend = trend.Trend.Select(t => new BarChartItem(
            t.Month, t.Planned, StatisticsFormat.Number(t.Planned), "#9DA3AB")).ToList();
        ActualTrend = trend.Trend.Select(t => new BarChartItem(
            t.Month, t.Actual, StatisticsFormat.Number(t.Actual), "#2563EB")).ToList();

        Anomalies = result.Anomalies;
        Backdata = result.Backdata;
        OnPropertyChanged(nameof(FilteredBackdata));
    }

    private static string Summarize(MealTypeBreakdownDto? breakdown)
        => breakdown is null
            ? "-"
            : $"{breakdown.MealTypeName} {StatisticsFormat.Number(breakdown.ActualSum)}명 / 계획 {StatisticsFormat.Number(breakdown.PlannedSum)}명 ({StatisticsFormat.SignedPercent(breakdown.DeviationRate)})";
}

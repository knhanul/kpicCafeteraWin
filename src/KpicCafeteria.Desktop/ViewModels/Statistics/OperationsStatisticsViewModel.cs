using CommunityToolkit.Mvvm.ComponentModel;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels.Statistics;

/// <summary>
/// 운영 기록 통계 ViewModel.
/// 기존 operations_statistics.py 출력을 표시한다.
/// </summary>
public sealed partial class OperationsStatisticsViewModel : StatisticsViewModelBase
{
    private readonly OperationsStatisticsService _service;

    public OperationsStatisticsViewModel(OperationsStatisticsService service, IMessageService messages, ILogger<OperationsStatisticsViewModel> logger)
        : base(messages, logger)
    {
        _service = service;
    }

    // ---- KPI ----

    [ObservableProperty]
    private string serviceCount = "-";

    [ObservableProperty]
    private string actualInputRate = "-";

    [ObservableProperty]
    private string preservationRate = "-";

    [ObservableProperty]
    private string mealPlanOutputRate = "-";

    [ObservableProperty]
    private string cookingOutputRate = "-";

    [ObservableProperty]
    private string lunchSummary = "-";

    [ObservableProperty]
    private string dinnerSummary = "-";

    // ---- 추세 ----

    [ObservableProperty]
    private IReadOnlyList<BarChartItem> actualInputTrend = [];

    [ObservableProperty]
    private IReadOnlyList<BarChartItem> preservationTrend = [];

    [ObservableProperty]
    private IReadOnlyList<BarChartItem> cookingOutputTrend = [];

    // ---- 이상징후 ----

    [ObservableProperty]
    private IReadOnlyList<RecordGapDto> recordGaps = [];

    [ObservableProperty]
    private IReadOnlyList<LateInputDto> lateInputs = [];

    // ---- 보존식 분석 ----

    [ObservableProperty]
    private string preservationSummary = "-";

    [ObservableProperty]
    private IReadOnlyList<ManagerCountDto> preservationByManager = [];

    [ObservableProperty]
    private IReadOnlyList<TemperatureRecordDto> temperatureRecords = [];

    // ---- 백데이터 ----

    [ObservableProperty]
    private IReadOnlyList<OperationsBackdataRowDto> backdata = [];

    [ObservableProperty]
    private string backdataFilter = string.Empty;

    public IReadOnlyList<OperationsBackdataRowDto> FilteredBackdata
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

        ServiceCount = StatisticsFormat.Number(result.Summary.ServiceCount);
        ActualInputRate = StatisticsFormat.Percent(result.Summary.ActualInputRate);
        PreservationRate = StatisticsFormat.Percent(result.Summary.PreservationRate);
        MealPlanOutputRate = StatisticsFormat.Percent(result.Summary.MealPlanOutputRate);
        CookingOutputRate = StatisticsFormat.Percent(result.Summary.CookingOutputRate);
        LunchSummary = Summarize(result.Breakdown.GetValueOrDefault("lunch"));
        DinnerSummary = Summarize(result.Breakdown.GetValueOrDefault("dinner"));

        ActualInputTrend = result.Trend.Select(t => new BarChartItem(
            t.Month, t.ActualInputRate ?? 0, StatisticsFormat.Percent(t.ActualInputRate), "#2563EB")).ToList();
        PreservationTrend = result.Trend.Select(t => new BarChartItem(
            t.Month, t.PreservationRate ?? 0, StatisticsFormat.Percent(t.PreservationRate), "#16A34A")).ToList();
        CookingOutputTrend = result.Trend.Select(t => new BarChartItem(
            t.Month, t.CookingOutputRate ?? 0, StatisticsFormat.Percent(t.CookingOutputRate), "#D97706")).ToList();

        RecordGaps = result.Anomalies.RecordGaps;
        LateInputs = result.Anomalies.LateInputs;

        PreservationSummary =
            $"수거 {result.Preservation.CollectedCount}건 ({StatisticsFormat.Percent(result.Preservation.CollectedRate)}) · " +
            $"폐기 {result.Preservation.DisposedCount}건 ({StatisticsFormat.Percent(result.Preservation.DisposedRate)})";
        PreservationByManager = result.Preservation.ByManager;
        TemperatureRecords = result.Preservation.TemperatureRecords;

        Backdata = result.Backdata;
        OnPropertyChanged(nameof(FilteredBackdata));
    }

    private static string Summarize(OperationsBreakdownDto? breakdown)
        => breakdown is null
            ? "-"
            : $"{breakdown.MealTypeName} {breakdown.ServiceCount}건 · 실제 {StatisticsFormat.Percent(breakdown.ActualInputRate)} · 보존식 {StatisticsFormat.Percent(breakdown.PreservationRate)} · 조리지시서 {StatisticsFormat.Percent(breakdown.CookingOutputRate)}";
}

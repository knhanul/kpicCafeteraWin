using CommunityToolkit.Mvvm.ComponentModel;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels.Statistics;

/// <summary>
/// 운영 대시보드 ViewModel.
/// 기존 dashboard_service.py operations_dashboard 출력을 표시한다.
/// </summary>
public sealed partial class DashboardViewModel : StatisticsViewModelBase
{
    private readonly DashboardService _dashboard;

    public DashboardViewModel(DashboardService dashboard, IMessageService messages, ILogger<DashboardViewModel> logger)
        : base(messages, logger)
    {
        _dashboard = dashboard;
    }

    // ---- KPI ----

    [ObservableProperty]
    private int operatingDays;

    [ObservableProperty]
    private int uniqueMenuCount;

    [ObservableProperty]
    private string lunchSummary = "-";

    [ObservableProperty]
    private string dinnerSummary = "-";

    // ---- 추세 ----

    [ObservableProperty]
    private IReadOnlyList<BarChartItem> plannedTrend = [];

    [ObservableProperty]
    private IReadOnlyList<BarChartItem> actualTrend = [];

    // ---- 이상징후 ----

    [ObservableProperty]
    private IReadOnlyList<MealAnomalyDto> mealAnomalies = [];

    [ObservableProperty]
    private IReadOnlyList<MenuRepeatDto> menuRepeats = [];

    [ObservableProperty]
    private IReadOnlyList<IngredientChangeDto> ingredientChanges = [];

    [ObservableProperty]
    private IReadOnlyList<RecordGapDto> recordGaps = [];

    // ---- 요약 ----

    [ObservableProperty]
    private IReadOnlyList<MenuUsageDto> menuUsage = [];

    [ObservableProperty]
    private IReadOnlyList<RepeatedMenuDto> repeatedMenus = [];

    [ObservableProperty]
    private IReadOnlyList<IngredientGroupDto> ingredientGroups = [];

    [ObservableProperty]
    private string workflowText = "-";

    protected override async Task LoadCoreAsync(DateOnly start, DateOnly end, string mealType)
    {
        var result = await _dashboard.GetAsync(start, end, mealType);

        OperatingDays = result.Kpis.OperatingDays;
        UniqueMenuCount = result.Kpis.UniqueMenuCount;
        LunchSummary = Summarize(result.Kpis.Lunch);
        DinnerSummary = Summarize(result.Kpis.Dinner);

        PlannedTrend = result.Trend.Select(t => new BarChartItem(
            t.Month, t.Planned, StatisticsFormat.Number(t.Planned), "#9DA3AB")).ToList();
        ActualTrend = result.Trend.Select(t => new BarChartItem(
            t.Month, t.Actual, StatisticsFormat.Number(t.Actual), "#2563EB")).ToList();

        MealAnomalies = result.Anomalies.Meal;
        MenuRepeats = result.Anomalies.MenuRepeats;
        IngredientChanges = result.Anomalies.IngredientChanges;
        RecordGaps = result.Anomalies.RecordGaps;

        MenuUsage = result.MenuUsage;
        RepeatedMenus = result.RepeatedMenus;
        IngredientGroups = result.IngredientGroups;
        WorkflowText = $"조리지시서 {result.Workflow.CookingOutput}건 · 보존식 {result.Workflow.PreservationCompleted}건 · 실제식수 입력 {result.Workflow.ActualRecorded}건";
    }

    private static string Summarize(MealTypeBreakdownDto? breakdown)
        => breakdown is null
            ? "-"
            : $"{breakdown.MealTypeName} {StatisticsFormat.Number(breakdown.ActualSum)}명 / 계획 {StatisticsFormat.Number(breakdown.PlannedSum)}명 ({StatisticsFormat.SignedPercent(breakdown.DeviationRate)})";
}

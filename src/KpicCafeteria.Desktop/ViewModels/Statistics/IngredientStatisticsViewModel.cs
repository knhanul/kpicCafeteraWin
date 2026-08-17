using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels.Statistics;

/// <summary>
/// 식재료 통계 ViewModel.
/// 기존 ingredient_statistics.py ingredient_statistics / ingredient_detail 출력을 표시한다.
/// 상세 정보는 같은 화면의 오른쪽 패널에서 확인한다.
/// </summary>
public sealed partial class IngredientStatisticsViewModel : StatisticsViewModelBase
{
    private readonly IngredientStatisticsService _service;

    public IngredientStatisticsViewModel(IngredientStatisticsService service, IMessageService messages, ILogger<IngredientStatisticsViewModel> logger)
        : base(messages, logger)
    {
        _service = service;
        ShowDetailCommand = new AsyncRelayCommand<IngredientTopDto?>(ShowDetailAsync);
        CloseDetailCommand = new RelayCommand(CloseDetail);
    }

    // ---- KPI ----

    [ObservableProperty]
    private string uniqueIngredientCount = "-";

    [ObservableProperty]
    private string totalUsageCount = "-";

    [ObservableProperty]
    private string newIngredientCount = "-";

    [ObservableProperty]
    private string unusedIngredientCount = "-";

    // ---- 목록 ----

    [ObservableProperty]
    private IReadOnlyList<IngredientTopDto> topIngredients = [];

    [ObservableProperty]
    private IReadOnlyList<UnusedIngredientDto> unusedIngredients = [];

    [ObservableProperty]
    private IReadOnlyList<IngredientUsageBackdataRowDto> backdata = [];

    [ObservableProperty]
    private string backdataFilter = string.Empty;

    public IReadOnlyList<IngredientUsageBackdataRowDto> FilteredBackdata
        => string.IsNullOrWhiteSpace(BackdataFilter)
            ? Backdata
            : Backdata.Where(r =>
                r.Date.ToString("yyyy-MM-dd").Contains(BackdataFilter, StringComparison.OrdinalIgnoreCase)
                || r.IngredientName.Contains(BackdataFilter, StringComparison.OrdinalIgnoreCase)
                || r.MealTypeName.Contains(BackdataFilter, StringComparison.OrdinalIgnoreCase)).ToList();

    partial void OnBackdataFilterChanged(string value) => OnPropertyChanged(nameof(FilteredBackdata));

    // ---- 상세 패널 ----

    [ObservableProperty]
    private bool isDetailVisible;

    [ObservableProperty]
    private string detailTitle = string.Empty;

    [ObservableProperty]
    private string detailSummary = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<MonthlyUsageDto> detailMonthlyUsage = [];

    [ObservableProperty]
    private IReadOnlyList<IngredientRecentHistoryDto> detailRecentHistory = [];

    [ObservableProperty]
    private IReadOnlyList<CoUsedIngredientDto> detailCoUsed = [];

    [ObservableProperty]
    private IReadOnlyList<IngredientUsageBackdataRowDto> detailBackdata = [];

    public IAsyncRelayCommand<IngredientTopDto?> ShowDetailCommand { get; }

    public IRelayCommand CloseDetailCommand { get; }

    protected override async Task LoadCoreAsync(DateOnly start, DateOnly end, string mealType)
    {
        var result = await _service.GetAsync(start, end, mealType, unusedDays: 90);

        UniqueIngredientCount = StatisticsFormat.Number(result.Summary.UniqueIngredientCount);
        TotalUsageCount = StatisticsFormat.Number(result.Summary.TotalUsageCount);
        NewIngredientCount = StatisticsFormat.Number(result.Summary.NewIngredientCount);
        UnusedIngredientCount = StatisticsFormat.Number(result.Summary.UnusedIngredientCount);

        TopIngredients = result.TopIngredients;
        UnusedIngredients = result.UnusedIngredients;
        Backdata = result.Backdata;
        OnPropertyChanged(nameof(FilteredBackdata));

        CloseDetail();
    }

    private async Task ShowDetailAsync(IngredientTopDto? ingredient)
    {
        if (ingredient?.IngredientId is null)
        {
            return;
        }

        var (start, end) = Period.Resolve();
        var detail = await _service.GetDetailAsync(ingredient.IngredientId.Value, start, end, SelectedMealType);
        if (detail is null)
        {
            return;
        }

        DetailTitle = $"{detail.IngredientName} ({detail.StatGroup})";
        DetailSummary =
            $"사용 {detail.Summary.UsageCount}회 · 중식 {detail.Summary.LunchCount}회 · 석식 {detail.Summary.DinnerCount}회 · " +
            $"사용량 {StatisticsFormat.Number(detail.Summary.Quantity)} · " +
            $"첫 사용 {StatisticsFormat.Date(detail.Summary.FirstUsed)} · 최근 {StatisticsFormat.Date(detail.Summary.LastUsed)} · " +
            $"평균 간격 {StatisticsFormat.Number(detail.Summary.AvgInterval)}일";
        DetailMonthlyUsage = detail.MonthlyUsage;
        DetailRecentHistory = detail.RecentHistory;
        DetailCoUsed = detail.CoUsed;
        DetailBackdata = detail.Backdata;
        IsDetailVisible = true;
    }

    private void CloseDetail()
    {
        IsDetailVisible = false;
        DetailTitle = string.Empty;
        DetailSummary = string.Empty;
        DetailMonthlyUsage = [];
        DetailRecentHistory = [];
        DetailCoUsed = [];
        DetailBackdata = [];
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels.Statistics;

/// <summary>
/// 메뉴 통계 ViewModel.
/// 기존 menu_statistics.py menu_statistics / menu_detail 출력을 표시한다.
/// 상세 정보는 같은 화면의 오른쪽 패널에서 확인한다.
/// </summary>
public sealed partial class MenuStatisticsViewModel : StatisticsViewModelBase
{
    private readonly MenuStatisticsService _service;

    public MenuStatisticsViewModel(MenuStatisticsService service, IMessageService messages, ILogger<MenuStatisticsViewModel> logger)
        : base(messages, logger)
    {
        _service = service;
        ShowDetailCommand = new AsyncRelayCommand<MenuTopDto?>(ShowDetailAsync);
        CloseDetailCommand = new RelayCommand(CloseDetail);
    }

    // ---- KPI ----

    [ObservableProperty]
    private string uniqueMenuCount = "-";

    [ObservableProperty]
    private string totalUsageCount = "-";

    [ObservableProperty]
    private string newMenuCount = "-";

    [ObservableProperty]
    private string repeatMenuCount = "-";

    [ObservableProperty]
    private string unusedMenuCount = "-";

    // ---- 목록 ----

    [ObservableProperty]
    private IReadOnlyList<MenuTopDto> topMenus = [];

    [ObservableProperty]
    private IReadOnlyList<MenuRepeatDto> repeats = [];

    [ObservableProperty]
    private IReadOnlyList<UnusedMenuDto> unusedMenus = [];

    [ObservableProperty]
    private IReadOnlyList<MenuUsageBackdataRowDto> backdata = [];

    [ObservableProperty]
    private string backdataFilter = string.Empty;

    public IReadOnlyList<MenuUsageBackdataRowDto> FilteredBackdata
        => string.IsNullOrWhiteSpace(BackdataFilter)
            ? Backdata
            : Backdata.Where(r =>
                r.Date.ToString("yyyy-MM-dd").Contains(BackdataFilter, StringComparison.OrdinalIgnoreCase)
                || r.MenuName.Contains(BackdataFilter, StringComparison.OrdinalIgnoreCase)
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
    private IReadOnlyList<MenuRecentHistoryDto> detailRecentHistory = [];

    [ObservableProperty]
    private IReadOnlyList<CoUsedMenuDto> detailCoUsed = [];

    [ObservableProperty]
    private IReadOnlyList<MenuUsageBackdataRowDto> detailBackdata = [];

    public IAsyncRelayCommand<MenuTopDto?> ShowDetailCommand { get; }

    public IRelayCommand CloseDetailCommand { get; }

    protected override async Task LoadCoreAsync(DateOnly start, DateOnly end, string mealType)
    {
        var result = await _service.GetAsync(start, end, mealType, unusedDays: 90);

        UniqueMenuCount = StatisticsFormat.Number(result.Summary.UniqueMenuCount);
        TotalUsageCount = StatisticsFormat.Number(result.Summary.TotalUsageCount);
        NewMenuCount = StatisticsFormat.Number(result.Summary.NewMenuCount);
        RepeatMenuCount = StatisticsFormat.Number(result.Summary.RepeatMenuCount);
        UnusedMenuCount = StatisticsFormat.Number(result.Summary.UnusedMenuCount);

        TopMenus = result.TopMenus;
        Repeats = result.Repeats;
        UnusedMenus = result.UnusedMenus;
        Backdata = result.Backdata;
        OnPropertyChanged(nameof(FilteredBackdata));

        CloseDetail();
    }

    private async Task ShowDetailAsync(MenuTopDto? menu)
    {
        if (menu?.MenuId is null)
        {
            return;
        }

        var (start, end) = Period.Resolve();
        var detail = await _service.GetDetailAsync(menu.MenuId.Value, start, end, SelectedMealType);
        if (detail is null)
        {
            return;
        }

        DetailTitle = $"{detail.MenuName} ({detail.Role})";
        DetailSummary =
            $"사용 {detail.Summary.UsageCount}회 · 중식 {detail.Summary.LunchCount}회 · 석식 {detail.Summary.DinnerCount}회 · " +
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

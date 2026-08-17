using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels.Statistics;

/// <summary>
/// 통계 화면 공용 ViewModel.
/// 기간/식사유형 선택, 로딩 상태, 오류 처리 공통 로직을 제공한다.
/// </summary>
public abstract partial class StatisticsViewModelBase : ObservableObject
{
    private readonly IMessageService _messages;
    private readonly ILogger _logger;

    protected StatisticsViewModelBase(IMessageService messages, ILogger logger)
    {
        _messages = messages;
        _logger = logger;
        LoadCommand = new AsyncRelayCommand(LoadAsync);
        Period.PeriodChanged += (_, _) => _ = LoadCommand.ExecuteAsync(null);
    }

    public StatisticsPeriodViewModel Period { get; } = new();

    public IReadOnlyList<PresetItem> MealTypeOptions { get; } =
    [
        new() { Key = "all", Label = "전체" },
        new() { Key = "lunch", Label = "중식" },
        new() { Key = "dinner", Label = "석식" },
    ];

    [ObservableProperty]
    private string selectedMealType = "all";

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = string.Empty;

    public IAsyncRelayCommand LoadCommand { get; }

    partial void OnSelectedMealTypeChanged(string value)
    {
        _ = LoadCommand.ExecuteAsync(null);
    }

    /// <summary>기간/식사유형 변경 시 호출되는 로드 진입점.</summary>
    private async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = "로딩 중...";
        try
        {
            var (start, end) = Period.Resolve();
            await LoadCoreAsync(start, end, SelectedMealType);
            StatusText = $"{start:yyyy-MM-dd} ~ {end:yyyy-MM-dd}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "통계 로드 중 오류가 발생했습니다.");
            _messages.ShowError($"통계를 불러오지 못했습니다.\n{ex.Message}");
            StatusText = "로드 실패";
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected abstract Task LoadCoreAsync(DateOnly start, DateOnly end, string mealType);
}

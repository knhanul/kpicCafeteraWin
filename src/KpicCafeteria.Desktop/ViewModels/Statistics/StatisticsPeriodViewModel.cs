using CommunityToolkit.Mvvm.ComponentModel;
using KpicCafeteria.Application.Statistics;

namespace KpicCafeteria.Desktop.ViewModels.Statistics;

/// <summary>
/// 통계 기간 선택 공용 ViewModel.
/// 기존 Web 구현(app.js periodRange)의 프리셋(이번 달/3개월/6개월/12개월/올해/직접 선택)을 유지한다.
/// </summary>
public sealed partial class StatisticsPeriodViewModel : ObservableObject
{
    public StatisticsPeriodViewModel()
    {
        ApplyPreset(StatisticsPeriod.ThisMonth);
    }

    /// <summary>기간 변경 알림 (프리셋/직접 선택 모두).</summary>
    public event EventHandler? PeriodChanged;

    public IReadOnlyList<PresetItem> Presets { get; } = StatisticsPeriod.Presets;

    [ObservableProperty]
    private string selectedPreset = StatisticsPeriod.ThisMonth;

    /// <summary>직접 선택 모드 여부 (날짜 선택기 활성화).</summary>
    public bool IsCustom => SelectedPreset == StatisticsPeriod.Custom;

    [ObservableProperty]
    private DateTime startDate;

    [ObservableProperty]
    private DateTime endDate;

    partial void OnSelectedPresetChanged(string value)
    {
        ApplyPreset(value);
        OnPropertyChanged(nameof(IsCustom));
    }

    partial void OnStartDateChanged(DateTime value)
    {
        PeriodChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnEndDateChanged(DateTime value)
    {
        PeriodChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>현재 선택 기간을 (시작일, 종료일)로 반환한다.</summary>
    public (DateOnly Start, DateOnly End) Resolve()
        => StatisticsPeriod.Resolve(
            SelectedPreset,
            DateOnly.FromDateTime(StartDate),
            DateOnly.FromDateTime(EndDate));

    private void ApplyPreset(string preset)
    {
        var (start, end) = StatisticsPeriod.Resolve(preset);
        StartDate = start.ToDateTime(TimeOnly.MinValue);
        EndDate = end.ToDateTime(TimeOnly.MinValue);
        PeriodChanged?.Invoke(this, EventArgs.Empty);
    }
}

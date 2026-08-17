using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>배식 기본값 행.</summary>
public partial class MealTypeSettingRowViewModel : ObservableObject
{
    public int Id { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    [ObservableProperty]
    private int defaultPlannedCount;

    [ObservableProperty]
    private string defaultServiceTime = string.Empty;

    [ObservableProperty]
    private int sortOrder;

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private string? description;
}

/// <summary>
/// 배식 기본값 화면 ViewModel.
/// </summary>
public partial class MealDefaultsViewModel : ObservableObject
{
    private readonly MasterDataService _service;
    private readonly IMessageService _messages;
    private readonly ILogger<MealDefaultsViewModel> _logger;

    public MealDefaultsViewModel(MasterDataService service, IMessageService messages, ILogger<MealDefaultsViewModel> logger)
    {
        _service = service;
        _messages = messages;
        _logger = logger;

        LoadCommand = new AsyncRelayCommand(LoadAsync);
        SaveCommand = new AsyncRelayCommand(SaveAsync);

        _ = LoadAsync();
    }

    public ObservableCollection<MealTypeSettingRowViewModel> Rows { get; } = [];

    public IAsyncRelayCommand LoadCommand { get; }

    public IAsyncRelayCommand SaveCommand { get; }

    private async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var rows = await _service.GetMealTypeSettingsAsync();
            Rows.Clear();
            foreach (var row in rows)
            {
                Rows.Add(new MealTypeSettingRowViewModel
                {
                    Id = row.Id,
                    Code = row.Code,
                    Name = row.Name,
                    DefaultPlannedCount = row.DefaultPlannedCount,
                    DefaultServiceTime = row.DefaultServiceTime,
                    SortOrder = row.SortOrder,
                    IsActive = row.IsActive,
                    Description = row.Description,
                });
            }
        });
    }

    private async Task SaveAsync()
    {
        await ExecuteAsync(async () =>
        {
            var items = Rows
                .Select(row => new MealTypeSettingInput(
                    row.Code, row.DefaultPlannedCount, row.DefaultServiceTime, row.SortOrder, row.IsActive, row.Description))
                .ToList();

            await _service.UpdateMealTypeSettingsAsync(items);
            await LoadAsync();
            _messages.ShowInfo("저장되었습니다.");
        });
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (MasterDataException ex)
        {
            _messages.ShowError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "배식 기본값 작업 중 예상하지 못한 오류가 발생했습니다.");
            _messages.ShowError("예상하지 못한 오류가 발생했습니다.");
        }
    }
}

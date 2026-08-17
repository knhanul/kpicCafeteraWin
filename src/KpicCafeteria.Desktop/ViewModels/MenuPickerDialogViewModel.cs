using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Application.Workspace;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>메뉴 선택기 레시피 옵션.</summary>
public sealed record MenuPickerRecipeOption(int Id, string Name, int Version);

/// <summary>메뉴 선택기 항목.</summary>
public partial class MenuPickerItemViewModel : ObservableObject
{
    public int MenuId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public bool AlreadyAdded { get; init; }

    public string StatusText => AlreadyAdded ? "추가됨" : string.Empty;

    public IReadOnlyList<MenuPickerRecipeOption> Recipes { get; init; } = [];

    [ObservableProperty]
    private bool isSelected;

    [ObservableProperty]
    private int? selectedRecipeId;
}

/// <summary>
/// 메뉴 선택기 대화상자 ViewModel.
/// 검색/역할 필터로 200건 이상도 검색 가능하다.
/// </summary>
public partial class MenuPickerDialogViewModel : ObservableObject
{
    private readonly WorkspaceService _service;
    private readonly ILogger<MenuPickerDialogViewModel> _logger;
    private readonly int _serviceId;

    public MenuPickerDialogViewModel(WorkspaceService service, ILogger<MenuPickerDialogViewModel> logger, int serviceId)
    {
        _service = service;
        _logger = logger;
        _serviceId = serviceId;

        SearchCommand = new AsyncRelayCommand(LoadAsync);
        _ = LoadAsync();
    }

    public ObservableCollection<MenuPickerItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private string? roleFilter;

    [ObservableProperty]
    private bool isBusy;

    public IReadOnlyList<string> MenuRoles => MasterDataCodes.MenuRoles;

    public IAsyncRelayCommand SearchCommand { get; }

    /// <summary>확인 시 선택된 항목 목록.</summary>
    public List<MenuPickerSelection>? Result { get; private set; }

    partial void OnSearchQueryChanged(string value) => _ = LoadAsync();

    partial void OnRoleFilterChanged(string? value) => _ = LoadAsync();

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _service.SearchMenuPickerAsync(SearchQuery, RoleFilter, _serviceId, limit: 200);
            Items.Clear();
            foreach (var item in result.Items)
            {
                Items.Add(new MenuPickerItemViewModel
                {
                    MenuId = item.Id,
                    Name = item.Name,
                    Role = item.Role,
                    AlreadyAdded = item.AlreadyAdded,
                    Recipes = item.Recipes.Select(r => new MenuPickerRecipeOption(r.Id, r.Name, r.Version)).ToList(),
                    SelectedRecipeId = item.DefaultRecipeId,
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "메뉴 선택기 로드 중 오류가 발생했습니다.");
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>확인 버튼 처리. 선택된 항목을 Result에 저장한다.</summary>
    public void Confirm()
    {
        Result = Items
            .Where(i => i.IsSelected && !i.AlreadyAdded)
            .Select(i => new MenuPickerSelection(i.MenuId, i.SelectedRecipeId))
            .ToList();
    }
}

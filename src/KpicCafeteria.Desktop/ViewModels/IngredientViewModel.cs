using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>식재료 목록 항목.</summary>
public sealed record IngredientListItemViewModel(int Id, string Name, string StatGroup, string? DefaultUnit, bool Active)
{
    public string DisplayName => Active ? Name : $"{Name} (미사용)";
}

/// <summary>별칭 행.</summary>
public sealed record AliasRowViewModel(int Id, string Alias);

/// <summary>
/// 식재료 화면 ViewModel.
/// </summary>
public partial class IngredientViewModel : ObservableObject
{
    private readonly MasterDataService _service;
    private readonly IMessageService _messages;
    private readonly ILogger<IngredientViewModel> _logger;
    private IngredientListItemViewModel? _previousIngredient;
    private bool _suppressSelection;

    public IngredientViewModel(MasterDataService service, IMessageService messages, ILogger<IngredientViewModel> logger)
    {
        _service = service;
        _messages = messages;
        _logger = logger;

        LoadIngredientsCommand = new AsyncRelayCommand(LoadIngredientsAsync);
        NewIngredientCommand = new AsyncRelayCommand(NewIngredientAsync);
        SaveIngredientCommand = new AsyncRelayCommand(SaveIngredientAsync);
        ArchiveIngredientCommand = new AsyncRelayCommand(ArchiveIngredientAsync);
        AddAliasCommand = new AsyncRelayCommand(AddAliasAsync);
        RemoveAliasCommand = new AsyncRelayCommand<AliasRowViewModel>(RemoveAliasAsync);

        _ = LoadIngredientsAsync();
    }

    // ---- 목록 ----

    public ObservableCollection<IngredientListItemViewModel> Ingredients { get; } = [];

    [ObservableProperty]
    private string searchQuery = string.Empty;

    [ObservableProperty]
    private string? statGroupFilter;

    [ObservableProperty]
    private bool showInactive;

    [ObservableProperty]
    private IngredientListItemViewModel? selectedIngredient;

    public IReadOnlyList<string> StatGroups => MasterDataCodes.StatGroups;

    public IReadOnlyList<string> Units => MasterDataCodes.Units;

    // ---- 편집 ----

    [ObservableProperty]
    private int? editingIngredientId;

    [ObservableProperty]
    private string ingredientName = string.Empty;

    [ObservableProperty]
    private string statGroup = "기타";

    [ObservableProperty]
    private string? defaultUnit;

    [ObservableProperty]
    private double? purchasePackageQuantity;

    [ObservableProperty]
    private string? purchasePackageUnit;

    [ObservableProperty]
    private double? kgFactor;

    [ObservableProperty]
    private bool analysisExcluded;

    [ObservableProperty]
    private bool ingredientActive = true;

    [ObservableProperty]
    private bool editorDirty;

    // ---- 별칭 ----

    public ObservableCollection<AliasRowViewModel> Aliases { get; } = [];

    [ObservableProperty]
    private string newAlias = string.Empty;

    // ---- Commands ----

    public IAsyncRelayCommand LoadIngredientsCommand { get; }

    public IAsyncRelayCommand NewIngredientCommand { get; }

    public IAsyncRelayCommand SaveIngredientCommand { get; }

    public IAsyncRelayCommand ArchiveIngredientCommand { get; }

    public IAsyncRelayCommand AddAliasCommand { get; }

    public IAsyncRelayCommand<AliasRowViewModel> RemoveAliasCommand { get; }

    // ---- 선택/변경 처리 ----

    partial void OnSelectedIngredientChanged(IngredientListItemViewModel? value)
    {
        if (value is null || _suppressSelection)
        {
            return;
        }

        if (EditorDirty && !_messages.Confirm("변경 내용이 있습니다. 저장하지 않고 이동하시겠습니까?"))
        {
            _suppressSelection = true;
            SelectedIngredient = _previousIngredient;
            _suppressSelection = false;
            return;
        }

        _previousIngredient = value;
        _ = ExecuteAsync(() => LoadIngredientDetailAsync(value.Id));
    }

    partial void OnSearchQueryChanged(string value) => _ = LoadIngredientsAsync();

    partial void OnStatGroupFilterChanged(string? value) => _ = LoadIngredientsAsync();

    partial void OnShowInactiveChanged(bool value) => _ = LoadIngredientsAsync();

    partial void OnIngredientNameChanged(string value) => EditorDirty = true;

    partial void OnStatGroupChanged(string value) => EditorDirty = true;

    partial void OnDefaultUnitChanged(string? value) => EditorDirty = true;

    partial void OnPurchasePackageQuantityChanged(double? value) => EditorDirty = true;

    partial void OnPurchasePackageUnitChanged(string? value) => EditorDirty = true;

    partial void OnKgFactorChanged(double? value) => EditorDirty = true;

    partial void OnAnalysisExcludedChanged(bool value) => EditorDirty = true;

    partial void OnIngredientActiveChanged(bool value) => EditorDirty = true;

    // ---- 목록/상세 ----

    private async Task LoadIngredientsAsync()
    {
        await ExecuteAsync(async () =>
        {
            bool? active = ShowInactive ? null : true;
            var result = await _service.SearchIngredientsAsync(SearchQuery, StatGroupFilter, active, limit: 200);
            Ingredients.Clear();
            foreach (var item in result.Items)
            {
                Ingredients.Add(new IngredientListItemViewModel(item.Id, item.Name, item.StatGroup, item.DefaultUnit, item.Active));
            }
        });
    }

    private async Task LoadIngredientDetailAsync(int ingredientId)
    {
        await ExecuteAsync(async () =>
        {
            var detail = await _service.GetIngredientAsync(ingredientId);

            EditingIngredientId = detail.Ingredient.Id;
            IngredientName = detail.Ingredient.Name;
            StatGroup = detail.Ingredient.StatGroup;
            DefaultUnit = detail.Ingredient.DefaultUnit;
            PurchasePackageQuantity = detail.Ingredient.PurchasePackageQuantity;
            PurchasePackageUnit = detail.Ingredient.PurchasePackageUnit;
            KgFactor = detail.Ingredient.KgFactor;
            AnalysisExcluded = detail.Ingredient.AnalysisExcluded;
            IngredientActive = detail.Ingredient.Active;
            EditorDirty = false;

            Aliases.Clear();
            foreach (var alias in detail.Aliases)
            {
                Aliases.Add(new AliasRowViewModel(alias.Id, alias.Alias));
            }

            NewAlias = string.Empty;
        });
    }

    private Task NewIngredientAsync()
    {
        if (EditorDirty && !_messages.Confirm("변경 내용이 있습니다. 저장하지 않고 새 재료를 만드시겠습니까?"))
        {
            return Task.CompletedTask;
        }

        ClearEditor();
        return Task.CompletedTask;
    }

    private async Task SaveIngredientAsync()
    {
        await ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(IngredientName))
            {
                _messages.ShowError("재료명을 입력해 주세요.");
                return;
            }

            var input = new IngredientInput(IngredientName, StatGroup, DefaultUnit, PurchasePackageQuantity, PurchasePackageUnit, KgFactor, AnalysisExcluded, IngredientActive);
            if (EditingIngredientId is null)
            {
                var created = await _service.CreateIngredientAsync(input);
                EditingIngredientId = created.Id;
            }
            else
            {
                await _service.UpdateIngredientAsync(EditingIngredientId.Value, input);
            }

            EditorDirty = false;
            await LoadIngredientsAsync();
            var saved = Ingredients.FirstOrDefault(i => i.Id == EditingIngredientId);
            if (saved is not null)
            {
                _suppressSelection = true;
                SelectedIngredient = saved;
                _suppressSelection = false;
            }

            _messages.ShowInfo("저장되었습니다.");
        });
    }

    private async Task ArchiveIngredientAsync()
    {
        if (SelectedIngredient is null)
        {
            return;
        }

        if (!_messages.Confirm($"재료 '{SelectedIngredient.Name}'을(를) 미사용 처리하시겠습니까?"))
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.ArchiveIngredientAsync(SelectedIngredient.Id);
            EditorDirty = false;
            await LoadIngredientsAsync();
            SelectedIngredient = null;
            ClearEditor();
            Aliases.Clear();
            _messages.ShowInfo("미사용 처리되었습니다.");
        });
    }

    private void ClearEditor()
    {
        EditingIngredientId = null;
        IngredientName = string.Empty;
        StatGroup = "기타";
        DefaultUnit = null;
        PurchasePackageQuantity = null;
        PurchasePackageUnit = null;
        KgFactor = null;
        AnalysisExcluded = false;
        IngredientActive = true;
        EditorDirty = false;
    }

    // ---- 별칭 ----

    private async Task AddAliasAsync()
    {
        if (EditingIngredientId is null)
        {
            _messages.ShowInfo("먼저 재료를 선택해 주세요.");
            return;
        }

        if (string.IsNullOrWhiteSpace(NewAlias))
        {
            _messages.ShowError("별칭을 입력해 주세요.");
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.AddAliasAsync(EditingIngredientId.Value, NewAlias);
            await LoadIngredientDetailAsync(EditingIngredientId.Value);
            _messages.ShowInfo("별칭이 추가되었습니다.");
        });
    }

    private async Task RemoveAliasAsync(AliasRowViewModel? alias)
    {
        if (alias is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.RemoveAliasAsync(alias.Id);
            if (EditingIngredientId is not null)
            {
                await LoadIngredientDetailAsync(EditingIngredientId.Value);
            }
        });
    }

    // ---- 공통 ----

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
            _logger.LogError(ex, "식재료 작업 중 예상하지 못한 오류가 발생했습니다.");
            _messages.ShowError("예상하지 못한 오류가 발생했습니다.");
        }
    }
}

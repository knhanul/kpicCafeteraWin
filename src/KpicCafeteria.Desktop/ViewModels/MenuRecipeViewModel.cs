using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Desktop.Services;
using Microsoft.Extensions.Logging;

namespace KpicCafeteria.Desktop.ViewModels;

/// <summary>메뉴 목록 항목.</summary>
public sealed record MenuListItemViewModel(int Id, string Name, string Role, bool Active, int RecipeCount, int? DefaultRecipeId)
{
    public string DisplayName => Active ? Name : $"{Name} (미사용)";
}

/// <summary>레시피 목록 항목.</summary>
public sealed record RecipeListItemViewModel(int Id, string Name, int Version, bool IsDefault, bool Active, int IngredientCount)
{
    public string DisplayName => $"{Name} (v{Version}){(IsDefault ? " · 기본" : "")}{(Active ? "" : " · 미사용")}";
}

/// <summary>
/// 레시피 재료 그리드 행.
/// 재료명이 단일 소스이며, IngredientId는 재료명에서 파생한다.
/// (ComboBox SelectedValue+Text 이중 바인딩 충돌 방지)
/// </summary>
public partial class RecipeIngredientRowViewModel : ObservableObject
{
    private readonly Func<string, int?> _resolveIngredientId;

    public RecipeIngredientRowViewModel(Func<string, int?> resolveIngredientId)
    {
        _resolveIngredientId = resolveIngredientId;
    }

    /// <summary>재료 ID. 로드 시 설정되며, 재료명이 바뀌면 재료명에서 다시 파생한다.</summary>
    public int? IngredientId { get; private set; }

    [ObservableProperty]
    private string ingredientName = string.Empty;

    [ObservableProperty]
    private double? quantityPer100;

    [ObservableProperty]
    private string? unit;

    [ObservableProperty]
    private bool isPrimary;

    /// <summary>DB에서 로드된 값을 설정한다 (재료명 변경 시 ID 파생 로직을 건너뜀).</summary>
    public void SetLoaded(int? ingredientId, string name)
    {
        IngredientId = ingredientId;
        IngredientName = name;
    }

    partial void OnIngredientNameChanged(string value)
        => IngredientId = _resolveIngredientId(value.Trim());
}

/// <summary>재료 선택 옵션 (레시피 그리드 ComboBox용).</summary>
public sealed record IngredientOption(int Id, string Name, string? DefaultUnit);

/// <summary>
/// 메뉴·레시피 화면 ViewModel.
/// ViewModel → MasterDataService → Repository → DbContext 구조를 유지한다.
/// </summary>
public partial class MenuRecipeViewModel : ObservableObject
{
    private readonly MasterDataService _service;
    private readonly IMessageService _messages;
    private readonly ILogger<MenuRecipeViewModel> _logger;
    private readonly Dictionary<string, int> _ingredientNameToId = new(StringComparer.OrdinalIgnoreCase);
    private MenuListItemViewModel? _previousMenu;
    private RecipeListItemViewModel? _previousRecipe;
    private bool _suppressSelection;

    public MenuRecipeViewModel(MasterDataService service, IMessageService messages, ILogger<MenuRecipeViewModel> logger)
    {
        _service = service;
        _messages = messages;
        _logger = logger;

        LoadMenusCommand = new AsyncRelayCommand(() => LoadMenusAsync());
        NewMenuCommand = new AsyncRelayCommand(NewMenuAsync);
        SaveMenuCommand = new AsyncRelayCommand(SaveMenuAsync);
        ArchiveMenuCommand = new AsyncRelayCommand(ArchiveMenuAsync);
        NewRecipeCommand = new AsyncRelayCommand(NewRecipeAsync);
        SaveRecipeCommand = new AsyncRelayCommand(SaveRecipeAsync);
        ArchiveRecipeCommand = new AsyncRelayCommand(ArchiveRecipeAsync);
        SetDefaultRecipeCommand = new AsyncRelayCommand(SetDefaultRecipeAsync);
        AddIngredientRowCommand = new RelayCommand(AddIngredientRow);
        RemoveIngredientRowCommand = new RelayCommand<RecipeIngredientRowViewModel>(RemoveIngredientRow);

        _ = LoadMenusAsync();
        _ = LoadIngredientOptionsAsync();
    }

    // ---- 메뉴 목록 ----

    public ObservableCollection<MenuListItemViewModel> Menus { get; } = [];

    [ObservableProperty]
    private string menuSearchQuery = string.Empty;

    [ObservableProperty]
    private string? menuRoleFilter;

    [ObservableProperty]
    private bool showInactive;

    [ObservableProperty]
    private MenuListItemViewModel? selectedMenu;

    public IReadOnlyList<string> MenuRoles => MasterDataCodes.MenuRoles;

    // ---- 메뉴 편집 ----

    [ObservableProperty]
    private int? editingMenuId;

    [ObservableProperty]
    private string menuName = string.Empty;

    [ObservableProperty]
    private string canonicalName = string.Empty;

    [ObservableProperty]
    private string menuRole = "기타";

    [ObservableProperty]
    private bool menuActive = true;

    [ObservableProperty]
    private bool menuEditorDirty;

    // ---- 레시피 목록 ----

    public ObservableCollection<RecipeListItemViewModel> Recipes { get; } = [];

    [ObservableProperty]
    private RecipeListItemViewModel? selectedRecipe;

    // ---- 레시피 편집 ----

    [ObservableProperty]
    private int? editingRecipeId;

    [ObservableProperty]
    private string recipeName = string.Empty;

    [ObservableProperty]
    private string? recipeNote;

    [ObservableProperty]
    private bool recipeActive = true;

    [ObservableProperty]
    private bool recipeIsDefault;

    [ObservableProperty]
    private bool recipeEditorDirty;

    public ObservableCollection<RecipeIngredientRowViewModel> RecipeIngredients { get; } = [];

    public ObservableCollection<IngredientOption> IngredientOptions { get; } = [];

    // ---- Commands ----

    public IAsyncRelayCommand LoadMenusCommand { get; }

    public IAsyncRelayCommand NewMenuCommand { get; }

    public IAsyncRelayCommand SaveMenuCommand { get; }

    public IAsyncRelayCommand ArchiveMenuCommand { get; }

    public IAsyncRelayCommand NewRecipeCommand { get; }

    public IAsyncRelayCommand SaveRecipeCommand { get; }

    public IAsyncRelayCommand ArchiveRecipeCommand { get; }

    public IAsyncRelayCommand SetDefaultRecipeCommand { get; }

    public RelayCommand AddIngredientRowCommand { get; }

    public RelayCommand<RecipeIngredientRowViewModel> RemoveIngredientRowCommand { get; }

    // ---- 선택 변경 처리 ----

    partial void OnSelectedMenuChanged(MenuListItemViewModel? value)
    {
        if (value is null || _suppressSelection)
        {
            return;
        }

        if (MenuEditorDirty || RecipeEditorDirty)
        {
            if (!_messages.Confirm("변경 내용이 있습니다. 저장하지 않고 이동하시겠습니까?"))
            {
                _suppressSelection = true;
                SelectedMenu = _previousMenu;
                _suppressSelection = false;
                return;
            }
        }

        _previousMenu = value;
        _ = ExecuteAsync(() => LoadMenuDetailAsync(value.Id));
    }

    partial void OnSelectedRecipeChanged(RecipeListItemViewModel? value)
    {
        if (value is null || _suppressSelection)
        {
            return;
        }

        if (RecipeEditorDirty)
        {
            if (!_messages.Confirm("변경 내용이 있습니다. 저장하지 않고 이동하시겠습니까?"))
            {
                _suppressSelection = true;
                SelectedRecipe = _previousRecipe;
                _suppressSelection = false;
                return;
            }
        }

        _previousRecipe = value;
        _ = ExecuteAsync(() => LoadRecipeDetailAsync(value.Id));
    }

    partial void OnMenuSearchQueryChanged(string value) => _ = LoadMenusAsync();

    partial void OnMenuRoleFilterChanged(string? value) => _ = LoadMenusAsync();

    partial void OnShowInactiveChanged(bool value) => _ = LoadMenusAsync();

    partial void OnMenuNameChanged(string value) => MenuEditorDirty = true;

    partial void OnCanonicalNameChanged(string value) => MenuEditorDirty = true;

    partial void OnMenuRoleChanged(string value) => MenuEditorDirty = true;

    partial void OnMenuActiveChanged(bool value) => MenuEditorDirty = true;

    partial void OnRecipeNameChanged(string value) => RecipeEditorDirty = true;

    partial void OnRecipeNoteChanged(string? value) => RecipeEditorDirty = true;

    partial void OnRecipeActiveChanged(bool value) => RecipeEditorDirty = true;

    partial void OnRecipeIsDefaultChanged(bool value) => RecipeEditorDirty = true;

    // ---- 메뉴 ----

    private async Task LoadMenusAsync()
    {
        await ExecuteAsync(async () =>
        {
            bool? active = ShowInactive ? null : true;
            var result = await _service.SearchMenusAsync(MenuSearchQuery, MenuRoleFilter, active, limit: 200);
            Menus.Clear();
            foreach (var item in result.Items)
            {
                Menus.Add(new MenuListItemViewModel(item.Id, item.Name, item.Role, item.Active, item.RecipeCount, item.DefaultRecipeId));
            }
        });
    }

    private Task NewMenuAsync()
    {
        if (MenuEditorDirty && !_messages.Confirm("변경 내용이 있습니다. 저장하지 않고 새 메뉴를 만드시겠습니까?"))
        {
            return Task.CompletedTask;
        }

        EditingMenuId = null;
        MenuName = string.Empty;
        CanonicalName = string.Empty;
        MenuRole = "기타";
        MenuActive = true;
        MenuEditorDirty = false;
        Recipes.Clear();
        SelectedRecipe = null;
        ClearRecipeEditor();
        return Task.CompletedTask;
    }

    private async Task SaveMenuAsync()
    {
        await ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(MenuName))
            {
                _messages.ShowError("메뉴명을 입력해 주세요.");
                return;
            }

            var input = new MenuInput(MenuName, CanonicalName, MenuRole, MenuActive);
            if (EditingMenuId is null)
            {
                var created = await _service.CreateMenuAsync(input);
                EditingMenuId = created.Id;
            }
            else
            {
                await _service.UpdateMenuAsync(EditingMenuId.Value, input);
            }

            MenuEditorDirty = false;
            await LoadMenusAsync();
            var saved = Menus.FirstOrDefault(m => m.Id == EditingMenuId);
            if (saved is not null)
            {
                _suppressSelection = true;
                SelectedMenu = saved;
                _suppressSelection = false;
            }

            _messages.ShowInfo("저장되었습니다.");
        });
    }

    private async Task ArchiveMenuAsync()
    {
        if (SelectedMenu is null)
        {
            return;
        }

        if (!_messages.Confirm($"메뉴 '{SelectedMenu.Name}'을(를) 미사용 처리하시겠습니까?"))
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.ArchiveMenuAsync(SelectedMenu.Id);
            MenuEditorDirty = false;
            RecipeEditorDirty = false;
            await LoadMenusAsync();
            SelectedMenu = null;
            ClearMenuEditor();
            ClearRecipeEditor();
            _messages.ShowInfo("미사용 처리되었습니다.");
        });
    }

    private async Task LoadMenuDetailAsync(int menuId)
    {
        await ExecuteAsync(async () =>
        {
            var detail = await _service.GetMenuAsync(menuId);

            EditingMenuId = detail.Menu.Id;
            MenuName = detail.Menu.Name;
            CanonicalName = detail.Menu.CanonicalName;
            MenuRole = detail.Menu.Role;
            MenuActive = detail.Menu.Active;
            MenuEditorDirty = false;

            Recipes.Clear();
            foreach (var recipe in detail.Recipes)
            {
                Recipes.Add(new RecipeListItemViewModel(recipe.Id, recipe.Name, recipe.Version, recipe.IsDefault, recipe.Active, recipe.IngredientCount));
            }

            SelectedRecipe = null;
            ClearRecipeEditor();
        });
    }

    private void ClearMenuEditor()
    {
        EditingMenuId = null;
        MenuName = string.Empty;
        CanonicalName = string.Empty;
        MenuRole = "기타";
        MenuActive = true;
        MenuEditorDirty = false;
    }

    // ---- 레시피 ----

    private Task NewRecipeAsync()
    {
        if (SelectedMenu is null)
        {
            _messages.ShowInfo("먼저 메뉴를 선택해 주세요.");
            return Task.CompletedTask;
        }

        if (RecipeEditorDirty && !_messages.Confirm("변경 내용이 있습니다. 저장하지 않고 새 레시피를 만드시겠습니까?"))
        {
            return Task.CompletedTask;
        }

        ClearRecipeEditor();
        return Task.CompletedTask;
    }

    private async Task SaveRecipeAsync()
    {
        if (SelectedMenu is null)
        {
            _messages.ShowInfo("먼저 메뉴를 선택해 주세요.");
            return;
        }

        await ExecuteAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(RecipeName) && EditingRecipeId is null)
            {
                _messages.ShowError("레시피명을 입력해 주세요.");
                return;
            }

            var items = RecipeIngredients
                .Where(row => !string.IsNullOrWhiteSpace(row.IngredientName))
                .Select(row => new RecipeItemInput(row.IngredientId, row.IngredientName, row.QuantityPer100, row.Unit, row.IsPrimary))
                .ToList();

            var input = new RecipeInput(RecipeName, RecipeNote, RecipeIsDefault, RecipeActive, items);
            RecipeDto saved;
            if (EditingRecipeId is null)
            {
                saved = await _service.CreateRecipeAsync(SelectedMenu.Id, input);
            }
            else
            {
                saved = await _service.UpdateRecipeAsync(EditingRecipeId.Value, input);
            }

            RecipeEditorDirty = false;
            await LoadMenuDetailAsync(SelectedMenu.Id);
            var listItem = Recipes.FirstOrDefault(r => r.Id == saved.Id);
            if (listItem is not null)
            {
                _suppressSelection = true;
                SelectedRecipe = listItem;
                _suppressSelection = false;
            }

            _messages.ShowInfo("저장되었습니다.");
        });
    }

    private async Task ArchiveRecipeAsync()
    {
        if (SelectedRecipe is null)
        {
            return;
        }

        if (!_messages.Confirm($"레시피 '{SelectedRecipe.Name}'을(를) 미사용 처리하시겠습니까?"))
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.ArchiveRecipeAsync(SelectedRecipe.Id);
            RecipeEditorDirty = false;
            if (SelectedMenu is not null)
            {
                await LoadMenuDetailAsync(SelectedMenu.Id);
            }

            _messages.ShowInfo("미사용 처리되었습니다.");
        });
    }

    private async Task SetDefaultRecipeAsync()
    {
        if (SelectedRecipe is null)
        {
            return;
        }

        await ExecuteAsync(async () =>
        {
            await _service.SetDefaultRecipeAsync(SelectedRecipe.Id);
            if (SelectedMenu is not null)
            {
                await LoadMenuDetailAsync(SelectedMenu.Id);
            }

            _messages.ShowInfo("기본 레시피로 지정되었습니다.");
        });
    }

    private async Task LoadRecipeDetailAsync(int recipeId)
    {
        await ExecuteAsync(async () =>
        {
            var recipe = await _service.GetRecipeAsync(recipeId);

            EditingRecipeId = recipe.Id;
            RecipeName = recipe.Name;
            RecipeNote = recipe.Note;
            RecipeActive = recipe.Active;
            RecipeIsDefault = recipe.IsDefault;
            RecipeEditorDirty = false;

            RecipeIngredients.Clear();
            foreach (var ingredient in recipe.Ingredients)
            {
                var row = new RecipeIngredientRowViewModel(ResolveIngredientId);
                row.SetLoaded(ingredient.IngredientId, ingredient.IngredientName);
                row.QuantityPer100 = ingredient.QuantityPer100;
                row.Unit = ingredient.Unit;
                row.IsPrimary = ingredient.IsPrimary;
                RecipeIngredients.Add(row);
            }
        });
    }

    private void ClearRecipeEditor()
    {
        EditingRecipeId = null;
        RecipeName = string.Empty;
        RecipeNote = null;
        RecipeActive = true;
        RecipeIsDefault = false;
        RecipeEditorDirty = false;
        RecipeIngredients.Clear();
    }

    private void AddIngredientRow()
        => RecipeIngredients.Add(new RecipeIngredientRowViewModel(ResolveIngredientId));

    private int? ResolveIngredientId(string name)
        => _ingredientNameToId.TryGetValue(name, out var id) ? id : null;

    private void RemoveIngredientRow(RecipeIngredientRowViewModel? row)
    {
        if (row is not null)
        {
            RecipeIngredients.Remove(row);
        }
    }

    // ---- 재료 옵션 ----

    private async Task LoadIngredientOptionsAsync()
    {
        await ExecuteAsync(async () =>
        {
            var result = await _service.SearchIngredientsAsync(null, null, true, limit: 1000);
            IngredientOptions.Clear();
            _ingredientNameToId.Clear();
            foreach (var item in result.Items)
            {
                IngredientOptions.Add(new IngredientOption(item.Id, item.Name, item.DefaultUnit));
                _ingredientNameToId[item.Name] = item.Id;
            }
        });
    }

    // ---- 공통 ----

    /// <summary>업무 오류는 메시지로, 예상 외 오류는 로깅 후 일반 메시지로 표시한다.</summary>
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
            _logger.LogError(ex, "메뉴·레시피 작업 중 예상하지 못한 오류가 발생했습니다.");
            _messages.ShowError("예상하지 못한 오류가 발생했습니다.");
        }
    }
}

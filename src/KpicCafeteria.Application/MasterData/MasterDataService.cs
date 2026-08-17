using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Domain;
using KpicCafeteria.Domain.Entities;

namespace KpicCafeteria.Application.MasterData;

/// <summary>
/// 기준정보(메뉴/식재료/별칭/다중 레시피/배식 기본값) 업무 서비스.
/// 기존 Python master.py / master_data.py의 업무규칙을 그대로 유지한다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\master.py
/// C:\Pjt\kpicCafeteria\backend\app\routers\master_data.py
/// </summary>
public sealed class MasterDataService
{
    private readonly IMasterDataRepositoryFactory _factory;

    public MasterDataService(IMasterDataRepositoryFactory factory)
    {
        _factory = factory;
    }

    /// <summary>작업 단위별 새 리포지토리(DbContext)를 생성한다.</summary>
    private IMasterDataRepository CreateRepository() => _factory.Create();

    // =======================================================================
    // Menu
    // =======================================================================

    public async Task<MenuSearchResult> SearchMenusAsync(
        string? query, string? role, bool? active, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var rows = await repository.SearchMenusAsync(query, role, active, limit + 1, offset, cancellationToken);
        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).Select(MapMenuListItem).ToList();
        var total = await repository.CountMenusAsync(query, role, active, cancellationToken);
        return new MenuSearchResult(items, total, offset, limit, hasMore);
    }

    public async Task<MenuDetailDto> GetMenuAsync(int id, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var menu = await repository.GetMenuAsync(id, cancellationToken)
            ?? throw new MasterDataNotFoundException("메뉴를 찾을 수 없습니다.");
        return MapMenuDetail(menu);
    }

    public async Task<MenuDto> CreateMenuAsync(MenuInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var name = input.Name.Trim();
        if (await repository.FindMenuByNameAsync(name, cancellationToken) is not null)
        {
            throw new DuplicateMenuNameException();
        }

        var menu = new Menu
        {
            Name = name,
            CanonicalName = string.IsNullOrWhiteSpace(input.CanonicalName) ? name : input.CanonicalName.Trim(),
            Role = input.Role,
            Active = input.Active,
        };
        repository.Add(menu);
        await repository.SaveChangesAsync(cancellationToken);
        return MapMenu(menu);
    }

    public async Task<MenuDto> UpdateMenuAsync(int id, MenuInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var menu = await repository.GetMenuAsync(id, cancellationToken)
            ?? throw new MasterDataNotFoundException("메뉴를 찾을 수 없습니다.");

        var name = input.Name.Trim();
        var existing = await repository.FindMenuByNameAsync(name, cancellationToken);
        if (existing is not null && existing.Id != id)
        {
            throw new DuplicateMenuNameException();
        }

        menu.Name = name;
        menu.CanonicalName = string.IsNullOrWhiteSpace(input.CanonicalName) ? name : input.CanonicalName.Trim();
        menu.Role = input.Role;
        menu.Active = input.Active;
        await repository.SaveChangesAsync(cancellationToken);
        return MapMenu(menu);
    }

    /// <summary>
    /// 메뉴 미사용 처리. 물리 삭제하지 않으며, 메뉴의 모든 레시피도 함께 미사용 처리한다.
    /// 과거 식단 스냅샷에는 영향을 주지 않는다.
    /// </summary>
    public async Task ArchiveMenuAsync(int id, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var menu = await repository.GetMenuAsync(id, cancellationToken)
            ?? throw new MasterDataNotFoundException("메뉴를 찾을 수 없습니다.");

        menu.Active = false;
        foreach (var recipe in menu.Recipes)
        {
            recipe.Active = false;
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    // =======================================================================
    // Ingredient
    // =======================================================================

    public async Task<IngredientSearchResult> SearchIngredientsAsync(
        string? query, string? statGroup, bool? active, int limit = 100, int offset = 0, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var rows = await repository.SearchIngredientsAsync(query, statGroup, active, limit + 1, offset, cancellationToken);
        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).Select(MapIngredient).ToList();
        var total = await repository.CountIngredientsAsync(query, statGroup, active, cancellationToken);
        return new IngredientSearchResult(items, total, offset, limit, hasMore);
    }

    public async Task<IngredientDetailDto> GetIngredientAsync(int id, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var row = await repository.GetIngredientAsync(id, cancellationToken)
            ?? throw new MasterDataNotFoundException("재료를 찾을 수 없습니다.");
        return new IngredientDetailDto(
            MapIngredient(row),
            row.Aliases.Select(a => new AliasDto(a.Id, a.Alias)).ToList());
    }

    public async Task<IngredientDto> CreateIngredientAsync(IngredientInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var name = input.Name.Trim();
        if (await repository.FindIngredientByNameAsync(name, cancellationToken) is not null)
        {
            throw new DuplicateIngredientNameException();
        }

        var row = new Ingredient
        {
            Name = name,
            StatGroup = input.StatGroup,
            DefaultUnit = input.DefaultUnit,
            PurchasePackageQuantity = input.PurchasePackageQuantity,
            PurchasePackageUnit = input.PurchasePackageUnit,
            KgFactor = input.KgFactor,
            AnalysisExcluded = input.AnalysisExcluded,
            Active = input.Active,
        };
        repository.Add(row);
        await repository.SaveChangesAsync(cancellationToken);
        return MapIngredient(row);
    }

    public async Task<IngredientDto> UpdateIngredientAsync(int id, IngredientInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var row = await repository.GetIngredientAsync(id, cancellationToken)
            ?? throw new MasterDataNotFoundException("재료를 찾을 수 없습니다.");

        var name = input.Name.Trim();
        var existing = await repository.FindIngredientByNameAsync(name, cancellationToken);
        if (existing is not null && existing.Id != id)
        {
            throw new DuplicateIngredientNameException();
        }

        row.Name = name;
        row.StatGroup = input.StatGroup;
        row.DefaultUnit = input.DefaultUnit;
        row.PurchasePackageQuantity = input.PurchasePackageQuantity;
        row.PurchasePackageUnit = input.PurchasePackageUnit;
        row.KgFactor = input.KgFactor;
        row.AnalysisExcluded = input.AnalysisExcluded;
        row.Active = input.Active;
        await repository.SaveChangesAsync(cancellationToken);
        return MapIngredient(row);
    }

    /// <summary>재료 미사용 처리. 물리 삭제하지 않는다.</summary>
    public async Task ArchiveIngredientAsync(int id, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var row = await repository.GetIngredientAsync(id, cancellationToken)
            ?? throw new MasterDataNotFoundException("재료를 찾을 수 없습니다.");

        row.Active = false;
        await repository.SaveChangesAsync(cancellationToken);
    }

    // =======================================================================
    // Alias
    // =======================================================================

    /// <summary>
    /// 별칭 추가. 이미 존재하는 별칭이면 소유 재료를 변경한다 (기존 규칙).
    /// </summary>
    public async Task<AliasDto> AddAliasAsync(int ingredientId, string alias, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var ingredient = await repository.GetIngredientAsync(ingredientId, cancellationToken)
            ?? throw new MasterDataNotFoundException("재료를 찾을 수 없습니다.");

        var trimmed = alias.Trim();
        var existing = await repository.FindAliasAsync(trimmed, cancellationToken);
        IngredientAlias result;
        if (existing is not null)
        {
            existing.IngredientId = ingredientId;
            result = existing;
        }
        else
        {
            result = new IngredientAlias { Alias = trimmed, IngredientId = ingredientId, Source = "사용자" };
            repository.Add(result);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return new AliasDto(result.Id, result.Alias);
    }

    /// <summary>별칭 삭제 (Windows UI 관리용 추가 기능).</summary>
    public async Task RemoveAliasAsync(int aliasId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var alias = await repository.GetAliasAsync(aliasId, cancellationToken)
            ?? throw new MasterDataNotFoundException("별칭을 찾을 수 없습니다.");

        repository.Remove(alias);
        await repository.SaveChangesAsync(cancellationToken);
    }

    // =======================================================================
    // Recipe
    // =======================================================================

    public async Task<RecipeDto> GetRecipeAsync(int recipeId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var recipe = await repository.GetRecipeAsync(recipeId, cancellationToken)
            ?? throw new MasterDataNotFoundException("레시피를 찾을 수 없습니다.");
        return MapRecipe(recipe);
    }

    public async Task<RecipeDto> CreateRecipeAsync(int menuId, RecipeInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var menu = await repository.GetMenuAsync(menuId, cancellationToken)
            ?? throw new MasterDataNotFoundException("메뉴를 찾을 수 없습니다.");

        Recipe recipe;
        await repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var resolved = await ResolveRecipeItemsAsync(repository, input.Ingredients, cancellationToken);
            var key = CompositionKey.Create(resolved.Select(x => x.Ingredient.Id));

            var duplicate = await repository.FindRecipeByCompositionAsync(menuId, key, null, cancellationToken);
            if (duplicate is not null)
            {
                throw new DuplicateRecipeCompositionException(duplicate.Name);
            }

            var nextVersion = (await repository.GetMaxRecipeVersionAsync(menuId, cancellationToken)) + 1;
            var hasActiveRecipe = await repository.HasActiveRecipeAsync(menuId, cancellationToken);

            recipe = new Recipe
            {
                MenuId = menuId,
                Name = string.IsNullOrWhiteSpace(input.Name) ? $"레시피 {nextVersion}" : input.Name.Trim(),
                Version = nextVersion,
                CompositionKey = key,
                Note = input.Note,
                Active = input.Active,
                // 첫 활성 레시피는 자동으로 기본 레시피가 된다.
                IsDefault = input.IsDefault || !hasActiveRecipe,
            };
            repository.Add(recipe);
            await repository.SaveChangesAsync(cancellationToken);

            ReplaceRecipeItems(recipe, resolved);
            if (recipe.IsDefault)
            {
                await SetDefaultRecipeAsync(repository, recipe, cancellationToken);
            }

            await repository.SaveChangesAsync(cancellationToken);
            await repository.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var created = await repository.GetRecipeAsync(recipe.Id, cancellationToken);
        return MapRecipe(created!);
    }

    public async Task<RecipeDto> UpdateRecipeAsync(int recipeId, RecipeInput input, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var recipe = await repository.GetRecipeAsync(recipeId, cancellationToken)
            ?? throw new MasterDataNotFoundException("레시피를 찾을 수 없습니다.");

        await repository.BeginTransactionAsync(cancellationToken);
        try
        {
            var resolved = await ResolveRecipeItemsAsync(repository, input.Ingredients, cancellationToken);
            var key = CompositionKey.Create(resolved.Select(x => x.Ingredient.Id));

            var duplicate = await repository.FindRecipeByCompositionAsync(recipe.MenuId, key, recipeId, cancellationToken);
            if (duplicate is not null)
            {
                throw new DuplicateRecipeCompositionException(duplicate.Name);
            }

            recipe.Name = string.IsNullOrWhiteSpace(input.Name) ? recipe.Name : input.Name.Trim();
            recipe.Note = input.Note;
            recipe.Active = input.Active;
            recipe.CompositionKey = key;
            ReplaceRecipeItems(recipe, resolved);

            if (input.IsDefault)
            {
                await SetDefaultRecipeAsync(repository, recipe, cancellationToken);
            }
            else if (recipe.IsDefault && !recipe.Active)
            {
                // 기본 레시피를 비활성화하면 버전 순 활성 레시피가 대체된다.
                var replacement = await repository.FindActiveReplacementRecipeAsync(recipe.MenuId, recipeId, cancellationToken);
                if (replacement is not null)
                {
                    await SetDefaultRecipeAsync(repository, replacement, cancellationToken);
                }
            }

            await repository.SaveChangesAsync(cancellationToken);
            await repository.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await repository.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        var updated = await repository.GetRecipeAsync(recipeId, cancellationToken);
        return MapRecipe(updated!);
    }

    /// <summary>
    /// 레시피 미사용 처리. 물리 삭제하지 않는다.
    /// 기본 레시피를 미사용 처리하면 버전 순 활성 레시피가 대체 기본이 된다.
    /// </summary>
    public async Task ArchiveRecipeAsync(int recipeId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var recipe = await repository.GetRecipeAsync(recipeId, cancellationToken)
            ?? throw new MasterDataNotFoundException("레시피를 찾을 수 없습니다.");

        recipe.Active = false;
        recipe.IsDefault = false;

        var replacement = await repository.FindActiveReplacementRecipeAsync(recipe.MenuId, recipeId, cancellationToken);
        if (replacement is not null)
        {
            await SetDefaultRecipeAsync(repository, replacement, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>기본 레시피 지정. 활성 레시피만 가능하며, 이전 기본은 해제된다.</summary>
    public async Task SetDefaultRecipeAsync(int recipeId, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var recipe = await repository.GetRecipeAsync(recipeId, cancellationToken);
        if (recipe is null || !recipe.Active)
        {
            throw new MasterDataNotFoundException("사용 가능한 레시피를 찾을 수 없습니다.");
        }

        await SetDefaultRecipeAsync(repository, recipe, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    // =======================================================================
    // MealTypeSetting
    // =======================================================================

    public async Task<List<MealTypeSettingDto>> GetMealTypeSettingsAsync(CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        var rows = await repository.GetMealTypeSettingsAsync(cancellationToken);
        return rows.Select(MapMealTypeSetting).ToList();
    }

    public async Task<List<MealTypeSettingDto>> UpdateMealTypeSettingsAsync(
        IReadOnlyList<MealTypeSettingInput> items, CancellationToken cancellationToken = default)
    {
        using var repository = CreateRepository();
        foreach (var item in items)
        {
            if (item.DefaultPlannedCount < 0)
            {
                throw new InvalidPlannedCountException();
            }

            var normalizedTime = TimeInput24.Normalize(item.DefaultServiceTime)
                ?? throw new InvalidTimeFormatException();

            var row = await repository.FindMealTypeSettingByCodeAsync(item.Code, cancellationToken)
                ?? throw new MealTypeNotFoundException(item.Code);

            row.DefaultPlannedCount = item.DefaultPlannedCount;
            row.DefaultServiceTime = TimeOnly.Parse(normalizedTime);
            row.Active = item.IsActive;
            row.SortOrder = item.SortOrder;
            row.Description = item.Description;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return await GetMealTypeSettingsAsync(cancellationToken);
    }

    // =======================================================================
    // 내부 헬퍼
    // =======================================================================

    /// <summary>
    /// 레시피 재료 항목 해석.
    /// 1) ingredient_id → 재료 조회
    /// 2) 재료명 → 대소문자 무시 조회
    /// 3) 없으면 자동 생성 (StatGroup="기타", ReviewStatus="자동등록-분류필요", Active=true, DefaultUnit=항목 단위)
    /// 4) 그래도 없으면 오류
    /// 5) 같은 재료 중복이면 오류
    /// </summary>
    private async Task<List<(Ingredient Ingredient, RecipeItemInput Item)>> ResolveRecipeItemsAsync(
        IMasterDataRepository repository, IReadOnlyList<RecipeItemInput> items, CancellationToken cancellationToken)
    {
        var resolved = new List<(Ingredient, RecipeItemInput)>();
        var seen = new HashSet<int>();

        foreach (var item in items)
        {
            Ingredient? ingredient = null;
            if (item.IngredientId is not null)
            {
                ingredient = await repository.GetIngredientAsync(item.IngredientId.Value, cancellationToken);
            }

            var name = (item.IngredientName ?? string.Empty).Trim();
            if (ingredient is null && name.Length > 0)
            {
                ingredient = await repository.FindIngredientByNameAsync(name, cancellationToken);
            }

            if (ingredient is null && name.Length > 0)
            {
                ingredient = new Ingredient
                {
                    Name = name,
                    StatGroup = "기타",
                    DefaultUnit = item.Unit,
                    ReviewStatus = "자동등록-분류필요",
                    Active = true,
                };
                repository.Add(ingredient);
                await repository.SaveChangesAsync(cancellationToken);
            }

            if (ingredient is null)
            {
                throw new RecipeIngredientNotFoundException();
            }

            if (!seen.Add(ingredient.Id))
            {
                throw new DuplicateRecipeIngredientException(ingredient.Name);
            }

            resolved.Add((ingredient, item));
        }

        return resolved;
    }

    /// <summary>
    /// 레시피 재료 전체 교체 (기존 행 삭제 후 재생성).
    /// 단위가 비어 있으면 재료 기본단위를 사용한다.
    /// </summary>
    private static void ReplaceRecipeItems(Recipe recipe, List<(Ingredient Ingredient, RecipeItemInput Item)> resolved)
    {
        recipe.Ingredients.Clear();
        for (var index = 0; index < resolved.Count; index++)
        {
            var (ingredient, item) = resolved[index];
            recipe.Ingredients.Add(new RecipeIngredient
            {
                IngredientId = ingredient.Id,
                SortOrder = index + 1,
                QuantityPer100 = item.QuantityPer100,
                Unit = item.Unit ?? ingredient.DefaultUnit,
                IsPrimary = item.IsPrimary,
            });
        }
    }

    /// <summary>메뉴의 모든 레시피 중 대상만 IsDefault=true로 설정한다.</summary>
    private async Task SetDefaultRecipeAsync(IMasterDataRepository repository, Recipe recipe, CancellationToken cancellationToken)
    {
        var siblings = await repository.GetRecipesByMenuAsync(recipe.MenuId, cancellationToken);
        foreach (var sibling in siblings)
        {
            sibling.IsDefault = sibling.Id == recipe.Id;
        }
    }

    // =======================================================================
    // DTO 매핑
    // =======================================================================

    private static MenuDto MapMenu(Menu menu)
    {
        var activeRecipes = menu.Recipes.Where(r => r.Active).ToList();
        var defaultRecipe = activeRecipes.FirstOrDefault(r => r.IsDefault);
        return new MenuDto(
            menu.Id,
            menu.Name,
            menu.CanonicalName,
            menu.Role,
            menu.Active,
            menu.ReviewStatus,
            activeRecipes.Count,
            defaultRecipe?.Id ?? activeRecipes.FirstOrDefault()?.Id);
    }

    private static MenuListItemDto MapMenuListItem(Menu menu)
    {
        var activeRecipes = menu.Recipes.Where(r => r.Active).ToList();
        var defaultRecipe = activeRecipes.FirstOrDefault(r => r.IsDefault);
        return new MenuListItemDto(
            menu.Id,
            menu.Name,
            menu.Role,
            menu.Active,
            activeRecipes.Count,
            defaultRecipe?.Id ?? activeRecipes.FirstOrDefault()?.Id);
    }

    private static MenuDetailDto MapMenuDetail(Menu menu)
        => new(
            MapMenu(menu),
            menu.Recipes.OrderBy(r => r.Version).Select(r => new RecipeListItemDto(
                r.Id, r.Name, r.Version, r.IsDefault, r.Active, r.Ingredients.Count, r.CompositionKey)).ToList());

    private static IngredientDto MapIngredient(Ingredient row)
        => new(
            row.Id,
            row.Name,
            row.StatGroup,
            row.DefaultUnit,
            row.PurchasePackageQuantity,
            row.PurchasePackageUnit,
            row.KgFactor,
            row.AnalysisExcluded,
            row.Active,
            row.ReviewStatus);

    private static RecipeDto MapRecipe(Recipe recipe)
        => new(
            recipe.Id,
            recipe.MenuId,
            recipe.Name,
            recipe.Version,
            recipe.CompositionKey,
            recipe.Note,
            recipe.IsDefault,
            recipe.Active,
            recipe.Ingredients.OrderBy(i => i.SortOrder).Select(i => new RecipeIngredientDto(
                i.Id,
                i.IngredientId,
                i.Ingredient?.Name ?? string.Empty,
                i.Ingredient?.StatGroup ?? string.Empty,
                i.QuantityPer100,
                i.Unit,
                i.IsPrimary,
                i.SortOrder)).ToList());

    private static MealTypeSettingDto MapMealTypeSetting(MealTypeSetting row)
        => new(
            row.Id,
            row.Code,
            row.Name,
            row.DefaultPlannedCount,
            row.DefaultServiceTime?.ToString("HH:mm") ?? string.Empty,
            row.SortOrder,
            row.Active,
            row.Description);
}

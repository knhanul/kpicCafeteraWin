using KpicCafeteria.Domain.Entities;

namespace KpicCafeteria.Application.Abstractions.Repositories;

/// <summary>
/// 기준정보(메뉴/재료/별칭/레시피/배식유형) 리포지토리.
/// 과도한 범용화 없이 현재 업무에 필요한 만큼만 정의한다.
/// 리포지토리는 DbContext를 소유하므로 사용 후 Dispose해야 한다.
/// </summary>
public interface IMasterDataRepository : IDisposable
{
    // ---- Menu ----
    Task<Menu?> GetMenuAsync(int id, CancellationToken cancellationToken = default);

    Task<Menu?> FindMenuByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<List<Menu>> SearchMenusAsync(string? query, string? role, bool? active, int limit, int offset, CancellationToken cancellationToken = default);

    Task<int> CountMenusAsync(string? query, string? role, bool? active, CancellationToken cancellationToken = default);

    // ---- Ingredient ----
    Task<Ingredient?> GetIngredientAsync(int id, CancellationToken cancellationToken = default);

    Task<Ingredient?> FindIngredientByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<List<Ingredient>> SearchIngredientsAsync(string? query, string? statGroup, bool? active, int limit, int offset, CancellationToken cancellationToken = default);

    Task<int> CountIngredientsAsync(string? query, string? statGroup, bool? active, CancellationToken cancellationToken = default);

    // ---- Recipe ----
    Task<Recipe?> GetRecipeAsync(int id, CancellationToken cancellationToken = default);

    Task<Recipe?> FindRecipeByCompositionAsync(int menuId, string compositionKey, int? excludeRecipeId, CancellationToken cancellationToken = default);

    Task<List<Recipe>> GetRecipesByMenuAsync(int menuId, CancellationToken cancellationToken = default);

    Task<int> GetMaxRecipeVersionAsync(int menuId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveRecipeAsync(int menuId, CancellationToken cancellationToken = default);

    Task<Recipe?> FindActiveReplacementRecipeAsync(int menuId, int excludeRecipeId, CancellationToken cancellationToken = default);

    // ---- Excel 아카이브용 전체 조회 ----
    Task<List<Menu>> GetAllMenusAsync(CancellationToken cancellationToken = default);

    Task<List<Ingredient>> GetAllIngredientsAsync(CancellationToken cancellationToken = default);

    Task<List<Recipe>> GetAllRecipesWithDetailsAsync(CancellationToken cancellationToken = default);

    // ---- Alias ----
    Task<IngredientAlias?> FindAliasAsync(string alias, CancellationToken cancellationToken = default);

    Task<IngredientAlias?> GetAliasAsync(int id, CancellationToken cancellationToken = default);

    // ---- MealTypeSetting ----
    Task<List<MealTypeSetting>> GetMealTypeSettingsAsync(CancellationToken cancellationToken = default);

    Task<MealTypeSetting?> FindMealTypeSettingByCodeAsync(string code, CancellationToken cancellationToken = default);

    // ---- Mutation ----
    void Add(Menu menu);

    void Add(Ingredient ingredient);

    void Add(Recipe recipe);

    void Add(IngredientAlias alias);

    void Remove(IngredientAlias alias);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ---- Transaction ----
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

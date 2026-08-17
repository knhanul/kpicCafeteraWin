using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;

namespace KpicCafeteria.Application.Abstractions.Repositories;

/// <summary>
/// 식단(배식/식단 메뉴/보존식/실제 식수) 리포지토리.
/// 리포지토리는 DbContext를 소유하므로 사용 후 Dispose해야 한다.
/// </summary>
public interface IMealServiceRepository : IDisposable
{
    // ---- MealService ----
    Task<MealService?> GetServiceAsync(int id, CancellationToken cancellationToken = default);

    Task<MealService?> FindServiceAsync(DateOnly serviceDate, MealType mealType, CancellationToken cancellationToken = default);

    Task<List<MealService>> GetServicesInRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>문서 출력용: ID 목록으로 메뉴/재료/보존식까지 로드.</summary>
    Task<List<MealService>> GetServicesByIdsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default);

    /// <summary>문서 출력용: 기간 내 메뉴/재료/보존식까지 로드.</summary>
    Task<List<MealService>> GetServicesWithDetailsInRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    // ---- 기준정보 참조 ----
    Task<Menu?> GetMenuWithRecipesAsync(int id, CancellationToken cancellationToken = default);

    Task<List<Menu>> GetMenusWithRecipesAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default);

    Task<Recipe?> GetRecipeAsync(int id, CancellationToken cancellationToken = default);

    Task<Ingredient?> GetIngredientAsync(int id, CancellationToken cancellationToken = default);

    Task<MealTypeSetting?> FindActiveMealTypeSettingAsync(string code, CancellationToken cancellationToken = default);

    Task<List<Menu>> SearchMenusWithRecipesAsync(string? query, string? role, bool? active, int limit, int offset, CancellationToken cancellationToken = default);

    Task<int> CountMenusAsync(string? query, string? role, bool? active, CancellationToken cancellationToken = default);

    // ---- MealServiceMenu ----
    Task<MealServiceMenu?> GetServiceMenuWithMenuRecipesAsync(int id, CancellationToken cancellationToken = default);

    // ---- Mutation ----
    void Add(MealService service);

    void Add(MealServiceMenu menu);

    void Add(PreservationRecord record);

    void Add(MealActual actual);

    void Remove(MealService service);

    void Remove(MealServiceMenu menu);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ---- Transaction ----
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

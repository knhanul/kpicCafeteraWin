using KpicCafeteria.Domain.Entities;

namespace KpicCafeteria.Application.Abstractions.Repositories;

/// <summary>
/// 발주 리포지토리.
/// 리포지토리는 DbContext를 소유하므로 사용 후 Dispose해야 한다.
/// </summary>
public interface IOrderRepository : IDisposable
{
    // ---- 조회 ----
    /// <summary>기간 내 식단(메뉴/재료 스냅샷/재료 참조) 로드. 발주 집계의 원본.</summary>
    Task<List<MealService>> GetServicesWithIngredientsInRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>기간 내 저장된 발주 항목 로드 (OrderGroup 포함).</summary>
    Task<List<OrderItem>> GetItemsInRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);

    /// <summary>배식유형 설정 전체 로드 (코드→이름 변환용).</summary>
    Task<List<MealTypeSetting>> GetMealTypeSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// upsert 키로 기존 항목 조회.
    /// IngredientId가 있으면 (service_date, ingredient_id), 없으면 (service_date, 재료명 스냅샷) 기준.
    /// </summary>
    Task<OrderItem?> FindItemAsync(DateOnly serviceDate, int? ingredientId, string ingredientName, CancellationToken cancellationToken = default);

    // ---- Mutation ----
    void Add(OrderItem item);

    void Add(OrderGroup group);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    // ---- Transaction ----
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

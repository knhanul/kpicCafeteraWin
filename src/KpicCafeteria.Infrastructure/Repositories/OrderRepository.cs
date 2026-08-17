using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KpicCafeteria.Infrastructure.Repositories;

/// <summary>
/// 발주 리포지토리 구현.
/// 작업 단위별로 새 DbContext를 주입받아 사용한다 (장기 보유 금지).
/// </summary>
public sealed class OrderRepository : IOrderRepository
{
    private readonly CafeteriaDbContext _db;
    private IDbContextTransaction? _transaction;

    public OrderRepository(CafeteriaDbContext db)
    {
        _db = db;
    }

    public Task<List<MealService>> GetServicesWithIngredientsInRangeAsync(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        => _db.MealServices
            .AsNoTracking()
            .Include(x => x.Menus).ThenInclude(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .Where(x => x.ServiceDate >= startDate && x.ServiceDate <= endDate)
            .OrderBy(x => x.ServiceDate).ThenBy(x => x.MealType)
            .ToListAsync(cancellationToken);

    public Task<List<OrderItem>> GetItemsInRangeAsync(
        DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        => _db.OrderItems
            .AsNoTracking()
            .Include(x => x.OrderGroup)
            .Include(x => x.Ingredient)
            .Where(x => x.ServiceDate >= startDate && x.ServiceDate <= endDate)
            .ToListAsync(cancellationToken);

    public Task<List<MealTypeSetting>> GetMealTypeSettingsAsync(CancellationToken cancellationToken = default)
        => _db.MealTypeSettings.AsNoTracking().ToListAsync(cancellationToken);

    public Task<OrderItem?> FindItemAsync(
        DateOnly serviceDate, int? ingredientId, string ingredientName, CancellationToken cancellationToken = default)
    {
        if (ingredientId is int id)
        {
            return _db.OrderItems.FirstOrDefaultAsync(
                x => x.ServiceDate == serviceDate && x.IngredientId == id, cancellationToken);
        }

        return _db.OrderItems.FirstOrDefaultAsync(
            x => x.ServiceDate == serviceDate && x.IngredientId == null && x.IngredientNameSnapshot == ingredientName,
            cancellationToken);
    }

    public void Add(OrderItem item) => _db.OrderItems.Add(item);

    public void Add(OrderGroup group) => _db.OrderGroups.Add(group);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
        => _transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void Dispose() => _db.Dispose();
}

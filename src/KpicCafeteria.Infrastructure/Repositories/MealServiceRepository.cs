using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KpicCafeteria.Infrastructure.Repositories;

/// <summary>
/// 식단 리포지토리 구현.
/// 작업 단위별로 새 DbContext를 주입받아 사용한다 (장기 보유 금지).
/// </summary>
public sealed class MealServiceRepository : IMealServiceRepository
{
    private readonly CafeteriaDbContext _db;
    private IDbContextTransaction? _transaction;

    public MealServiceRepository(CafeteriaDbContext db)
    {
        _db = db;
    }

    public Task<MealService?> GetServiceAsync(int id, CancellationToken cancellationToken = default)
        => _db.MealServices
            .Include(x => x.Menus).ThenInclude(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .Include(x => x.Menus).ThenInclude(x => x.Menu)
            .Include(x => x.Menus).ThenInclude(x => x.SourceRecipe)
            .Include(x => x.Preservation)
            .Include(x => x.Actual)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<MealService?> FindServiceAsync(DateOnly serviceDate, MealType mealType, CancellationToken cancellationToken = default)
        => _db.MealServices.FirstOrDefaultAsync(
            x => x.ServiceDate == serviceDate && x.MealType == mealType, cancellationToken);

    public Task<List<MealService>> GetServicesInRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        => _db.MealServices
            .AsNoTracking()
            .Include(x => x.Menus)
            .Include(x => x.Preservation)
            .Include(x => x.Actual)
            .Where(x => x.ServiceDate >= startDate && x.ServiceDate <= endDate)
            .OrderBy(x => x.ServiceDate).ThenBy(x => x.MealType)
            .ToListAsync(cancellationToken);

    public Task<List<MealService>> GetServicesByIdsAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
        => _db.MealServices
            .AsNoTracking()
            .Include(x => x.Menus).ThenInclude(x => x.Ingredients)
            .Include(x => x.Preservation)
            .Include(x => x.Actual)
            .Where(x => ids.Contains(x.Id))
            .OrderBy(x => x.ServiceDate).ThenBy(x => x.MealType)
            .ToListAsync(cancellationToken);

    public Task<List<MealService>> GetServicesWithDetailsInRangeAsync(DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
        => _db.MealServices
            .AsNoTracking()
            .Include(x => x.Menus).ThenInclude(x => x.Ingredients)
            .Include(x => x.Preservation)
            .Include(x => x.Actual)
            .Where(x => x.ServiceDate >= startDate && x.ServiceDate <= endDate)
            .OrderBy(x => x.ServiceDate).ThenBy(x => x.MealType)
            .ToListAsync(cancellationToken);

    public Task<Menu?> GetMenuWithRecipesAsync(int id, CancellationToken cancellationToken = default)
        => _db.Menus
            .Include(x => x.Recipes).ThenInclude(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<List<Menu>> GetMenusWithRecipesAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken = default)
        => _db.Menus
            .Include(x => x.Recipes).ThenInclude(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .Where(x => ids.Contains(x.Id))
            .ToListAsync(cancellationToken);

    public Task<Recipe?> GetRecipeAsync(int id, CancellationToken cancellationToken = default)
        => _db.Recipes
            .Include(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Ingredient?> GetIngredientAsync(int id, CancellationToken cancellationToken = default)
        => _db.Ingredients.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<MealTypeSetting?> FindActiveMealTypeSettingAsync(string code, CancellationToken cancellationToken = default)
        => _db.MealTypeSettings.FirstOrDefaultAsync(
            x => x.Code == code && x.Active, cancellationToken);

    public Task<List<Menu>> SearchMenusWithRecipesAsync(string? query, string? role, bool? active, int limit, int offset, CancellationToken cancellationToken = default)
    {
        var stmt = _db.Menus.AsNoTracking()
            .Include(x => x.Recipes).ThenInclude(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = $"%{query.Trim()}%";
            stmt = stmt.Where(x => EF.Functions.Like(x.Name, term) || EF.Functions.Like(x.CanonicalName, term));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            stmt = stmt.Where(x => x.Role == role);
        }

        if (active is not null)
        {
            stmt = stmt.Where(x => x.Active == active);
        }

        return stmt.OrderBy(x => x.Name).Skip(offset).Take(limit).ToListAsync(cancellationToken);
    }

    public Task<int> CountMenusAsync(string? query, string? role, bool? active, CancellationToken cancellationToken = default)
    {
        var stmt = _db.Menus.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = $"%{query.Trim()}%";
            stmt = stmt.Where(x => EF.Functions.Like(x.Name, term) || EF.Functions.Like(x.CanonicalName, term));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            stmt = stmt.Where(x => x.Role == role);
        }

        if (active is not null)
        {
            stmt = stmt.Where(x => x.Active == active);
        }

        return stmt.CountAsync(cancellationToken);
    }

    public Task<MealServiceMenu?> GetServiceMenuWithMenuRecipesAsync(int id, CancellationToken cancellationToken = default)
        => _db.MealServiceMenus
            .Include(x => x.Ingredients)
            .Include(x => x.Service!).ThenInclude(x => x.Menus)
            .Include(x => x.Menu!).ThenInclude(x => x.Recipes).ThenInclude(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public void Add(MealService service) => _db.MealServices.Add(service);

    public void Add(MealServiceMenu menu) => _db.MealServiceMenus.Add(menu);

    public void Add(PreservationRecord record) => _db.PreservationRecords.Add(record);

    public void Add(MealActual actual) => _db.MealActuals.Add(actual);

    public void Remove(MealService service) => _db.MealServices.Remove(service);

    public void Remove(MealServiceMenu menu) => _db.MealServiceMenus.Remove(menu);

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

    public void Dispose()
    {
        _transaction?.Dispose();
        _db.Dispose();
    }
}

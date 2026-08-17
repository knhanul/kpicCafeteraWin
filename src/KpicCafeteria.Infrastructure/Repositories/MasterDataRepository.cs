using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace KpicCafeteria.Infrastructure.Repositories;

/// <summary>
/// 기준정보 리포지토리 구현.
/// 작업 단위별로 새 DbContext를 주입받아 사용한다 (장기 보유 금지).
/// </summary>
public sealed class MasterDataRepository : IMasterDataRepository
{
    private readonly CafeteriaDbContext _db;
    private IDbContextTransaction? _transaction;

    public MasterDataRepository(CafeteriaDbContext db)
    {
        _db = db;
    }

    // ---- Menu ----

    public Task<Menu?> GetMenuAsync(int id, CancellationToken cancellationToken = default)
        => _db.Menus
            .Include(x => x.Recipes).ThenInclude(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Menu?> FindMenuByNameAsync(string name, CancellationToken cancellationToken = default)
        => _db.Menus.FirstOrDefaultAsync(x => x.Name == name, cancellationToken);

    public Task<List<Menu>> SearchMenusAsync(string? query, string? role, bool? active, int limit, int offset, CancellationToken cancellationToken = default)
    {
        var stmt = _db.Menus.AsNoTracking().AsQueryable();
        stmt = ApplyMenuFilters(stmt, query, role, active);
        return stmt.OrderBy(x => x.Name)
            .Skip(offset).Take(limit)
            .Select(x => new Menu
            {
                Id = x.Id,
                Name = x.Name,
                Role = x.Role,
                Active = x.Active,
                CanonicalName = x.CanonicalName,
                ReviewStatus = x.ReviewStatus,
                Recipes = x.Recipes.Select(r => new Recipe { Id = r.Id, IsDefault = r.IsDefault, Active = r.Active }).ToList(),
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountMenusAsync(string? query, string? role, bool? active, CancellationToken cancellationToken = default)
    {
        var stmt = _db.Menus.AsNoTracking().AsQueryable();
        stmt = ApplyMenuFilters(stmt, query, role, active);
        return stmt.CountAsync(cancellationToken);
    }

    private static IQueryable<Menu> ApplyMenuFilters(IQueryable<Menu> stmt, string? query, string? role, bool? active)
    {
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

        return stmt;
    }

    // ---- Ingredient ----

    public Task<Ingredient?> GetIngredientAsync(int id, CancellationToken cancellationToken = default)
        => _db.Ingredients
            .Include(x => x.Aliases)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Ingredient?> FindIngredientByNameAsync(string name, CancellationToken cancellationToken = default)
        => _db.Ingredients.FirstOrDefaultAsync(x => x.Name.ToLower() == name.ToLower(), cancellationToken);

    public Task<List<Ingredient>> SearchIngredientsAsync(string? query, string? statGroup, bool? active, int limit, int offset, CancellationToken cancellationToken = default)
    {
        var stmt = _db.Ingredients.AsNoTracking().AsQueryable();
        stmt = ApplyIngredientFilters(stmt, query, statGroup, active);
        return stmt.OrderBy(x => x.Name)
            .Skip(offset).Take(limit)
            .Select(x => new Ingredient
            {
                Id = x.Id,
                Name = x.Name,
                StatGroup = x.StatGroup,
                DefaultUnit = x.DefaultUnit,
                KgFactor = x.KgFactor,
                AnalysisExcluded = x.AnalysisExcluded,
                Active = x.Active,
                ReviewStatus = x.ReviewStatus,
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountIngredientsAsync(string? query, string? statGroup, bool? active, CancellationToken cancellationToken = default)
    {
        var stmt = _db.Ingredients.AsNoTracking().AsQueryable();
        stmt = ApplyIngredientFilters(stmt, query, statGroup, active);
        return stmt.CountAsync(cancellationToken);
    }

    private static IQueryable<Ingredient> ApplyIngredientFilters(IQueryable<Ingredient> stmt, string? query, string? statGroup, bool? active)
    {
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = $"%{query.Trim()}%";
            stmt = stmt.Where(x =>
                EF.Functions.Like(x.Name, term) ||
                x.Aliases.Any(a => EF.Functions.Like(a.Alias, term)));
        }

        if (!string.IsNullOrWhiteSpace(statGroup))
        {
            stmt = stmt.Where(x => x.StatGroup == statGroup);
        }

        if (active is not null)
        {
            stmt = stmt.Where(x => x.Active == active);
        }

        return stmt;
    }

    // ---- Recipe ----

    public Task<Recipe?> GetRecipeAsync(int id, CancellationToken cancellationToken = default)
        => _db.Recipes
            .Include(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Recipe?> FindRecipeByCompositionAsync(int menuId, string compositionKey, int? excludeRecipeId, CancellationToken cancellationToken = default)
    {
        var stmt = _db.Recipes.Where(x => x.MenuId == menuId && x.CompositionKey == compositionKey);
        if (excludeRecipeId is not null)
        {
            stmt = stmt.Where(x => x.Id != excludeRecipeId);
        }

        return stmt.FirstOrDefaultAsync(cancellationToken);
    }

    public Task<List<Recipe>> GetRecipesByMenuAsync(int menuId, CancellationToken cancellationToken = default)
        => _db.Recipes
            .Where(x => x.MenuId == menuId)
            .OrderBy(x => x.Version)
            .ToListAsync(cancellationToken);

    public async Task<int> GetMaxRecipeVersionAsync(int menuId, CancellationToken cancellationToken = default)
        => (await _db.Recipes.Where(x => x.MenuId == menuId)
            .Select(x => (int?)x.Version)
            .MaxAsync(cancellationToken)) ?? 0;

    public Task<bool> HasActiveRecipeAsync(int menuId, CancellationToken cancellationToken = default)
        => _db.Recipes.AnyAsync(x => x.MenuId == menuId && x.Active, cancellationToken);

    public Task<Recipe?> FindActiveReplacementRecipeAsync(int menuId, int excludeRecipeId, CancellationToken cancellationToken = default)
        => _db.Recipes
            .Where(x => x.MenuId == menuId && x.Id != excludeRecipeId && x.Active)
            .OrderBy(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);

    // ---- Excel 아카이브용 전체 조회 ----

    public Task<List<Menu>> GetAllMenusAsync(CancellationToken cancellationToken = default)
        => _db.Menus.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<List<Ingredient>> GetAllIngredientsAsync(CancellationToken cancellationToken = default)
        => _db.Ingredients.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task<List<Recipe>> GetAllRecipesWithDetailsAsync(CancellationToken cancellationToken = default)
        => _db.Recipes
            .AsNoTracking()
            .Include(x => x.Menu)
            .Include(x => x.Ingredients).ThenInclude(x => x.Ingredient)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    // ---- Alias ----

    public Task<IngredientAlias?> FindAliasAsync(string alias, CancellationToken cancellationToken = default)
        => _db.IngredientAliases.FirstOrDefaultAsync(x => x.Alias == alias, cancellationToken);

    public Task<IngredientAlias?> GetAliasAsync(int id, CancellationToken cancellationToken = default)
        => _db.IngredientAliases.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    // ---- MealTypeSetting ----

    public Task<List<MealTypeSetting>> GetMealTypeSettingsAsync(CancellationToken cancellationToken = default)
        => _db.MealTypeSettings
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public Task<MealTypeSetting?> FindMealTypeSettingByCodeAsync(string code, CancellationToken cancellationToken = default)
        => _db.MealTypeSettings.FirstOrDefaultAsync(x => x.Code == code, cancellationToken);

    // ---- Mutation ----

    public void Add(Menu menu) => _db.Menus.Add(menu);

    public void Add(Ingredient ingredient) => _db.Ingredients.Add(ingredient);

    public void Add(Recipe recipe) => _db.Recipes.Add(recipe);

    public void Add(IngredientAlias alias) => _db.IngredientAliases.Add(alias);

    public void Remove(IngredientAlias alias) => _db.IngredientAliases.Remove(alias);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);

    // ---- Transaction ----

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

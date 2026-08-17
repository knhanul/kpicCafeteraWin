using KpicCafeteria.Application.Statistics;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Infrastructure.Persistence;

namespace KpicCafeteria.Tests.TestInfrastructure;

/// <summary>
/// 통계 서비스 테스트용 하네스.
/// 실제 SQLite 엔진(in-memory)을 사용하며, 서비스 작업마다 새 DbContext를 생성한다.
/// </summary>
public sealed class StatisticsTestHarness : IDisposable
{
    private readonly SqliteTestDatabase _database;

    public StatisticsTestHarness()
    {
        _database = new SqliteTestDatabase();
    }

    public MealStatisticsService CreateMealStatisticsService()
        => new(new TestStatisticsRepositoryFactory(_database.Connection));

    public MenuStatisticsService CreateMenuStatisticsService()
        => new(new TestStatisticsRepositoryFactory(_database.Connection));

    public IngredientStatisticsService CreateIngredientStatisticsService()
        => new(new TestStatisticsRepositoryFactory(_database.Connection));

    public OperationsStatisticsService CreateOperationsStatisticsService()
        => new(new TestStatisticsRepositoryFactory(_database.Connection));

    public DashboardService CreateDashboardService()
        => new(
            new TestStatisticsRepositoryFactory(_database.Connection),
            CreateMealStatisticsService(),
            CreateMenuStatisticsService(),
            CreateOperationsStatisticsService());

    public CafeteriaDbContext CreateContext() => _database.CreateContext();

    public void Dispose() => _database.Dispose();
}

/// <summary>
/// 통계 테스트용 데이터 작성기.
/// 실제 엔티티를 저장하므로 Snapshot/기준정보 관계를 그대로 검증할 수 있다.
/// </summary>
public sealed class StatisticsFixture
{
    private readonly CafeteriaDbContext _db;
    private readonly List<Menu> _menus = [];
    private readonly List<Ingredient> _ingredients = [];
    private readonly List<MealService> _services = [];

    public StatisticsFixture(CafeteriaDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<Menu> Menus => _menus;

    public IReadOnlyList<Ingredient> Ingredients => _ingredients;

    public IReadOnlyList<MealService> Services => _services;

    /// <summary>메뉴 기준정보 추가.</summary>
    public Menu AddMenu(string name, string role = "주찬", string canonicalName = "", bool active = true)
    {
        var menu = new Menu
        {
            Name = name,
            CanonicalName = string.IsNullOrEmpty(canonicalName) ? name : canonicalName,
            Role = role,
            Active = active,
        };
        _db.Menus.Add(menu);
        _menus.Add(menu);
        return menu;
    }

    /// <summary>식재료 기준정보 추가.</summary>
    public Ingredient AddIngredient(
        string name, string statGroup = "육류", string? defaultUnit = "kg", double? kgFactor = null,
        bool analysisExcluded = false, bool active = true)
    {
        var ingredient = new Ingredient
        {
            Name = name,
            StatGroup = statGroup,
            DefaultUnit = defaultUnit,
            KgFactor = kgFactor,
            AnalysisExcluded = analysisExcluded,
            Active = active,
        };
        _db.Ingredients.Add(ingredient);
        _ingredients.Add(ingredient);
        return ingredient;
    }

    /// <summary>배식 추가 (메뉴/재료 스냅샷 포함).</summary>
    public MealService AddService(
        DateOnly date,
        MealType mealType,
        int plannedCount,
        int? actualCount = null,
        DateTime? recordedAt = null,
        DateTime? mealPlanOutputAt = null,
        DateTime? cookingOutputAt = null,
        bool preservationCompleted = false,
        bool preservationCollected = false,
        bool preservationDisposed = false,
        string? preservationManager = null,
        string? preservationTemperature = null,
        params (Menu? Menu, string MenuName, string Role, (Ingredient? Ingredient, string Name, double? QuantityTotal, double? QuantityPer100, string? Unit)[] Ingredients)[] menuItems)
    {
        var service = new MealService
        {
            ServiceDate = date,
            MealType = mealType,
            PlannedCount = plannedCount,
            MealPlanOutputAt = ToUtc(mealPlanOutputAt),
            CookingOutputAt = ToUtc(cookingOutputAt),
        };

        var sortOrder = 1;
        foreach (var (menu, menuName, role, ingredients) in menuItems)
        {
            var serviceMenu = new MealServiceMenu
            {
                // Navigation 속성으로 연결: 저장 시점에 FK가 자동 배정된다.
                Menu = menu,
                MenuNameSnapshot = menuName,
                SortOrder = sortOrder++,
                IsRepresentative = sortOrder == 2,
            };
            var ingredientSort = 1;
            foreach (var (ingredient, name, quantityTotal, quantityPer100, unit) in ingredients)
            {
                serviceMenu.Ingredients.Add(new MealServiceMenuIngredient
                {
                    Ingredient = ingredient,
                    IngredientNameSnapshot = name,
                    QuantityTotal = quantityTotal,
                    QuantityPer100 = quantityPer100,
                    Unit = unit,
                    SortOrder = ingredientSort++,
                });
            }

            service.Menus.Add(serviceMenu);
        }

        if (actualCount is not null)
        {
            service.Actual = new MealActual
            {
                ActualCount = actualCount,
                RecordedAt = ToUtc(recordedAt ?? date.ToDateTime(new TimeOnly(12, 0))),
            };
        }

        if (preservationCompleted || preservationCollected || preservationDisposed || preservationManager is not null)
        {
            service.Preservation = new PreservationRecord
            {
                CollectedAt = ToUtc(preservationCollected ? date.ToDateTime(new TimeOnly(10, 0)) : null),
                CompletedAt = ToUtc(preservationCompleted ? date.ToDateTime(new TimeOnly(10, 30)) : null),
                DisposalAt = ToUtc(preservationDisposed ? date.ToDateTime(new TimeOnly(18, 0)) : null),
                ManagerName = preservationManager,
                FreezerTemperature = preservationTemperature,
            };
        }

        _db.MealServices.Add(service);
        _services.Add(service);
        return service;
    }

    /// <summary>모든 데이터를 저장한다.</summary>
    public void Save()
    {
        _db.SaveChanges();
    }

    /// <summary>Unspecified Kind DateTime을 UTC로 정규화 (DB는 UTC 저장).</summary>
    private static DateTime? ToUtc(DateTime? value)
        => value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}

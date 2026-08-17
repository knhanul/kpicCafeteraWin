using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Tests;

/// <summary>
/// 실제 SQLite 엔진에서 스키마/제약/FK 동작 검증.
/// EF Core InMemory Provider는 사용하지 않는다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\models.py
/// </summary>
public class DbContextTests
{
    // ---- DB 생성 ----

    [Fact]
    public void Migrate_CreatesAllTables()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var tables = db.Database.SqlQueryRaw<string>(
            "SELECT name AS value FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name != '__EFMigrationsHistory'")
            .ToList();

        Assert.Contains("meal_type_settings", tables);
        Assert.Contains("menus", tables);
        Assert.Contains("ingredients", tables);
        Assert.Contains("ingredient_aliases", tables);
        Assert.Contains("recipes", tables);
        Assert.Contains("recipe_ingredients", tables);
        Assert.Contains("meal_services", tables);
        Assert.Contains("meal_service_menus", tables);
        Assert.Contains("meal_service_menu_ingredients", tables);
        Assert.Contains("preservation_records", tables);
        Assert.Contains("meal_actuals", tables);
        Assert.Contains("order_items", tables);
        Assert.Contains("order_groups", tables);
        Assert.Contains("document_templates", tables);
        Assert.Contains("import_jobs", tables);
        Assert.Contains("backup_records", tables);
        Assert.Contains("data_archives", tables);
        Assert.Contains("audit_logs", tables);

        // Windows 버전에서 제외한 테이블
        Assert.DoesNotContain("users", tables);
        Assert.DoesNotContain("document_previews", tables);
    }

    // ---- Unique 제약 ----

    [Fact]
    public void MenuName_IsUnique()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        db.Menus.Add(new Menu { Name = "육개장", CanonicalName = "육개장" });
        db.SaveChanges();
        db.Menus.Add(new Menu { Name = "육개장", CanonicalName = "육개장" });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void IngredientName_IsUnique()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        db.Ingredients.Add(new Ingredient { Name = "돼지고기" });
        db.SaveChanges();
        db.Ingredients.Add(new Ingredient { Name = "돼지고기" });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void MealService_DateAndMealType_IsUnique()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var date = new DateOnly(2026, 8, 17);
        db.MealServices.Add(new MealService { ServiceDate = date, MealType = MealType.LUNCH });
        db.SaveChanges();
        db.MealServices.Add(new MealService { ServiceDate = date, MealType = MealType.LUNCH });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void MealService_SameDateDifferentMealType_IsAllowed()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var date = new DateOnly(2026, 8, 17);
        db.MealServices.Add(new MealService { ServiceDate = date, MealType = MealType.LUNCH });
        db.MealServices.Add(new MealService { ServiceDate = date, MealType = MealType.DINNER });

        db.SaveChanges();

        Assert.Equal(2, db.MealServices.Count());
    }

    [Fact]
    public void Recipe_MenuAndVersion_IsUnique()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var menu = new Menu { Name = "제육볶음", CanonicalName = "제육볶음" };
        db.Menus.Add(menu);
        db.SaveChanges();

        db.Recipes.Add(new Recipe { MenuId = menu.Id, Version = 1, CompositionKey = "1" });
        db.SaveChanges();
        db.Recipes.Add(new Recipe { MenuId = menu.Id, Version = 1, CompositionKey = "2" });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void Recipe_MenuAndCompositionKey_IsUnique()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var menu = new Menu { Name = "제육볶음", CanonicalName = "제육볶음" };
        db.Menus.Add(menu);
        db.SaveChanges();

        db.Recipes.Add(new Recipe { MenuId = menu.Id, Version = 1, CompositionKey = "1,4,8" });
        db.SaveChanges();
        db.Recipes.Add(new Recipe { MenuId = menu.Id, Version = 2, CompositionKey = "1,4,8" });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    [Fact]
    public void IngredientAlias_Alias_IsUnique()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var ingredient = new Ingredient { Name = "돼지고기" };
        db.Ingredients.Add(ingredient);
        db.SaveChanges();

        db.IngredientAliases.Add(new IngredientAlias { Alias = "돈육", IngredientId = ingredient.Id });
        db.SaveChanges();
        db.IngredientAliases.Add(new IngredientAlias { Alias = "돈육", IngredientId = ingredient.Id });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    // ---- FK Cascade (DB 레벨) ----

    [Fact]
    public void DeleteMealService_CascadesToChildren()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var service = new MealService
        {
            ServiceDate = new DateOnly(2026, 8, 17),
            MealType = MealType.LUNCH,
            Menus =
            [
                new MealServiceMenu
                {
                    MenuNameSnapshot = "제육볶음",
                    Ingredients = [new MealServiceMenuIngredient { IngredientNameSnapshot = "돼지고기" }],
                },
            ],
            Preservation = new PreservationRecord { ManagerName = "홍길동" },
            Actual = new MealActual { ActualCount = 380 },
        };
        db.MealServices.Add(service);
        db.SaveChanges();

        var serviceId = service.Id;

        // EF 변경 추적을 우회해 DB 레벨에서 삭제 (FK 동작 검증)
        db.Database.ExecuteSqlRaw("DELETE FROM meal_services WHERE id = {0}", serviceId);

        Assert.Equal(0, db.MealServiceMenus.Count());
        Assert.Equal(0, db.MealServiceMenuIngredients.Count());
        Assert.Equal(0, db.PreservationRecords.Count());
        Assert.Equal(0, db.MealActuals.Count());
    }

    [Fact]
    public void DeleteRecipe_CascadesToRecipeIngredients()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var ingredient = new Ingredient { Name = "돼지고기" };
        var menu = new Menu { Name = "제육볶음", CanonicalName = "제육볶음" };
        db.AddRange(ingredient, menu);
        db.SaveChanges();

        var recipe = new Recipe
        {
            MenuId = menu.Id,
            Version = 1,
            CompositionKey = ingredient.Id.ToString(),
            Ingredients = [new RecipeIngredient { IngredientId = ingredient.Id, QuantityPer100 = 10 }],
        };
        db.Recipes.Add(recipe);
        db.SaveChanges();

        var recipeId = recipe.Id;

        db.Database.ExecuteSqlRaw("DELETE FROM recipes WHERE id = {0}", recipeId);

        Assert.Equal(0, db.RecipeIngredients.Count());
    }

    [Fact]
    public void DeleteIngredient_CascadesToAliases()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var ingredient = new Ingredient { Name = "돼지고기" };
        db.Ingredients.Add(ingredient);
        db.SaveChanges();

        db.IngredientAliases.Add(new IngredientAlias { Alias = "돈육", IngredientId = ingredient.Id });
        db.SaveChanges();

        var ingredientId = ingredient.Id;

        db.Database.ExecuteSqlRaw("DELETE FROM ingredients WHERE id = {0}", ingredientId);

        Assert.Equal(0, db.IngredientAliases.Count());
    }

    // ---- FK SET NULL (스냅샷 보존) ----

    [Fact]
    public void DeleteMenu_SetsNullOnMealServiceMenu_AndPreservesSnapshot()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var menu = new Menu { Name = "육개장", CanonicalName = "육개장" };
        db.Menus.Add(menu);
        db.SaveChanges();

        var service = new MealService
        {
            ServiceDate = new DateOnly(2026, 8, 17),
            MealType = MealType.LUNCH,
            Menus = [new MealServiceMenu { MenuId = menu.Id, MenuNameSnapshot = "육개장" }],
        };
        db.MealServices.Add(service);
        db.SaveChanges();

        var menuId = menu.Id;

        db.Database.ExecuteSqlRaw("DELETE FROM menus WHERE id = {0}", menuId);

        // 변경 추적 캐시를 피하기 위해 새 컨텍스트에서 DB 값을 읽는다.
        using var fresh = testDb.CreateContext();
        var snapshot = fresh.MealServiceMenus.Single();
        Assert.Null(snapshot.MenuId);
        Assert.Equal("육개장", snapshot.MenuNameSnapshot);
    }

    [Fact]
    public void DeleteIngredient_SetsNullOnMealServiceMenuIngredient_AndPreservesSnapshot()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var ingredient = new Ingredient { Name = "돼지고기" };
        db.Ingredients.Add(ingredient);
        db.SaveChanges();

        var service = new MealService
        {
            ServiceDate = new DateOnly(2026, 8, 17),
            MealType = MealType.LUNCH,
            Menus =
            [
                new MealServiceMenu
                {
                    MenuNameSnapshot = "제육볶음",
                    Ingredients =
                    [
                        new MealServiceMenuIngredient
                        {
                            IngredientId = ingredient.Id,
                            IngredientNameSnapshot = "돼지고기",
                            QuantityTotal = 40,
                            QuantityPer100 = 10,
                            Unit = "kg",
                        },
                    ],
                },
            ],
        };
        db.MealServices.Add(service);
        db.SaveChanges();

        var ingredientId = ingredient.Id;

        db.Database.ExecuteSqlRaw("DELETE FROM ingredients WHERE id = {0}", ingredientId);

        // 변경 추적 캐시를 피하기 위해 새 컨텍스트에서 DB 값을 읽는다.
        using var fresh = testDb.CreateContext();
        var snapshot = fresh.MealServiceMenuIngredients.Single();
        Assert.Null(snapshot.IngredientId);
        Assert.Equal("돼지고기", snapshot.IngredientNameSnapshot);
        Assert.Equal(40.0, snapshot.QuantityTotal);
        Assert.Equal(10.0, snapshot.QuantityPer100);
        Assert.Equal("kg", snapshot.Unit);
    }

    // ---- OrderItem nullable IngredientId unique 동작 ----

    [Fact]
    public void OrderItem_SameDateNullIngredientId_AllowsMultipleRows()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var date = new DateOnly(2026, 8, 17);
        db.OrderItems.Add(new OrderItem { ServiceDate = date, IngredientId = null, IngredientNameSnapshot = "삭제된재료A" });
        db.OrderItems.Add(new OrderItem { ServiceDate = date, IngredientId = null, IngredientNameSnapshot = "삭제된재료B" });

        // SQLite는 UNIQUE 인덱스에서 NULL을 서로 다른 값으로 취급한다.
        db.SaveChanges();

        Assert.Equal(2, db.OrderItems.Count());
    }

    [Fact]
    public void OrderItem_SameDateSameIngredientId_IsRejected()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        var ingredient = new Ingredient { Name = "돼지고기" };
        db.Ingredients.Add(ingredient);
        db.SaveChanges();

        var date = new DateOnly(2026, 8, 17);
        db.OrderItems.Add(new OrderItem { ServiceDate = date, IngredientId = ingredient.Id, IngredientNameSnapshot = "돼지고기" });
        db.SaveChanges();
        db.OrderItems.Add(new OrderItem { ServiceDate = date, IngredientId = ingredient.Id, IngredientNameSnapshot = "돼지고기" });

        Assert.Throws<DbUpdateException>(() => db.SaveChanges());
    }

    // ---- enum 문자열 저장 ----

    [Fact]
    public void MealType_StoredAsCompatibleString()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        db.MealServices.Add(new MealService { ServiceDate = new DateOnly(2026, 8, 17), MealType = MealType.LUNCH });
        db.SaveChanges();

        var stored = db.Database.SqlQueryRaw<string>("SELECT meal_type AS value FROM meal_services LIMIT 1").Single();
        Assert.Equal("LUNCH", stored);
    }

    [Fact]
    public void OrderStatus_StoredAsCompatibleLowercaseString()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        db.OrderItems.Add(new OrderItem
        {
            ServiceDate = new DateOnly(2026, 8, 17),
            IngredientNameSnapshot = "돼지고기",
            Status = OrderStatus.Ordered,
        });
        db.SaveChanges();

        var stored = db.Database.SqlQueryRaw<string>("SELECT status AS value FROM order_items LIMIT 1").Single();
        Assert.Equal("ordered", stored);
    }
}

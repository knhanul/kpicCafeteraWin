using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// 스냅샷 무결성 검증.
/// 기준 메뉴/레시피/재료가 나중에 수정되어도 과거 식단의 스냅샷은 자동 변경되지 않는다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\workspace.py (_copy_recipe_to_service_menu)
/// C:\Pjt\kpicCafeteria\docs\03-domain-model-analysis.md (스냅샷 패턴)
/// </summary>
public class SnapshotPersistenceTests
{
    [Fact]
    public void MenuNameSnapshot_IsNotAffected_WhenMenuRenamed()
    {
        using var testDb = new SqliteTestDatabase();

        // 1. 식단 추가 당시 메뉴명 "육개장"
        int serviceMenuId;
        using (var db = testDb.CreateContext())
        {
            var menu = new Menu { Name = "육개장", CanonicalName = "육개장" };
            db.Menus.Add(menu);
            db.SaveChanges();

            var service = new MealService
            {
                ServiceDate = new DateOnly(2026, 8, 17),
                MealType = MealType.LUNCH,
                Menus = [new MealServiceMenu { MenuId = menu.Id, MenuNameSnapshot = menu.Name }],
            };
            db.MealServices.Add(service);
            db.SaveChanges();

            serviceMenuId = service.Menus[0].Id;
        }

        // 2. 이후 기준 메뉴명을 "소고기육개장"으로 변경
        using (var db = testDb.CreateContext())
        {
            var menu = db.Menus.Single(x => x.Name == "육개장");
            menu.Name = "소고기육개장";
            db.SaveChanges();
        }

        // 3. 기존 식단 스냅샷은 여전히 "육개장"
        using (var db = testDb.CreateContext())
        {
            var snapshot = db.MealServiceMenus.Single(x => x.Id == serviceMenuId);
            Assert.Equal("육개장", snapshot.MenuNameSnapshot);
        }
    }

    [Fact]
    public void RecipeSnapshot_IsNotAffected_WhenRecipeRenamed()
    {
        using var testDb = new SqliteTestDatabase();

        int serviceMenuId;
        using (var db = testDb.CreateContext())
        {
            var menu = new Menu { Name = "제육볶음", CanonicalName = "제육볶음" };
            db.Menus.Add(menu);
            db.SaveChanges();

            var recipe = new Recipe { MenuId = menu.Id, Version = 1, Name = "기본 레시피", CompositionKey = "EMPTY" };
            db.Recipes.Add(recipe);
            db.SaveChanges();

            var service = new MealService
            {
                ServiceDate = new DateOnly(2026, 8, 17),
                MealType = MealType.LUNCH,
                Menus =
                [
                    new MealServiceMenu
                    {
                        MenuId = menu.Id,
                        MenuNameSnapshot = menu.Name,
                        RecipeId = recipe.Id,
                        RecipeNameSnapshot = recipe.Name,
                        RecipeVersionSnapshot = recipe.Version,
                    },
                ],
            };
            db.MealServices.Add(service);
            db.SaveChanges();

            serviceMenuId = service.Menus[0].Id;
        }

        using (var db = testDb.CreateContext())
        {
            var recipe = db.Recipes.Single();
            recipe.Name = "매운 제육 레시피";
            recipe.Version = 2;
            db.SaveChanges();
        }

        using (var db = testDb.CreateContext())
        {
            var snapshot = db.MealServiceMenus.Single(x => x.Id == serviceMenuId);
            Assert.Equal("기본 레시피", snapshot.RecipeNameSnapshot);
            Assert.Equal(1, snapshot.RecipeVersionSnapshot);
        }
    }

    [Fact]
    public void IngredientSnapshot_IsNotAffected_WhenIngredientRenamed()
    {
        using var testDb = new SqliteTestDatabase();

        int ingredientSnapshotId;
        using (var db = testDb.CreateContext())
        {
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
                                IngredientNameSnapshot = ingredient.Name,
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

            ingredientSnapshotId = service.Menus[0].Ingredients[0].Id;
        }

        using (var db = testDb.CreateContext())
        {
            var ingredient = db.Ingredients.Single(x => x.Name == "돼지고기");
            ingredient.Name = "돈육";
            db.SaveChanges();
        }

        using (var db = testDb.CreateContext())
        {
            var snapshot = db.MealServiceMenuIngredients.Single(x => x.Id == ingredientSnapshotId);
            Assert.Equal("돼지고기", snapshot.IngredientNameSnapshot);
            Assert.Equal(40.0, snapshot.QuantityTotal);
            Assert.Equal(10.0, snapshot.QuantityPer100);
            Assert.Equal("kg", snapshot.Unit);
        }
    }
}

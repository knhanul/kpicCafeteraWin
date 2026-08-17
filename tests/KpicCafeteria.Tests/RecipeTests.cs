using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// 다중 레시피 업무규칙 검증 (A~H 시나리오).
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\tests\test_multi_recipe.py
/// C:\Pjt\kpicCafeteria\backend\app\routers\master.py (create_recipe/update_recipe/archive_recipe)
/// </summary>
public class RecipeTests
{
    private static async Task<int> CreateIngredientAsync(MasterDataService service, string name, string statGroup = "기타", string? unit = "kg")
    {
        var dto = await service.CreateIngredientAsync(new IngredientInput(name, statGroup, unit, null, null, null, false, true));
        return dto.Id;
    }

    private static async Task<int> CreateMenuAsync(MasterDataService service, string name, string role = "주찬")
    {
        var dto = await service.CreateMenuAsync(new MenuInput(name, null, role, true));
        return dto.Id;
    }

    // A. 동일 구성 + 수량 변경 → 새 Recipe 생성 X, 기존 Recipe 수정 O
    [Fact]
    public async Task QuantityOnlyChange_UpdatesExistingRecipe_DoesNotCreateNew()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var meat = await CreateIngredientAsync(service, "돼지고기", "돼지고기");
        var menuId = await CreateMenuAsync(service, "돼지불고기");

        var first = await service.CreateRecipeAsync(menuId, new RecipeInput(
            "기본", null, true, true,
            [new RecipeItemInput(meat, null, 8, null, false)]));

        var updated = await service.UpdateRecipeAsync(first.Id, new RecipeInput(
            "기본", null, true, true,
            [new RecipeItemInput(meat, null, 12, null, false)]));

        // 같은 레시피가 수정된다 (새 레시피 아님)
        Assert.Equal(first.Id, updated.Id);
        Assert.Equal(first.Version, updated.Version);
        Assert.Equal(first.CompositionKey, updated.CompositionKey);
        Assert.Equal(12.0, updated.Ingredients[0].QuantityPer100);

        // 레시피는 여전히 1개
        var detail = await service.GetMenuAsync(menuId);
        Assert.Single(detail.Recipes);
    }

    // B. 다른 구성 → CompositionKey 다름, 새 Recipe 가능
    [Fact]
    public async Task DifferentComposition_CreatesNewRecipe()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var meat = await CreateIngredientAsync(service, "돼지고기", "돼지고기");
        var onion = await CreateIngredientAsync(service, "양파", "채소");
        var soySauce = await CreateIngredientAsync(service, "간장", "장류·소스·조미료");
        var menuId = await CreateMenuAsync(service, "돼지불고기");

        var first = await service.CreateRecipeAsync(menuId, new RecipeInput(
            "기본", null, true, true,
            [new RecipeItemInput(meat, null, 8, null, false), new RecipeItemInput(onion, null, 3, null, false)]));

        var second = await service.CreateRecipeAsync(menuId, new RecipeInput(
            "간장 버전", null, false, true,
            [new RecipeItemInput(meat, null, 8, null, false), new RecipeItemInput(soySauce, null, 2, null, false)]));

        Assert.NotEqual(first.CompositionKey, second.CompositionKey);
        Assert.Equal(2, (await service.GetMenuAsync(menuId)).Recipes.Count);
    }

    // C. 동일 CompositionKey 중복 등록 거부
    [Fact]
    public async Task DuplicateComposition_IsRejected()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var tofu = await CreateIngredientAsync(service, "두부", "두류·두부");
        var menuId = await CreateMenuAsync(service, "두부조림", "부찬");

        await service.CreateRecipeAsync(menuId, new RecipeInput(
            "기본", null, true, true,
            [new RecipeItemInput(tofu, null, 5, null, false)]));

        var ex = await Assert.ThrowsAsync<DuplicateRecipeCompositionException>(() =>
            service.CreateRecipeAsync(menuId, new RecipeInput(
                "중복", null, false, true,
                [new RecipeItemInput(tofu, null, 9, null, false)])));

        Assert.Contains("같은 재료 구성의 레시피가 이미 있습니다", ex.Message);
    }

    // D. Version: 같은 메뉴 순차 증가, 다른 메뉴는 v1부터
    [Fact]
    public async Task Version_IncrementsPerMenu()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var meat = await CreateIngredientAsync(service, "돼지고기", "돼지고기");
        var onion = await CreateIngredientAsync(service, "양파", "채소");
        var menuA = await CreateMenuAsync(service, "메뉴A");
        var menuB = await CreateMenuAsync(service, "메뉴B");

        var a1 = await service.CreateRecipeAsync(menuA, new RecipeInput(null, null, false, true, [new RecipeItemInput(meat, null, 8, null, false)]));
        var a2 = await service.CreateRecipeAsync(menuA, new RecipeInput(null, null, false, true, [new RecipeItemInput(meat, null, 8, null, false), new RecipeItemInput(onion, null, 3, null, false)]));
        var a3 = await service.CreateRecipeAsync(menuA, new RecipeInput(null, null, false, true, [new RecipeItemInput(onion, null, 3, null, false)]));
        var b1 = await service.CreateRecipeAsync(menuB, new RecipeInput(null, null, false, true, [new RecipeItemInput(meat, null, 8, null, false)]));

        Assert.Equal(1, a1.Version);
        Assert.Equal(2, a2.Version);
        Assert.Equal(3, a3.Version);
        Assert.Equal(1, b1.Version);
    }

    // E. Default: 한 메뉴에서 최대 하나
    [Fact]
    public async Task DefaultRecipe_OnlyOnePerMenu()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var meat = await CreateIngredientAsync(service, "돼지고기", "돼지고기");
        var onion = await CreateIngredientAsync(service, "양파", "채소");
        var menuId = await CreateMenuAsync(service, "제육볶음");

        var first = await service.CreateRecipeAsync(menuId, new RecipeInput("기본", null, true, true, [new RecipeItemInput(meat, null, 10, null, false)]));
        var second = await service.CreateRecipeAsync(menuId, new RecipeInput("양파 추가", null, true, true, [new RecipeItemInput(meat, null, 10, null, false), new RecipeItemInput(onion, null, 5, null, false)]));

        // 두 번째 레시피가 기본으로 지정되면 첫 번째는 해제된다.
        var detail = await service.GetMenuAsync(menuId);
        Assert.True(detail.Recipes.Single(r => r.Id == second.Id).IsDefault);
        Assert.False(detail.Recipes.Single(r => r.Id == first.Id).IsDefault);
        Assert.Single(detail.Recipes, r => r.IsDefault);
    }

    // F. Default 비활성 → 다른 활성 레시피 자동 Default
    [Fact]
    public async Task DeactivatingDefaultRecipe_AssignsReplacementDefault()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var meat = await CreateIngredientAsync(service, "돼지고기", "돼지고기");
        var onion = await CreateIngredientAsync(service, "양파", "채소");
        var menuId = await CreateMenuAsync(service, "제육볶음");

        var first = await service.CreateRecipeAsync(menuId, new RecipeInput("기본", null, false, true, [new RecipeItemInput(meat, null, 10, null, false)]));
        var second = await service.CreateRecipeAsync(menuId, new RecipeInput("양파 추가", null, false, true, [new RecipeItemInput(meat, null, 10, null, false), new RecipeItemInput(onion, null, 5, null, false)]));

        // 첫 레시피가 기본 (자동 지정)
        Assert.True(first.IsDefault);

        // 기본 레시피 미사용 처리 → 버전 순 활성 레시피가 대체
        await service.ArchiveRecipeAsync(first.Id);

        var detail = await service.GetMenuAsync(menuId);
        Assert.False(detail.Recipes.Single(r => r.Id == first.Id).IsDefault);
        Assert.True(detail.Recipes.Single(r => r.Id == second.Id).IsDefault);
    }

    // G. 미등록 재료명 → 자동 Ingredient 생성 (StatGroup=기타, ReviewStatus=자동등록-분류필요)
    [Fact]
    public async Task UnknownIngredientName_AutoCreatesIngredient()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var menuId = await CreateMenuAsync(service, "새로운 메뉴");

        var recipe = await service.CreateRecipeAsync(menuId, new RecipeInput(
            "기본", null, false, true,
            [new RecipeItemInput(null, "미등록재료", 3.5, "kg", false)]));

        Assert.Single(recipe.Ingredients);
        Assert.Equal("미등록재료", recipe.Ingredients[0].IngredientName);
        Assert.Equal(3.5, recipe.Ingredients[0].QuantityPer100);
        Assert.Equal("kg", recipe.Ingredients[0].Unit);

        // 자동 생성된 재료 확인
        var search = await service.SearchIngredientsAsync("미등록재료", null, null);
        var created = Assert.Single(search.Items);
        Assert.Equal("기타", created.StatGroup);
        Assert.Equal("자동등록-분류필요", created.ReviewStatus);
        Assert.True(created.Active);
    }

    // H. 한 레시피에 같은 재료 중복 → 저장 거부
    [Fact]
    public async Task DuplicateIngredientInRecipe_IsRejected()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var onion = await CreateIngredientAsync(service, "양파", "채소");
        var meat = await CreateIngredientAsync(service, "돼지고기", "돼지고기");
        var menuId = await CreateMenuAsync(service, "제육볶음");

        var ex = await Assert.ThrowsAsync<DuplicateRecipeIngredientException>(() =>
            service.CreateRecipeAsync(menuId, new RecipeInput(
                "중복", null, false, true,
                [
                    new RecipeItemInput(onion, null, 5, null, false),
                    new RecipeItemInput(meat, null, 10, null, false),
                    new RecipeItemInput(onion, null, 2, null, false),
                ])));

        Assert.Contains("레시피에 같은 재료가 중복되었습니다", ex.Message);
    }

    // 추가: 메뉴 미사용 처리 시 레시피도 함께 미사용 처리
    [Fact]
    public async Task ArchiveMenu_DeactivatesAllRecipes()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var meat = await CreateIngredientAsync(service, "돼지고기", "돼지고기");
        var menuId = await CreateMenuAsync(service, "제육볶음");
        await service.CreateRecipeAsync(menuId, new RecipeInput("기본", null, false, true, [new RecipeItemInput(meat, null, 10, null, false)]));

        await service.ArchiveMenuAsync(menuId);

        var detail = await service.GetMenuAsync(menuId);
        Assert.False(detail.Menu.Active);
        Assert.All(detail.Recipes, r => Assert.False(r.Active));
    }
}

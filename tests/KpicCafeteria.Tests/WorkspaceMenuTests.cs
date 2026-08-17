using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Application.Workspace;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// 메뉴 추가/일괄 추가/스냅샷/레시피 변경/식단 편집/메뉴 선택기 검증.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\tests\test_meal_editor.py
/// C:\Pjt\kpicCafeteria\backend\tests\test_menu_picker.py
/// C:\Pjt\kpicCafeteria\backend\app\routers\workspace.py
/// </summary>
public class WorkspaceMenuTests
{
    private static readonly DateOnly Monday = new(2026, 8, 17);

    private static async Task<int> CreateIngredientAsync(MasterDataService master, string name, string statGroup = "기타", string? unit = "kg")
        => (await master.CreateIngredientAsync(new IngredientInput(name, statGroup, unit, null, null, null, false, true))).Id;

    private static async Task<int> CreateMenuAsync(MasterDataService master, string name, string role = "주찬")
        => (await master.CreateMenuAsync(new MenuInput(name, null, role, true))).Id;

    private static async Task<int> CreateRecipeAsync(MasterDataService master, int menuId, string name, IReadOnlyList<(int IngredientId, double Qty)> items)
    {
        var recipe = await master.CreateRecipeAsync(menuId, new RecipeInput(
            name, null, false, true,
            items.Select(i => new RecipeItemInput(i.IngredientId, null, i.Qty, null, false)).ToList()));
        return recipe.Id;
    }

    // =======================================================================
    // 메뉴 단건 추가 / 스냅샷
    // =======================================================================

    [Fact]
    public async Task AddMenu_CopiesSnapshots()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var pork = await CreateIngredientAsync(master, "돼지고기", "돼지고기");
        var onion = await CreateIngredientAsync(master, "양파", "채소");
        var menuId = await CreateMenuAsync(master, "제육볶음");
        await CreateRecipeAsync(master, menuId, "기본 레시피", [(pork, 10), (onion, 5)]);

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenu = await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, null));

        var menu = Assert.Single(withMenu.Menus);
        Assert.Equal("제육볶음", menu.Name); // MenuNameSnapshot
        Assert.NotNull(menu.RecipeId);
        Assert.Equal("기본 레시피", menu.RecipeName); // RecipeNameSnapshot
        Assert.Equal(1, menu.RecipeVersion); // RecipeVersionSnapshot

        // 재료 스냅샷: total = 10 * 400 / 100 = 40, unit = kg
        Assert.Equal(2, menu.Ingredients.Count);
        var first = menu.Ingredients[0];
        Assert.Equal("돼지고기", first.Name); // IngredientNameSnapshot
        Assert.Equal(10.0, first.QuantityPer100);
        Assert.Equal(40.0, first.QuantityTotal);
        Assert.Equal("kg", first.Unit);
    }

    [Fact]
    public async Task AddMenu_FirstJoochanMenu_BecomesRepresentative()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var rice = await CreateMenuAsync(master, "현미밥", "밥·죽");
        var pork = await CreateMenuAsync(master, "제육볶음", "주찬");

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(rice, null));
        var withPork = await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(pork, null));

        Assert.False(withPork.Menus[0].IsRepresentative); // 밥
        Assert.True(withPork.Menus[1].IsRepresentative); // 주찬
    }

    [Fact]
    public async Task AddMenu_DuplicateMenu_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menuId = await CreateMenuAsync(master, "제육볶음");
        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, null));

        var ex = await Assert.ThrowsAsync<MenuAlreadyAddedException>(() =>
            workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, null)));
        Assert.Contains("이미 식단에 추가된 메뉴입니다", ex.Message);
    }

    [Fact]
    public async Task AddMenu_InactiveMenu_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menu = await master.CreateMenuAsync(new MenuInput("제육볶음", null, "주찬", true));
        await master.ArchiveMenuAsync(menu.Id);

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await Assert.ThrowsAsync<MenuNotFoundException>(() =>
            workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menu.Id, null)));
    }

    [Fact]
    public async Task AddMenu_RecipeFromAnotherMenu_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var pork = await CreateIngredientAsync(master, "돼지고기", "돼지고기");
        var menuA = await CreateMenuAsync(master, "메뉴A");
        var menuB = await CreateMenuAsync(master, "메뉴B");
        var recipeB = await CreateRecipeAsync(master, menuB, "B 레시피", [(pork, 5)]);

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await Assert.ThrowsAsync<RecipeNotAvailableException>(() =>
            workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuA, recipeB)));
    }

    [Fact]
    public async Task AddMenu_MenuWithoutRecipe_IsAllowed()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menuId = await CreateMenuAsync(master, "물", "기타");
        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));

        var withMenu = await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, null));
        var menu = Assert.Single(withMenu.Menus);
        Assert.Null(menu.RecipeId);
        Assert.Empty(menu.Ingredients);
    }

    // =======================================================================
    // 일괄 추가
    // =======================================================================

    [Fact]
    public async Task BatchAddMenus_AddsMultipleMenus()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var pork = await CreateIngredientAsync(master, "돼지고기", "돼지고기");
        var menu1 = await CreateMenuAsync(master, "백미밥", "밥·죽");
        var menu2 = await CreateMenuAsync(master, "제육볶음");
        var recipe1 = await CreateRecipeAsync(master, menu1, "밥 레시피", [(pork, 5)]);
        var recipe2 = await CreateRecipeAsync(master, menu2, "불고기 레시피", [(pork, 8)]);

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var result = await workspace.BatchAddMenusAsync(serviceDto.Id,
        [
            new BatchAddMenuItemInput(menu1, recipe1, 1),
            new BatchAddMenuItemInput(menu2, recipe2, 2),
        ]);

        Assert.Equal(2, result.Menus.Count);
        Assert.Equal("백미밥", result.Menus[0].Name);
        Assert.Equal("제육볶음", result.Menus[1].Name);
        Assert.Equal(2, result.Menus.Sum(m => m.Ingredients.Count)); // 재료 스냅샷 복사
    }

    [Fact]
    public async Task BatchAddMenus_EmptyItems_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await Assert.ThrowsAsync<EmptyMenuSelectionException>(() =>
            workspace.BatchAddMenusAsync(serviceDto.Id, []));
    }

    [Fact]
    public async Task BatchAddMenus_DuplicateMenuInRequest_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menuId = await CreateMenuAsync(master, "제육볶음");
        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));

        await Assert.ThrowsAsync<DuplicateMenuInRequestException>(() =>
            workspace.BatchAddMenusAsync(serviceDto.Id,
            [
                new BatchAddMenuItemInput(menuId, null, 1),
                new BatchAddMenuItemInput(menuId, null, 2),
            ]));
    }

    [Fact]
    public async Task BatchAddMenus_AlreadyAddedMenu_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menuId = await CreateMenuAsync(master, "제육볶음");
        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, null));

        await Assert.ThrowsAsync<MenuAlreadyAddedException>(() =>
            workspace.BatchAddMenusAsync(serviceDto.Id, [new BatchAddMenuItemInput(menuId, null, 1)]));
    }

    [Fact]
    public async Task BatchAddMenus_InactiveMenu_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menu = await master.CreateMenuAsync(new MenuInput("제육볶음", null, "주찬", true));
        await master.ArchiveMenuAsync(menu.Id);

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await Assert.ThrowsAsync<MenuNotFoundException>(() =>
            workspace.BatchAddMenusAsync(serviceDto.Id, [new BatchAddMenuItemInput(menu.Id, null, 1)]));
    }

    [Fact]
    public async Task BatchAddMenus_RecipeFromAnotherMenu_IsRejected()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var pork = await CreateIngredientAsync(master, "돼지고기", "돼지고기");
        var menu1 = await CreateMenuAsync(master, "메뉴1");
        var menu2 = await CreateMenuAsync(master, "메뉴2");
        var recipe2 = await CreateRecipeAsync(master, menu2, "레시피2", [(pork, 5)]);

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await Assert.ThrowsAsync<RecipeNotInMenuException>(() =>
            workspace.BatchAddMenusAsync(serviceDto.Id, [new BatchAddMenuItemInput(menu1, recipe2, 1)]));
    }

    // =======================================================================
    // 스냅샷 불변성
    // =======================================================================

    [Fact]
    public async Task Snapshots_AreNotAffected_WhenMasterDataChanges()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var pork = await CreateIngredientAsync(master, "돼지고기", "돼지고기");
        var menu = await master.CreateMenuAsync(new MenuInput("육개장", null, "국·탕", true));
        var recipe = await master.CreateRecipeAsync(menu.Id, new RecipeInput("기본 레시피", null, false, true,
            [new RecipeItemInput(pork, null, 10, null, false)]));

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenu = await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menu.Id, null));
        var menuItemId = withMenu.Menus[0].Id;

        // 기준 메뉴/레시피/재료 수정
        await master.UpdateMenuAsync(menu.Id, new MenuInput("소고기육개장", null, "국·탕", true));
        await master.UpdateRecipeAsync(recipe.Id, new RecipeInput("수정된 레시피", null, false, true,
            [new RecipeItemInput(pork, null, 20, null, false)]));
        await master.UpdateIngredientAsync(pork, new IngredientInput("돈육", "돼지고기", "kg", null, null, null, false, true));

        // 스냅샷은 그대로
        var reloaded = await workspace.GetServiceAsync(serviceDto.Id);
        var snapshot = reloaded.Menus.Single(m => m.Id == menuItemId);
        Assert.Equal("육개장", snapshot.Name);
        Assert.Equal("기본 레시피", snapshot.RecipeName);
        Assert.Equal(1, snapshot.RecipeVersion);
        Assert.Equal("돼지고기", snapshot.Ingredients[0].Name);
        Assert.Equal(10.0, snapshot.Ingredients[0].QuantityPer100);
        Assert.Equal(40.0, snapshot.Ingredients[0].QuantityTotal);
    }

    // =======================================================================
    // 레시피 변경
    // =======================================================================

    [Fact]
    public async Task ChangeRecipe_ReplacesIngredients_NotMerges()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var a = await CreateIngredientAsync(master, "재료A");
        var b = await CreateIngredientAsync(master, "재료B");
        var c = await CreateIngredientAsync(master, "재료C");
        var d = await CreateIngredientAsync(master, "재료D");
        var menuId = await CreateMenuAsync(master, "테스트메뉴");

        var recipeA = await CreateRecipeAsync(master, menuId, "레시피A", [(a, 1), (b, 2), (c, 3)]);
        var recipeB = await CreateRecipeAsync(master, menuId, "레시피B", [(a, 1), (b, 2), (d, 4)]);

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenu = await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, recipeA));
        var menuItemId = withMenu.Menus[0].Id;

        var changed = await workspace.ChangeServiceMenuRecipeAsync(menuItemId, recipeB);
        var snapshot = changed.Menus.Single(m => m.Id == menuItemId);

        // A,B,D만 남아야 한다 (C 제거, 병합 아님)
        var names = snapshot.Ingredients.Select(i => i.Name).ToList();
        Assert.Equal(["재료A", "재료B", "재료D"], names);
        Assert.DoesNotContain("재료C", names);
        Assert.Equal("레시피B", snapshot.RecipeName);
    }

    // =======================================================================
    // 식단 편집 (Meal Editor)
    // =======================================================================

    [Fact]
    public async Task SaveMealEditor_SavesConceptAndCalculatesPer100()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var pork = await CreateIngredientAsync(master, "돼지고기", "돼지고기");
        var menuId = await CreateMenuAsync(master, "돼지불고기");

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenu = await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, null));
        var menuItemId = withMenu.Menus[0].Id;

        var saved = await workspace.SaveMealEditorAsync(serviceDto.Id, new MealEditorInput(
            150, "12:30", "LA갈비 특식", "내부 메모",
            [
                new MealEditorMenuInput(menuItemId, "메뉴 비고", true,
                [
                    new MealEditorIngredientInput(pork, "돼지고기", 12.0, "kg"),
                ]),
            ]));

        Assert.Equal(150, saved.PlannedCount);
        Assert.Equal("LA갈비 특식", saved.ConceptTitle);
        Assert.Equal("내부 메모", saved.Note);

        var menu = saved.Menus.Single(m => m.Id == menuItemId);
        Assert.Equal("메뉴 비고", menu.Note);
        Assert.True(menu.IsRepresentative);
        var ingredient = Assert.Single(menu.Ingredients);
        Assert.Equal(12.0, ingredient.QuantityTotal);
        Assert.Equal(8.0, ingredient.QuantityPer100!.Value, precision: 10); // 12 * 100 / 150
    }

    [Fact]
    public async Task SaveMealEditor_ReplacesIngredients()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var pork = await CreateIngredientAsync(master, "돼지고기", "돼지고기");
        var onion = await CreateIngredientAsync(master, "양파", "채소");
        var menuId = await CreateMenuAsync(master, "돼지불고기");

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenu = await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, null));
        var menuItemId = withMenu.Menus[0].Id;

        // 1차: 재료 1개
        await workspace.SaveMealEditorAsync(serviceDto.Id, new MealEditorInput(100, null, null, null,
        [
            new MealEditorMenuInput(menuItemId, null, false, [new MealEditorIngredientInput(pork, "돼지고기", 10.0, "kg")]),
        ]));

        // 2차: 재료 2개 (교체)
        var saved = await workspace.SaveMealEditorAsync(serviceDto.Id, new MealEditorInput(100, null, null, null,
        [
            new MealEditorMenuInput(menuItemId, null, false,
            [
                new MealEditorIngredientInput(pork, "돼지고기", 8.0, "kg"),
                new MealEditorIngredientInput(onion, "양파", 3.0, "kg"),
            ]),
        ]));

        var menu = saved.Menus.Single(m => m.Id == menuItemId);
        Assert.Equal(2, menu.Ingredients.Count);
        Assert.Contains(menu.Ingredients, i => i.Name == "돼지고기");
        Assert.Contains(menu.Ingredients, i => i.Name == "양파");
    }

    [Fact]
    public async Task SaveMealEditor_EmptyIngredients_ClearsAll()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var pork = await CreateIngredientAsync(master, "돼지고기", "돼지고기");
        var menuId = await CreateMenuAsync(master, "돼지불고기");

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenu = await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, null));
        var menuItemId = withMenu.Menus[0].Id;

        await workspace.SaveMealEditorAsync(serviceDto.Id, new MealEditorInput(100, null, null, null,
        [
            new MealEditorMenuInput(menuItemId, null, false, [new MealEditorIngredientInput(pork, "돼지고기", 10.0, "kg")]),
        ]));

        var saved = await workspace.SaveMealEditorAsync(serviceDto.Id, new MealEditorInput(100, null, null, null,
        [
            new MealEditorMenuInput(menuItemId, null, false, []),
        ]));

        var menu = saved.Menus.Single(m => m.Id == menuItemId);
        Assert.Empty(menu.Ingredients);
    }

    [Fact]
    public async Task SaveMealEditor_Representative_FirstTrueWins()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menu1 = await CreateMenuAsync(master, "메뉴1", "밥·죽");
        var menu2 = await CreateMenuAsync(master, "메뉴2", "주찬");

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenus = await workspace.BatchAddMenusAsync(serviceDto.Id,
        [
            new BatchAddMenuItemInput(menu1, null, 1),
            new BatchAddMenuItemInput(menu2, null, 2),
        ]);
        var item1Id = withMenus.Menus[0].Id;
        var item2Id = withMenus.Menus[1].Id;

        // 둘 다 True로 보내면 첫 번째만 인정
        var saved = await workspace.SaveMealEditorAsync(serviceDto.Id, new MealEditorInput(100, null, null, null,
        [
            new MealEditorMenuInput(item1Id, null, true, []),
            new MealEditorMenuInput(item2Id, null, true, []),
        ]));

        Assert.True(saved.Menus.Single(m => m.Id == item1Id).IsRepresentative);
        Assert.False(saved.Menus.Single(m => m.Id == item2Id).IsRepresentative);
    }

    // =======================================================================
    // 메뉴 삭제 / 순서
    // =======================================================================

    [Fact]
    public async Task DeleteServiceMenu_RenumbersSortOrder()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menu1 = await CreateMenuAsync(master, "메뉴1", "밥·죽");
        var menu2 = await CreateMenuAsync(master, "메뉴2", "주찬");
        var menu3 = await CreateMenuAsync(master, "메뉴3", "부찬");

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenus = await workspace.BatchAddMenusAsync(serviceDto.Id,
        [
            new BatchAddMenuItemInput(menu1, null, 1),
            new BatchAddMenuItemInput(menu2, null, 2),
            new BatchAddMenuItemInput(menu3, null, 3),
        ]);
        var middleId = withMenus.Menus[1].Id;

        var afterDelete = await workspace.DeleteServiceMenuAsync(middleId);
        Assert.Equal(2, afterDelete.Menus.Count);
        Assert.Equal(1, afterDelete.Menus[0].SortOrder);
        Assert.Equal(2, afterDelete.Menus[1].SortOrder);
    }

    [Fact]
    public async Task ReorderMenus_AppliesNewOrder()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menu1 = await CreateMenuAsync(master, "메뉴1", "밥·죽");
        var menu2 = await CreateMenuAsync(master, "메뉴2", "주찬");

        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var withMenus = await workspace.BatchAddMenusAsync(serviceDto.Id,
        [
            new BatchAddMenuItemInput(menu1, null, 1),
            new BatchAddMenuItemInput(menu2, null, 2),
        ]);
        var id1 = withMenus.Menus[0].Id;
        var id2 = withMenus.Menus[1].Id;

        var reordered = await workspace.ReorderMenusAsync(serviceDto.Id, [id2, id1]);
        Assert.Equal(id2, reordered.Menus[0].Id);
        Assert.Equal(1, reordered.Menus[0].SortOrder);
        Assert.Equal(id1, reordered.Menus[1].Id);
        Assert.Equal(2, reordered.Menus[1].SortOrder);
    }

    // =======================================================================
    // 메뉴 선택기
    // =======================================================================

    [Fact]
    public async Task MenuPicker_SearchAndRoleFilter()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        await CreateMenuAsync(master, "백미밥", "밥·죽");
        await CreateMenuAsync(master, "LA갈비구이", "주찬");

        var byQuery = await workspace.SearchMenuPickerAsync("갈비", null, null);
        Assert.Single(byQuery.Items);
        Assert.Equal("LA갈비구이", byQuery.Items[0].Name);

        var byRole = await workspace.SearchMenuPickerAsync(null, "밥·죽", null);
        Assert.Single(byRole.Items);
        Assert.Equal("백미밥", byRole.Items[0].Name);
    }

    [Fact]
    public async Task MenuPicker_MarksAlreadyAdded()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        var menuId = await CreateMenuAsync(master, "제육볶음");
        var serviceDto = await workspace.CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        await workspace.AddMenuAsync(serviceDto.Id, new AddMenuInput(menuId, null));

        var result = await workspace.SearchMenuPickerAsync(null, null, serviceDto.Id);
        Assert.True(result.Items.Single().AlreadyAdded);
    }

    [Fact]
    public async Task MenuPicker_MoreThan200Menus_CanSearchTail()
    {
        using var harness = new WorkspaceTestHarness();
        var workspace = harness.CreateWorkspaceService();
        var master = harness.CreateMasterDataService();

        // 250개 메뉴 생성
        for (var i = 1; i <= 250; i++)
        {
            await CreateMenuAsync(master, $"메뉴{i:D3}", "기타");
        }

        // 뒤쪽 메뉴도 검색으로 찾을 수 있어야 한다
        var result = await workspace.SearchMenuPickerAsync("메뉴250", null, null);
        var item = Assert.Single(result.Items);
        Assert.Equal("메뉴250", item.Name);
        Assert.Equal(1, result.Total); // 필터된 결과 수

        // 필터 없이 전체 조회 시 250건 모두 집계
        var all = await workspace.SearchMenuPickerAsync(null, null, null);
        Assert.Equal(250, all.Total);
    }
}

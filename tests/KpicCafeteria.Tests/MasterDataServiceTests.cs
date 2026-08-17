using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// 메뉴/식재료/별칭/배식 기본값 업무규칙 검증.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\master.py
/// C:\Pjt\kpicCafeteria\backend\app\routers\master_data.py
/// C:\Pjt\kpicCafeteria\backend\tests\test_master_data.py (TestMealServiceDefaults)
/// </summary>
public class MasterDataServiceTests
{
    // =======================================================================
    // Menu
    // =======================================================================

    [Fact]
    public async Task CreateMenu_CanonicalNameDefaultsToName()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var menu = await service.CreateMenuAsync(new MenuInput("제육볶음", null, "주찬", true));

        Assert.Equal("제육볶음", menu.Name);
        Assert.Equal("제육볶음", menu.CanonicalName);
        Assert.Equal("주찬", menu.Role);
        Assert.True(menu.Active);
    }

    [Fact]
    public async Task UpdateMenu_ChangesFields()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var menu = await service.CreateMenuAsync(new MenuInput("제육볶음", null, "주찬", true));
        var updated = await service.UpdateMenuAsync(menu.Id, new MenuInput("매운제육볶음", "제육볶음", "주찬", true));

        Assert.Equal("매운제육볶음", updated.Name);
        Assert.Equal("제육볶음", updated.CanonicalName);
    }

    [Fact]
    public async Task CreateMenu_DuplicateName_IsRejected()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        await service.CreateMenuAsync(new MenuInput("제육볶음", null, "주찬", true));

        var ex = await Assert.ThrowsAsync<DuplicateMenuNameException>(() =>
            service.CreateMenuAsync(new MenuInput("제육볶음", null, "주찬", true)));
        Assert.Contains("같은 이름의 메뉴가 있습니다", ex.Message);
    }

    [Fact]
    public async Task UpdateMenu_DuplicateName_IsRejected()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var menuA = await service.CreateMenuAsync(new MenuInput("제육볶음", null, "주찬", true));
        await service.CreateMenuAsync(new MenuInput("불고기", null, "주찬", true));

        await Assert.ThrowsAsync<DuplicateMenuNameException>(() =>
            service.UpdateMenuAsync(menuA.Id, new MenuInput("불고기", null, "주찬", true)));
    }

    [Fact]
    public async Task ArchiveMenu_DeactivatesMenu()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var menu = await service.CreateMenuAsync(new MenuInput("제육볶음", null, "주찬", true));
        await service.ArchiveMenuAsync(menu.Id);

        var detail = await service.GetMenuAsync(menu.Id);
        Assert.False(detail.Menu.Active);
    }

    [Fact]
    public async Task ArchiveMenu_DoesNotAffectPastMealServiceSnapshot()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var menu = await service.CreateMenuAsync(new MenuInput("육개장", null, "국·탕", true));

        // 과거 식단 스냅샷 생성 (메뉴명 스냅샷)
        int serviceMenuId;
        using (var db = harness.CreateContext())
        {
            var mealService = new MealService
            {
                ServiceDate = new DateOnly(2026, 8, 17),
                MealType = MealType.LUNCH,
                Menus = [new MealServiceMenu { MenuId = menu.Id, MenuNameSnapshot = menu.Name }],
            };
            db.MealServices.Add(mealService);
            db.SaveChanges();
            serviceMenuId = mealService.Menus[0].Id;
        }

        // 메뉴 미사용 처리
        await service.ArchiveMenuAsync(menu.Id);

        // 스냅샷은 그대로 유지
        using (var db = harness.CreateContext())
        {
            var snapshot = db.MealServiceMenus.Single(x => x.Id == serviceMenuId);
            Assert.Equal("육개장", snapshot.MenuNameSnapshot);
        }
    }

    // =======================================================================
    // Ingredient
    // =======================================================================

    [Fact]
    public async Task CreateIngredient_DefaultsApplied()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var ingredient = await service.CreateIngredientAsync(new IngredientInput("돼지고기", "돼지고기", "kg", null, null, 1.0, false, true));

        Assert.Equal("돼지고기", ingredient.Name);
        Assert.Equal("돼지고기", ingredient.StatGroup);
        Assert.Equal("kg", ingredient.DefaultUnit);
        Assert.Equal(1.0, ingredient.KgFactor);
        Assert.True(ingredient.Active);
    }

    [Fact]
    public async Task UpdateIngredient_ChangesFields()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var ingredient = await service.CreateIngredientAsync(new IngredientInput("돼지고기", "돼지고기", "kg", null, null, null, false, true));
        var updated = await service.UpdateIngredientAsync(ingredient.Id, new IngredientInput("돈육", "돼지고기", "kg", null, null, null, true, true));

        Assert.Equal("돈육", updated.Name);
        Assert.True(updated.AnalysisExcluded);
    }

    [Fact]
    public async Task CreateIngredient_DuplicateName_IsRejected()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        await service.CreateIngredientAsync(new IngredientInput("돼지고기", "돼지고기", "kg", null, null, null, false, true));

        var ex = await Assert.ThrowsAsync<DuplicateIngredientNameException>(() =>
            service.CreateIngredientAsync(new IngredientInput("돼지고기", "돼지고기", "kg", null, null, null, false, true)));
        Assert.Contains("같은 이름의 재료가 있습니다", ex.Message);
    }

    [Fact]
    public async Task ArchiveIngredient_DeactivatesIngredient()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var ingredient = await service.CreateIngredientAsync(new IngredientInput("돼지고기", "돼지고기", "kg", null, null, null, false, true));
        await service.ArchiveIngredientAsync(ingredient.Id);

        var detail = await service.GetIngredientAsync(ingredient.Id);
        Assert.False(detail.Ingredient.Active);
    }

    // =======================================================================
    // Alias
    // =======================================================================

    [Fact]
    public async Task AddAlias_CreatesAlias()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var ingredient = await service.CreateIngredientAsync(new IngredientInput("돼지고기", "돼지고기", "kg", null, null, null, false, true));
        var alias = await service.AddAliasAsync(ingredient.Id, "돈육");

        Assert.Equal("돈육", alias.Alias);

        var detail = await service.GetIngredientAsync(ingredient.Id);
        Assert.Contains(detail.Aliases, a => a.Alias == "돈육");
    }

    [Fact]
    public async Task AddAlias_DuplicateAlias_ChangesOwnership()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var pork = await service.CreateIngredientAsync(new IngredientInput("돼지고기", "돼지고기", "kg", null, null, null, false, true));
        var beef = await service.CreateIngredientAsync(new IngredientInput("소고기", "소고기", "kg", null, null, null, false, true));

        await service.AddAliasAsync(pork.Id, "돈육");

        // 같은 별칭을 다른 재료에 추가 → 소유 재료 변경
        await service.AddAliasAsync(beef.Id, "돈육");

        var porkDetail = await service.GetIngredientAsync(pork.Id);
        var beefDetail = await service.GetIngredientAsync(beef.Id);
        Assert.DoesNotContain(porkDetail.Aliases, a => a.Alias == "돈육");
        Assert.Contains(beefDetail.Aliases, a => a.Alias == "돈육");
    }

    [Fact]
    public async Task RemoveAlias_DeletesAlias()
    {
        using var harness = new MasterDataTestHarness();
        var service = harness.CreateService();

        var ingredient = await service.CreateIngredientAsync(new IngredientInput("돼지고기", "돼지고기", "kg", null, null, null, false, true));
        var alias = await service.AddAliasAsync(ingredient.Id, "돈육");

        await service.RemoveAliasAsync(alias.Id);

        var detail = await service.GetIngredientAsync(ingredient.Id);
        Assert.Empty(detail.Aliases);
    }

    // =======================================================================
    // MealTypeSetting
    // =======================================================================

    [Fact]
    public async Task GetMealTypeSettings_ReturnsSeededDefaults()
    {
        using var harness = new MasterDataTestHarness();
        using (var db = harness.CreateContext())
        {
            await Infrastructure.Persistence.DatabaseInitializer.SeedAsync(db);
        }

        var service = harness.CreateService();
        var rows = await service.GetMealTypeSettingsAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal("LUNCH", rows[0].Code);
        Assert.Equal(400, rows[0].DefaultPlannedCount);
        Assert.Equal("11:40", rows[0].DefaultServiceTime);
        Assert.Equal("DINNER", rows[1].Code);
        Assert.Equal(100, rows[1].DefaultPlannedCount);
        Assert.Equal("17:30", rows[1].DefaultServiceTime);
    }

    [Fact]
    public async Task UpdateMealTypeSettings_ChangesCountsAndTimes()
    {
        using var harness = new MasterDataTestHarness();
        using (var db = harness.CreateContext())
        {
            await Infrastructure.Persistence.DatabaseInitializer.SeedAsync(db);
        }

        var service = harness.CreateService();
        var rows = await service.UpdateMealTypeSettingsAsync(
        [
            new MealTypeSettingInput("LUNCH", 350, "12:00", 1, true, null),
            new MealTypeSettingInput("DINNER", 80, "18:00", 2, true, null),
        ]);

        Assert.Equal(350, rows.Single(r => r.Code == "LUNCH").DefaultPlannedCount);
        Assert.Equal("12:00", rows.Single(r => r.Code == "LUNCH").DefaultServiceTime);
        Assert.Equal(80, rows.Single(r => r.Code == "DINNER").DefaultPlannedCount);
        Assert.Equal("18:00", rows.Single(r => r.Code == "DINNER").DefaultServiceTime);
    }

    [Fact]
    public async Task UpdateMealTypeSettings_NegativeCount_IsRejected()
    {
        using var harness = new MasterDataTestHarness();
        using (var db = harness.CreateContext())
        {
            await Infrastructure.Persistence.DatabaseInitializer.SeedAsync(db);
        }

        var service = harness.CreateService();
        await Assert.ThrowsAsync<InvalidPlannedCountException>(() =>
            service.UpdateMealTypeSettingsAsync(
            [
                new MealTypeSettingInput("LUNCH", -1, "12:00", 1, true, null),
            ]));
    }

    [Fact]
    public async Task UpdateMealTypeSettings_InvalidTime_IsRejected()
    {
        using var harness = new MasterDataTestHarness();
        using (var db = harness.CreateContext())
        {
            await Infrastructure.Persistence.DatabaseInitializer.SeedAsync(db);
        }

        var service = harness.CreateService();
        var ex = await Assert.ThrowsAsync<InvalidTimeFormatException>(() =>
            service.UpdateMealTypeSettingsAsync(
            [
                new MealTypeSettingInput("LUNCH", 100, "25:99", 1, true, null),
            ]));
        Assert.Contains("HH:MM", ex.Message);
    }

    [Fact]
    public async Task UpdateMealTypeSettings_UnknownCode_IsRejected()
    {
        using var harness = new MasterDataTestHarness();
        using (var db = harness.CreateContext())
        {
            await Infrastructure.Persistence.DatabaseInitializer.SeedAsync(db);
        }

        var service = harness.CreateService();
        var ex = await Assert.ThrowsAsync<MealTypeNotFoundException>(() =>
            service.UpdateMealTypeSettingsAsync(
            [
                new MealTypeSettingInput("BREAKFAST", 50, "08:00", 1, true, null),
            ]));
        Assert.Contains("BREAKFAST", ex.Message);
    }

    [Fact]
    public async Task UpdateMealTypeSettings_ChangesActiveAndSortOrder()
    {
        using var harness = new MasterDataTestHarness();
        using (var db = harness.CreateContext())
        {
            await Infrastructure.Persistence.DatabaseInitializer.SeedAsync(db);
        }

        var service = harness.CreateService();
        var rows = await service.UpdateMealTypeSettingsAsync(
        [
            new MealTypeSettingInput("LUNCH", 400, "11:40", 2, false, "비활성 테스트"),
            new MealTypeSettingInput("DINNER", 100, "17:30", 1, true, null),
        ]);

        Assert.False(rows.Single(r => r.Code == "LUNCH").IsActive);
        Assert.Equal("비활성 테스트", rows.Single(r => r.Code == "LUNCH").Description);
        Assert.Equal(2, rows.Single(r => r.Code == "LUNCH").SortOrder);
        Assert.Equal(1, rows.Single(r => r.Code == "DINNER").SortOrder);
    }
}

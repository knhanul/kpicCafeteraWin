using KpicCafeteria.Infrastructure.Persistence;
using KpicCafeteria.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Tests;

/// <summary>
/// 기본 데이터 Seed 검증.
/// 최초 생성 시 중식(LUNCH)/석식(DINNER)만 생성되고, 재실행 시 중복 생성되지 않는다.
/// 사용자/admin 계정, 기본 메뉴/재료는 생성하지 않는다.
/// </summary>
public class SeedTests
{
    [Fact]
    public async Task Seed_CreatesLunchAndDinnerSettings()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        await DatabaseInitializer.SeedAsync(db);

        var settings = await db.MealTypeSettings.OrderBy(x => x.SortOrder).ToListAsync();
        Assert.Equal(2, settings.Count);

        var lunch = settings[0];
        Assert.Equal("LUNCH", lunch.Code);
        Assert.Equal("중식", lunch.Name);
        Assert.Equal(400, lunch.DefaultPlannedCount);
        Assert.Equal(new TimeOnly(11, 40), lunch.DefaultServiceTime);
        Assert.Equal(1, lunch.SortOrder);
        Assert.True(lunch.Active);

        var dinner = settings[1];
        Assert.Equal("DINNER", dinner.Code);
        Assert.Equal("석식", dinner.Name);
        Assert.Equal(100, dinner.DefaultPlannedCount);
        Assert.Equal(new TimeOnly(17, 30), dinner.DefaultServiceTime);
        Assert.Equal(2, dinner.SortOrder);
        Assert.True(dinner.Active);
    }

    [Fact]
    public async Task Seed_SecondRun_DoesNotDuplicate()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        await DatabaseInitializer.SeedAsync(db);
        await DatabaseInitializer.SeedAsync(db);

        Assert.Equal(2, await db.MealTypeSettings.CountAsync());
    }

    [Fact]
    public async Task Seed_DoesNotCreateUsersOrMenus()
    {
        using var testDb = new SqliteTestDatabase();
        using var db = testDb.CreateContext();

        await DatabaseInitializer.SeedAsync(db);

        Assert.Equal(0, await db.Menus.CountAsync());
        Assert.Equal(0, await db.Ingredients.CountAsync());
    }

    [Fact]
    public async Task InitializeAsync_AppliesMigrationAndSeeds()
    {
        using var testDb = new SqliteTestDatabase();
        var factory = new TestDbContextFactory(testDb.Connection);
        var initializer = new DatabaseInitializer(factory);

        await initializer.InitializeAsync();

        using var db = testDb.CreateContext();
        Assert.Equal(2, await db.MealTypeSettings.CountAsync());
    }
}

using KpicCafeteria.Application.DataManagement;
using KpicCafeteria.Domain.Domain;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.DataManagement;

/// <summary>기존 Python MigrationImporter C# 이식.</summary>
public sealed class MigrationImporter
{
    private static readonly string[] ExpectedSheets =
    [
        "01_배식설정",
        "02_메뉴기준정보",
        "03_재료기준정보",
        "04_재료별칭_선택",
        "05_메뉴별재료_기준",
        "06_식단이력_이관",
        "07_식단재료_이관",
    ];

    private readonly string _path;

    public MigrationImporter(string path) => _path = path;

    public async Task<ImportPreview> PreviewAsync(CancellationToken cancellationToken = default)
    {
        var preview = new ImportPreview { Filename = Path.GetFileName(_path) };

        try
        {
            using var reader = new XlsxWorkbookReader(_path);
            var missing = ExpectedSheets.Where(s => !reader.HasSheet(s)).ToList();
            if (missing.Count > 0)
            {
                preview.Errors.Add(new ImportIssue(
                    "MISSING_SHEET",
                    $"필수 시트가 없습니다: {string.Join(", ", missing)}"));
            }

            foreach (var sheet in ExpectedSheets)
            {
                if (reader.HasSheet(sheet))
                {
                    preview.SheetCounts[sheet] = reader.RowCount(sheet);
                }
            }

            preview.Ready = !preview.Errors.Any();
        }
        catch (Exception ex)
        {
            preview.Errors.Add(new ImportIssue("WORKBOOK_ERROR", ex.Message));
            preview.Ready = false;
        }

        preview.MealTypeCount = preview.SheetCounts.GetValueOrDefault("01_배식설정");
        preview.MenuCount = preview.SheetCounts.GetValueOrDefault("02_메뉴기준정보");
        preview.IngredientCount = preview.SheetCounts.GetValueOrDefault("03_재료기준정보");
        preview.AliasCount = preview.SheetCounts.GetValueOrDefault("04_재료별칭_선택");
        preview.RecipeRowCount = preview.SheetCounts.GetValueOrDefault("05_메뉴별재료_기준");
        preview.MealHistoryRowCount = preview.SheetCounts.GetValueOrDefault("06_식단이력_이관");
        preview.MealIngredientRowCount = preview.SheetCounts.GetValueOrDefault("07_식단재료_이관");

        return await Task.FromResult(preview);
    }

    public async Task<ImportApplyResult> ApplyAsync(
        CafeteriaDbContext db,
        ImportMode mode,
        CancellationToken cancellationToken = default)
    {
        var result = new ImportApplyResult { Filename = Path.GetFileName(_path), Mode = mode };

        using var reader = new XlsxWorkbookReader(_path);
        var missing = ExpectedSheets.Where(s => !reader.HasSheet(s)).ToList();
        if (missing.Count > 0)
            throw new ImportException($"필수 시트가 없습니다: {string.Join(", ", missing)}");

        if (mode == ImportMode.Replace)
            await ClearBusinessDataAsync(db, cancellationToken);

        // 01_배식설정
        var mealTypeByCode = new Dictionary<string, MealTypeSetting>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in reader.ReadRows("01_배식설정"))
        {
            var name = XlsxCellParser.CleanText(row.GetValueOrDefault("배식유형"));
            if (string.IsNullOrEmpty(name))
                continue;
            var code = MapMealCode(name);
            var setting = await db.MealTypeSettings
                .FirstOrDefaultAsync(x => x.Code == code, cancellationToken)
                ?? new MealTypeSetting { Code = code };
            if (setting.Id == 0)
                db.MealTypeSettings.Add(setting);
            setting.Name = name;
            setting.DefaultPlannedCount = XlsxCellParser.CleanInt(row.GetValueOrDefault("기본계획식수")) ?? 0;
            setting.DefaultServiceTime = XlsxCellParser.ParseTime(row.GetValueOrDefault("기본배식시간"));
            setting.Active = XlsxCellParser.CleanBool(row.GetValueOrDefault("사용여부"), true);
            setting.Description = XlsxCellParser.CleanText(row.GetValueOrDefault("설명")) is { Length: > 0 } d ? d : null;
            mealTypeByCode[code] = setting;
            result.MealTypes++;
        }

        await db.SaveChangesAsync(cancellationToken);

        // 02_메뉴기준정보
        var menuByCode = new Dictionary<string, Menu>(StringComparer.Ordinal);
        foreach (var row in reader.ReadRows("02_메뉴기준정보"))
        {
            var code = XlsxCellParser.CleanText(row.GetValueOrDefault("메뉴ID"));
            var name = XlsxCellParser.CleanText(row.GetValueOrDefault("메뉴명"));
            if (string.IsNullOrEmpty(name))
                continue;
            var menu = await FindMenuAsync(db, code, name, cancellationToken)
                ?? new Menu { Name = name, CanonicalName = name };
            if (menu.Id == 0)
                db.Menus.Add(menu);
            if (!string.IsNullOrEmpty(code))
                menu.SourceCode = code;
            menu.Name = name;
            menu.CanonicalName = XlsxCellParser.CleanText(row.GetValueOrDefault("통계집계메뉴명")) is { Length: > 0 } c ? c : name;
            menu.Role = XlsxCellParser.CleanText(row.GetValueOrDefault("메뉴역할")) is { Length: > 0 } r ? r : "기타";
            menu.Active = XlsxCellParser.CleanBool(row.GetValueOrDefault("사용여부"), true);
            menu.ReviewStatus = XlsxCellParser.CleanText(row.GetValueOrDefault("검토상태")) is { Length: > 0 } s ? s : "정상";
            await db.SaveChangesAsync(cancellationToken);
            if (!string.IsNullOrEmpty(code))
                menuByCode[code] = menu;
            result.Menus++;
        }

        // 03_재료기준정보
        var ingredientByCode = new Dictionary<string, Ingredient>(StringComparer.Ordinal);
        foreach (var row in reader.ReadRows("03_재료기준정보"))
        {
            var code = XlsxCellParser.CleanText(row.GetValueOrDefault("재료ID"));
            var name = XlsxCellParser.CleanText(row.GetValueOrDefault("표준재료명"));
            if (string.IsNullOrEmpty(name))
                continue;
            var ingredient = await FindIngredientAsync(db, code, name, cancellationToken)
                ?? new Ingredient { Name = name };
            if (ingredient.Id == 0)
                db.Ingredients.Add(ingredient);
            if (!string.IsNullOrEmpty(code))
                ingredient.SourceCode = code;
            ingredient.Name = name;
            ingredient.StatGroup = XlsxCellParser.CleanText(row.GetValueOrDefault("통계분석군")) is { Length: > 0 } g ? g : "기타";
            ingredient.DefaultUnit = XlsxCellParser.CleanText(row.GetValueOrDefault("기본단위")) is { Length: > 0 } u ? u : null;
            ingredient.KgFactor = XlsxCellParser.CleanDouble(row.GetValueOrDefault("kg환산계수"));
            ingredient.AnalysisExcluded = XlsxCellParser.CleanBool(row.GetValueOrDefault("분석제외"), false);
            ingredient.Active = XlsxCellParser.CleanBool(row.GetValueOrDefault("사용여부"), true);
            ingredient.ReviewStatus = XlsxCellParser.CleanText(row.GetValueOrDefault("검토상태")) is { Length: > 0 } s ? s : "정상";
            await db.SaveChangesAsync(cancellationToken);
            if (!string.IsNullOrEmpty(code))
                ingredientByCode[code] = ingredient;
            result.Ingredients++;
        }

        // 04_재료별칭_선택
        foreach (var row in reader.ReadRows("04_재료별칭_선택"))
        {
            var aliasName = XlsxCellParser.CleanText(row.GetValueOrDefault("원재료별칭"));
            var ingredientCode = XlsxCellParser.CleanText(row.GetValueOrDefault("재료ID"));
            if (string.IsNullOrEmpty(aliasName) ||
                string.IsNullOrEmpty(ingredientCode) ||
                !ingredientByCode.TryGetValue(ingredientCode, out var ingredient))
                continue;
            var alias = await db.IngredientAliases
                .FirstOrDefaultAsync(a => a.Alias == aliasName, cancellationToken)
                ?? new IngredientAlias { Alias = aliasName, Ingredient = ingredient };
            if (alias.Id == 0)
                db.IngredientAliases.Add(alias);
            alias.Ingredient = ingredient;
            alias.IngredientId = ingredient.Id;
            alias.Source = XlsxCellParser.CleanText(row.GetValueOrDefault("출처")) is { Length: > 0 } s ? s : "기존데이터";
            result.Aliases++;
        }

        await db.SaveChangesAsync(cancellationToken);

        // 05_메뉴별재료_기준
        var recipeSourceRows = new List<RecipeSourceRow>();
        foreach (var row in reader.ReadRows("05_메뉴별재료_기준"))
        {
            var menuCode = XlsxCellParser.CleanText(row.GetValueOrDefault("메뉴ID"));
            var ingredientCode = XlsxCellParser.CleanText(row.GetValueOrDefault("재료ID"));
            if (!menuByCode.TryGetValue(menuCode, out var menu) ||
                !ingredientByCode.TryGetValue(ingredientCode, out var ingredient))
                continue;
            recipeSourceRows.Add(new RecipeSourceRow(
                menu,
                ingredient,
                XlsxCellParser.CleanInt(row.GetValueOrDefault("재료순서")) ?? 1,
                XlsxCellParser.CleanDouble(row.GetValueOrDefault("100인기준수량")),
                XlsxCellParser.CleanText(row.GetValueOrDefault("단위")) is { Length: > 0 } u ? u : ingredient.DefaultUnit,
                XlsxCellParser.CleanText(row.GetValueOrDefault("검토상태")) is { Length: > 0 } s ? s : "정상"));
            result.Recipes++;
        }

        var grouped = GroupRecipeRowsByComposition(recipeSourceRows);
        var recipeByMenuKey = new Dictionary<(int MenuId, string Key), Recipe>();
        var defaultRecipeByMenu = new Dictionary<int, Recipe>();
        var existingRecipes = await db.Recipes
            .Where(r => r.Active)
            .ToListAsync(cancellationToken);
        var existingRecipeByMenu = existingRecipes
            .GroupBy(r => r.MenuId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(r => r.CompositionKey, r => r));

        foreach (var (menuId, groups) in grouped)
        {
            foreach (var (compositionKey, rowsInGroup) in groups)
            {
                Recipe? recipe = null;
                if (existingRecipeByMenu.TryGetValue(menuId, out var byKey) && byKey.TryGetValue(compositionKey, out var foundRecipe))
                    recipe = foundRecipe;
                if (recipe is null)
                {
                    var maxVersion = await db.Recipes
                        .Where(r => r.MenuId == menuId)
                        .OrderByDescending(r => r.Version)
                        .Select(r => (int?)r.Version)
                        .FirstOrDefaultAsync(cancellationToken) ?? 0;
                    recipe = new Recipe
                    {
                        MenuId = menuId,
                        Name = $"기본 레시피 v{maxVersion + 1}",
                        Version = maxVersion + 1,
                        CompositionKey = compositionKey,
                        IsDefault = false,
                        Active = true,
                    };
                    db.Recipes.Add(recipe);
                    await db.SaveChangesAsync(cancellationToken);
                }

                var itemByIngredientId = new Dictionary<int, RecipeIngredient>();
                foreach (var row in rowsInGroup)
                {
                    var ing = row.Ingredient;
                    if (!itemByIngredientId.TryGetValue(ing.Id, out var item))
                    {
                        item = await db.RecipeIngredients
                            .FirstOrDefaultAsync(ri => ri.RecipeId == recipe.Id && ri.IngredientId == ing.Id, cancellationToken)
                            ?? new RecipeIngredient { Recipe = recipe, Ingredient = ing };
                        if (item.Id == 0)
                            db.RecipeIngredients.Add(item);
                    }
                    item.SortOrder = row.SortOrder;
                    item.QuantityPer100 = row.QuantityPer100;
                    item.Unit = row.Unit;
                    item.ReviewStatus = row.ReviewStatus;
                    item.IngredientId = ing.Id;
                    itemByIngredientId[ing.Id] = item;
                }

                recipeByMenuKey[(menuId, compositionKey)] = recipe;
                if (recipe.IsDefault)
                    defaultRecipeByMenu[menuId] = recipe;
            }

            if (!defaultRecipeByMenu.ContainsKey(menuId) && recipeByMenuKey.Any(x => x.Key.MenuId == menuId))
            {
                var first = recipeByMenuKey.Where(x => x.Key.MenuId == menuId).MinBy(x => x.Value.Version).Value;
                first.IsDefault = true;
                defaultRecipeByMenu[menuId] = first;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // 06_식단이력_이관
        var serviceMap = new Dictionary<(DateOnly Date, string MealType), MealService>();
        var serviceMenuMap = new Dictionary<(DateOnly Date, string MealType, string MenuKey, int Order), MealServiceMenu>();
        var serviceMenuById = new Dictionary<int, MealServiceMenu>();
        var serviceMenuIngredientIds = new Dictionary<int, HashSet<int>>();

        foreach (var row in reader.ReadRows("06_식단이력_이관"))
        {
            var serviceDate = XlsxCellParser.ParseDate(row.GetValueOrDefault("일자"));
            var mealName = XlsxCellParser.CleanText(row.GetValueOrDefault("배식유형"));
            var mealType = MapMealCode(mealName);
            var menuCode = XlsxCellParser.CleanText(row.GetValueOrDefault("메뉴ID"));
            var menuName = XlsxCellParser.CleanText(row.GetValueOrDefault("메뉴명"));
            var menuOrder = XlsxCellParser.CleanInt(row.GetValueOrDefault("메뉴순서")) ?? 1;
            if (!serviceDate.HasValue || string.IsNullOrEmpty(mealType) || string.IsNullOrEmpty(menuName))
                continue;
            var mealTypeEnum = ParseMealType(mealType);

            if (!serviceMap.TryGetValue((serviceDate.Value, mealType), out var service))
            {
                service = await db.MealServices
                    .FirstOrDefaultAsync(s => s.ServiceDate == serviceDate.Value && s.MealType == mealTypeEnum, cancellationToken)
                    ?? new MealService { ServiceDate = serviceDate.Value, MealType = mealTypeEnum };
                if (service.Id == 0)
                    db.MealServices.Add(service);
                service.PlannedCount = XlsxCellParser.CleanInt(row.GetValueOrDefault("계획식수"))
                    ?? await DefaultCountAsync(db, mealType, cancellationToken);
                service.ServiceTime = XlsxCellParser.ParseTime(row.GetValueOrDefault("배식시간"))
                    ?? await DefaultTimeAsync(db, mealType, cancellationToken);
                await db.SaveChangesAsync(cancellationToken);
                serviceMap[(serviceDate.Value, mealType)] = service;
                result.Services++;
            }

            menuByCode.TryGetValue(menuCode ?? string.Empty, out var menu);
            var menuKey = string.IsNullOrEmpty(menuCode) ? menuName : menuCode;
            if (!serviceMenuMap.TryGetValue((serviceDate.Value, mealType, menuKey, menuOrder), out var serviceMenu))
            {
                serviceMenu = await db.MealServiceMenus
                    .FirstOrDefaultAsync(m => m.MealServiceId == service.Id &&
                                              m.SortOrder == menuOrder &&
                                              m.MenuNameSnapshot == menuName, cancellationToken);
                if (serviceMenu is null)
                {
                    var sourceRecipe = menu is not null && defaultRecipeByMenu.TryGetValue(menu.Id, out var r) ? r : null;
                    serviceMenu = new MealServiceMenu
                    {
                        MealServiceId = service.Id,
                        Service = service,
                        Menu = menu,
                        MenuId = menu?.Id,
                        RecipeId = sourceRecipe?.Id,
                        SortOrder = menuOrder,
                        MenuNameSnapshot = menuName,
                        RecipeNameSnapshot = sourceRecipe?.Name,
                        RecipeVersionSnapshot = sourceRecipe?.Version,
                    };
                    db.MealServiceMenus.Add(serviceMenu);
                }
            }
            serviceMenu.Note = XlsxCellParser.CleanText(row.GetValueOrDefault("메뉴비고")) is { Length: > 0 } n ? n : null;
            await db.SaveChangesAsync(cancellationToken);
            serviceMenuMap[(serviceDate.Value, mealType, menuKey, menuOrder)] = serviceMenu;
            serviceMenuById[serviceMenu.Id] = serviceMenu;
            result.MealHistoryRows++;
        }

        // 07_식단재료_이관
        foreach (var row in reader.ReadRows("07_식단재료_이관"))
        {
            var serviceDate = XlsxCellParser.ParseDate(row.GetValueOrDefault("일자"));
            var mealName = XlsxCellParser.CleanText(row.GetValueOrDefault("배식유형"));
            var mealType = MapMealCode(mealName);
            var menuCode = XlsxCellParser.CleanText(row.GetValueOrDefault("메뉴ID"));
            var menuName = XlsxCellParser.CleanText(row.GetValueOrDefault("메뉴명"));
            var menuOrder = XlsxCellParser.CleanInt(row.GetValueOrDefault("메뉴순서")) ?? 1;
            var ingredientCode = XlsxCellParser.CleanText(row.GetValueOrDefault("재료ID"));
            if (!serviceDate.HasValue)
                continue;
            var menuKey = string.IsNullOrEmpty(menuCode) ? menuName : menuCode;
            if (!serviceMenuMap.TryGetValue((serviceDate.Value, mealType, menuKey, menuOrder), out var serviceMenu))
                continue;
            ingredientByCode.TryGetValue(ingredientCode ?? string.Empty, out var ingredient);
            var ingredientName = XlsxCellParser.CleanText(row.GetValueOrDefault("표준재료명"))
                is { Length: > 0 } s ? s : XlsxCellParser.CleanText(row.GetValueOrDefault("원본재료명"));
            if (string.IsNullOrEmpty(ingredientName))
                continue;
            if (ingredient is not null)
            {
                if (!serviceMenuIngredientIds.ContainsKey(serviceMenu.Id))
                    serviceMenuIngredientIds[serviceMenu.Id] = [];
                serviceMenuIngredientIds[serviceMenu.Id].Add(ingredient.Id);
            }
            var sortOrder = XlsxCellParser.CleanInt(row.GetValueOrDefault("재료순서")) ?? 1;
            var existing = await db.MealServiceMenuIngredients
                .FirstOrDefaultAsync(i => i.MealServiceMenuId == serviceMenu.Id &&
                                          i.SortOrder == sortOrder &&
                                          i.IngredientNameSnapshot == ingredientName, cancellationToken);
            if (existing is null)
            {
                existing = new MealServiceMenuIngredient { MealServiceMenuId = serviceMenu.Id, ServiceMenu = serviceMenu };
                db.MealServiceMenuIngredients.Add(existing);
            }
            var total = XlsxCellParser.CleanDouble(row.GetValueOrDefault("수량"));
            var planned = serviceMap.TryGetValue((serviceDate.Value, mealType), out var svc) ? svc.PlannedCount : 0;
            existing.Ingredient = ingredient;
            existing.IngredientId = ingredient?.Id;
            existing.SortOrder = sortOrder;
            existing.IngredientNameSnapshot = ingredientName;
            existing.QuantityTotal = total;
            existing.QuantityPer100 = total is not null && planned > 0 ? total * 100 / planned : null;
            existing.Unit = XlsxCellParser.CleanText(row.GetValueOrDefault("단위")) is { Length: > 0 } u
                ? u
                : ingredient?.DefaultUnit;
            existing.SourceNote = XlsxCellParser.CleanText(row.GetValueOrDefault("원본비고")) is { Length: > 0 } n ? n : null;
            existing.SourceRow = XlsxCellParser.CleanText(row.GetValueOrDefault("원본행")) is { Length: > 0 } rowText ? rowText : null;
            result.MealIngredientRows++;
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var (serviceMenuId, ingredientIds) in serviceMenuIngredientIds)
        {
            if (!serviceMenuById.TryGetValue(serviceMenuId, out var serviceMenu) || !serviceMenu.MenuId.HasValue)
                continue;
            var key = CompositionKey.Create(ingredientIds);
            if (!recipeByMenuKey.TryGetValue((serviceMenu.MenuId.Value, key), out var recipe))
                recipe = defaultRecipeByMenu.GetValueOrDefault(serviceMenu.MenuId.Value);
            if (recipe is null)
                continue;
            serviceMenu.RecipeId = recipe.Id;
            serviceMenu.RecipeNameSnapshot = recipe.Name;
            serviceMenu.RecipeVersionSnapshot = recipe.Version;
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static async Task ClearBusinessDataAsync(CafeteriaDbContext db, CancellationToken cancellationToken)
    {
        await db.MealActuals.ExecuteDeleteAsync(cancellationToken);
        await db.PreservationRecords.ExecuteDeleteAsync(cancellationToken);
        await db.MealServiceMenuIngredients.ExecuteDeleteAsync(cancellationToken);
        await db.MealServiceMenus.ExecuteDeleteAsync(cancellationToken);
        await db.MealServices.ExecuteDeleteAsync(cancellationToken);
        await db.RecipeIngredients.ExecuteDeleteAsync(cancellationToken);
        await db.Recipes.ExecuteDeleteAsync(cancellationToken);
        await db.IngredientAliases.ExecuteDeleteAsync(cancellationToken);
        await db.Ingredients.ExecuteDeleteAsync(cancellationToken);
        await db.Menus.ExecuteDeleteAsync(cancellationToken);
        await db.MealTypeSettings.ExecuteDeleteAsync(cancellationToken);
    }

    private static string MapMealCode(string name)
    {
        return name switch
        {
            "중식" => "LUNCH",
            "석식" => "DINNER",
            _ => name.ToUpperInvariant(),
        };
    }

    private static MealType ParseMealType(string code)
        => Enum.Parse<MealType>(code, true);

    private static async Task<Menu?> FindMenuAsync(CafeteriaDbContext db, string? code, string name, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(code))
        {
            var byCode = await db.Menus.FirstOrDefaultAsync(m => m.SourceCode == code, cancellationToken);
            if (byCode is not null)
                return byCode;
        }
        return await db.Menus.FirstOrDefaultAsync(m => m.Name == name, cancellationToken);
    }

    private static async Task<Ingredient?> FindIngredientAsync(CafeteriaDbContext db, string? code, string name, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(code))
        {
            var byCode = await db.Ingredients.FirstOrDefaultAsync(i => i.SourceCode == code, cancellationToken);
            if (byCode is not null)
                return byCode;
        }
        return await db.Ingredients.FirstOrDefaultAsync(i => i.Name == name, cancellationToken);
    }

    private static async Task<int> DefaultCountAsync(CafeteriaDbContext db, string mealType, CancellationToken cancellationToken)
    {
        var setting = await db.MealTypeSettings.FirstOrDefaultAsync(s => s.Code == mealType, cancellationToken);
        return setting?.DefaultPlannedCount ?? 0;
    }

    private static async Task<TimeOnly?> DefaultTimeAsync(CafeteriaDbContext db, string mealType, CancellationToken cancellationToken)
    {
        var setting = await db.MealTypeSettings.FirstOrDefaultAsync(s => s.Code == mealType, cancellationToken);
        return setting?.DefaultServiceTime;
    }

    private static string CompositionKeyFromIngredients(IEnumerable<int> ids)
        => CompositionKey.Create(ids);

    private static Dictionary<int, Dictionary<string, List<RecipeSourceRow>>> GroupRecipeRowsByComposition(List<RecipeSourceRow> rows)
    {
        var grouped = new Dictionary<int, Dictionary<string, List<RecipeSourceRow>>>();
        var currentBlock = new Dictionary<int, List<RecipeSourceRow>>();
        var lastSort = new Dictionary<int, int>();

        void Flush(int menuId)
        {
            if (!currentBlock.TryGetValue(menuId, out var list) || list.Count == 0)
                return;
            var key = CompositionKeyFromIngredients(list.Select(r => r.Ingredient.Id));
            if (!grouped.ContainsKey(menuId))
                grouped[menuId] = [];
            if (!grouped[menuId].ContainsKey(key))
                grouped[menuId][key] = [];
            grouped[menuId][key].AddRange(list);
            currentBlock[menuId] = [];
        }

        foreach (var row in rows)
        {
            var menuId = row.Menu.Id;
            var sort = row.SortOrder;
            if (lastSort.TryGetValue(menuId, out var last) && sort <= last)
                Flush(menuId);
            if (!currentBlock.ContainsKey(menuId))
                currentBlock[menuId] = [];
            currentBlock[menuId].Add(row);
            lastSort[menuId] = sort;
        }

        foreach (var menuId in currentBlock.Keys.ToList())
            Flush(menuId);

        return grouped;
    }

    private sealed record RecipeSourceRow(
        Menu Menu,
        Ingredient Ingredient,
        int SortOrder,
        double? QuantityPer100,
        string? Unit,
        string ReviewStatus);
}

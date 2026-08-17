using ClosedXML.Excel;
using KpicCafeteria.Application.Documents;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// Excel 데이터 아카이브 생성 검증.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\admin.py (_build_excel)
/// </summary>
public class ExcelExportServiceTests
{
    private static readonly DateOnly Monday = new(2026, 8, 17);

    private static async Task<DocumentTestHarness> CreateHarnessAsync()
    {
        var harness = new DocumentTestHarness();
        using (var db = harness.CreateContext())
        {
            var menu = new Menu { Name = "제육볶음", Role = "MAIN", Active = true, ReviewStatus = "APPROVED" };
            var ingredient = new Ingredient { Name = "돼지고기", StatGroup = "육류", DefaultUnit = "kg", KgFactor = 1.0, Active = true, ReviewStatus = "APPROVED" };
            db.Menus.Add(menu);
            db.Ingredients.Add(ingredient);
            await db.SaveChangesAsync();

            var recipe = new Recipe
            {
                MenuId = menu.Id,
                Name = "기본 레시피",
                Version = 1,
                Active = true,
                IsDefault = true,
                CompositionKey = "default",
                Ingredients =
                [
                    new RecipeIngredient
                    {
                        IngredientId = ingredient.Id,
                        QuantityPer100 = 10,
                        Unit = "kg",
                        SortOrder = 1,
                    },
                ],
            };
            db.Recipes.Add(recipe);

            db.MealServices.Add(new MealService
            {
                ServiceDate = Monday,
                MealType = MealType.LUNCH,
                PlannedCount = 400,
                ServiceTime = new TimeOnly(11, 40),
                Menus =
                [
                    new MealServiceMenu
                    {
                        MenuNameSnapshot = "제육볶음",
                        SortOrder = 1,
                        CookingInstruction = "볶는다",
                    },
                ],
                Preservation = new PreservationRecord
                {
                    CollectionTime = "12:30",
                    CollectorName = "김영양사",
                    ManagerName = "이관리자",
                    FreezerTemperature = "-18°C",
                },
                Actual = new MealActual { ActualCount = 380 },
            });
            await db.SaveChangesAsync();
        }

        return harness;
    }

    [Fact]
    public async Task Export_ProducesWorkbookWithEightSheets()
    {
        using var harness = await CreateHarnessAsync();
        var service = harness.CreateExcelExportService();

        var (content, filename) = await service.ExportAsync(Monday, Monday);

        Assert.NotEmpty(content);
        Assert.Equal($"데이터아카이브_{Monday:yyyyMMdd}-{Monday:yyyyMMdd}.xlsx", filename);

        using var workbook = new XLWorkbook(new MemoryStream(content));
        var expected = new[]
        {
            "식단기록", "조리지시서", "보존식기록", "실제식수",
            "메뉴기준정보", "재료기준정보", "레시피", "식단재료",
            "식사유형설정", "발주기록", "발주그룹",
        };
        Assert.Equal(expected, workbook.Worksheets.Select(ws => ws.Name).ToArray());
    }

    [Fact]
    public async Task Export_ContainsSeededRows()
    {
        using var harness = await CreateHarnessAsync();
        var service = harness.CreateExcelExportService();

        var (content, _) = await service.ExportAsync(Monday, Monday);

        using var workbook = new XLWorkbook(new MemoryStream(content));
        var mealSheet = workbook.Worksheet("식단기록");
        Assert.Contains(mealSheet.RowsUsed().Skip(1), row => row.Cell(6).GetString() == "제육볶음");

        var recipeSheet = workbook.Worksheet("레시피");
        Assert.Contains(recipeSheet.RowsUsed().Skip(1), row => row.Cell(5).GetString() == "돼지고기");
    }

    [Fact]
    public async Task Export_StartAfterEnd_Throws()
    {
        using var harness = await CreateHarnessAsync();
        var service = harness.CreateExcelExportService();

        await Assert.ThrowsAsync<DocumentException>(() => service.ExportAsync(Monday.AddDays(1), Monday));
    }

    [Fact]
    public async Task SaveToArchive_WritesFile()
    {
        using var harness = await CreateHarnessAsync();
        var service = harness.CreateExcelExportService();

        var path = await service.SaveToArchiveAsync(Monday, Monday);

        Assert.True(File.Exists(path));
        Assert.NotEmpty(await File.ReadAllBytesAsync(path));
    }
}

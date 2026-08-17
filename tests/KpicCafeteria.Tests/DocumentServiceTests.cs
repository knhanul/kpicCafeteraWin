using System.IO.Compression;
using KpicCafeteria.Application.Documents;
using KpicCafeteria.Application.Workspace;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Documents.Templates;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// 문서 생성 파이프라인 검증 (HWPX 생성, PDF 변환, 출력 기록).
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\services\document_hwpx.py
/// C:\Pjt\kpicCafeteria\backend\app\routers\documents.py
/// </summary>
public class DocumentServiceTests
{
    private static readonly DateOnly Monday = new(2026, 8, 17);

    private static async Task<DocumentTestHarness> CreateHarnessWithServiceAsync(
        string documentType = "MEAL_PLAN",
        bool withPreservation = false)
    {
        var harness = new DocumentTestHarness();

        // 활성 양식 등록
        var templateService = harness.CreateTemplateService();
        var bytes = DefaultTemplateResources.TryGetTemplateBytes(documentType)
            ?? throw new InvalidOperationException($"임베디드 기본 양식 없음: {documentType}");
        await templateService.RegisterAsync(documentType, "기본 양식", bytes, "default.hwpx", activate: true);

        // 배식 + 메뉴 + 재료 시드
        using (var db = harness.CreateContext())
        {
            var service = new MealService
            {
                ServiceDate = Monday,
                MealType = MealType.LUNCH,
                PlannedCount = 400,
                ServiceTime = new TimeOnly(11, 40),
                ConceptTitle = "여름 보양식",
                Menus =
                [
                    new MealServiceMenu
                    {
                        MenuNameSnapshot = "제육볶음",
                        SortOrder = 1,
                        CookingInstruction = "중불에서 볶는다",
                        CookingNote = "간장 추가",
                        Ingredients =
                        [
                            new MealServiceMenuIngredient
                            {
                                IngredientNameSnapshot = "돼지고기",
                                QuantityTotal = 40,
                                QuantityPer100 = 10,
                                Unit = "kg",
                                SortOrder = 1,
                            },
                        ],
                    },
                ],
            };
            if (withPreservation)
            {
                service.Preservation = new PreservationRecord
                {
                    CollectionTime = "12:30",
                    CollectorName = "김영양사",
                    ManagerName = "이관리자",
                    FreezerTemperature = "-18°C",
                    DisposalAt = new DateTime(2026, 8, 24, 10, 0, 0),
                };
            }

            db.MealServices.Add(service);
            await db.SaveChangesAsync();
        }

        return harness;
    }

    // =======================================================================
    // HWPX 생성
    // =======================================================================

    [Fact]
    public async Task GenerateHwpx_MealPlan_ProducesValidHwpx()
    {
        using var harness = await CreateHarnessWithServiceAsync("MEAL_PLAN");
        var service = harness.CreateDocumentService();

        var (content, filename) = await service.GenerateHwpxAsync("MEAL_PLAN", startDate: Monday, endDate: Monday);

        Assert.NotEmpty(content);
        Assert.EndsWith(".hwpx", filename);
        Assert.Contains("식단표", filename);

        // 유효한 ZIP + section0 포함
        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.FullName == "Contents/section0.xml");

        // 남은 플레이스홀더 없음
        var section = zip.GetEntry("Contents/section0.xml")!;
        using var reader = new StreamReader(section.Open());
        var xml = await reader.ReadToEndAsync();
        Assert.DoesNotContain("{{", xml);
    }

    [Fact]
    public async Task GenerateHwpx_CookingInstruction_ProducesValidHwpx()
    {
        using var harness = await CreateHarnessWithServiceAsync("COOKING_INSTRUCTION");
        var service = harness.CreateDocumentService();

        var (content, filename) = await service.GenerateHwpxAsync("COOKING_INSTRUCTION", startDate: Monday, endDate: Monday);

        Assert.NotEmpty(content);
        Assert.Contains("조리지시서", filename);
        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.FullName == "Contents/section0.xml");
    }

    [Fact]
    public async Task GenerateHwpx_PreservationRecord_ProducesValidHwpx()
    {
        using var harness = await CreateHarnessWithServiceAsync("PRESERVATION_RECORD", withPreservation: true);
        var service = harness.CreateDocumentService();

        var (content, filename) = await service.GenerateHwpxAsync("PRESERVATION_RECORD", startDate: Monday, endDate: Monday);

        Assert.NotEmpty(content);
        Assert.Contains("보존식", filename);
        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        Assert.Contains(zip.Entries, e => e.FullName == "Contents/section0.xml");
    }

    [Fact]
    public async Task GenerateHwpx_NoServices_Throws()
    {
        using var harness = new DocumentTestHarness();
        var templateService = harness.CreateTemplateService();
        var bytes = DefaultTemplateResources.TryGetTemplateBytes("MEAL_PLAN")!;
        await templateService.RegisterAsync("MEAL_PLAN", "기본", bytes, "d.hwpx", activate: true);
        var service = harness.CreateDocumentService();

        await Assert.ThrowsAsync<NoServicesException>(() =>
            service.GenerateHwpxAsync("MEAL_PLAN", startDate: Monday, endDate: Monday));
    }

    [Fact]
    public async Task GenerateHwpx_NoActiveTemplate_Throws()
    {
        using var harness = new DocumentTestHarness();
        await harness.CreateWorkspaceService().CreateServiceAsync(new ServiceCreateInput(Monday, MealType.LUNCH));
        var service = harness.CreateDocumentService();

        await Assert.ThrowsAsync<ActiveTemplateNotFoundException>(() =>
            service.GenerateHwpxAsync("MEAL_PLAN", startDate: Monday, endDate: Monday));
    }

    [Fact]
    public async Task GenerateHwpx_UnsupportedType_Throws()
    {
        using var harness = await CreateHarnessWithServiceAsync("MEAL_PLAN");
        var service = harness.CreateDocumentService();

        await Assert.ThrowsAsync<UnsupportedDocumentTypeException>(() =>
            service.GenerateHwpxAsync("PURCHASE_ORDER", startDate: Monday, endDate: Monday));
    }

    [Fact]
    public async Task GenerateHwpx_MealPlan_LineBreakCountMatchesMenus()
    {
        using var harness = await CreateHarnessWithMultipleMenusAsync();
        var service = harness.CreateDocumentService();

        var (content, _) = await service.GenerateHwpxAsync("MEAL_PLAN", startDate: Monday, endDate: Monday);

        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var section = zip.GetEntry("Contents/section0.xml")!;
        using var reader = new StreamReader(section.Open());
        var xml = await reader.ReadToEndAsync();

        var lineBreakCount = System.Text.RegularExpressions.Regex.Matches(xml, "lineBreak").Count;
        Assert.True(lineBreakCount > 0, "lineBreak 요소가 하나도 없습니다.");
        Assert.DoesNotContain("lineBreak/><hp:lineBreak", xml, StringComparison.OrdinalIgnoreCase);

        var tElements = System.Text.RegularExpressions.Regex.Matches(xml, @"<hp:t[^>]*>(.*?)</hp:t>", System.Text.RegularExpressions.RegexOptions.Singleline);
        foreach (System.Text.RegularExpressions.Match t in tElements)
        {
            Assert.DoesNotContain("\r", t.Value);
            Assert.DoesNotContain("\n", t.Value);
        }
    }

    private static async Task<DocumentTestHarness> CreateHarnessWithMultipleMenusAsync()
    {
        var harness = new DocumentTestHarness();

        var templateService = harness.CreateTemplateService();
        var bytes = DefaultTemplateResources.TryGetTemplateBytes("MEAL_PLAN")
            ?? throw new InvalidOperationException("임베디드 기본 양식 없음: MEAL_PLAN");
        await templateService.RegisterAsync("MEAL_PLAN", "기본 양식", bytes, "default.hwpx", activate: true);

        using (var db = harness.CreateContext())
        {
            var menus = new[] { "잡곡밥", "부대찌개", "감자채볶음", "쫄면", "샐러드", "깍두기" };
            var service = new MealService
            {
                ServiceDate = Monday,
                MealType = MealType.LUNCH,
                PlannedCount = 400,
                ServiceTime = new TimeOnly(11, 40),
                Menus = menus.Select((name, i) => new MealServiceMenu
                {
                    MenuNameSnapshot = name,
                    SortOrder = i + 1,
                }).ToList(),
            };
            db.MealServices.Add(service);
            await db.SaveChangesAsync();
        }

        return harness;
    }

    // =======================================================================
    // PDF 생성
    // =======================================================================

    [Fact]
    public async Task GeneratePdf_UsesRenderer_ReturnsPdfBytes()
    {
        using var harness = await CreateHarnessWithServiceAsync("MEAL_PLAN");
        var service = harness.CreateDocumentService();

        var (content, filename) = await service.GeneratePdfAsync("MEAL_PLAN", startDate: Monday, endDate: Monday);

        Assert.StartsWith("%PDF", System.Text.Encoding.UTF8.GetString(content));
        Assert.EndsWith(".pdf", filename);
    }

    // =======================================================================
    // 출력 기록
    // =======================================================================

    [Fact]
    public async Task MarkOutput_CookingInstruction_SetsCookingOutputAt()
    {
        using var harness = await CreateHarnessWithServiceAsync("COOKING_INSTRUCTION");
        var service = harness.CreateDocumentService();
        var services = await service.ResolveServicesAsync(null, Monday, Monday);

        await service.MarkOutputAsync("COOKING_INSTRUCTION", services);

        using var db = harness.CreateContext();
        var row = db.MealServices.Single();
        Assert.NotNull(row.CookingOutputAt);
        Assert.Null(row.MealPlanOutputAt);
    }

    [Fact]
    public async Task MarkOutput_MealPlan_SetsMealPlanOutputAt()
    {
        using var harness = await CreateHarnessWithServiceAsync("MEAL_PLAN");
        var service = harness.CreateDocumentService();
        var services = await service.ResolveServicesAsync(null, Monday, Monday);

        await service.MarkOutputAsync("MEAL_PLAN", services);

        using var db = harness.CreateContext();
        var row = db.MealServices.Single();
        Assert.NotNull(row.MealPlanOutputAt);
        Assert.Null(row.CookingOutputAt);
    }
}

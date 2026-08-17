using System.IO.Compression;
using System.Xml.Linq;
using KpicCafeteria.Application.Documents;
using KpicCafeteria.Documents.Documents;
using KpicCafeteria.Documents.Hwpx;
using KpicCafeteria.Documents.Templates;
using KpicCafeteria.Tests.TestInfrastructure;

namespace KpicCafeteria.Tests;

/// <summary>
/// 6단계 문서 출력 시스템 최종 검증.
/// 실제 임베디드 HWPX 템플릿을 사용해 렌더링 결과(ZIP/XML 구조, 플레이스홀더 잔존,
/// 반복 페이지 수, 필드 일치)와 이번 수정 사항의 회귀를 검증한다.
/// </summary>
public class DocumentSystemVerificationTests
{
    private static readonly DateOnly Monday = new(2026, 8, 17);

    // =======================================================================
    // 회귀 테스트: 이번 수정에서 확인된 사항
    // =======================================================================

    [Fact]
    public void TopLevelParagraphPlaceholder_IsReplaced()
    {
        // 식단표 템플릿의 {{PERIOD_TITLE}}은 최상위 p 요소(헤더 컨트롤 포함 run)에 위치한다.
        var bytes = DefaultTemplateResources.TryGetTemplateBytes("MEAL_PLAN")!;
        var content = DocumentRenderer.RenderBytes(bytes, "MEAL_PLAN", MealPlanPayload(weeks: 2));

        var xml = ReadSection(content, "Contents/section0.xml");
        Assert.DoesNotContain("{{", xml);
        Assert.Contains("8월 3주", xml); // WeekLabel.PeriodLabel 형식
    }

    [Fact]
    public void Preservation_UsesSampleHourAndMinute()
    {
        var bytes = DefaultTemplateResources.TryGetTemplateBytes("PRESERVATION_RECORD")!;
        var content = DocumentRenderer.RenderBytes(bytes, "PRESERVATION_RECORD", PreservationPayload(meals: 1));

        var xml = ReadSection(content, "Contents/section0.xml");
        Assert.DoesNotContain("{{", xml);
        Assert.DoesNotContain("{{B1_SAMPLE_HOUR}}", xml);
        Assert.Contains("12", xml);
        Assert.Contains("30", xml);
    }

    [Fact]
    public void Preservation_RequiredPlaceholders_DoNotIncludeSampleDatetime()
    {
        var required = HwpxPlaceholder.RequiredByType["PRESERVATION_RECORD"];
        Assert.DoesNotContain("B1_SAMPLE_DATETIME", required);
        Assert.DoesNotContain("PERIOD_TITLE", required);
        Assert.Contains("B1_SAMPLE_HOUR", required);
        Assert.Contains("B1_SAMPLE_MINUTE", required);
    }

    [Fact]
    public async Task UnsupportedDocumentType_ThrowsBeforeTemplateLookup()
    {
        // 템플릿을 하나도 등록하지 않은 상태에서도 UnsupportedDocumentTypeException이 먼저 발생해야 한다.
        using var harness = new DocumentTestHarness();
        var service = harness.CreateDocumentService();

        await Assert.ThrowsAsync<UnsupportedDocumentTypeException>(() =>
            service.GetActiveTemplatePathAsync("PURCHASE_ORDER"));
    }

    // =======================================================================
    // 실제 템플릿 필드와 Renderer 필드 일치
    // =======================================================================

    [Theory]
    [InlineData("MEAL_PLAN")]
    [InlineData("COOKING_INSTRUCTION")]
    [InlineData("PRESERVATION_RECORD")]
    public void TemplatePlaceholders_AreCoveredByRequiredFields(string documentType)
    {
        var bytes = DefaultTemplateResources.TryGetTemplateBytes(documentType)!;
        var validation = HwpxTemplateValidator.ValidateTemplateBytes(bytes, documentType, "default.hwpx");
        var required = HwpxPlaceholder.RequiredByType[documentType];

        // 템플릿의 모든 플레이스홀더는 필수 필드 목록에 포함되어야 한다 (렌더러가 반드시 채운다).
        var missing = validation.Placeholders.Where(p => !required.Contains(p)).ToList();
        Assert.True(missing.Count == 0, $"템플릿 플레이스홀더 중 필수 목록에 없는 항목: {string.Join(", ", missing)}");
    }

    // =======================================================================
    // 반복 페이지 경계값
    // =======================================================================

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(6, 3)]
    public void MealPlan_RepeatPageCount_MatchesCeilWeeksOverTwo(int weeks, int expectedPages)
    {
        var bytes = DefaultTemplateResources.TryGetTemplateBytes("MEAL_PLAN")!;
        var content = DocumentRenderer.RenderBytes(bytes, "MEAL_PLAN", MealPlanPayload(weeks));

        Assert.Equal(expectedPages, PageCount(content));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(5, 5)]
    [InlineData(10, 10)]
    public void CookingInstruction_RepeatPageCount_MatchesDayCount(int days, int expectedPages)
    {
        var bytes = DefaultTemplateResources.TryGetTemplateBytes("COOKING_INSTRUCTION")!;
        var content = DocumentRenderer.RenderBytes(bytes, "COOKING_INSTRUCTION", CookingPayload(days));

        Assert.Equal(expectedPages, PageCount(content));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(6, 2)]
    [InlineData(7, 3)]
    [InlineData(10, 4)]
    public void Preservation_RepeatPageCount_MatchesCeilMealsOverThree(int meals, int expectedPages)
    {
        var bytes = DefaultTemplateResources.TryGetTemplateBytes("PRESERVATION_RECORD")!;
        var content = DocumentRenderer.RenderBytes(bytes, "PRESERVATION_RECORD", PreservationPayload(meals));

        Assert.Equal(expectedPages, PageCount(content));
    }

    // =======================================================================
    // 실제 템플릿 문서 생성 (ZIP/XML 구조)
    // =======================================================================

    [Theory]
    [InlineData("MEAL_PLAN", "식단표")]
    [InlineData("COOKING_INSTRUCTION", "조리지시서")]
    [InlineData("PRESERVATION_RECORD", "보존식")]
    public async Task GenerateHwpx_RealTemplate_ProducesValidStructure(string documentType, string filenamePart)
    {
        using var harness = new DocumentTestHarness();
        var templateService = harness.CreateTemplateService();
        var bytes = DefaultTemplateResources.TryGetTemplateBytes(documentType)!;
        await templateService.RegisterAsync(documentType, "기본 양식", bytes, "default.hwpx", activate: true);

        // 배식 시드 (LUNCH + DINNER, 보존식 포함)
        using (var db = harness.CreateContext())
        {
            var meal = new KpicCafeteria.Domain.Entities.MealService
            {
                ServiceDate = Monday,
                MealType = KpicCafeteria.Domain.Enums.MealType.LUNCH,
                PlannedCount = 400,
                ServiceTime = new TimeOnly(11, 40),
                ConceptTitle = "여름 보양식",
                Menus =
                [
                    new KpicCafeteria.Domain.Entities.MealServiceMenu
                    {
                        MenuNameSnapshot = "제육볶음",
                        SortOrder = 1,
                        CookingInstruction = "중불에서 볶는다",
                        CookingNote = "간장 추가",
                        Ingredients =
                        [
                            new KpicCafeteria.Domain.Entities.MealServiceMenuIngredient
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
            if (documentType == "PRESERVATION_RECORD")
            {
                meal.Preservation = new KpicCafeteria.Domain.Entities.PreservationRecord
                {
                    CollectionTime = "12:30",
                    CollectorName = "김영양사",
                    ManagerName = "이관리자",
                    FreezerTemperature = "-18°C",
                    DisposalAt = new DateTime(2026, 8, 24, 10, 0, 0),
                };
            }

            db.MealServices.Add(meal);
            db.SaveChanges();
        }

        var service = harness.CreateDocumentService();
        var (content, filename) = await service.GenerateHwpxAsync(documentType, startDate: Monday, endDate: Monday);

        Assert.NotEmpty(content);
        Assert.Contains(filenamePart, filename);
        Assert.EndsWith(".hwpx", filename);

        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        // HWPX 필수 구조
        Assert.Contains(zip.Entries, e => e.FullName == "mimetype");
        Assert.Contains(zip.Entries, e => e.FullName == "Contents/content.hpf");
        Assert.Contains(zip.Entries, e => e.FullName == "Contents/section0.xml");

        // XML 파싱 가능 + 플레이스홀더 잔존 없음 (모든 섹션)
        foreach (var entry in zip.Entries.Where(e => e.FullName.StartsWith("Contents/") && e.FullName.EndsWith(".xml")))
        {
            using var reader = new StreamReader(entry.Open());
            var xml = reader.ReadToEnd();
            XDocument.Parse(xml); // 구조 정상
            Assert.DoesNotContain("{{", xml);
        }
    }

    // =======================================================================
    // 헬퍼
    // =======================================================================

    private static string ReadSection(byte[] content, string sectionName)
    {
        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var entry = zip.GetEntry(sectionName) ?? throw new InvalidOperationException($"섹션 없음: {sectionName}");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    /// <summary>반복 페이지 수 = pageBreak="1" 최상위 p 개수 + 1.</summary>
    private static int PageCount(byte[] content)
    {
        var xml = ReadSection(content, "Contents/section0.xml");
        var root = XDocument.Parse(xml).Root!;
        var pageBreaks = root.Elements()
            .Where(e => e.Name.LocalName == "p" && e.Attribute("pageBreak")?.Value == "1")
            .Count();
        return pageBreaks + 1;
    }

    private static MealPlanPayload MealPlanPayload(int weeks)
    {
        var weekPayloads = Enumerable.Range(0, weeks).Select(weekIndex =>
        {
            var start = Monday.AddDays(weekIndex * 7);
            var days = Enumerable.Range(0, 5).Select(dayIndex =>
            {
                var date = start.AddDays(dayIndex);
                return new MealPlanDayPayload(
                    date,
                    $"{date:MM.dd}(월)",
                    "월요일",
                    new MealPlanServicePayload("LUNCH", "중식", 400, "11:40", "여름 보양식", ["제육볶음"]),
                    new MealPlanServicePayload("DINNER", "석식", 100, "17:30", null, []));
            }).ToList();
            return new MealPlanWeekPayload(start, start.AddDays(4), $"{weekIndex + 1}주차", days);
        }).ToList();

        return new MealPlanPayload("식단표", WeekLabel.PeriodLabel(weekPayloads[0].Start, weekPayloads[^1].End), weekPayloads);
    }

    private static CookingPayload CookingPayload(int days)
    {
        var dayPayloads = Enumerable.Range(0, days).Select(dayIndex =>
        {
            var date = Monday.AddDays(dayIndex);
            return new CookingDayPayload(
                date,
                $"{date:yyyy년 MM월 dd일}",
                "월요일",
                [
                    new CookingMealPayload("LUNCH", "중식", 400, "11:40",
                    [
                        new CookingMenuPayload("제육볶음",
                        [
                            new CookingIngredientPayload("돼지고기", 40, 10, "kg", ""),
                        ], "중불에서 볶는다", "간장 추가"),
                    ]),
                    new CookingMealPayload("DINNER", "석식", 100, "17:30", []),
                ]);
        }).ToList();

        return new CookingPayload("조리지시서", "2026년 8월 17일", dayPayloads);
    }

    private static PreservationPayload PreservationPayload(int meals)
    {
        var records = Enumerable.Range(0, meals).Select(index =>
        {
            var date = Monday.AddDays(index / 2);
            return new PreservationRecordPayload(
                $"{date:yyyy년 MM월 dd일} 월요일 중식",
                "월요일",
                "중식",
                new DateTime(2026, 8, 17, 12, 30, 0),
                "이관리자",
                ["제육볶음"],
                "-18°C",
                "2026년 08월 24일 10시 00분",
                "김영양사",
                "12:30");
        }).ToList();

        return new PreservationPayload("보존식 기록지", "2026년 8월 17일", records);
    }
}

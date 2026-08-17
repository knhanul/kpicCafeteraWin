using ClosedXML.Excel;
using KpicCafeteria.Domain.Entities;

namespace KpicCafeteria.Documents.Excel;

/// <summary>
/// Excel 데이터 아카이브 생성.
/// 기존 admin.py _build_excel에 대응. 사용자목록 시트는 제외한다.
/// </summary>
public static class ExcelArchiveExporter
{
    public static byte[] Build(
        IReadOnlyList<MealService> services,
        IReadOnlyList<Menu> menus,
        IReadOnlyList<Ingredient> ingredients,
        IReadOnlyList<Recipe> recipes,
        IReadOnlyList<MealTypeSetting> mealTypes,
        IReadOnlyList<OrderItem>? orderItems = null,
        IReadOnlyList<OrderGroup>? orderGroups = null)
    {
        orderItems ??= [];
        orderGroups ??= [];
        using var workbook = new XLWorkbook();

        var menusByService = services
            .SelectMany(s => s.Menus.Select(m => (Service: s, Menu: m)))
            .GroupBy(x => x.Service.Id)
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Menu.SortOrder).Select(x => x.Menu).ToList());

        // 1. 식단 기록
        AddSheet(workbook, "식단기록", ["날짜", "식사구분", "계획인원", "서비스시간", "콘셉트", "메뉴목록", "비고"],
            services.Select(s => new object?[]
            {
                s.ServiceDate.ToString("yyyy-MM-dd"),
                s.MealType.ToString(),
                s.PlannedCount,
                s.ServiceTime?.ToString("HH\\:mm") ?? "",
                s.ConceptTitle ?? "",
                string.Join(", ", menusByService.GetValueOrDefault(s.Id, []).Select(m => m.MenuNameSnapshot)),
                s.Note ?? "",
            }));

        // 2. 조리지시서
        var cookingRows = services
            .SelectMany(s => menusByService.GetValueOrDefault(s.Id, []).Select(m => new object?[]
            {
                s.ServiceDate.ToString("yyyy-MM-dd"),
                s.MealType.ToString(),
                m.MenuNameSnapshot,
                m.CookingInstruction ?? "",
                m.CookingNote ?? "",
            }));
        AddSheet(workbook, "조리지시서", ["날짜", "식사구분", "메뉴명", "조리지시", "조리비고"], cookingRows);

        // 3. 보존식 기록
        var preservationRows = services
            .Where(s => s.Preservation is not null)
            .Select(s => new object?[]
            {
                s.ServiceDate.ToString("yyyy-MM-dd"),
                s.MealType.ToString(),
                s.Preservation!.CollectionTime ?? "",
                s.Preservation.CollectorName ?? "",
                s.Preservation.FreezerTemperature ?? "",
                s.Preservation.ManagerName ?? "",
                s.Preservation.DisposalAt?.ToString("yyyy-MM-dd HH\\:mm") ?? "",
                s.Preservation.Note ?? "",
            });
        AddSheet(workbook, "보존식기록", ["날짜", "식사구분", "채수시간", "채수자", "냉동고온도", "관리자", "폐기시간", "비고"], preservationRows);

        // 4. 실제 식수
        var actualRows = services
            .Where(s => s.Actual is not null)
            .Select(s => new object?[]
            {
                s.ServiceDate.ToString("yyyy-MM-dd"),
                s.MealType.ToString(),
                s.Actual!.ActualCount,
                s.Actual.Note ?? "",
                s.Actual.RecordedAt?.ToString("yyyy-MM-dd HH\\:mm") ?? "",
            });
        AddSheet(workbook, "실제식수", ["날짜", "식사구분", "실제인원", "비고", "기록시간"], actualRows);

        // 5. 메뉴 기준정보
        AddSheet(workbook, "메뉴기준정보", ["ID", "메뉴명", "역할", "사용여부", "검토상태"],
            menus.OrderBy(m => m.Name).Select(m => new object?[]
            {
                m.Id, m.Name, m.Role, m.Active ? "사용" : "미사용", m.ReviewStatus,
            }));

        // 6. 재료 기준정보
        AddSheet(workbook, "재료기준정보", ["ID", "재료명", "통계분석군", "기본단위", "kg환산계수", "사용여부"],
            ingredients.OrderBy(i => i.Name).Select(i => new object?[]
            {
                i.Id, i.Name, i.StatGroup, i.DefaultUnit ?? "", i.KgFactor, i.Active ? "사용" : "미사용",
            }));

        // 7. 레시피
        var recipeRows = recipes
            .OrderBy(r => r.Id)
            .SelectMany(r => r.Ingredients.OrderBy(ri => ri.SortOrder).Select(ri => new object?[]
            {
                r.Id,
                r.Menu?.Name ?? "",
                r.Name,
                r.Version,
                ri.Ingredient?.Name ?? "",
                ri.QuantityPer100,
                ri.Unit ?? "",
            }));
        AddSheet(workbook, "레시피", ["레시피ID", "메뉴명", "레시피명", "버전", "재료명", "100인분량", "단위"], recipeRows);

        // 8. 식단재료
        var ingredientRows = services
            .SelectMany(s => s.Menus.SelectMany(m => (m.Ingredients ?? [])
                .OrderBy(i => i.SortOrder)
                .Select(i => new object?[]
                {
                    s.ServiceDate.ToString("yyyy-MM-dd"),
                    s.MealType.ToString(),
                    m.MenuNameSnapshot,
                    i.IngredientNameSnapshot,
                    i.QuantityTotal,
                    i.QuantityPer100,
                    i.Unit ?? "",
                    i.SourceNote ?? "",
                })));
        AddSheet(workbook, "식단재료", ["사용일", "MealType", "메뉴명", "재료명", "QuantityTotal", "QuantityPer100", "Unit", "SourceNote"], ingredientRows);

        // 9. 식사유형 설정
        AddSheet(workbook, "식사유형설정", ["코드", "이름", "기본인원", "기본시간", "정렬순서", "사용여부"],
            mealTypes.OrderBy(mt => mt.SortOrder).Select(mt => new object?[]
            {
                mt.Code, mt.Name, mt.DefaultPlannedCount, mt.DefaultServiceTime?.ToString("HH\\:mm") ?? "", mt.SortOrder, mt.Active ? "사용" : "미사용",
            }));

        // 10. 발주기록
        var orderRows = orderItems
            .OrderBy(i => i.ServiceDate)
            .Select(i => new object?[]
            {
                i.ServiceDate.ToString("yyyy-MM-dd"),
                i.IngredientNameSnapshot,
                i.Ingredient?.Name ?? "",
                i.RequiredQuantity,
                i.RequiredUnit ?? "",
                i.OrderQuantity,
                i.OrderUnit ?? "",
                i.OrderDate?.ToString("yyyy-MM-dd") ?? "",
                i.DeliveryDate?.ToString("yyyy-MM-dd") ?? "",
                i.Status.ToString(),
                i.OrderGroup?.Id,
                i.OrderNote ?? "",
            });
        AddSheet(workbook, "발주기록", ["사용일", "재료명", "기준재료", "필요량", "필요단위", "발주량", "발주단위", "발주일", "배송일", "상태", "그룹ID", "비고"], orderRows);

        // 11. 발주그룹
        var groupRows = orderGroups
            .OrderBy(g => g.OrderDate)
            .Select(g => new object?[]
            {
                g.Id,
                g.IngredientNameSnapshot,
                g.OrderQuantity,
                g.OrderUnit ?? "",
                g.OrderDate?.ToString("yyyy-MM-dd") ?? "",
                g.DeliveryDate?.ToString("yyyy-MM-dd") ?? "",
                g.TotalRequiredQuantity,
                g.RequiredUnit ?? "",
            });
        AddSheet(workbook, "발주그룹", ["그룹ID", "재료명", "발주량", "발주단위", "발주일", "배송일", "총필요량", "필요단위"], groupRows);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddSheet(XLWorkbook workbook, string title, IReadOnlyList<string> headers, IEnumerable<object?[]> rows)
    {
        var sheet = workbook.Worksheets.Add(title);
        for (var index = 0; index < headers.Count; index++)
        {
            sheet.Cell(1, index + 1).Value = headers[index];
        }

        var rowIndex = 2;
        foreach (var row in rows)
        {
            for (var index = 0; index < row.Length; index++)
            {
                var value = row[index];
                if (value is not null)
                {
                    sheet.Cell(rowIndex, index + 1).Value = XLCellValue.FromObject(value);
                }
            }

            rowIndex++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(1, rowIndex - 1, 4, 50);
    }
}

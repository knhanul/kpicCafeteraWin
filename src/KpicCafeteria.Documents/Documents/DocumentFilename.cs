namespace KpicCafeteria.Documents.Documents;

/// <summary>
/// 문서 파일명 규칙.
/// 기존 document_hwpx.py filename_for_dto/_dated_filename에 대응.
/// </summary>
public static class DocumentFilename
{
    public static string ForDto(object dto, string extension = "hwpx")
    {
        var baseName = dto switch
        {
            MealPlanDocumentDto mealPlan => $"식단표_{mealPlan.Period.StartDate:yyyyMMdd}_{mealPlan.Period.EndDate:yyyyMMdd}",
            CookingInstructionDocumentDto cooking => DatedFilename("조리지시서", cooking.Days.Select(d => d.Date).ToList()),
            PreservationRecordDocumentDto preservation => DatedFilename("보존식기록지", preservation.Records.Select(r => r.Date).ToList()),
            _ => throw new ArgumentException($"지원하지 않는 DTO입니다: {dto.GetType().Name}", nameof(dto)),
        };
        return $"{baseName}.{extension}";
    }

    public static string DatedFilename(string prefix, IReadOnlyList<DateOnly> dates)
    {
        if (dates.Count == 0)
        {
            return prefix;
        }

        var start = dates.Min();
        var end = dates.Max();
        return start == end
            ? $"{prefix}_{start:yyyyMMdd}"
            : $"{prefix}_{start:yyyyMMdd}_{end:yyyyMMdd}";
    }
}

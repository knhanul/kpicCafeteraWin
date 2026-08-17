using KpicCafeteria.Documents.Hwpx;

namespace KpicCafeteria.Documents.Documents;

/// <summary>
/// 렌더러 페이로드 (DTO → Payload).
/// 기존 document_hwpx.py _dto_to_payload에 대응.
/// </summary>
public sealed record MealPlanServicePayload(
    string MealType,
    string MealName,
    int? PlannedCount,
    string ServiceTime,
    string? ConceptTitle,
    IReadOnlyList<string> Menus);

public sealed record MealPlanDayPayload(
    DateOnly Date,
    string DateLabel,
    string Weekday,
    MealPlanServicePayload Lunch,
    MealPlanServicePayload Dinner);

public sealed record MealPlanWeekPayload(
    DateOnly Start,
    DateOnly End,
    string WeekLabel,
    IReadOnlyList<MealPlanDayPayload> Days);

public sealed record MealPlanPayload(
    string Title,
    string PeriodLabel,
    IReadOnlyList<MealPlanWeekPayload> Weeks);

public sealed record CookingIngredientPayload(
    string Name,
    double? QuantityTotal,
    double? QuantityPer100,
    string Unit,
    string Remark);

public sealed record CookingMenuPayload(
    string Name,
    IReadOnlyList<CookingIngredientPayload> Ingredients,
    string Instruction,
    string Note);

public sealed record CookingMealPayload(
    string MealType,
    string MealName,
    int? PlannedCount,
    string ServiceTime,
    IReadOnlyList<CookingMenuPayload> Menus);

public sealed record CookingDayPayload(
    DateOnly Date,
    string DateLabel,
    string Weekday,
    IReadOnlyList<CookingMealPayload> Services);

public sealed record CookingPayload(
    string Title,
    string PeriodLabel,
    IReadOnlyList<CookingDayPayload> Days);

public sealed record PreservationRecordPayload(
    string DateLabel,
    string Weekday,
    string MealName,
    DateTime? SampleDatetime,
    string ManagerName,
    IReadOnlyList<string> MenuItems,
    string FreezerTemperature,
    string DiscardDatetime,
    string CollectorName,
    string CollectionTime);

public sealed record PreservationPayload(
    string Title,
    string PeriodLabel,
    IReadOnlyList<PreservationRecordPayload> Records);

/// <summary>DTO → Payload 변환.</summary>
public static class DocumentPayloadBuilder
{
    public static MealPlanPayload ToMealPlanPayload(MealPlanDocumentDto dto)
    {
        var weeks = dto.Weeks.Select(week => new MealPlanWeekPayload(
            week.StartDate,
            week.EndDate,
            WeekLabel.WeekLabelOf(week.StartDate),
            week.Days.Select(day => new MealPlanDayPayload(
                day.Date,
                day.DateLabel,
                day.Weekday,
                ToService(day.Lunch),
                ToService(day.Dinner))).ToList())).ToList();

        var periodLabel = dto.Weeks.Count > 0
            ? WeekLabel.PeriodLabel(dto.Weeks[0].StartDate, dto.Weeks[^1].EndDate)
            : "";

        return new MealPlanPayload(dto.Title, periodLabel, weeks);
    }

    private static MealPlanServicePayload ToService(MealPlanMealDto meal)
    {
        return new MealPlanServicePayload(
            meal.MealType.ToUpperInvariant(),
            meal.MealName,
            meal.MealCount,
            meal.ServiceTime is { } time ? time.ToString("HH\\:mm") : "",
            meal.ConceptTitle,
            meal.Menus.ToList());
    }

    public static CookingPayload ToCookingPayload(CookingInstructionDocumentDto dto)
    {
        var firstDate = dto.Days.Count > 0 ? dto.Days[0].Date : (DateOnly?)null;
        var lastDate = dto.Days.Count > 0 ? dto.Days[^1].Date : (DateOnly?)null;
        var periodLabel = firstDate is { } first && lastDate is { } last ? WeekLabel.PeriodLabel(first, last) : "";

        var days = dto.Days.Select(day => new CookingDayPayload(
            day.Date,
            day.DateLabel,
            day.Weekday,
            new[] { ToService(day.Lunch), ToService(day.Dinner) }.ToList())).ToList();

        return new CookingPayload(dto.Title, periodLabel, days);
    }

    private static CookingMealPayload ToService(CookingInstructionMealDto meal)
    {
        return new CookingMealPayload(
            meal.MealType.ToUpperInvariant(),
            meal.MealName,
            meal.MealCount,
            meal.ServiceTime is { } time ? time.ToString("HH\\:mm") : "",
            meal.Menus.Select(menu => new CookingMenuPayload(
                menu.Name,
                menu.Ingredients.Select(item => new CookingIngredientPayload(
                    item.Name,
                    item.Quantity,
                    item.QuantityPer100,
                    item.Unit ?? "",
                    item.Remark ?? "")).ToList(),
                menu.Instruction ?? "",
                menu.Note ?? "")).ToList());
    }

    public static PreservationPayload ToPreservationPayload(PreservationRecordDocumentDto dto)
    {
        var firstDate = dto.Records.Count > 0 ? dto.Records[0].Date : (DateOnly?)null;
        var lastDate = dto.Records.Count > 0 ? dto.Records[^1].Date : (DateOnly?)null;
        var periodLabel = firstDate is { } first && lastDate is { } last ? WeekLabel.PeriodLabel(first, last) : "";

        var records = dto.Records.Select(record => new PreservationRecordPayload(
            $"{record.DateLabel} {record.Weekday} {record.MealName}",
            record.Weekday,
            record.MealName,
            record.CollectedAt,
            record.Manager ?? "",
            record.Menus.ToList(),
            record.FreezerTemperature ?? "",
            FormatDateTime(record.DisposalAt),
            record.Collector ?? "",
            record.CollectionTime ?? "")).ToList();

        return new PreservationPayload(dto.Title, periodLabel, records);
    }

    private static string FormatDateTime(DateTime? value)
    {
        return value is { } dt ? $"{dt:yyyy년 MM월 dd일 HH시 mm분}" : "";
    }
}

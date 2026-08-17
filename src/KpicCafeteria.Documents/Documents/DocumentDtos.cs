namespace KpicCafeteria.Documents.Documents;

/// <summary>
/// 문서 DTO.
/// 기존 document_dtos.py에 대응.
/// </summary>
public sealed record DocumentPeriodDto(DateOnly StartDate, DateOnly EndDate);

public sealed record MealPlanMealDto(
    string MealType,
    string MealName,
    int? MealCount,
    TimeOnly? ServiceTime,
    string? ConceptTitle,
    IReadOnlyList<string> Menus);

public sealed record MealPlanDayDto(
    DateOnly Date,
    string DateLabel,
    string Weekday,
    MealPlanMealDto Lunch,
    MealPlanMealDto Dinner);

public sealed record MealPlanWeekDto(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<MealPlanDayDto> Days);

public sealed record MealPlanDocumentDto(
    DocumentPeriodDto Period,
    string Title,
    IReadOnlyList<MealPlanWeekDto> Weeks);

public sealed record CookingInstructionIngredientDto(
    string Name,
    double? Quantity,
    double? QuantityPer100,
    string? Unit,
    string? Remark);

public sealed record CookingInstructionMenuDto(
    string Name,
    IReadOnlyList<CookingInstructionIngredientDto> Ingredients,
    string? Instruction,
    string? Note);

public sealed record CookingInstructionMealDto(
    string MealType,
    string MealName,
    int? MealCount,
    TimeOnly? ServiceTime,
    IReadOnlyList<CookingInstructionMenuDto> Menus);

public sealed record CookingInstructionDayDto(
    DateOnly Date,
    string DateLabel,
    string Weekday,
    CookingInstructionMealDto Lunch,
    CookingInstructionMealDto Dinner);

public sealed record CookingInstructionDocumentDto(
    string Title,
    IReadOnlyList<CookingInstructionDayDto> Days);

public sealed record PreservationRecordBlockDto(
    DateOnly Date,
    string DateLabel,
    string Weekday,
    string MealType,
    string MealName,
    IReadOnlyList<string> Menus,
    string? CollectionTime,
    DateTime? CollectedAt,
    string? Manager,
    string? FreezerTemperature,
    DateOnly? DiscardDate,
    DateTime? DisposalAt,
    string? Collector);

public sealed record PreservationRecordDocumentDto(
    string Title,
    IReadOnlyList<PreservationRecordBlockDto> Records);

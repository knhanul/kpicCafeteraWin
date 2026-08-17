using KpicCafeteria.Domain.Enums;

namespace KpicCafeteria.Application.Workspace;

// ---------------------------------------------------------------------------
// 기간 조회
// ---------------------------------------------------------------------------

public sealed record WorkspacePeriodDto(
    DateOnly StartDate,
    DateOnly EndDate,
    int WeekCount,
    IReadOnlyList<WorkspaceWeekDto> Weeks);

public sealed record WorkspaceWeekDto(
    DateOnly WeekStart,
    DateOnly WeekEnd,
    IReadOnlyList<WorkspaceDayDto> Days);

public sealed record WorkspaceDayDto(
    DateOnly Date,
    string Weekday,
    IReadOnlyList<MealServiceDto> Services);

// ---------------------------------------------------------------------------
// MealService
// ---------------------------------------------------------------------------

public sealed record MealServiceDto(
    int Id,
    DateOnly ServiceDate,
    MealType MealType,
    string MealTypeName,
    int PlannedCount,
    TimeOnly? ServiceTime,
    string? ConceptTitle,
    string? Note,
    bool PreservationCompleted,
    bool ActualRecorded,
    int? ActualCount,
    IReadOnlyList<MealServiceMenuDto> Menus);

public sealed record MealServiceMenuDto(
    int Id,
    int? MenuId,
    int? RecipeId,
    string? RecipeName,
    int? RecipeVersion,
    string Name,
    int SortOrder,
    string? Note,
    bool IsRepresentative,
    string? CookingInstruction,
    string? CookingNote,
    IReadOnlyList<MealServiceIngredientDto> Ingredients);

public sealed record MealServiceIngredientDto(
    int Id,
    int? IngredientId,
    string Name,
    string StatGroup,
    double? QuantityTotal,
    double? QuantityPer100,
    string? Unit,
    string? SourceNote);

// ---------------------------------------------------------------------------
// 입력
// ---------------------------------------------------------------------------

public sealed record ServiceCreateInput(DateOnly ServiceDate, MealType MealType);

public sealed record ServiceUpdateInput(int PlannedCount, string? ServiceTime, string? ConceptTitle, string? Note);

public sealed record AddMenuInput(int MenuId, int? RecipeId);

public sealed record BatchAddMenuItemInput(int MenuId, int? RecipeId, int SortOrder);

public sealed record ServiceMenuInput(string? Note, bool IsRepresentative, string? CookingInstruction, string? CookingNote);

public sealed record IngredientSnapshotInput(int? IngredientId, string Name, double? QuantityTotal, double? QuantityPer100, string? Unit, string? SourceNote);

public sealed record MealEditorIngredientInput(int? IngredientId, string Name, double? QuantityTotal, string? Unit);

public sealed record MealEditorMenuInput(int? ServiceMenuId, string? Note, bool IsRepresentative, IReadOnlyList<MealEditorIngredientInput> Ingredients);

public sealed record MealEditorInput(
    int PlannedCount,
    string? ServiceTime,
    string? ConceptTitle,
    string? Note,
    IReadOnlyList<MealEditorMenuInput> Menus);

// ---------------------------------------------------------------------------
// 보존식 / 실제 식수
// ---------------------------------------------------------------------------

public sealed record PreservationRecordDto(
    int ServiceId,
    DateTime? CollectedAt,
    string? ManagerName,
    string? FreezerTemperature,
    DateTime? DisposalAt,
    string? CollectorName,
    string? CollectionTime,
    string? Note,
    bool Completed,
    DateTime? CompletedAt);

public sealed record PreservationInput(
    DateTime? CollectedAt,
    string? ManagerName,
    string? FreezerTemperature,
    DateTime? DisposalAt,
    string? CollectorName,
    string? CollectionTime,
    string? Note,
    bool Completed);

public sealed record MealActualDto(
    int ServiceId,
    int PlannedCount,
    int? ActualCount,
    string? Note,
    DateTime? RecordedAt);

public sealed record ActualInput(int? ActualCount, string? Note);

// ---------------------------------------------------------------------------
// 메뉴 선택기
// ---------------------------------------------------------------------------

public sealed record MenuPickerRecipeDto(
    int Id,
    string Name,
    int Version,
    bool IsDefault,
    int IngredientCount,
    IReadOnlyList<string> IngredientSummary);

public sealed record MenuPickerItemDto(
    int Id,
    string Name,
    string Role,
    bool AlreadyAdded,
    int? DefaultRecipeId,
    IReadOnlyList<MenuPickerRecipeDto> Recipes);

public sealed record MenuPickerResultDto(
    IReadOnlyList<MenuPickerItemDto> Items,
    int Total);

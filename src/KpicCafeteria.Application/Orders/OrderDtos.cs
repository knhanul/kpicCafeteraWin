namespace KpicCafeteria.Application.Orders;

/// <summary>발주 항목의 사용 메뉴 출처 (Snapshot 기준).</summary>
public sealed record OrderSourceMenuDto(
    string MenuName,
    double Quantity,
    string? Unit,
    DateOnly ServiceDate,
    string MealType,
    string MealTypeName);

/// <summary>발주 항목 (재료별/사용일별 공통 데이터 모델).</summary>
public sealed record OrderItemDto(
    int? Id,
    DateOnly ServiceDate,
    int? IngredientId,
    string IngredientName,
    double? RequiredQuantity,
    string? RequiredUnit,
    double? SuggestedOrderQuantity,
    string? SuggestedUnit,
    bool PackageCompatible,
    double? PackageQuantity,
    string? PackageUnit,
    double? OrderQuantity,
    string? OrderUnit,
    DateOnly? OrderDate,
    DateOnly? DeliveryDate,
    string Status,
    bool InPlan,
    string? OrderNote,
    int? OrderGroupId,
    double? OrderGroupQuantity,
    string? OrderGroupUnit,
    IReadOnlyList<OrderSourceMenuDto> SourceMenus);

/// <summary>기간별 발주 조회 결과.</summary>
public sealed record OrderListResultDto(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<OrderItemDto> Items);

/// <summary>발주 항목 저장 입력 (upsert 키: ServiceDate + IngredientId 또는 ServiceDate + 재료명 스냅샷).</summary>
public sealed record OrderItemSaveInput(
    DateOnly ServiceDate,
    int? IngredientId,
    string IngredientName,
    double? RequiredQuantity,
    string? RequiredUnit,
    double? OrderQuantity,
    string? OrderUnit,
    DateOnly? OrderDate,
    DateOnly? DeliveryDate,
    string Status,
    string? OrderNote);

/// <summary>묶음 발주 생성 입력.</summary>
public sealed record OrderGroupCreateInput(
    IReadOnlyList<OrderItemSaveInput> Items,
    double? OrderQuantity,
    string? OrderUnit,
    DateOnly? OrderDate,
    DateOnly? DeliveryDate);

/// <summary>일괄 변경 입력 (변경 항목이 하나도 없으면 실행하지 않는다).</summary>
public sealed record OrderBulkUpdateInput(
    IReadOnlyList<OrderItemSaveInput> Items,
    DateOnly? OrderDate,
    DateOnly? DeliveryDate,
    string? Status);

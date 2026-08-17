using KpicCafeteria.Domain.Common;
using KpicCafeteria.Domain.Enums;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 발주 항목.
/// 기존 models.py OrderItem (order_items)에 대응.
/// (service_date, ingredient_id) UNIQUE — 단, ingredient_id가 NULL이면
/// SQLite 특성상 중복 허용되며, 업무 규칙상 (service_date, 재료명 스냅샷)으로 구분한다.
/// </summary>
public class OrderItem : IHasCreatedAt, IHasUpdatedAt
{
    public int Id { get; set; }

    /// <summary>사용일.</summary>
    public DateOnly ServiceDate { get; set; }

    /// <summary>재료 참조 (삭제 시 SET NULL, 스냅샷은 유지).</summary>
    public int? IngredientId { get; set; }

    /// <summary>재료명 스냅샷.</summary>
    public string IngredientNameSnapshot { get; set; } = string.Empty;

    /// <summary>식단 집계 필요량.</summary>
    public double? RequiredQuantity { get; set; }

    public string? RequiredUnit { get; set; }

    /// <summary>사용자 발주량.</summary>
    public double? OrderQuantity { get; set; }

    public string? OrderUnit { get; set; }

    /// <summary>발주일.</summary>
    public DateOnly? OrderDate { get; set; }

    /// <summary>배송일.</summary>
    public DateOnly? DeliveryDate { get; set; }

    /// <summary>발주 비고 (사용자 입력, 재고관리 용도 아님).</summary>
    public string? OrderNote { get; set; }

    /// <summary>발주 상태 (DB에는 "pending"/"ordered"/"skipped" 문자열로 저장).</summary>
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    /// <summary>묶음 발주 그룹 참조 (삭제 시 SET NULL).</summary>
    public int? OrderGroupId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Ingredient? Ingredient { get; set; }

    public OrderGroup? OrderGroup { get; set; }
}

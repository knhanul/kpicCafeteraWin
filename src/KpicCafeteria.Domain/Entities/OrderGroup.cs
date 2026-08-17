using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 묶음 발주 그룹 (같은 재료의 여러 사용일 항목 묶음).
/// 기존 models.py OrderGroup (order_groups)에 대응.
/// </summary>
public class OrderGroup : IHasCreatedAt
{
    public int Id { get; set; }

    /// <summary>재료 참조 (삭제 시 SET NULL, 스냅샷은 유지).</summary>
    public int? IngredientId { get; set; }

    /// <summary>재료명 스냅샷.</summary>
    public string IngredientNameSnapshot { get; set; } = string.Empty;

    /// <summary>묶음 발주량.</summary>
    public double? OrderQuantity { get; set; }

    public string? OrderUnit { get; set; }

    /// <summary>발주일.</summary>
    public DateOnly? OrderDate { get; set; }

    /// <summary>배송일.</summary>
    public DateOnly? DeliveryDate { get; set; }

    /// <summary>항목 필요량 합계.</summary>
    public double? TotalRequiredQuantity { get; set; }

    public string? RequiredUnit { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }
}

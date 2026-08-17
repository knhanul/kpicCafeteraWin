namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 식단 재료 스냅샷.
/// 기존 models.py MealServiceMenuIngredient (meal_service_menu_ingredients)에 대응.
/// 기준 재료가 나중에 수정/삭제되어도 스냅샷은 자동 변경되지 않는다.
/// </summary>
public class MealServiceMenuIngredient
{
    public int Id { get; set; }

    public int MealServiceMenuId { get; set; }

    /// <summary>기준 재료 참조 (삭제 시 SET NULL, 스냅샷은 유지).</summary>
    public int? IngredientId { get; set; }

    public int SortOrder { get; set; } = 1;

    /// <summary>재료명 스냅샷.</summary>
    public string IngredientNameSnapshot { get; set; } = string.Empty;

    /// <summary>총 수량 (계획식수 기준).</summary>
    public double? QuantityTotal { get; set; }

    /// <summary>100인 기준 수량.</summary>
    public double? QuantityPer100 { get; set; }

    public string? Unit { get; set; }

    /// <summary>원본 비고.</summary>
    public string? SourceNote { get; set; }

    /// <summary>이관 원본 행.</summary>
    public string? SourceRow { get; set; }

    public MealServiceMenu? ServiceMenu { get; set; }

    public Ingredient? Ingredient { get; set; }
}

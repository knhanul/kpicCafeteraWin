namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 식단 메뉴 (스냅샷).
/// 기존 models.py MealServiceMenu (meal_service_menus)에 대응.
/// 기준 메뉴/레시피가 나중에 수정되어도 과거 식단은 자동 변경되지 않는다.
/// </summary>
public class MealServiceMenu
{
    public int Id { get; set; }

    public int MealServiceId { get; set; }

    /// <summary>기준 메뉴 참조 (삭제 시 SET NULL, 스냅샷은 유지).</summary>
    public int? MenuId { get; set; }

    /// <summary>적용 레시피 참조 (삭제 시 SET NULL, 스냅샷은 유지).</summary>
    public int? RecipeId { get; set; }

    public int SortOrder { get; set; } = 1;

    /// <summary>메뉴명 스냅샷 (추가 시점의 메뉴명).</summary>
    public string MenuNameSnapshot { get; set; } = string.Empty;

    /// <summary>레시피명 스냅샷.</summary>
    public string? RecipeNameSnapshot { get; set; }

    /// <summary>레시피 버전 스냅샷.</summary>
    public int? RecipeVersionSnapshot { get; set; }

    /// <summary>메뉴 비고.</summary>
    public string? Note { get; set; }

    /// <summary>대표 메뉴 (배식당 1개, 주찬 자동 지정).</summary>
    public bool IsRepresentative { get; set; }

    /// <summary>조리지시.</summary>
    public string? CookingInstruction { get; set; }

    /// <summary>조리비고.</summary>
    public string? CookingNote { get; set; }

    public MealService? Service { get; set; }

    public Menu? Menu { get; set; }

    public Recipe? SourceRecipe { get; set; }

    /// <summary>재료 스냅샷 목록 (sort_order 순, cascade delete-orphan).</summary>
    public List<MealServiceMenuIngredient> Ingredients { get; set; } = [];
}

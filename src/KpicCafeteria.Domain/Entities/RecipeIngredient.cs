namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 레시피 재료 (100인 기준 수량).
/// 기존 models.py RecipeIngredient (recipe_ingredients)에 대응.
/// </summary>
public class RecipeIngredient
{
    public int Id { get; set; }

    public int RecipeId { get; set; }

    public int IngredientId { get; set; }

    public int SortOrder { get; set; } = 1;

    /// <summary>100인 기준 수량.</summary>
    public double? QuantityPer100 { get; set; }

    /// <summary>단위 (재료 기본단위 fallback).</summary>
    public string? Unit { get; set; }

    /// <summary>주재료 표시.</summary>
    public bool IsPrimary { get; set; }

    public string ReviewStatus { get; set; } = "정상";

    public Recipe? Recipe { get; set; }

    public Ingredient? Ingredient { get; set; }
}

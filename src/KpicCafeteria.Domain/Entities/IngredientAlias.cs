namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 재료 별칭.
/// 기존 models.py IngredientAlias (ingredient_aliases)에 대응.
/// </summary>
public class IngredientAlias
{
    public int Id { get; set; }

    /// <summary>원재료별칭 (UNIQUE, 검색 키).</summary>
    public string Alias { get; set; } = string.Empty;

    public int IngredientId { get; set; }

    /// <summary>출처 ("기존데이터"/"사용자").</summary>
    public string? Source { get; set; }

    public Ingredient? Ingredient { get; set; }
}

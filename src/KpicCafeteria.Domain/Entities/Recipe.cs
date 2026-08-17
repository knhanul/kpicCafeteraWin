using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 레시피 (메뉴의 재료 구성 버전).
/// 기존 models.py Recipe (recipes)에 대응.
/// (menu_id, version) UNIQUE, (menu_id, composition_key) UNIQUE.
/// 삭제는 물리 삭제가 아니라 Active=false 미사용 처리.
/// </summary>
public class Recipe : IHasCreatedAt, IHasUpdatedAt
{
    public int Id { get; set; }

    public int MenuId { get; set; }

    /// <summary>레시피명.</summary>
    public string Name { get; set; } = "기본 레시피";

    /// <summary>메뉴별 순차 버전.</summary>
    public int Version { get; set; } = 1;

    /// <summary>재료 구성 키 (CompositionKey.Create 결과).</summary>
    public string CompositionKey { get; set; } = string.Empty;

    public string? Note { get; set; }

    /// <summary>기본 레시피 여부 (메뉴당 1개).</summary>
    public bool IsDefault { get; set; }

    /// <summary>사용 여부 (미사용 처리).</summary>
    public bool Active { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Menu? Menu { get; set; }

    /// <summary>재료 목록 (sort_order 순, cascade delete-orphan).</summary>
    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

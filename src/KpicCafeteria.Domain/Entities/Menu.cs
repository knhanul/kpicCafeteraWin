using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 메뉴 기준정보.
/// 기존 models.py Menu (menus)에 대응.
/// 삭제는 물리 삭제가 아니라 Active=false 미사용 처리.
/// </summary>
public class Menu : IHasCreatedAt, IHasUpdatedAt
{
    public int Id { get; set; }

    /// <summary>이관 원본 코드.</summary>
    public string? SourceCode { get; set; }

    /// <summary>메뉴명 (UNIQUE).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>통계집계메뉴명.</summary>
    public string CanonicalName { get; set; } = string.Empty;

    /// <summary>메뉴역할 (주찬/부찬 등).</summary>
    public string Role { get; set; } = "기타";

    /// <summary>사용 여부 (미사용 처리).</summary>
    public bool Active { get; set; } = true;

    /// <summary>검토 상태.</summary>
    public string ReviewStatus { get; set; } = "정상";

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>레시피 목록 (버전 순, cascade delete-orphan).</summary>
    public List<Recipe> Recipes { get; set; } = [];
}

using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 재료 기준정보.
/// 기존 models.py Ingredient (ingredients)에 대응.
/// 삭제는 물리 삭제가 아니라 Active=false 미사용 처리.
/// </summary>
public class Ingredient : IHasCreatedAt, IHasUpdatedAt
{
    public int Id { get; set; }

    /// <summary>이관 원본 코드.</summary>
    public string? SourceCode { get; set; }

    /// <summary>표준재료명 (UNIQUE).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>통계분석군.</summary>
    public string StatGroup { get; set; } = "기타";

    /// <summary>기본 단위.</summary>
    public string? DefaultUnit { get; set; }

    /// <summary>판매 포장수량 (예: 2). 없으면 자유 발주 대상.</summary>
    public double? PurchasePackageQuantity { get; set; }

    /// <summary>판매 포장단위 (예: kg). 없으면 자유 발주 대상.</summary>
    public string? PurchasePackageUnit { get; set; }

    /// <summary>kg 환산계수 (통계 중량 계산용).</summary>
    public double? KgFactor { get; set; }

    /// <summary>통계 분석 제외 여부.</summary>
    public bool AnalysisExcluded { get; set; }

    /// <summary>사용 여부 (미사용 처리).</summary>
    public bool Active { get; set; } = true;

    /// <summary>검토 상태.</summary>
    public string ReviewStatus { get; set; } = "정상";

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>별칭 목록 (cascade delete-orphan).</summary>
    public List<IngredientAlias> Aliases { get; set; } = [];
}

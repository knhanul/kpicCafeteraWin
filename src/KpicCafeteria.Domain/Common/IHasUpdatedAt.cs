namespace KpicCafeteria.Domain.Common;

/// <summary>
/// 수정 시각(UTC)을 가지는 엔티티 마커.
/// 기존 models.py의 updated_at 컬럼에 대응한다.
/// </summary>
public interface IHasUpdatedAt
{
    DateTime? UpdatedAt { get; set; }
}

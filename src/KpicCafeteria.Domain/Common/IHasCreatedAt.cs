namespace KpicCafeteria.Domain.Common;

/// <summary>
/// 생성 시각(UTC)을 가지는 엔티티 마커.
/// 기존 models.py의 created_at 컬럼에 대응한다.
/// </summary>
public interface IHasCreatedAt
{
    DateTime CreatedAt { get; set; }
}

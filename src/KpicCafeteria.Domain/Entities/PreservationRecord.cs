using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 보존식 기록 (배식당 1건).
/// 기존 models.py PreservationRecord (preservation_records)에 대응.
/// </summary>
public class PreservationRecord : IHasUpdatedAt
{
    public int Id { get; set; }

    public int MealServiceId { get; set; }

    /// <summary>채수일시.</summary>
    public DateTime? CollectedAt { get; set; }

    /// <summary>관리자.</summary>
    public string? ManagerName { get; set; }

    /// <summary>냉동고 온도 (문자열, 형식 검증 없음).</summary>
    public string? FreezerTemperature { get; set; }

    /// <summary>폐기일시.</summary>
    public DateTime? DisposalAt { get; set; }

    /// <summary>채수자.</summary>
    public string? CollectorName { get; set; }

    /// <summary>채수시간 (HH:MM 문자열).</summary>
    public string? CollectionTime { get; set; }

    public string? Note { get; set; }

    /// <summary>완료 시각 (completed 체크 시 기록).</summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public MealService? Service { get; set; }
}

using KpicCafeteria.Domain.Common;
using KpicCafeteria.Domain.Enums;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 배식 (식단 일자 + 식사 유형).
/// 기존 models.py MealService (meal_services)에 대응.
/// (service_date, meal_type) UNIQUE.
/// </summary>
public class MealService : IHasCreatedAt, IHasUpdatedAt
{
    public int Id { get; set; }

    /// <summary>서비스 날짜 (순수 업무 날짜, TimeZone 변환 대상 아님).</summary>
    public DateOnly ServiceDate { get; set; }

    /// <summary>배식 유형 (DB에는 "LUNCH"/"DINNER" 문자열로 저장).</summary>
    public MealType MealType { get; set; }

    /// <summary>계획식수.</summary>
    public int PlannedCount { get; set; }

    /// <summary>배식시간.</summary>
    public TimeOnly? ServiceTime { get; set; }

    /// <summary>콘셉트 (예: "여름 보양식").</summary>
    public string? ConceptTitle { get; set; }

    public string? Note { get; set; }

    /// <summary>식단표 출력 시각.</summary>
    public DateTime? MealPlanOutputAt { get; set; }

    /// <summary>조리지시서 출력 시각.</summary>
    public DateTime? CookingOutputAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    /// <summary>식단 메뉴 목록 (sort_order 순, cascade delete-orphan).</summary>
    public List<MealServiceMenu> Menus { get; set; } = [];

    /// <summary>보존식 기록 (1:1, cascade delete-orphan).</summary>
    public PreservationRecord? Preservation { get; set; }

    /// <summary>실제 식수 (1:1, cascade delete-orphan).</summary>
    public MealActual? Actual { get; set; }
}

using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 배식유형 설정 (중식/석식 기본값).
/// 기존 models.py MealTypeSetting (meal_type_settings)에 대응.
/// </summary>
public class MealTypeSetting : IHasCreatedAt, IHasUpdatedAt
{
    public int Id { get; set; }

    /// <summary>배식유형 코드 ("LUNCH"/"DINNER").</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>배식유형 이름 ("중식"/"석식").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>기본 계획식수.</summary>
    public int DefaultPlannedCount { get; set; }

    /// <summary>기본 배식시간.</summary>
    public TimeOnly? DefaultServiceTime { get; set; }

    /// <summary>화면 정렬 순서.</summary>
    public int SortOrder { get; set; }

    /// <summary>사용 여부.</summary>
    public bool Active { get; set; } = true;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}

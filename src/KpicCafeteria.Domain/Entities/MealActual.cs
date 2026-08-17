namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 실제 식수 (배식당 1건, 보존식 기록과 독립 저장).
/// 기존 models.py MealActual (meal_actuals)에 대응.
/// </summary>
public class MealActual
{
    public int Id { get; set; }

    public int MealServiceId { get; set; }

    /// <summary>실제 식수.</summary>
    public int? ActualCount { get; set; }

    public string? Note { get; set; }

    /// <summary>입력 시각.</summary>
    public DateTime? RecordedAt { get; set; }

    public MealService? Service { get; set; }
}

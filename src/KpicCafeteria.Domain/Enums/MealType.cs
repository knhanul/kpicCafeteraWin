namespace KpicCafeteria.Domain.Enums;

/// <summary>
/// 배식 유형. DB에는 기존 코드와 호환되는 문자열("LUNCH"/"DINNER")로 저장된다.
/// </summary>
public enum MealType
{
    LUNCH,
    DINNER,
}

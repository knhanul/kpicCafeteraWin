namespace KpicCafeteria.Domain.Enums;

/// <summary>
/// 발주 상태. DB에는 기존 코드와 호환되는 소문자 문자열("pending"/"ordered"/"skipped")로 저장된다.
/// </summary>
public enum OrderStatus
{
    Pending,
    Ordered,
    Skipped,
}

using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 감사 로그.
/// 기존 models.py AuditLog (audit_logs)에 대응.
/// Windows 버전은 users 테이블이 없으므로 UserId는 FK 없이 일반 정수 컬럼으로 저장한다.
/// </summary>
public class AuditLog : IHasCreatedAt
{
    public int Id { get; set; }

    /// <summary>사용자 ID (Windows 버전: FK 없음, 기존 DB 이관 호환용).</summary>
    public int? UserId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    /// <summary>상세 (JSON).</summary>
    public Dictionary<string, object?> Detail { get; set; } = [];

    public DateTime CreatedAt { get; set; }
}

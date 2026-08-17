using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// HWPX 문서 템플릿.
/// 기존 models.py DocumentTemplate (document_templates)에 대응.
/// </summary>
public class DocumentTemplate : IHasCreatedAt, IHasUpdatedAt
{
    public int Id { get; set; }

    /// <summary>문서 유형 (MEAL_PLAN/COOKING_INSTRUCTION/PRESERVATION_RECORD).</summary>
    public string DocumentType { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string OriginalFilename { get; set; } = string.Empty;

    public string? StoredFilename { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public int? FileSize { get; set; }

    public string? ChecksumSha256 { get; set; }

    /// <summary>활성 여부 (유형당 1개).</summary>
    public bool Active { get; set; }

    /// <summary>유형별 순차 버전.</summary>
    public int Version { get; set; } = 1;

    /// <summary>검증 상태.</summary>
    public bool IsValid { get; set; }

    public string? ValidationMessage { get; set; }

    /// <summary>검출된 플레이스홀더 목록 (JSON).</summary>
    public Dictionary<string, object?>? PlaceholderSummary { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }
}

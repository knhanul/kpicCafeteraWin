using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// XLSX 이관 작업.
/// 기존 models.py ImportJob (import_jobs)에 대응.
/// </summary>
public class ImportJob : IHasCreatedAt
{
    public int Id { get; set; }

    public string Token { get; set; } = string.Empty;

    public string Filename { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    /// <summary>상태 (PREVIEWED/INVALID/COMPLETED/FAILED).</summary>
    public string Status { get; set; } = "PREVIEWED";

    /// <summary>시트별 행 수 요약 (JSON).</summary>
    public Dictionary<string, object?> Summary { get; set; } = [];

    /// <summary>오류 목록 (JSON).</summary>
    public List<Dictionary<string, object?>> Errors { get; set; } = [];

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

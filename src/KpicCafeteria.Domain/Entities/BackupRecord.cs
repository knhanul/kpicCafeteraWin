using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// 백업 기록.
/// 기존 models.py BackupRecord (backup_records)에 대응.
/// </summary>
public class BackupRecord : IHasCreatedAt
{
    public int Id { get; set; }

    public string Filename { get; set; } = string.Empty;

    public string StoredFilename { get; set; } = string.Empty;

    public int? FileSize { get; set; }

    /// <summary>백업 유형 (manual/auto).</summary>
    public string BackupType { get; set; } = "manual";

    public string Status { get; set; } = "completed";

    public string? ChecksumSha256 { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }
}

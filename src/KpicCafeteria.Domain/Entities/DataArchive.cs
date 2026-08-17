using KpicCafeteria.Domain.Common;

namespace KpicCafeteria.Domain.Entities;

/// <summary>
/// Excel 데이터 아카이브 기록.
/// 기존 models.py DataArchive (data_archives)에 대응.
/// </summary>
public class DataArchive : IHasCreatedAt
{
    public int Id { get; set; }

    public string Filename { get; set; } = string.Empty;

    public string StoredFilename { get; set; } = string.Empty;

    public int? FileSize { get; set; }

    public string Status { get; set; } = "completed";

    public DateOnly? DateFrom { get; set; }

    public DateOnly? DateTo { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }
}

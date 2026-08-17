namespace KpicCafeteria.Application.DataManagement;

/// <summary>백업 목록 항목.</summary>
public sealed class BackupInfo
{
    public int Id { get; set; }

    public string Filename { get; set; } = string.Empty;

    public string StoredPath { get; set; } = string.Empty;

    public long? FileSize { get; set; }

    public BackupType BackupType { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime? CreatedAt { get; set; }
}

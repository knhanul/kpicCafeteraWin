namespace KpicCafeteria.Application.DataManagement;

/// <summary>백업 패키지 Manifest.</summary>
public sealed class BackupManifest
{
    public int BackupVersion { get; set; } = 1;

    public DateTime CreatedAt { get; set; }

    public string ApplicationVersion { get; set; } = string.Empty;

    public string DatabaseSchemaVersion { get; set; } = string.Empty;

    public string DatabaseFileName { get; set; } = string.Empty;

    public string BackupType { get; set; } = "manual";

    public Dictionary<string, string> FileChecksums { get; set; } = [];
}

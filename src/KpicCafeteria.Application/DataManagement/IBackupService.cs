namespace KpicCafeteria.Application.DataManagement;

/// <summary>시스템 백업 서비스.</summary>
public interface IBackupService
{
    /// <summary>수동 백업을 생성한다.</summary>
    Task<BackupInfo> CreateManualBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>조건에 맞으면 자동 백업을 생성한다.</summary>
    Task<BackupInfo?> EnsureAutoBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>복구 직전 사전 백업을 생성한다.</summary>
    Task<BackupInfo> CreatePreRestoreBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>저장된 백업 목록을 조회한다.</summary>
    Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>자동 백업 보관 정책에 따라 오래된 파일을 정리한다.</summary>
    Task<int> CleanupAutoBackupsAsync(CancellationToken cancellationToken = default);
}

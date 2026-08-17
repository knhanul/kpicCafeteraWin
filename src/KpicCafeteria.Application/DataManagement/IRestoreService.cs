namespace KpicCafeteria.Application.DataManagement;

/// <summary>시스템 복구 서비스.</summary>
public interface IRestoreService
{
    /// <summary>백업 파일을 검증하고 메타데이터를 반환한다.</summary>
    Task<BackupManifest> ValidateAsync(string packagePath, CancellationToken cancellationToken = default);

    /// <summary>백업 패키지를 복구한다. 성공 시 재시작 필요 여부를 반환한다.</summary>
    Task<bool> RestoreAsync(string packagePath, CancellationToken cancellationToken = default);
}

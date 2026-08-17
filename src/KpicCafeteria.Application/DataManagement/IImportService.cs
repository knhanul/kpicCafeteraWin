namespace KpicCafeteria.Application.DataManagement;

/// <summary>데이터 이관 서비스.</summary>
public interface IImportService
{
    /// <summary>XLSX 파일을 미리보기/검증한다.</summary>
    Task<ImportPreview> PreviewAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>검증된 XLSX를 DB에 적용한다.</summary>
    Task<ImportApplyResult> ApplyAsync(
        string filePath,
        ImportMode mode,
        CancellationToken cancellationToken = default);
}

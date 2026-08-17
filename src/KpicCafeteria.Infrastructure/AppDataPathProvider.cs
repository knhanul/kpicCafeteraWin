using KpicCafeteria.Application.Abstractions;

namespace KpicCafeteria.Infrastructure;

/// <summary>
/// 앱 데이터 저장 경로 제공자.
/// 기본 위치: %LOCALAPPDATA%\KpicCafeteria
/// 각 디렉터리는 접근 시 자동 생성된다.
/// </summary>
public sealed class AppDataPathProvider : IAppDataPathProvider
{
    private readonly string _root;

    public AppDataPathProvider(string? root = null)
    {
        _root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KpicCafeteria");
    }

    public string DataDirectory => EnsureDirectory(Path.Combine(_root, "Data"));

    public string DatabasePath => Path.Combine(DataDirectory, "cafeteria.db");

    public string TemplateDirectory => EnsureDirectory(Path.Combine(_root, "Templates"));

    public string BackupDirectory => EnsureDirectory(Path.Combine(_root, "Backups"));

    public string ArchiveDirectory => EnsureDirectory(Path.Combine(_root, "Archives"));

    public string TempDirectory => EnsureDirectory(Path.Combine(_root, "Temp"));

    public string LogDirectory => EnsureDirectory(Path.Combine(_root, "Logs"));

    private static string EnsureDirectory(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}

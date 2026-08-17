using KpicCafeteria.Application.Abstractions;

namespace KpicCafeteria.Tests.TestInfrastructure;

/// <summary>
/// 테스트용 임시 앱 데이터 경로 제공자.
/// </summary>
public sealed class TestAppDataPathProvider : IAppDataPathProvider, IDisposable
{
    private readonly string _root;

    public TestAppDataPathProvider()
    {
        _root = Path.Combine(Path.GetTempPath(), "KpicCafeteriaTests", Guid.NewGuid().ToString("N"));
    }

    public string DataDirectory => Ensure(Path.Combine(_root, "Data"));

    public string DatabasePath => Path.Combine(DataDirectory, "cafeteria.db");

    public string TemplateDirectory => Ensure(Path.Combine(_root, "Templates"));

    public string BackupDirectory => Ensure(Path.Combine(_root, "Backups"));

    public string ArchiveDirectory => Ensure(Path.Combine(_root, "Archives"));

    public string TempDirectory => Ensure(Path.Combine(_root, "Temp"));

    public string LogDirectory => Ensure(Path.Combine(_root, "Logs"));

    private static string Ensure(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // 정리 실패는 무시
        }
    }
}

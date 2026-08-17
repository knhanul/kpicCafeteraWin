namespace KpicCafeteria.Application.Abstractions;

/// <summary>
/// 앱 데이터 저장 경로 제공자.
/// 기본 위치는 %LOCALAPPDATA%\KpicCafeteria 이며, 각 디렉터리는 접근 시 자동 생성된다.
/// 경로 문자열을 여러 클래스에 하드코딩하지 않는다.
/// </summary>
public interface IAppDataPathProvider
{
    /// <summary>데이터 디렉터리 (DB 파일 위치).</summary>
    string DataDirectory { get; }

    /// <summary>SQLite DB 파일 경로.</summary>
    string DatabasePath { get; }

    /// <summary>HWPX 템플릿 디렉터리.</summary>
    string TemplateDirectory { get; }

    /// <summary>백업 디렉터리.</summary>
    string BackupDirectory { get; }

    /// <summary>아카이브 디렉터리.</summary>
    string ArchiveDirectory { get; }

    /// <summary>임시 디렉터리.</summary>
    string TempDirectory { get; }

    /// <summary>로그 디렉터리.</summary>
    string LogDirectory { get; }
}

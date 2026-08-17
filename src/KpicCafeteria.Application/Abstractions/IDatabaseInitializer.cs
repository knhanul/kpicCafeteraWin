namespace KpicCafeteria.Application.Abstractions;

/// <summary>
/// 앱 시작 시 DB 초기화 (EF Migration 적용 + 기본 데이터 Seed).
/// </summary>
public interface IDatabaseInitializer
{
    /// <summary>
    /// SQLite DB에 마이그레이션을 적용하고 기본 데이터(중식/석식 배식유형)를 시드한다.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

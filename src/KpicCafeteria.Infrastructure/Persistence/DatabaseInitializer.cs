using KpicCafeteria.Application.Abstractions;
using KpicCafeteria.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.Persistence;

/// <summary>
/// 앱 시작 시 DB 초기화.
/// SQLite 운영 설정(WAL, busy timeout) 적용 → EF Migration 실행 → 기본 데이터 Seed.
/// </summary>
public sealed class DatabaseInitializer : IDatabaseInitializer
{
    private readonly IDbContextFactory<CafeteriaDbContext> _factory;

    public DatabaseInitializer(IDbContextFactory<CafeteriaDbContext> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);

        await ApplySqlitePragmasAsync(db, cancellationToken);
        await db.Database.MigrateAsync(cancellationToken);
        await SeedAsync(db, cancellationToken);
    }

    /// <summary>
    /// SQLite 운영 설정.
    /// - WAL 모드: 단일 프로세스 데스크톱 앱에 적합 (읽기/쓰기 동시성 향상)
    /// - busy_timeout: 파일 잠금 충돌 시 대기
    /// - Foreign Keys는 연결 문자열의 "Foreign Keys=True"로 활성화한다.
    /// </summary>
    private static async Task ApplySqlitePragmasAsync(CafeteriaDbContext db, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = "PRAGMA busy_timeout=5000;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 기본 데이터 Seed. 이미 존재하면 중복 생성하지 않는다.
    /// 사용자/admin 계정, 기본 메뉴/재료는 생성하지 않는다.
    /// </summary>
    public static async Task SeedAsync(CafeteriaDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.MealTypeSettings.AnyAsync(cancellationToken))
        {
            return;
        }

        db.MealTypeSettings.AddRange(
            new MealTypeSetting
            {
                Code = "LUNCH",
                Name = "중식",
                DefaultPlannedCount = 400,
                DefaultServiceTime = new TimeOnly(11, 40),
                SortOrder = 1,
                Active = true,
            },
            new MealTypeSetting
            {
                Code = "DINNER",
                Name = "석식",
                DefaultPlannedCount = 100,
                DefaultServiceTime = new TimeOnly(17, 30),
                SortOrder = 2,
                Active = true,
            });

        await db.SaveChangesAsync(cancellationToken);
    }
}

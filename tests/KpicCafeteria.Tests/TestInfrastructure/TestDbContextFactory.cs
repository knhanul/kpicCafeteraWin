using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Tests.TestInfrastructure;

/// <summary>
/// 테스트용 IDbContextFactory. 공유 SQLite 연결 위에 새 DbContext를 만든다.
/// </summary>
public sealed class TestDbContextFactory : IDbContextFactory<CafeteriaDbContext>
{
    private readonly SqliteConnection _connection;

    public TestDbContextFactory(SqliteConnection connection)
    {
        _connection = connection;
    }

    public CafeteriaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CafeteriaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CafeteriaDbContext(options);
    }

    public Task<CafeteriaDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(CreateDbContext());
}

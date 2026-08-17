using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Tests.TestInfrastructure;

/// <summary>
/// 실제 SQLite 엔진을 사용하는 테스트용 임시 DB.
/// EF Core InMemory Provider는 사용하지 않는다.
/// 연결을 유지한 채 여러 DbContext가 같은 DB를 공유한다.
/// </summary>
public sealed class SqliteTestDatabase : IDisposable
{
    public SqliteConnection Connection { get; }

    public SqliteTestDatabase()
    {
        Connection = new SqliteConnection("DataSource=:memory:;Foreign Keys=True");
        Connection.Open();

        using (var db = CreateContext())
        {
            db.Database.Migrate();
        }
    }

    public CafeteriaDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CafeteriaDbContext>()
            .UseSqlite(Connection)
            .Options;
        return new CafeteriaDbContext(options);
    }

    public void Dispose()
    {
        Connection.Dispose();
    }
}

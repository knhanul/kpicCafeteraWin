using KpicCafeteria.Application.Orders;
using KpicCafeteria.Infrastructure.Persistence;

namespace KpicCafeteria.Tests.TestInfrastructure;

/// <summary>
/// OrderService 테스트용 하네스.
/// 실제 SQLite 엔진(in-memory)을 사용하며, 서비스 작업마다 새 DbContext를 생성한다.
/// 중식/석식 배식유형 기본값을 시드한다.
/// </summary>
public sealed class OrderTestHarness : IDisposable
{
    private readonly SqliteTestDatabase _database;

    public OrderTestHarness()
    {
        _database = new SqliteTestDatabase();
        using var db = _database.CreateContext();
        DatabaseInitializer.SeedAsync(db).GetAwaiter().GetResult();
    }

    public OrderService CreateOrderService()
        => new(new TestOrderRepositoryFactory(_database.Connection));

    public CafeteriaDbContext CreateContext() => _database.CreateContext();

    public void Dispose() => _database.Dispose();
}

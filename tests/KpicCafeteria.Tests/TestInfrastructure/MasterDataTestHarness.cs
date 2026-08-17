using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Infrastructure.Persistence;

namespace KpicCafeteria.Tests.TestInfrastructure;

/// <summary>
/// MasterDataService 테스트용 하네스.
/// 실제 SQLite 엔진(in-memory)을 사용하며, 서비스 작업마다 새 DbContext를 생성한다.
/// </summary>
public sealed class MasterDataTestHarness : IDisposable
{
    private readonly SqliteTestDatabase _database;

    public MasterDataTestHarness()
    {
        _database = new SqliteTestDatabase();
    }

    public MasterDataService CreateService()
        => new(new TestMasterDataRepositoryFactory(_database.Connection));

    public CafeteriaDbContext CreateContext() => _database.CreateContext();

    public void Dispose() => _database.Dispose();
}

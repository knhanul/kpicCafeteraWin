using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Infrastructure.Persistence;
using KpicCafeteria.Infrastructure.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Tests.TestInfrastructure;

/// <summary>
/// 테스트용 문서 템플릿 리포지토리 팩토리.
/// 공유 SQLite 연결 위에 새 DbContext를 만들어 리포지토리를 생성한다.
/// </summary>
public sealed class TestDocumentTemplateRepositoryFactory : IDocumentTemplateRepositoryFactory
{
    private readonly SqliteConnection _connection;

    public TestDocumentTemplateRepositoryFactory(SqliteConnection connection)
    {
        _connection = connection;
    }

    public IDocumentTemplateRepository Create()
    {
        var options = new DbContextOptionsBuilder<CafeteriaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new DocumentTemplateRepository(new CafeteriaDbContext(options));
    }
}

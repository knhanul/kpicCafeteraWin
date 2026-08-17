using KpicCafeteria.Application.Documents;
using KpicCafeteria.Application.MasterData;
using KpicCafeteria.Application.Workspace;
using KpicCafeteria.Documents.Pdf;
using KpicCafeteria.Infrastructure.Persistence;

namespace KpicCafeteria.Tests.TestInfrastructure;

/// <summary>
/// 문서 출력 서비스 테스트용 하네스.
/// 실제 SQLite 엔진(in-memory) + 임시 템플릿 디렉터리를 사용한다.
/// </summary>
public sealed class DocumentTestHarness : IDisposable
{
    private readonly SqliteTestDatabase _database;
    private readonly TestAppDataPathProvider _paths;

    public DocumentTestHarness()
    {
        _database = new SqliteTestDatabase();
        _paths = new TestAppDataPathProvider();
        using var db = _database.CreateContext();
        DatabaseInitializer.SeedAsync(db).GetAwaiter().GetResult();
    }

    public DocumentTemplateService CreateTemplateService()
        => new(new TestDocumentTemplateRepositoryFactory(_database.Connection), _paths);

    public DocumentService CreateDocumentService()
        => new(
            new TestMealServiceRepositoryFactory(_database.Connection),
            new TestDocumentTemplateRepositoryFactory(_database.Connection),
            new FakePdfRenderer());

    public ExcelExportService CreateExcelExportService()
        => new(
            new TestMealServiceRepositoryFactory(_database.Connection),
            new TestMasterDataRepositoryFactory(_database.Connection),
            new TestOrderRepositoryFactory(_database.Connection),
            _paths);

    public WorkspaceService CreateWorkspaceService()
        => new(new TestMealServiceRepositoryFactory(_database.Connection));

    public MasterDataService CreateMasterDataService()
        => new(new TestMasterDataRepositoryFactory(_database.Connection));

    public CafeteriaDbContext CreateContext() => _database.CreateContext();

    public void Dispose()
    {
        _database.Dispose();
        _paths.Dispose();
    }
}

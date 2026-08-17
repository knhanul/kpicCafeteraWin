using System.IO.Compression;
using KpicCafeteria.Application.DataManagement;
using KpicCafeteria.Application.Documents;
using KpicCafeteria.Domain.Entities;
using KpicCafeteria.Domain.Enums;
using KpicCafeteria.Infrastructure.DataManagement;
using KpicCafeteria.Infrastructure.Persistence;
using KpicCafeteria.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Tests.DataManagement;

public sealed class DataManagementTests : IDisposable
{
    private readonly TestAppDataPathProvider _paths;
    private readonly SqliteConnection _connection;

    public DataManagementTests()
    {
        _paths = new TestAppDataPathProvider();
        _connection = new SqliteConnection($"Data Source=\"{_paths.DatabasePath}\"");
        _connection.Open();
        using var db = CreateDbContext();
        db.Database.Migrate();
    }

    public void Dispose()
    {
        _connection.Dispose();
        _paths.Dispose();
    }

    private CafeteriaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CafeteriaDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new CafeteriaDbContext(options);
    }

    private IDbContextFactory<CafeteriaDbContext> CreateFactory()
        => new TestDbContextFactory(_connection);

    [Fact]
    public void ExcelArchiveExporter_Build_ContainsNewSheets()
    {
        var service = new MealService
        {
            ServiceDate = new DateOnly(2026, 1, 1),
            MealType = MealType.LUNCH,
            PlannedCount = 100,
            Menus =
            [
                new MealServiceMenu
                {
                    MenuNameSnapshot = "A",
                    SortOrder = 1,
                    Ingredients =
                    [
                        new MealServiceMenuIngredient { IngredientNameSnapshot = "X", QuantityTotal = 10, SortOrder = 1 },
                    ],
                },
            ],
        };
        var orderItem = new OrderItem
        {
            ServiceDate = new DateOnly(2026, 1, 1),
            IngredientNameSnapshot = "X",
            OrderGroup = new OrderGroup { IngredientNameSnapshot = "X", OrderQuantity = 10 },
        };

        var content = KpicCafeteria.Documents.Excel.ExcelArchiveExporter.Build(
            [service],
            [],
            [],
            [],
            [],
            [orderItem],
            [orderItem.OrderGroup]);

        Assert.True(content.Length > 0);
        using var zip = new ZipArchive(new MemoryStream(content), ZipArchiveMode.Read);
        var workbook = zip.GetEntry("xl/workbook.xml")
            ?? throw new InvalidOperationException("xl/workbook.xml not found");
        using var reader = new StreamReader(workbook.Open());
        var xml = reader.ReadToEnd();
        Assert.Contains("식단재료", xml);
        Assert.Contains("발주기록", xml);
        Assert.Contains("발주그룹", xml);
    }

    [Fact]
    public async Task BackupService_CreateManualBackup_CreatesPackage()
    {
        var factory = CreateFactory();
        var backup = new BackupService(_paths, factory);

        var info = await backup.CreateManualBackupAsync();

        Assert.Equal("completed", info.Status);
        Assert.True(info.FileSize > 0);
        Assert.True(File.Exists(info.StoredPath));

        using var zip = ZipFile.OpenRead(info.StoredPath);
        Assert.NotNull(zip.GetEntry("manifest.json"));
        Assert.NotNull(zip.GetEntry("cafeteria.db"));
    }

    [Fact]
    public async Task RestoreService_Validate_CreatedBackup()
    {
        var factory = CreateFactory();
        var backup = new BackupService(_paths, factory);
        var restore = new RestoreService(_paths, factory, backup);

        var info = await backup.CreateManualBackupAsync();
        var manifest = await restore.ValidateAsync(info.StoredPath);

        Assert.Equal(1, manifest.BackupVersion);
        Assert.Equal("cafeteria.db", manifest.DatabaseFileName);
    }

    [Fact]
    public async Task XlsxCellParser_ConvertsValues()
    {
        Assert.Equal(1, XlsxCellParser.CleanInt(1.0));
        Assert.Equal(1.5, XlsxCellParser.CleanDouble("1.5"));
        Assert.True(XlsxCellParser.CleanBool("Y"));
        Assert.False(XlsxCellParser.CleanBool("N"));
        var d = XlsxCellParser.ParseDate(45000.0);
        Assert.True(d.HasValue);
        var t = XlsxCellParser.ParseTime("11:40");
        Assert.Equal(new TimeOnly(11, 40), t);
    }
}

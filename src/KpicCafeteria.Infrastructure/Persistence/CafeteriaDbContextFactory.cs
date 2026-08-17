using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KpicCafeteria.Infrastructure.Persistence;

/// <summary>
/// dotnet ef 마이그레이션 명령용 Design-time 팩토리.
/// 실제 앱은 Desktop의 DI 컨테이너에서 DbContext를 생성한다.
/// </summary>
public sealed class CafeteriaDbContextFactory : IDesignTimeDbContextFactory<CafeteriaDbContext>
{
    public CafeteriaDbContext CreateDbContext(string[] args)
    {
        var paths = new AppDataPathProvider();
        var options = new DbContextOptionsBuilder<CafeteriaDbContext>()
            .UseSqlite($"Data Source={paths.DatabasePath};Foreign Keys=True")
            .Options;
        return new CafeteriaDbContext(options);
    }
}

using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.Repositories;

/// <summary>
/// 작업 단위별 새 통계 리포지토리 생성.
/// IDbContextFactory로 새 DbContext를 만들어 리포지토리에 전달한다.
/// </summary>
public sealed class StatisticsRepositoryFactory : IStatisticsRepositoryFactory
{
    private readonly IDbContextFactory<CafeteriaDbContext> _factory;

    public StatisticsRepositoryFactory(IDbContextFactory<CafeteriaDbContext> factory)
    {
        _factory = factory;
    }

    public IStatisticsRepository Create()
        => new StatisticsRepository(_factory.CreateDbContext());
}

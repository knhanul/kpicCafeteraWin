using KpicCafeteria.Application.Abstractions.Repositories;
using KpicCafeteria.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace KpicCafeteria.Infrastructure.Repositories;

/// <summary>
/// 작업 단위별 새 발주 리포지토리 생성.
/// IDbContextFactory로 새 DbContext를 만들어 리포지토리에 전달한다.
/// </summary>
public sealed class OrderRepositoryFactory : IOrderRepositoryFactory
{
    private readonly IDbContextFactory<CafeteriaDbContext> _factory;

    public OrderRepositoryFactory(IDbContextFactory<CafeteriaDbContext> factory)
    {
        _factory = factory;
    }

    public IOrderRepository Create()
        => new OrderRepository(_factory.CreateDbContext());
}

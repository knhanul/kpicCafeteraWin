namespace KpicCafeteria.Application.Abstractions;

/// <summary>
/// 작업 단위. 여러 변경을 하나의 트랜잭션으로 묶는다.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

namespace KpicCafeteria.Application.Abstractions.Repositories;

/// <summary>
/// 작업 단위별 발주 리포지토리 생성.
/// Master Data / Workspace와 동일하게 작업 단위별 DbContext를 사용한다.
/// </summary>
public interface IOrderRepositoryFactory
{
    IOrderRepository Create();
}

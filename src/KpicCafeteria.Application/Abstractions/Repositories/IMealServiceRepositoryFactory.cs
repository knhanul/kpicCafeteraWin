namespace KpicCafeteria.Application.Abstractions.Repositories;

/// <summary>
/// 작업 단위별로 새 식단 리포지토리(및 DbContext)를 생성하는 팩토리.
/// DbContext가 장기간 살아남지 않도록 서비스는 작업마다 Create()로 새 인스턴스를 사용한다.
/// </summary>
public interface IMealServiceRepositoryFactory
{
    IMealServiceRepository Create();
}

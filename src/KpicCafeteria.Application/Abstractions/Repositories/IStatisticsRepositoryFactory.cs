namespace KpicCafeteria.Application.Abstractions.Repositories;

/// <summary>
/// 작업 단위별 새 통계 리포지토리 생성.
/// 통계는 읽기 전용이므로 조회 후 반드시 Dispose로 DbContext를 정리한다.
/// </summary>
public interface IStatisticsRepositoryFactory
{
    IStatisticsRepository Create();
}

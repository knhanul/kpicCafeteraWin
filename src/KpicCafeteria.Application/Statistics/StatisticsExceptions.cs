namespace KpicCafeteria.Application.Statistics;

/// <summary>통계 계산 중 발생하는 예외.</summary>
public class StatisticsException : Exception
{
    public StatisticsException(string message)
        : base(message)
    {
    }

    public StatisticsException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

/// <summary>통계 대상 데이터가 없는 경우.</summary>
public sealed class NoStatisticsDataException : StatisticsException
{
    public NoStatisticsDataException()
        : base("선택한 기간에 통계 데이터가 없습니다.")
    {
    }
}

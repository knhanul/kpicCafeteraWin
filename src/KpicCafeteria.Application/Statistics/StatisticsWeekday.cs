namespace KpicCafeteria.Application.Statistics;

/// <summary>
/// 요일 계산 헬퍼.
/// Python datetime.weekday()와 동일하게 월요일=0 ... 일요일=6을 사용한다.
/// (C# DayOfWeek는 일요일=0이므로 변환이 필요하다.)
/// </summary>
public static class StatisticsWeekday
{
    public static readonly string[] Names = ["월요일", "화요일", "수요일", "목요일", "금요일", "토요일", "일요일"];

    /// <summary>월요일=0 ... 일요일=6.</summary>
    public static int Index(DateOnly date) => ((int)date.DayOfWeek + 6) % 7;

    public static string Name(DateOnly date) => Names[Index(date)];
}

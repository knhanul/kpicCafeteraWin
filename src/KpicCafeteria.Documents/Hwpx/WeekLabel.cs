namespace KpicCafeteria.Documents.Hwpx;

/// <summary>
/// 주차/기간 Label 계산.
/// 기존 document_service.py _week_label/_period_label에 대응.
/// 규칙: 월 1일이 월~금이면 그 주가 1주, 토~일이면 다음 주가 1주.
/// </summary>
public static class WeekLabel
{
    public static readonly string[] WeekdayLabels = ["월요일", "화요일", "수요일", "목요일", "금요일", "토요일", "일요일"];

    /// <summary>"X월 Y주" 반환 (월요일 기준).</summary>
    public static string WeekLabelOf(DateOnly d)
    {
        var monday = d.AddDays(-(int)d.DayOfWeek + 1); // DayOfWeek: Sunday=0
        var firstOfMonth = new DateOnly(monday.Year, monday.Month, 1);
        var firstWeekday = (int)firstOfMonth.DayOfWeek; // 0=Sun ... 6=Sat
        DateOnly week1Monday;
        if (firstWeekday >= 1 && firstWeekday <= 5)
        {
            // 1일이 월~금 → 1일이 속한 주가 1주
            week1Monday = firstOfMonth.AddDays(-(firstWeekday - 1));
        }
        else
        {
            // 1일이 토~일 → 다음 월요일이 1주 시작
            var daysToMonday = firstWeekday == 0 ? 1 : 7 - firstWeekday + 1;
            week1Monday = firstOfMonth.AddDays(daysToMonday);
        }

        if (monday < week1Monday)
        {
            var prevMonthLast = firstOfMonth.AddDays(-1);
            return WeekLabelOf(prevMonthLast);
        }

        var weekNum = (monday.DayNumber - week1Monday.DayNumber) / 7 + 1;
        return $"{monday.Month}월 {weekNum}주";
    }

    /// <summary>"8월 3주" / "8월 3~4주" / "8월 3주~9월 1주" 형태.</summary>
    public static string PeriodLabel(DateOnly first, DateOnly last)
    {
        var left = WeekLabelOf(first);
        var right = WeekLabelOf(last);
        if (left == right)
        {
            return left;
        }

        var leftMonth = left.Split('월')[0];
        var rightMonth = right.Split('월')[0];
        if (leftMonth == rightMonth)
        {
            var leftWeek = left.Split(' ')[1];
            var rightWeek = right.Split(' ')[1];
            return $"{left.Split(' ')[0]} {leftWeek}~{rightWeek}";
        }

        return $"{left}~{right}";
    }
}

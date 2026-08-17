using System.Text.RegularExpressions;

namespace KpicCafeteria.Application.MasterData;

/// <summary>
/// 24시간제 시간 입력 정규화기.
/// 기존 프론트엔드 normalizeTime24/addMinutes 로직을 C#으로 이식한 순수 로직.
/// UI에 업무 로직을 직접 넣지 않기 위해 독립 클래스로 분리한다.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\static\app.js (normalizeTime24, addMinutes)
/// C:\Pjt\kpicCafeteria\backend\tests\test_time_input24.py
/// </summary>
public static class TimeInput24
{
    private static readonly Regex HmsPattern = new(@"^(\d{1,2}):(\d{1,2}):(\d{1,2})$", RegexOptions.Compiled);
    private static readonly Regex SeparatedPattern = new(@"^(\d{1,2})\D+(\d{1,2})$", RegexOptions.Compiled);

    /// <summary>
    /// 자유 입력을 "HH:MM"으로 정규화한다.
    /// 예: "1140"→"11:40", "930"→"09:30", "9"→"09:00", "11.40"→"11:40", "11:30:00"→"11:30"
    /// 빈 값 또는 유효하지 않은 값이면 null을 반환한다.
    /// </summary>
    public static string? Normalize(string? rawValue)
    {
        var trimmed = (rawValue ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        int hour;
        int minute;
        var hms = HmsPattern.Match(trimmed);
        if (hms.Success)
        {
            hour = int.Parse(hms.Groups[1].Value);
            minute = int.Parse(hms.Groups[2].Value);
            if (hour is >= 0 and <= 23 && minute is >= 0 and <= 59)
            {
                return $"{hour:00}:{minute:00}";
            }

            return null;
        }

        var separated = SeparatedPattern.Match(trimmed);
        if (separated.Success)
        {
            hour = int.Parse(separated.Groups[1].Value);
            minute = int.Parse(separated.Groups[2].Value);
        }
        else
        {
            var digits = Regex.Replace(trimmed, @"\D", string.Empty);
            if (digits.Length == 0 || digits.Length > 4)
            {
                return null;
            }

            if (digits.Length <= 2)
            {
                hour = int.Parse(digits);
                minute = 0;
            }
            else if (digits.Length == 3)
            {
                hour = int.Parse(digits[..1]);
                minute = int.Parse(digits[1..]);
            }
            else
            {
                hour = int.Parse(digits[..2]);
                minute = int.Parse(digits[2..4]);
            }
        }

        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            return null;
        }

        return $"{hour:00}:{minute:00}";
    }

    /// <summary>
    /// "HH:MM" 문자열에 분 단위 델타를 더한다 (24시간 순환).
    /// 유효하지 않은 입력이면 InvalidTimeFormatException을 던진다.
    /// </summary>
    public static string AddMinutes(string time, int delta)
    {
        var normalized = Normalize(time) ?? throw new InvalidTimeFormatException();
        var parts = normalized.Split(':');
        var hour = int.Parse(parts[0]);
        var minute = int.Parse(parts[1]);
        var total = (hour * 60 + minute + delta + 1440) % 1440;
        return $"{total / 60:00}:{total % 60:00}";
    }
}

using KpicCafeteria.Application.MasterData;

namespace KpicCafeteria.Tests;

/// <summary>
/// 24시간제 시간 입력 정규화 검증.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\tests\test_time_input24.py
/// C:\Pjt\kpicCafeteria\backend\app\static\app.js (normalizeTime24, addMinutes)
/// </summary>
public class TimeInput24Tests
{
    [Theory]
    [InlineData("1140", "11:40")]
    [InlineData("1730", "17:30")]
    [InlineData("930", "09:30")]
    [InlineData("9", "09:00")]
    [InlineData("09", "09:00")]
    [InlineData("11:40", "11:40")]
    [InlineData("11 40", "11:40")]
    [InlineData("11.40", "11:40")]
    [InlineData("9:5", "09:05")]
    [InlineData("00:00", "00:00")]
    [InlineData("23:59", "23:59")]
    [InlineData("11:30:00", "11:30")]
    [InlineData("17:30:00", "17:30")]
    public void Normalize_ValidInputs(string input, string expected)
    {
        Assert.Equal(expected, TimeInput24.Normalize(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("24:00")]
    [InlineData("11:60")]
    [InlineData("9999")]
    [InlineData("abcd")]
    [InlineData("12345")]
    [InlineData("24:00:00")]
    [InlineData(null)]
    public void Normalize_InvalidInputs_ReturnsNull(string? input)
    {
        Assert.Null(TimeInput24.Normalize(input));
    }

    [Theory]
    [InlineData("11:40", 5, "11:45")]
    [InlineData("11:40", -5, "11:35")]
    [InlineData("23:58", 5, "00:03")]
    [InlineData("00:02", -5, "23:57")]
    [InlineData("11:58", 5, "12:03")]
    [InlineData("00:03", -5, "23:58")]
    [InlineData("11:40", 0, "11:40")]
    [InlineData("12:00", 1440, "12:00")]
    public void AddMinutes_ValidInputs(string input, int delta, string expected)
    {
        Assert.Equal(expected, TimeInput24.AddMinutes(input, delta));
    }

    [Fact]
    public void AddMinutes_InvalidInput_Throws()
    {
        Assert.Throws<InvalidTimeFormatException>(() => TimeInput24.AddMinutes("25:00", 5));
    }
}

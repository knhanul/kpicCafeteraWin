using KpicCafeteria.Domain.Domain;

namespace KpicCafeteria.Tests;

/// <summary>
/// 100인 기준 ↔ 계획식수 기준 수량 환산 규칙 검증.
///
/// Reference:
/// C:\Pjt\kpicCafeteria\backend\app\routers\workspace.py
///   _copy_recipe_to_service_menu / update_service_menu_ingredients / save_meal_editor
/// </summary>
public class QuantityCalculatorTests
{
    [Fact]
    public void CalculateTotal_10kgPer100_Planned400_Returns40kg()
    {
        // 10kg / 100인, planned = 400 → total = 40kg
        Assert.Equal(40.0, QuantityCalculator.CalculateTotal(10.0, 400));
    }

    [Fact]
    public void CalculatePer100_Total40_Planned400_Returns10()
    {
        // total = 40, planned = 400 → per100 = 10
        Assert.Equal(10.0, QuantityCalculator.CalculatePer100(40.0, 400));
    }

    [Fact]
    public void CalculateTotal_NullPer100_ReturnsNull()
    {
        // 기존 코드: per_100이 None이면 total도 None
        Assert.Null(QuantityCalculator.CalculateTotal(null, 400));
    }

    [Fact]
    public void CalculatePer100_NullTotal_ReturnsNull()
    {
        Assert.Null(QuantityCalculator.CalculatePer100(null, 400));
    }

    [Fact]
    public void CalculatePer100_PlannedZero_ReturnsNull()
    {
        // 기존 코드: planned_count가 0이면 역산하지 않고 per_100을 null로 유지
        Assert.Null(QuantityCalculator.CalculatePer100(40.0, 0));
    }

    [Fact]
    public void CalculateTotal_PlannedZero_ReturnsZero()
    {
        // 정방향은 planned가 0이면 0이 된다 (기존 코드와 동일: 곱셈만 수행)
        Assert.Equal(0.0, QuantityCalculator.CalculateTotal(10.0, 0));
    }

    [Fact]
    public void CalculateTotal_Planned100_ReturnsPer100()
    {
        // planned = 100이면 total == per_100
        Assert.Equal(12.5, QuantityCalculator.CalculateTotal(12.5, 100));
    }

    [Theory]
    [InlineData(1.0, 1)]
    [InlineData(0.5, 250)]
    [InlineData(3.0, 333)]
    public void CalculateTotal_RoundTrip(double per100, int planned)
    {
        var total = QuantityCalculator.CalculateTotal(per100, planned);
        var roundTrip = QuantityCalculator.CalculatePer100(total, planned);
        Assert.NotNull(roundTrip);
        Assert.Equal(per100, roundTrip!.Value, precision: 10);
    }
}

using KpicCafeteria.Domain.Domain;

namespace KpicCafeteria.Tests;

/// <summary>
/// 추천 발주량 계산기 검증.
/// 판매 포장단위 기반 SuggestedOrderQuantity 계산과 단위 변환 규칙.
/// </summary>
public class OrderQuantityCalculatorTests
{
    // =======================================================================
    // 포장단위 (필수 테스트 49)
    // =======================================================================

    [Fact]
    public void Suggested_800g_2kgPackage_Returns2kg()
    {
        var result = OrderQuantityCalculator.CalculateSuggested(800, "g", 2, "kg");
        Assert.Equal(2.0, result);
    }

    [Fact]
    public void Suggested_4p1kg_2kgPackage_Returns6kg()
    {
        var result = OrderQuantityCalculator.CalculateSuggested(4.1, "kg", 2, "kg");
        Assert.Equal(6.0, result);
    }

    [Fact]
    public void Suggested_4kg_2kgPackage_Returns4kg()
    {
        var result = OrderQuantityCalculator.CalculateSuggested(4, "kg", 2, "kg");
        Assert.Equal(4.0, result);
    }

    // =======================================================================
    // 포장단위 없음 (필수 테스트 50)
    // =======================================================================

    [Fact]
    public void Suggested_NoPackage_ReturnsRequired()
    {
        var result = OrderQuantityCalculator.CalculateSuggested(5.2, "kg", null, null);
        Assert.Equal(5.2, result);
    }

    [Fact]
    public void Suggested_NoPackageUnit_ReturnsRequired()
    {
        var result = OrderQuantityCalculator.CalculateSuggested(3, "kg", 2, null);
        Assert.Equal(3.0, result);
    }

    // =======================================================================
    // 단위 변환 (필수 테스트 51)
    // =======================================================================

    [Fact]
    public void Suggested_1500ml_1LPackage_Returns2L()
    {
        var result = OrderQuantityCalculator.CalculateSuggested(1500, "ml", 1, "L");
        Assert.Equal(2.0, result);
    }

    [Fact]
    public void Suggested_IncompatibleUnits_ReturnsNull()
    {
        // 2개 + 1kg → 자동 추천 불가 (임의 환산하지 않는다)
        var result = OrderQuantityCalculator.CalculateSuggested(2, "개", 1, "kg");
        Assert.Null(result);
    }

    [Fact]
    public void Suggested_BoxToKg_ReturnsNull()
    {
        var result = OrderQuantityCalculator.CalculateSuggested(1, "박스", 10, "kg");
        Assert.Null(result);
    }

    [Fact]
    public void ConversionFactor_GramToKg_IsCorrect()
    {
        Assert.Equal(0.001, OrderQuantityCalculator.TryGetConversionFactor("g", "kg"));
        Assert.Equal(1000.0, OrderQuantityCalculator.TryGetConversionFactor("kg", "g"));
    }

    [Fact]
    public void ConversionFactor_MlToL_IsCorrect()
    {
        Assert.Equal(0.001, OrderQuantityCalculator.TryGetConversionFactor("ml", "L"));
        Assert.Equal(1000.0, OrderQuantityCalculator.TryGetConversionFactor("L", "ml"));
    }

    [Fact]
    public void ConversionFactor_SameUnit_IsOne()
    {
        Assert.Equal(1.0, OrderQuantityCalculator.TryGetConversionFactor("kg", "kg"));
        Assert.Equal(1.0, OrderQuantityCalculator.TryGetConversionFactor("KG", "kg"));
    }

    [Fact]
    public void ConversionFactor_UnknownUnits_IsNull()
    {
        Assert.Null(OrderQuantityCalculator.TryGetConversionFactor("개", "kg"));
        Assert.Null(OrderQuantityCalculator.TryGetConversionFactor("봉", "g"));
        Assert.Null(OrderQuantityCalculator.TryGetConversionFactor("통", "L"));
    }

    [Fact]
    public void AreUnitsCompatible_OnlyKnownPairs()
    {
        Assert.True(OrderQuantityCalculator.AreUnitsCompatible("g", "kg"));
        Assert.True(OrderQuantityCalculator.AreUnitsCompatible("ml", "l"));
        Assert.False(OrderQuantityCalculator.AreUnitsCompatible("개", "kg"));
        Assert.False(OrderQuantityCalculator.AreUnitsCompatible("팩", "g"));
    }

    // =======================================================================
    // 추천 단위
    // =======================================================================

    [Fact]
    public void SuggestedUnit_WithPackage_ReturnsPackageUnit()
    {
        Assert.Equal("kg", OrderQuantityCalculator.SuggestedUnit("g", "kg"));
    }

    [Fact]
    public void SuggestedUnit_WithoutPackage_ReturnsRequiredUnit()
    {
        Assert.Equal("kg", OrderQuantityCalculator.SuggestedUnit("kg", null));
    }

    [Fact]
    public void SuggestedUnit_Incompatible_ReturnsNull()
    {
        Assert.Null(OrderQuantityCalculator.SuggestedUnit("개", "kg"));
    }

    // =======================================================================
    // 경계값
    // =======================================================================

    [Fact]
    public void Suggested_ZeroRequired_ReturnsNull()
    {
        Assert.Null(OrderQuantityCalculator.CalculateSuggested(0, "kg", 2, "kg"));
        Assert.Null(OrderQuantityCalculator.CalculateSuggested(null, "kg", 2, "kg"));
    }

    [Fact]
    public void Suggested_ExactMultiple_ReturnsExact()
    {
        // 6kg 필요 / 2kg 포장 → 6kg
        Assert.Equal(6.0, OrderQuantityCalculator.CalculateSuggested(6, "kg", 2, "kg"));
    }
}

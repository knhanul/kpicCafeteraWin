namespace KpicCafeteria.Domain.Domain;

/// <summary>
/// 발주 추천량 계산기.
/// 판매 포장단위를 고려한 SuggestedOrderQuantity를 계산한다.
/// 재고 계산 로직은 포함하지 않는다.
/// </summary>
public static class OrderQuantityCalculator
{
    /// <summary>
    /// 단위 변환 계수. from 단위 값을 to 단위 값으로 바꾸는 배율을 반환한다.
    /// 변환 계수가 명확하지 않은 단위(개/봉/팩/박스/통 등)는 null을 반환하며 임의 환산하지 않는다.
    /// </summary>
    public static double? TryGetConversionFactor(string? fromUnit, string? toUnit)
    {
        var from = Normalize(fromUnit);
        var to = Normalize(toUnit);
        if (from is null || to is null)
        {
            return null;
        }

        if (from == to)
        {
            return 1.0;
        }

        return (from, to) switch
        {
            ("g", "kg") => 0.001,
            ("kg", "g") => 1000.0,
            ("ml", "l") => 0.001,
            ("l", "ml") => 1000.0,
            _ => null,
        };
    }

    /// <summary>두 단위가 변환 가능한지 여부.</summary>
    public static bool AreUnitsCompatible(string? a, string? b)
        => TryGetConversionFactor(a, b) is not null;

    /// <summary>
    /// 추천 발주량 계산.
    /// - 판매 포장 정보가 없으면 필요량 그대로 반환.
    /// - 필요량과 판매단위를 동일 단위로 변환 후 ceil(필요량/포장수량) × 포장수량.
    /// - 단위 호환이 불가능하면 null (포장단위 확인 필요, 임의 환산하지 않음).
    /// </summary>
    public static double? CalculateSuggested(
        double? requiredQuantity,
        string? requiredUnit,
        double? packageQuantity,
        string? packageUnit)
    {
        if (requiredQuantity is null or <= 0)
        {
            return null;
        }

        // 판매 포장 정보가 없는 재료: 필요량을 그대로 추천값으로 사용 (자유 발주 대상).
        if (packageQuantity is null or <= 0 || string.IsNullOrWhiteSpace(packageUnit))
        {
            return requiredQuantity;
        }

        var factor = TryGetConversionFactor(requiredUnit, packageUnit);
        if (factor is null)
        {
            return null;
        }

        var requiredInPackageUnit = requiredQuantity.Value * factor.Value;
        var packages = Math.Ceiling(requiredInPackageUnit / packageQuantity.Value);
        return packages * packageQuantity.Value;
    }

    /// <summary>
    /// 추천 발주량의 단위.
    /// 판매 포장단위가 있으면 포장단위, 없으면 필요량 단위, 호환 불가면 null.
    /// </summary>
    public static string? SuggestedUnit(string? requiredUnit, string? packageUnit)
    {
        if (string.IsNullOrWhiteSpace(packageUnit))
        {
            return requiredUnit;
        }

        if (requiredUnit is not null && !AreUnitsCompatible(requiredUnit, packageUnit))
        {
            return null;
        }

        return packageUnit;
    }

    private static string? Normalize(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
        {
            return null;
        }

        return unit.Trim().ToLowerInvariant();
    }
}
